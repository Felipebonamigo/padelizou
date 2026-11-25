using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O TIE-BREAK DO SET DE 6 (22/11/2026, Wallau). 🗣️ *"na final não tá abrindo a opção de tie-break no
// set — tá 6x6 o set, daí não abre o tie pra contar"*. E o Felipe, aprovando a regra: *"pode fazer
// assim, só cuida que alguns jogos vão até 6, alguns até 9. E o tie-break alguns vão até 7 e outros
// até 9"*.
//
// Até aqui o tie-break só existia onde o limite NÃO estende (até 9 → 8x8). No limite PAR o
// (N-1)x(N-1) estende pra N+1 — o "vencer por dois" de games, que continua igual —, e o NxN agora
// abre o tie-break, que fecha em (N+1)xN: o 6x6 → 7x6 do padel. O alvo dos pontos (7, 9, 10) é o
// que o organizador configurou, vence por 2 como sempre.
public class TieBreakNoEmpateDoSetTests
{
    private static FormatoDaPartida.Formato Ate(int games, int pontos) =>
        new(1, games, ContagemDeGamesDoTorneio.Ate, pontos);

    [Theory]
    [InlineData(6, 7)]
    [InlineData(6, 9)]
    [InlineData(4, 7)]
    public void No_jogo_par_o_empate_NxN_abre_o_tie_break(int games, int pontos)
    {
        Assert.True(TieBreakDoJogo.PodeAcontecer(Ate(games, pontos)));
        Assert.True(TieBreakDoJogo.EmAndamento(Ate(games, pontos), games, games));
        // O (N-1)x(N-1) continua indo pro game a mais — o "vencer por dois" não muda.
        Assert.False(TieBreakDoJogo.EmAndamento(Ate(games, pontos), games - 1, games - 1));
    }

    [Fact]
    public void Quem_fecha_o_tie_break_do_6x6_leva_o_set_por_7x6()
    {
        Assert.Equal((7, 6), TieBreakDoJogo.GamesAoFechar(Ate(6, 7), 7, 5));
        Assert.Equal((6, 7), TieBreakDoJogo.GamesAoFechar(Ate(6, 7), 4, 7));
        Assert.Equal((7, 6), TieBreakDoJogo.GamesAoFechar(Ate(6, 9), 9, 7));
        Assert.Null(TieBreakDoJogo.GamesAoFechar(Ate(6, 9), 7, 5)); // até 9 não fecha no 7
        Assert.True(FormatoDaPartida.PodeEncerrar(6, 7, 6, tieBreakNaPenultima: false));
    }

    [Fact]
    public void O_jogo_ate_9_continua_como_era()
    {
        Assert.True(TieBreakDoJogo.EmAndamento(Ate(9, 7), 8, 8));
        Assert.False(TieBreakDoJogo.EmAndamento(Ate(9, 7), 9, 9));
        Assert.Equal((9, 8), TieBreakDoJogo.GamesAoFechar(Ate(9, 7), 7, 5));
    }

    [Fact]
    public void Sem_tie_break_configurado_o_6x6_segue_no_game_a_mais()
    {
        Assert.False(TieBreakDoJogo.EmAndamento(Ate(6, 0), 6, 6));
    }
}
