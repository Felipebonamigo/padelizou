using Padelizou.Services;

namespace Padelizou.Tests;

// A categoria CASAL: a mista sem nível — casais de verdade jogando juntos.
//
// Pro Padelímetro ela é irmã da mista, e pelo mesmo motivo: a dupla tem um de cada, então o
// resultado não diz em qual das duas réguas cada um corre. O que estes testes seguram é que
// ela seja tratada assim em TODOS os lugares que decidem régua — o reconhecimento é por
// trecho do nome, espalhado por três serviços, e é exatamente o tipo de regra que entra num
// e esquece do outro.
public class CategoriaCasalTests
{
    [Theory]
    [InlineData("Categoria Casal")]
    [InlineData("Casal")]
    [InlineData("Torneio de Casais")]
    [InlineData("CASAL")]
    public void Reconhece_a_categoria_pelo_nome(string nome)
    {
        Assert.True(FaixasDePadelimetro.EhCasal(nome));
        Assert.True(FaixasDePadelimetro.ForaDaEscada(nome));
    }

    [Theory]
    [InlineData("4ª Categoria Masculina")]
    [InlineData("Categoria Open Feminina")]
    [InlineData("Categoria Iniciantes Masculina")]
    public void Nao_confunde_com_categoria_de_escada(string nome)
    {
        Assert.False(FaixasDePadelimetro.EhCasal(nome));
        Assert.False(FaixasDePadelimetro.ForaDaEscada(nome));
    }

    // Sem faixa não há trava de nível nem teto de soma: casal de 2ª com 7ª joga junto, que é
    // o ponto da categoria.
    [Fact]
    public void Nao_tem_faixa_como_a_mista()
    {
        Assert.Null(FaixasDePadelimetro.DaCategoria("Categoria Casal"));
        Assert.Null(FaixasDePadelimetro.DaCategoria("Categoria Mista A"));
    }

    // Quem estreia por uma categoria de casal nasce entre as duas réguas, como na mista sem
    // letra — e não no meio neutro (500), que é onde cai um nome fora de qualquer convenção.
    [Fact]
    public void Quem_estreia_nasce_no_meio_do_caminho_entre_as_duas_reguas()
    {
        Assert.Equal(FaixasDePadelimetro.Entrada("Categoria Mista"), FaixasDePadelimetro.Entrada("Categoria Casal"));
        Assert.NotEqual(FaixasDePadelimetro.EntradaNeutra, FaixasDePadelimetro.Entrada("Categoria Casal"));
    }

    // Mesmo troféu da mista, e pelo mesmo motivo: é OUTRO jogo, não um degrau da escada — o
    // troféu não tem por que dizer "mais forte" nem "mais fraco".
    [Fact]
    public void Campeao_leva_o_trofeu_de_vidro_da_mista()
    {
        Assert.Equal(TrofeuDeMaterial.Do("Categoria Mista A"), TrofeuDeMaterial.Do("Categoria Casal"));
    }

    // Não trava nível: OrdemCategoria 0 é o que diz "esta categoria não define tier".
    [Fact]
    public void Nao_trava_nivel_de_ninguem()
    {
        Assert.Equal(0, EstatisticasService.OrdemCategoria("Categoria Casal"));
    }
}
