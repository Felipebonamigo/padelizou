using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// MISTA e CASAIS são de um homem e uma mulher (Felipe, 08/08/2026).
//
// Até aqui isso era honra pura: o sistema não guardava o sexo de ninguém, então não conferia
// nem avisava. O campo `Jogador.Sexo` nasceu por causa desta regra — e nada além dela o lê.
public class SexoNaMistaECasaisTests
{
    private static Jogador Com(string? sexo, string nome = "Fulano de Tal") =>
        new() { Nome = nome, Cpf = "11144477735", Sexo = sexo };

    private static Jogador Homem(string nome = "Joao da Silva") => Com(SexoDoJogador.Masculino, nome);
    private static Jogador Mulher(string nome = "Maria Souza") => Com(SexoDoJogador.Feminino, nome);

    [Theory]
    [InlineData("Categoria Mista A")]
    [InlineData("Categoria Mista D")]
    [InlineData("Categoria Casais")]
    // O nome antigo continua valendo: existe torneio no banco com a categoria criada antes
    // do renome, e ela não deixa de ser de casal por causa disso.
    [InlineData("Categoria Casal")]
    public void Essas_categorias_exigem_um_de_cada(string nome)
    {
        Assert.True(SexoDoJogador.ExigeUmDeCada(nome));
    }

    [Theory]
    [InlineData("4ª Categoria Masculina")]
    [InlineData("5ª Categoria Feminina")]
    [InlineData("Categoria Open Masculino")]
    public void As_outras_nao_exigem_nada(string nome)
    {
        Assert.True(SexoDoJogador.ExigeUmDeCada(nome) == false);
        Assert.Null(SexoDoJogador.MotivoParaNaoEntrar(nome, Homem(), Homem("Pedro Alves")));
    }

    [Fact]
    public void Um_homem_e_uma_mulher_entram()
    {
        Assert.Null(SexoDoJogador.MotivoParaNaoEntrar("Categoria Casais", Homem(), Mulher()));
        Assert.Null(SexoDoJogador.MotivoParaNaoEntrar("Categoria Mista A", Mulher(), Homem()));
    }

    [Fact]
    public void Dois_do_mesmo_sexo_nao_entram()
    {
        var recusa = SexoDoJogador.MotivoParaNaoEntrar("Categoria Casais", Homem(), Homem("Pedro Alves"));

        Assert.NotNull(recusa);
        Assert.Contains("um homem e uma mulher", recusa);
    }

    [Fact]
    public void Quem_nao_informou_e_barrado_com_o_NOME_dele_na_frase()
    {
        // ⚠️ Barrar antes de comparar não é detalhe: sem isso a recusa sairia como "vocês são
        // do mesmo sexo" pra uma dupla que talvez esteja certa, e ninguém saberia o que fazer.
        var recusa = SexoDoJogador.MotivoParaNaoEntrar(
            "Categoria Mista B", Homem(), Com(null, "Carla Menezes"));

        Assert.NotNull(recusa);
        Assert.Contains("Carla", recusa);
        Assert.Contains("não informou", recusa);
        Assert.DoesNotContain("vocês dois", recusa);
    }

    [Fact]
    public void Dupla_aberta_so_cobra_de_quem_esta_entrando()
    {
        // "Procurando parceiro": não dá pra conferir um par que ainda não existe. Quem entra
        // precisa ter informado; o par é conferido quando o parceiro for definido.
        Assert.Null(SexoDoJogador.MotivoParaNaoEntrar("Categoria Casais", Homem(), null));
        Assert.NotNull(SexoDoJogador.MotivoParaNaoEntrar("Categoria Casais", Com(null), null));
    }

    [Theory]
    [InlineData("masculino", "Masculino")]
    [InlineData("FEMININO", "Feminino")]
    [InlineData("  Feminino  ", "Feminino")]
    public void A_entrada_e_normalizada(string digitado, string esperado)
    {
        Assert.Equal(esperado, SexoDoJogador.Normalizar(digitado));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("outro")]
    [InlineData("M")]
    public void O_que_nao_e_reconhecido_vira_NULO_e_nao_um_terceiro_valor(string? digitado)
    {
        // Valor estranho tem que virar "não informou" — nunca uma terceira categoria entrando
        // calada no banco, que nenhuma tela sabe mostrar e nenhuma regra sabe comparar.
        Assert.Null(SexoDoJogador.Normalizar(digitado));
        Assert.False(SexoDoJogador.Informou(Com(digitado)));
    }
}
