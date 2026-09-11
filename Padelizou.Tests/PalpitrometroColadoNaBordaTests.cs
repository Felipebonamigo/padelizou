using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// 11/09/2026 — O PALPITRÔMETRO DO CARD AO VIVO ESTAVA COLADO NA BORDA.
//
// 🗣️ Felipe, num print do card do jogo ao vivo com o palpitrômetro embaixo da transmissão:
// *"essa parte aqui ta muito colada no card, arrume e veja se tem mais algo assim"*.
//
// 🕳️ A CAUSA É UMA VARIÁVEL QUE NÃO EXISTE. O Bootstrap 5.3 escreve o padding do `.card-body`
// assim:
//
//     .card-body { padding: var(--bs-card-spacer-y) var(--bs-card-spacer-x); }
//
// e declara as duas variáveis DENTRO da regra do `.card`. O card do Ao Vivo é `.pdz-live-card`
// — casa nossa, não `.card` —, então ali as duas não existem, a declaração inteira vira
// inválida no cálculo e o padding cai pro valor inicial: **zero**. Nenhum aviso, nenhum erro:
// o bloco simplesmente encosta nos quatro lados.
//
// ⚠️ E SÓ APARECIA NO COMPUTADOR. O `site.css` tem `.card-body { padding: 1.15rem !important }`
// dentro de `@media (max-width: 767px)` — no celular o `!important` tapava o buraco. Por isso o
// print do defeito é de tela grande, e por isso ele durou.
//
// A correção não é ensinar as variáveis ao card (seria carregar o componente errado inteiro):
// é o bloco ter o padding DELE, alinhado com o cabeçalho e com o rótulo da transmissão, que já
// usam 1.1rem de cada lado.
public class PalpitrometroColadoNaBordaTests
{
    // ── 1. O BLOCO TEM RESPIRO, E ALINHADO COM O RESTO DO CARD ─────────────────────────────

    [Fact]
    public void O_palpitrometro_do_card_ao_vivo_nao_usa_card_body()
    {
        var bloco = AberturaDoBlocoDoPalpitrometro();

        Assert.DoesNotContain("card-body", bloco);
    }

    [Fact]
    public void O_bloco_do_palpitrometro_tem_a_classe_que_carrega_o_padding()
    {
        Assert.Contains("pdz-live-palpite", AberturaDoBlocoDoPalpitrometro());
    }

    // O número não é escolhido aqui: ele é o do cabeçalho. Travar "1.1rem" na mão deixaria os
    // dois livres pra se separarem no dia em que um mudasse — e desalinhado é o defeito.
    [Fact]
    public void O_padding_lateral_do_palpitrometro_e_o_mesmo_do_cabecalho_do_card()
    {
        Assert.Equal(PaddingLateralDe(".pdz-live-header"), PaddingLateralDe(".pdz-live-palpite"));
    }

    [Fact]
    public void E_o_mesmo_do_rotulo_da_transmissao_logo_acima()
    {
        Assert.Equal(PaddingLateralDe(".pdz-live-video-label"), PaddingLateralDe(".pdz-live-palpite"));
    }

    // O fundo também: com `card-body` valendo zero, o "12 voto(s)" encostava na borda de baixo.
    [Fact]
    public void O_bloco_tambem_descola_do_fim_do_card()
    {
        var padding = Shorthand(".pdz-live-palpite");

        Assert.True(padding.Length >= 3, $".pdz-live-palpite precisa declarar o padding de baixo; achei `{string.Join(' ', padding)}`");
        Assert.NotEqual("0", padding[2]);
    }

    // ── 2. E NÃO SOBROU MAIS NENHUM ASSIM ("veja se tem mais algo assim") ──────────────────
    //
    // O gate mecânico da mesma armadilha: `.card-body`, `.card-header`, `.card-footer` e
    // `.card-img-overlay` tiram TODO o espaçamento deles de variáveis que só a regra do `.card`
    // declara. Fora de um `.card`, qualquer um dos quatro nasce sem padding nenhum — calado,
    // como este nasceu.
    [Fact]
    public void Nenhuma_view_usa_peca_de_card_fora_de_um_card()
    {
        var soltas = new List<string>();

        foreach (var view in Directory.EnumerateFiles(Path.Combine(RaizDoRepo(), "Padelizou", "Views"),
                                                      "*.cshtml", SearchOption.AllDirectories))
            soltas.AddRange(PecasDeCardSemCard(File.ReadAllText(view),
                                               Path.GetRelativePath(RaizDoRepo(), view)));

        Assert.True(soltas.Count == 0,
            "Peça de card fora de um `.card` — o Bootstrap declara `--bs-card-spacer-x/y` na regra "
            + "do `.card`, então aqui o padding vira ZERO sem erro nenhum:\n  " + string.Join("\n  ", soltas));
    }

    // ── AS FERRAMENTAS ─────────────────────────────────────────────────────────────────────

    // Pilha de tags, como o navegador monta. Não é um parser de HTML de verdade e não precisa
    // ser: o que se pergunta é só "algum pai deste elemento tem a classe `card`?".
    private static IEnumerable<string> PecasDeCardSemCard(string razor, string arquivo)
    {
        // Comentário do Razor e template dentro de <script> ficam de fora: o primeiro não é
        // markup, e o segundo é string que o JS injeta NOUTRO lugar da página — o `.card` dele
        // não está neste arquivo, e cobrar aqui seria acusar quem está certo.
        razor = Regex.Replace(razor, @"@\*.*?\*@", Apagar, RegexOptions.Singleline);
        razor = Regex.Replace(razor, @"<script\b.*?</script>", Apagar, RegexOptions.Singleline);

        var pilha = new List<string>();
        foreach (Match tag in Regex.Matches(razor, @"<(/?)([a-zA-Z][\w-]*)((?:""[^""]*""|'[^']*'|[^>""'])*?)(/?)>",
                                            RegexOptions.Singleline))
        {
            var nome = tag.Groups[2].Value.ToLowerInvariant();
            if (Vazias.Contains(nome)) continue;

            if (tag.Groups[1].Value == "/")
            {
                var ultimo = pilha.FindLastIndex(p => p.StartsWith(nome + " ", StringComparison.Ordinal));
                if (ultimo >= 0) pilha.RemoveRange(ultimo, pilha.Count - ultimo);
                continue;
            }

            var classes = Regex.Match(tag.Groups[3].Value, @"class\s*=\s*""([^""]*)""").Groups[1].Value;
            var peca = Pecas.FirstOrDefault(p => TemClasse(classes, p));

            if (peca != null && !TemClasse(classes, "card") && !pilha.Any(p => TemClasse(p, "card")))
                yield return $"{arquivo}:{razor[..tag.Index].Count(c => c == '\n') + 1}  .{peca}";

            if (tag.Groups[4].Value != "/") pilha.Add(nome + " " + classes);
        }
    }

    private static readonly string[] Pecas = ["card-body", "card-header", "card-footer", "card-img-overlay"];

    // `partial` e `text` são tag helpers do Razor (não fecham); o resto é a lista de elementos
    // vazios do HTML.
    private static readonly HashSet<string> Vazias =
    [
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta",
        "param", "source", "track", "wbr", "partial", "text"
    ];

    private static bool TemClasse(string classes, string nome) =>
        Regex.IsMatch(classes, $@"(^|\s){Regex.Escape(nome)}(\s|$)");

    // Troca o trecho por espaços: some do markup sem mexer na contagem de linhas.
    private static string Apagar(Match m) => Regex.Replace(m.Value, @"[^\n]", " ");

    private static string AberturaDoBlocoDoPalpitrometro()
    {
        var view = LerDaWeb("Views", "Torneios", "_JogosDoTorneio.cshtml");
        var bloco = Regex.Match(view, @"<div class=""[^""]*"">\s*<partial name=""_Palpitrometro""");

        Assert.True(bloco.Success, "não achei o bloco que embrulha o _Palpitrometro no card ao vivo");
        return bloco.Value;
    }

    // O padding lateral é o 2º valor do shorthand (`padding: cima lado baixo`), ou o único
    // quando o shorthand tem um valor só.
    private static string PaddingLateralDe(string seletor)
    {
        var valores = Shorthand(seletor);
        return valores.Length == 1 ? valores[0] : valores[1];
    }

    private static string[] Shorthand(string seletor)
    {
        var css = LerDaWeb("wwwroot", "css", "site.css");
        var regra = Regex.Match(css, @"^" + Regex.Escape(seletor) + @"\s*\{([^}]*)\}",
                                RegexOptions.Singleline | RegexOptions.Multiline);
        Assert.True(regra.Success, $"não achei a regra {seletor} no site.css");

        var padding = Regex.Match(regra.Groups[1].Value, @"(?:^|;|\{)\s*padding:\s*([^;}]+)");
        Assert.True(padding.Success, $"a regra {seletor} precisa declarar `padding`");

        return padding.Groups[1].Value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string LerDaWeb(params string[] caminho) =>
        File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", Path.Combine(caminho)));

    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return dir!.FullName;
    }
}
