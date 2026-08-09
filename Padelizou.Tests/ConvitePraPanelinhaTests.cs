using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using padelizou.Controllers;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// Convidar alguém pra PANELINHA (o grupo), pelo sistema.
//
// O convite pelo WhatsApp não passa pelo servidor — é um link montado na tela, e serve pra
// quem ainda NÃO tem conta. Este caminho é o contrário: a pessoa já está no Padelizou, e o
// aviso cai na caixa de entrada dela.
public class ConvitePraPanelinhaTests
{
    private static GruposController Controller(DbPadelContext ctx, int euId, IPushNotificationService push)
    {
        var controller = new GruposController(
            ctx, Substitute.For<ISessaoGrupoService>(), push, NullLogger<GruposController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, euId.ToString()) }, "Teste")),
                },
            },
        };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        controller.Url = TestInfra.UrlDeTeste();
        return controller;
    }

    private static async Task<(GrupoPrivado grupo, Jogador dono, Jogador convidado)> MontarAsync(DbPadelContext ctx)
    {
        var dono = new Jogador { Nome = "Dono da panelinha", Cpf = "88800000001", Login = "dono" };
        var convidado = new Jogador { Nome = "Quem vai ser convidado", Cpf = "88800000002", Login = "convidado" };
        ctx.Jogadores.AddRange(dono, convidado);
        await ctx.SaveChangesAsync();

        var grupo = new GrupoPrivado { Nome = "Pinel Gravatai", CodigoConvite = "QDRE95", AdministradorId = dono.Id };
        ctx.GruposPrivados.Add(grupo);
        await ctx.SaveChangesAsync();

        ctx.JogadoresGrupo.Add(new JogadorGrupo { GrupoId = grupo.Id, JogadorId = dono.Id, PontuacaoInterna = 0 });
        await ctx.SaveChangesAsync();

        return (grupo, dono, convidado);
    }

    [Fact]
    public async Task Convite_avisa_a_pessoa_com_o_LINK_de_entrar()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, dono, convidado) = await MontarAsync(ctx);
        var push = Substitute.For<IPushNotificationService>();

        await Controller(ctx, dono.Id, push).ConvidarParaGrupo(grupo.Id, "convidado");

        await push.Received(1).EnviarParaJogadorAsync(
            convidado.Id,
            Arg.Is<string>(t => t.Contains("panelinha")),
            Arg.Is<string>(c => c.Contains(grupo.Nome)),
            Arg.Any<string?>(),
            Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Acha_a_pessoa_pelo_CPF_com_pontuacao()
    {
        // Quem convida copia o CPF do jeito que tem na mão — do WhatsApp, de um papel.
        using var ctx = TestInfra.NovoContexto();
        var (grupo, dono, convidado) = await MontarAsync(ctx);
        var push = Substitute.For<IPushNotificationService>();

        await Controller(ctx, dono.Id, push).ConvidarParaGrupo(grupo.Id, "888.000.000-02");

        await push.Received(1).EnviarParaJogadorAsync(
            convidado.Id, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Quem_NAO_e_da_panelinha_nao_convida_ninguem()
    {
        // ⚠️ A guarda que importa: sem ela, qualquer pessoa logada mandaria convite em nome de
        // uma panelinha que não é dela — spam saindo com a nossa cara.
        using var ctx = TestInfra.NovoContexto();
        var (grupo, _, convidado) = await MontarAsync(ctx);

        var deFora = new Jogador { Nome = "Estranho", Cpf = "88800000003", Login = "estranho" };
        ctx.Jogadores.Add(deFora);
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();

        var resultado = await Controller(ctx, deFora.Id, push).ConvidarParaGrupo(grupo.Id, "convidado");

        Assert.IsType<ForbidResult>(resultado);
        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!, default, default);
    }

    [Fact]
    public async Task Convidar_quem_JA_esta_dentro_nao_manda_aviso()
    {
        // Aviso repetido pra quem já está no grupo é o começo de todo canal virar spam.
        using var ctx = TestInfra.NovoContexto();
        var (grupo, dono, convidado) = await MontarAsync(ctx);
        ctx.JogadoresGrupo.Add(new JogadorGrupo { GrupoId = grupo.Id, JogadorId = convidado.Id, PontuacaoInterna = 0 });
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();

        await Controller(ctx, dono.Id, push).ConvidarParaGrupo(grupo.Id, "convidado");

        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!, default, default);
    }

    [Fact]
    public async Task Identificador_que_nao_existe_avisa_e_nao_estoura()
    {
        // O caminho comum do erro de digitação — e a mensagem tem que apontar pro convite por
        // WhatsApp, que é o que serve pra quem ainda não tem conta.
        using var ctx = TestInfra.NovoContexto();
        var (grupo, dono, _) = await MontarAsync(ctx);
        var push = Substitute.For<IPushNotificationService>();

        var controller = Controller(ctx, dono.Id, push);
        await controller.ConvidarParaGrupo(grupo.Id, "nao-existe-esse-login");

        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!, default, default);
        Assert.Contains("WhatsApp", controller.TempData["Erro"] as string ?? "");
    }
}
