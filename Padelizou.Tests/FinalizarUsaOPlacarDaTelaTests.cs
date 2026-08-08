using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// FINALIZAR ENCERRA COM O QUE ESTÁ NA TELA (Felipe, 08/08/2026).
//
// O relato: "eu marquei 6 x 1 e ele apareceu essa tela" — a confirmação oferecia encerrar
// **0 x 1**, o placar GRAVADO, ignorando o que ele acabara de digitar no card.
//
// A causa tinha duas metades, e as duas vinham da mesma premissa envelhecida. O comentário da
// view dizia "o placar já está na tela e JÁ FOI SALVO (cada toque no −/+ grava)" — verdade
// quando foi escrito, mentira depois que a edição virou EM LOTE ("Salvar placares"). A partir
// dali os números editáveis passaram a pertencer ao formulário do lote (form=
// "pdzPlacaresAoVivo") e o Finalizar mandava só o `partidaId`: encerrava com o banco.
//
// ⚠️ É a família de bug que este repo já conhece: DUAS cópias do mesmo número, e a que o
// organizador estava OLHANDO era a que não valia.
public class FinalizarUsaOPlacarDaTelaTests
{
    private static async Task<(DbPadelContext ctx, Torneio torneio, Partida jogo, Jogador org)>
        ComUmJogoNoArAsync(int gamesDaFase = 9, string contagem = ContagemDeGamesDoTorneio.Ate)
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 8);
        torneio.GamesFaseGrupos = gamesDaFase;
        torneio.ContagemDeGames = contagem;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var jogo = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).OrderBy(p => p.Id).FirstAsync();
        await TestInfra.NovoPartidasController(ctx, org.Id).ColocarNoAr(jogo.Id);

        return (ctx, torneio, jogo, org);
    }

    [Fact]
    public async Task O_placar_digitado_no_card_e_o_que_encerra_o_jogo()
    {
        // O caso exato do relato: o banco tem 0 x 1 (só o 1 chegou a ser salvo) e o
        // organizador digitou 6 x 1 antes de apertar Finalizar.
        var (ctx, _, jogo, org) = await ComUmJogoNoArAsync();
        using var _c = ctx;

        jogo.GamesDupla1 = 0;
        jogo.GamesDupla2 = 1;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .FinalizarPartida(jogo.Id, voltarPara: null, games1: 6, games2: 1);

        var depois = await ctx.Partidas.FirstAsync(p => p.Id == jogo.Id);
        Assert.Equal(6, depois.GamesDupla1);
        Assert.Equal(1, depois.GamesDupla2);
        Assert.Equal("Finalizada", depois.Status);
        Assert.Equal(depois.Dupla1Id, depois.VencedorId);   // 6 x 1 é vitória da dupla 1
    }

    [Fact]
    public async Task Sem_placar_na_requisicao_vale_o_que_esta_gravado()
    {
        // A Mesa de Controle e a fila offline finalizam sem mandar placar — elas já gravaram
        // antes. Passar a exigir o número quebraria as duas.
        var (ctx, _, jogo, org) = await ComUmJogoNoArAsync();
        using var _c = ctx;

        jogo.GamesDupla1 = 9;
        jogo.GamesDupla2 = 4;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).FinalizarPartida(jogo.Id);

        var depois = await ctx.Partidas.FirstAsync(p => p.Id == jogo.Id);
        Assert.Equal(9, depois.GamesDupla1);
        Assert.Equal(4, depois.GamesDupla2);
        Assert.Equal("Finalizada", depois.Status);
    }

    [Fact]
    public async Task O_empate_e_recusado_pelo_placar_NOVO_e_nao_pelo_velho()
    {
        // A ordem importa: o placar da tela entra ANTES da recusa por empate. Se entrasse
        // depois, o organizador que corrigiu 3 x 3 para 5 x 3 seria barrado por um empate que
        // não existe mais — e ele estaria vendo 5 x 3 na frente dele.
        var (ctx, _, jogo, org) = await ComUmJogoNoArAsync();
        using var _c = ctx;

        jogo.GamesDupla1 = 3;
        jogo.GamesDupla2 = 3;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .FinalizarPartida(jogo.Id, voltarPara: null, games1: 5, games2: 3);

        var depois = await ctx.Partidas.FirstAsync(p => p.Id == jogo.Id);
        Assert.Equal("Finalizada", depois.Status);
        Assert.Equal(5, depois.GamesDupla1);
    }

    [Fact]
    public async Task Placar_da_tela_que_empata_continua_sendo_recusado()
    {
        // O contrário também: mandar 4 x 4 pela tela não pode furar a regra de que alguém
        // tem que fechar o jogo (QuemVenceu.MotivoParaNaoFinalizar).
        var (ctx, _, jogo, org) = await ComUmJogoNoArAsync();
        using var _c = ctx;

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .FinalizarPartida(jogo.Id, voltarPara: null, games1: 4, games2: 4);

        var depois = await ctx.Partidas.FirstAsync(p => p.Id == jogo.Id);
        Assert.NotEqual("Finalizada", depois.Status);
        Assert.Null(depois.VencedorId);
    }

    [Fact]
    public async Task O_teto_da_fase_vale_tambem_por_esta_porta()
    {
        // Finalizar não é uma porta dos fundos: num torneio até 4, mandar 9 pela tela grava o
        // teto, igual ao salvar em lote. Sem isso o organizador contornaria o formato do
        // torneio só escolhendo por onde encerrar.
        var (ctx, _, jogo, org) = await ComUmJogoNoArAsync(gamesDaFase: 4);
        using var _c = ctx;

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .FinalizarPartida(jogo.Id, voltarPara: null, games1: 9, games2: 0);

        var depois = await ctx.Partidas.FirstAsync(p => p.Id == jogo.Id);
        Assert.Equal(4, depois.GamesDupla1);
        Assert.Equal(0, depois.GamesDupla2);
    }

    [Fact]
    public async Task Na_soma_o_total_e_respeitado_ao_finalizar()
    {
        // A contagem por soma tem que valer aqui igual: numa soma de 7, um 5 x 5 vindo da
        // tela vira 5 x 2 — o segundo lado fica com o que sobrou do bolo.
        var (ctx, _, jogo, org) = await ComUmJogoNoArAsync(
            gamesDaFase: 7, contagem: ContagemDeGamesDoTorneio.Soma);
        using var _c = ctx;

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .FinalizarPartida(jogo.Id, voltarPara: null, games1: 5, games2: 5);

        var depois = await ctx.Partidas.FirstAsync(p => p.Id == jogo.Id);
        Assert.Equal(5, depois.GamesDupla1);
        Assert.Equal(2, depois.GamesDupla2);
        Assert.Equal(7, (depois.GamesDupla1 ?? 0) + (depois.GamesDupla2 ?? 0));
    }

    [Fact]
    public async Task Jogo_ja_finalizado_nao_e_reescrito_pelo_placar_da_tela()
    {
        // A fila offline da Mesa reentrega o mesmo "finalizar" quando a rede volta. Se o
        // placar da tela fosse aplicado de novo, um reenvio velho reescreveria o resultado de
        // um jogo já encerrado — e ninguém descobriria.
        var (ctx, _, jogo, org) = await ComUmJogoNoArAsync();
        using var _c = ctx;

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.FinalizarPartida(jogo.Id, voltarPara: null, games1: 9, games2: 3);

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .FinalizarPartida(jogo.Id, voltarPara: null, games1: 1, games2: 8);

        var depois = await ctx.Partidas.FirstAsync(p => p.Id == jogo.Id);
        Assert.Equal(9, depois.GamesDupla1);
        Assert.Equal(3, depois.GamesDupla2);
    }
}
