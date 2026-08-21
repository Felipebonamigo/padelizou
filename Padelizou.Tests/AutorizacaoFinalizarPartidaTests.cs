using Microsoft.AspNetCore.Mvc;
using Padelizou.Controllers;
using Padelizou.Models;

namespace Padelizou.Tests;

// Regressão do achado de 21/08: FinalizarPartida era a ÚNICA ação de placar sem
// [HttpPost]/[Authorize] — respondia a GET e escapava do carimbo antifalsificação global. E a
// checagem de autorização só rodava quando partida.TorneioId != null, então uma partida com
// TorneioId nulo (nunca acontece hoje, mas o tipo permite) PULAVA a checagem em vez de ser
// recusada por ela. Ver TorneiosController.Placar.cs:FinalizarPartida.
public class AutorizacaoFinalizarPartidaTests
{
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
            GamesDupla1 = 6,
            GamesDupla2 = 3,
        };
        ctx.Partidas.Add(partida);

        var estranho = new Jogador { Nome = "Estranho", Cpf = "88888888888" };
        ctx.Jogadores.Add(estranho);
        ctx.SaveChanges();

        return (partida, organizador, estranho);
    }

    [Fact]
    public async Task Estranho_nao_finaliza_partida_de_torneio_que_nao_organiza()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, _, estranho) = MontarPartida(ctx);

        var controller = TestInfra.NovoTorneiosController(ctx, estranho.Id);
        var resultado = await controller.FinalizarPartida(partida.Id);

        Assert.IsType<ForbidResult>(resultado);

        // Forbid depois de já ter finalizado não desfaz nada — a checagem tem que vir antes.
        var depois = await ctx.Partidas.FindAsync(partida.Id);
        Assert.Equal("AoVivo", depois!.Status);
    }

    [Fact]
    public async Task Organizador_finaliza_normalmente()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, organizador, _) = MontarPartida(ctx);

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        var resultado = await controller.FinalizarPartida(partida.Id);

        Assert.IsNotType<ForbidResult>(resultado);

        var depois = await ctx.Partidas.FindAsync(partida.Id);
        Assert.Equal("Finalizada", depois!.Status);
    }

    // O tipo permite TorneioId nulo (não acontece hoje — toda Partida nasce de um torneio), mas
    // se acontecesse a checagem tinha que RECUSAR, não pular a autorização.
    [Fact]
    public async Task Partida_sem_torneio_e_recusada_em_vez_de_pular_a_checagem()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, organizador, _) = MontarPartida(ctx);
        partida.TorneioId = null;
        ctx.SaveChanges();

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        var resultado = await controller.FinalizarPartida(partida.Id);

        Assert.IsType<ForbidResult>(resultado);

        var depois = await ctx.Partidas.FindAsync(partida.Id);
        Assert.Equal("AoVivo", depois!.Status);
    }
}
