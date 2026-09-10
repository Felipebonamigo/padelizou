using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — O BALCÃO, AO DAR A QUADRA QUE VAGOU, APAGAVA A QUADRA DO JOGO QUE ACABOU DE JOGAR.
//
// Ensaio do torneio do Er numa app de verdade, no modo "por ordem de liberação" (jogo nasce
// sem quadra; quem dá a quadra é o balcão do check-in, na hora em que a quadra vaga): o jogo
// das 18:00 na Arena 1 terminou, e o balcão deu a Arena 1 ao próximo jogo do mesmo horário.
// `TrocaDeQuadra.QuemOcupa` achou o jogo FINALIZADO como "ocupante" da Arena 1 e a troca de
// quadras aconteceu: o jogo que acabou de jogar ficou com a quadra do outro — nula. Dez jogos
// finalizados terminaram o dia sem quadra, e a mensagem dizia "o BA175B assume a quadra que
// estava livre".
//
// A regra já estava escrita no próprio serviço, só que pra outra pergunta: "jogo já jogado tem
// quadra de HISTÓRIA: foi ali que a bola rolou" (MotivoParaNaoMudar recusa MOVER um jogo
// finalizado). Quem terminou não OCUPA quadra nenhuma: a quadra vagou — é por isso que o
// balcão está dando ela a outro.
public class BalcaoNaoApagaAQuadraDoJogoFinalizadoTests
{
    private static readonly DateTime Dezoito = new(2026, 9, 11, 18, 0, 0);

    private static Partida Jogo(int id, string codigo, string? quadra, string status) => new()
    {
        Id = id, Codigo = codigo, TorneioId = 1, Status = status,
        NomeQuadra = quadra, HorarioPrevisto = Dezoito,
        Dupla1Id = id * 10, Dupla2Id = id * 10 + 1,
    };

    [Fact]
    public void Jogo_finalizado_nao_ocupa_a_quadra()
    {
        var acabou = Jogo(1, "AAA", "Arena 1", "Finalizada");
        var proximo = Jogo(2, "BBB", null, "Agendada");

        Assert.Null(TrocaDeQuadra.QuemOcupa(proximo, "Arena 1", new[] { acabou, proximo }));
    }

    [Fact]
    public void Jogo_em_quadra_agora_continua_ocupando()
    {
        // "A quadra 3 molhou": o jogo em andamento ocupa, e a troca com ele é o que o
        // organizador quer.
        var emQuadra = Jogo(1, "AAA", "Arena 1", "AoVivo");
        var proximo = Jogo(2, "BBB", null, "Agendada");

        Assert.Same(emQuadra, TrocaDeQuadra.QuemOcupa(proximo, "Arena 1", new[] { emQuadra, proximo }));
    }

    // Pelo caminho da Mesa: o POST do balcão.
    [Fact]
    public async Task Dar_a_quadra_que_vagou_ao_proximo_jogo_nao_tira_a_quadra_de_quem_ja_jogou()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4, status: "Fase de Grupos");
        torneio.SemHorarioPrevisto = true;
        ctx.Quadras.AddRange(
            new Quadra { TorneioId = torneio.Id, Nome = "Arena 1" },
            new Quadra { TorneioId = torneio.Id, Nome = "Arena 2" });
        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).OrderBy(d => d.Id).ToListAsync();

        var acabou = new Partida
        {
            TorneioId = torneio.Id, CategoriaId = categoria.Id, Codigo = "ACABOU", Fase = "Grupo A",
            Dupla1Id = duplas[0].Id, Dupla2Id = duplas[1].Id, Status = "Finalizada",
            HorarioPrevisto = Dezoito, NomeQuadra = "Arena 1", VencedorId = duplas[0].Id,
        };
        var proximo = new Partida
        {
            TorneioId = torneio.Id, CategoriaId = categoria.Id, Codigo = "PROXIMO", Fase = "Grupo B",
            Dupla1Id = duplas[2].Id, Dupla2Id = duplas[3].Id, Status = "Agendada",
            HorarioPrevisto = Dezoito, NomeQuadra = null,
        };
        ctx.Partidas.AddRange(acabou, proximo);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.TrocarQuadra(torneio.Id, proximo.Id, "Arena 1");

        Assert.Null(controller.TempData["Erro"]);
        ctx.ChangeTracker.Clear();
        Assert.Equal("Arena 1", (await ctx.Partidas.SingleAsync(p => p.Codigo == "ACABOU")).NomeQuadra);
        Assert.Equal("Arena 1", (await ctx.Partidas.SingleAsync(p => p.Codigo == "PROXIMO")).NomeQuadra);
        Assert.DoesNotContain("assume", (string?)controller.TempData["Sucesso"] ?? "");
    }
}
