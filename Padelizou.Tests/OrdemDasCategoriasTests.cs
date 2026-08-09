using Padelizou.Services;

namespace Padelizou.Tests;

// A ordem e o nome das categorias NA TELA (08/08/2026).
//
// Queixa do Felipe olhando o NATA PADEL TOUR no celular: a "4ª Categoria Feminina" aparecia
// no FIM da lista, depois da 7ª — porque a lista saía na ordem em que as categorias foram
// CRIADAS, e ela foi acrescentada depois. E cada opção ocupava duas linhas, então a lista não
// cabia na tela.
public class OrdemDasCategoriasTests
{
    private static List<string> Ordenar(params string[] nomes) =>
        nomes.OrderBy(CategoriaNaTela.Ordem).ToList();

    [Fact]
    public void A_categoria_criada_depois_entra_no_lugar_dela()
    {
        // O caso exato da tela: a 4ª Feminina foi criada por último e tem que aparecer ANTES
        // da 5ª Feminina, não no fim de tudo.
        var ordenadas = Ordenar(
            "4ª Categoria Masculina", "5ª Categoria Masculina", "6ª Categoria Masculina",
            "7ª Categoria Masculina", "5ª Categoria Feminina", "6ª Categoria Feminina",
            "7ª Categoria Feminina", "4ª Categoria Feminina");

        Assert.Equal(new[]
        {
            "4ª Categoria Masculina", "5ª Categoria Masculina", "6ª Categoria Masculina",
            "7ª Categoria Masculina",
            "4ª Categoria Feminina", "5ª Categoria Feminina", "6ª Categoria Feminina",
            "7ª Categoria Feminina",
        }, ordenadas);
    }

    [Fact]
    public void Open_encabeca_e_iniciantes_fecha_a_escada()
    {
        var ordenadas = Ordenar(
            "7ª Categoria Masculina", "Categoria Iniciantes Masculina",
            "Categoria Open Masculino", "2ª Categoria Masculina");

        Assert.Equal(new[]
        {
            "Categoria Open Masculino", "2ª Categoria Masculina", "7ª Categoria Masculina",
            "Categoria Iniciantes Masculina",
        }, ordenadas);
    }

    [Fact]
    public void Mista_e_Casais_vem_depois_das_de_nivel()
    {
        var ordenadas = Ordenar(
            "Categoria Casais", "Categoria Mista B", "Categoria Mista A", "5ª Categoria Feminina");

        Assert.Equal(new[]
        {
            "5ª Categoria Feminina", "Categoria Mista A", "Categoria Mista B", "Categoria Casais",
        }, ordenadas);
    }

    [Theory]
    [InlineData("4ª Categoria Masculina", "4ª Masculina")]
    [InlineData("Categoria Open Feminina", "Open Feminina")]
    [InlineData("Categoria Mista A", "Mista A")]
    [InlineData("Categoria Casais", "Casais")]
    public void O_nome_curto_tira_a_palavra_que_nao_informa_nada(string gravado, string naTela)
    {
        // "Categoria" dentro de um campo já chamado Categoria não diz nada — e era ela que
        // jogava cada opção pra segunda linha no celular.
        Assert.Equal(naTela, CategoriaNaTela.Curto(gravado));
    }

    [Fact]
    public void O_nome_GRAVADO_nao_muda()
    {
        // ⚠️ Encurtar é só exibição. As regras leem o nome inteiro: é por ele que Mista e
        // Casais são reconhecidas (SexoDoJogador) e que o de-para do Ranking RS casa.
        const string gravado = "Categoria Casais";

        Assert.True(SexoDoJogador.ExigeUmDeCada(gravado));
        // E o curto continua reconhecível, pra não quebrar se um dia alguém o usar numa regra.
        Assert.True(SexoDoJogador.ExigeUmDeCada(CategoriaNaTela.Curto(gravado)));
    }

    [Fact]
    public void Nome_estranho_nao_estoura_e_vai_pro_fim()
    {
        var ordenadas = Ordenar("Torneio dos Amigos", "4ª Categoria Masculina");

        Assert.Equal("4ª Categoria Masculina", ordenadas[0]);
        Assert.Equal("", CategoriaNaTela.Curto(null));
    }
}
