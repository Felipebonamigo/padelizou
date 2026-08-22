using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A categoria LENDAS: veterania, sem nível — quem entra é pela idade/tempo de padel, não pela
// força. Uma só, sem A/B/C e sem separar por sexo.
//
// Ela é "fora da escada" pelo mesmo motivo de Casal/Mista (não gradua, não trava nível, troféu
// de vidro) — mas, ao contrário das duas, NÃO exige um homem e uma mulher: dois veteranos do
// mesmo sexo jogam juntos numa boa. É o ponto onde "fora da escada" e "exige um de cada"
// deixam de ser a mesma pergunta, e é exatamente o tipo de acoplamento que passa despercebido.
public class CategoriaLendasTests
{
    private static Jogador Com(string? sexo, string nome = "Fulano de Tal") =>
        new() { Nome = nome, Cpf = "11144477735", Sexo = sexo };

    [Theory]
    [InlineData("Categoria Lendas")]
    [InlineData("Lendas")]
    [InlineData("Torneio das Lendas")]
    [InlineData("LENDAS")]
    public void Reconhece_a_categoria_pelo_nome(string nome)
    {
        Assert.True(FaixasDePadelimetro.EhLendas(nome));
        Assert.True(FaixasDePadelimetro.ForaDaEscada(nome));
    }

    [Theory]
    [InlineData("4ª Categoria Masculina")]
    [InlineData("Categoria Open Feminina")]
    [InlineData("Categoria Mista A")]
    [InlineData("Categoria Casais")]
    public void Nao_confunde_com_outra_categoria(string nome)
    {
        Assert.False(FaixasDePadelimetro.EhLendas(nome));
    }

    // Sem faixa não há trava de nível: lendas de 2ª com 7ª joga junto, que é o ponto da
    // categoria.
    [Fact]
    public void Nao_tem_faixa_como_a_mista_e_o_casal()
    {
        Assert.Null(FaixasDePadelimetro.DaCategoria("Categoria Lendas"));
    }

    // Quem estreia por Lendas nasce entre as duas réguas, como Mista/Casal sem letra — e não
    // no meio neutro (500), que é onde cai um nome fora de qualquer convenção.
    [Fact]
    public void Quem_estreia_nasce_no_meio_do_caminho_entre_as_duas_reguas()
    {
        Assert.Equal(FaixasDePadelimetro.Entrada("Categoria Mista"), FaixasDePadelimetro.Entrada("Categoria Lendas"));
        Assert.NotEqual(FaixasDePadelimetro.EntradaNeutra, FaixasDePadelimetro.Entrada("Categoria Lendas"));
    }

    // Mesmo troféu de vidro da mista e do casal, e pelo mesmo motivo: é OUTRO corte de gente,
    // não um degrau da escada de força.
    [Fact]
    public void Campeao_leva_o_trofeu_de_vidro()
    {
        Assert.Equal(TrofeuDeMaterial.Do("Categoria Mista A"), TrofeuDeMaterial.Do("Categoria Lendas"));
    }

    // Não trava nível: OrdemCategoria 0 é o que diz "esta categoria não define tier".
    [Fact]
    public void Nao_trava_nivel_de_ninguem()
    {
        Assert.Equal(0, EstatisticasService.OrdemCategoria("Categoria Lendas"));
    }

    // ⚠️ O ponto central desta categoria: diferente de Mista/Casal, ela NÃO é par misto.
    [Fact]
    public void Ao_contrario_de_mista_e_casal_nao_exige_um_homem_e_uma_mulher()
    {
        Assert.False(SexoDoJogador.ExigeUmDeCada("Categoria Lendas"));
    }

    [Fact]
    public void Dois_homens_ou_duas_mulheres_entram_juntos()
    {
        var homem1 = Com(SexoDoJogador.Masculino, "Joao");
        var homem2 = Com(SexoDoJogador.Masculino, "Pedro");
        var mulher1 = Com(SexoDoJogador.Feminino, "Maria");
        var mulher2 = Com(SexoDoJogador.Feminino, "Ana");

        Assert.Null(SexoDoJogador.MotivoParaNaoEntrar("Categoria Lendas", homem1, homem2));
        Assert.Null(SexoDoJogador.MotivoParaNaoEntrar("Categoria Lendas", mulher1, mulher2));
        // E um de cada também continua valendo, sem restrição nenhuma.
        Assert.Null(SexoDoJogador.MotivoParaNaoEntrar("Categoria Lendas", homem1, mulher1));
    }

    // Quem nem informou o sexo entra numa boa: a categoria não cobra o dado, então
    // MotivoParaNaoEntrar não tem por que barrar por falta dele.
    [Fact]
    public void Quem_nao_informou_o_sexo_tambem_entra()
    {
        Assert.Null(SexoDoJogador.MotivoParaNaoEntrar("Categoria Lendas", Com(null), Com(null, "Outro")));
    }
}
