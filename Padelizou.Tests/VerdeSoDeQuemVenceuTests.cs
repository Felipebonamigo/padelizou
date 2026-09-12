using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 11/09/2026 — O VERDE DO CARD AO VIVO É DE QUEM VENCEU, e não de quem está na frente.
//
// 🗣️ Felipe, num print de um jogo 8 x 6 em quadra: "pq q esse aqui ta o numero verde se o jogo
// n terminou? acho que ele se perdeu quando eu diminui do 9".
//
// Ele não se perdeu — a régua era `GamesDupla1 > GamesDupla2`, então QUALQUER vantagem pintava
// de lime. O mesmo lime do card finalizado, que ali significa "campeão do jogo": duas cores
// iguais pra duas coisas diferentes, na mesma tela, e quem lê acredita na mais forte.
//
// ⚠️ E O VERDE ATRASAVA. A classe só nascia no HTML do servidor: o −/+ salvava por `fetch` e
// atualizava o NÚMERO, nunca a cor. Um 9 x 8 que virava 8 x 8 ficava com o empate pintado de
// verde do lado de cima até a atualização automática passar — e ela NÃO passa enquanto o
// cursor está dentro do campo (`estaOcupado` em jogos-ao-vivo-atualiza.js). Quem digita em vez
// de tocar no −/+ podia ficar com o verde no lado errado por tempo indeterminado.
//
// A régua nova não é uma terceira conta: é a composição das duas que já existiam —
// `FormatoDaPartida.PodeEncerrar` (o jogo já está decidido?) e `QuemVenceu.Lado` (decidido a
// favor de quem?). Escrever `games1 >= 9` aqui seria o `limiteGames: 9` cravado de novo.
public class VerdeSoDeQuemVenceuTests
{
    private static readonly FormatoDaPartida.Formato Ate9 = new(Sets: 1, Games: 9);
    private static readonly FormatoDaPartida.Formato Ate4 = new(Sets: 1, Games: 4);
    private static readonly FormatoDaPartida.Formato Soma7 =
        new(Sets: 1, Games: 7, Contagem: ContagemDeGamesDoTorneio.Soma);

    // O print do Felipe, virado em teste.
    [Fact]
    public void Num_jogo_ate_9_o_8x6_ainda_nao_tem_vencedor()
    {
        Assert.Null(QuemVenceu.LadoJaDecidido(Ate9, null, null, 8, 6));
    }

    [Fact]
    public void Quem_chega_no_numero_que_vence_fica_verde()
    {
        Assert.Equal(1, QuemVenceu.LadoJaDecidido(Ate9, null, null, 9, 6));
        Assert.Equal(2, QuemVenceu.LadoJaDecidido(Ate9, null, null, 6, 9));
    }

    // O "vencer por dois" do padel: num jogo até 4, o 3x3 estende o limite pra 5 — ninguém
    // venceu ali, apesar de o 3 ser "quase lá". É a conta que o card não pode reescrever.
    [Fact]
    public void No_jogo_ate_4_o_empate_na_penultima_nao_da_vencedor_a_ninguem()
    {
        Assert.Null(QuemVenceu.LadoJaDecidido(Ate4, null, null, 3, 3));
        Assert.Null(QuemVenceu.LadoJaDecidido(Ate4, null, null, 4, 4));
        Assert.Equal(1, QuemVenceu.LadoJaDecidido(Ate4, null, null, 4, 3));
        Assert.Equal(1, QuemVenceu.LadoJaDecidido(Ate4, null, null, 5, 4));
    }

    // Na SOMA o jogo fecha quando os games acabam, não quando alguém chega num número: 6 x 0
    // numa soma de 7 ainda tem um game pra jogar.
    [Fact]
    public void Na_soma_o_verde_so_acende_quando_os_games_acabam()
    {
        Assert.Null(QuemVenceu.LadoJaDecidido(Soma7, null, null, 6, 0));
        Assert.Equal(1, QuemVenceu.LadoJaDecidido(Soma7, null, null, 4, 3));
        Assert.Equal(2, QuemVenceu.LadoJaDecidido(Soma7, null, null, 3, 4));
    }

    // Soma PAR empatada é o caso que separa "ganhando" de "venceu" com clareza: a soma fechou,
    // e ainda assim não há vencedor. O verde não pode acender pra nenhum dos dois lados.
    [Fact]
    public void Soma_que_fecha_empatada_nao_pinta_ninguem()
    {
        var soma14 = new FormatoDaPartida.Formato(1, 14, ContagemDeGamesDoTorneio.Soma);

        Assert.Null(QuemVenceu.LadoJaDecidido(soma14, null, null, 7, 7));
    }

    // O set guardado manda no games, como já manda no finalizar (QuemVenceu.Da). Sem isto o
    // card pintaria de verde quem venceu só o SET que está em quadra.
    [Fact]
    public void Com_sets_em_jogo_o_set_decide_quem_fica_verde()
    {
        Assert.Equal(2, QuemVenceu.LadoJaDecidido(Ate9, 0, 1, 9, 6));
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════
    // O SERVIDOR ENTREGA A RESPOSTA PRONTA — nas duas portas por onde o card recebe placar.
    // ═══════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task A_tela_recebe_do_servidor_quem_venceu_em_cada_jogo_no_ar()
    {
        var (ctx, torneio, aoVivo, org) = await ComJogosNoArAsync(2);
        using var _ = ctx;

        (aoVivo[0].GamesDupla1, aoVivo[0].GamesDupla2) = (9, 6);
        (aoVivo[1].GamesDupla1, aoVivo[1].GamesDupla2) = (8, 6);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        Assert.IsType<ViewResult>(await controller.Jogos(torneio.Id, null, null));

        var vencedores = (Dictionary<int, int>)controller.ViewBag.VencedorNoPlacar;

        Assert.Equal(1, vencedores[aoVivo[0].Id]);
        Assert.Equal(0, vencedores[aoVivo[1].Id]);   // 8 x 6 está ganhando, não venceu
    }

    // ⚠️ ESTE É O CONSERTO DO ATRASO. Sem o vencedor na resposta do salvar, o JavaScript não
    // tem como corrigir a cor sozinho — e a atualização automática, que corrigiria, não roda
    // com o cursor dentro do campo.
    [Fact]
    public async Task Salvar_por_fetch_devolve_quem_venceu_junto_com_o_placar()
    {
        var (ctx, torneio, aoVivo, org) = await ComJogosNoArAsync(1);
        using var _ = ctx;

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        controller.ControllerContext.HttpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";

        var resposta = await controller.SalvarPlacaresAoVivo(
            torneio.Id, new[] { aoVivo[0].Id }, new[] { 9 }, new[] { 6 });

        var texto = System.Text.Json.JsonSerializer.Serialize(
            Assert.IsType<JsonResult>(resposta).Value);

        Assert.Contains("\"vencedor\":1", texto);

        // E o 9 → 8 do print do Felipe APAGA o verde na mesma resposta.
        var depois = await controller.SalvarPlacaresAoVivo(
            torneio.Id, new[] { aoVivo[0].Id }, new[] { 8 }, new[] { 6 });

        Assert.Contains("\"vencedor\":0", System.Text.Json.JsonSerializer.Serialize(
            Assert.IsType<JsonResult>(depois).Value));
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════
    // AS GUARDAS DE ARQUIVO — o defeito é 100% de tela: o banco continua certo, nenhum teste
    // de controller fica vermelho, e o único sintoma é a cor errada na frente do organizador.
    // ═══════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void O_card_ao_vivo_NAO_pinta_de_verde_quem_esta_so_ganhando()
    {
        var view = LerDaWeb("Views", "Torneios", "_JogosDoTorneio.cshtml");

        // A cor sai da resposta do servidor, nunca de uma comparação feita no Razor.
        Assert.DoesNotContain("(jogo.GamesDupla1 ?? 0) > (jogo.GamesDupla2 ?? 0)", view);
        Assert.DoesNotContain("pdz-live-placar-ganhando", view);
        Assert.Contains("pdz-live-placar-venceu", view);
    }

    [Fact]
    public void O_verde_acompanha_o_placar_salvo_sem_esperar_a_atualizacao_automatica()
    {
        var js = LerDaWeb("wwwroot", "js", "placar-ao-vivo.js");

        // Lê o vencedor que o servidor mandou…
        Assert.Contains("linha.vencedor", js);
        // …e mexe na classe do placar com ele.
        Assert.Contains("pdz-live-placar-venceu", js);
    }

    [Fact]
    public void O_CSS_do_verde_acompanhou_o_nome_novo()
    {
        var css = LerDaWeb("wwwroot", "css", "site.css");

        Assert.Contains(".pdz-live-placar-venceu", css);
        Assert.DoesNotContain("pdz-live-placar-ganhando", css);
    }

    private static async Task<(DbPadelContext ctx, Torneio torneio, List<Partida> aoVivo, Jogador org)>
        ComJogosNoArAsync(int quantos)
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 8);
        torneio.QuantidadeQuadras = 5;
        torneio.GamesFaseGrupos = 9;
        await ctx.SaveChangesAsync();

        var doTorneio = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doTorneio.GerarChaves(torneio.Id);

        var partidas = TestInfra.NovoPartidasController(ctx, org.Id);
        var aoVivo = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id)
            .OrderBy(p => p.Id).Take(quantos).ToListAsync();
        foreach (var jogo in aoVivo) await partidas.ColocarNoAr(jogo.Id);

        return (ctx, torneio, aoVivo, org);
    }

    // Mesmo achador dos outros testes de guarda: os testes rodam a partir de bin/.
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
