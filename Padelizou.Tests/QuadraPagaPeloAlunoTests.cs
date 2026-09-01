using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using Xunit;

namespace Padelizou.Tests;

// Às vezes o aluno acerta o aluguel da quadra direto com o clube: a aula nasce marcada e o
// custo do local não conta como despesa do professor naquela aula. E a visão semanal do
// Financeiro: cada aula realizada cai na semana (segunda a domingo) em que aconteceu.
public class QuadraPagaPeloAlunoTests
{
    private static (DbPadelContext ctx, Jogador professor, LocalAula local) Montar(decimal? custoPorAula = 50)
    {
        var ctx = TestInfra.NovoContexto();

        var professor = new Jogador { Nome = "Jonatas", Login = "jonatas", Cpf = "99900000001", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();

        var local = new LocalAula
        {
            ProfessorId = professor.Id, Nome = "Batata Padel",
            PrecoPadrao = 110, Ativo = true, CustoPorAula = custoPorAula,
        };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();

        return (ctx, professor, local);
    }

    // `PagaEm` preenchido de propósito: desde 25/08/2026 "Recebido" é o dinheiro que ENTROU, e
    // não a aula que aconteceu (ver Services/RecebimentoDaAula). Sem isto, estes testes — que
    // são sobre CUSTO de quadra — mediriam a régua de recebimento em vez da de custo, e o
    // líquido sairia negativo por um motivo que não tem nada a ver com o que eles perguntam.
    private static Aula Realizada(Jogador professor, LocalAula local, DateTime quando, bool alunoPagaQuadra = false) => new()
    {
        ProfessorId = professor.Id, LocalAulaId = local.Id, NomeAlunoAvulso = "Medina",
        DataHora = quando, Preco = 110, Status = PoliticaAula.Realizada, AlunoPagaQuadra = alunoPagaQuadra,
        PagaEm = quando.AddHours(1),
    };

    private static async Task<FinanceiroProfessorVM> AbrirFinanceiroAsync(
        DbPadelContext ctx, int professorId, string? semanas = null)
    {
        var resultado = await TestInfra.NovoAulasController(ctx, professorId).Financeiro("mes", semanas);
        return (FinanceiroProfessorVM)Assert.IsType<ViewResult>(resultado).Model!;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_aula_guarda_quem_paga_a_quadra(bool alunoPaga)
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        await TestInfra.NovoAulasController(ctx, professor.Id).AdicionarManual(
            localId: local.Id, nomeAluno: "Medina", telefoneAluno: null,
            data: DataEHoraDoFormulario.ParaCampoDeData(DateTime.Today.AddDays(2).AddHours(7)), hora: DataEHoraDoFormulario.ParaCampoDeHora(DateTime.Today.AddDays(2).AddHours(7)), preco: null,
            recorrente: false, semanasRecorrencia: 0, alunoPagaQuadra: alunoPaga);

        Assert.Equal(alunoPaga, (await ctx.Aulas.SingleAsync()).AlunoPagaQuadra);
    }

    [Fact]
    public async Task A_serie_recorrente_inteira_carrega_a_escolha()
    {
        // A aula fixa de toda semana é justamente o caso mais comum de acerto direto com o
        // clube — valer só na primeira faria as demais voltarem a contar custo.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        await TestInfra.NovoAulasController(ctx, professor.Id).AdicionarManual(
            localId: local.Id, nomeAluno: "Medina", telefoneAluno: null,
            data: DataEHoraDoFormulario.ParaCampoDeData(DateTime.Today.AddDays(2).AddHours(7)), hora: DataEHoraDoFormulario.ParaCampoDeHora(DateTime.Today.AddDays(2).AddHours(7)), preco: null,
            recorrente: true, semanasRecorrencia: 4, alunoPagaQuadra: true);

        var aulas = await ctx.Aulas.ToListAsync();
        Assert.Equal(4, aulas.Count);
        Assert.All(aulas, a => Assert.True(a.AlunoPagaQuadra));
    }

    [Fact]
    public async Task Quadra_paga_pelo_aluno_nao_entra_no_custo_do_professor()
    {
        var (ctx, professor, local) = Montar(custoPorAula: 50);
        using var _ = ctx;

        var hoje = DateTime.Today.AddHours(7);
        ctx.Aulas.Add(Realizada(professor, local, hoje));
        ctx.Aulas.Add(Realizada(professor, local, hoje.AddHours(1), alunoPagaQuadra: true));
        await ctx.SaveChangesAsync();

        var vm = await AbrirFinanceiroAsync(ctx, professor.Id);

        var porLocal = Assert.Single(vm.PorLocal);
        Assert.Equal(2, porLocal.Aulas);
        Assert.Equal(50, porLocal.Custo);           // só a aula em que o PROFESSOR paga
        Assert.Equal(220 - 50, porLocal.Liquido);
    }

    [Fact]
    public async Task Local_sem_custo_cadastrado_segue_sem_custo()
    {
        // A flag não pode inventar um custo zero onde nunca houve custo informado — o
        // Líquido desse local continua saindo como "—".
        var (ctx, professor, local) = Montar(custoPorAula: null);
        using var _ = ctx;

        ctx.Aulas.Add(Realizada(professor, local, DateTime.Today.AddHours(7), alunoPagaQuadra: true));
        await ctx.SaveChangesAsync();

        var vm = await AbrirFinanceiroAsync(ctx, professor.Id);

        Assert.Null(Assert.Single(vm.PorLocal).Custo);
    }

    // ⚠️ Desde 01/09/2026 as barras são as semanas DE UM MÊS, e não mais uma janela rolante de
    // 6 semanas (pedido do Felipe; ver Services/SemanasDoMes). O que este teste sempre disse
    // continua valendo, e é o que ele checa: cada aula cai na SEMANA em que aconteceu.
    //
    // O cenário é ancorado num mês FIXO, e não em "hoje": duas correções no dia 01/09/2026
    // saíram de testes que semeavam em "ontem" e liam o mês corrente.
    [Fact]
    public async Task Cada_aula_cai_na_semana_em_que_aconteceu()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        // 12/08 e 19/08 de 2026 são quartas de semanas diferentes.
        ctx.Aulas.Add(Realizada(professor, local, new DateTime(2026, 8, 12, 7, 0, 0)));
        ctx.Aulas.Add(Realizada(professor, local, new DateTime(2026, 8, 19, 7, 0, 0)));
        await ctx.SaveChangesAsync();

        var vm = await AbrirFinanceiroAsync(ctx, professor.Id, "2026-08");

        // Agosto/2026: [01–02] [03–09] [10–16] [17–23] [24–30] [31–31].
        Assert.Equal(110, vm.Semanas[2].Valor);
        Assert.Equal(110, vm.Semanas[3].Valor);
        Assert.Equal(220, vm.Semanas.Sum(s => s.Valor));
    }

    // A semana segue de segunda a domingo NO MEIO do mês; nas pontas ela é recortada, e é esse
    // recorte que faz a soma das barras ser o faturamento do mês (Services/SemanasDoMes).
    [Fact]
    public async Task A_semana_comeca_na_segunda_e_termina_no_domingo_no_meio_do_mes()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var vm = await AbrirFinanceiroAsync(ctx, professor.Id, "2026-08");

        foreach (var s in vm.Semanas.Skip(1).SkipLast(1))
        {
            Assert.Equal(DayOfWeek.Monday, s.Inicio.DayOfWeek);
            Assert.Equal(DayOfWeek.Sunday, s.Fim.DayOfWeek);
        }

        // E o mês abre e fecha nas pontas dele, não em domingo nenhum.
        Assert.Equal(new DateTime(2026, 8, 1), vm.Semanas.First().Inicio);
        Assert.Equal(new DateTime(2026, 8, 31), vm.Semanas.Last().Fim);
    }

    // Sem parâmetro o card abre no mês CORRENTE, e a semana de hoje está entre as barras — é o
    // que a última linha da janela rolante garantia, e continua garantido.
    [Fact]
    public async Task Sem_escolher_mes_o_card_mostra_a_semana_de_hoje()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var vm = await AbrirFinanceiroAsync(ctx, professor.Id);

        Assert.Contains(vm.Semanas, s => DateTime.Today >= s.Inicio && DateTime.Today <= s.Fim);
    }

    [Fact]
    public async Task Cada_aula_cai_no_ano_em_que_aconteceu()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var hoje = DateTime.Today;
        ctx.Aulas.Add(Realizada(professor, local, hoje.AddHours(7)));
        ctx.Aulas.Add(Realizada(professor, local, hoje.AddYears(-1).AddHours(7)));
        await ctx.SaveChangesAsync();

        var vm = await AbrirFinanceiroAsync(ctx, professor.Id);

        Assert.Equal(6, vm.UltimosAnos.Count);
        Assert.Equal(110, vm.UltimosAnos[5].Valor);  // a última linha é o ano atual
        Assert.Equal(110, vm.UltimosAnos[4].Valor);
        Assert.All(vm.UltimosAnos.Take(4), a => Assert.Equal(0, a.Valor));
    }

    [Fact]
    public async Task Os_ultimos_6_anos_terminam_no_ano_atual_e_sao_consecutivos()
    {
        var (ctx, professor, _) = Montar();
        using var _ = ctx;

        var vm = await AbrirFinanceiroAsync(ctx, professor.Id);

        Assert.Equal(6, vm.UltimosAnos.Count);
        Assert.Equal(DateTime.Today.Year, vm.UltimosAnos[5].Ano);
        for (int i = 1; i < vm.UltimosAnos.Count; i++)
        {
            Assert.Equal(vm.UltimosAnos[i - 1].Ano + 1, vm.UltimosAnos[i].Ano);
        }
    }
}
