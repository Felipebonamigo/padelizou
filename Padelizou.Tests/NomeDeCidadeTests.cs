using Padelizou.Services;

namespace Padelizou.Tests;

// "Gravatai", "Gravataí" e "gravatai" apareceram juntas no filtro do ranking, como se fossem três
// cidades. Cada uma veio de alguém digitando do seu jeito no cadastro.
public class NomeDeCidadeTests
{
    [Theory]
    [InlineData("Gravataí", "gravatai")]
    [InlineData("Gravatai", "GRAVATAÍ")]
    [InlineData("  Gravataí  ", "gravatai")]
    [InlineData("São Paulo", "sao paulo")]
    [InlineData("Santo Antônio", "SANTO ANTONIO")]
    public void Grafias_diferentes_da_mesma_cidade_sao_reconhecidas(string a, string b)
    {
        Assert.True(NomeDeCidade.Mesma(a, b), $"'{a}' e '{b}' deveriam ser a mesma cidade");
    }

    [Theory]
    [InlineData("Canoas", "Caxias")]
    [InlineData("Porto Alegre", "Porto Velho")]
    [InlineData("Gravataí", "Gravatá")]        // cidades diferentes de verdade (RS x PE)
    public void Cidades_diferentes_continuam_diferentes(string a, string b)
    {
        Assert.False(NomeDeCidade.Mesma(a, b), $"'{a}' e '{b}' NÃO são a mesma cidade");
    }

    [Fact]
    public void Vazio_nao_casa_com_nada_nem_com_outro_vazio()
    {
        // Senão dois cadastros sem cidade seriam "a mesma cidade", e o primeiro registro em
        // branco viraria uma cidade de nome vazio que todo mundo passaria a reusar.
        Assert.False(NomeDeCidade.Mesma("", ""));
        Assert.False(NomeDeCidade.Mesma(null, null));
        Assert.False(NomeDeCidade.Mesma("   ", "Canoas"));
    }

    [Theory]
    [InlineData("  Gravataí  ", "Gravataí")]
    [InlineData("Porto    Alegre", "Porto Alegre")]
    [InlineData("\tCanoas\n", "Canoas")]
    public void Espaco_sobrando_e_duplicado_sai_do_nome_guardado(string digitado, string esperado)
    {
        Assert.Equal(esperado, NomeDeCidade.Arrumar(digitado));
    }

    [Fact]
    public void O_acento_e_a_caixa_de_quem_digitou_certo_sao_preservados()
    {
        // A chave sem acento serve pra COMPARAR. O que vai pra tela é o que a pessoa escreveu:
        // "Gravataí" não pode virar "Gravatai" no banco.
        Assert.Equal("Gravataí", NomeDeCidade.Arrumar("  Gravataí "));
        Assert.Equal("São Paulo", NomeDeCidade.Arrumar("São Paulo"));
    }

    [Theory]
    [InlineData("rs", "RS")]
    [InlineData(" sp ", "SP")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Uf_e_padronizada_em_maiuscula(string? digitado, string? esperado)
    {
        Assert.Equal(esperado, NomeDeCidade.ArrumarEstado(digitado));
    }
}
