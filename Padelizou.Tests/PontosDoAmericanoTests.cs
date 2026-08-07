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
    [InlineData(8, 100)]   // a referência
    [InlineData(9, 113)]
    [InlineData(12, 150)]
    [InlineData(16, 200)]  // o teto do Americano livre: o dobro
    public void Ganhar_vale_mais_quanto_mais_gente_tem_em_quadra(int pessoas, int esperado)
    {
        Assert.Equal(esperado, PontosDoAmericano.Pontos(colocacao: 1, pessoas: pessoas));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    public void Abaixo_de_8_pessoas_nao_vale_ponto_nenhum(int pessoas)
    {
        // 4 ou 5 pessoas é o tamanho em que quatro conhecidos fabricam resultado sem esforço,
        // e sem precisar combinar nada — basta jogar mole. O peso sozinho não resolvia: só
        // fazia o ponto ser menor, e ponto menor toda semana continua sendo ponto de graça.
        Assert.Equal(0, PontosDoAmericano.Pontos(colocacao: 1, pessoas: pessoas));
        Assert.Equal(0, PontosDoAmericano.Pontos(colocacao: 2, pessoas: pessoas));
        Assert.False(PontosDoAmericano.PontuaNesteTamanho(pessoas));
    }

    [Fact]
    public void O_piso_e_exatamente_8_e_nao_7()
    {
        Assert.False(PontosDoAmericano.PontuaNesteTamanho(7));
        Assert.True(PontosDoAmericano.PontuaNesteTamanho(8));
        Assert.Equal(100, PontosDoAmericano.Pontos(colocacao: 1, pessoas: 8));
    }

    [Fact]
    public void Vencer_um_americano_pequeno_vale_menos_que_ser_vice_num_grande()
    {
        // É a razão de existir o peso: sem ele, um rodízio de 8 renderia o mesmo que
        // enfrentar 15 pessoas.
        var campeaoDe8 = PontosDoAmericano.Pontos(colocacao: 1, pessoas: 8);   // 100
        var viceDe16 = PontosDoAmericano.Pontos(colocacao: 2, pessoas: 16);    // 120

        Assert.True(viceDe16 > campeaoDe8);
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
        // 10 pessoas (2 grupos de 5), do 5º lugar pra trás: 10 × 1,25 = 12,5.
        //
        // É o caso que pega o padrão do .NET: com ToEven (o default de Math.Round) isso viraria
        // 12, e 37,5 viraria 38 — dois jogadores com a mesma conta receberiam pontos diferentes
        // conforme a paridade, e isso não se explica pra ninguém.
        Assert.Equal(13, PontosDoAmericano.Pontos(colocacao: 5, pessoas: 10));

        // 25 × (9/8) = 28,125 → 28: pra baixo quando é pra baixo mesmo.
        Assert.Equal(28, PontosDoAmericano.Pontos(colocacao: 4, pessoas: 9));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Numero_impossivel_vale_zero_em_vez_de_estourar(int pessoas)
    {
        Assert.Equal(0, PontosDoAmericano.Pontos(colocacao: 1, pessoas: pessoas));
        Assert.Equal(0m, PontosDoAmericano.CustoParaPontuar(pessoas));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(7)]
    public void Quem_nao_pontua_tambem_nao_paga(int pessoas)
    {
        // Um Americano que fechou com 6 pessoas fica fora do ranking pelo piso. Cobrar por um
        // ponto que não existe seria vender o que não foi entregue — e o organizador não tem
        // como garantir quantos aparecem no sábado.
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
