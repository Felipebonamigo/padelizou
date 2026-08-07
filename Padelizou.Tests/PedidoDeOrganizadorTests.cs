using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;
using padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;
using Estado = Padelizou.Services.PermissaoDeOrganizador.EstadoDoPedido;

namespace Padelizou.Tests;

// PEDIR A LIBERAÇÃO DO TORNEIO OFICIAL PELO APP.
//
// Pedido do Felipe (08/08/2026, dia do lançamento): *"caso a pessoa não tenha autorização
// para criar torneio que não seja americano, coloque um botão lá, solicitar permissão"*.
//
// Antes a recusa terminava em "fale com a gente pelo WhatsApp". Funciona, e vaza gente: quem
// decidiu criar um torneio às 23h de um domingo não abre conversa com desconhecido — fecha o
// app e não volta. O pedido de um toque transforma a recusa numa FILA, que é o que o admin
// resolve em dois minutos.
public class PedidoDeOrganizadorTests
{
    private static Jogador Comum() => new() { Id = 1, Nome = "Fulano", Cpf = "99900000001" };

    // ── A régua ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Os_quatro_estados_do_pedido()
    {
        Assert.Equal(Estado.NuncaPediu, PermissaoDeOrganizador.EstadoDe(Comum()));

        var esperando = Comum();
        esperando.SolicitouOrganizadorEm = DateTime.Now;
        Assert.Equal(Estado.Esperando, PermissaoDeOrganizador.EstadoDe(esperando));

        var recusado = Comum();
        recusado.PedidoDeOrganizadorRecusadoEm = DateTime.Now;
        Assert.Equal(Estado.Recusado, PermissaoDeOrganizador.EstadoDe(recusado));

        var liberado = Comum();
        liberado.IsOrganizadorTorneio = true;
        Assert.Equal(Estado.PodeCriar, PermissaoDeOrganizador.EstadoDe(liberado));
    }

    // ⚠️ Quem JÁ PODE criar não entra na fila: sem isto o admin abriria a tela, veria um
    // pedido e gastaria o tempo dele pra descobrir que não havia nada a decidir.
    [Fact]
    public void Quem_ja_pode_criar_nao_pede()
    {
        var liberado = Comum();
        liberado.IsOrganizadorTorneio = true;

        Assert.NotNull(PermissaoDeOrganizador.MotivoParaNaoPedir(liberado));
        Assert.Null(PermissaoDeOrganizador.MotivoParaNaoPedir(Comum()));
    }

    [Fact]
    public void Quem_ja_esta_na_fila_nao_pede_de_novo()
    {
        var esperando = Comum();
        esperando.SolicitouOrganizadorEm = DateTime.Now;

        Assert.NotNull(PermissaoDeOrganizador.MotivoParaNaoPedir(esperando));
    }

    // Recusado PODE pedir de novo: um "não" de hoje não é sentença perpétua — o clube dela
    // pode crescer no mês que vem.
    [Fact]
    public void Recusado_pode_pedir_de_novo()
    {
        var recusado = Comum();
        recusado.PedidoDeOrganizadorRecusadoEm = DateTime.Now;

        Assert.Null(PermissaoDeOrganizador.MotivoParaNaoPedir(recusado));
    }

    [Fact]
    public void Cada_estado_tem_frase_propria()
    {
        // Dois estados com a mesma frase deixariam a pessoa sem saber se o pedido foi ou não.
        var frases = Enum.GetValues<Estado>().Select(TextoDoPedidoDeOrganizador.Frase).ToList();

        Assert.All(frases, f => Assert.False(string.IsNullOrWhiteSpace(f)));
        Assert.Equal(frases.Count, frases.Distinct().Count());
    }

    // ── O caminho de verdade ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Pedir_poe_na_fila_e_avisa_o_admin()
    {
        using var ctx = TestInfra.NovoContexto();
        var jogador = Comum();
        var raiz = new Jogador { Id = 9, Nome = "Felipe", Cpf = "99900000009", IsAdminRaiz = true };
        ctx.Jogadores.AddRange(jogador, raiz);
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        await TestInfra.NovoTorneiosController(ctx, jogador.Id, push: push)
            .SolicitarPerfilDeOrganizador("sou professor no Batata Padel");

        var salvo = await ctx.Jogadores.FindAsync(jogador.Id);
        Assert.NotNull(salvo!.SolicitouOrganizadorEm);
        Assert.Equal("sou professor no Batata Padel", salvo.MotivoDoPedidoDeOrganizador);
        Assert.False(salvo.IsOrganizadorTorneio);   // pedir não libera nada sozinho

        // O admin precisa SABER que tem fila — senão ela só é descoberta por acaso.
        await push.Received(1).EnviarParaJogadorAsync(raiz.Id, "Alguém quer criar torneios",
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Motivo_gigante_nao_faz_o_pedido_se_perder()
    {
        // O campo é opcional e a coluna aceita 500. Recusar por excesso faria a pessoa perder
        // o pedido por ter escrito demais — corta e segue.
        using var ctx = TestInfra.NovoContexto();
        var jogador = Comum();
        ctx.Jogadores.Add(jogador);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, jogador.Id)
            .SolicitarPerfilDeOrganizador(new string('x', 900));

        var salvo = await ctx.Jogadores.FindAsync(jogador.Id);
        Assert.NotNull(salvo!.SolicitouOrganizadorEm);
        Assert.Equal(500, salvo.MotivoDoPedidoDeOrganizador!.Length);
    }

    [Fact]
    public async Task Liberar_tira_o_pedido_da_fila()
    {
        using var ctx = TestInfra.NovoContexto();
        var jogador = Comum();
        jogador.SolicitouOrganizadorEm = DateTime.Now;
        jogador.PedidoDeOrganizadorRecusadoEm = DateTime.Now.AddDays(-30);
        var raiz = new Jogador { Id = 9, Nome = "Felipe", Cpf = "99900000009", IsAdminRaiz = true };
        ctx.Jogadores.AddRange(jogador, raiz);
        await ctx.SaveChangesAsync();

        await Admin(ctx, raiz.Id).DarPerfilDeOrganizador(jogador.Id);

        var salvo = await ctx.Jogadores.FindAsync(jogador.Id);
        Assert.True(salvo!.IsOrganizadorTorneio);
        // Fica na fila = admin decide duas vezes a mesma coisa.
        Assert.Null(salvo.SolicitouOrganizadorEm);
        Assert.Null(salvo.PedidoDeOrganizadorRecusadoEm);
    }

    [Fact]
    public async Task Recusar_tira_da_fila_sem_fechar_a_porta()
    {
        using var ctx = TestInfra.NovoContexto();
        var jogador = Comum();
        jogador.SolicitouOrganizadorEm = DateTime.Now;
        var raiz = new Jogador { Id = 9, Nome = "Felipe", Cpf = "99900000009", IsAdminRaiz = true };
        ctx.Jogadores.AddRange(jogador, raiz);
        await ctx.SaveChangesAsync();

        await Admin(ctx, raiz.Id).RecusarPedidoDeOrganizador(jogador.Id);

        var salvo = await ctx.Jogadores.FindAsync(jogador.Id);
        Assert.Null(salvo!.SolicitouOrganizadorEm);
        Assert.NotNull(salvo.PedidoDeOrganizadorRecusadoEm);
        Assert.False(salvo.IsOrganizadorTorneio);

        // E ela pode pedir de novo — o pedido novo limpa a recusa, senão o admin veria um
        // pedido fresco já marcado como negado.
        await TestInfra.NovoTorneiosController(ctx, jogador.Id).SolicitarPerfilDeOrganizador("agora com clube fixo");

        var depois = await ctx.Jogadores.FindAsync(jogador.Id);
        Assert.NotNull(depois!.SolicitouOrganizadorEm);
        Assert.Null(depois.PedidoDeOrganizadorRecusadoEm);
    }

    [Fact]
    public async Task So_o_admin_raiz_decide()
    {
        using var ctx = TestInfra.NovoContexto();
        var jogador = Comum();
        jogador.SolicitouOrganizadorEm = DateTime.Now;
        var nomeado = new Jogador { Id = 5, Nome = "Ajudante", Cpf = "99900000005", IsAdminGeral = true };
        ctx.Jogadores.AddRange(jogador, nomeado);
        await ctx.SaveChangesAsync();

        Assert.IsType<ForbidResult>(await Admin(ctx, nomeado.Id).DarPerfilDeOrganizador(jogador.Id));
        Assert.IsType<ForbidResult>(await Admin(ctx, nomeado.Id).RecusarPedidoDeOrganizador(jogador.Id));

        var salvo = await ctx.Jogadores.FindAsync(jogador.Id);
        Assert.False(salvo!.IsOrganizadorTorneio);
        Assert.NotNull(salvo.SolicitouOrganizadorEm);   // nada mudou
    }

    private static AdminController Admin(DbPadelContext ctx, int usuarioLogadoId)
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
        controller.Url = TestInfra.UrlDeTeste();
        return controller;
    }
}
