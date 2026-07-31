using Padelizou.Models;
using Xunit;

namespace Padelizou.Tests;

// O pacote de aulas virou tabela própria (era um só, em três colunas do LocalAula) porque o
// professor quase sempre anuncia mais de uma oferta na mesma quadra: 4, 8, 12 aulas, cada uma
// com desconto maior. E o endereço do local virou opcional — muita aula é num clube que todo
// aluno já sabe onde fica.
public class PacoteDeAulasTests
{
    [Theory]
    [InlineData(4, 320, 80)]
    [InlineData(8, 600, 75)]
    [InlineData(2, 150, 75)]
    public void Preco_por_aula_e_o_que_o_aluno_compara(int aulas, decimal preco, decimal esperado)
    {
        var pacote = new PacoteDeAulas { QuantidadeAulas = aulas, Preco = preco };
        Assert.Equal(esperado, pacote.PrecoPorAula);
    }

    [Fact]
    public void Divisao_quebrada_arredonda_e_a_sobra_vai_pra_ultima_aula()
    {
        // 3 aulas por R$ 310 dá 103,333... O sistema cria as aulas com o valor arredondado e
        // joga a sobra na última (ver AulasController.Solicitar) pra a SOMA fechar exatamente
        // com o preço anunciado — senão o professor recebe um centavo a menos ou a mais do que
        // combinou, e a conta dele nunca bate.
        var pacote = new PacoteDeAulas { QuantidadeAulas = 3, Preco = 310m };

        Assert.Equal(103.33m, pacote.PrecoPorAula);

        var ultima = pacote.Preco - pacote.PrecoPorAula * (pacote.QuantidadeAulas - 1);
        Assert.Equal(103.34m, ultima);
        Assert.Equal(310m, pacote.PrecoPorAula * 2 + ultima);
    }

    [Fact]
    public void Pacote_sem_aula_nao_divide_por_zero()
    {
        // Não deveria existir (a tela exige 2+), mas dado velho ou POST à mão chegam assim, e
        // uma divisão por zero derrubaria a tela do professor inteira.
        var pacote = new PacoteDeAulas { QuantidadeAulas = 0, Preco = 200m };
        Assert.Equal(200m, pacote.PrecoPorAula);
    }

    [Theory]
    [InlineData("Chakra", "Rua Alexandrino de Alencar", "Chakra, Rua Alexandrino de Alencar")]
    [InlineData("Chakra", null, "Chakra")]
    [InlineData("Chakra", "", "Chakra")]
    [InlineData("Chakra", "   ", "Chakra")]
    public void Local_sem_endereco_nao_deixa_virgula_pendurada(string nome, string? endereco, string esperado)
    {
        // Isto vai pro convite do Google Agenda e pro e-mail de confirmação. Montar
        // "Nome, Endereco" na mão em cada lugar produzia "Chakra, " no dia em que alguém
        // deixasse o endereço em branco.
        var local = new LocalAula { Nome = nome, Endereco = endereco };
        Assert.Equal(esperado, local.NomeComEndereco);
    }
}
