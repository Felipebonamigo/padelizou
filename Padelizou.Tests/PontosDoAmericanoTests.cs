using Padelizou.Services;

namespace Padelizou.Tests;

// O ranking do Americano é COLOCAÇÃO COM PESO (decisão do Felipe, 07/08/2026): ganhar um
// Americano de 16 vale mais que ganhar um de 4.
//
// A alternativa — somar games — foi recusada porque premiaria volume: um Americano de 20
// rodadas renderia mais que dois de 7, e o ranking mediria tempo livre em vez de padel.
public class PontosDoAmericanoTests
{
    [Fact]
    public void No_tamanho_de_referencia_a_tabela_sai_inteira()
    {
        // 8 pessoas = peso 1, então aqui os pontos são a tabela crua. É o caso que a pessoa
        // vai usar pra entender o resto.
        Assert.Equal(100, PontosDoAmericano.Pontos(colocacao: 1, pessoas: 8));
        Assert.Equal(60, PontosDoAmericano.Pontos(2, 8));
        Assert.Equal(40, PontosDoAmericano.Pontos(3, 8));
        Assert.Equal(25, PontosDoAmericano.Pontos(4, 8));
        Assert.Equal(10, PontosDoAmericano.Pontos(5, 8));
        Assert.Equal(10, PontosDoAmericano.Pontos(8, 8));
    }

    [Theory]
    [InlineData(4, 50)]    // metade da gente, metade do ponto
    [InlineData(8, 100)]
    [InlineData(12, 150)]
    [InlineData(16, 200)]  // o teto do Americano livre: o dobro
    public void Ganhar_vale_mais_quanto_mais_gente_tem_em_quadra(int pessoas, int esperado)
    {
        Assert.Equal(esperado, PontosDoAmericano.Pontos(colocacao: 1, pessoas: pessoas));
    }

    [Fact]
    public void Vencer_um_americano_pequeno_vale_menos_que_ser_vice_num_grande()
    {
        // É a razão de existir o peso: sem ele, juntar 3 amigos toda semana renderia o mesmo
        // que enfrentar 15 pessoas.
        var campeaoDe4 = PontosDoAmericano.Pontos(colocacao: 1, pessoas: 4);   // 50
        var viceDe16 = PontosDoAmericano.Pontos(colocacao: 2, pessoas: 16);    // 120

        Assert.True(viceDe16 > campeaoDe4);
    }

    [Fact]
    public void A_ordem_das_colocacoes_nunca_se_inverte_dentro_do_mesmo_americano()
    {
        // Vale pra todo tamanho que o Americano fecha: quem terminou na frente não pode
        // receber menos que quem terminou atrás. É a propriedade que o peso poderia quebrar
        // se um dia ele deixasse de ser um multiplicador.
        foreach (var pessoas in new[] { 4, 5, 8, 9, 12, 13, 16 })
        {
            for (int colocacao = 1; colocacao < pessoas; colocacao++)
            {
                Assert.True(
                    PontosDoAmericano.Pontos(colocacao, pessoas) >=
                    PontosDoAmericano.Pontos(colocacao + 1, pessoas),
                    $"{colocacao}º recebeu menos que {colocacao + 1}º num Americano de {pessoas}");
            }
        }
    }

    [Fact]
    public void Meio_ponto_arredonda_pra_cima_sempre()
    {
        // 5 pessoas, 4º lugar: 25 × 0,625 = 15,625 → 16. E o caso que pega o padrão do .NET:
        // com ToEven (o default de Math.Round) dois jogadores com a mesma conta receberiam
        // pontos diferentes conforme a paridade, e isso não se explica pra ninguém.
        Assert.Equal(16, PontosDoAmericano.Pontos(colocacao: 4, pessoas: 5));

        // 25 × (4/8) = 12,5 → 13, e não 12.
        Assert.Equal(13, PontosDoAmericano.Pontos(colocacao: 4, pessoas: 4));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Numero_impossivel_vale_zero_em_vez_de_estourar(int pessoas)
    {
        Assert.Equal(0, PontosDoAmericano.Pontos(colocacao: 1, pessoas: pessoas));
        Assert.Equal(0m, PontosDoAmericano.CustoParaPontuar(pessoas));
    }

    [Fact]
    public void Colocacao_invalida_tambem_vale_zero()
    {
        Assert.Equal(0, PontosDoAmericano.Pontos(colocacao: 0, pessoas: 8));
        Assert.Equal(0, PontosDoAmericano.Pontos(colocacao: -1, pessoas: 8));
    }

    [Theory]
    [InlineData(8, 40)]
    [InlineData(12, 60)]
    [InlineData(16, 80)]
    public void O_custo_de_valer_ponto_e_por_pessoa_inscrita(int pessoas, decimal esperado)
    {
        Assert.Equal(esperado, PontosDoAmericano.CustoParaPontuar(pessoas));
    }
}
