using Microsoft.EntityFrameworkCore;
using NSubstitute;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// ITEM 4: AS AULAS CAEM UM MÊS DEPOIS DO VENCIMENTO. 🗣️ Felipe: *"fica marcado até 1 mes depois
// do vencimento... caso acabe esse 1 mes de prazo, cancele todas as aulas e avise os alunos e o
// professor"*.
//
// ⚠️ O ALUNO É O INOCENTE DESTA HISTÓRIA. Ele não escolheu o plano de ninguém, e o pior desfecho
// possível é ele viajar pra quadra por uma aula que o sistema já tinha dado como morta. Por isso
// o aviso a ELE é parte do cancelamento, e não um extra.
public class CancelamentoPorBloqueioTests
{
    private static PlanoProfessorSettings Cfg => new() { BloqueioAPartirDe = new DateTime(2026, 01, 01) };

    // Venceu 01/09 → bloqueia 11/09 → aulas caem 01/10 10h.
    private static readonly DateTime Venceu = new(2026, 09, 01);
    private static readonly DateTime Cancela = new(2026, 10, 01, 10, 0, 0);

    private static (DbPadelContext ctx, Jogador prof, Jogador aluno, LocalAula local) Montar(DateTime? pagaAte)
    {
        var ctx = TestInfra.NovoContexto();

        var prof = new Jogador
        {
            Nome = "Marcio", Login = "marcio", Cpf = "55500000001", IsProfessor = true,
            PlanoProfessor = PlanoDoProfessor.Assinante, AssinaturaProfessorPagaAte = pagaAte,
        };
        var aluno = new Jogador { Nome = "Leonardo", Login = "leo", Cpf = "55500000002" };
        ctx.Jogadores.AddRange(prof, aluno);
        ctx.SaveChanges();

        var local = new LocalAula { ProfessorId = prof.Id, Nome = "Wallau", PrecoPadrao = 100, Ativo = true };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();

        return (ctx, prof, aluno, local);
    }

    private static Aula Marcada(Jogador prof, Jogador? aluno, LocalAula local, DateTime quando,
        string status = PoliticaAula.Confirmada) => new()
    {
        ProfessorId = prof.Id,
        AlunoId = aluno?.Id,
        NomeAlunoAvulso = aluno == null ? "Visitante" : null,
        LocalAulaId = local.Id,
        DataHora = quando,
        DuracaoMinutos = 60,
        Preco = 100m,
        Status = status,
    };

    private static Task<int> Varrer(DbPadelContext ctx, IPushNotificationService push, DateTime agora,
        PlanoProfessorSettings? cfg = null) =>
        CancelamentoPorBloqueioBackgroundService.VarrerAsync(ctx, push, cfg ?? Cfg, agora);

    // ── O cancelamento ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task No_fim_do_prazo_as_aulas_futuras_caem()
    {
        var (ctx, prof, aluno, local) = Montar(Venceu);
        using var _ = ctx;

        ctx.Aulas.AddRange(
            Marcada(prof, aluno, local, Cancela.AddDays(3)),
            Marcada(prof, aluno, local, Cancela.AddDays(10), PoliticaAula.Pendente));
        await ctx.SaveChangesAsync();

        var canceladas = await Varrer(ctx, Substitute.For<IPushNotificationService>(), Cancela.AddHours(1));

        Assert.Equal(2, canceladas);
        Assert.All(await ctx.Aulas.ToListAsync(), a => Assert.Equal(PoliticaAula.Cancelada, a.Status));
    }

    [Fact]
    public async Task Ninguem_e_cobrado_por_esse_cancelamento()
    {
        var (ctx, prof, aluno, local) = Montar(Venceu);
        using var _ = ctx;

        prof.CobraFaltaSemAviso = true;
        prof.HorasMinimasCancelamento = 24;
        ctx.Aulas.Add(Marcada(prof, aluno, local, Cancela.AddHours(2)));   // fora do prazo de aviso
        await ctx.SaveChangesAsync();

        await Varrer(ctx, Substitute.For<IPushNotificationService>(), Cancela.AddHours(1));

        var aula = await ctx.Aulas.FirstAsync();

        // ⚠️ `CanceladaPor` NÃO PODE SER "Aluno" — é a única string que faz `DeveCobrar` cobrar,
        // e cobrar multa de quem não teve nada a ver com o plano do professor seria o pior
        // desfecho possível deste item. Quem cancelou foi o Padelizou, e a palavra diz isso.
        Assert.Equal(PoliticaAula.CanceladaPeloSistema, aula.CanceladaPor);
        Assert.False(PoliticaAula.DeveCobrar(prof, aula, aula.CanceladaPor!, Cancela));
        Assert.False(aula.CobrarMesmoFaltando);
    }

    [Fact]
    public async Task O_passado_fica_onde_esta()
    {
        var (ctx, prof, aluno, local) = Montar(Venceu);
        using var _ = ctx;

        // ⚠️ Aula JÁ DADA é dinheiro a receber e histórico do aluno. Cancelar pra trás apagaria
        // a dívida de quem deve e a ficha de quem treinou.
        ctx.Aulas.AddRange(
            Marcada(prof, aluno, local, Cancela.AddDays(-20), PoliticaAula.Realizada),
            Marcada(prof, aluno, local, Cancela.AddDays(-2)));
        await ctx.SaveChangesAsync();

        var canceladas = await Varrer(ctx, Substitute.For<IPushNotificationService>(), Cancela.AddHours(1));

        Assert.Equal(0, canceladas);
        Assert.DoesNotContain(await ctx.Aulas.ToListAsync(), a => a.Status == PoliticaAula.Cancelada);
    }

    [Fact]
    public async Task Antes_do_prazo_nada_cai()
    {
        var (ctx, prof, aluno, local) = Montar(Venceu);
        using var _ = ctx;

        ctx.Aulas.Add(Marcada(prof, aluno, local, Cancela.AddDays(5)));
        await ctx.SaveChangesAsync();

        // Véspera: bloqueado há 19 dias, e as aulas seguem de pé. É o prazo inteiro que o Felipe
        // pediu — um mês pra ele resolver sem perder a agenda.
        Assert.Equal(0, await Varrer(ctx, Substitute.For<IPushNotificationService>(), Cancela.AddDays(-1)));
    }

    [Fact]
    public async Task Quem_pagou_antes_do_fim_do_prazo_nao_perde_nada()
    {
        var (ctx, prof, aluno, local) = Montar(new DateTime(2026, 12, 01));
        using var _ = ctx;

        ctx.Aulas.Add(Marcada(prof, aluno, local, Cancela.AddDays(5)));
        await ctx.SaveChangesAsync();

        Assert.Equal(0, await Varrer(ctx, Substitute.For<IPushNotificationService>(), Cancela.AddHours(1)));
    }

    [Fact]
    public async Task Com_o_bloqueio_dormente_nada_cai()
    {
        var (ctx, prof, aluno, local) = Montar(Venceu);
        using var _ = ctx;

        ctx.Aulas.Add(Marcada(prof, aluno, local, Cancela.AddDays(5)));
        await ctx.SaveChangesAsync();

        // ⚠️ A trava mais importante do bloco: este é o serviço que APAGA agenda. Com o
        // interruptor desligado ele não pode encostar em nada.
        Assert.Equal(0, await Varrer(ctx, Substitute.For<IPushNotificationService>(),
            Cancela.AddHours(1), new PlanoProfessorSettings()));
    }

    [Fact]
    public async Task Rodar_de_novo_nao_avisa_de_novo()
    {
        var (ctx, prof, aluno, local) = Montar(Venceu);
        using var _ = ctx;

        ctx.Aulas.Add(Marcada(prof, aluno, local, Cancela.AddDays(3)));
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        Assert.Equal(1, await Varrer(ctx, push, Cancela.AddHours(1)));

        // ⚠️ A idempotência sem coluna nova: a aula cancelada some da consulta de "futuras
        // ativas", então a segunda passada não acha nada. A varredura roda de hora em hora — sem
        // isto, o aluno levaria "sua aula foi cancelada" doze vezes por dia.
        Assert.Equal(0, await Varrer(ctx, push, Cancela.AddHours(2)));
    }

    // ── Os avisos ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task O_aluno_e_o_professor_sao_avisados_uma_vez_cada()
    {
        var (ctx, prof, aluno, local) = Montar(Venceu);
        using var _ = ctx;

        // Três aulas do MESMO aluno: ele é uma pessoa, não três avisos.
        ctx.Aulas.AddRange(
            Marcada(prof, aluno, local, Cancela.AddDays(3)),
            Marcada(prof, aluno, local, Cancela.AddDays(10)),
            Marcada(prof, aluno, local, Cancela.AddDays(17)));
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        await Varrer(ctx, push, Cancela.AddHours(1));

        var avisados = push.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IPushNotificationService.EnviarParaJogadorAsync))
            .Select(c => (int)c.GetArguments()[0]!)
            .ToList();

        Assert.Equal(new[] { prof.Id, aluno.Id }.OrderBy(x => x), avisados.OrderBy(x => x));
    }

    // ── O furo que o item 2 deixou aberto ─────────────────────────────────────────────────

    [Fact]
    public async Task O_aluno_nao_consegue_marcar_com_professor_bloqueado()
    {
        var (ctx, prof, aluno, local) = Montar(Venceu);
        using var _ = ctx;

        // ⚠️ O BLOQUEIO SOME DA BUSCA, MAS O POST CONTINUA EXISTINDO. `Solicitar` é opt-out do
        // filtro — e com razão, porque ele olha quem está LOGADO, e quem está logado aqui é o
        // aluno. O que faltava era checar o professor ALVO.
        //
        // Sem isto, o aluno com o link (ou com a aba aberta de antes) marca, a aula nasce
        // Pendente, o professor não pode aceitar (ConfirmarSolicitacao está bloqueada) e ele
        // fica pendurado esperando uma confirmação que não pode acontecer — exatamente o
        // desfecho que o desenho inteiro existe pra evitar.
        var controller = TestInfra.NovoAulasController(ctx, aluno.Id, plano: Cfg);

        await controller.Solicitar(prof.Id, local.Id, DateTime.Now.AddDays(3),
            ehPacote: false, recorrente: false, semanasRecorrencia: 0);

        Assert.Empty(await ctx.Aulas.ToListAsync());
    }

    [Fact]
    public async Task Aluno_avulso_nao_quebra_a_varredura()
    {
        var (ctx, prof, _, local) = Montar(Venceu);
        using var _c = ctx;

        // ⚠️ `AlunoId` NULO é o caso mais comum da agenda do professor (aluno sem conta, só
        // nome). Um `!.Value` descuidado aqui derrubaria o cancelamento de todo mundo que
        // viesse depois na fila — não só o dele.
        ctx.Aulas.Add(Marcada(prof, null, local, Cancela.AddDays(3)));
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();

        Assert.Equal(1, await Varrer(ctx, push, Cancela.AddHours(1)));

        var avisados = push.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IPushNotificationService.EnviarParaJogadorAsync))
            .Select(c => (int)c.GetArguments()[0]!)
            .ToList();

        // Só o professor: o avulso não tem conta pra receber aviso, e é ele quem vai ter que
        // avisar essa pessoa por fora.
        Assert.Equal(new[] { prof.Id }, avisados);
    }
}
