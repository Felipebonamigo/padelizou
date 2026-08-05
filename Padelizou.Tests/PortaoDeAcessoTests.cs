using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using padelizou.Controllers;
using Padelizou.Services;

namespace Padelizou.Tests;

// O portão do beta: senha compartilhada, que chega por WhatsApp. O usuário NÃO é segredo — o
// segredo é a senha — então exigir a caixa certa só criaria chamado de suporte (o teclado do
// celular decide sozinho se capitaliza). Além da credencial principal existem as EXTRAS, com
// o mesmo poder, pra dar entrada separada a outra pessoa sem reemitir a senha de todo mundo.
public class PortaoDeAcessoTests
{
    private static AcessoAntecipadoController Controller()
    {
        var settings = new AcessoAntecipadoSettings
        {
            Habilitado = true,
            Usuario = "Corneteiros",
            Senha = "corneta",
            Extras = { new CredencialDoPortao { Usuario = "bonamigo", Senha = "bonamigo" } },
        };
        var contexto = new ControllerContext { HttpContext = new DefaultHttpContext() };
        var controller = new AcessoAntecipadoController(Options.Create(settings), Options.Create(new BetaSettings()))
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

    [Theory]
    [InlineData("bonamigo", "bonamigo")]
    [InlineData("BONAMIGO", "bonamigo")]   // a tolerância de caixa no usuário vale pras extras
    [InlineData(" bonamigo ", " bonamigo ")]
    public void Credencial_extra_abre_o_portao_igual(string usuario, string senha)
    {
        Assert.True(Entrou(Controller().Entrar(usuario, senha, null)));
    }

    [Fact]
    public void Extra_nao_afrouxa_a_principal_nem_o_contrario()
    {
        // Cada dupla é uma dupla: usuário de uma com senha da outra não entra.
        Assert.False(Entrou(Controller().Entrar("bonamigo", "corneta", null)));
        Assert.False(Entrou(Controller().Entrar("Corneteiros", "bonamigo", null)));
        Assert.False(Entrou(Controller().Entrar("bonamigo", "BONAMIGO", null)));
    }

    [Fact]
    public void Extra_sem_senha_nao_vira_porta_escancarada()
    {
        // Um Extras meio preenchido no systemd (usuário sim, senha esquecida) não pode deixar
        // qualquer um entrar deixando o campo em branco.
        var settings = new AcessoAntecipadoSettings
        {
            Habilitado = true,
            Usuario = "Corneteiros",
            Senha = "corneta",
            Extras = { new CredencialDoPortao { Usuario = "meia-boca", Senha = "" } },
        };

        Assert.False(settings.Confere("meia-boca", ""));
        Assert.False(settings.Confere("meia-boca", null));
        Assert.False(settings.Confere("meia-boca", "qualquer"));
        Assert.True(settings.Confere("Corneteiros", "corneta"));
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

    // ---- O aviso de "isto é o ambiente de teste" ----
    // Ele mora no portão porque, depois dele, o dev e o Padelizou de verdade são
    // visualmente idênticos. O erro que evita é caro: inscrever gente, cobrar e marcar
    // torneio num banco que é apagado sem aviso.

    [Fact]
    public void Producao_nao_mostra_aviso_de_ambiente_de_teste()
    {
        // O teste que importa dos dois. A flag nasce FALSA de propósito: quem se declara é
        // a cópia, não o original — se fosse ao contrário, um ambiente novo sem a chave se
        // passaria por produção. E carimbar "isto é teste" no site de verdade espantaria
        // usuário real.
        var controller = ControllerCom(new BetaSettings());

        controller.Entrar(returnUrl: null);

        Assert.False((bool)controller.ViewBag.AmbienteDeTeste);
    }

    [Fact]
    public void Dev_anuncia_que_nao_e_o_padelizou_oficial()
    {
        var controller = ControllerCom(new BetaSettings { AmbienteDeTeste = true });

        controller.Entrar(returnUrl: null);

        Assert.True((bool)controller.ViewBag.AmbienteDeTeste);
    }

    private static AcessoAntecipadoController ControllerCom(BetaSettings beta)
    {
        var settings = new AcessoAntecipadoSettings
        {
            Habilitado = true,
            Usuario = "Corneteiros",
            Senha = "corneta",
        };

        return new AcessoAntecipadoController(Options.Create(settings), Options.Create(beta))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }
}
