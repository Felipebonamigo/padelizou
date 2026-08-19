using Microsoft.EntityFrameworkCore;
using padelizou.Controllers;   // AulasController ficou no namespace legado, em minúsculo
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using Xunit;

namespace Padelizou.Tests;

// Fechar o mês: as aulas de um mês viram uma conta por aluno, que vence no mês seguinte
// (ver Services/FechamentoDoMes). É o modelo do professor mensalista.
//
// "Quais aulas entram nesta conta" tem três respostas erradas fáceis, e cada uma custa
// dinheiro de alguém: contar a cancelada cobra o que não foi combinado, esquecer a falta
// cobrável perde o dinheiro do mensalista, e contar a reposição cobra duas vezes a MESMA aula.
public class FechamentoDoMesTests
{
    private static Aula Aula(string status, decimal preco = 110, bool cobrarMesmoFaltando = false,
        int? recuperaAulaId = null, DateTime? quando = null) =>
        new()
        {
            ProfessorId = 1, NomeAlunoAvulso = "Medina", LocalAulaId = 1,
            DataHora = quando ?? new DateTime(2026, 4, 10, 9, 0, 0),
            Preco = preco, Status = status, CobrarMesmoFaltando = cobrarMesmoFaltando,
            RecuperaAulaId = recuperaAulaId,
        };

    // ─── O que entra na conta ─────────────────────────────────────────────────────────

    [Fact]
    public void A_aula_dada_entra()
    {
        Assert.True(FechamentoDoMes.EntraNaConta(Aula(PoliticaAula.Realizada)));
    }

    [Fact]
    public void A_falta_cobravel_e_o_vai_recuperar_entram()
    {
        // Quem paga o mês não desconta a falta. Deixar de fora seria devolver dinheiro que
        // ninguém pediu — e é o motivo de o Rafael cobrar por mês.
        Assert.True(FechamentoDoMes.EntraNaConta(Aula(PoliticaAula.Faltou, cobrarMesmoFaltando: true)));
        Assert.True(FechamentoDoMes.EntraNaConta(Aula(PoliticaAula.ARecuperar, cobrarMesmoFaltando: true)));
    }

    [Theory]
    [InlineData(PoliticaAula.Cancelada)]
    [InlineData(PoliticaAula.Recusada)]
    [InlineData(PoliticaAula.Pendente)]
    [InlineData(PoliticaAula.Confirmada)]
    public void O_que_nao_aconteceu_e_nao_e_cobravel_fica_de_fora(string status)
    {
        Assert.False(FechamentoDoMes.EntraNaConta(Aula(status)));
    }

    [Fact]
    public void A_falta_NAO_cobravel_fica_de_fora()
    {
        // O professor decidiu não cobrar aquela falta. Cobrar assim mesmo seria desfazer a
        // decisão dele um mês depois, quando ninguém está olhando.
        Assert.False(FechamentoDoMes.EntraNaConta(Aula(PoliticaAula.Faltou, cobrarMesmoFaltando: false)));
    }

    [Fact]
    public void A_REPOSICAO_nao_entra_em_conta_nenhuma()
    {
        // O erro mais caro possível desta tela. A reposição aconteceu, mas o dinheiro dela já
        // entrou na conta do mês da aula original — foi por isso que ela nasceu sem preço.
        // Contá-la aqui cobraria a mesma aula duas vezes, em dois meses diferentes.
        var reposicao = Aula(PoliticaAula.Realizada, preco: 0, recuperaAulaId: 42);
        Assert.False(FechamentoDoMes.EntraNaConta(reposicao));
    }

    // ─── Quando vence ─────────────────────────────────────────────────────────────────

    [Fact]
    public void A_conta_vence_no_mes_SEGUINTE_ao_das_aulas()
    {
        // "Fecha as aulas do mês e cobra no mês subsequente", nas palavras do Rafael.
        Assert.Equal(new DateTime(2026, 5, 10), FechamentoDoMes.Vencimento(2026, 4, 10));
        Assert.Equal(new DateTime(2027, 1, 5), FechamentoDoMes.Vencimento(2026, 12, 5));
    }

    [Fact]
    public void Dia_que_nao_existe_no_mes_cai_no_ultimo()
    {
        // Fevereiro não tem 31. Pedir 31 devolve o último dia em vez de estourar na cara do
        // professor no meio do fechamento.
        Assert.Equal(new DateTime(2026, 2, 28), FechamentoDoMes.Vencimento(2026, 1, 31));
    }

    [Fact]
    public void O_mes_so_fecha_depois_de_acabar()
    {
        // Fechar março no dia 12 de março geraria conta pela metade, e o professor só
        // descobriria quando o aluno reclamasse do valor.
        var hoje = new DateTime(2026, 4, 15);
        Assert.True(FechamentoDoMes.PodeFechar(2026, 3, hoje));
        Assert.False(FechamentoDoMes.PodeFechar(2026, 4, hoje));
        Assert.False(FechamentoDoMes.PodeFechar(2026, 5, hoje));
    }

    // ─── De ponta a ponta, com banco ──────────────────────────────────────────────────

    private static (DbPadelContext ctx, Jogador professor, LocalAula local) Montar()
    {
        var ctx = TestInfra.NovoContexto();

        var professor = new Jogador { Nome = "Joao", Login = "joaof", Cpf = "99900000031", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();

        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Arena", PrecoPadrao = 110, Ativo = true };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();

        return (ctx, professor, local);
    }

    private static readonly DateTime MesPassado =
        new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);

    private static Aula Lancar(DbPadelContext ctx, Jogador professor, LocalAula local, string nome,
        decimal preco, string status = PoliticaAula.Realizada, bool cobravel = false, int dia = 10)
    {
        var aula = new Aula
        {
            ProfessorId = professor.Id, NomeAlunoAvulso = nome, LocalAulaId = local.Id,
            DataHora = MesPassado.AddDays(dia - 1).AddHours(9),
            Preco = preco, Status = status, CobrarMesmoFaltando = cobravel, QuantidadeAlunos = 1,
        };
        ctx.Aulas.Add(aula);
        ctx.SaveChanges();
        return aula;
    }

    [Fact]
    public async Task Fechar_o_mes_cria_uma_conta_por_aluno()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        Lancar(ctx, professor, local, "Medina", 110, dia: 3);
        Lancar(ctx, professor, local, "Medina", 110, dia: 10);
        Lancar(ctx, professor, local, "Duda", 150, dia: 4);

        await TestInfra.NovoAulasController(ctx, professor.Id)
            .FecharMes(MesPassado.Year, MesPassado.Month, diaVencimento: 10);

        var contas = await ctx.FaturasDeAlunos.OrderByDescending(f => f.Valor).ToListAsync();
        Assert.Equal(2, contas.Count);
        Assert.Equal(220, contas[0].Valor);
        Assert.Equal(2, contas[0].QuantidadeAulas);
        Assert.Equal(150, contas[1].Valor);
        Assert.All(contas, c => Assert.Equal(FechamentoDoMes.Aberta, c.Status));
    }

    [Fact]
    public async Task Fechar_duas_vezes_nao_cobra_o_mesmo_mes_duas_vezes()
    {
        // O professor clica de novo com frequência: a tela continua mostrando o mês depois
        // de fechado.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        Lancar(ctx, professor, local, "Medina", 110);
        var controller = TestInfra.NovoAulasController(ctx, professor.Id);

        await controller.FecharMes(MesPassado.Year, MesPassado.Month, 10);
        await controller.FecharMes(MesPassado.Year, MesPassado.Month, 10);

        Assert.Equal(1, await ctx.FaturasDeAlunos.CountAsync());
    }

    [Fact]
    public async Task O_responsavel_e_quem_aparece_na_conta()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        Lancar(ctx, professor, local, "Pedrinho", 110);
        ctx.CadastrosDeAlunos.Add(new CadastroDoAluno
        {
            ProfessorId = professor.Id, NomeAvulso = "Pedrinho",
            ResponsavelNome = "Marcia", ResponsavelCelular = "51988881111", ResponsavelCpf = "52998224725",
        });
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, professor.Id)
            .FecharMes(MesPassado.Year, MesPassado.Month, 10);

        var conta = await ctx.FaturasDeAlunos.SingleAsync();
        Assert.Equal("Marcia", conta.PagadorNome);
        Assert.Equal("52998224725", conta.PagadorCpf);
        Assert.Equal("51988881111", conta.PagadorCelular);
    }

    [Fact]
    public async Task Mudar_o_responsavel_depois_NAO_reescreve_a_conta_ja_fechada()
    {
        // A fatura é um documento. Trocar o responsável em junho não pode reescrever a conta
        // de abril que já foi mandada pra outra pessoa.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        Lancar(ctx, professor, local, "Pedrinho", 110);
        var cadastro = new CadastroDoAluno
        {
            ProfessorId = professor.Id, NomeAvulso = "Pedrinho", ResponsavelNome = "Marcia",
        };
        ctx.CadastrosDeAlunos.Add(cadastro);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, professor.Id)
            .FecharMes(MesPassado.Year, MesPassado.Month, 10);

        cadastro.ResponsavelNome = "Outro pagador";
        await ctx.SaveChangesAsync();

        Assert.Equal("Marcia", (await ctx.FaturasDeAlunos.SingleAsync()).PagadorNome);
    }

    [Fact]
    public async Task A_aula_que_o_aluno_vai_recuperar_entra_na_conta_do_mes_dela()
    {
        // O caso inteiro do mensalista, de ponta a ponta: ele faltou, foi cobrado, e vai
        // repor. A conta do mês tem que ter as duas aulas.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        Lancar(ctx, professor, local, "Medina", 110, dia: 3);
        var faltou = Lancar(ctx, professor, local, "Medina", 110, PoliticaAula.Confirmada, dia: 10);

        await TestInfra.NovoAulasController(ctx, professor.Id).MarcarParaRecuperar(faltou.Id);
        await TestInfra.NovoAulasController(ctx, professor.Id)
            .FecharMes(MesPassado.Year, MesPassado.Month, 10);

        var conta = await ctx.FaturasDeAlunos.SingleAsync();
        Assert.Equal(220, conta.Valor);
        Assert.Equal(2, conta.QuantidadeAulas);
    }

    [Fact]
    public async Task A_reposicao_do_mes_seguinte_NAO_cobra_de_novo()
    {
        // O risco de cobrar duas vezes atravessa a virada do mês: a aula original entrou na
        // conta de abril, a reposição acontece em maio. Se ela entrasse na conta de maio, o
        // aluno pagaria a mesma aula em dois meses.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var faltou = Lancar(ctx, professor, local, "Medina", 110, PoliticaAula.Confirmada, dia: 10);
        await TestInfra.NovoAulasController(ctx, professor.Id).MarcarParaRecuperar(faltou.Id);

        // A reposição cai no mês seguinte ao das aulas — o mês corrente.
        var quandoRepor = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddDays(4).AddHours(9);
        await TestInfra.NovoAulasController(ctx, professor.Id).Encaixar(faltou.Id, local.Id, quandoRepor);

        var reposicao = await ctx.Aulas.SingleAsync(a => a.RecuperaAulaId != null);
        reposicao.Status = PoliticaAula.Realizada;
        await ctx.SaveChangesAsync();

        var vista = await TestInfra.NovoAulasController(ctx, professor.Id)
            .Faturamento(quandoRepor.Year, quandoRepor.Month);
        var vm = Assert.IsType<FaturamentoVM>(Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(vista).Model);

        Assert.Empty(vm.Previas);
    }

    [Fact]
    public async Task Marcar_recebido_e_desfazer()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        Lancar(ctx, professor, local, "Medina", 110);
        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.FecharMes(MesPassado.Year, MesPassado.Month, 10);

        var conta = await ctx.FaturasDeAlunos.SingleAsync();
        await controller.MarcarFaturaPaga(conta.Id);
        Assert.Equal(FechamentoDoMes.Paga, (await ctx.FaturasDeAlunos.SingleAsync()).Status);

        await controller.ReabrirFatura(conta.Id);
        var depois = await ctx.FaturasDeAlunos.SingleAsync();
        Assert.Equal(FechamentoDoMes.Aberta, depois.Status);
        Assert.Null(depois.PagaEm);
    }

    [Fact]
    public async Task Cancelar_a_conta_libera_o_mes_pra_fechar_de_novo()
    {
        // É o conserto de quem fechou com o preço errado: cancela, arruma a aula, fecha de novo.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        Lancar(ctx, professor, local, "Medina", 110);
        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.FecharMes(MesPassado.Year, MesPassado.Month, 10);

        var conta = await ctx.FaturasDeAlunos.SingleAsync();
        await controller.CancelarFatura(conta.Id);
        await controller.FecharMes(MesPassado.Year, MesPassado.Month, 10);

        Assert.Equal(1, await ctx.FaturasDeAlunos.CountAsync(f => f.Status == FechamentoDoMes.Aberta));
        Assert.Equal(2, await ctx.FaturasDeAlunos.CountAsync());
    }

    [Fact]
    public async Task Professor_nao_da_baixa_na_conta_de_outro_professor()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var intruso = new Jogador { Nome = "Intruso", Login = "intruso2", Cpf = "99900000032", IsProfessor = true };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        Lancar(ctx, professor, local, "Medina", 110);
        await TestInfra.NovoAulasController(ctx, professor.Id)
            .FecharMes(MesPassado.Year, MesPassado.Month, 10);

        var conta = await ctx.FaturasDeAlunos.SingleAsync();
        await TestInfra.NovoAulasController(ctx, intruso.Id).MarcarFaturaPaga(conta.Id);

        Assert.Equal(FechamentoDoMes.Aberta, (await ctx.FaturasDeAlunos.SingleAsync()).Status);
    }

    [Fact]
    public async Task A_tela_avisa_quem_ainda_nao_tem_CPF_de_pagador()
    {
        // Sem CPF do pagador o gateway recusa a cobrança. A conta continua valendo — o
        // professor acerta por fora —, mas ele precisa saber ANTES de fechar.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        Lancar(ctx, professor, local, "Medina", 110);

        var vista = await TestInfra.NovoAulasController(ctx, professor.Id)
            .Faturamento(MesPassado.Year, MesPassado.Month);
        var vm = Assert.IsType<FaturamentoVM>(Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(vista).Model);

        Assert.Equal(1, vm.SemCpfDoPagador);
        Assert.False(Assert.Single(vm.Previas).PodeCobrarPeloApp);
    }
}
