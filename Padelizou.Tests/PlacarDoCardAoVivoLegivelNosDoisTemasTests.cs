using System.Globalization;
using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// 11/09/2026 — O NÚMERO DO PLACAR AO VIVO SUMIA NO TEMA CLARO.
//
// Achado de carona enquanto o verde do card era consertado, e medido no navegador (Chromium
// da própria sessão): `color: rgb(255,255,255)` sobre `background-color: rgb(255,255,255)`.
// Branco no branco — o organizador abria a lista de jogos e via CAIXAS VAZIAS no lugar dos
// games, desde 21/08/2026.
//
// 🕳️ A CAUSA É UMA MISTURA DE DOIS MUNDOS. O cabeçalho do card é escuro NOS DOIS TEMAS (um
// gradiente navy fixo, `.pdz-live-header`), mas o campo de placar que mora nele pegava
// `background: var(--pdz-surface)` — o token que SEGUE o tema da página e vale `#ffffff` no
// claro — com a tinta cravada em `#fff`. No tema escuro os dois combinavam por coincidência;
// no claro, o número deixava de existir. Nenhum teste da suíte vê cor, e a tela não dá erro:
// o placar simplesmente não está lá.
//
// ⚠️ ESTE TESTE MEDE CONTRASTE, NÃO NOME DE TOKEN. Proibir `var(--pdz-surface)` numa linha
// travaria a solução de hoje, não o defeito; o que não pode voltar é o número ilegível — em
// QUALQUER dos dois temas, na cor normal e na de quem venceu.
public class PlacarDoCardAoVivoLegivelNosDoisTemasTests
{
    // 3:1 é o mínimo da WCAG AA pra texto grande — e este é 1.9rem em peso 800, lido de pé,
    // no balcão, com a quadra rolando. Abaixo disso não é estilo, é placar que não se lê.
    private const double MinimoDeContraste = 3.0;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void O_numero_do_placar_se_le_no_tema(bool escuro)
    {
        var css = LerDaWeb("wwwroot", "css", "site.css");
        var vars = VariaveisDoTema(css, escuro);

        var fundo = Cor(css, vars, ".pdz-live-input", "background");
        var tinta = Cor(css, vars, ".pdz-live-input", "color");

        Assert.True(Contraste(tinta, fundo) >= MinimoDeContraste,
            $"O placar do card AO VIVO ficou ilegível no tema {(escuro ? "escuro" : "claro")}: " +
            $"tinta {tinta} sobre fundo {fundo} dá {Contraste(tinta, fundo):0.00}:1.");
    }

    // O verde de quem venceu é a cor que este card existe pra mostrar — ele não pode ser o
    // caso que fica ilegível.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void O_verde_de_quem_venceu_se_le_no_tema(bool escuro)
    {
        var css = LerDaWeb("wwwroot", "css", "site.css");
        var vars = VariaveisDoTema(css, escuro);

        var fundo = Cor(css, vars, ".pdz-live-input", "background");
        var lime = Cor(css, vars, ".pdz-live-placar-venceu .pdz-live-input", "color");

        Assert.True(Contraste(lime, fundo) >= MinimoDeContraste,
            $"O verde de quem venceu ficou ilegível no tema {(escuro ? "escuro" : "claro")}: " +
            $"{lime} sobre {fundo} dá {Contraste(lime, fundo):0.00}:1.");
    }

    // ⚠️ E A REGRA GERAL, que é a causa e não o sintoma: o placar do card AO VIVO mora num
    // fundo ESCURO FIXO, então NENHUMA cor dele pode seguir o tema da página. Enquanto
    // seguirem, o defeito volta na próxima cor que alguém trocar — foi assim que o número
    // ficou branco no branco por 21 dias, e é o que deixa o −/+ virar dois botões brancos
    // gritando num card escuro.
    [Theory]
    [InlineData(".pdz-live-input", "background")]
    [InlineData(".pdz-live-input", "color")]
    [InlineData(".pdz-live-passo", "background")]
    [InlineData(".pdz-live-passo", "color")]
    public void A_cor_do_placar_nao_muda_com_o_tema_da_pagina(string seletor, string propriedade)
    {
        var css = LerDaWeb("wwwroot", "css", "site.css");

        var claro = Cor(css, VariaveisDoTema(css, escuro: false), seletor, propriedade);
        var escuro = Cor(css, VariaveisDoTema(css, escuro: true), seletor, propriedade);

        Assert.True(claro == escuro,
            $"`{propriedade}` de `{seletor}` muda com o tema ({claro} no claro, {escuro} no escuro), " +
            "mas o card AO VIVO é escuro nos dois — é essa diferença que apaga o placar.");
    }

    // ── A leitura do CSS ────────────────────────────────────────────────────────────────

    // As variáveis valendo num tema: o `:root` é a base e o bloco do escuro sobrescreve o que
    // redefine — a mesma cascata que o navegador faz.
    private static Dictionary<string, string> VariaveisDoTema(string css, bool escuro)
    {
        var vars = Declaracoes(css, ":root");
        if (escuro)
            foreach (var (chave, valor) in Declaracoes(css, ":root[data-bs-theme=\"dark\"]"))
                vars[chave] = valor;
        return vars;
    }

    private static Dictionary<string, string> Declaracoes(string css, string seletor)
    {
        var achadas = new Dictionary<string, string>();

        // ⚠️ COMENTÁRIO FORA ANTES DE TUDO. Este arquivo explica muita coisa citando CSS no
        // meio do texto ("`color: inherit` NÃO SERVE AQUI"), e uma citação dessas abre uma
        // declaração falsa que engole até o `;` seguinte — o de verdade. Aconteceu aqui.
        css = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
        // ⚠️ O seletor tem que começar A LINHA. Sem isso, `.pdz-live-input` casaria também
        // dentro de `.pdz-live-placar-venceu .pdz-live-input`, e o teste mediria o verde de
        // quem venceu achando que media a cor normal — passou por aqui na primeira rodada.
        foreach (Match bloco in Regex.Matches(css, @"(?m)^[ \t]*" + Regex.Escape(seletor) + @"[ \t]*\{([^}]*)\}"))
            foreach (Match decl in Regex.Matches(bloco.Groups[1].Value, @"([\w-]+)\s*:\s*([^;]+);"))
                achadas[decl.Groups[1].Value.Trim()] = decl.Groups[2].Value.Trim();
        return achadas;
    }

    // A cor que um seletor pinta numa propriedade, com os `var(--x)` já resolvidos. A última
    // declaração vence, como na cascata.
    private static string Cor(string css, Dictionary<string, string> vars, string seletor, string propriedade)
    {
        var valor = Declaracoes(css, seletor).GetValueOrDefault(propriedade)
            ?? throw new Xunit.Sdk.XunitException($"Não achei `{propriedade}` em `{seletor}` no site.css.");

        for (int volta = 0; volta < 5 && valor.Contains("var(", StringComparison.Ordinal); volta++)
            valor = Regex.Replace(valor, @"var\(\s*(--[\w-]+)\s*\)", m => vars.GetValueOrDefault(m.Groups[1].Value, m.Value));

        return valor.Trim();
    }

    // ── O contraste da WCAG ─────────────────────────────────────────────────────────────

    private static double Contraste(string cor1, string cor2)
    {
        double l1 = Luminancia(cor1), l2 = Luminancia(cor2);
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }

    private static double Luminancia(string cor)
    {
        var hex = Regex.Match(cor, @"#([0-9a-fA-F]{6}|[0-9a-fA-F]{3})\b");
        if (!hex.Success)
            throw new Xunit.Sdk.XunitException(
                $"`{cor}` não é uma cor sólida que dê pra medir. O número do placar precisa de " +
                "uma cor conferível nos dois temas — não de uma que só o navegador sabe resolver.");

        var d = hex.Groups[1].Value;
        if (d.Length == 3) d = string.Concat(d.Select(c => $"{c}{c}"));

        double Canal(int i)
        {
            double v = int.Parse(d.Substring(i * 2, 2), NumberStyles.HexNumber) / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Canal(0) + 0.7152 * Canal(1) + 0.0722 * Canal(2);
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
