using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using padelizou.Controllers;
using Padelizou.Services;

namespace Padelizou.Tests;

// O portão do beta: uma senha só, compartilhada, que chega por WhatsApp. O usuário NÃO é
// segredo — o segredo é a senha — então exigir a caixa certa só criaria chamado de suporte
// (o teclado do celular decide sozinho se capitaliza).
public class PortaoDeAcessoTests
{
    private static AcessoAntecipadoController Controller()
    {
        var settings = new AcessoAntecipadoSettings
        {
            Habilitado = true,
            Usuario = "Corneteiros",
            Senha = "corneta",
        };
        var contexto = new ControllerContext { HttpContext = new DefaultHttpContext() };
        var controller = new AcessoAntecipadoController(Options.Create(settings))
        {
            ControllerContext = contexto,
            // UrlHelper de verdade, não dublê: quem decide se o destino é de fora é o
            // IsLocalUrl dele, e é exatamente isso que um dos testes precisa exercitar.
            Url = new Microsoft.AspNetCore.Mvc.Routing.UrlHelper(new ActionContext(
                contexto.HttpContext,
                new Microsoft.AspNetCore.Routing.RouteData(),
                new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor())),
        };
        return controller;
    }

    private static bool Entrou(IActionResult resultado) => resultado is LocalRedirectResult;

    [Theory]
    [InlineData("Corneteiros")]
    [InlineData("corneteiros")]   // o celular não capitalizou
    [InlineData("CORNETEIROS")]   // o celular capitalizou tudo
    [InlineData("  Corneteiros ")] // copiou da mensagem e veio espaço junto
    public void Usuario_entra_em_qualquer_caixa(string digitado)
    {
        Assert.True(Entrou(Controller().Entrar(digitado, "corneta", null)));
    }

    [Fact]
    public void Espaco_colado_na_senha_nao_recusa_quem_digitou_certo()
    {
        Assert.True(Entrou(Controller().Entrar("Corneteiros", " corneta ", null)));
    }

    [Fact]
    public void A_senha_continua_valendo_com_a_caixa_exata()
    {
        // Só o usuário é tolerante. A senha é o segredo: aceitar "CORNETA" reduziria o
        // espaço de busca de quem tentasse adivinhar.
        Assert.False(Entrou(Controller().Entrar("Corneteiros", "CORNETA", null)));
        Assert.False(Entrou(Controller().Entrar("Corneteiros", "corneta1", null)));
        Assert.False(Entrou(Controller().Entrar("outro", "corneta", null)));
    }

    [Fact]
    public void Campos_vazios_nao_derrubam_a_pagina()
    {
        // O POST pode chegar sem nada (formulário em cache, requisição à mão).
        Assert.False(Entrou(Controller().Entrar(null!, null!, null)));
        Assert.False(Entrou(Controller().Entrar("", "", null)));
    }

    [Fact]
    public void Destino_de_fora_do_site_e_ignorado()
    {
        // returnUrl é do visitante: sem a checagem de URL local, o portão viraria trampolim
        // pra outro site ("entre aqui e você cai lá").
        var resultado = Controller().Entrar("Corneteiros", "corneta", "https://site-de-fora.com/x");

        var local = Assert.IsType<LocalRedirectResult>(resultado);
        Assert.Equal("/", local.Url);
    }
}
