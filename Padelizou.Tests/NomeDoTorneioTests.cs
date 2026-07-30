using Padelizou.Services;

namespace Padelizou.Tests;

// Dois torneios com o mesmo nome, do mesmo organizador, ao mesmo tempo. Aconteceu no primeiro
// dia com gente real (30/07/2026): "Amigos do Eder" criado duas vezes, provavelmente pelo botão
// apertado de novo. Os dois ficaram de pé, dividindo as inscrições.
public class NomeDoTorneioTests
{
    private const string Aberto = "Inscrições Abertas";

    [Fact]
    public void Mesmo_nome_do_mesmo_organizador_e_barrado()
    {
        var meus = new[] { ("Amigos do Eder", (string?)Aberto) };

        Assert.Equal("Amigos do Eder", NomeDoTorneio.JaExisteEntre(meus, "Amigos do Eder"));
    }

    [Theory]
    [InlineData("amigos do eder")]        // sem maiúscula
    [InlineData("AMIGOS DO EDER")]
    [InlineData("  Amigos do Eder  ")]    // espaço nas pontas
    [InlineData("Amigos  do  Eder")]      // espaço duplicado no meio
    public void A_comparacao_e_como_uma_pessoa_compararia(string digitado)
    {
        var meus = new[] { ("Amigos do Eder", (string?)Aberto) };

        Assert.NotNull(NomeDoTorneio.JaExisteEntre(meus, digitado));
    }

    [Fact]
    public void Acento_nao_faz_de_dois_torneios_coisas_diferentes()
    {
        var meus = new[] { ("Copa Verão", (string?)Aberto) };

        Assert.NotNull(NomeDoTorneio.JaExisteEntre(meus, "Copa Verao"));
    }

    [Fact]
    public void Nome_diferente_passa()
    {
        var meus = new[] { ("Amigos do Eder", (string?)Aberto) };

        Assert.Null(NomeDoTorneio.JaExisteEntre(meus, "Amigos do Paulo"));
    }

    [Fact]
    public void Depois_de_finalizado_o_nome_libera()
    {
        // Quem organiza o "Amigos do Eder" todo mês precisa criar a edição seguinte. A trava
        // é contra DUPLICATA no ar, não contra repetir o nome pra sempre.
        var meus = new[] { ("Amigos do Eder", (string?)"Finalizado") };

        Assert.Null(NomeDoTorneio.JaExisteEntre(meus, "Amigos do Eder"));
    }

    [Theory]
    [InlineData("Inscrições Abertas")]
    [InlineData("Chaves em Sorteio")]
    [InlineData("Fase de Grupos")]
    [InlineData("Mata-Mata")]
    public void Torneio_em_qualquer_fase_nao_finalizada_ainda_impede(string status)
    {
        var meus = new[] { ("Amigos do Eder", (string?)status) };

        Assert.NotNull(NomeDoTorneio.JaExisteEntre(meus, "Amigos do Eder"));
    }

    [Fact]
    public void Lista_vazia_nao_impede_nada()
    {
        Assert.Null(NomeDoTorneio.JaExisteEntre(Array.Empty<(string, string?)>(), "Qualquer Nome"));
    }

    [Fact]
    public void Nome_vazio_nao_casa_com_nada()
    {
        // Nome em branco é problema de outra validação; aqui ele não pode casar por acidente
        // com um torneio existente e virar "você já tem um torneio chamado ...".
        var meus = new[] { ("Amigos do Eder", (string?)Aberto) };

        Assert.Null(NomeDoTorneio.JaExisteEntre(meus, ""));
        Assert.Null(NomeDoTorneio.JaExisteEntre(meus, "   "));
        Assert.Null(NomeDoTorneio.JaExisteEntre(meus, null));
    }
}
