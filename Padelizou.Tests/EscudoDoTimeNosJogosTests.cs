using System.IO;
using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// 11/09/2026, MESMO DIA E DUAS TELAS DEPOIS — o escudo do time nasceu no chip do jogador
// (EscudoDoTimeNoChipTests) e o Felipe, olhando a aba **Jogos** em produção, pediu o resto:
//
// 🗣️ *"aqui nos jogos tambem coloque as bandeiras igual tinha me mostrado no plano A"* —
//    a LISTA de Agendadas/Finalizadas (`_JogoEmLinha`), que tinha ficado de fora porque lá
//    não existe nome de time pra acompanhar o escudo.
// 🗣️ *"e no ao vivo use esse do B"* — no card AO VIVO o escudo vira SELO no canto da foto,
//    que era a outra maquete.
//
// ⚠️ DUAS FORMAS PRO MESMO DADO, e é escolha dele: do lado do nome em todo lugar, e selo na
// foto só no ao vivo. Quem decide é o card ao vivo, por ViewData — o chip é o mesmo parcial
// que a tabela do grupo e a lista de inscritos usam, e essas continuam como estão no ar.
//
// ⚠️ A suíte não renderiza Razor: guarda de ARQUIVO, como em SeloDeCampeaoDaCategoriaTests.
public class EscudoDoTimeNosJogosTests
{
    // ── A LISTA DE JOGOS (plano A) ─────────────────────────────────────────────────────────

    [Fact]
    public void A_linha_do_jogo_desenha_o_escudo_dos_DOIS_jogadores()
    {
        var view = LerDaWeb("Views", "Torneios", "_JogoEmLinha.cshtml");

        Assert.Contains("pdz-jl-escudo", view);
        // Os dois lados, e não só quem abre a dupla: numa dupla de times diferentes o escudo
        // de um só conta metade da história.
        Assert.Contains("Escudo(d.Jogador1)", view);
        Assert.Contains("Escudo(d.Jogador2)", view);
    }

    [Fact]
    public void O_escudo_da_linha_passa_pelo_HtmlEncode()
    {
        var view = LerDaWeb("Views", "Torneios", "_JogoEmLinha.cshtml");

        // ⚠️ Aqui o HTML é montado EM STRING (HtmlContentBuilder.AppendHtml), então o encoding
        // que o Razor faz sozinho em `@atributo` não acontece — e o nome do time é texto
        // digitado por quem cadastra o time. Sem encode, uma aspa no nome fecha o atributo.
        var escudo = Regex.Match(view, @"string Escudo\(.*?\n    \}", RegexOptions.Singleline);

        Assert.True(escudo.Success, "não achei o método Escudo(...) no _JogoEmLinha");
        Assert.Equal(2, Regex.Matches(escudo.Value, "HtmlEncode").Count);
    }

    [Fact]
    public void O_escudo_da_linha_nao_e_cortado()
    {
        var css = LerDaWeb("wwwroot", "css", "site.css");

        var regra = Regex.Match(css, @"\.pdz-jl-escudo\s*\{([^}]*)\}", RegexOptions.Singleline);

        Assert.True(regra.Success, "não achei a regra .pdz-jl-escudo no site.css");
        Assert.Contains("object-fit: contain", regra.Groups[1].Value);
        Assert.DoesNotContain("cover", regra.Groups[1].Value);
    }

    // ── O CARD AO VIVO (plano B: selo na foto) ─────────────────────────────────────────────

    [Fact]
    public void Só_o_card_ao_vivo_pede_o_escudo_como_selo()
    {
        var jogos = LerDaWeb("Views", "Torneios", "_JogosDoTorneio.cshtml");

        Assert.Contains("EscudoComoSelo", jogos);

        // As DUAS duplas do card recebem o aviso. Passar em uma só desenharia o selo num lado
        // e o escudo do outro jeito no outro, no mesmo jogo.
        var comViewData = Regex.Matches(jogos, @"<partial name=""_LadoDaPartida""[^>]*view-data=", RegexOptions.Singleline);
        Assert.Equal(2, comViewData.Count);
    }

    [Fact]
    public void O_chip_desenha_o_selo_DENTRO_da_moldura_da_foto()
    {
        var chip = LerDaWeb("Views", "Torneios", "_JogadorChip.cshtml");

        Assert.Contains("EscudoComoSelo", chip);

        // O selo precisa estar dentro da moldura da FOTO, e não solto no chip: é ela que
        // ancora o canto. Fora dela, o canto vira número mágico e desalinha quando o nome
        // quebra em duas linhas no celular.
        var moldura = Regex.Match(chip, @"pdz-chip-foto-selo.*?</span>", RegexOptions.Singleline);

        Assert.True(moldura.Success, "não achei a moldura da foto com selo no chip");
        Assert.Contains("_EscudoDoTime", moldura.Value);
    }

    [Fact]
    public void O_selo_se_ancora_na_moldura_da_foto()
    {
        var css = LerDaWeb("wwwroot", "css", "site.css");

        var moldura = Regex.Match(css, @"\.pdz-chip-foto-selo\s*\{([^}]*)\}", RegexOptions.Singleline);
        Assert.True(moldura.Success, "não achei a regra .pdz-chip-foto-selo no site.css");
        Assert.Contains("position: relative", moldura.Groups[1].Value);

        var selo = Regex.Match(css, @"\.pdz-chip-foto-selo \.pdz-chip-escudo\s*\{([^}]*)\}", RegexOptions.Singleline);
        Assert.True(selo.Success, "não achei a regra do selo dentro da moldura no site.css");
        Assert.Contains("position: absolute", selo.Groups[1].Value);
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
