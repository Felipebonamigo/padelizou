using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A DUPLA NÃO ESPERA A RODADA INTEIRA PRA SABER CONTRA QUEM JOGA.
//
// 🗣️ Felipe, 11/09/2026: *"quando um grupo finalizar os 3 jogos, já coloque eles para a próxima
// fase conforme a classificação, não precisa necessariamente terminar todos os jogos dos outros
// grupos/chaves para ir avançando, ou por exemplo terminou a primeira quarta de final, esse que
// já classificou, já vai a dupla para a semi, mesmo que as outras quartas não tenham finalizado"*.
//
// Até aqui o robô esperava a fase INTEIRA: `AvancoDaChave` devolvia lista vazia se qualquer
// partida da fase não estivesse finalizada, e a Semifinal só nascia quando a última Quartas
// acabasse. Numa categoria de 8 duplas (2 quartas + 2 byes) isso quer dizer que a dupla que
// venceu às 10h ficava sem jogo até as 13h — com o adversário dela já conhecido desde as 10h,
// porque quem a espera FOLGOU a rodada.
//
// ⚠️ O QUE A GEOMETRIA PERMITE, E É MENOS DO QUE A FRASE SUGERE. A rodada seguinte é a lista
// [vencedores na ordem dos jogos, byes] cruzada PRIMEIRO × ÚLTIMO (ChaveamentoMataMata.
// ParearVencedores): a Semifinal 1 é "vencedor da Quartas 1 × última vaga", e não "a primeira
// dupla que classificar". Então o jogo nasce quando as DUAS vagas dele são conhecidas — que
// pode ser uma quarta só, quando a outra vaga é um bye.
//
// ⚠️ E NASCE EM ORDEM DE QUADRO: o jogo 2 de uma fase só nasce depois do 1. A numeração da fase
// é a ordem de criação (ReservasDeHorario.NumeroNaFase), e é dela que saem o desenho da chave
// (Services/OrdemDoQuadro), a procedência da prévia ("Vencedor Semifinal 2") e a reserva de
// horário que o organizador fez naquele jogo previsto. Deixar a Semifinal 2 nascer primeiro
// faria as três apontarem pro jogo errado — o preço de uma vaga adiantada seria a chave inteira
// mentindo.
public class AvancoParcialDaChaveTests
{
    // 8 duplas → 3 grupos (3/3/2) → 6 classificados num quadro de 8: 2 Quartas e 2 byes.
    // É a forma mais comum do Er, e a que mais sofre com a espera: metade do quadro folgou.
    private static async Task<(DbPadelContext Ctx, Categoria Categoria, List<Partida> Quartas,
                               List<int> Byes, Padelizou.Controllers.TorneiosController Controller)>
        AteAsQuartasAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 8);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        // Fase de grupos inteira: vence sempre a dupla de menor Id, 9x3.
        var jogosDeGrupo = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id).OrderBy(p => p.Id).ToListAsync();
        foreach (var jogo in jogosDeGrupo)
        {
            bool venceA1 = jogo.Dupla1Id < jogo.Dupla2Id;
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, venceA1 ? 9 : 3, venceA1 ? 3 : 9);
        }

        var quartas = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Quartas de Final")
            .OrderBy(p => p.Id).ToListAsync();
        var byes = await AvancoDaChave.ByesDaCategoriaAsync(ctx, categoria.Id, TestInfra.SemPontosDoRanking);

        Assert.Equal(2, quartas.Count);
        Assert.Equal(2, byes.Count);

        return (ctx, categoria, quartas, byes, controller);
    }

    private static Task<List<Partida>> SemifinaisAsync(DbPadelContext ctx, int categoriaId) =>
        ctx.Partidas.Where(p => p.CategoriaId == categoriaId && p.Fase == "Semifinal")
            .OrderBy(p => p.Id).ToListAsync();

    // O pedido, na forma que a geometria permite: a Quartas 1 termina e a Semifinal 1 nasce na
    // hora, com a Quartas 2 ainda em quadra. O adversário já era conhecido — é o bye da vaga
    // oposta, que não depende de jogo nenhum.
    [Fact]
    public async Task Terminada_a_quartas_1_a_semifinal_1_nasce_com_a_quartas_2_ainda_em_quadra()
    {
        var (ctx, categoria, quartas, byes, controller) = await AteAsQuartasAsync();
        using var _ = ctx;

        await TestInfra.FinalizarComPlacarAsync(ctx, controller, quartas[0], 9, 3);

        var semis = await SemifinaisAsync(ctx, categoria.Id);
        Assert.Single(semis);

        // A Semifinal 1 é a vaga 0 × a vaga 3 da lista [venc(Q1), venc(Q2), bye1, bye2]:
        // o vencedor da Quartas 1 contra o ÚLTIMO bye.
        Assert.Equal(new HashSet<int> { quartas[0].VencedorId!.Value, byes[1] },
                     new HashSet<int> { semis[0].Dupla1Id, semis[0].Dupla2Id });

        // E a Quartas 2 continua de pé, sem ninguém tê-la ressuscitado nem antecipado.
        var quartasAgora = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Quartas de Final").ToListAsync();
        Assert.Equal(2, quartasAgora.Count);
        Assert.Equal("Agendada", quartasAgora.Single(q => q.Id == quartas[1].Id).Status);
    }

    // A outra metade do quadro continua nascendo: terminada a Quartas 2, a Semifinal 2 entra.
    [Fact]
    public async Task Terminada_a_segunda_quartas_a_semifinal_2_entra_e_o_quadro_fecha()
    {
        var (ctx, categoria, quartas, byes, controller) = await AteAsQuartasAsync();
        using var _ = ctx;

        await TestInfra.FinalizarComPlacarAsync(ctx, controller, quartas[0], 9, 3);
        await TestInfra.FinalizarComPlacarAsync(ctx, controller, quartas[1], 9, 3);

        var semis = await SemifinaisAsync(ctx, categoria.Id);
        Assert.Equal(2, semis.Count);

        Assert.Equal(new HashSet<int> { quartas[0].VencedorId!.Value, byes[1] },
                     new HashSet<int> { semis[0].Dupla1Id, semis[0].Dupla2Id });
        Assert.Equal(new HashSet<int> { quartas[1].VencedorId!.Value, byes[0] },
                     new HashSet<int> { semis[1].Dupla1Id, semis[1].Dupla2Id });
    }

    // ⚠️ A TRAVA DA ORDEM. A Quartas 2 termina PRIMEIRO: a Semifinal 2 tem as duas vagas
    // conhecidas (vencedor da Q2 e o bye 1), mas não pode nascer antes da Semifinal 1 — ela
    // viraria a "Semifinal 1" na numeração, no desenho da chave e na reserva de horário.
    [Fact]
    public async Task A_semifinal_2_nao_nasce_antes_da_1_mesmo_com_as_duas_vagas_conhecidas()
    {
        var (ctx, categoria, quartas, byes, controller) = await AteAsQuartasAsync();
        using var _ = ctx;

        await TestInfra.FinalizarComPlacarAsync(ctx, controller, quartas[1], 9, 3);

        Assert.Empty(await SemifinaisAsync(ctx, categoria.Id));

        // Terminada a Quartas 1, as duas nascem — e na ordem do quadro, não na de quem
        // terminou primeiro.
        await TestInfra.FinalizarComPlacarAsync(ctx, controller, quartas[0], 9, 3);

        var semis = await SemifinaisAsync(ctx, categoria.Id);
        Assert.Equal(2, semis.Count);
        Assert.Contains(quartas[0].VencedorId!.Value, new[] { semis[0].Dupla1Id, semis[0].Dupla2Id });
        Assert.Contains(quartas[1].VencedorId!.Value, new[] { semis[1].Dupla1Id, semis[1].Dupla2Id });
    }

    // O bye não some no meio do caminho. Era esta a armadilha do Interno de 05/08/2026: os byes
    // só contavam enquanto o mata-mata tinha UMA fase, e a Semifinal 1 nascendo cedo apagaria
    // os dois byes da conta — a lista de vagas cairia de 4 pra 2 e a "próxima fase" viraria a
    // Final, montada por cima de uma Semifinal com um jogo só.
    [Fact]
    public async Task Com_a_semifinal_1_no_ar_a_final_nao_nasce_por_cima()
    {
        var (ctx, categoria, quartas, _, controller) = await AteAsQuartasAsync();
        using var _ctx = ctx;

        await TestInfra.FinalizarComPlacarAsync(ctx, controller, quartas[0], 9, 3);

        // A pergunta é refeita a cada finalizar: reabrir e refinalizar a mesma Quartas 1 cai
        // exatamente aqui.
        await TestInfra.FinalizarComPlacarAsync(ctx, controller, quartas[0], 9, 3);

        Assert.Single(await SemifinaisAsync(ctx, categoria.Id));
        Assert.Empty(await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Final").ToListAsync());
    }

    // A prévia continua contando a mesma história: com a Semifinal 1 já real e a 2 por vir, a
    // tela tem que mostrar a Semifinal 2 e a Final — e não parar de projetar porque a fase mais
    // adiantada virou uma semifinal solitária.
    [Fact]
    public async Task Com_meia_semifinal_real_a_previa_ainda_promete_a_outra_e_a_final()
    {
        var (ctx, categoria, quartas, _, controller) = await AteAsQuartasAsync();
        using var _ = ctx;

        await TestInfra.FinalizarComPlacarAsync(ctx, controller, quartas[0], 9, 3);

        var deMataMata = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && (p.Fase == "Quartas de Final" || p.Fase == "Semifinal"))
            .ToListAsync();
        var byesAgora = await AvancoDaChave.ByesDaCategoriaAsync(ctx, categoria.Id, TestInfra.SemPontosDoRanking);

        var previa = ProximasFasesDaChave.Montar(
            deMataMata.Select(p => new ProximasFasesDaChave.PartidaDaChave(
                p.Id, p.Fase, p.Dupla1Id.ToString(), p.Dupla2Id.ToString(), p.HorarioPrevisto)).ToList(),
            byesAgora.Select(b => b.ToString()).ToList(),
            categoria.Nome, categoria.Id);

        var semiPrevista = previa.Rodadas.SingleOrDefault(r => r.Fase == "Semifinal");
        Assert.NotNull(semiPrevista);
        Assert.Single(semiPrevista!.Confrontos);   // só a que falta
        Assert.Equal("Vencedor Quartas de Final 2", semiPrevista.Confrontos[0].Lado1.Rotulo);

        var finalPrevista = previa.Rodadas.SingleOrDefault(r => r.Fase == "Final");
        Assert.NotNull(finalPrevista);
        Assert.Equal("Vencedor Semifinal 1", finalPrevista!.Confrontos[0].Lado1.Rotulo);
        Assert.Equal("Vencedor Semifinal 2", finalPrevista.Confrontos[0].Lado2.Rotulo);
    }
}
