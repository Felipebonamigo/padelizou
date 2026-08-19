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

    private static Aula Realizada(Jogador professor, LocalAula local, DateTime quando, bool alunoPagaQuadra = false) => new()
    {
        ProfessorId = professor.Id, LocalAulaId = local.Id, NomeAlunoAvulso = "Medina",
        DataHora = quando, Preco = 110, Status = PoliticaAula.Realizada, AlunoPagaQuadra = alunoPagaQuadra,
    };

    private static async Task<FinanceiroProfessorVM> AbrirFinanceiroAsync(DbPadelContext ctx, int professorId)
    {
        var resultado = await TestInfra.NovoAulasController(ctx, professorId).Financeiro("mes");
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
            dataHora: DateTime.Today.AddDays(2).AddHours(7), preco: null,
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
            dataHora: DateTime.Today.AddDays(2).AddHours(7), preco: null,
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

    [Fact]
    public async Task Cada_aula_cai_na_semana_em_que_aconteceu()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var hoje = DateTime.Today;
        ctx.Aulas.Add(Realizada(professor, local, hoje.AddHours(7)));
        ctx.Aulas.Add(Realizada(professor, local, hoje.AddDays(-7).AddHours(7)));
        await ctx.SaveChangesAsync();

        var vm = await AbrirFinanceiroAsync(ctx, professor.Id);

        Assert.Equal(6, vm.UltimasSemanas.Count);
        Assert.Equal(110, vm.UltimasSemanas[5].Valor);  // a última linha é a semana atual
        Assert.Equal(110, vm.UltimasSemanas[4].Valor);
        Assert.All(vm.UltimasSemanas.Take(4), s => Assert.Equal(0, s.Valor));
    }

    [Fact]
    public async Task A_semana_comeca_na_segunda_e_termina_no_domingo()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var vm = await AbrirFinanceiroAsync(ctx, professor.Id);

        Assert.All(vm.UltimasSemanas, s => Assert.Equal(DayOfWeek.Monday, s.Inicio.DayOfWeek));
        Assert.All(vm.UltimasSemanas, s => Assert.Equal(DayOfWeek.Sunday, s.Fim.DayOfWeek));
        // A semana atual é a última barra: hoje está dentro dela.
        var atual = vm.UltimasSemanas[5];
        Assert.InRange(DateTime.Today, atual.Inicio, atual.Fim);
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
