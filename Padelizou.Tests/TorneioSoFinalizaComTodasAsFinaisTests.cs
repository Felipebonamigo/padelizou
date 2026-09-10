using Microsoft.EntityFrameworkCore;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O TORNEIO SÓ VIRA "FINALIZADO" QUANDO ACABA EM TODAS AS CATEGORIAS.
//
// Ensaio do Er (10/09/2026, anomalia C2): a final da 4ª Feminina terminou às 12:10 de sábado
// — 1 de 12 — e o torneio inteiro virou "Finalizado": /Torneios listou o Er em "Finalizados",
// disse "nenhum torneio em andamento", a votação de MVP abriu e a Home do jogador perdeu o
// card "Seus torneios", com ele ainda tendo duas finais por jogar.
//
// A regra: o carimbo da CATEGORIA (campeã com UltimaFase = "Campeao") acontece na hora, por
// categoria; o carimbo do TORNEIO só quando não sobra jogo por jogar em categoria nenhuma.
public class TorneioSoFinalizaComTodasAsFinaisTests
{
    // Duas categorias de 4 duplas: 2 grupos de 2 → Semifinal → Final em cada uma. A segunda é
    // montada à mão, igual ao que TestInfra.MontarTorneio faz com a primeira.
    private static (Torneio torneio, Categoria a, Categoria b, Jogador org) MontarComDuasCategorias(DbPadelContext ctx)
    {
        var (torneio, a, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);

        var b = new Categoria { Nome = "3ª Categoria Feminina", Codigo = "CAT3F", Torneio = torneio };
        ctx.Categorias.Add(b);
        for (int i = 0; i < 4; i++)
        {
            var j1 = TestInfra.NovoJogador(50 + i * 2 + 1);
            var j2 = TestInfra.NovoJogador(50 + i * 2 + 2);
            ctx.Jogadores.AddRange(j1, j2);
            ctx.Duplas.Add(new Dupla { Categoria = b, Jogador1 = j1, Jogador2 = j2 });
        }
        ctx.SaveChanges();

        return (torneio, a, b, org);
    }

    // Joga a categoria até não sobrar jogo (dupla 1 sempre vence); o robô cria cada fase
    // seguinte. `semFinalizarAFase` deixa essa fase em aberto — pra final ser fechada por W.O.
    private static async Task JogarAsync(DbPadelContext ctx, TorneiosController controller,
        int categoriaId, string? semFinalizarAFase = null)
    {
        for (int rodada = 0; rodada < 10; rodada++)
        {
            var porJogar = await ctx.Partidas
                .Where(p => p.CategoriaId == categoriaId && p.Status != "Finalizada"
                            && p.Fase != semFinalizarAFase)
                .ToListAsync();
            if (porJogar.Count == 0) return;

            foreach (var jogo in porJogar)
                await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, 9, 3);
        }
        throw new InvalidOperationException("A categoria não terminou em 10 rodadas.");
    }

    [Fact]
    public async Task Final_da_primeira_categoria_coroa_a_campea_mas_o_torneio_so_finaliza_com_a_ultima()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, a, b, org) = MontarComDuasCategorias(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        // A categoria A até a final, inclusive.
        await JogarAsync(ctx, controller, a.Id);

        var finalDaA = await ctx.Partidas.SingleAsync(p => p.CategoriaId == a.Id && p.Fase == "Final");
        Assert.Equal("Finalizada", finalDaA.Status);

        // O carimbo da CATEGORIA acontece na hora...
        var duplasDaA = await ctx.Duplas.Where(d => d.CategoriaId == a.Id).ToListAsync();
        Assert.Equal(1, duplasDaA.Count(d => d.UltimaFase == "Campeao"));

        // ...mas o do TORNEIO não: a B ainda tem jogo por jogar.
        Assert.True(await ctx.Partidas.AnyAsync(p => p.CategoriaId == b.Id && p.Status != "Finalizada"));
        var depoisDaPrimeiraFinal = await ctx.Torneios.SingleAsync(t => t.Id == torneio.Id);
        Assert.NotEqual("Finalizado", depoisDaPrimeiraFinal.Status);

        // A B até a final: agora sim.
        await JogarAsync(ctx, controller, b.Id);

        var duplasDaB = await ctx.Duplas.Where(d => d.CategoriaId == b.Id).ToListAsync();
        Assert.Equal(1, duplasDaB.Count(d => d.UltimaFase == "Campeao"));
        var depoisDaUltimaFinal = await ctx.Torneios.SingleAsync(t => t.Id == torneio.Id);
        Assert.Equal("Finalizado", depoisDaUltimaFinal.Status);
    }

    // O W.O. NA FINAL passa pelo mesmo encerramento — e pela mesma régua.
    [Fact]
    public async Task Wo_na_final_da_primeira_categoria_tambem_nao_finaliza_o_torneio()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, a, b, org) = MontarComDuasCategorias(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        await JogarAsync(ctx, controller, a.Id, semFinalizarAFase: "Final");
        var finalDaA = await ctx.Partidas.SingleAsync(p => p.CategoriaId == a.Id && p.Fase == "Final");
        Assert.NotEqual("Finalizada", finalDaA.Status);

        var partidas = TestInfra.NovoPartidasController(ctx, org.Id);
        await partidas.RegistrarWo(finalDaA.Id, duplaQueNaoCompareceuId: finalDaA.Dupla2Id);

        Assert.Equal("Finalizada", (await ctx.Partidas.SingleAsync(p => p.Id == finalDaA.Id)).Status);
        Assert.Equal("Campeao", (await ctx.Duplas.SingleAsync(d => d.Id == finalDaA.Dupla1Id)).UltimaFase);
        Assert.NotEqual("Finalizado", (await ctx.Torneios.SingleAsync(t => t.Id == torneio.Id)).Status);

        await JogarAsync(ctx, controller, b.Id);
        Assert.Equal("Finalizado", (await ctx.Torneios.SingleAsync(t => t.Id == torneio.Id)).Status);
    }

    // O irmão do Americano: o DESEMPATE de uma categoria termina, a outra ainda joga.
    [Fact]
    public async Task Desempate_do_americano_numa_categoria_nao_finaliza_o_torneio_com_outra_em_jogo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, a, b, _) = MontarComDuasCategorias(ctx);
        torneio.Formato = FormatoDoTorneio.Americano;
        torneio.Status = "Fase de Grupos";

        var duplasDaA = await ctx.Duplas.Where(d => d.CategoriaId == a.Id).Take(2).ToListAsync();
        var duplasDaB = await ctx.Duplas.Where(d => d.CategoriaId == b.Id).Take(2).ToListAsync();

        var desempateDaA = new Partida
        {
            TorneioId = torneio.Id, CategoriaId = a.Id,
            Dupla1Id = duplasDaA[0].Id, Dupla2Id = duplasDaA[1].Id,
            Fase = TabelaDoAmericano.FaseDesempate, Status = "Finalizada",
            GamesDupla1 = 9, GamesDupla2 = 3, VencedorId = duplasDaA[0].Id, Codigo = "DESA",
        };
        var rodadaDaB = new Partida
        {
            TorneioId = torneio.Id, CategoriaId = b.Id,
            Dupla1Id = duplasDaB[0].Id, Dupla2Id = duplasDaB[1].Id,
            Fase = "Americano - Rodada 1", Status = "Agendada", Codigo = "RODB",
        };
        ctx.Partidas.AddRange(desempateDaA, rodadaDaB);
        await ctx.SaveChangesAsync();

        var encerramento = TestInfra.NovoEncerramento(ctx);
        await encerramento.AplicarAsync(desempateDaA, acabouDeTerminar: true,
            new EncerramentoDaPartida.LinksDoAviso(null, null));

        // A categoria A tem campeão (a linha solo do Americano individual)...
        Assert.True(await ctx.Duplas.AnyAsync(d => d.CategoriaId == a.Id && d.UltimaFase == "Campeao"));
        // ...e o torneio continua em andamento, porque a B ainda tem rodada por jogar.
        Assert.NotEqual("Finalizado", (await ctx.Torneios.SingleAsync(t => t.Id == torneio.Id)).Status);
    }
}
