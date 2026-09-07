using Xunit;

namespace Padelizou.Tests;

// 07/09/2026 — A ABA "PALPITEIROS" dentro da página do torneio: pedido do Felipe — "Ja deixe
// uma Aba no torneio para verificar o ranking do palpitometro".
//
// ⚠️ POR QUE TESTE DE FONTE: a suíte não renderiza Razor. A REGRA de quando o ranking existe
// (jogos apurados > 0 e algum palpite bateu com eles) já tem trava de comportamento de verdade
// em RankingDePalpiteirosTests — o que só existe na VIEW (a aba, o gate pela mesma checagem
// barata do botão, o painel reusando a parcial) não tem como um teste de comportamento
// alcançar.
public class AbaPalpiteirosNaPaginaDoTorneioTests
{
    [Fact]
    public void A_aba_existe_e_e_gateada_por_TemRankingDePalpiteiros()
    {
        var fonte = Details();

        var inicioNav = fonte.IndexOf("id=\"palpiteiros-tab\"", StringComparison.Ordinal);
        Assert.True(inicioNav >= 0, "Não achei o botão da aba Palpiteiros (id=\"palpiteiros-tab\").");

        // O `@if (ViewBag.TemRankingDePalpiteiros == true)` que gateia o BOTÃO da aba vem
        // ANTES dele no arquivo — a MESMA checagem barata que já decide o botão fora das abas,
        // pra não montar o ranking inteiro em toda visita à página mais visitada do site.
        var gate = fonte.LastIndexOf("ViewBag.TemRankingDePalpiteiros == true", inicioNav, StringComparison.Ordinal);
        Assert.True(gate >= 0 && gate < inicioNav,
            "O botão da aba Palpiteiros precisa estar atrás de um `ViewBag.TemRankingDePalpiteiros == true`.");
    }

    [Fact]
    public void O_painel_da_aba_tambem_e_gateado_e_reusa_a_parcial_do_ranking()
    {
        var bloco = BlocoDoPainel();

        Assert.Contains("id=\"palpiteiros\"", bloco);
        // Não é uma quarta cópia do pódio/régua/tabela — reusa a MESMA parcial que a página
        // cheia (/Torneios/Palpiteiros) e o hub do Ranking já usam.
        Assert.Contains("_RankingDePalpiteiros", bloco);
        Assert.Contains("ViewBag.RankingDePalpiteiros", bloco);
    }

    private static string BlocoDoPainel()
    {
        var fonte = Details();

        var inicioPainel = fonte.IndexOf("id=\"palpiteiros\" role=\"tabpanel\"", StringComparison.Ordinal);
        Assert.True(inicioPainel >= 0, "Não achei a abertura do painel da aba Palpiteiros.");

        var fim = fonte.IndexOf("<!-- ABA: CHAVES E GRUPOS", inicioPainel, StringComparison.Ordinal);
        Assert.True(fim > inicioPainel, "Não achei o fim do painel da aba Palpiteiros (a aba de Chaves e Grupos vem depois dela).");

        return fonte[inicioPainel..fim];
    }

    private static string Details() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
