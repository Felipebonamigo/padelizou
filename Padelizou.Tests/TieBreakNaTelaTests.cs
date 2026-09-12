using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// AS GUARDAS DE TELA DO TIE-BREAK (12/09/2026).
//
// ⚠️ Os defeitos que este arquivo pega são 100% de TELA: o banco continua certo, nenhum teste de
// controller fica vermelho, e o sintoma aparece na frente do organizador com a quadra rolando.
// É a mesma razão de existir das guardas do VerdeSoDeQuemVenceuTests.
public class TieBreakNaTelaTests
{
    private static async Task<(DbPadelContext ctx, Torneio torneio, List<Partida> aoVivo, Jogador org)>
        ComUmJogoNoArAsync(int pontosDoTieBreak = 7, int gamesDaFase = 9)
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
            .OrderBy(p => p.Id).Take(1).ToListAsync();
        foreach (var jogo in aoVivo) await partidas.ColocarNoAr(jogo.Id);

        return (ctx, torneio, aoVivo, org);
    }

    // ═══ O SERVIDOR ENTREGA A RESPOSTA PRONTA ═══════════════════════════════════════════════

    [Fact]
    public async Task A_tela_recebe_o_formato_de_cada_jogo_no_ar()
    {
        // É com ele que o card pergunta "este jogo está em tie-break?" sem conhecer número
        // nenhum — mesma entrega do teto de games e do vencedor.
        var (ctx, torneio, aoVivo, org) = await ComUmJogoNoArAsync();
        using var _ = ctx;

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        Assert.IsType<ViewResult>(await controller.Jogos(torneio.Id, null, null));

        var formatos = (Dictionary<int, FormatoDaPartida.Formato>)controller.ViewBag.FormatoDoJogo;
        Assert.Equal(7, formatos[aoVivo[0].Id].PontosTieBreak);
    }

    [Fact]
    public async Task Salvar_por_fetch_devolve_o_estado_do_tie_break_e_o_placar_do_fechamento()
    {
        // ⚠️ ESTE É O CONSERTO DO ATRASO, e ele não é cosmético: a atualização automática NÃO
        // roda com o cursor dentro de um campo (`estaOcupado` em jogos-ao-vivo-atualiza.js), e
        // quem marca o 8º game está com o dedo exatamente ali. Sem o tie-break na resposta do
        // salvar, o bloco não apareceria no 8x8 — por 15 segundos ou por tempo indeterminado.
        var (ctx, torneio, aoVivo, org) = await ComUmJogoNoArAsync();
        using var _ = ctx;

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        controller.ControllerContext.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        // 8x8: o tie-break começa, e ninguém pode fechar ainda.
        var comecou = System.Text.Json.JsonSerializer.Serialize(Assert.IsType<JsonResult>(
            await controller.SalvarPlacaresAoVivo(torneio.Id, new[] { aoVivo[0].Id },
                new[] { 8 }, new[] { 8 }, null, new[] { 0 }, new[] { 0 })).Value);

        Assert.Contains("\"emAndamento\":true", comecou);
        Assert.Contains("\"fecha1\":null", comecou);

        // 7-5: dá pra fechar, e o servidor diz COM QUE PLACAR (9 x 8).
        var fechavel = System.Text.Json.JsonSerializer.Serialize(Assert.IsType<JsonResult>(
            await controller.SalvarPlacaresAoVivo(torneio.Id, new[] { aoVivo[0].Id },
                new[] { 8 }, new[] { 8 }, null, new[] { 7 }, new[] { 5 })).Value);

        Assert.Contains("\"fecha1\":9", fechavel);
        Assert.Contains("\"fecha2\":8", fechavel);
        Assert.Contains("\"etiqueta\":\"tie-break 7-5\"", fechavel);

        // 7-6 NÃO fecha: o tie-break se vence por dois.
        var apertado = System.Text.Json.JsonSerializer.Serialize(Assert.IsType<JsonResult>(
            await controller.SalvarPlacaresAoVivo(torneio.Id, new[] { aoVivo[0].Id },
                new[] { 8 }, new[] { 8 }, null, new[] { 7 }, new[] { 6 })).Value);

        Assert.Contains("\"fecha1\":null", apertado);

        // Fechado (9x8): o bloco sai de cena e o fechamento deixa de ser oferecido.
        var fechado = System.Text.Json.JsonSerializer.Serialize(Assert.IsType<JsonResult>(
            await controller.SalvarPlacaresAoVivo(torneio.Id, new[] { aoVivo[0].Id },
                new[] { 9 }, new[] { 8 }, null, new[] { 7 }, new[] { 5 })).Value);

        Assert.Contains("\"emAndamento\":false", fechado);
        Assert.Contains("\"houve\":true", fechado);
        Assert.Contains("\"fecha1\":null", fechado);
    }

    // ═══ AS GUARDAS DE ARQUIVO ══════════════════════════════════════════════════════════════

    [Fact]
    public void O_card_desenha_o_bloco_do_tie_break_mesmo_fora_dele()
    {
        // Se o bloco só existisse no 8x8, ele dependeria da atualização automática pra nascer —
        // e ela está travada justamente quando o 8º game é marcado. Ele existe escondido e o
        // JavaScript o acende com a resposta do POST.
        var view = LerDaWeb("Views", "Torneios", "_JogosDoTorneio.cshtml");

        Assert.Contains("TieBreakDoJogo.PodeAcontecer(formatoAoVivo)", view);
        Assert.Contains("data-tiebreak=", view);
        Assert.Contains("hidden=\"@(!emTieBreak)\"", view);
    }

    [Fact]
    public void Os_pontos_viajam_no_POST_mesmo_fora_do_tie_break()
    {
        // ⚠️ O POST do card é em LOTE e casa `partidaId[]`, `games1[]`, `games2[]` e `pontos1[]`
        // por ÍNDICE. Um campo que só existe em alguns cards desalinha tudo: com três jogos no ar
        // e só o segundo em tie-break, `pontos1[0]` chegaria como se fosse do primeiro.
        var view = LerDaWeb("Views", "Torneios", "_JogosDoTorneio.cshtml");

        // Fase SEM tie-break: o campo escondido é o único caminho dos pontos.
        Assert.Contains("<input type=\"hidden\" name=\"pontos1\"", view);
        Assert.Contains("<input type=\"hidden\" name=\"pontos2\"", view);

        // Fase COM tie-break: quem viaja é o contador do bloco, que existe no HTML mesmo fora
        // do 8x8 (escondido). ⚠️ E aí o escondido NÃO pode existir junto — os dois no mesmo
        // card mandam dois `pontos1` e desalinham o lote inteiro. Ver
        // PlacarAoVivoNaoAtropelaOVizinhoTests.
        Assert.Contains("@if (!temBlocoDeTieBreak)", view);
        Assert.DoesNotContain("@if (!emTieBreak)", view);
    }

    [Fact]
    public void O_placar_ao_vivo_acha_os_campos_por_NOME_e_nao_por_indice()
    {
        // Aqui era `campos[0]`/`campos[1]` sobre todos os `.pdz-live-input` do card. Com os dois
        // campos novos do tie-break, índice virou um acoplamento com a ORDEM do HTML: mover o
        // bloco pra cima do placar passaria a escrever o game no campo de pontos, calado.
        var js = LerDaWeb("wwwroot", "js", "placar-ao-vivo.js");

        Assert.Contains("'.pdz-live-input[name=\"' + nome + '\"]'", js);
        Assert.DoesNotContain("var campos = card.querySelectorAll(\".pdz-live-input\")", js);
    }

    [Fact]
    public void O_JavaScript_nao_decide_nada_sobre_o_tie_break_sozinho()
    {
        // "Está em tie-break?", "dá pra fechar?", "com que placar?" e até a etiqueta vêm do
        // servidor. Uma segunda cópia da régua no JS é o erro do `limiteGames: 9` cravado.
        var js = LerDaWeb("wwwroot", "js", "placar-ao-vivo.js");

        Assert.Contains("tb.emAndamento", js);
        Assert.Contains("tb.fecha1", js);
        Assert.Contains("tb.etiqueta", js);
        // Nada de alvo de pontos nem de paridade escritos aqui.
        Assert.DoesNotContain("% 2", js);
        Assert.DoesNotContain("=== 7", js);
    }

    [Fact]
    public void A_Mesa_offline_manda_os_pontos_na_fila_e_nao_recopia_a_paridade()
    {
        var js = LerDaWeb("wwwroot", "js", "mesa-offline.js");

        // A fila leva a contagem pro servidor.
        Assert.Contains("pontosTieBreak1=", js);
        Assert.Contains("pontosTieBreak2=", js);

        // E o alvo vem do servidor por partida, como o limite de games.
        Assert.Contains("item.tieBreak", js);

        // ⚠️ A paridade continua escrita UMA vez só (no teto de games): o `emTieBreak` pergunta
        // pra ela em vez de recalcular. Duas cópias aqui e um jogo até 4 passaria a abrir
        // tie-break no 3x3, que é regra de outro torneio.
        Assert.Single(Regex.Matches(js, "limite % 2"));
    }

    [Fact]
    public void A_Mesa_offline_aguenta_fila_gravada_antes_do_tie_break_existir()
    {
        // Item de fila antigo não tem `pontos1/pontos2`: sem o padrão, o número na tela viraria
        // "undefined" e o primeiro toque no "+" faria NaN — no aparelho de quem está sem sinal.
        var js = LerDaWeb("wwwroot", "js", "mesa-offline.js");

        Assert.Contains("{ pontos1: 0, pontos2: 0, ...placar }", js);
    }

    [Fact]
    public void A_tela_cheia_deixa_corrigir_o_tie_break_de_um_jogo_que_ja_fechou()
    {
        // É a tela de CORRIGIR: o bloco aparece sempre que a fase comporta tie-break, e não só
        // no empate — senão o acerto de um 7-5 digitado errado não teria porta nenhuma.
        var view = LerDaWeb("Views", "Partidas", "ControlePlacar.cshtml");

        Assert.Contains("TieBreakDoJogo.PodeAcontecer(formato)", view);
        Assert.Contains("name=\"pontosTieBreak1\"", view);
        Assert.Contains("name=\"pontosTieBreak2\"", view);
    }

    [Fact]
    public void A_gestao_do_torneio_avisa_quando_o_tie_break_vai_ficar_inerte()
    {
        // O padrão da final é 6 games (par), onde o 5x5 vai pro 7º game e tie-break nenhum
        // acontece. Sem o aviso, o organizador configura 10, confia, e descobre na quadra.
        var view = LerDaWeb("Views", "Torneios", "Details.cshtml");

        Assert.Contains("TieBreakDoJogo.AFaseComporta", view);
        Assert.Contains("name=\"pontosTieBreakGrupos\"", view);
        Assert.Contains("name=\"pontosTieBreakMataMata\"", view);
        Assert.Contains("name=\"pontosTieBreakFinal\"", view);
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
