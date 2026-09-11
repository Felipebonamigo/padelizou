using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// 11/09/2026 — O `site.css` NÃO PODE TER CHAVE SOBRANDO.
//
// 🕳️ NASCEU DE UM ESTRAGO DE VERDADE, e é por isso que ele existe. Resolvendo um conflito de
// merge no `site.css` eu engoli o `}` que fechava o `.pdz-jl-escudo`. A partir dali, TODA regra
// seguinte virou declaração solta dentro dela — `.pdz-col-num`, `.pdz-tabela-grupo` e o que
// viesse depois pararam de existir.
//
// ⚠️ E A SUÍTE INTEIRA PASSOU: 6.421 testes, 0 falhas, com o arquivo quebrado. São 104 arquivos
// de teste que leem fonte com `File.ReadAllText` e procuram substring — nenhum deles PARSEIA
// coisa nenhuma, então "a regra está escrita no arquivo" continuava verdadeiro enquanto "a
// regra é aplicada pelo navegador" tinha deixado de ser.
//
// Esta é a conferência mais barata que separa as duas: contar chaves.
public class CssNaoPodeFicarQuebradoTests
{
    [Fact]
    public void As_chaves_do_site_css_fecham_todas()
    {
        var css = SemComentarioDeCss(Ler("site.css"));

        var abre = css.Count(c => c == '{');
        var fecha = css.Count(c => c == '}');

        Assert.True(abre == fecha,
            $"O site.css está desbalanceado: {abre} `{{` para {fecha} `}}`. "
            + "Uma chave que falta transforma toda regra seguinte em declaração solta — e nenhum "
            + "teste de substring percebe, porque o texto continua lá.");
    }

    [Fact]
    public void E_nenhuma_regra_fica_aberta_por_tempo_demais()
    {
        // Contar chaves acha o desbalanceamento do arquivo INTEIRO, mas não acha o caso em que
        // duas faltam e uma sobra. Um bloco de regra normal é curto; um que engoliu as regras
        // seguintes é enorme. O teto é folgado de propósito — o maior bloco legítimo hoje tem
        // bem menos que isso, e o que quebrou tinha o arquivo inteiro dentro.
        var css = SemComentarioDeCss(Ler("site.css"));

        foreach (Match m in Regex.Matches(css, @"\{[^{}]*\}", RegexOptions.Singleline))
        {
            Assert.True(m.Length < 2500,
                $"Bloco de regra com {m.Length} caracteres — provável `}}` faltando antes dele. Começa em: "
                + m.Value[..Math.Min(120, m.Value.Length)].Replace('\n', ' '));
        }
    }

    // Chave dentro de comentário não conta: este arquivo explica seletor em prosa o tempo todo.
    private static string SemComentarioDeCss(string css) =>
        Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);

    private static string Ler(string arquivo) =>
        File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "wwwroot", "css", arquivo));

    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "wwwroot", "css", "site.css")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return dir!.FullName;
    }
}
