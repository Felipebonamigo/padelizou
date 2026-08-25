using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using Xunit;

namespace Padelizou.Tests;

// A fronteira entre as DUAS marcas de recebimento que passaram a existir: a da AULA
// (Aula.PagaEm, o avulso que manda o Pix na sexta) e a da CONTA DO MÊS
// (FaturaDoAluno.PagaEm, o mensalista). Elas precisam contar a mesma história — e o jeito de
// não contarem é o mais caro que existe nesta tela: cobrar a mesma aula duas vezes.
public class ContaDoMesEAulaPagaTests
{
    private static readonly DateTime MesPassado =
        new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);

    private static (DbPadelContext ctx, Jogador professor, LocalAula local) Montar()
    {
        var ctx = TestInfra.NovoContexto();

        var professor = new Jogador { Nome = "Jonatas", Login = "jonatas", Cpf = "99900000031", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();

        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Batata Padel", PrecoPadrao = 110, Ativo = true };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();

        return (ctx, professor, local);
    }

    private static Aula Lancar(DbPadelContext ctx, Jogador professor, LocalAula local, string nome,
        decimal preco, int dia = 10, DateTime? pagaEm = null)
    {
        var aula = new Aula
        {
            ProfessorId = professor.Id, NomeAlunoAvulso = nome, LocalAulaId = local.Id,
            DataHora = MesPassado.AddDays(dia - 1).AddHours(9),
            Preco = preco, Status = PoliticaAula.Realizada, QuantidadeAlunos = 1,
            PagaEm = pagaEm,
        };
        ctx.Aulas.Add(aula);
        ctx.SaveChanges();
        return aula;
    }

    // ⚠️ O ERRO MAIS CARO DESTA TELA. O professor recebeu R$ 110 em dinheiro na quadra e deu
    // baixa na aula; no fim do mês ele fecha a competência. Sem esta régua, a mesma aula é
    // cobrada de novo — e o aluno recebe uma conta por dinheiro que já entregou.
    [Fact]
    public void Aula_ja_paga_por_fora_nao_entra_na_conta_do_mes()
    {
        var paga = new Aula
        {
            ProfessorId = 1, NomeAlunoAvulso = "Medina", LocalAulaId = 1,
            DataHora = MesPassado.AddDays(9).AddHours(9),
            Preco = 110, Status = PoliticaAula.Realizada, PagaEm = DateTime.Now,
        };

        Assert.False(FechamentoDoMes.EntraNaConta(paga));
    }

    [Fact]
    public void Falta_cobravel_ja_paga_tambem_fica_de_fora()
    {
        var paga = new Aula
        {
            ProfessorId = 1, NomeAlunoAvulso = "Medina", LocalAulaId = 1,
            DataHora = MesPassado.AddDays(9).AddHours(9),
            Preco = 110, Status = PoliticaAula.Faltou, CobrarMesmoFaltando = true,
            PagaEm = DateTime.Now,
        };

        Assert.False(FechamentoDoMes.EntraNaConta(paga));
    }

    [Fact]
    public async Task A_conta_do_mes_cobra_so_o_que_ainda_nao_foi_pago()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        Lancar(ctx, professor, local, "Medina", 110, dia: 3, pagaEm: DateTime.Now);
        Lancar(ctx, professor, local, "Medina", 110, dia: 10);
        Lancar(ctx, professor, local, "Medina", 110, dia: 17);

        await TestInfra.NovoAulasController(ctx, professor.Id)
            .FecharMes(MesPassado.Year, MesPassado.Month, diaVencimento: 10);

        var conta = await ctx.FaturasDeAlunos.SingleAsync();
        Assert.Equal(220m, conta.Valor);
        Assert.Equal(2, conta.QuantidadeAulas);
    }

    // Se TODAS já foram pagas por fora, não há conta pra fechar — e uma conta de R$ 0 na tela
    // do professor é pior que nenhuma.
    [Fact]
    public async Task Mes_inteiro_pago_por_fora_nao_gera_conta()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        Lancar(ctx, professor, local, "Medina", 110, dia: 3, pagaEm: DateTime.Now);
        Lancar(ctx, professor, local, "Medina", 110, dia: 10, pagaEm: DateTime.Now);

        await TestInfra.NovoAulasController(ctx, professor.Id)
            .FecharMes(MesPassado.Year, MesPassado.Month, diaVencimento: 10);

        Assert.Empty(await ctx.FaturasDeAlunos.ToListAsync());
    }

    // ─── Dar baixa na conta do mês carimba as aulas dela ──────────────────────────────

    // Sem isto, a conta de abril diz "paga" e as oito aulas de abril continuam, cada uma,
    // dizendo "a cobrar" no Financeiro. Duas telas, duas verdades.
    [Fact]
    public async Task Marcar_a_conta_do_mes_como_paga_carimba_as_aulas_dela()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        Lancar(ctx, professor, local, "Medina", 110, dia: 3);
        Lancar(ctx, professor, local, "Medina", 110, dia: 10);
        var deOutro = Lancar(ctx, professor, local, "Duda", 150, dia: 4);

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.FecharMes(MesPassado.Year, MesPassado.Month, diaVencimento: 10);

        var contaDoMedina = await ctx.FaturasDeAlunos.SingleAsync(f => f.NomeAvulso == "Medina");
        await controller.MarcarFaturaPaga(contaDoMedina.Id);

        var doMedina = await ctx.Aulas.Where(a => a.NomeAlunoAvulso == "Medina").ToListAsync();
        Assert.Equal(2, doMedina.Count);
        Assert.All(doMedina, a => Assert.NotNull(a.PagaEm));

        // ⚠️ Contraprova: a conta da Duda não foi paga, e a aula dela não pode ter sido tocada.
        Assert.Null((await ctx.Aulas.FindAsync(deOutro.Id))!.PagaEm);
    }

    [Fact]
    public async Task Reabrir_a_conta_apaga_o_carimbo_das_aulas()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        Lancar(ctx, professor, local, "Medina", 110, dia: 3);
        Lancar(ctx, professor, local, "Medina", 110, dia: 10);

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.FecharMes(MesPassado.Year, MesPassado.Month, diaVencimento: 10);

        var conta = await ctx.FaturasDeAlunos.SingleAsync();
        await controller.MarcarFaturaPaga(conta.Id);
        await controller.ReabrirFatura(conta.Id);

        var aulas = await ctx.Aulas.ToListAsync();
        Assert.All(aulas, a => Assert.Null(a.PagaEm));
    }

    // ⚠️ Reabrir não pode apagar o recebimento que veio de FORA da conta: aquele Pix aconteceu
    // e não tem nada a ver com a baixa que o professor está desfazendo.
    [Fact]
    public async Task Reabrir_nao_apaga_o_pagamento_de_aula_que_nao_estava_na_conta()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var jaPagaAntes = Lancar(ctx, professor, local, "Medina", 110, dia: 3, pagaEm: DateTime.Now);
        Lancar(ctx, professor, local, "Medina", 110, dia: 10);

        var controller = TestInfra.NovoAulasController(ctx, professor.Id);
        await controller.FecharMes(MesPassado.Year, MesPassado.Month, diaVencimento: 10);

        var conta = await ctx.FaturasDeAlunos.SingleAsync();
        await controller.MarcarFaturaPaga(conta.Id);
        await controller.ReabrirFatura(conta.Id);

        Assert.NotNull((await ctx.Aulas.FindAsync(jaPagaAntes.Id))!.PagaEm);
    }

    [Fact]
    public async Task Professor_de_fora_nao_carimba_as_aulas_dando_baixa_na_conta_alheia()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var intruso = new Jogador { Nome = "Outro", Login = "outro", Cpf = "99900000032", IsProfessor = true };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        Lancar(ctx, professor, local, "Medina", 110, dia: 3);
        await TestInfra.NovoAulasController(ctx, professor.Id)
            .FecharMes(MesPassado.Year, MesPassado.Month, diaVencimento: 10);

        var conta = await ctx.FaturasDeAlunos.SingleAsync();
        await TestInfra.NovoAulasController(ctx, intruso.Id).MarcarFaturaPaga(conta.Id);

        Assert.All(await ctx.Aulas.ToListAsync(), a => Assert.Null(a.PagaEm));
    }
}
