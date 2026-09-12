using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// 12/09/2026 — O −/+ DO CARD AO VIVO SUBIU EM CIMA DA BOLINHA DO SAQUE.
//
// 🗣️ Felipe, com um print do jogo ao vivo no iPhone: *"A bolinha ta em cima do placar"*.
//
// 🕳️ A CAUSA NÃO É A BOLINHA, É UM `min-width` ESCRITO À MÃO. A linha do card é
// `[nomes] [bolinha] [placar]`, e o bloco do placar (`.pdz-live-placar`) alinha o conteúdo à
// DIREITA (`justify-content: flex-end`). Item de flex encolhe por padrão, e o piso que o
// impediria de encolher abaixo do próprio conteúdo — o `min-width: auto` que o navegador dá
// de graça — estava DESLIGADO por um `min-width: 2.4rem` escrito na regra. Resultado: num
// celular o bloco encolhia pra menos que o `−  [4]  +` que mora dentro dele, e o que não
// coube saiu pelo lado ERRADO: alinhado à direita, o transbordo vaza pela ESQUERDA, que é
// exatamente onde está a bolinha.
//
// 📏 MEDIDO NO CHROMIUM DESTA SESSÃO (harness com o `site.css` de verdade, 393px = o iPhone
// do print): o bloco pedia 133,2px de conteúdo e recebia 97,2px — os 36px que sobraram
// pousaram na coluna da bolinha, e o `−` começava 14,6px ANTES de a bola terminar. Na faixa
// do `col-md-6` (768–991px, onde o card fica com 336px) a invasão era de 37,1px: a bola
// sumia inteira debaixo do botão.
//
// ⚠️ E O CONSERTO TEM UM SEGUNDO LADO, senão a sobreposição só troca de dono: travado o
// placar, quem passa a não caber é o NOME — e o chip do jogador também não encolhe sozinho
// (`.pdz-chip-texto` sem `min-width: 0` respeita a palavra inteira e vaza pra direita, em
// cima da mesma bolinha). Os dois precisam ceder juntos.
public class BolinhaDoSaqueNaoFicaEmbaixoDoPlacarTests
{
    // O celular do print. 393px é o iPhone 14/15/16 em CSS pixels — e é a largura mais
    // estreita que ainda precisa mostrar nome, bolinha e placar na mesma linha.
    private const double LarguraDoCelular = 393;

    // O que o Bootstrap come antes de o card começar: `.container` tem 12px de cada lado e a
    // coluna repete os 12px do gutter. Mais 1px de borda do card em cada lado.
    private const double GutterDoBootstrap = 12 + 12;
    private const double BordaDoCard = 1;

    // O nome mais comprido de um card real hoje ("Vitor Bittencourt") pede ~9rem com a foto:
    // 36px de avatar + .5rem de vão + a palavra em negrito. Abaixo disso o navegador parte a
    // palavra no meio ("Bittencour / t"), que é o defeito seguinte na fila.
    private const double NomePrecisaDeRem = 9;

    [Fact]
    public void O_bloco_do_placar_nao_encolhe()
    {
        var css = LerDaWeb("wwwroot", "css", "site.css");
        var placar = Declaracoes(css, ".pdz-live-placar");

        Assert.True(EncolhimentoZero(placar),
            "`.pdz-live-placar` alinha o conteúdo à direita e continua encolhível: o `−/+` que " +
            "não couber vai transbordar pela ESQUERDA, em cima da bolinha do saque. " +
            "Ou `flex-shrink: 0`, ou um `flex` cujo encolhimento seja 0.");
    }

    [Fact]
    public void O_nome_cede_o_espaco_em_vez_de_avancar_na_bolinha()
    {
        var css = LerDaWeb("wwwroot", "css", "site.css");

        var texto = Declaracoes(css, ".pdz-live-jogadores .pdz-chip-texto");
        Assert.True(texto.GetValueOrDefault("min-width") == "0",
            "O texto do chip no card AO VIVO precisa de `min-width: 0`. Sem isso ele exige a " +
            "palavra inteira e vaza pra direita, em cima da bolinha — a mesma sobreposição, " +
            "com o nome no lugar do `−`.");

        var quebra = Declaracoes(css, ".pdz-live-jogadores .pdz-chip-nome,\n.pdz-live-jogadores .pdz-chip-clube");
        Assert.True(quebra.GetValueOrDefault("overflow-wrap") is "break-word" or "anywhere",
            "`min-width: 0` sozinho não basta num nome de uma palavra só: sem `overflow-wrap` " +
            "a palavra continua saindo da caixa. Ela precisa poder quebrar no aperto.");
    }

    // O teste que trava o DESENHO e não a regra: com as larguras fixas de hoje, quanto sobra
    // pro nome no celular do Felipe? Se a resposta for "menos que o nome", o card volta a se
    // resolver sobrepondo alguma coisa.
    [Fact]
    public void Sobra_nome_para_ler_no_celular()
    {
        var css = LerDaWeb("wwwroot", "css", "site.css");
        double rem = RaizEmPx(css);

        // Do viewport até a largura útil da linha: gutter do Bootstrap, borda, o padding do
        // cabeçalho escuro e o padding da própria linha.
        double linha = LarguraDoCelular - GutterDoBootstrap - 2 * BordaDoCard
            - 2 * PxDe(css, ".pdz-live-header", "padding", rem, indice: 1);
        double util = linha
            - 2 * PxDe(css, ".pdz-live-linha", "padding", rem, indice: 1)
            - 2 * PxDe(css, ".pdz-live-linha", "gap", rem, indice: 0);

        // O que NUNCA encolhe na linha: o alvo de toque da bolinha e o contador inteiro.
        double bolinha = PxDe(css, ".pdz-saque-toque", "width", rem, indice: 0);
        double contador =
            2 * PxDe(css, ".pdz-live-passo", "width", rem, indice: 0)
            + LarguraDoCampo(css, rem)
            + 2 * PxDe(css, ".pdz-live-contador", "gap", rem, indice: 0);

        double sobraProNome = util - bolinha - contador;

        Assert.True(sobraProNome >= NomePrecisaDeRem * rem,
            $"Num celular de {LarguraDoCelular}px sobram {sobraProNome:0.0}px pro nome, e ele " +
            $"precisa de {NomePrecisaDeRem * rem:0.0}px. A bolinha ({bolinha:0.0}px) e o " +
            $"contador ({contador:0.0}px) não cabem junto com o nome — e é assim que o card " +
            "volta a se resolver empilhando uma coisa em cima da outra.");
    }

    // A mesma conta na faixa do tablet: com `col-md-6` o card ficava com 336px entre 768 e
    // 991px — 60px a menos que o celular do print, e foi lá que a bola sumiu INTEIRA debaixo
    // do botão. Card ao vivo divide a linha só quando a linha é grande de verdade.
    [Fact]
    public void O_card_ao_vivo_so_divide_a_linha_no_computador()
    {
        var view = LerDaWeb("Views", "Torneios", "_JogosDoTorneio.cshtml");

        // A CLASSE do card, e não a palavra solta: o comentário ao lado dela explica por que
        // o `col-md-6` saiu, e citar o que se proíbe não pode reprovar o arquivo.
        Assert.DoesNotContain("class=\"col-md-6", view);
        Assert.Contains("class=\"col-lg-6", view);
    }

    // ── A leitura do CSS ────────────────────────────────────────────────────────────────

    // `flex-shrink: 0`, ou o encolhimento do `flex` (2º número do atalho; `flex: 0 0 auto`).
    private static bool EncolhimentoZero(Dictionary<string, string> regra)
    {
        if (regra.GetValueOrDefault("flex-shrink") == "0") return true;

        var atalho = regra.GetValueOrDefault("flex");
        if (atalho == null) return false;
        if (atalho is "none") return true;

        var partes = atalho.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return partes.Length >= 2 && partes[1] == "0";
    }

    // A largura do campo do placar em px. Ela é declarada em `em`, e `em` ali é o tamanho de
    // fonte DO PRÓPRIO CAMPO (1.9rem) — não o da página.
    private static double LarguraDoCampo(string css, double rem)
    {
        var campo = Declaracoes(css, ".pdz-live-input");
        double fonte = EmPx(campo["font-size"], rem, rem);
        return EmPx(campo["width"], rem, fonte);
    }

    private static double PxDe(string css, string seletor, string propriedade, double rem, int indice)
    {
        var valor = Declaracoes(css, seletor).GetValueOrDefault(propriedade)
            ?? throw new Xunit.Sdk.XunitException($"Não achei `{propriedade}` em `{seletor}` no site.css.");

        var partes = valor.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return EmPx(partes[Math.Min(indice, partes.Length - 1)], rem, rem);
    }

    private static double EmPx(string valor, double rem, double fonte)
    {
        var m = Regex.Match(valor, @"^(-?[\d.]+)(rem|em|px)$");
        if (!m.Success) throw new Xunit.Sdk.XunitException($"`{valor}` não é uma medida que dê pra somar.");

        double numero = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        return m.Groups[2].Value switch { "rem" => numero * rem, "em" => numero * fonte, _ => numero };
    }

    // O rem do CELULAR: o `html` base vale 15px e só vira 16px dentro do `@media (min-width:
    // 768px)`. Quem manda aqui é a PRIMEIRA regra — a de fora do media query.
    private static double RaizEmPx(string css)
    {
        var m = Regex.Match(SemComentario(css), @"(?m)^[ \t]*html[ \t]*\{([^}]*)\}");
        var tamanho = Regex.Match(m.Groups[1].Value, @"font-size\s*:\s*([\d.]+)px");
        return double.Parse(tamanho.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private static Dictionary<string, string> Declaracoes(string css, string seletor)
    {
        var achadas = new Dictionary<string, string>();

        // O seletor tem que começar A LINHA, senão `.pdz-live-input` casaria também dentro de
        // `.pdz-live-placar-venceu .pdz-live-input` (o mesmo cuidado do teste de contraste).
        var alvo = string.Join(@"[ \t]*,\s*", seletor.Split('\n').Select(s => Regex.Escape(s.Trim().TrimEnd(','))));
        foreach (Match bloco in Regex.Matches(SemComentario(css), @"(?m)^[ \t]*" + alvo + @"[ \t]*\{([^}]*)\}"))
            foreach (Match decl in Regex.Matches(bloco.Groups[1].Value, @"([\w-]+)\s*:\s*([^;]+);"))
                achadas[decl.Groups[1].Value.Trim()] = decl.Groups[2].Value.Trim();
        return achadas;
    }

    // Comentário fora antes de tudo: este arquivo cita CSS dentro do texto, e uma citação
    // dessas abre declaração falsa que engole o `;` seguinte.
    private static string SemComentario(string css) =>
        Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);

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
