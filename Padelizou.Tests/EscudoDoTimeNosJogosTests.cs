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

    // ⚠️ ESTE TESTE PERDEU O "SÓ" DO NOME no mesmo dia, de propósito: a tabela do grupo passou a
    // pedir o selo também (ver o teste da tabela, mais abaixo). O que ele guarda não mudou — as
    // DUAS duplas do card ao vivo recebem o aviso.
    [Fact]
    public void O_card_ao_vivo_pede_o_escudo_como_selo_nas_duas_duplas()
    {
        var jogos = LerDaWeb("Views", "Torneios", "_JogosDoTorneio.cshtml");

        Assert.Contains("EscudoComoSelo", jogos);

        // Passar em uma só desenharia o selo num lado e o escudo do outro jeito no outro, no
        // mesmo jogo.
        var comViewData = Regex.Matches(jogos, @"<partial name=""_LadoDaPartida""[^>]*view-data=", RegexOptions.Singleline);
        Assert.Equal(2, comViewData.Count);
    }

    // ── A TABELA DO GRUPO (o mesmo selo do ao vivo) ────────────────────────────────────────
    //
    // 🗣️ Felipe, com um print das duas tabelas de grupo: *"nessa parte aqui, dos grupos, faça
    // igual dos jogos ao vivo, com o logo redondinho no canto da foto"*.
    //
    // Lá o escudo ficava à direita do nome — e é a coluna mais apertada do site (180px, nome
    // truncado com reticências), então ele roubava letra de um nome que já estava cortado.
    // No canto da foto não ocupa linha nenhuma.

    [Fact]
    public void A_tabela_do_grupo_pede_o_mesmo_selo()
    {
        var details = LerDaWeb("Views", "Torneios", "Details.cshtml");

        // A CÉLULA COMPACTA, e não qualquer `_LadoDaPartida` do arquivo: é ela a tabela do
        // grupo. O `_LadoDaPartida` da lista de TIMES, no mesmo Details, segue como está.
        Assert.Matches(new Regex(@"pdz-chip-compacto[^>]*>\s*<partial name=""_LadoDaPartida""[^>]*view-data=",
                                 RegexOptions.Singleline), details);
    }

    [Fact]
    public void O_selo_encolhe_na_tabela_do_grupo()
    {
        var css = LerDaWeb("wwwroot", "css", "site.css");

        // A foto do compacto tem 26px (contra 36px do ao vivo). O selo de 18px cobriria mais de
        // um terço do rosto — no compacto ele encolhe junto.
        var regra = Regex.Match(css, @"^\.pdz-chip-compacto \.pdz-chip-foto-selo \.pdz-chip-escudo\s*\{([^}]*)\}",
                                RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(regra.Success, "não achei a regra do selo no modo compacto no site.css");
        Assert.Matches(new Regex(@"width:\s*1[0-5]px"), regra.Groups[1].Value);
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

        // ⚠️ `^` no começo do seletor: sem ele este padrão casa também com a regra do COMPACTO
        // (`.pdz-chip-compacto .pdz-chip-foto-selo .pdz-chip-escudo`), que só ajusta tamanho — e
        // o teste passaria a medir a regra errada, que é como ele quebrou quando ela nasceu.
        var selo = Regex.Match(css, @"^\.pdz-chip-foto-selo \.pdz-chip-escudo\s*\{([^}]*)\}",
                               RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(selo.Success, "não achei a regra do selo dentro da moldura no site.css");
        Assert.Contains("position: absolute", selo.Groups[1].Value);
    }

    // ── A MOLDURA QUE IGUALA AS BANDEIRINHAS ───────────────────────────────────────────────
    //
    // 🗣️ Felipe, com o escudo já no ar: *"pq tem algumas bandeirinhas sem fundo igual as demais
    // ainda"*.
    //
    // 🕳️ O DEFEITO NÃO ERA DO DEPLOY, ERA DO ARQUIVO DE CADA LOGO. Medidos os 17 escudos em uso
    // no torneio 26: só 3 têm fundo transparente; os outros 14 trazem fundo DENTRO da imagem —
    // retângulo preto (Compass, Chakra, Os Loberos), azul-escuro (Operados), branco (Dez Padel),
    // e dois JPEG. Sem fundo nenhum no CSS, cada um aparece como veio e a fileira fica salpicada.
    //
    // ✅ A moldura clara é o que iguala: todo escudo vira a MESMA caixinha, com o logo `contain`
    // dentro. Não deixa todos da mesma cor — isso só editando as imagens —, mas dá a todos a
    // mesma forma, o mesmo tamanho e o mesmo contorno, nos dois temas.

    [Fact]
    public void Todo_escudo_tem_fundo_claro_proprio()
    {
        var css = LerDaWeb("wwwroot", "css", "site.css");

        foreach (var classe in new[] { @"\.pdz-chip-escudo", @"\.pdz-jl-escudo" })
        {
            var regra = Regex.Match(css, classe + @"\s*\{([^}]*)\}", RegexOptions.Singleline);
            Assert.True(regra.Success, $"não achei a regra {classe} no site.css");

            // Fundo claro FIXO nos dois temas: os logos opacos são quase todos brancos por
            // dentro — uma moldura que mudasse com o tema desigualaria de novo no escuro.
            Assert.Matches(new Regex(@"background:\s*(#fff\b|#ffffff\b|white\b)", RegexOptions.IgnoreCase),
                           regra.Groups[1].Value);
            Assert.Contains("border", regra.Groups[1].Value);
        }
    }

    [Fact]
    public void O_selo_do_ao_vivo_tambem_e_claro_e_nao_navy()
    {
        var css = LerDaWeb("wwwroot", "css", "site.css");

        // O selo nasceu com fundo navy (o card é navy). Com os logos de verdade isso apaga os
        // escuros — o "Compass", preto sobre navy, some dentro do próprio selo.
        var selo = Regex.Match(css, @"^\.pdz-chip-foto-selo \.pdz-chip-escudo\s*\{([^}]*)\}",
                               RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(selo.Success, "não achei a regra do selo no site.css");

        // O VALOR da propriedade, não o texto da regra: o comentário ao lado explica por que o
        // navy saiu, e um `DoesNotContain("navy")` cru se enganaria com a própria explicação.
        var fundo = Regex.Match(selo.Groups[1].Value, @"background:\s*([^;]+);");
        Assert.True(fundo.Success, "o selo precisa declarar um fundo próprio");
        Assert.Matches(new Regex(@"^(#fff|#ffffff|white)$", RegexOptions.IgnoreCase), fundo.Groups[1].Value.Trim());
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
