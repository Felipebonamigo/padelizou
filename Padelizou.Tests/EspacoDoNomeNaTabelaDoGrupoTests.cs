using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// 11/09/2026 — O NOME DA DUPLA FICA COM A SOBRA DA TABELA DO GRUPO, E NÃO OS NÚMEROS.
//
// 🗣️ Felipe, com um print da Fase de Grupos: *"Quero que os numeros com J V D SG fiquem um
// pouco menos espaçados, para que caiba mais do nome das pessoas"*.
//
// O chip compacto de 10/09/2026 (avatar 36→26px, clube escondido, nome truncando) resolveu a
// ALTURA da linha e deixou de lado a LARGURA: a célula da dupla ganhou teto de 180px e, com
// `table-layout: auto`, toda a sobra do card voltava pras quatro colunas de UM DÍGITO — no
// print, "Augusto Ohlweiler" sai "Augusto Ohl…" ao lado de um "0" com uma tira de ~90px só
// pra ele.
//
// ⚠️ TIRAR O TETO DE 180px SOZINHO NÃO RESOLVERIA, e parece que resolveria: em layout `auto`
// a coluna sem teto passa a pedir o NOME INTEIRO (o chip é `white-space: nowrap`), e aí ou a
// tabela estoura a `.table-responsive` e rola de lado, ou o navegador reparte a sobra do jeito
// dele de novo. Quem decide é o `table-layout: fixed`: as quatro colunas pedem o que precisam,
// e a única sem largura declarada — a da dupla — fica com o resto.
public class EspacoDoNomeNaTabelaDoGrupoTests
{
    [Fact]
    public void A_tabela_do_grupo_reparte_por_largura_declarada()
    {
        // `fixed` é o que faz a sobra ir pra coluna da dupla em vez de ser distribuída entre
        // todas. Sem isso as larguras abaixo viram só um PISO pras colunas de número.
        Assert.Matches(new Regex(@"table-layout:\s*fixed"), RegraDoCss(@"\.pdz-grupo-tabela"));
    }

    [Fact]
    public void Os_numeros_ocupam_so_o_que_um_digito_precisa()
    {
        var regra = RegraDoCss(@"\.pdz-grupo-tabela th:not\(:first-child\),\s*\.pdz-grupo-tabela td:not\(:first-child\)");

        var largura = ValorEmPx(regra, "width");
        Assert.True(largura <= 34, $"A coluna de número está com {largura}px — um dígito não precisa disso.");

        // O `.5rem` de cada lado que o Bootstrap dá a toda célula (`--bs-table-cell-padding-x`)
        // é, sozinho, mais largo que o dígito que ele emoldura.
        Assert.True(ValorEmRem(regra, "padding-left") <= .2m, "O padding lateral do Bootstrap continua valendo na coluna de número.");
        Assert.True(ValorEmRem(regra, "padding-right") <= .2m, "O padding lateral do Bootstrap continua valendo na coluna de número.");
    }

    [Fact]
    public void O_saldo_de_games_e_mais_largo_que_as_outras_tres()
    {
        // ⚠️ Em layout fixo a célula NÃO cresce pelo conteúdo: apertar o SG no mesmo tamanho de
        // J/V/D corta o "+12" em vez de empurrar a coluna. J/V/D guardam um dígito; o SG guarda
        // sinal + dois.
        var saldo = ValorEmPx(RegraDoCss(@"\.pdz-grupo-tabela th:last-child,\s*\.pdz-grupo-tabela td:last-child"), "width");
        var outras = ValorEmPx(RegraDoCss(@"\.pdz-grupo-tabela th:not\(:first-child\),\s*\.pdz-grupo-tabela td:not\(:first-child\)"), "width");

        Assert.True(saldo > outras, $"O SG ({saldo}px) precisa ser mais largo que J/V/D ({outras}px).");
        Assert.True(saldo >= 40, $"O SG com {saldo}px não cabe um \"+12\" em negrito.");
    }

    [Fact]
    public void A_coluna_da_dupla_nao_tem_mais_teto_na_view()
    {
        var tabela = TabelaDoGrupoNaView();

        Assert.Contains("pdz-grupo-tabela", tabela);

        // Os dois tetos que devolviam a sobra pros números. O `width: 50%` no cabeçalho
        // reservava metade do card pros quatro dígitos; o `max-width: 180px` impedia a coluna
        // da dupla de aceitar a sobra mesmo depois do layout fixo.
        Assert.DoesNotMatch(new Regex(@"width:\s*50%"), tabela);
        Assert.DoesNotMatch(new Regex(@"max-width:\s*180px"), tabela);
    }

    // A `<table>` da fase de grupos dentro do Details.cshtml: do `<table` que antecede o
    // cabeçalho "Saldo de Games" até o `</table>` seguinte. Recortar por AQUI, e não pelo
    // arquivo inteiro, porque `width: 50%` é comum em outras partes de uma view de 8.000 linhas.
    private static string TabelaDoGrupoNaView()
    {
        var view = File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "Views", "Torneios", "Details.cshtml"));

        var sg = view.IndexOf("<th title=\"Saldo de Games\">SG</th>", StringComparison.Ordinal);
        Assert.True(sg > 0, "Não achei o cabeçalho SG da tabela do grupo no Details.cshtml.");

        var abre = view.LastIndexOf("<table", sg, StringComparison.Ordinal);
        var fecha = view.IndexOf("</table>", sg, StringComparison.Ordinal);
        Assert.True(abre > 0 && fecha > abre, "Não achei a `<table>` que envolve o cabeçalho SG.");

        // Sem os COMENTÁRIOS: o `width: 50%` que este trabalho tirou está citado na prosa que
        // explica por que ele saiu, e o guarda é sobre a marcação, não sobre o que ela conta.
        var tabela = view[abre..fecha];
        tabela = Regex.Replace(tabela, @"@\*.*?\*@", "", RegexOptions.Singleline);
        return Regex.Replace(tabela, @"<!--.*?-->", "", RegexOptions.Singleline);
    }

    private static int ValorEmPx(string regra, string propriedade)
    {
        var m = Regex.Match(regra, propriedade + @":\s*(\d+)px");
        Assert.True(m.Success, $"Não achei `{propriedade}` em px nesta regra.");
        return int.Parse(m.Groups[1].Value);
    }

    private static decimal ValorEmRem(string regra, string propriedade)
    {
        var m = Regex.Match(regra, propriedade + @":\s*(\d*\.?\d+)rem");
        Assert.True(m.Success, $"Não achei `{propriedade}` em rem nesta regra.");
        return decimal.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string RegraDoCss(string seletor)
    {
        var css = File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "wwwroot", "css", "site.css"));
        var m = Regex.Match(css, seletor + @"\s*\{([^}]*)\}", RegexOptions.Singleline);
        Assert.True(m.Success, $"Não achei a regra `{seletor}` no site.css.");
        return m.Groups[1].Value;
    }

    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "wwwroot", "css", "site.css")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return dir!.FullName;
    }
}
