using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Controllers;
using padelizou.Controllers;
using Padelizou.Filters;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;
using Xunit;

namespace Padelizou.Tests;

// 5 DIAS DE USO NORMAL DEPOIS DO VENCIMENTO, E A AGENDA FECHA POR INTEIRO NO DIA SEGUINTE.
//
// 🗣️ Felipe, 14/10/2026: *"tem professor acessando sem pagar... após o não pagamento daremos
// apenas 5 dias de utilização normal, depois um aviso de bloqueio nas marcações e acesso à
// agenda"*. Eram 7 dias de carência e 10 de bloqueio, e a leitura da agenda seguia aberta.
//
// Três coisas guardadas aqui:
//  1. os prazos (5 de carência, bloqueio às 10h do dia seguinte);
//  2. quem NUNCA teve relógio (o professor que usa fora do painel) não escapa mais — o prazo
//     dele parte da data em que o interruptor foi ligado;
//  3. a LEITURA da agenda do professor fecha junto com as marcações.
public class BloqueioEmCincoDiasTests
{
    private static readonly DateTime Estreia = new(2026, 01, 01);

    private static PlanoProfessorSettings Cfg => new() { BloqueioAPartirDe = Estreia };

    // ═══════════════════ 1. OS PRAZOS ═══════════════════

    [Fact]
    public void Os_prazos_sao_cinco_dias_de_carencia_e_o_bloqueio_no_dia_seguinte()
    {
        var cfg = new PlanoProfessorSettings();

        Assert.Equal(5, cfg.DiasDeCarencia);
        // Colar os dois no mesmo número fecharia a porta no instante em que a taxa sobe; um dia
        // depois é o primeiro em que `CondicoesDeAssinante` e o bloqueio concordam.
        Assert.Equal(cfg.DiasDeCarencia + 1, cfg.DiasAteOBloqueio);
    }

    [Fact]
    public void A_mensalidade_vencida_ainda_vale_ate_o_quinto_dia_e_a_agenda_fecha_no_sexto_as_10h()
    {
        var prof = new Jogador
        {
            Nome = "Prof", Cpf = "88800000001", IsProfessor = true,
            PlanoProfessor = PlanoDoProfessor.Assinante,
            AssinaturaProfessorPagaAte = new DateTime(2026, 9, 18, 12, 0, 0),
        };

        // Dia 5 (23/09), ainda dentro da carência: assinante em dia, agenda aberta.
        Assert.Equal(PlanoDoProfessor.Situacao.AssinanteEmDia,
            PlanoDoProfessor.SituacaoDe(prof, new DateTime(2026, 9, 23, 11, 0, 0), Cfg));
        Assert.False(BloqueioDoProfessor.EstaBloqueado(prof, new DateTime(2026, 9, 23, 23, 0, 0), Cfg));

        // Depois dos 5 dias a taxa já sobe — e a agenda ainda abre até o dia seguinte.
        Assert.Equal(PlanoDoProfessor.Situacao.AssinanteEmAtraso,
            PlanoDoProfessor.SituacaoDe(prof, new DateTime(2026, 9, 23, 13, 0, 0), Cfg));

        Assert.Equal(new DateTime(2026, 9, 24, 10, 0, 0), BloqueioDoProfessor.BloqueiaEm(prof, Cfg));
        Assert.False(BloqueioDoProfessor.EstaBloqueado(prof, new DateTime(2026, 9, 24, 9, 59, 0), Cfg));
        Assert.True(BloqueioDoProfessor.EstaBloqueado(prof, new DateTime(2026, 9, 24, 10, 0, 0), Cfg));
    }

    // ═══════════════════ 2. QUEM NUNCA TEVE RELÓGIO ═══════════════════

    // O professor que já existia quando o interruptor foi ligado e nunca abriu o painel nem o
    // plano: sem relógio de teste, sem mensalidade. Antes, ficava SEM PRAZO e nunca era barrado.
    [Fact]
    public void Professor_antigo_sem_relogio_fecha_no_dia_seguinte_a_data_de_ligar()
    {
        var cfg = new PlanoProfessorSettings { BloqueioAPartirDe = new DateTime(2026, 11, 1) };
        var prof = new Jogador
        {
            Nome = "Prof", Cpf = "88800000002", IsProfessor = true, CriadoEm = new DateTime(2026, 3, 1),
        };

        Assert.Equal(new DateTime(2026, 11, 2, 10, 0, 0), BloqueioDoProfessor.BloqueiaEm(prof, cfg));
        Assert.False(BloqueioDoProfessor.EstaBloqueado(prof, new DateTime(2026, 11, 2, 9, 0, 0), cfg));
        Assert.True(BloqueioDoProfessor.EstaBloqueado(prof, new DateTime(2026, 11, 2, 10, 0, 0), cfg));
    }

    // O professor NOVO (conta criada depois de ligar) não nasce bloqueado: o teste dele ainda
    // vai começar. Sem esta guarda, a conta de hoje fecharia amanhã sem ter tido teste.
    [Fact]
    public void Professor_novo_sem_relogio_nao_tem_prazo()
    {
        var cfg = new PlanoProfessorSettings { BloqueioAPartirDe = new DateTime(2026, 11, 1) };
        var prof = new Jogador
        {
            Nome = "Prof", Cpf = "88800000003", IsProfessor = true, CriadoEm = new DateTime(2026, 11, 5),
        };

        Assert.Null(BloqueioDoProfessor.BloqueiaEm(prof, cfg));
        Assert.False(BloqueioDoProfessor.EstaBloqueado(prof, new DateTime(2026, 12, 1), cfg));
    }

    [Fact]
    public void Dormente_continua_dormente_pra_quem_nunca_teve_relogio()
    {
        var prof = new Jogador
        {
            Nome = "Prof", Cpf = "88800000004", IsProfessor = true, CriadoEm = new DateTime(2026, 3, 1),
        };

        Assert.Null(BloqueioDoProfessor.BloqueiaEm(prof, new PlanoProfessorSettings()));
    }

    // Quem não é professor nunca entra nesta conta.
    [Fact]
    public void Quem_nao_e_professor_nao_tem_prazo()
    {
        var cfg = new PlanoProfessorSettings { BloqueioAPartirDe = new DateTime(2026, 11, 1) };
        var jogador = new Jogador { Nome = "Aluno", Cpf = "88800000005", CriadoEm = new DateTime(2026, 3, 1) };

        Assert.Null(BloqueioDoProfessor.BloqueiaEm(jogador, cfg));
    }

    // O aviso também alcança esse professor — senão ele fecharia sem nunca ter sido avisado.
    [Fact]
    public async Task O_aviso_de_bloqueio_alcanca_o_professor_antigo_sem_relogio()
    {
        using var ctx = TestInfra.NovoContexto();
        var cfg = new PlanoProfessorSettings { BloqueioAPartirDe = new DateTime(2026, 11, 1) };
        var antigo = new Jogador
        {
            Nome = "Antigo", Cpf = "88800000006", IsProfessor = true, CriadoEm = new DateTime(2026, 3, 1),
        };
        var novo = new Jogador
        {
            Nome = "Novo", Cpf = "88800000007", IsProfessor = true, CriadoEm = new DateTime(2026, 11, 5),
        };
        ctx.Jogadores.AddRange(antigo, novo);
        await ctx.SaveChangesAsync();
        var push = Substitute.For<IPushNotificationService>();

        // 28/10, 11h: falta 5 dias pro bloqueio de 02/11.
        var avisados = await AvisosDoPlanoDoProfessorBackgroundService.VarrerAsync(
            ctx, push, cfg, new DateTime(2026, 10, 28, 11, 0, 0));

        Assert.Equal(1, avisados);
        await push.Received(1).EnviarParaJogadorAsync(antigo.Id, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
        await push.DidNotReceive().EnviarParaJogadorAsync(novo.Id, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // ═══════════════════ 3. A LEITURA DA AGENDA FECHA ═══════════════════

    private sealed class ControllerDeTeste : Controller { }

    private static Jogador Bloqueado() => new()
    {
        Nome = "Prof", Cpf = "88800000010", IsProfessor = true,
        PlanoProfessor = PlanoDoProfessor.Assinante,
        AssinaturaProfessorPagaAte = new DateTime(2025, 01, 01),
        CriadoEm = new DateTime(2024, 1, 1),
    };

    private static ActionExecutingContext ContextoGet(int jogadorId, Type controller, string acao,
        bool ajax = false)
    {
        var metodo = controller.GetMethods()
            .First(m => m.Name == acao && !m.GetCustomAttributes(typeof(HttpPostAttribute), true).Any());

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, jogadorId.ToString()) }, "Teste")),
        };
        http.Request.Method = HttpMethods.Get;
        if (ajax) http.Request.Headers.XRequestedWith = "XMLHttpRequest";

        var descritor = new ControllerActionDescriptor { MethodInfo = metodo, ActionName = acao };
        var actionContext = new ActionContext(http, new RouteData(), descritor);
        var c = new ControllerDeTeste { ControllerContext = new ControllerContext(actionContext) };
        c.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            http, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());

        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
            new Dictionary<string, object?>(), c);
    }

    private static async Task<(bool Seguiu, ActionExecutingContext Ctx)> RodarGetAsync(
        DbPadelContext ctx, int jogadorId, Type controller, string acao, bool ajax = false,
        PlanoProfessorSettings? cfg = null)
    {
        var filtro = new ExigePlanoAtivoAttribute.Filtro(ctx, Options.Create(cfg ?? Cfg));
        var contexto = ContextoGet(jogadorId, controller, acao, ajax);

        var seguiu = false;
        await filtro.OnActionExecutionAsync(contexto, () =>
        {
            seguiu = true;
            return Task.FromResult(new ActionExecutedContext(contexto, new List<IFilterMetadata>(), contexto.Controller));
        });
        return (seguiu, contexto);
    }

    [Theory]
    [InlineData("MinhaAgenda")]
    [InlineData("Dashboard")]
    [InlineData("MeusAlunos")]
    [InlineData("Financeiro")]
    [InlineData("MeusHorarios")]
    public async Task A_leitura_da_agenda_do_professor_bloqueado_para_e_vai_pra_tela_do_plano(string acao)
    {
        using var ctx = TestInfra.NovoContexto();
        var prof = Bloqueado();
        ctx.Jogadores.Add(prof);
        await ctx.SaveChangesAsync();

        var (seguiu, contexto) = await RodarGetAsync(ctx, prof.Id, typeof(AulasController), acao);

        Assert.False(seguiu, $"o GET de {acao} atendeu professor bloqueado");
        var redirect = Assert.IsType<RedirectToActionResult>(contexto.Result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("PlanoProfessor", redirect.ControllerName);
    }

    [Fact]
    public async Task Pedido_de_fetch_na_leitura_recebe_403_e_nao_uma_pagina()
    {
        using var ctx = TestInfra.NovoContexto();
        var prof = Bloqueado();
        ctx.Jogadores.Add(prof);
        await ctx.SaveChangesAsync();

        var (seguiu, contexto) = await RodarGetAsync(ctx, prof.Id, typeof(AulasController),
            "BuscarAlunoNoSistema", ajax: true);

        Assert.False(seguiu);
        Assert.Equal(403, Assert.IsType<ObjectResult>(contexto.Result).StatusCode);
    }

    // O lado do ALUNO e as telas públicas seguem abertas: o professor bloqueado que também é
    // aluno de alguém não perde a aula DELE, e a vitrine continua sendo vitrine.
    [Theory]
    [InlineData(typeof(AulasController), "MinhasAulas")]
    [InlineData(typeof(AulasController), "Solicitar")]
    [InlineData(typeof(AulasController), "MinhaEvolucao")]
    [InlineData(typeof(ProfessoresController), "Perfil")]
    [InlineData(typeof(JogoAulaController), "Detalhes")]
    public async Task O_lado_do_aluno_e_as_telas_publicas_seguem_abertos(Type controller, string acao)
    {
        using var ctx = TestInfra.NovoContexto();
        var prof = Bloqueado();
        ctx.Jogadores.Add(prof);
        await ctx.SaveChangesAsync();

        var (seguiu, _) = await RodarGetAsync(ctx, prof.Id, controller, acao);

        Assert.True(seguiu, $"{controller.Name}.{acao} fechou pro professor bloqueado — é tela de aluno/pública");
    }

    [Fact]
    public async Task Professor_em_dia_le_a_agenda_normalmente()
    {
        using var ctx = TestInfra.NovoContexto();
        var prof = Bloqueado();
        prof.AssinaturaProfessorPagaAte = DateTime.Now.AddDays(20);
        ctx.Jogadores.Add(prof);
        await ctx.SaveChangesAsync();

        var (seguiu, _) = await RodarGetAsync(ctx, prof.Id, typeof(AulasController), "MinhaAgenda");

        Assert.True(seguiu);
    }

    // A porta de saída nunca fecha, na leitura também.
    [Fact]
    public async Task A_tela_do_plano_abre_pro_professor_bloqueado()
    {
        using var ctx = TestInfra.NovoContexto();
        var prof = Bloqueado();
        ctx.Jogadores.Add(prof);
        await ctx.SaveChangesAsync();

        Assert.False(typeof(PlanoProfessorController)
            .GetCustomAttributes(typeof(ExigePlanoAtivoAttribute), true).Any());
    }

    // O relógio do professor NOVO começa no primeiro uso de qualquer rota fechável — e não só
    // no painel — pra que ninguém use de graça por uma porta lateral.
    [Fact]
    public async Task O_primeiro_uso_de_professor_novo_comeca_o_relogio_do_teste()
    {
        using var ctx = TestInfra.NovoContexto();
        var novo = new Jogador { Nome = "Novo", Cpf = "88800000011", IsProfessor = true, CriadoEm = DateTime.Now };
        ctx.Jogadores.Add(novo);
        await ctx.SaveChangesAsync();

        var (seguiu, _) = await RodarGetAsync(ctx, novo.Id, typeof(AulasController), "MeusLocais");

        Assert.True(seguiu);
        Assert.NotNull((await ctx.Jogadores.FindAsync(novo.Id))!.TesteProfessorInicio);
    }

    // O professor antigo NÃO ganha 15 dias de teste novos por bater no filtro: o prazo dele é o
    // da data de ligar.
    [Fact]
    public async Task Professor_antigo_nao_ganha_teste_novo_ao_passar_pelo_filtro()
    {
        using var ctx = TestInfra.NovoContexto();
        var antigo = new Jogador
        {
            Nome = "Antigo", Cpf = "88800000012", IsProfessor = true, CriadoEm = new DateTime(2024, 1, 1),
        };
        ctx.Jogadores.Add(antigo);
        await ctx.SaveChangesAsync();

        // Já passou da data de ligar + 1 dia: o filtro tem que barrar, e não dar teste.
        var (seguiu, _) = await RodarGetAsync(ctx, antigo.Id, typeof(AulasController), "MeusLocais");

        Assert.False(seguiu);
        Assert.Null((await ctx.Jogadores.FindAsync(antigo.Id))!.TesteProfessorInicio);
    }

    // Dormente (interruptor desligado): o filtro não mexe em ninguém, nem no relógio.
    [Fact]
    public async Task Dormente_o_filtro_nao_comeca_relogio_nenhum()
    {
        using var ctx = TestInfra.NovoContexto();
        var novo = new Jogador { Nome = "Novo", Cpf = "88800000013", IsProfessor = true, CriadoEm = DateTime.Now };
        ctx.Jogadores.Add(novo);
        await ctx.SaveChangesAsync();

        var (seguiu, _) = await RodarGetAsync(ctx, novo.Id, typeof(AulasController), "MeusLocais",
            cfg: new PlanoProfessorSettings());

        Assert.True(seguiu);
        Assert.Null((await ctx.Jogadores.FindAsync(novo.Id))!.TesteProfessorInicio);
    }

    // ═══════════════════ 4. O LINK DO E-MAIL ═══════════════════

    // ⚠️ `ConfirmarPorEmail` é GET SEM LOGIN, e GRAVA: aceitar a aula por ali é "marcação" tanto
    // quanto o botão da tela. O filtro não sabe quem é (não há usuário), então a própria ação
    // confere o professor da aula. Sem isto, o professor bloqueado seguia confirmando aula nova
    // pelo e-mail que o próprio Padelizou manda.
    private static async Task<(DbPadelContext ctx, Aula aula, Jogador prof)> AulaPendenteAsync(Jogador prof)
    {
        var ctx = TestInfra.NovoContexto();
        var aluno = new Jogador { Nome = "Aluno", Cpf = "88800000020" };
        ctx.Jogadores.AddRange(prof, aluno);
        await ctx.SaveChangesAsync();
        var local = new LocalAula { Nome = "Quadra", ProfessorId = prof.Id };
        ctx.LocaisAula.Add(local);
        await ctx.SaveChangesAsync();
        var aula = new Aula
        {
            ProfessorId = prof.Id, AlunoId = aluno.Id, LocalAulaId = local.Id,
            DataHora = DateTime.Now.AddDays(3), Status = "Pendente",
        };
        ctx.Aulas.Add(aula);
        await ctx.SaveChangesAsync();
        return (ctx, aula, prof);
    }

    [Fact]
    public async Task Professor_bloqueado_nao_confirma_aula_nova_pelo_link_do_email()
    {
        var (ctx, aula, _) = await AulaPendenteAsync(Bloqueado());
        using var _c = ctx;
        var controller = TestInfra.NovoAulasController(ctx, 0, plano: Cfg);

        var resultado = await controller.ConfirmarPorEmail(aula.Id, aula.TokenConfirmacao, aceitar: true);

        Assert.IsType<ViewResult>(resultado);
        Assert.True((bool?)controller.ViewBag.ProfessorBloqueado == true);
        Assert.Equal("Pendente", (await ctx.Aulas.FindAsync(aula.Id))!.Status);
    }

    [Fact]
    public async Task Professor_em_dia_confirma_pelo_link_do_email()
    {
        var prof = Bloqueado();
        prof.AssinaturaProfessorPagaAte = DateTime.Now.AddDays(20);
        var (ctx, aula, _) = await AulaPendenteAsync(prof);
        using var _c = ctx;
        var controller = TestInfra.NovoAulasController(ctx, 0, plano: Cfg);

        await controller.ConfirmarPorEmail(aula.Id, aula.TokenConfirmacao, aceitar: true);

        Assert.Equal("Confirmada", (await ctx.Aulas.FindAsync(aula.Id))!.Status);
    }

    // Recusar não cria nada na agenda: o professor bloqueado pode recusar.
    [Fact]
    public async Task Professor_bloqueado_ainda_pode_recusar_pelo_link()
    {
        var (ctx, aula, _) = await AulaPendenteAsync(Bloqueado());
        using var _c = ctx;
        var controller = TestInfra.NovoAulasController(ctx, 0, plano: Cfg);

        await controller.ConfirmarPorEmail(aula.Id, aula.TokenConfirmacao, aceitar: false);

        Assert.Equal("Recusada", (await ctx.Aulas.FindAsync(aula.Id))!.Status);
    }

    // ⚠️ O EF InMemory não valida SQL: a consulta do aviso (com a quarta ponta, comparando
    // `CriadoEm` nulável com a estreia) precisa traduzir pro Postgres de verdade.
    [Fact]
    public void A_consulta_do_aviso_traduz_pro_Postgres()
    {
        var opcoes = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
            .Options;
        using var ctx = new DbPadelContext(opcoes);
        DateTime? estreia = new DateTime(2026, 11, 1);

        var sql = ctx.Jogadores
            .Where(j => j.IsProfessor
                     && (j.TesteProfessorInicio != null
                      || j.AssinaturaProfessorPagaAte != null
                      || j.CortesiaProfessorAte != null
                      || (estreia != null && j.CriadoEm != null && j.CriadoEm < estreia)))
            .ToQueryString();

        Assert.Contains("CriadoEm", sql);
    }
}
