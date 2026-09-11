using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// 11/09/2026 — AS COLUNAS DE NÚMERO DA TABELA DO GRUPO ENCOLHEM.
//
// 🗣️ Felipe, com o print da Fase de Grupos do 2ª Etapa ER Padel Tour: *"alguns escudos estão
// com fundo branco, consegue arrumar?"* e *"acho que dá pra diminuir o tamanho do J V D SG da
// coluna, pra caber mais do nome"*.
//
// 🕳️ MEDIDO NOS 17 ESCUDOS DO TORNEIO, baixados do ar — não deduzido do CSS, que afirmava
// "escudo é PNG transparente" e errava em 14 dos 17 (nenhum é PNG):
//   • 10 têm FUNDO BRANCO ASSADO no arquivo (os 4 cantos opacos e claros). Dois são `.jpeg`,
//     formato que NÃO TEM canal alpha — fundo opaco é garantido ali.
//   •  4 têm fundo colorido.
//   •  3 são de fato transparentes.
//
// ⚠️ E POR ISSO "TIRAR O BRANCO" SERIA O CONSERTO ERRADO: 8 dos 17 têm DESENHO ESCURO (luminância
// média < 70) e sumiriam no navy da página. A chapinha em TODOS transforma acidente em decisão.
//
// ⚠️ MAS O ESCUDO NÃO É MAIS ASSUNTO DESTE ARQUIVO. Uma sessão PARALELA recebeu o mesmo pedido
// no mesmo dia ("pq tem algumas bandeirinhas sem fundo igual as demais"), mediu os mesmos 17
// escudos, chegou aos mesmos números e MESCLOU PRIMEIRO. A moldura que está no ar é a dela
// (quadrada, 18px, com borda) — a minha (altura fixa, largura livre, abraçando o escudo) foi
// descartada no merge. Testes meus travando o MEU desenho fariam este arquivo brigar com o que
// já está publicado, então saíram. Fica aqui só o que era de fato meu: as colunas.
public class ColunasDeNumeroDaTabelaDoGrupoTests
{




    [Theory]
    [InlineData("Jogos")]
    [InlineData("Vitórias")]
    [InlineData("Derrotas")]
    [InlineData("Saldo de Games")]
    public void As_quatro_colunas_de_numero_sao_estreitas_por_CLASSE(string titulo)
    {
        // Elas dividiam metade da tabela em quatro (~12,5% cada) pra mostrar um dígito. O que
        // sobrava pro nome era 180px, e "Marcelo Konf…" truncava.
        var th = TagDoTh(titulo);

        Assert.Contains("pdz-col-num", th);
    }

    [Fact]
    public void E_a_largura_delas_mora_no_CSS_com_a_do_nome_liberada()
    {
        var regra = Bloco(Css(), ".pdz-col-num");
        Assert.Matches(new Regex(@"width:\s*\d+px"), regra);

        // A coluna do nome não pode continuar presa nos 180px: estreitar as outras quatro sem
        // soltar esta devolveria o espaço pra margem, não pro nome.
        var view = Details();
        var celula = view.IndexOf("pdz-chip-compacto", StringComparison.Ordinal);
        Assert.True(celula >= 0, "Não achei a célula da dupla na tabela do grupo.");
        var tag = view[view.LastIndexOf('<', celula)..view.IndexOf('>', celula)];
        Assert.DoesNotContain("max-width: 180px", tag);
    }

    [Fact]
    public void A_tabela_e_de_layout_FIXO_senao_o_nome_empurra_a_tabela_pra_fora()
    {
        // 🕳️ DEFEITO MEU, PEGO NO RENDER E NÃO NO TESTE (11/09/2026). Soltar o `max-width: 180px`
        // da célula do nome sem fixar o layout faz o OPOSTO do pedido: com largura automática a
        // coluna CRESCE pra caber "Marcelo Konfidera" inteiro, a tabela passa do cartão e o
        // `.table-responsive` vira ROLAGEM HORIZONTAL — pior que o nome truncado.
        //
        // Medido no Chromium com o CSS real, janela de 500px: antes, Dupla=212px e as quatro de
        // número somando 211px; depois, Dupla=305px e as quatro somando 120px, sem rolagem.
        var regra = Bloco(Css(), ".pdz-tabela-grupo");

        Assert.Matches(new Regex(@"table-layout:\s*fixed"), regra);
    }

    // A tag do <th> que tem este title.
    private static string TagDoTh(string titulo)
    {
        var view = Details();
        var i = view.IndexOf($"<th title=\"{titulo}\"", StringComparison.Ordinal);
        Assert.True(i >= 0, $"Não achei o <th> de \"{titulo}\" na tabela do grupo.");
        return view[i..view.IndexOf('>', i)];
    }

    // O corpo da primeira regra cujo seletor é EXATAMENTE este.
    private static string Bloco(string css, string seletor)
    {
        var m = Regex.Match(css, Regex.Escape(seletor) + @"\s*\{([^}]*)\}");
        Assert.True(m.Success, $"Não achei a regra `{seletor}` no site.css.");
        return m.Groups[1].Value;
    }

    private static string Css() =>
        File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "wwwroot", "css", "site.css"));

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
