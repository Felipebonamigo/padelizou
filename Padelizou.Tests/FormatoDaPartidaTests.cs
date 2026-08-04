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

    // ---- O limite não é teto seco: "vencer por dois" ----
    // Regra da quadra (Felipe, 05/08/2026): jogo até 4 que empata em 3x3 vai até 5; até 6
    // que empata em 5x5 vai até 7. Já o de 9 NÃO estende — 8x8 se resolve no tie-break, e o
    // 9º game é ele. Quem separa os casos é a paridade do limite.

    [Theory]
    [InlineData(4, 0, 0, 4)]   // começo de jogo: teto normal
    [InlineData(4, 3, 0, 4)]   // 3x0 não é empate — segue valendo 4
    [InlineData(4, 3, 3, 5)]   // empatou na penúltima: vai até 5
    [InlineData(4, 4, 3, 5)]   // e continua valendo depois do empate
    [InlineData(6, 5, 5, 7)]
    [InlineData(6, 5, 4, 6)]
    [InlineData(9, 8, 8, 9)]   // ímpar não estende: o 9º game É o tie-break
    [InlineData(1, 0, 0, 1)]
    public void O_teto_sobe_um_game_quando_empata_na_penultima(int limite, int g1, int g2, int teto)
    {
        Assert.Equal(teto, FormatoDaPartida.TetoDeGames(limite, g1, g2));
    }

    [Theory]
    [InlineData(4, 4, 0, true)]    // alguém chegou em 4 na frente: acabou
    [InlineData(4, 3, 3, false)]   // empate na penúltima: NINGUÉM venceu ainda
    [InlineData(4, 4, 3, true)]    // 4x3 já decide (o desempate foi jogado)
    [InlineData(4, 5, 4, true)]    // e o teto estendido também encerra
    [InlineData(4, 2, 1, false)]
    [InlineData(9, 9, 7, true)]
    [InlineData(9, 8, 8, false)]
    public void Encerrar_so_quando_alguem_venceu(int limite, int g1, int g2, bool pode)
    {
        Assert.Equal(pode, FormatoDaPartida.PodeEncerrar(limite, g1, g2));
    }
}
