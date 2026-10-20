using Padelizou.Services;

namespace Padelizou.Tests;

// "Precisamos colocar legenda nas categorias mistas" (Felipe, 20/10/2026): quem se inscreve
// numa "Mista B" não sabe que ela é 4ª Masc + 3ª Fem — o nome diz a letra, não os níveis.
//
// SÓ EXIBIÇÃO, como CategoriaNaTela.Curto: o nome gravado não muda, porque é por ele que as
// regras reconhecem Mista (SexoDoJogador, FaixasDePadelimetro, CategoriaDoRankingRs).
public class LegendaDasCategoriasMistasTests
{
    [Theory]
    [InlineData("Categoria Mista A", "3ª Masc e 2ª Fem")]
    [InlineData("Categoria Mista B", "4ª Masc e 3ª Fem")]
    [InlineData("Categoria Mista C", "5ª Masc e 4ª Fem")]
    [InlineData("Categoria Mista D", "6ª Masc e 5ª Fem")]
    [InlineData("Categoria Mista E", "7ª Masc, 7ª Fem e 6ª Fem")]
    public void Cada_mista_conhecida_tem_a_legenda_dos_niveis(string nome, string esperada)
        => Assert.Equal(esperada, CategoriaNaTela.Legenda(nome));

    [Theory]
    [InlineData("Mista B")]
    [InlineData("  categoria mista b  ")]
    [InlineData("MISTA B")]
    public void A_legenda_acha_a_mista_pelo_nome_curto_ou_com_caixa_diferente(string nome)
        => Assert.Equal("4ª Masc e 3ª Fem", CategoriaNaTela.Legenda(nome));

    [Theory]
    [InlineData("Categoria Mista F")]
    [InlineData("4ª Categoria Masculina")]
    [InlineData("Categoria Casais")]
    [InlineData("")]
    [InlineData(null)]
    public void Categoria_sem_legenda_definida_devolve_nulo(string? nome)
        => Assert.Null(CategoriaNaTela.Legenda(nome));

    [Fact]
    public void ComLegenda_junta_o_nome_curto_e_a_legenda()
        => Assert.Equal("Mista B: 4ª Masc e 3ª Fem", CategoriaNaTela.ComLegenda("Categoria Mista B"));

    [Fact]
    public void ComLegenda_sem_legenda_e_igual_ao_nome_curto()
    {
        Assert.Equal("4ª Masculina", CategoriaNaTela.ComLegenda("4ª Categoria Masculina"));
        Assert.Equal("Mista F", CategoriaNaTela.ComLegenda("Categoria Mista F"));
    }

    [Fact]
    public void A_legenda_nao_mexe_na_ordem_nem_no_nome_curto()
    {
        // Curto segue sendo o nome sem legenda: é ele que vai pro card, pro WhatsApp e pra chave.
        Assert.Equal("Mista B", CategoriaNaTela.Curto("Categoria Mista B"));
    }
}
