using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;
using padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// O PORTÃO LIGADO E DESLIGADO DE DENTRO DO APP.
//
// Pedido do Felipe na noite do Interno (05/08/2026) e feito depois: um botão no painel admin
// pra abrir o site. Até então o portão era só `AcessoAntecipado:Habilitado` no systemd, e
// mexer nele exigia ssh no servidor mais restart do serviço — o que não se faz do celular no
// dia do lançamento.
//
// A regra: o systemd dá o PADRÃO, o banco pode dizer o contrário, e o banco ganha.
// (As regras da senha do portão em si estão em PortaoDeAcessoTests.)
public class BotaoDoPortaoTests
{
    private static AcessoAntecipadoSettings Config(bool habilitado) =>
        new() { Habilitado = habilitado, Usuario = "corneteiros", Senha = "corneta" };

    [Fact]
    public void Sem_ninguem_ter_mexido_vale_a_configuracao_do_servidor()
    {
        var portao = new PortaoDeAcesso();

        Assert.True(portao.EstaHabilitado(Config(true)));
        Assert.False(portao.EstaHabilitado(Config(false)));
        Assert.False(portao.DecididoPeloAdmin);
    }

    [Fact]
    public async Task Desligar_pelo_painel_ganha_da_configuracao()
    {
        using var ctx = TestInfra.NovoContexto();
        var portao = new PortaoDeAcesso();

        await portao.DefinirAsync(ctx, habilitado: false, porJogadorId: 1);

        // O servidor continua dizendo "ligado" — e não manda mais.
        Assert.False(portao.EstaHabilitado(Config(true)));
        Assert.True(portao.DecididoPeloAdmin);
    }

    // ⚠️ O TESTE QUE JUSTIFICA A TABELA. Um valor só em memória voltaria ao padrão no
    // primeiro restart — e restart é o que todo deploy faz. O portão reapareceria sozinho
    // dias depois do lançamento, com o site já divulgado, e ninguém ligaria uma coisa à outra.
    [Fact]
    public async Task A_decisao_sobrevive_ao_restart()
    {
        using var ctx = TestInfra.NovoContexto();
        await new PortaoDeAcesso().DefinirAsync(ctx, habilitado: false, porJogadorId: 1);

        // Instância nova = processo novo. É o que o Program.cs faz no start.
        var depoisDoRestart = new PortaoDeAcesso();
        await depoisDoRestart.CarregarAsync(ctx);

        Assert.False(depoisDoRestart.EstaHabilitado(Config(true)));
    }

    [Fact]
    public async Task Ligar_de_volta_tambem_grava()
    {
        using var ctx = TestInfra.NovoContexto();
        var portao = new PortaoDeAcesso();

        await portao.DefinirAsync(ctx, habilitado: false, porJogadorId: 1);
        await portao.DefinirAsync(ctx, habilitado: true, porJogadorId: 1);

        // Uma linha só por chave — o índice único garante no banco, e o serviço precisa
        // reaproveitar a linha em vez de empilhar histórico que ninguém lê.
        Assert.Single(ctx.ConfiguracoesDoSistema.Where(c => c.Chave == PortaoDeAcesso.Chave));

        var depoisDoRestart = new PortaoDeAcesso();
        await depoisDoRestart.CarregarAsync(ctx);
        Assert.True(depoisDoRestart.EstaHabilitado(Config(false)));
    }

    // ⚠️ Só o admin RAIZ. Desligar o portão abre o CADASTRO pro mundo inteiro; administrador
    // nomeado entra pra ajudar na operação do dia a dia, não pra decidir a hora do lançamento.
    [Fact]
    public async Task Administrador_nomeado_nao_mexe_no_portao()
    {
        using var ctx = TestInfra.NovoContexto();
        var nomeado = new Jogador { Nome = "Ajudante", Cpf = "99900000011", IsAdminGeral = true };
        ctx.Jogadores.Add(nomeado);
        await ctx.SaveChangesAsync();

        var portao = new PortaoDeAcesso();
        var resposta = await Controlador(ctx, nomeado.Id)
            .AlternarPortao(portao, habilitar: false);

        Assert.IsType<ForbidResult>(resposta);
        Assert.True(portao.EstaHabilitado(Config(true)));   // nada mudou
        Assert.Empty(ctx.ConfiguracoesDoSistema);
    }

    [Fact]
    public async Task Admin_raiz_desliga_o_portao_pelo_painel()
    {
        using var ctx = TestInfra.NovoContexto();
        var raiz = new Jogador { Nome = "Felipe", Cpf = "99900000001", IsAdminRaiz = true };
        ctx.Jogadores.Add(raiz);
        await ctx.SaveChangesAsync();

        var portao = new PortaoDeAcesso();
        var resposta = await Controlador(ctx, raiz.Id)
            .AlternarPortao(portao, habilitar: false);

        Assert.IsType<RedirectToActionResult>(resposta);
        Assert.False(portao.EstaHabilitado(Config(true)));

        // Fica registrado QUEM mexeu: a pergunta que se faz depois é "por que está aberto?".
        var linha = Assert.Single(ctx.ConfiguracoesDoSistema);
        Assert.Equal(raiz.Id, linha.AtualizadoPorJogadorId);
        Assert.Equal("false", linha.Valor);
    }

    private static AdminController Controlador(DbPadelContext ctx, int usuarioLogadoId)
    {
        var controller = new AdminController(
            ctx,
            Substitute.For<IPushNotificationService>(),
            new ConfigurationBuilder().Build(),
            Options.Create(new RegistroResultadosSettings()));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.ToString()) }, "Teste")),
            },
        };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        return controller;
    }
}
