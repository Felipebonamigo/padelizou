using System.Globalization;
using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// 12/09/2026 — O ALVO DE PASSAR O SAQUE ESTAVA NA TELA E NÃO DAVA PRA VER.
//
// 🗣️ Felipe, com um print do card AO VIVO em que ele é o organizador (tem −/+, Finalizar,
// lápis e desfazer): *"Permita aqui tambem mudar a bolinha de qual dupla esta sacando"*.
//
// 🕳️ ELE JÁ PODIA. O `_BolinhaDoSaque` desenha um <form> de 40px do lado de CADA dupla desde
// 11/09/2026, e o −/+ do print sai do MESMO `ehOrganizador` que liga o `podeTrocarSaque` — o
// alvo estava renderizado nas duas linhas. O que não existia era a bola APAGADA: ela é um anel
// de `var(--pdz-border)`, e o cabeçalho do card é navy FIXO nos dois temas. No tema claro esse
// token vale `rgba(28, 39, 66, .09)` — NAVY A 9% EM CIMA DE NAVY, e ainda por cima de um
// `opacity: .55`. Medido no próprio print do Felipe: os 41×41px onde a bola devia estar são
// uma cor CHAPADA, (36, 44, 67), mínimo igual ao máximo. Contraste 1,0:1.
//
// ⚠️ É A TERCEIRA VEZ DO MESMO TOKEN. `--pdz-border` parece "o cinza discreto do tema" e é o
// cinza de UMA LINHA de 1px; virando ícone ele some. Antes apagou a bolinha do check-in
// (12/09) e a linha da chave (que virou o `--pdz-linha-chave`).
//
// ⚠️ E AQUI TEM UMA SEGUNDA CAMADA, que é a regra que o PlacarDoCardAoVivoLegivelNosDoisTemas
// já escreveu pro placar: o cabeçalho do card é ESCURO NOS DOIS TEMAS, então nenhuma cor dele
// pode seguir o tema da página. Enquanto seguir, o defeito volta na próxima cor que alguém
// trocar — foi assim que o número do placar ficou branco no branco por 21 dias.
//
// ⚠️ ESTE TESTE MEDE O QUE O OLHO VÊ, não nome de token: resolve os `var()`, compõe o alpha do
// anel (e o `opacity` da regra) contra o fundo real da linha do card e cobra contraste. Proibir
// um token travaria a solução de hoje; o que não pode voltar é o alvo invisível.
public class AlvoDeTrocarOSaqueEVisivelTests
{
    // 3:1 é o mínimo da WCAG 2.1 pra COMPONENTE não-textual (SC 1.4.11) — a régua de "dá pra
    // enxergar que tem um controle ali", que é exatamente a pergunta deste anel.
    private const double MinimoDeContraste = 3.0;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_bola_apagada_e_o_alvo_e_ela_precisa_ser_vista_no_tema(bool escuro)
    {
        var css = LerDaWeb("wwwroot", "css", "site.css");
        var vars = VariaveisDoTema(css, escuro);

        var fundo = FundoDaLinhaDoCard(css, vars);
        var anel = AnelDaBolaApagada(css, vars);

        Assert.True(Contraste(anel, fundo) >= MinimoDeContraste,
            $"A bola APAGADA — que é o alvo de passar o saque pra outra dupla — sumiu no tema " +
            $"{(escuro ? "escuro" : "claro")}: anel {Hex(anel)} sobre a linha do card {Hex(fundo)} " +
            $"dá {Contraste(anel, fundo):0.00}:1, e o mínimo é {MinimoDeContraste:0.0}:1. " +
            "Quem não vê o alvo não sabe que pode tocar nele.");
    }

    [Fact]
    public void A_cor_do_alvo_nao_muda_com_o_tema_da_pagina()
    {
        // A CAUSA, e não o sintoma. O `.pdz-live-header` é navy nos DOIS temas (gradiente fixo),
        // então uma cor que segue o tema da página está sempre errada em um dos dois — e é por
        // isso que o anel ficou pior no tema CLARO, que é onde `--pdz-border` vira navy.
        var css = LerDaWeb("wwwroot", "css", "site.css");

        var claro = AnelDaBolaApagada(css, VariaveisDoTema(css, escuro: false));
        var escuro = AnelDaBolaApagada(css, VariaveisDoTema(css, escuro: true));

        Assert.True(claro == escuro,
            $"O anel da bola apagada muda com o tema da página ({Hex(claro)} no claro, " +
            $"{Hex(escuro)} no escuro), mas o card AO VIVO é escuro nos dois.");
    }

    // ── O que o olho vê ─────────────────────────────────────────────────────────────────

    // O FUNDO DEBAIXO DA BOLA: o gradiente do cabeçalho com a faixa da linha por cima
    // (`.pdz-live-linha` é um branco a 5%). Do gradiente vale o pedaço mais CLARO — é o pior
    // caso pra um anel claro, e medir pelo melhor caso seria medir a tela que não existe.
    private static (double R, double G, double B) FundoDaLinhaDoCard(string css, Dictionary<string, string> vars)
    {
        var gradiente = Cor(css, vars, ".pdz-live-header", "background");
        var paradas = Cores(gradiente);
        Assert.True(paradas.Count > 0, "Não achei nenhuma cor no fundo do `.pdz-live-header`.");

        var parada = paradas.OrderByDescending(Luminancia).First();
        var maisClara = (parada.R, parada.G, parada.B);

        var faixa = Cores(Cor(css, vars, ".pdz-live-linha", "background")).FirstOrDefault();
        return faixa == default ? maisClara : Sobre(faixa, maisClara);
    }

    // O ANEL: a cor do `box-shadow` da bola apagada, já com o `opacity` da regra embutido —
    // um `opacity` baixo apaga tanto quanto um alpha baixo, e foi ele que multiplicou o defeito.
    private static (double R, double G, double B) AnelDaBolaApagada(string css, Dictionary<string, string> vars)
    {
        var regra = Declaracoes(css, ".pdz-bolinha-apagada");
        Assert.True(regra.Count > 0, "A regra `.pdz-bolinha-apagada` sumiu do site.css.");

        var sombra = regra.GetValueOrDefault("box-shadow")
            ?? throw new Xunit.Sdk.XunitException("A bola apagada perdeu o `box-shadow` — sem ele não há anel nenhum.");

        var cor = Cores(Resolver(sombra, vars)).FirstOrDefault();
        Assert.True(cor != default, $"Não achei uma cor conferível no `box-shadow` da bola apagada: `{sombra}`.");

        var opacidade = double.TryParse(regra.GetValueOrDefault("opacity"), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var o) ? o : 1.0;

        var fundo = FundoDaLinhaDoCard(css, vars);
        return Sobre((cor.R, cor.G, cor.B, cor.A * opacidade), fundo);
    }

    // Alpha por cima de uma cor sólida — a conta que o navegador faz pra pintar o pixel.
    private static (double R, double G, double B) Sobre((double R, double G, double B, double A) frente,
                                                        (double R, double G, double B) fundo) =>
        (frente.A * frente.R + (1 - frente.A) * fundo.R,
         frente.A * frente.G + (1 - frente.A) * fundo.G,
         frente.A * frente.B + (1 - frente.A) * fundo.B);

    // ── A leitura do CSS (mesmo desenho do PlacarDoCardAoVivoLegivelNosDoisTemas) ────────

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

        // ⚠️ COMENTÁRIO FORA ANTES DE TUDO: este CSS cita CSS no meio da explicação, e a citação
        // abre uma declaração falsa que engole até o `;` de verdade.
        css = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
        // ⚠️ O seletor tem que começar A LINHA: senão `.pdz-bolinha-apagada` casaria também
        // dentro de `.pdz-saque-toque:hover .pdz-bolinha-apagada`, e o teste mediria o hover
        // achando que media o estado parado.
        foreach (Match bloco in Regex.Matches(css, @"(?m)^[ \t]*" + Regex.Escape(seletor) + @"[ \t]*\{([^}]*)\}"))
            foreach (Match decl in Regex.Matches(bloco.Groups[1].Value, @"([\w-]+)\s*:\s*([^;]+);"))
                achadas[decl.Groups[1].Value.Trim()] = decl.Groups[2].Value.Trim();
        return achadas;
    }

    private static string Cor(string css, Dictionary<string, string> vars, string seletor, string propriedade)
    {
        var valor = Declaracoes(css, seletor).GetValueOrDefault(propriedade)
            ?? throw new Xunit.Sdk.XunitException($"Não achei `{propriedade}` em `{seletor}` no site.css.");
        return Resolver(valor, vars);
    }

    private static string Resolver(string valor, Dictionary<string, string> vars)
    {
        for (int volta = 0; volta < 5 && valor.Contains("var(", StringComparison.Ordinal); volta++)
            valor = Regex.Replace(valor, @"var\(\s*(--[\w-]+)\s*\)", m => vars.GetValueOrDefault(m.Groups[1].Value, m.Value));
        return valor.Trim();
    }

    // Toda cor conferível de um valor, na ordem em que aparecem: #rgb, #rrggbb, rgb() e rgba().
    private static List<(double R, double G, double B, double A)> Cores(string valor)
    {
        var achadas = new List<(double, double, double, double)>();
        foreach (Match m in Regex.Matches(valor,
            @"#([0-9a-fA-F]{6}|[0-9a-fA-F]{3})\b|rgba?\(\s*([\d.]+)[\s,]+([\d.]+)[\s,]+([\d.]+)\s*(?:[,/]\s*([\d.]+)\s*)?\)"))
        {
            if (m.Groups[1].Success)
            {
                var d = m.Groups[1].Value;
                if (d.Length == 3) d = string.Concat(d.Select(c => $"{c}{c}"));
                achadas.Add((Canal(d, 0), Canal(d, 1), Canal(d, 2), 1.0));
            }
            else
            {
                achadas.Add((Numero(m.Groups[2]), Numero(m.Groups[3]), Numero(m.Groups[4]),
                             m.Groups[5].Success ? Numero(m.Groups[5]) : 1.0));
            }
        }
        return achadas;

        static double Canal(string d, int i) => int.Parse(d.Substring(i * 2, 2), NumberStyles.HexNumber);
        static double Numero(Group g) => double.Parse(g.Value, CultureInfo.InvariantCulture);
    }

    // ── O contraste da WCAG ─────────────────────────────────────────────────────────────

    private static double Contraste((double R, double G, double B) a, (double R, double G, double B) b)
    {
        double l1 = Luminancia(a), l2 = Luminancia(b);
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }

    private static double Luminancia((double R, double G, double B, double A) cor) =>
        Luminancia((cor.R, cor.G, cor.B));

    private static double Luminancia((double R, double G, double B) cor)
    {
        static double Linear(double canal)
        {
            double v = canal / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linear(cor.R) + 0.7152 * Linear(cor.G) + 0.0722 * Linear(cor.B);
    }

    private static string Hex((double R, double G, double B) cor) =>
        $"#{(int)Math.Round(cor.R):x2}{(int)Math.Round(cor.G):x2}{(int)Math.Round(cor.B):x2}";

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
