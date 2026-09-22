using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O AVULSO SAI DE CARTAZ (Felipe, 22/09/2026): *"vamos tirar essa do avulso, apenas marca com
// mensalidade, e nos 15 dias de teste"*.
//
// ⚠️ POR QUE ELE MORREU, e o motivo não é preço: o Avulso cobrava 10% **"por aula paga no
// app"** — e a aula comum NUNCA passa pelo app. Só duas portas geram cobrança de verdade
// (`JogoAulaController:209` e `AulasController.Faturamento:260`); na aula normal o professor
// clica em "recebida", `Aula.PagaEm` é carimbado e o Padelizou recebe R$ 0,00. O Avulso era,
// na prática, o plano grátis pra sempre — e um clique nele tirava o professor do alcance de
// qualquer cobrança futura.
//
// ⚠️ O ENUM `Situacao.Avulso` E A COLUNA CONTINUAM VIVOS, e isso não é sobra esquecida: existe
// professor com `PlanoProfessor = "Avulso"` gravado. Apagar o ramo da tela faria ele cair no
// `default:`, que diz *"você ainda não escolheu um plano"* — e ele escolheu. O que sai é a
// OFERTA; o que fica é o reconhecimento de quem já está lá.
public class OAvulsoSaiDeCartazTests
{
    private static Jogador NovoProfessor(string? plano = null) =>
        new() { Nome = "Prof", Cpf = "11144477735", IsProfessor = true, PlanoProfessor = plano };

    // ── A trava do servidor ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Escolher_o_Avulso_e_recusado()
    {
        using var ctx = TestInfra.NovoContexto();
        var professor = NovoProfessor();
        ctx.Jogadores.Add(professor);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoPlanoProfessorController(ctx, professor.Id);
        var resultado = await controller.Escolher(PlanoDoProfessor.Avulso);

        // ⚠️ A trava é do SERVIDOR, não da tela: some o botão e o POST continua existindo pra
        // quem tem a página velha aberta numa aba — e é exatamente ele que gravaria o plano
        // que acabou de ser aposentado.
        Assert.IsType<BadRequestResult>(resultado);

        var depois = await ctx.Jogadores.FindAsync(professor.Id);
        Assert.Null(depois!.PlanoProfessor);
    }

    [Fact]
    public async Task Escolher_Assinante_continua_funcionando()
    {
        using var ctx = TestInfra.NovoContexto();
        var professor = NovoProfessor();
        ctx.Jogadores.Add(professor);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoPlanoProfessorController(ctx, professor.Id);
        var resultado = await controller.Escolher(PlanoDoProfessor.Assinante);

        // O controle: sem ele, uma trava escrita larga demais (recusar tudo) passaria por
        // "Avulso recusado" e trancaria a única porta que sobrou.
        Assert.IsType<RedirectToActionResult>(resultado);

        var depois = await ctx.Jogadores.FindAsync(professor.Id);
        Assert.Equal(PlanoDoProfessor.Assinante, depois!.PlanoProfessor);
    }

    // ── A tela ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_tela_nao_oferece_mais_o_Avulso()
    {
        var tela = Arquivo(Path.Combine("Views", "PlanoProfessor", "Index.cshtml"));

        // O que importa é o CAMPO que o POST manda, não o rótulo do botão: é ele que grava.
        Assert.DoesNotContain("value=\"@PlanoDoProfessor.Avulso\"", tela);
        Assert.DoesNotContain("Ficar no Avulso", tela);
    }

    [Fact]
    public void Quem_ja_esta_no_Avulso_continua_sendo_reconhecido_na_tela()
    {
        var tela = Arquivo(Path.Combine("Views", "PlanoProfessor", "Index.cshtml"));

        // ⚠️ Este é o teste que impede a limpeza "óbvia" da próxima sessão: apagar o ramo do
        // Avulso junto com o card jogaria quem já escolheu no `default:`, que fala de quem NÃO
        // escolheu. O texto pode mudar; o ramo tem que existir.
        Assert.Contains("PlanoDoProfessor.Situacao.Avulso", tela);
    }

    // ── A última porta: o console do admin ────────────────────────────────────────────────

    [Fact]
    public async Task Admin_nao_pode_mais_atribuir_o_Avulso()
    {
        using var ctx = TestInfra.NovoContexto();
        var raiz = Raiz(ctx);
        var professor = NovoProfessor(PlanoDoProfessor.Assinante);
        ctx.Jogadores.Add(professor);
        await ctx.SaveChangesAsync();

        await Painel(ctx, raiz.Id).DefinirPlanoDoProfessor(professor.Id, PlanoDoProfessor.Avulso);

        // ⚠️ RECUSA, E NÃO "CAI NO DEFAULT". O switch do admin manda tudo o que não reconhece
        // pra `null` ("ainda não escolheu") — então tirar só o braço do Avulso faria a página
        // antiga, aberta numa aba, APAGAR calada o plano de quem já tinha um. Errar apagando
        // dado é pior que recusar, e o teste existe por causa dessa diferença.
        var depois = await ctx.Jogadores.FindAsync(professor.Id);
        Assert.Equal(PlanoDoProfessor.Assinante, depois!.PlanoProfessor);
    }

    [Fact]
    public async Task Admin_continua_podendo_voltar_alguem_para_ainda_nao_escolheu()
    {
        using var ctx = TestInfra.NovoContexto();
        var raiz = Raiz(ctx);
        var professor = NovoProfessor(PlanoDoProfessor.Assinante);
        ctx.Jogadores.Add(professor);
        await ctx.SaveChangesAsync();

        // O controle: a recusa do Avulso não pode levar junto a única forma de desfazer um
        // registro errado — nem a de mover um Avulso antigo pra fora de lá.
        await Painel(ctx, raiz.Id).DefinirPlanoDoProfessor(professor.Id, "");

        Assert.Null((await ctx.Jogadores.FindAsync(professor.Id))!.PlanoProfessor);
    }

    [Fact]
    public void O_painel_do_admin_nao_oferece_mais_o_Avulso()
    {
        var tela = Arquivo(Path.Combine("Views", "Admin", "Professores.cshtml"));

        Assert.DoesNotContain("(PlanoDoProfessor.Avulso, \"Avulso\")", tela);
    }

    private static padelizou.Controllers.AdminController Painel(DbPadelContext ctx, int usuarioLogadoId)
    {
        var controller = new padelizou.Controllers.AdminController(
            ctx,
            NSubstitute.Substitute.For<IPushNotificationService>(),
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(),
            Microsoft.Extensions.Options.Options.Create(new RegistroResultadosSettings()));

        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                new[] { new System.Security.Claims.Claim(
                    System.Security.Claims.ClaimTypes.NameIdentifier, usuarioLogadoId.ToString()) }, "Teste")),
        };
        http.Request.Method = Microsoft.AspNetCore.Http.HttpMethods.Post;

        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext, NSubstitute.Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        controller.Url = TestInfra.UrlDeTeste();
        return controller;
    }

    private static Jogador Raiz(DbPadelContext ctx)
    {
        var raiz = new Jogador
        {
            Nome = "Felipe", Cpf = "11100000000", Login = "raiz", IsAdminGeral = true, IsAdminRaiz = true,
        };
        ctx.Jogadores.Add(raiz);
        ctx.SaveChanges();
        return raiz;
    }

    private static string Arquivo(string caminhoRelativo) =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), caminhoRelativo));

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto Padelizou.");
    }
}
