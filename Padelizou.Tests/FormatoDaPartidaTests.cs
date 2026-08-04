using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O organizador escolhia sets e games por fase no cadastro e NADA lia isso: a Mesa de
// Controle tinha `limiteGames: 9` cravado no JavaScript. Ele configurava "4 games", a Mesa
// deixava marcar até 9, e ninguém era avisado da diferença.
public class FormatoDaPartidaTests
{
    private static Torneio TorneioDoVirgili() => new()
    {
        Nome = "Interno",
        SetsFaseGrupos = 1,
        GamesFaseGrupos = 4,      // "todos os jogos vão até 4"
        SetsFaseMataMata = 1,
        GamesFaseMataMata = 4,
        SetsFaseFinal = 1,
        GamesFaseFinal = 6,       // "...e as semis e finais até 6"
    };

    [Theory]
    [InlineData("Grupo A", 4)]
    [InlineData("Fase de Grupos", 4)]
    [InlineData("Primeira Rodada", 4)]
    [InlineData("Oitavas de Final", 4)]
    [InlineData("Quartas de Final", 4)]
    [InlineData("Semifinal", 6)]
    [InlineData("Final", 6)]
    public void Cada_fase_pega_o_limite_que_o_organizador_configurou(string fase, int games)
    {
        Assert.Equal(games, FormatoDaPartida.De(TorneioDoVirgili(), fase).Games);
    }

    [Fact]
    public void Semifinal_acompanha_a_final_e_nao_o_mata_mata()
    {
        // A decisão de produto: quem escreve "as semis e a final são mais longas" está
        // falando das duas. Ninguém configura semifinal com regra de oitavas.
        var torneio = TorneioDoVirgili();

        Assert.Equal(FormatoDaPartida.De(torneio, "Final").Games,
                     FormatoDaPartida.De(torneio, "Semifinal").Games);
        Assert.NotEqual(FormatoDaPartida.De(torneio, "Quartas de Final").Games,
                        FormatoDaPartida.De(torneio, "Semifinal").Games);
    }

    [Fact]
    public void Americano_joga_com_a_regra_geral()
    {
        Assert.Equal(4, FormatoDaPartida.De(TorneioDoVirgili(), "Americano - Rodada 1").Games);
    }

    [Fact]
    public void Torneio_antigo_com_zero_gravado_cai_no_padrao()
    {
        // Zero viraria uma Mesa onde não dá pra marcar nem um game — trava a quadra.
        var zerado = new Torneio { Nome = "Antigo" };

        Assert.Equal(FormatoDaPartida.GamesPadrao, FormatoDaPartida.De(zerado, "Grupo A").Games);
        Assert.Equal(FormatoDaPartida.SetsPadrao, FormatoDaPartida.De(zerado, "Final").Sets);
    }

    [Fact]
    public void Sem_torneio_nao_estoura()
    {
        Assert.Equal(FormatoDaPartida.GamesPadrao, FormatoDaPartida.De(null, "Final").Games);
    }
}
