using Padelizou.Services;

namespace Padelizou.Tests;

// A chave do torneio restrito passou a poder ser ESCOLHIDA (pedido do Felipe, 30/07/2026):
// quem organiza quer uma chave que se diga por telefone e se lembre no grupo do WhatsApp.
// Sorteada continua sendo o padrão de quem não escolhe nada.
public class ChaveDeAcessoDoTorneioTests
{
    [Fact]
    public void Chave_escolhida_e_a_que_vale()
    {
        Assert.Equal("virgili10", ChaveDeAcessoDoTorneio.Definir("virgili10"));
    }

    [Fact]
    public void Sem_escolha_a_chave_e_sorteada_com_6_caracteres()
    {
        var chave = ChaveDeAcessoDoTorneio.Definir(null);

        Assert.Equal(6, chave.Length);
        Assert.NotEqual(chave, ChaveDeAcessoDoTorneio.Definir(""));   // não é uma constante
    }

    [Fact]
    public void A_sorteada_nao_usa_caracteres_que_se_confundem()
    {
        // 0/O e 1/I são indistinguíveis quando alguém lê a chave em voz alta ou copia de
        // um print — e aí a inscrição falha sem a pessoa entender por quê.
        for (var i = 0; i < 50; i++)
        {
            Assert.DoesNotContain(ChaveDeAcessoDoTorneio.Sortear(), c => c is '0' or 'O' or '1' or 'I');
        }
    }

    [Fact]
    public void Espaco_nas_pontas_sai_e_a_caixa_fica_como_foi_digitada()
    {
        // A caixa é preservada porque é assim que o organizador vai mostrar a chave; a
        // conferência na inscrição já compara ignorando maiúsculas.
        Assert.Equal("Virgili10", ChaveDeAcessoDoTorneio.Definir("  Virgili10 "));
    }

    [Theory]
    [InlineData("abc", "pelo menos")]
    [InlineData("chave com espaco", "espaços")]
    [InlineData("chavemuitolongademaisparaalguemrepetir", "no máximo")]
    public void Chave_ruim_e_recusada_com_o_motivo(string escolhida, string trechoEsperado)
    {
        var problema = ChaveDeAcessoDoTorneio.ProblemaCom(escolhida);

        Assert.NotNull(problema);
        Assert.Contains(trechoEsperado, problema);
    }

    [Fact]
    public void Vazio_nao_e_problema_e_so_cai_no_sorteio()
    {
        Assert.Null(ChaveDeAcessoDoTorneio.ProblemaCom(null));
        Assert.Null(ChaveDeAcessoDoTorneio.ProblemaCom("   "));
    }

    [Fact]
    public void Chave_recusada_nunca_vira_a_chave_do_torneio()
    {
        // Se Definir aceitasse o que ProblemaCom recusa, o organizador criaria o torneio
        // com uma chave que a tela disse não servir — e ninguém conseguiria se inscrever.
        var comEspaco = ChaveDeAcessoDoTorneio.Definir("chave com espaco");

        Assert.Equal(6, comEspaco.Length);
        Assert.DoesNotContain(' ', comEspaco);
    }
}
