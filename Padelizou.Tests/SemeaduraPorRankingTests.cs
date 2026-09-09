using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A SEMEADURA POR RANKING, das duas pontas: como o ranking distribui as duplas nos GRUPOS, e
// como os classificados caem nos LADOS do quadro do mata-mata.
//
// 🗣️ Os dois pedidos do Felipe (09/09/2026), no mesmo fôlego:
//   1. "digamos q tenham 9 duplas no torneio e todas rankeadas / Grupo A (1º do ranking, 9º do
//      ranking e 6º) / Grupo B (2º, 8º, 5º) / Grupo C (3º, 7º, 4º)";
//   2. "tem q seguir o chaveamento, que o primeiro do A e primeiro do B (teoricamente os 2
//      melhores rankeados) só se enfrentem na final".
//
// ⚠️ O (2) JÁ ESTAVA CERTO e continua aqui como guarda, não como conserto: `ChaveamentoMataMata`
// separa os lados do quadro desde 05/08/2026. Ele vira teste porque a semeadura dos GRUPOS
// mudou no mesmo dia — e é ela que decide quem classifica em que posição, que é a entrada do
// chaveamento. Quebrar o (2) mexendo no (1) não daria erro nenhum: daria a final adiantada pra
// semifinal, e ninguém descobriria até o dia do torneio.
public class SemeaduraPorRankingTests
{
    [Fact]
    public async Task O_grupo_do_primeiro_do_ranking_leva_o_pior_de_cada_faixa()
    {
        // 🗣️ Felipe, 09/09/2026: "quando houver ranking, seguindo a logica, nos torneios,
        // digamos q tenham 9 duplas no torneio e todas rankeadas / Grupo A (1º do ranking, 9º
        // do ranking e 6º) / Grupo B (2º, 8º, 5º) / Grupo C (3º, 7º, 4º)".
        //
        // 🕳️ O ZIGUE-ZAGUE ANTIGO PUNIA QUEM ESTAVA MELHOR. Ele era a serpentina clássica
        // (A→C, C→A, A→C), e a terceira passada recomeçando em A dava Grupo A = 1º, 6º, 7º e
        // Grupo C = 3º, 4º, 9º: somando as colocações, o grupo do LÍDER era o mais forte dos
        // três (14) e o do 3º cabeça o mais fraco (16). Ser cabeça de chave saía caro.
        //
        // A régua do Felipe é a convenção esportiva: a primeira passada abre os grupos com os
        // melhores (A,B,C) e TODA passada seguinte vem invertida (C,B,A), então o grupo do 1º
        // recebe o pior de cada faixa. O equilíbrio é o mesmo — as somas continuam 14/15/16,
        // só que agora a favor de quem se classificou melhor.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 9);
        var rankDaDupla = await SemearRankingCompletoAsync(ctx, categoria);

        await TestInfra.NovoTorneiosController(ctx, organizador.Id).GerarChaves(torneio.Id);

        var porGrupo = (await ctx.Duplas
                .Where(d => d.CategoriaId == categoria.Id && d.Grupo != null).ToListAsync())
            .GroupBy(d => d.Grupo!)
            .ToDictionary(g => g.Key, g => g.Select(d => rankDaDupla[d.Id]).OrderBy(r => r).ToArray());

        Assert.Equal(new[] { 1, 6, 9 }, porGrupo["A"]);
        Assert.Equal(new[] { 2, 5, 8 }, porGrupo["B"]);
        Assert.Equal(new[] { 3, 4, 7 }, porGrupo["C"]);
    }

    [Fact]
    public async Task Os_dois_melhores_do_ranking_so_se_encontram_na_final()
    {
        // O torneio inteiro, do sorteio à final, com 21 duplas todas rankeadas (7 grupos de 3)
        // — o formato que o Felipe pediu pra conferir. O melhor rankeado vence sempre, que é a
        // premissa do "teoricamente" dele: assim a colocação no ranking é o único critério em
        // jogo, e o caminho de cada um até a final é o desenho puro da chave.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 21);
        var rank = await SemearRankingCompletoAsync(ctx, categoria);
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        await controller.GerarChaves(torneio.Id);
        await controller.AprovarChaves(torneio.Id);

        string? ondeSeEncontraram = null;
        var fasesJogadas = new List<string>();

        // 20 voltas cobre 7 grupos + 4 fases de mata-mata com folga; o laço para sozinho
        // quando não sobra jogo agendado.
        for (int volta = 1; volta <= 20; volta++)
        {
            var pendentes = await ctx.Partidas
                .Where(p => p.TorneioId == torneio.Id && p.Status == "Agendada")
                .ToListAsync();
            if (pendentes.Count == 0) break;

            var fase = pendentes[0].Fase;
            fasesJogadas.Add(fase);

            foreach (var jogo in pendentes.Where(p => p.Fase == fase).ToList())
            {
                int r1 = rank[jogo.Dupla1Id], r2 = rank[jogo.Dupla2Id];
                if (Math.Min(r1, r2) == 1 && Math.Max(r1, r2) == 2) ondeSeEncontraram = fase;

                await TestInfra.FinalizarComPlacarAsync(
                    ctx, controller, jogo, r1 < r2 ? 6 : 0, r1 < r2 ? 0 : 6);
            }
        }

        Assert.Equal("Final", ondeSeEncontraram);

        // E o torneio andou de verdade até lá — sem isto, um mata-mata que travasse nas oitavas
        // passaria no assert de cima por nunca ter posto os dois frente a frente.
        Assert.Contains("Final", fasesJogadas);
        Assert.Contains("Semifinal", fasesJogadas);
    }

    // Dá a cada dupla da categoria um total de pontos DIFERENTE, pelo caminho real que o
    // sorteio lê. A dupla i participa de (n - i) torneios anteriores e participação paga
    // `10 × peso`, então a dupla 0 termina em 1º do ranking. Devolve duplaId → colocação.
    private static async Task<Dictionary<int, int>> SemearRankingCompletoAsync(
        DbPadelContext ctx, Categoria categoria)
    {
        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id)
            .OrderBy(d => d.Id).ToListAsync();

        for (int i = 0; i < duplas.Count; i++)
        {
            for (int n = 0; n < duplas.Count - i; n++)
            {
                var passado = new Torneio
                {
                    Nome = $"Etapa anterior {i}-{n}",
                    Codigo = $"ANT{i:00}{n:00}",
                    Status = "Finalizado",
                    DataInicio = new DateTime(2026, 5, 1, 9, 0, 0),
                };
                ctx.Torneios.Add(passado);
                var cat = new Categoria
                {
                    Nome = "2ª Categoria Masculina",
                    Codigo = $"CAT{i:00}{n:00}",
                    Torneio = passado,
                };
                ctx.Categorias.Add(cat);
                await ctx.SaveChangesAsync();

                ctx.Duplas.Add(new Dupla
                {
                    CategoriaId = cat.Id,
                    Jogador1Id = duplas[i].Jogador1Id,
                    Jogador2Id = duplas[i].Jogador2Id,
                    UltimaFase = "Grupos",
                });
            }
        }
        await ctx.SaveChangesAsync();

        var pontos = await new EstatisticasService(ctx).ObterPontosPorJogadorAsync(
            duplas.SelectMany(d => new[] { d.Jogador1Id, d.Jogador2Id!.Value }));

        return duplas
            .Select(d => (d.Id, Pontos: pontos[d.Jogador1Id] + pontos[d.Jogador2Id!.Value]))
            .OrderByDescending(x => x.Pontos)
            .Select((x, i) => (x.Id, Colocacao: i + 1))
            .ToDictionary(x => x.Id, x => x.Colocacao);
    }
}
