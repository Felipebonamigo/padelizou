using Padelizou.Services;

namespace Padelizou.Tests;

// Quantos jogos cada tamanho de categoria gera. Existe pra sustentar a conta de preço do
// pacote "nós registramos os resultados": o custo é por JOGO, então saber o número de jogos
// ANTES de anunciar o preço é a diferença entre cobrar certo e cobrar no escuro.
public class CustoPorJogoTests
{
    // duplas -> jogos (grupos + mata-mata), pela mesma regra do GerarChaves.
    [Theory]
    [InlineData(4, 5)]
    [InlineData(8, 10)]
    [InlineData(12, 19)]
    [InlineData(16, 21)]
    [InlineData(24, 39)]
    [InlineData(32, 46)]
    public void Jogos_por_categoria(int duplas, int jogosEsperados)
    {
        Assert.Equal(jogosEsperados, PrevisaoDoTorneio.TotalDeJogos(duplas));
    }

    [Fact]
    public void O_custo_acompanha_o_numero_de_jogos_e_nao_os_dias()
    {
        // 4 categorias de 12 duplas: 4 × 19 = 76 jogos. A R$ 10 o jogo, R$ 760 de custo —
        // seja o torneio de um dia ou de três.
        var jogos = 4 * PrevisaoDoTorneio.TotalDeJogos(12);

        Assert.Equal(76, jogos);
        Assert.Equal(760m, jogos * 10m);
    }

    [Fact]
    public void Por_atleta_o_custo_fica_estavel_entre_tamanhos_diferentes()
    {
        // É o que torna o preço fácil de explicar: cobrando por jogo, o valor por atleta
        // inscrito cai quase sempre na mesma faixa, de categoria pequena a grande.
        foreach (var duplas in new[] { 8, 12, 16, 24, 32 })
        {
            var jogos = PrevisaoDoTorneio.TotalDeJogos(duplas);
            var atletas = duplas * 2;
            var porAtleta = jogos * 10m / atletas;   // custo nosso, por atleta

            Assert.InRange(porAtleta, 5.5m, 8.5m);
        }
    }
}
