using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Padelizou.Models;
using Xunit;

namespace Padelizou.Tests;

// Desde que a inscrição no torneio passou a exigir login, o caminho "quero me inscrever →
// entrar → voltar pro torneio" ficou comum. Sem o returnUrl, quem entrava caía no perfil e
// tinha que achar o torneio de novo — no meio de uma inscrição, é o bastante pra desistir.
//
// O returnUrl vem da barra de endereços, então ele também é uma porta: sem a checagem de URL
// local, o login viraria trampolim pra outro site ("entre aqui e você cai lá").
public class LoginRetornoTests
{
    private const string Senha = "segredo123";

    private static padelizou.Controllers.AuthController ControllerComJogador(DbPadelContext ctx)
    {
        var jogador = new Jogador { Nome = "Felipe", Cpf = "11144477735", Login = "felipe.bona" };
        jogador.SenhaHash = new PasswordHasher<Jogador>().HashPassword(jogador, Senha);
        ctx.Jogadores.Add(jogador);
        ctx.SaveChanges();

        var controller = TestInfra.NovoAuthController(ctx);

        // SignInAsync procura o IAuthenticationService nos serviços da requisição; sem ele o
        // login estoura antes de chegar no redirecionamento, que é o que este teste mede.
        var servicos = Substitute.For<IServiceProvider>();
        servicos.GetService(typeof(IAuthenticationService)).Returns(Substitute.For<IAuthenticationService>());
        controller.HttpContext.RequestServices = servicos;

        // UrlHelper de verdade, não dublê: quem decide se o destino é de fora é o IsLocalUrl
        // dele, e é exatamente isso que precisa ser exercitado.
        controller.Url = new Microsoft.AspNetCore.Mvc.Routing.UrlHelper(new ActionContext(
            controller.HttpContext,
            new Microsoft.AspNetCore.Routing.RouteData(),
            new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()));

        return controller;
    }

    [Fact]
    public async Task Volta_pro_torneio_de_onde_a_pessoa_veio()
    {
        using var ctx = TestInfra.NovoContexto();
        var controller = ControllerComJogador(ctx);

        var resultado = await controller.Login("felipe.bona", Senha, "/Torneios/Details/17");

        var local = Assert.IsType<LocalRedirectResult>(resultado);
        Assert.Equal("/Torneios/Details/17", local.Url);
    }

    [Fact]
    public async Task Destino_de_fora_do_site_e_ignorado()
    {
        using var ctx = TestInfra.NovoContexto();
        var controller = ControllerComJogador(ctx);

        var resultado = await controller.Login("felipe.bona", Senha, "https://site-de-fora.com/x");

        // Cai no destino padrão, não no site de fora.
        var redirecionamento = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Perfil", redirecionamento.ActionName);
    }

    [Fact]
    public async Task Sem_returnUrl_segue_indo_pro_perfil()
    {
        using var ctx = TestInfra.NovoContexto();
        var controller = ControllerComJogador(ctx);

        var resultado = await controller.Login("felipe.bona", Senha, null);

        var redirecionamento = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Perfil", redirecionamento.ActionName);
    }

    [Fact]
    public async Task Senha_errada_nao_perde_o_destino()
    {
        // Errar a senha uma vez não pode apagar a volta: a pessoa tentaria de novo e
        // terminaria no perfil em vez do torneio que ela queria.
        using var ctx = TestInfra.NovoContexto();
        var controller = ControllerComJogador(ctx);

        var resultado = await controller.Login("felipe.bona", "errada", "/Torneios/Details/17");

        Assert.IsType<ViewResult>(resultado);
        Assert.Equal("/Torneios/Details/17", controller.ViewBag.ReturnUrl);
    }
}
