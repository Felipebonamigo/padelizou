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
    // ⚠️ ERAM DOIS ARQUIVOS até 12/09/2026 (a chave de verdade e a prévia). Viraram UM: a aba
    // trocava de desenho no dia em que o mata-mata nascia, e o Felipe pediu pra manter como
    // estava. A régua não mudou — mudou o número de lugares onde ela pode ser desfeita.
    [InlineData("_ChaveDoMataMata.cshtml", "a vaga da chave")]
    public void A_vaga_da_chave_nao_carrega_o_dia_da_semana(string arquivo, string qualLinha)
    {
        var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", arquivo));

        Assert.False(fonte.Contains("DiaDaSemana.Curto", StringComparison.Ordinal),
            $"O dia da semana voltou pra {qualLinha}: ele come o nome do clube, que é cortado sem reticências.");
    }

    // ⚠️ O TESTE QUE FICAVA AQUI RECORTAVA UM TRECHO DO `Details.cshtml` pela classe
    // `pdz-chave-projetada-quando`, porque a prévia era markup solto no meio de um arquivo que
    // tem OUTRA data de jogo (o mini-jogo do grupo, onde o dia da semana cabe e fica). Em
    // 11/09/2026 a prévia virou um partial com o cartão da chave de verdade: o recorte deixou de
    // ser necessário, e o arquivo inteiro entrou na Theory acima — que é uma trava mais forte,
    // porque pega o arquivo todo em vez de uma janela de texto.

    [Fact]
    public void A_linha_da_vaga_continua_sem_reticencias_e_com_a_quadra_por_ultimo()
    {
        // A régua acima só faz sentido enquanto ESTAS duas coisas forem verdade. No dia em que
        // alguém der `text-overflow: ellipsis` à linha (ou tirar o `margin-left: auto` da
        // quadra), a conta muda e a proibição precisa ser reavaliada — e é este teste que avisa.
        var css = File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "wwwroot", "css", "site.css"));

        // É UMA LINHA SÓ desde 11/09/2026: a prévia passou a usar `.pdz-chave-quando`, a mesma
        // da chave de verdade, e a régua que valia pras duas passou a ter um lugar só pra morar.
        foreach (var regra in new[] { ".pdz-chave-quando" })
        {
            var inicio = css.IndexOf(regra + " {", StringComparison.Ordinal);
            Assert.True(inicio >= 0, $"Não achei a regra {regra}.");
            var bloco = css[inicio..css.IndexOf('}', inicio)];

            Assert.Contains("overflow: hidden", bloco);
            Assert.DoesNotContain("text-overflow", bloco);
        }

        Assert.Contains(".pdz-chave-quadra { margin-left: auto;", css);
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
