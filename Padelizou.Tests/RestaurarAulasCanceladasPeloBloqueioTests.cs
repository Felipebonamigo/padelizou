using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using padelizou.Controllers;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 28/10/2026 — DESFAZER O QUE O BLOQUEIO CANCELOU ANTES DA HORA.
//
// 🗣️ O Jônatas pagou e a agenda estava toda vermelha: o cancelamento tinha caído junto com o
// bloqueio, em 21/10 (ver CancelamentoNaoCaiJuntoComOBloqueioTests). Corrigida a data, as aulas
// que já tinham caído continuavam caídas. 🗣️ Felipe: *"crie o botão de restaurar para todos"*.
//
// ⚠️ A JANELA É O QUE TORNA O BOTÃO SEGURO. Com a conta corrigida, o PRIMEIRO cancelamento
// legítimo possível é `estreia + (DiasAteCancelarAsAulas − DiasAteOBloqueio)` — 14/11. Todo
// cancelamento do sistema ANTES disso é o defeito, e nenhum depois dele é. Então o botão pode
// ser apertado de novo amanhã, ou daqui a um ano, sem ressuscitar aula que caiu com razão.
public class RestaurarAulasCanceladasPeloBloqueioTests
{
    private static readonly DateTime Estreia = new(2026, 10, 21);
    private static readonly DateTime CaiuEm = new(2026, 10, 21, 10, 0, 0);
    private static readonly DateTime Agora = new(2026, 10, 28, 15, 0, 0);

    private static PlanoProfessorSettings Cfg => new() { BloqueioAPartirDe = Estreia };

    private sealed record Cenario(DbPadelContext Ctx, Jogador Prof, Jogador Aluno, LocalAula Local);

    private static Cenario Montar()
    {
        var ctx = TestInfra.NovoContexto();
        var prof = new Jogador
        {
            Nome = "Jônatas", Login = "jonatas", Cpf = "66600000001", IsProfessor = true,
            AssinaturaProfessorPagaAte = new DateTime(2026, 11, 27),
        };
        var aluno = new Jogador { Nome = "Bruna", Login = "bruna", Cpf = "66600000002" };
        ctx.Jogadores.AddRange(prof, aluno);
        ctx.SaveChanges();

        var local = new LocalAula { ProfessorId = prof.Id, Nome = "Arena", PrecoPadrao = 100, Ativo = true };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();
        return new Cenario(ctx, prof, aluno, local);
    }

    private static Aula Caida(Cenario c, DateTime quando, DateTime? caiuEm = null, string por = PoliticaAula.CanceladaPeloSistema,
        Guid? serie = null) => new()
    {
        ProfessorId = c.Prof.Id, AlunoId = c.Aluno.Id, LocalAulaId = c.Local.Id,
        DataHora = quando, DuracaoMinutos = 60, Preco = 100m,
        Status = PoliticaAula.Cancelada, CanceladaPor = por, CanceladaEm = caiuEm ?? CaiuEm,
        RecorrenciaId = serie, RecorrenciaSemFim = false,
    };

    private static Task<RestauracaoDoCancelamentoPorBloqueio.Resultado> Restaurar(Cenario c, IPushNotificationService? push = null) =>
        RestauracaoDoCancelamentoPorBloqueio.RestaurarAsync(c.Ctx, push ?? Substitute.For<IPushNotificationService>(), Agora, Cfg);

    private static async Task<Aula> Recarregar(Cenario c, int id)
    {
        c.Ctx.ChangeTracker.Clear();
        return await c.Ctx.Aulas.SingleAsync(a => a.Id == id);
    }

    // ── O que volta ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Aula_futura_derrubada_pelo_bloqueio_volta_de_pe()
    {
        var c = Montar();
        var aula = Caida(c, Agora.AddDays(2));
        c.Ctx.Aulas.Add(aula);
        await c.Ctx.SaveChangesAsync();

        var r = await Restaurar(c);

        Assert.Equal(1, r.Restauradas);
        var gravada = await Recarregar(c, aula.Id);
        Assert.Equal(PoliticaAula.Confirmada, gravada.Status);
        Assert.Null(gravada.CanceladaPor);
        Assert.Null(gravada.CanceladaEm);
    }

    [Fact]
    public async Task Cancelada_por_gente_fica_cancelada()
    {
        // Aluno ou professor que desmarcou decidiu isso — o botão só desfaz o que o SISTEMA fez.
        var c = Montar();
        var doAluno = Caida(c, Agora.AddDays(2), por: "Aluno");
        var doProfessor = Caida(c, Agora.AddDays(3), por: "Professor");
        c.Ctx.Aulas.AddRange(doAluno, doProfessor);
        await c.Ctx.SaveChangesAsync();

        var r = await Restaurar(c);

        Assert.Equal(0, r.Restauradas);
        Assert.Equal(PoliticaAula.Cancelada, (await Recarregar(c, doAluno.Id)).Status);
        Assert.Equal(PoliticaAula.Cancelada, (await Recarregar(c, doProfessor.Id)).Status);
    }

    [Fact]
    public async Task Aula_que_ja_passou_nao_volta()
    {
        // Ressuscitar o dia 24/10 poria na agenda uma aula que não aconteceu, esperando presença.
        var c = Montar();
        var passada = Caida(c, new DateTime(2026, 10, 24, 9, 0, 0));
        c.Ctx.Aulas.Add(passada);
        await c.Ctx.SaveChangesAsync();

        var r = await Restaurar(c);

        Assert.Equal(0, r.Restauradas);
        Assert.Equal(PoliticaAula.Cancelada, (await Recarregar(c, passada.Id)).Status);
    }

    [Fact]
    public async Task Cancelamento_legitimo_depois_da_janela_fica_cancelado()
    {
        // 14/11 10h é o primeiro cancelamento que a conta corrigida pode produzir de verdade.
        var c = Montar();
        var primeiroLegitimo = Estreia.AddDays(Cfg.DiasAteCancelarAsAulas - Cfg.DiasAteOBloqueio)
            .AddHours(BloqueioDoProfessor.HoraDoBloqueio);
        var legitima = Caida(c, primeiroLegitimo.AddDays(5), caiuEm: primeiroLegitimo);
        c.Ctx.Aulas.Add(legitima);
        await c.Ctx.SaveChangesAsync();

        var r = await RestauracaoDoCancelamentoPorBloqueio.RestaurarAsync(
            c.Ctx, Substitute.For<IPushNotificationService>(), primeiroLegitimo.AddHours(1), Cfg);

        Assert.Equal(0, r.Restauradas);
        Assert.Equal(PoliticaAula.Cancelada, (await Recarregar(c, legitima.Id)).Status);
    }

    [Fact]
    public async Task Com_o_bloqueio_dormente_nada_e_restaurado()
    {
        var c = Montar();
        c.Ctx.Aulas.Add(Caida(c, Agora.AddDays(2)));
        await c.Ctx.SaveChangesAsync();

        var r = await RestauracaoDoCancelamentoPorBloqueio.RestaurarAsync(
            c.Ctx, Substitute.For<IPushNotificationService>(), Agora, new PlanoProfessorSettings());

        Assert.Equal(0, r.Restauradas);
    }

    // ── A aula fixa ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Serie_sem_fim_volta_a_se_repetir()
    {
        // O cancelamento desligou a repetição; as aulas JÁ DADAS da série ficaram com ela ligada
        // — é por elas que se sabe que a série era sem fim.
        var c = Montar();
        var serie = Guid.NewGuid();
        var dada = Caida(c, Agora.AddDays(-5), serie: serie);
        dada.Status = PoliticaAula.Realizada;
        dada.CanceladaPor = null;
        dada.CanceladaEm = null;
        dada.RecorrenciaSemFim = true;
        var caida = Caida(c, Agora.AddDays(2), serie: serie);
        c.Ctx.Aulas.AddRange(dada, caida);
        await c.Ctx.SaveChangesAsync();

        await Restaurar(c);

        Assert.True((await Recarregar(c, caida.Id)).RecorrenciaSemFim);
    }

    [Fact]
    public async Task Serie_que_o_professor_encerrou_nao_volta_a_se_repetir()
    {
        // "Encerrar repetição" desliga a série INTEIRA, inclusive as já dadas.
        var c = Montar();
        var serie = Guid.NewGuid();
        var dada = Caida(c, Agora.AddDays(-5), serie: serie);
        dada.Status = PoliticaAula.Realizada;
        dada.CanceladaPor = null;
        dada.CanceladaEm = null;
        var caida = Caida(c, Agora.AddDays(2), serie: serie);
        c.Ctx.Aulas.AddRange(dada, caida);
        await c.Ctx.SaveChangesAsync();

        await Restaurar(c);

        var gravada = await Recarregar(c, caida.Id);
        Assert.Equal(PoliticaAula.Confirmada, gravada.Status);
        Assert.False(gravada.RecorrenciaSemFim);
    }

    // ── Horário que já foi ocupado ────────────────────────────────────────────────────────

    [Fact]
    public async Task Horario_ja_ocupado_por_outra_aula_fica_de_fora()
    {
        // O professor viu a agenda vazia e encaixou outro aluno ali: as duas na mesma quadra
        // seria o pior desfecho — ele descobriria na hora, com dois alunos esperando.
        var c = Montar();
        var quando = Agora.AddDays(2);
        var caida = Caida(c, quando);
        var nova = new Aula
        {
            ProfessorId = c.Prof.Id, NomeAlunoAvulso = "Outro", LocalAulaId = c.Local.Id,
            DataHora = quando.AddMinutes(30), DuracaoMinutos = 60, Preco = 100m, Status = PoliticaAula.Confirmada,
        };
        c.Ctx.Aulas.AddRange(caida, nova);
        await c.Ctx.SaveChangesAsync();

        var r = await Restaurar(c);

        Assert.Equal(0, r.Restauradas);
        Assert.Equal(1, r.PuladasPorConflito);
        Assert.Equal(PoliticaAula.Cancelada, (await Recarregar(c, caida.Id)).Status);
    }

    [Fact]
    public async Task Colegas_da_mesma_turma_voltam_juntos()
    {
        // Duas linhas da mesma turma no mesmo horário são a MESMA sessão, não conflito.
        var c = Montar();
        var turma = Guid.NewGuid();
        var quando = Agora.AddDays(2);
        var a = Caida(c, quando);
        a.TurmaId = turma;
        var b = Caida(c, quando);
        b.TurmaId = turma;
        b.AlunoId = null;
        b.NomeAlunoAvulso = "Colega";
        c.Ctx.Aulas.AddRange(a, b);
        await c.Ctx.SaveChangesAsync();

        var r = await Restaurar(c);

        Assert.Equal(2, r.Restauradas);
    }

    // ── Os avisos ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Aluno_e_professor_sao_avisados_uma_vez_cada()
    {
        // O aluno recebeu "suas aulas foram canceladas". Sem o aviso de volta ele não aparece
        // na quadra — e a aula restaurada vira falta.
        var c = Montar();
        c.Ctx.Aulas.AddRange(Caida(c, Agora.AddDays(2)), Caida(c, Agora.AddDays(9)));
        await c.Ctx.SaveChangesAsync();
        var push = Substitute.For<IPushNotificationService>();

        await Restaurar(c, push);

        await push.Received(1).EnviarParaJogadorAsync(c.Aluno.Id, Arg.Any<string>(),
            Arg.Is<string>(corpo => corpo != null && corpo.Contains("Jônatas")), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
        await push.Received(1).EnviarParaJogadorAsync(c.Prof.Id, Arg.Any<string>(),
            Arg.Is<string>(corpo => corpo != null && corpo.Contains('2')), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Falha_no_aviso_nao_desfaz_a_restauracao()
    {
        var c = Montar();
        var aula = Caida(c, Agora.AddDays(2));
        c.Ctx.Aulas.Add(aula);
        await c.Ctx.SaveChangesAsync();
        var push = Substitute.For<IPushNotificationService>();
        push.EnviarParaJogadorAsync(default, default!, default!, default, default)
            .ReturnsForAnyArgs(Task.FromException(new InvalidOperationException("push fora do ar")));

        var r = await Restaurar(c, push);

        Assert.Equal(1, r.Restauradas);
        Assert.Equal(PoliticaAula.Confirmada, (await Recarregar(c, aula.Id)).Status);
    }

    // ── O botão ───────────────────────────────────────────────────────────────────────────

    private static AdminController Painel(DbPadelContext ctx, int logado)
    {
        var controller = new AdminController(
            ctx,
            Substitute.For<IPushNotificationService>(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            Microsoft.Extensions.Options.Options.Create(new RegistroResultadosSettings()));
        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, logado.ToString()) }, "Teste")),
        };
        http.Request.Method = HttpMethods.Post;
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            http, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        controller.Url = TestInfra.UrlDeTeste();
        return controller;
    }

    [Fact]
    public async Task So_o_admin_raiz_aperta_o_botao()
    {
        var c = Montar();
        var geral = new Jogador { Nome = "Admin Geral", Cpf = "66600000009", IsAdminGeral = true };
        c.Ctx.Jogadores.Add(geral);
        var aula = Caida(c, DateTime.Now.AddDays(2), caiuEm: DateTime.Now.AddHours(-1));
        c.Ctx.Aulas.Add(aula);
        await c.Ctx.SaveChangesAsync();

        var resultado = await Painel(c.Ctx, geral.Id).RestaurarAulasCanceladasPeloBloqueio(
            Microsoft.Extensions.Options.Options.Create(new PlanoProfessorSettings { BloqueioAPartirDe = DateTime.Today.AddDays(-3) }));

        Assert.IsType<ForbidResult>(resultado);
        Assert.Equal(PoliticaAula.Cancelada, (await Recarregar(c, aula.Id)).Status);
    }

    [Fact]
    public async Task O_raiz_restaura_e_ve_quantas_voltaram()
    {
        var c = Montar();
        var raiz = new Jogador { Nome = "Felipe", Cpf = "66600000000", IsAdminGeral = true, IsAdminRaiz = true };
        c.Ctx.Jogadores.Add(raiz);
        var aula = Caida(c, DateTime.Now.AddDays(2), caiuEm: DateTime.Now.AddHours(-1));
        c.Ctx.Aulas.Add(aula);
        await c.Ctx.SaveChangesAsync();
        var controller = Painel(c.Ctx, raiz.Id);

        var resultado = await controller.RestaurarAulasCanceladasPeloBloqueio(
            Microsoft.Extensions.Options.Options.Create(new PlanoProfessorSettings { BloqueioAPartirDe = DateTime.Today.AddDays(-3) }));

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(PoliticaAula.Confirmada, (await Recarregar(c, aula.Id)).Status);
        Assert.Contains("1", controller.TempData["Sucesso"]?.ToString());
    }

    [Fact]
    public void O_botao_e_POST_com_antiforgery()
    {
        var metodo = typeof(AdminController).GetMethod(nameof(AdminController.RestaurarAulasCanceladasPeloBloqueio));

        Assert.NotNull(metodo);
        Assert.NotEmpty(metodo.GetCustomAttributes(typeof(HttpPostAttribute), false));
        Assert.NotEmpty(metodo.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), false));
    }
}
