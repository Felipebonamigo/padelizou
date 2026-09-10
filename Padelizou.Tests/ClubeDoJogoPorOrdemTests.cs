using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// NO "POR ORDEM" A QUADRA NÃO PRECISA EXISTIR, MAS O CLUBE SEMPRE — e é o MOTOR que decide.
//
// 🗣️ Felipe, 10/09/2026: *"mesmo assim, tem q o sistema mesmo definir o que irão para o 'radar'
// no sabado de manhã, nao precisa ter a quadra definida, mas o clube sempre tem q estar
// definido"*.
//
// 🕳️ O MOTOR JÁ DECIDIA E JOGAVA FORA. `GradeDeJogos.Encaixar` escolhe a quadra de cada jogo — a
// janela do Radar no sábado de manhã, o "cede a quadra de casa" —, e a quadra carrega o clube. O
// modo "por ordem" apagava a quadra (`OrdemDeLiberacao.ApagarAsQuadras`, porque quem chama é a
// Mesa) e, sem coluna própria, o clube ia junto. A tela ficava muda em 97 jogos.
//
// ✅ `Partida.ClubeId` nasce pra guardar a decisão. É carimbado a partir da quadra escolhida ANTES
// de a quadra ser apagada — no "por ordem" e fora dele —, e a etiqueta lê o carimbo primeiro:
// é o dado mais específico que sobrevive.
public class ClubeDoJogoPorOrdemTests
{
    private const int ErPadel = 10;
    private const int Radar = 20;

    [Fact]
    public async Task Sortear_por_ordem_com_dois_clubes_deixa_todo_jogo_sem_quadra_mas_com_clube()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 12);

        ctx.Clubes.AddRange(
            new Clube { Id = ErPadel, Nome = "Er Padel", Endereco = "-", Contato = "-" },
            new Clube { Id = Radar, Nome = "Radar Esportes", Endereco = "-", Contato = "-" });
        torneio.ClubeId = ErPadel;
        torneio.SemHorarioPrevisto = true;              // por ordem: a Mesa chama
        torneio.QuantidadeQuadras = 2;
        ctx.Quadras.AddRange(
            new Quadra { TorneioId = torneio.Id, Nome = "Loja 7", ClubeId = null },
            new Quadra { TorneioId = torneio.Id, Nome = "Radar 1", ClubeId = Radar });
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();
        Assert.NotEmpty(jogos);

        Assert.All(jogos, j => Assert.Null(j.NomeQuadra));          // quadra: a Mesa decide
        Assert.All(jogos, j => Assert.NotNull(j.ClubeId));          // clube: o motor já decidiu

        // E os dois clubes aparecem: com a casa cheia, o motor mandou jogo pro Radar.
        Assert.Contains(jogos, j => j.ClubeId == Radar);
        Assert.Contains(jogos, j => j.ClubeId == ErPadel);
    }

    [Fact]
    public async Task Fora_do_por_ordem_o_clube_tambem_e_carimbado()
    {
        // O carimbo não é exclusividade do "por ordem": todo jogo com quadra escolhida ganha o
        // clube dela. É o que faz a etiqueta não depender de resolver nome de quadra em tela.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);

        ctx.Clubes.Add(new Clube { Id = ErPadel, Nome = "Er Padel", Endereco = "-", Contato = "-" });
        torneio.ClubeId = ErPadel;
        torneio.QuantidadeQuadras = 2;
        ctx.Quadras.AddRange(
            new Quadra { TorneioId = torneio.Id, Nome = "Loja 7", ClubeId = null },
            new Quadra { TorneioId = torneio.Id, Nome = "Nclass", ClubeId = null });
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var jogos = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();
        Assert.All(jogos, j => Assert.NotNull(j.NomeQuadra));
        Assert.All(jogos, j => Assert.Equal(ErPadel, j.ClubeId));
    }
}
