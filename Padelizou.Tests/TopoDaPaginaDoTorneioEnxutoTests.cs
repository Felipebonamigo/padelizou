using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — O TOPO DA PÁGINA DO TORNEIO PERDE PESO.
//
// 🗣️ Felipe, com o print do 2ª Etapa ER Padel Tour no celular: *"estou achando muito poluído
// essa tela, muita informação"* — e, olhando a lista de botões: *"palpiteiros me parece
// duplicado, não?"*.
//
// Estava. Entre o topo e o primeiro jogo havia DEZ blocos, e três deles eram botão com legenda
// permanente embaixo. O "Palpiteiros" aparecia DUAS vezes com o mesmo ícone e o mesmo nome: um
// botão pra página cheia (/Torneios/Palpiteiros) e uma aba (#palpiteiros) — os dois desenhando
// o MESMO parcial `_RankingDePalpiteiros`, atrás do MESMO portão. O botão era o mais antigo; a
// aba nasceu em 07/09 a pedido dele e ninguém tirou o botão.
public class TopoDaPaginaDoTorneioEnxutoTests
{
    [Fact]
    public void Palpiteiros_e_anunciado_UMA_vez_so_acima_das_abas()
    {
        // A escolha entre dois caminhos idênticos não é escolha: é hesitação cobrada de quem
        // só queria ver o jogo.
        var topo = TestInfra.SemComentarios(AcimaDasAbas());

        Assert.DoesNotContain("Palpiteiros", topo);
    }

    [Fact]
    public void Mas_a_ABA_Palpiteiros_continua_existindo()
    {
        // É ela que o Felipe pediu em 07/09 ("deixe uma aba no torneio para verificar o ranking
        // do palpitômetro"). Tirar o botão não podia levar a aba junto.
        var view = TestInfra.SemComentarios(Details());

        Assert.Contains("id=\"palpiteiros-tab\"", view);
        Assert.Contains("data-bs-target=\"#palpiteiros\"", view);
    }

    [Fact]
    public void E_a_PAGINA_CHEIA_nao_fica_orfa()
    {
        // 🕳️ `Details.cshtml` era o ÚNICO lugar do sistema que linkava pra /Torneios/Palpiteiros
        // (conferido: nenhum outro .cshtml ou .cs aponta pra ação). Tirar o botão sem mais nada
        // deixaria a página alcançável só por quem digitasse a URL — e ela existe justamente
        // pra ser mandada no grupo, como o comentário do `_RankingDePalpiteiros` diz.
        //
        // O link foi pra DENTRO do painel da aba: quem já está olhando o ranking é quem quer a
        // URL pra compartilhar.
        var view = TestInfra.SemComentarios(Details());

        var painel = view.IndexOf("id=\"palpiteiros\" role=\"tabpanel\"", StringComparison.Ordinal);
        Assert.True(painel >= 0, "Não achei o painel da aba Palpiteiros.");

        var link = view.IndexOf("asp-action=\"Palpiteiros\"", painel, StringComparison.Ordinal);
        Assert.True(link >= 0, "A página cheia ficou órfã: ninguém mais linka pra ela.");

        var fimDoPainel = view.IndexOf("role=\"tabpanel\"", painel + 30, StringComparison.Ordinal);
        Assert.True(fimDoPainel < 0 || link < fimDoPainel,
            "O link pra página cheia precisa estar DENTRO do painel da aba.");
    }

    [Theory]
    [InlineData("Cartaz pra divulgar", "Arte pronta pra postar")]
    [InlineData("Ver as fotos do torneio", "Publicadas pelo organizador")]
    public void A_explicacao_do_botao_vira_title_em_vez_de_linha_fixa(string rotulo, string explicacao)
    {
        // Legenda permanente ensina na PRIMEIRA visita e é ruído em todas as outras — a mesma
        // troca já feita hoje no "Sugestão ou bug" do rodapé.
        var topo = TestInfra.SemComentarios(AcimaDasAbas());

        var i = topo.IndexOf(rotulo, StringComparison.Ordinal);
        Assert.True(i >= 0, $"Não achei o botão \"{rotulo}\".");

        // ⚠️ O `title` tem que estar NA TAG do próprio botão, e não solto em qualquer lugar
        // acima: procurar a frase no topo inteiro passaria com ela de volta num <small>.
        //
        // ⚠️ E a tag é o `<a`/`<button>` que ENVOLVE o rótulo, não o `<` mais próximo — este
        // é o `</i>` do ícone, que vem logo antes do texto. A primeira versão deste teste
        // caiu nele e acusou `String: "</i"`.
        var abre = Math.Max(topo.LastIndexOf("<a ", i, StringComparison.Ordinal),
                            topo.LastIndexOf("<button ", i, StringComparison.Ordinal));
        Assert.True(abre >= 0, $"Não achei a tag que envolve \"{rotulo}\".");

        var tag = topo[abre..topo.IndexOf('>', abre)];
        Assert.Contains("title=", tag);
        Assert.Contains(explicacao, tag);
    }

    [Fact]
    public void Cartaz_e_Fotos_dividem_a_MESMA_linha()
    {
        // Duas linhas de um botão cada viram uma linha de dois. Empilhados, eram quatro linhas
        // com as legendas; agora é uma.
        var topo = TestInfra.SemComentarios(AcimaDasAbas());

        var cartaz = topo.IndexOf("Cartaz pra divulgar", StringComparison.Ordinal);
        var fotos = topo.IndexOf("Ver as fotos do torneio", StringComparison.Ordinal);
        Assert.True(cartaz >= 0 && fotos > cartaz, "Não achei os dois botões, nessa ordem.");

        // ⚠️ A REGRA ESTRUTURAL, e não a distância em caracteres: se nenhum container FECHA
        // entre os dois, eles estão no mesmo `d-flex` — e é o `</div>` que os separaria em
        // duas linhas. Distância em caracteres passaria com eles em linhas vizinhas.
        Assert.DoesNotContain("</div>", topo[cartaz..fotos]);
    }

    [Fact]
    public void E_nenhuma_legenda_fixa_sobra_na_linha_dos_botoes()
    {
        var topo = TestInfra.SemComentarios(AcimaDasAbas());
        var cartaz = topo.IndexOf("Cartaz pra divulgar", StringComparison.Ordinal);
        var fotos = topo.IndexOf("Ver as fotos do torneio", StringComparison.Ordinal);
        Assert.True(cartaz >= 0 && fotos > cartaz);

        Assert.DoesNotContain("<small", topo[cartaz..fotos]);
    }

    // Tudo que a pessoa rola ANTES de chegar na barra de abas — é dele que o Felipe reclamou.
    private static string AcimaDasAbas()
    {
        var view = Details();
        var abas = view.IndexOf("id=\"torneioTabs\"", StringComparison.Ordinal);
        Assert.True(abas >= 0, "Não achei a barra de abas.");
        return view[..abas];
    }

    private static string Details() =>
        File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "Views", "Torneios", "Details.cshtml"));

    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "wwwroot", "css", "site.css")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return dir!.FullName;
    }
}
