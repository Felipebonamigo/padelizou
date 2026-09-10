using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — O DIA DA SEMANA NÃO ENTRA NA ÁRVORE DA CHAVE, E O MOTIVO FOI MEDIDO.
//
// O pedido do Emerson ("colocar o dia da semana na data") entrou nas listas de jogos. Nas duas
// linhas da ÁRVORE do mata-mata ele foi DESFEITO, e este arquivo é a trava pra não voltar.
//
// 📏 MEDIDO NO CHROMIUM COM O `site.css` REAL, a 390px, na coluna mínima da grade
// (`.pdz-arv` é `minmax(6.5rem, 1fr)` no celular):
//
//   vaga da chave      — sem o dia: pede 130px, cabe 127px → "Er Padel" perde 3px de 45
//                        COM o dia: pede 149px, cabe 127px → perde 23px de 45
//   chave projetada    — sem o dia: pede 149px, cabe 123px → perde 27px de 49
//                        COM o dia: pede 173px, cabe 123px → perde 49 de 49 (SOME INTEIRO)
//   mini-jogo do grupo — a linha tem 351px: "Er Padel" fica inteiro dos dois jeitos
//
// 🕳️ O QUE FAZ ISSO SER SILENCIOSO: as duas linhas são `white-space: nowrap; overflow: hidden`
// SEM `text-overflow: ellipsis`, e a etiqueta de quadra/clube é a última da linha
// (`margin-left: auto`). Não há reticências, não há barra de rolagem — o nome do clube
// simplesmente encurta. Numa tela que existe pra dizer QUANDO e ONDE, trocar o ONDE por três
// letras é o pior lado da troca.
//
// 📌 FICA ANOTADO, e NÃO corrigido aqui: a chave projetada já cortava 27px ANTES desta
// mudança. É defeito pré-existente e é outra tarefa — reticências, ou repensar a linha.
public class DiaDaSemanaNaoCabeNaArvoreDaChaveTests
{
    [Theory]
    [InlineData("_ChaveDoMataMata.cshtml", "a vaga da chave")]
    public void A_vaga_da_chave_nao_carrega_o_dia_da_semana(string arquivo, string qualLinha)
    {
        var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", arquivo));

        Assert.False(fonte.Contains("DiaDaSemana.Curto", StringComparison.Ordinal),
            $"O dia da semana voltou pra {qualLinha}: ele come o nome do clube, que é cortado sem reticências.");
    }

    [Fact]
    public void A_chave_projetada_do_Details_tambem_nao()
    {
        var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

        // O `Details.cshtml` tem DUAS datas de jogo: o mini-jogo do grupo (que comporta o dia) e
        // a chave projetada (que não). Por isso a checagem é no trecho da projetada, e não no
        // arquivo inteiro — travar o arquivo todo proibiria também a linha que cabe.
        var inicio = fonte.IndexOf("pdz-chave-projetada-quando", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Não achei a linha da chave projetada.");

        var fim = fonte.IndexOf("pdz-chave-projetada-lado", inicio, StringComparison.Ordinal);
        Assert.True(fim > inicio, "Não achei o fim da linha da chave projetada.");

        Assert.DoesNotContain("DiaDaSemana.Curto", fonte[inicio..fim], StringComparison.Ordinal);
    }

    [Fact]
    public void As_duas_linhas_continuam_sem_reticencias_e_com_a_quadra_por_ultimo()
    {
        // A régua acima só faz sentido enquanto ESTAS duas coisas forem verdade. No dia em que
        // alguém der `text-overflow: ellipsis` à linha (ou tirar o `margin-left: auto` da
        // quadra), a conta muda e a proibição precisa ser reavaliada — e é este teste que avisa.
        var css = File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "wwwroot", "css", "site.css"));

        foreach (var regra in new[] { ".pdz-chave-quando", ".pdz-chave-projetada-quando" })
        {
            var inicio = css.IndexOf(regra + " {", StringComparison.Ordinal);
            Assert.True(inicio >= 0, $"Não achei a regra {regra}.");
            var bloco = css[inicio..css.IndexOf('}', inicio)];

            Assert.Contains("overflow: hidden", bloco);
            Assert.DoesNotContain("text-overflow", bloco);
        }

        Assert.Contains(".pdz-chave-quadra { margin-left: auto;", css);
        Assert.Contains(".pdz-chave-projetada-quadra { margin-left: auto;", css);
    }

    private static string PastaDoProjeto() => Path.Combine(RaizDoRepo(), "Padelizou");

    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "wwwroot", "css", "site.css")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return dir!.FullName;
    }
}
