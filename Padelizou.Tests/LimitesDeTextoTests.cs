using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// Algumas colunas são `character varying(n)`, e o Postgres não corta o que passa: ele RECUSA
// (`value too long for type character varying(30)`). Sem checagem, um login de 31 letras no
// cadastro derrubava a página com erro 500 e a pessoa perdia tudo o que digitou — no primeiro
// contato dela com o site. Estes testes fixam os tamanhos que o banco realmente tem.
public class LimitesDeTextoTests
{
    [Fact]
    public void No_limite_exato_passa()
    {
        Assert.Null(LimitesDeTexto.Problema(new string('a', 100), LimitesDeTexto.NomeDeJogador, "O nome"));
        Assert.Null(LimitesDeTexto.Problema(new string('a', 30), LimitesDeTexto.LoginDeJogador, "O login"));
        Assert.Null(LimitesDeTexto.Problema(new string('a', 150), LimitesDeTexto.NomeDeTorneio, "O nome do torneio"));
    }

    [Fact]
    public void Um_caractere_a_mais_ja_recusa()
    {
        Assert.NotNull(LimitesDeTexto.Problema(new string('a', 101), LimitesDeTexto.NomeDeJogador, "O nome"));
        Assert.NotNull(LimitesDeTexto.Problema(new string('a', 31), LimitesDeTexto.LoginDeJogador, "O login"));
        Assert.NotNull(LimitesDeTexto.Problema(new string('a', 151), LimitesDeTexto.NomeDeTorneio, "O nome do torneio"));
    }

    [Fact]
    public void Vazio_e_nulo_nao_sao_problema_daqui()
    {
        // Quem exige preenchimento é o `required` da tela e a checagem de cada ação — esta
        // função responde só sobre tamanho.
        Assert.Null(LimitesDeTexto.Problema(null, 10, "O nome"));
        Assert.Null(LimitesDeTexto.Problema("", 10, "O nome"));
        Assert.Null(LimitesDeTexto.Problema("   ", 10, "O nome"));
    }

    [Fact]
    public void Espaco_das_pontas_nao_conta()
    {
        // O valor é gravado com Trim; recusar por causa de espaço colado seria recusar quem
        // digitou certo.
        Assert.Null(LimitesDeTexto.Problema("  " + new string('a', 30) + "  ", LimitesDeTexto.LoginDeJogador, "O login"));
    }

    [Fact]
    public void A_mensagem_diz_quanto_cabe_e_quanto_veio()
    {
        var msg = LimitesDeTexto.Problema(new string('a', 35), 30, "O login")!;

        // Sem os dois números a pessoa fica adivinhando quanto precisa cortar.
        Assert.Contains("35", msg);
        Assert.Contains("30", msg);
    }

    [Theory]
    [InlineData(4, null)]                                  // mínimo
    [InlineData(30, null)]                                 // máximo
    [InlineData(3, "pelo menos")]                          // curto demais
    [InlineData(31, "muito longo")]                        // comprido demais
    public void ValidarLogin_cobre_os_dois_extremos(int tamanho, string? trechoEsperado)
    {
        var resultado = IdentidadeJogador.ValidarLogin(new string('a', tamanho));

        if (trechoEsperado == null) Assert.Null(resultado);
        else Assert.Contains(trechoEsperado, resultado);
    }
}
