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

    // ── CONTAGEM POR SOMA (Felipe, 08/08/2026) ────────────────────────────────────────────
    // "Soma de 7" = jogam-se 7 games no total e vence quem fizer mais. É outro significado
    // pro MESMO número: no "até 7" o 7 é a meta de um lado; na soma é o tamanho do jogo.

    private static Torneio TorneioSoma(int games = 7) => new()
    {
        Nome = "Rodizio", Codigo = "R1",
        ContagemDeGames = ContagemDeGamesDoTorneio.Soma,
        SetsFaseGrupos = 1, GamesFaseGrupos = games,
        SetsFaseMataMata = 1, GamesFaseMataMata = games,
        SetsFaseFinal = 1, GamesFaseFinal = games,
    };

    [Fact]
    public void Torneio_novo_conta_no_ate_como_sempre_contou()
    {
        Assert.Equal(ContagemDeGamesDoTorneio.Ate, new Torneio { Nome = "x", Codigo = "x" }.ContagemDeGames);
        Assert.False(FormatoDaPartida.De(new Torneio { Nome = "x", Codigo = "x" }, "Grupo A").EhSoma);
    }

    [Fact]
    public void Contagem_desconhecida_cai_no_ate()
    {
        // Coluna antiga (a migração grava "Ate", mas string vazia existiu no gerado do EF) e
        // POST montado à mão não podem virar uma regra de jogo inventada.
        var torneio = TorneioSoma();
        torneio.ContagemDeGames = "QualquerCoisa";
        Assert.False(FormatoDaPartida.De(torneio, "Grupo A").EhSoma);

        torneio.ContagemDeGames = "";
        Assert.False(FormatoDaPartida.De(torneio, "Grupo A").EhSoma);
    }

    [Theory]
    [InlineData(0, 0, 7)]   // ninguém marcou: dá pra chegar aos 7
    [InlineData(0, 3, 4)]   // o outro tem 3, sobram 4
    [InlineData(4, 3, 4)]   // fechado: o teto é o que já tenho
    [InlineData(0, 7, 0)]   // o outro fez todos: não sobra nada
    public void Na_soma_o_teto_de_cada_lado_e_o_que_sobrou(int meus, int doOutro, int teto)
    {
        var formato = FormatoDaPartida.De(TorneioSoma(7), "Grupo A");
        Assert.Equal(teto, FormatoDaPartida.TetoDoLado(formato, meus, doOutro));
    }

    [Theory]
    [InlineData(4, 3, true)]    // 7 jogados: acabou
    [InlineData(7, 0, true)]    // 7x0 também soma 7
    [InlineData(5, 2, true)]
    [InlineData(6, 0, false)]   // faltou um game
    [InlineData(3, 3, false)]
    [InlineData(0, 0, false)]
    public void Na_soma_o_jogo_fecha_quando_os_games_acabam(int g1, int g2, bool pode)
    {
        var formato = FormatoDaPartida.De(TorneioSoma(7), "Grupo A");
        Assert.Equal(pode, FormatoDaPartida.PodeEncerrar(formato, g1, g2));
    }

    [Fact]
    public void Na_soma_seis_a_zero_ainda_tem_game_pra_jogar()
    {
        // O contraste que dá nome à opção: no "até 7", 6x0 ainda não venceu mas 7x0 sim; na
        // soma de 7, 6x0 tem um game pendente — dar o jogo por acabado ali seria encurtá-lo.
        var soma = FormatoDaPartida.De(TorneioSoma(7), "Grupo A");
        Assert.False(FormatoDaPartida.PodeEncerrar(soma, 6, 0));
        Assert.True(FormatoDaPartida.PodeEncerrar(soma, 6, 1));
    }

    [Theory]
    [InlineData(5, 5, 5, 2)]   // 5x5 na soma de 7 não existe: o segundo lado fica com o resto
    [InlineData(9, 9, 7, 0)]   // os dois estourando: o primeiro leva o total, não sobra nada
    [InlineData(3, 9, 3, 4)]
    [InlineData(4, 3, 4, 3)]   // placar válido passa intacto
    [InlineData(-2, 3, 0, 3)]  // negativo vira zero, e não encolhe nem infla o outro lado
    public void Placar_invalido_na_soma_e_ajustado_sem_estourar_o_total(
        int enviou1, int enviou2, int vira1, int vira2)
    {
        var formato = FormatoDaPartida.De(TorneioSoma(7), "Grupo A");
        var (g1, g2) = FormatoDaPartida.PlacarValido(formato, enviou1, enviou2);

        Assert.Equal(vira1, g1);
        Assert.Equal(vira2, g2);
        Assert.True(g1 + g2 <= 7);
    }

    [Fact]
    public void No_ate_o_placar_valido_mantem_o_vencer_por_dois()
    {
        // A regressão que importa: mexer na soma não pode encolher o desempate do "até".
        var ate = FormatoDaPartida.De(TorneioDoVirgili(), "Grupo A");   // até 4

        Assert.Equal((3, 3), FormatoDaPartida.PlacarValido(ate, 3, 3));
        Assert.Equal((5, 3), FormatoDaPartida.PlacarValido(ate, 9, 3));   // 3x3 estende o teto a 5
        Assert.Equal((4, 0), FormatoDaPartida.PlacarValido(ate, 9, 0));   // sem empate, teto seco
    }

    [Theory]
    [InlineData(ContagemDeGamesDoTorneio.Soma, 8, true)]    // 4x4 é alcançável
    [InlineData(ContagemDeGamesDoTorneio.Soma, 7, false)]   // ímpar sempre aponta vencedor
    [InlineData(ContagemDeGamesDoTorneio.Ate, 8, false)]    // no "até" o par é o vencer-por-dois
    public void A_tela_avisa_quando_a_soma_par_pode_empatar(string contagem, int games, bool avisa)
    {
        Assert.Equal(avisa, ContagemDeGamesDoTorneio.PodeEmpatar(contagem, games));
    }
}
