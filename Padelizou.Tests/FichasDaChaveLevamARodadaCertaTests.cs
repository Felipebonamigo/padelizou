using Xunit;

namespace Padelizou.Tests;

// 12/09/2026 — AS FICHAS "OITAVAS · QUARTAS · SEMIFINAL · FINAL" DA PRÉVIA DO MATA-MATA.
//
// 🗣️ Felipe, com o print da prévia no celular: *"Esse botao de oitavas quartas semi e final, as
// vezes n funciona"*.
//
// 🕳️ ERAM DOIS DEFEITOS SOMADOS, e é por isso que falhava "às vezes" em vez de sempre. Os dois
// medidos no Chromium a 412px com o `site.css` de verdade, antes da correção:
//
//   1. ID REPETIDO. O partial é desenhado UMA VEZ POR CATEGORIA (Details.cshtml, dentro do laço
//      dos `.tab-pane`), e todas escreviam `id="pdz-chd-rodada-0..N"`. `href="#pdz-chd-rodada-1"`
//      acha o PRIMEIRO do documento — que mora no painel de OUTRA categoria, escondido por
//      `display: none`. Rolar pra um elemento invisível não faz nada, então num torneio de duas
//      ou mais categorias com prévia (o ER tem sete) NENHUMA ficha funcionava, exceto as da
//      primeira do DOM. Medido: `scrollLeft` ficou em 0 nos quatro cliques.
//
//   2. O ENCAIXE DESFAZIA O PULO. O trilho é `scroll-snap-type: x mandatory` e a navegação por
//      âncora alinha com `inline: nearest` — o mínimo pra caber. A rodada tem 66% da tela, então
//      esse mínimo para ENTRE dois pontos de encaixe, e o snap puxa de volta pro anterior.
//      Medido com uma categoria só (ids já únicos, portanto): QUARTAS não saía do lugar,
//      SEMIFINAL parava nas QUARTAS e FINAL parava na SEMIFINAL — sempre uma rodada atrás.
//      De quebra, a âncora rolava a PÁGINA (scrollY 0 → 639) e levava a própria barra de fichas
//      pra fora da tela.
//
// ✅ A CORREÇÃO: id único por categoria (conserta o fallback sem JS e o HTML inválido) e
// `js/chave-fichas.js`, que rola o TRILHO no lugar da âncora — `preventDefault()` mata o pulo
// vertical e o encaixe não tem o que desfazer, porque a rolagem para exatamente no ponto de
// encaixe. O comportamento do script está travado em `Padelizou.Tests/js/conferir-fichas-da-chave.js`
// (o `dotnet test` não enxerga `.js`; quem roda é o CI, no laço dos `conferir-*.js`).
//
// ⚠️ POR QUE ISTO É UM TESTE DE TEXTO: nada na suíte renderiza Razor nem tem navegador. Sem
// estas travas, alguém "simplifica" a ficha de volta pra âncora pura e nenhum outro teste fica
// vermelho — que é exatamente como o defeito entrou.
public class FichasDaChaveLevamARodadaCertaTests
{
    private static string Partial() =>
        File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "Views", "Torneios",
                                      "_ChaveDoMataMata.cshtml"));

    private static string Details() =>
        File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "Views", "Torneios", "Details.cshtml"));

    private static string Script() =>
        File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "wwwroot", "js", "chave-fichas.js"));

    // SÓ O CÓDIGO, sem os comentários: eles NOMEIAM o que não pode ser usado ("nunca
    // `document.getElementById`"), e uma conferência de texto crua reprova o próprio aviso.
    private static string CodigoDoScript() =>
        string.Join("\n", Script().Split('\n').Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));

    [Fact]
    public void Cada_categoria_tem_ids_de_rodada_so_dela()
    {
        var fonte = Partial();

        // O id da rodada tem que carregar a CATEGORIA. Sem isso, sete categorias escrevem os
        // mesmos quatro ids e a âncora sempre cai na primeira do documento — escondida.
        Assert.DoesNotContain("id=\"pdz-chd-rodada-", fonte);
        Assert.DoesNotContain("href=\"#pdz-chd-rodada-", fonte);
        Assert.Contains("id=\"pdz-chd-@(Model.Categoria)-rodada-@r\"", fonte);
        Assert.Contains("href=\"#pdz-chd-@(Model.Categoria)-rodada-@r\"", fonte);

        // E a categoria chega pelo MODELO, não por ViewData/contador de render: é o único jeito
        // de o id ser o mesmo na ficha e na rodada sem uma segunda fonte da verdade.
        Assert.Contains("int Categoria)", fonte);
    }

    [Fact]
    public void A_pagina_do_torneio_passa_a_categoria_e_carrega_o_script()
    {
        var details = Details();

        // O partial só consegue montar o id único se quem o desenha entregar a categoria — e
        // são DUAS chamadas desde 12/09/2026 (a prévia e a chave de verdade usam o mesmo
        // partial), então as duas precisam entregar.
        Assert.Equal(2, details.Split("<partial name=\"_ChaveDoMataMata\"").Length - 1);
        Assert.Equal(2, details.Split("categoria.Id)\" />").Length - 1);

        // Sem o script carregado, a ficha volta a ser âncora pura — e volta o "às vezes".
        Assert.Contains("~/js/chave-fichas.js", details);
    }

    [Fact]
    public void A_ficha_rola_o_trilho_em_vez_de_deixar_com_a_ancora()
    {
        var js = CodigoDoScript();

        // ⚠️ `preventDefault` é a linha que impede os DOIS estragos da âncora: o encaixe
        // desfazendo o pulo (que parava uma rodada atrás) e a página inteira rolando pra levar
        // a barra de fichas pra fora da tela.
        Assert.Contains("preventDefault", js);

        // Quem rola é o TRILHO da própria categoria, e a busca é dentro do quadro — nunca
        // `document.getElementById`, que é justamente o que encontra o painel escondido.
        Assert.Contains(".pdz-chd-trilho", js);
        Assert.DoesNotContain("document.getElementById", js);
        Assert.DoesNotContain("document.querySelector(", js);
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
