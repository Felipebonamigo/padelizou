using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// Regressão do achado de 26/07: ControlePlacar não checava organizador — qualquer um
// que alcançasse a rota mudava o placar de qualquer jogo.
public class AutorizacaoPlacarTests
{
    private static PartidasController NovoController(DbPadelContext ctx, int? usuarioLogadoId)
    {
        var c = new PartidasController(ctx, Substitute.For<IPalpiteService>());

        var user = usuarioLogadoId == null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.Value.ToString()) }, "Teste"));

        c.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user },
        };
        return c;
    }

    // Monta torneio + 1 partida, devolvendo o organizador e um estranho.
    private static (Partida partida, Jogador organizador, Jogador estranho) MontarPartida(DbPadelContext ctx)
    {
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Fase de Grupos");
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).Take(2).ToList();

        var partida = new Partida
        {
            CategoriaId = categoria.Id,
            TorneioId = torneio.Id,
            Codigo = "P1",
            Fase = "Grupo A",
            Dupla1Id = duplas[0].Id,
            Dupla2Id = duplas[1].Id,
            Status = "AoVivo",
            GamesDupla1 = 0,
            GamesDupla2 = 0,
        };
        ctx.Partidas.Add(partida);

        var estranho = new Jogador { Nome = "Estranho", Cpf = "88888888888" };
        ctx.Jogadores.Add(estranho);
        ctx.SaveChanges();

        return (partida, organizador, estranho);
    }

    [Fact]
    public async Task Estranho_nao_altera_placar_de_torneio_que_nao_organiza()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, _, estranho) = MontarPartida(ctx);

        var controller = NovoController(ctx, estranho.Id);
        var resultado = await controller.ControlePlacar(partida.Id, "AoVivo", 6, 0, null, null);

        Assert.IsType<ForbidResult>(resultado);

        // E o placar continua zerado — não basta devolver Forbid depois de gravar.
        var depois = await ctx.Partidas.FindAsync(partida.Id);
        Assert.Equal(0, depois!.GamesDupla1);
    }

    [Fact]
    public async Task Anonimo_nao_altera_placar()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, _, _) = MontarPartida(ctx);

        var controller = NovoController(ctx, usuarioLogadoId: null);
        var resultado = await controller.ControlePlacar(partida.Id, "AoVivo", 6, 0, null, null);

        Assert.IsType<ForbidResult>(resultado);
    }

    [Fact]
    public async Task Organizador_altera_o_placar_normalmente()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, organizador, _) = MontarPartida(ctx);

        var controller = NovoController(ctx, organizador.Id);
        var resultado = await controller.ControlePlacar(partida.Id, "AoVivo", 6, 3, "Quadra 1", null);

        Assert.IsNotType<ForbidResult>(resultado);

        var depois = await ctx.Partidas.FindAsync(partida.Id);
        Assert.Equal(6, depois!.GamesDupla1);
        Assert.Equal(3, depois.GamesDupla2);
        Assert.Equal("Quadra 1", depois.NomeQuadra);
    }

    [Fact]
    public async Task Tela_de_controle_tambem_barra_quem_nao_organiza()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, organizador, estranho) = MontarPartida(ctx);

        // Não adianta proteger só o POST: a tela mostra os controles e o token.
        var doEstranho = await NovoController(ctx, estranho.Id).ControlePlacar(partida.Id);
        Assert.IsType<ForbidResult>(doEstranho);

        var doOrganizador = await NovoController(ctx, organizador.Id).ControlePlacar(partida.Id);
        Assert.IsType<ViewResult>(doOrganizador);
    }
}
