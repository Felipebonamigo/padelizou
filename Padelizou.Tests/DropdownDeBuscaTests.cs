using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// A LISTA DE SUGESTÕES DA BUSCA POR NOME PRECISA DE FUNDO SÓLIDO.
//
// Reportado pelo Felipe em 17/08/2026, com print: digitando "Emerson P" na inscrição, a lista
// aparecia TRANSPARENTE — dava pra ler o CPF e os campos por baixo, com o texto de trás
// embolado no nome de quem se procura. Ilegível justamente na hora de escolher.
//
// ⚠️ A causa não era falta de estilo, era uma regra CERTA aplicada num lugar errado: o tema
// escuro deixa todo `.list-group-item` transparente de propósito (dentro de um card, o fundo
// do card é que deve aparecer). Mas esta lista FLUTUA sobre o formulário.
//
// ⚠️ E o defeito valia pros DOIS lados da dupla — o dropdown do parceiro tinha o mesmo
// markup e ninguém tinha reparado. Por isso o teste cobra a classe nos dois.
//
// CSS não quebra build nem teste de comportamento: sem isto, a regra pode ser removida numa
// limpeza e o defeito volta em silêncio, visível só pra quem abrir a tela no tema escuro.
public class DropdownDeBuscaTests
{
    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "wwwroot", "css", "site.css")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return dir!.FullName;
    }

    private static string Css() =>
        File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "wwwroot", "css", "site.css"));

    private static string TelaDoTorneio() =>
        File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "Views", "Torneios", "Details.cshtml"));

    [Fact]
    public void A_lista_de_sugestoes_tem_fundo_solido()
    {
        var css = Css();

        // O container e os itens: os dois precisam de fundo. Só o container não basta — o
        // `.list-group-item` transparente por cima continuaria deixando ver o que está atrás.
        Assert.Matches(new Regex(@"\.pdz-busca-lista\s*\{[^}]*background-color:\s*var\(--pdz-surface\)",
            RegexOptions.Singleline), css);

        Assert.Matches(new Regex(@"\.pdz-busca-lista\s+\.list-group-item[^{]*\{[^}]*background-color:\s*var\(--pdz-surface\)",
            RegexOptions.Singleline), css);
    }

    // ⚠️ A regra do tema escuro tem que estar no seletor, senão o `transparent` dela vence por
    // especificidade e a correção não sai do papel — exatamente o estado que o Felipe viu.
    //
    // ⚠️ O SELETOR TEM QUE TERMINAR AQUI (vírgula ou `{`), e isso não é preciosismo de regex:
    // a primeira versão deste teste procurava a substring solta e PASSOU com a regra principal
    // desligada — porque a linha do `:hover` contém o mesmo texto. Descoberto ao falsificar.
    // Fundo certo só no hover é o mesmo defeito: transparente até passar o dedo.
    [Fact]
    public void A_regra_vence_o_transparente_do_tema_escuro()
    {
        Assert.Matches(
            new Regex(@"\[data-bs-theme=""dark""\]\s+\.pdz-busca-lista\s+\.list-group-item\s*[,{]"),
            Css());
    }

    // Os DOIS lados da dupla. O do parceiro já nascia com o mesmo defeito, sem ninguém notar.
    [Fact]
    public void Os_dois_dropdowns_da_inscricao_usam_a_classe()
    {
        var tela = TelaDoTorneio();

        foreach (var id in new[] { "pdzJog1Lista", "pdzParceiroLista" })
        {
            // A classe e o id estão na mesma tag (a classe vem antes do id no markup).
            var temClasse = Regex.IsMatch(tela,
                @"class=""[^""]*pdz-busca-lista[^""]*""\s*\r?\n?\s*id=""" + id + @"""");

            Assert.True(temClasse,
                $"O dropdown {id} não usa .pdz-busca-lista — ele volta a ficar transparente no tema escuro.");
        }
    }
}
