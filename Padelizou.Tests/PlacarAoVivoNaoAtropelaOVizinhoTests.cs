using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Tests;

// O PLACAR DE UM CARD NÃO ESCREVE NO CARD DO VIZINHO (Felipe, 12/09/2026: *"um dos marcadores
// está reclamando que ao marcar não está salvando na hora, pode ser internet ruim e os outros
// marcando junto"*).
//
// 🕳️ O POST da lista AO VIVO é em LOTE e casa `partidaId[]` com `games1[]`, `games2[]`,
// `pontos1[]` e `pontos2[]` por ÍNDICE. Duas coisas entortam esse casamento, e as duas
// terminam no mesmo lugar: o número que o marcador acabou de tocar volta pro valor velho, sem
// erro em lugar nenhum.
//
//   1. O aparelho mandava o formulário INTEIRO a cada toque — todas as quadras no ar, com o
//      valor que a tela dele tinha. Dois marcadores no mesmo torneio, e o toque de um
//      reescrevia a quadra do outro com um placar de até 20 segundos atrás (a atualização
//      automática é que traz o do vizinho). É a tela de quem NÃO tocou que perde o game.
//
//   2. O card renderiza DOIS campos `pontos1` quando a fase comporta tie-break e o jogo ainda
//      não está nele (o escondido do lote + o do bloco do tie-break, que nasce escondido mas
//      é campo do mesmo jeito). Com dois jogos no ar, `pontos1` chega com mais entradas que
//      `partidaId` e o índice escorrega: o segundo jogo recebe a contagem do primeiro.
public class PlacarAoVivoNaoAtropelaOVizinhoTests
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
    public async Task Pontos_a_mais_no_lote_nao_gravam_o_tie_break_de_um_jogo_no_outro()
    {
        // O corpo exato que a tela de hoje monta com dois jogos no ar, o primeiro FORA do
        // tie-break (dois campos: o escondido e o do bloco) e o segundo DENTRO dele (um campo
        // só, porque ali o escondido não é renderizado):
        //
        //     partidaId = [A, B]     pontos1 = [0, 0, 6]
        //
        // O servidor lê `pontos1[1]` pro jogo B — que é o segundo campo do jogo A. O tie-break
        // 6x4 que está em quadra vira 0x0 no toque seguinte, e o marcador vê o ponto sumir.
        var (ctx, torneio, aoVivo, org) = await ComJogosNoArAsync(2);
        using var _ = ctx;

        var (a, b) = (aoVivo[0], aoVivo[1]);
        b.GamesDupla1 = 8;
        b.GamesDupla2 = 8;
        b.PontosTieBreak1 = 6;
        b.PontosTieBreak2 = 4;
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.SalvarPlacaresAoVivo(
            torneio.Id, new[] { a.Id, b.Id },
            games1: new[] { 5, 8 }, games2: new[] { 3, 8 },
            voltarPara: null,
            pontos1: new[] { 0, 0, 6 }, pontos2: new[] { 0, 0, 4 });

        var depois = await ctx.Partidas.FindAsync(b.Id);
        Assert.Equal(6, depois!.PontosTieBreak1);
        Assert.Equal(4, depois.PontosTieBreak2);
    }

    [Fact]
    public async Task Games_a_mais_no_lote_nao_gravam_nada()
    {
        // A mesma trava para os games, que já existia: array de tamanho diferente é lote
        // torto, e lote torto não escolhe em qual jogo escrever.
        var (ctx, torneio, aoVivo, org) = await ComJogosNoArAsync(2);
        using var _ = ctx;

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.SalvarPlacaresAoVivo(
            torneio.Id, new[] { aoVivo[0].Id },
            games1: new[] { 5, 9 }, games2: new[] { 3, 0 }, voltarPara: null);

        var depois = await ctx.Partidas.FindAsync(aoVivo[0].Id);
        Assert.Equal(0, depois!.GamesDupla1 ?? 0);
    }

    [Fact]
    public void O_card_tem_um_campo_de_pontos_so()
    {
        // ⚠️ O bloco do tie-break é renderizado SEMPRE que a fase o comporta (ele nasce
        // escondido — ver TieBreakNaTelaTests), e `hidden` não tira campo nenhum do POST. Então
        // a condição do campo escondido do lote tem que ser a NEGAÇÃO EXATA da condição do
        // bloco, e não "está em tie-break agora?": senão os dois existem ao mesmo tempo e o
        // lote sai com um `pontos1` a mais.
        var view = LerDaWeb("Views", "Torneios", "_JogosDoTorneio.cshtml");

        Assert.Contains("bool temBlocoDeTieBreak =", view);
        Assert.Contains("@if (!temBlocoDeTieBreak)", view);
        Assert.Contains("@if (temBlocoDeTieBreak", view);
        Assert.DoesNotContain("@if (!emTieBreak)", view);
    }

    [Fact]
    public void A_atualizacao_automatica_nao_troca_card_com_toque_por_entregar()
    {
        // O toque escreve o número na tela e só sai pro servidor 450ms depois (o debounce que
        // junta a rajada). Nesse vão a atualização automática pode entregar o HTML que ela
        // buscou ANTES do toque: ela troca o cabeçalho, o número volta pro valor velho — e o
        // POST que sai em seguida lê o campo já revertido. O toque some inteiro.
        var js = LerDaWeb("wwwroot", "js", "jogos-ao-vivo-atualiza.js");

        Assert.Contains("data-pdz-mexido", js);
    }

    private static string LerDaWeb(params string[] caminho)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
                return File.ReadAllText(Path.Combine(dir.FullName, "Padelizou", Path.Combine(caminho)));
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto web a partir de " + AppContext.BaseDirectory);
    }
}
