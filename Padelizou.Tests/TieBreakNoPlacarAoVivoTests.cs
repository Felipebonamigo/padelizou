using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A CONTAGEM DO TIE-BREAK CHEGANDO AO BANCO pelo POST em lote do card ao vivo — o caminho que
// o −/+ do tie-break usa (Felipe, 12/09/2026).
//
// ⚠️ Os pontos viajam no MESMO POST dos games, em arrays paralelos casados por ÍNDICE. É a
// parte que pode quebrar calada: um array mais curto, ou um campo que só existe em alguns
// cards, grava o tie-break de um jogo no outro.
public class TieBreakNoPlacarAoVivoTests
{
    private static async Task<(DbPadelContext ctx, Torneio torneio, List<Partida> aoVivo, Jogador org)>
        ComJogosNoArAsync(int quantos = 2, int gamesDaFase = 9, int pontosDoTieBreak = 7)
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 8);
        torneio.QuantidadeQuadras = 5;
        torneio.GamesFaseGrupos = gamesDaFase;
        torneio.PontosTieBreakGrupos = pontosDoTieBreak;
        await ctx.SaveChangesAsync();

        var doTorneio = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doTorneio.GerarChaves(torneio.Id);

        var partidas = TestInfra.NovoPartidasController(ctx, org.Id);
        var aoVivo = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id)
            .OrderBy(p => p.Id).Take(quantos).ToListAsync();
        foreach (var jogo in aoVivo) await partidas.ColocarNoAr(jogo.Id);

        return (ctx, torneio, aoVivo, org);
    }

    [Fact]
    public async Task O_lote_grava_os_pontos_do_tie_break()
    {
        var (ctx, torneio, aoVivo, org) = await ComJogosNoArAsync(2);
        using var _ = ctx;

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.SalvarPlacaresAoVivo(
            torneio.Id, aoVivo.Select(p => p.Id).ToArray(),
            games1: new[] { 8, 3 }, games2: new[] { 8, 1 },
            voltarPara: null,
            pontos1: new[] { 5, 0 }, pontos2: new[] { 3, 0 });

        var emTieBreak = await ctx.Partidas.FindAsync(aoVivo[0].Id);
        Assert.Equal(5, emTieBreak!.PontosTieBreak1);
        Assert.Equal(3, emTieBreak.PontosTieBreak2);
        Assert.Equal("AoVivo", emTieBreak.Status);   // contar ponto não encerra jogo
    }

    [Fact]
    public async Task Mexer_SO_nos_pontos_ja_conta_como_placar_salvo()
    {
        // ⚠️ O laço do lote tinha um atalho: "games iguais aos gravados? pula". Sem cuidar
        // dele, o ponto do tie-break seria ignorado em 8x8 — que é justamente o placar em que
        // os games PARAM de mudar e só o tie-break anda. O toque no "+" não gravaria nada.
        var (ctx, torneio, aoVivo, org) = await ComJogosNoArAsync(1);
        using var _ = ctx;

        var jogo = aoVivo[0];
        jogo.GamesDupla1 = 8;
        jogo.GamesDupla2 = 8;
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.SalvarPlacaresAoVivo(
            torneio.Id, new[] { jogo.Id },
            games1: new[] { 8 }, games2: new[] { 8 },
            voltarPara: null,
            pontos1: new[] { 1 }, pontos2: new[] { 0 });

        var depois = await ctx.Partidas.FindAsync(jogo.Id);
        Assert.Equal(1, depois!.PontosTieBreak1);
        Assert.Equal(0, depois.PontosTieBreak2);
    }

    [Fact]
    public async Task O_fechamento_grava_o_9o_game_e_os_pontos_no_mesmo_POST()
    {
        // É o que o botão "Fechar o tie-break em 9 x 8" manda: games novos e pontos juntos.
        // ⚠️ E NÃO finaliza: encerrar continua sendo o Finalizar, com a confirmação dele.
        var (ctx, torneio, aoVivo, org) = await ComJogosNoArAsync(1);
        using var _ = ctx;

        var jogo = aoVivo[0];
        jogo.GamesDupla1 = 8;
        jogo.GamesDupla2 = 8;
        jogo.PontosTieBreak1 = 6;
        jogo.PontosTieBreak2 = 5;
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.SalvarPlacaresAoVivo(
            torneio.Id, new[] { jogo.Id },
            games1: new[] { 9 }, games2: new[] { 8 },
            voltarPara: null,
            pontos1: new[] { 7 }, pontos2: new[] { 5 });

        var depois = await ctx.Partidas.FindAsync(jogo.Id);
        Assert.Equal(9, depois!.GamesDupla1);
        Assert.Equal(8, depois.GamesDupla2);
        Assert.Equal(7, depois.PontosTieBreak1);
        Assert.Equal(5, depois.PontosTieBreak2);
        Assert.Equal("AoVivo", depois.Status);
    }

    [Fact]
    public async Task Torneio_com_tie_break_desligado_nao_grava_ponto_nenhum()
    {
        // POST montado à mão contra um torneio que não usa tie-break: nada entra.
        var (ctx, torneio, aoVivo, org) = await ComJogosNoArAsync(1, pontosDoTieBreak: TieBreakDoJogo.Desligado);
        using var _ = ctx;

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.SalvarPlacaresAoVivo(
            torneio.Id, new[] { aoVivo[0].Id },
            games1: new[] { 8 }, games2: new[] { 8 },
            voltarPara: null,
            pontos1: new[] { 5 }, pontos2: new[] { 3 });

        var depois = await ctx.Partidas.FindAsync(aoVivo[0].Id);
        Assert.Null(depois!.PontosTieBreak1);
        Assert.Null(depois.PontosTieBreak2);
    }

    [Fact]
    public async Task Fase_de_numero_PAR_nao_grava_ponto_nenhum()
    {
        // Num jogo até 4 o desempate é o 5º game, não um tie-break — a régua diz que ali ele
        // nem pode acontecer, e o POST obedece à régua.
        var (ctx, torneio, aoVivo, org) = await ComJogosNoArAsync(1, gamesDaFase: 4, pontosDoTieBreak: 7);
        using var _ = ctx;

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.SalvarPlacaresAoVivo(
            torneio.Id, new[] { aoVivo[0].Id },
            games1: new[] { 3 }, games2: new[] { 3 },
            voltarPara: null,
            pontos1: new[] { 5 }, pontos2: new[] { 3 });

        var depois = await ctx.Partidas.FindAsync(aoVivo[0].Id);
        Assert.Null(depois!.PontosTieBreak1);
        Assert.Null(depois.PontosTieBreak2);
    }

    [Fact]
    public async Task Ponto_absurdo_e_cortado_no_teto()
    {
        var (ctx, torneio, aoVivo, org) = await ComJogosNoArAsync(1);
        using var _ = ctx;

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.SalvarPlacaresAoVivo(
            torneio.Id, new[] { aoVivo[0].Id },
            games1: new[] { 8 }, games2: new[] { 8 },
            voltarPara: null,
            pontos1: new[] { 4000 }, pontos2: new[] { -7 });

        var depois = await ctx.Partidas.FindAsync(aoVivo[0].Id);
        Assert.Equal(TieBreakDoJogo.TetoDosPontos, depois!.PontosTieBreak1);
        Assert.Equal(0, depois.PontosTieBreak2);
    }

    [Fact]
    public async Task Tela_aberta_antes_do_deploy_nao_apaga_um_tie_break_em_andamento()
    {
        // A aba que já estava aberta não tem os campos novos: os pontos chegam NULOS. Tratar
        // isso como zero apagaria, no primeiro toque no game, a contagem que está na quadra.
        var (ctx, torneio, aoVivo, org) = await ComJogosNoArAsync(1);
        using var _ = ctx;

        var jogo = aoVivo[0];
        jogo.PontosTieBreak1 = 5;
        jogo.PontosTieBreak2 = 4;
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.SalvarPlacaresAoVivo(
            torneio.Id, new[] { jogo.Id }, games1: new[] { 8 }, games2: new[] { 8 });

        var depois = await ctx.Partidas.FindAsync(jogo.Id);
        Assert.Equal(5, depois!.PontosTieBreak1);
        Assert.Equal(4, depois.PontosTieBreak2);
    }

    [Fact]
    public async Task Array_de_pontos_mais_curto_que_o_de_jogos_nao_estoura()
    {
        // POST recortado (tela antiga, requisição montada à mão): o que falta fica como está,
        // em vez de derrubar o salvamento dos games de todas as quadras.
        var (ctx, torneio, aoVivo, org) = await ComJogosNoArAsync(2);
        using var _ = ctx;

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.SalvarPlacaresAoVivo(
            torneio.Id, aoVivo.Select(p => p.Id).ToArray(),
            games1: new[] { 8, 5 }, games2: new[] { 8, 2 },
            voltarPara: null,
            pontos1: new[] { 3 }, pontos2: new[] { 2 });

        var primeiro = await ctx.Partidas.FindAsync(aoVivo[0].Id);
        var segundo = await ctx.Partidas.FindAsync(aoVivo[1].Id);

        Assert.Equal(3, primeiro!.PontosTieBreak1);
        Assert.Null(segundo!.PontosTieBreak1);
        Assert.Equal(5, segundo.GamesDupla1);     // e o placar de games do segundo entrou
    }
}
