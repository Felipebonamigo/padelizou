using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using padelizou.Controllers;
using Padelizou.Filters;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// O FILTRO EM AÇÃO. A régua de QUEM bloqueia está em BloqueioDoProfessorTests e o fio que liga
// o filtro aos 51 POSTs está no GateDoBloqueioDoProfessorTests — aqui se prova o comportamento:
// o que passa, o que para, e o que a pessoa vê quando para.
public class OBloqueioFechaOsPostsDoProfessorTests
{
    // `Controller` é abstrata; o filtro só precisa de algo que carregue TempData.
    private sealed class ControllerDeTeste : Controller { }

    private static readonly DateTime Vencido = new(2025, 01, 01);

    private static PlanoProfessorSettings Cfg => new() { BloqueioAPartirDe = new DateTime(2026, 01, 01) };

    private static Jogador Bloqueado() => new()
    {
        Nome = "Prof", Cpf = "88800000000", IsProfessor = true,
        PlanoProfessor = PlanoDoProfessor.Assinante,
        AssinaturaProfessorPagaAte = Vencido,
    };

    // Monta o contexto de uma ação real do AulasController, no verbo pedido.
    private static ActionExecutingContext Contexto(DbPadelContext ctx, int jogadorId, string verbo, string acao)
    {
        var metodo = typeof(AulasController)
            .GetMethods()
            .First(m => m.Name == acao && m.GetCustomAttributes(typeof(HttpPostAttribute), true).Any());

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, jogadorId.ToString()) }, "Teste")),
        };
        http.Request.Method = verbo;

        var descritor = new ControllerActionDescriptor { MethodInfo = metodo, ActionName = acao };
        var actionContext = new ActionContext(http, new RouteData(), descritor);

        var controller = new ControllerDeTeste();
        controller.ControllerContext = new ControllerContext(actionContext);
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            http, NSubstitute.Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());

        return new ActionExecutingContext(actionContext, new List<IFilterMetadata>(),
            new Dictionary<string, object?>(), controller);
    }

    private static async Task<(bool Seguiu, ActionExecutingContext Ctx)> RodarAsync(
        DbPadelContext ctx, int jogadorId, string verbo, string acao)
    {
        var filtro = new ExigePlanoAtivoAttribute.Filtro(ctx, Options.Create(Cfg));
        var contexto = Contexto(ctx, jogadorId, verbo, acao);

        var seguiu = false;
        await filtro.OnActionExecutionAsync(contexto, () =>
        {
            seguiu = true;
            return Task.FromResult(new ActionExecutedContext(contexto, new List<IFilterMetadata>(), contexto.Controller));
        });

        return (seguiu, contexto);
    }

    [Fact]
    public async Task O_POST_do_professor_bloqueado_para_e_ele_vai_parar_na_tela_do_plano()
    {
        using var ctx = TestInfra.NovoContexto();
        var prof = Bloqueado();
        ctx.Jogadores.Add(prof);
        await ctx.SaveChangesAsync();

        var (seguiu, contexto) = await RodarAsync(ctx, prof.Id, HttpMethods.Post, "AdicionarManual");

        Assert.False(seguiu);

        // ⚠️ PRA TELA DO PLANO, e não pra um 403 seco: a única saída do bloqueio é assinar, e
        // mandar a pessoa pra lugar nenhum a deixaria sem saber o que fazer.
        var destino = Assert.IsType<RedirectToActionResult>(contexto.Result);
        Assert.Equal("PlanoProfessor", destino.ControllerName);
    }

    [Fact]
    public async Task O_GET_do_mesmo_professor_passa_reto()
    {
        using var ctx = TestInfra.NovoContexto();
        var prof = Bloqueado();
        ctx.Jogadores.Add(prof);
        await ctx.SaveChangesAsync();

        // ⚠️ É O PEDIDO LITERAL DO FELIPE: "fica apenas a visualização do que ja esta marcado".
        // O filtro corta por VERBO, e não por lista de ações — é o que garante que nenhuma tela
        // de leitura feche por engano quando alguém acrescentar uma.
        var (seguiu, _) = await RodarAsync(ctx, prof.Id, HttpMethods.Get, "AdicionarManual");

        Assert.True(seguiu);
    }

    [Fact]
    public async Task O_opt_out_do_lado_do_aluno_passa_mesmo_bloqueado()
    {
        using var ctx = TestInfra.NovoContexto();
        var prof = Bloqueado();
        ctx.Jogadores.Add(prof);
        await ctx.SaveChangesAsync();

        // Professor bloqueado que também é aluno de alguém continua marcando a aula DELE.
        var (seguiu, _) = await RodarAsync(ctx, prof.Id, HttpMethods.Post, "Solicitar");

        Assert.True(seguiu);
    }

    [Fact]
    public async Task Quem_nao_e_professor_nunca_e_tocado_pelo_filtro()
    {
        using var ctx = TestInfra.NovoContexto();
        var aluno = new Jogador { Nome = "Aluno", Cpf = "88800000001" };
        ctx.Jogadores.Add(aluno);
        await ctx.SaveChangesAsync();

        // ⚠️ As telas do aluno moram nos MESMOS controllers. Sem esta saída, marcar cada uma com
        // opt-out seria a lista à mão que este desenho existe pra não ter.
        var (seguiu, _) = await RodarAsync(ctx, aluno.Id, HttpMethods.Post, "AdicionarManual");

        Assert.True(seguiu);
    }

    [Fact]
    public async Task Professor_em_dia_passa()
    {
        using var ctx = TestInfra.NovoContexto();
        var emDia = Bloqueado();
        emDia.AssinaturaProfessorPagaAte = DateTime.Now.AddMonths(1);
        ctx.Jogadores.Add(emDia);
        await ctx.SaveChangesAsync();

        var (seguiu, _) = await RodarAsync(ctx, emDia.Id, HttpMethods.Post, "AdicionarManual");

        Assert.True(seguiu);
    }
}
