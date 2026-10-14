namespace Padel.Core.Tests;

public class PlacarTests
{
    private static EventoDoPlacar Pontos(Placar p, int time, int quantos)
    {
        EventoDoPlacar ev = default;
        for (int i = 0; i < quantos; i++) ev = p.PontoPara(time);
        return ev;
    }

    private static EventoDoPlacar GanharGame(Placar p, int time) => Pontos(p, time, 4);

    [Fact]
    public void Os_pontos_contam_0_15_30_40()
    {
        var p = new Placar();
        Assert.Equal("0", p.TextoDosPontos(0));
        p.PontoPara(0); Assert.Equal("15", p.TextoDosPontos(0));
        p.PontoPara(0); Assert.Equal("30", p.TextoDosPontos(0));
        p.PontoPara(0); Assert.Equal("40", p.TextoDosPontos(0));
        Assert.Equal("0", p.TextoDosPontos(1));
    }

    [Fact]
    public void Quatro_pontos_seguidos_fecham_o_game()
    {
        var p = new Placar();
        Pontos(p, 0, 3);
        Assert.Equal(new EventoDoPlacar(TipoDeEventoDoPlacar.Game, 0), p.PontoPara(0));
        Assert.Equal([1, 0], p.Games);
        Assert.Equal([0, 0], p.Pontos);
    }

    [Fact]
    public void Com_ponto_de_ouro_40_40_decide_no_proximo_ponto()
    {
        var p = new Placar(pontoDeOuro: true);
        Pontos(p, 0, 3); Pontos(p, 1, 3);
        Assert.True(p.EmPontoDecisivo);
        Assert.Equal("40", p.TextoDosPontos(0));
        Assert.Equal(TipoDeEventoDoPlacar.Game, p.PontoPara(1).Tipo);
        Assert.Equal([0, 1], p.Games);
    }

    [Fact]
    public void Sem_ponto_de_ouro_40_40_vai_pra_vantagem_e_volta_a_iguais()
    {
        var p = new Placar(pontoDeOuro: false);
        Pontos(p, 0, 3); Pontos(p, 1, 3);
        Assert.False(p.EmPontoDecisivo);
        Assert.Equal(TipoDeEventoDoPlacar.Ponto, p.PontoPara(0).Tipo);
        Assert.Equal("AD", p.TextoDosPontos(0));
        Assert.Equal("40", p.TextoDosPontos(1));
        Assert.Equal(TipoDeEventoDoPlacar.Ponto, p.PontoPara(1).Tipo);
        Assert.Equal("40", p.TextoDosPontos(0));
        p.PontoPara(1);
        Assert.Equal(TipoDeEventoDoPlacar.Game, p.PontoPara(1).Tipo);
        Assert.Equal([0, 1], p.Games);
    }

    [Fact]
    public void O_set_fecha_em_6_games_com_dois_de_diferenca_e_6_5_continua()
    {
        var p = new Placar();
        for (int i = 0; i < 5; i++) { GanharGame(p, 0); GanharGame(p, 1); }
        Assert.Equal(TipoDeEventoDoPlacar.Game, GanharGame(p, 0).Tipo);
        Assert.Equal([6, 5], p.Games);
        Assert.False(p.EmTieBreak);
        Assert.Equal(TipoDeEventoDoPlacar.Partida, GanharGame(p, 0).Tipo);
        Assert.Equal(0, p.Vencedor);
        Assert.Equal("7-5", p.Resumo());
        Assert.Null(p.SetsAnteriores[0].TieBreak);
    }

    [Fact]
    public void Em_6_6_abre_tie_break_que_vai_a_7_com_dois_de_diferenca()
    {
        var p = new Placar();
        for (int i = 0; i < 6; i++) { GanharGame(p, 0); GanharGame(p, 1); }
        Assert.True(p.EmTieBreak);
        Assert.Equal("0", p.TextoDosPontos(0));
        Pontos(p, 0, 6); Pontos(p, 1, 6);
        Assert.Equal("6", p.TextoDosPontos(0));
        Assert.Equal(TipoDeEventoDoPlacar.Ponto, p.PontoPara(0).Tipo);   // 7-6 não fecha
        Assert.Equal(TipoDeEventoDoPlacar.Ponto, p.PontoPara(1).Tipo);   // 7-7
        p.PontoPara(1);
        Assert.Equal(TipoDeEventoDoPlacar.Partida, p.PontoPara(1).Tipo); // 9-7
        Assert.Equal(1, p.Vencedor);
        var tieBreak = p.SetsAnteriores[0].TieBreak;
        Assert.NotNull(tieBreak);
        Assert.Equal([7, 9], tieBreak);
        Assert.Equal("6-7(7)", p.Resumo());
    }

    [Fact]
    public void Melhor_de_tres_precisa_de_dois_sets()
    {
        var p = new Placar(setsParaVencer: 2);
        for (int i = 0; i < 6; i++) GanharGame(p, 0);
        Assert.Equal([1, 0], p.Sets);
        Assert.False(p.Acabou);
        for (int i = 0; i < 6; i++) GanharGame(p, 1);
        Assert.Equal([1, 1], p.Sets);
        for (int i = 0; i < 5; i++) GanharGame(p, 0);
        Assert.Equal(TipoDeEventoDoPlacar.Partida, GanharGame(p, 0).Tipo);
        Assert.Equal("6-0 0-6 6-0", p.Resumo());
        Assert.Equal(new EventoDoPlacar(TipoDeEventoDoPlacar.Encerrada, 0), p.PontoPara(1));
    }

    [Fact]
    public void O_saque_alterna_entre_os_times_a_cada_game_e_entre_os_jogadores_do_time()
    {
        var p = new Placar(timeQueSaca: 0);
        Assert.Equal(new Sacador(0, 0), p.Sacador);
        GanharGame(p, 0); Assert.Equal(new Sacador(1, 0), p.Sacador);
        GanharGame(p, 0); Assert.Equal(new Sacador(0, 1), p.Sacador);
        GanharGame(p, 1); Assert.Equal(new Sacador(1, 1), p.Sacador);
        GanharGame(p, 1); Assert.Equal(new Sacador(0, 0), p.Sacador);
    }

    [Fact]
    public void O_lado_do_saque_e_a_direita_com_soma_par_e_esquerda_com_impar()
    {
        var p = new Placar();
        Assert.Equal(LadoDoSaque.Direita, p.LadoDoSaque);
        p.PontoPara(0); Assert.Equal(LadoDoSaque.Esquerda, p.LadoDoSaque);
        p.PontoPara(1); Assert.Equal(LadoDoSaque.Direita, p.LadoDoSaque);
    }

    [Fact]
    public void No_tie_break_quem_abriu_saca_um_ponto_e_depois_trocam_de_dois_em_dois()
    {
        var p = new Placar(timeQueSaca: 0);
        for (int i = 0; i < 6; i++) { GanharGame(p, 0); GanharGame(p, 1); }
        Assert.Equal(0, p.Sacador.Time);
        p.PontoPara(0); Assert.Equal(1, p.Sacador.Time);
        p.PontoPara(0); Assert.Equal(1, p.Sacador.Time);
        p.PontoPara(0); Assert.Equal(0, p.Sacador.Time);
        p.PontoPara(1); Assert.Equal(0, p.Sacador.Time);
        p.PontoPara(1); Assert.Equal(1, p.Sacador.Time);
    }

    [Fact]
    public void Depois_do_tie_break_o_set_seguinte_comeca_com_quem_nao_abriu_o_tie_break()
    {
        var p = new Placar(timeQueSaca: 0, setsParaVencer: 2);
        for (int i = 0; i < 6; i++) { GanharGame(p, 0); GanharGame(p, 1); }
        Assert.Equal(0, p.Sacador.Time);
        Assert.Equal(TipoDeEventoDoPlacar.Set, Pontos(p, 0, 7).Tipo);
        Assert.Equal(1, p.Sacador.Time);
        Assert.Equal([0, 0], p.Games);
    }

    // ── Formato curto (DESIGN da fatia no Unreal, pilar 3: partida de 4 a 6 minutos) ──────────────────
    // Set até 4 games (vence com 2 de vantagem) e, em 4-4, tie-break; o "super tie-break" vai a 10, também por 2.

    private static void GanharGames(Placar p, int time, int quantos)
    {
        for (int i = 0; i < quantos; i++) GanharGame(p, time);
    }

    [Fact]
    public void Set_de_4_games_fecha_em_4_2()
    {
        var p = new Placar(gamesPorSet: 4);
        GanharGames(p, 1, 2);
        GanharGames(p, 0, 3);
        Assert.Equal(new EventoDoPlacar(TipoDeEventoDoPlacar.Partida, 0), GanharGame(p, 0));
        Assert.Equal("4-2", p.Resumo());
    }

    [Fact]
    public void Set_de_4_games_em_4_3_continua_e_fecha_em_5_3()
    {
        var p = new Placar(gamesPorSet: 4);
        GanharGames(p, 1, 3);
        GanharGames(p, 0, 3);
        Assert.Equal(new EventoDoPlacar(TipoDeEventoDoPlacar.Game, 0), GanharGame(p, 0));
        Assert.False(p.Acabou);
        Assert.Equal(new EventoDoPlacar(TipoDeEventoDoPlacar.Partida, 0), GanharGame(p, 0));
        Assert.Equal("5-3", p.Resumo());
    }

    [Fact]
    public void Em_4_4_entra_o_super_tie_break_que_vai_a_10_por_2()
    {
        var p = new Placar(gamesPorSet: 4, pontosDoTieBreak: 10);
        for (int i = 0; i < 4; i++) { GanharGame(p, 0); GanharGame(p, 1); }
        Assert.True(p.EmTieBreak);
        Pontos(p, 0, 9); Pontos(p, 1, 9);
        Assert.Equal("9", p.TextoDosPontos(0));
        Assert.Equal(new EventoDoPlacar(TipoDeEventoDoPlacar.Ponto, 0), p.PontoPara(0));   // 10-9: não basta
        Assert.False(p.Acabou);
        p.PontoPara(1);
        p.PontoPara(1);   // 10-11
        Assert.Equal(new EventoDoPlacar(TipoDeEventoDoPlacar.Partida, 1), p.PontoPara(1));   // 10-12
        Assert.Equal("4-5(10)", p.Resumo());
    }

    [Fact]
    public void O_super_tie_break_fecha_em_10_8()
    {
        var p = new Placar(gamesPorSet: 4, pontosDoTieBreak: 10);
        for (int i = 0; i < 4; i++) { GanharGame(p, 0); GanharGame(p, 1); }
        Pontos(p, 1, 8); Pontos(p, 0, 9);
        Assert.Equal(new EventoDoPlacar(TipoDeEventoDoPlacar.Partida, 0), p.PontoPara(0));
        Assert.Equal("5-4(8)", p.Resumo());
    }

    [Fact]
    public void O_padrao_continua_6_games_e_tie_break_a_7()
    {
        var p = new Placar();
        Assert.Equal(6, p.GamesPorSet);
        Assert.Equal(7, p.PontosDoTieBreak);
    }

    [Theory]
    [InlineData(0, 7)]
    [InlineData(4, 0)]
    public void Games_por_set_e_pontos_do_tie_break_precisam_ser_positivos(int games, int pontos) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Placar(gamesPorSet: games, pontosDoTieBreak: pontos));

    [Fact]
    public void A_partida_passa_o_formato_ao_placar_e_ele_nao_vale_online()
    {
        var curta = new OpcoesDaPartida { GamesPorSet = 4, PontosDoTieBreak = 10, Humanos = OpcoesDaPartida.NinguemHumano, Semente = 1 };
        var p = new Partida(curta);
        Assert.Equal(4, p.Placar.GamesPorSet);
        Assert.Equal(10, p.Placar.PontosDoTieBreak);
        Assert.False(curta.ValeOnline);   // o protocolo só carrega ponto de ouro e sets (como nos pontos corridos)
        Assert.True(new OpcoesDaPartida().ValeOnline);
    }

    [Fact]
    public void Uma_partida_curta_entre_IAs_termina_com_um_set_de_4()
    {
        var p = new Partida(new OpcoesDaPartida { GamesPorSet = 4, PontosDoTieBreak = 10, Humanos = OpcoesDaPartida.NinguemHumano, Semente = 3 });
        for (int i = 0; i < 120 * 60 * 30 && !p.Acabou; i++) p.Avancar(1f / 120f);
        Assert.True(p.Acabou);
        var set = p.Placar.SetsAnteriores.Single();
        int v = Math.Max(set.Games[0], set.Games[1]), d = Math.Min(set.Games[0], set.Games[1]);
        // Os únicos sets possíveis: 4-0, 4-1, 4-2, 5-3, e 5-4 pelo tie-break.
        bool valido = (v == 4 && d <= 2) || (v == 5 && d == 3 && set.TieBreak is null) || (v == 5 && d == 4 && set.TieBreak is not null);
        Assert.True(valido, $"set {set.Games[0]}-{set.Games[1]}");
    }
}
