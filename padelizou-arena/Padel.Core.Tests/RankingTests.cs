using Padel.Core.Ranking;

namespace Padel.Core.Tests;

// O rating do online ranqueado: o Padelímetro do Padelizou (RANKING.md, "Trilha A — o
// Padelímetro"; código em Padelizou/Services/Padelimetro.cs, FaixasDePadelimetro.cs e
// PadelimetroService.cs) portado pro Core, puro.
//
// Os números marcados "RANKING.md" ou "Padelizou" são os de lá — a régua do jogo tem que dar
// o MESMO número que o site daria pra mesma partida, senão "ranking cruzado" vira duas réguas.
// Os marcados "à mão" foram calculados a partir do código do Padelizou, fora do C#.
public class RankingTests
{
    // ── Ajudantes ──────────────────────────────────────────────────────────────────────────

    private static FaixaDoPadelimetro Faixa(string rotulo) =>
        FaixasDoPadelimetro.Escada.Single(f => f.Rotulo == rotulo);

    // Um jogador com rótulo coerente com o número (o estado que uma conta salva teria).
    private static NivelNoRanking Nivel(int pdz, int jogos) =>
        new(pdz, jogos, FaixasDoPadelimetro.DoNivel(pdz), jogos);

    private static LugarRanqueado Humano(string id, NivelNoRanking nivel, bool abandonou = false) =>
        LugarRanqueado.Humano(id, nivel, abandonou);

    private static LugarRanqueado[] Quatro(int a, int b, int c, int d, int jogos = 20) =>
        [Humano("a", Nivel(a, jogos)), Humano("b", Nivel(b, jogos)), Humano("c", Nivel(c, jogos)), Humano("d", Nivel(d, jogos))];

    private static IReadOnlyList<MovimentoNoRanking?> Aplicar(IReadOnlyList<LugarRanqueado> lugares,
        int? vencedor, int games0, int games1) =>
        Ranqueamento.Aplicar(new PartidaRanqueada(lugares, vencedor, games0, games1));

    private static MovimentoNoRanking Mov(IReadOnlyList<MovimentoNoRanking?> movimentos, int lugar)
    {
        var m = movimentos[lugar];
        Assert.NotNull(m);
        return m;
    }

    private static int[] Deltas(IReadOnlyList<MovimentoNoRanking?> movimentos) =>
        [.. Enumerable.Range(0, movimentos.Count).Select(i => Mov(movimentos, i).Delta)];

    // ── A matemática, com os números do RANKING.md ────────────────────────────────────────

    [Fact]
    public void Cem_pontos_de_diferenca_sao_64_por_cento_e_duzentos_sao_76()
    {
        // RANKING.md: "100 pontos de diferença ≈ 64% de favoritismo; 200 ≈ 76%".
        Assert.Equal(0.5, Padelimetro.Expectativa(500, 500), precision: 10);
        Assert.Equal(0.64, Padelimetro.Expectativa(600, 500), precision: 2);
        Assert.Equal(0.76, Padelimetro.Expectativa(700, 500), precision: 2);
        // As duas expectativas de um jogo somam 1.
        Assert.Equal(1.0, Padelimetro.Expectativa(640, 580) + Padelimetro.Expectativa(580, 640), precision: 10);
    }

    [Theory]
    [InlineData(6, 0, 1.6)]  // RANKING.md: "6x0 vale 1,6×"
    [InlineData(7, 6, 1.1)]  // RANKING.md: "7x6 vale 1,1×"
    [InlineData(0, 6, 1.6)]  // a diferença é absoluta
    [InlineData(9, 0, 1.6)]  // teto em 6 de diferença
    [InlineData(4, 4, 1.0)]  // games iguais não amplificam
    public void Fator_de_games_premia_o_passeio_com_teto_em_seis(int g0, int g1, double esperado)
    {
        Assert.Equal(esperado, Padelimetro.FatorDeGames(g0, g1), precision: 10);
    }

    [Fact]
    public void K_e_40_nos_primeiros_10_jogos_e_20_depois()
    {
        // RANKING.md: "K = 40 nos primeiros 10 jogos ('em calibração'), K = 20 depois".
        Assert.Equal(40, Padelimetro.K(0));
        Assert.Equal(40, Padelimetro.K(9));
        Assert.Equal(20, Padelimetro.K(10));
        Assert.Equal(20, Padelimetro.K(500));
        Assert.True(Padelimetro.EmCalibracao(9));
        Assert.False(Padelimetro.EmCalibracao(10));
    }

    [Fact]
    public void Os_numeros_de_variacao_do_Padelizou()
    {
        // Padelizou.Tests/PadelimetroTests.cs: favorito de 64% sobe 7, azarão de 36% sobe 13.
        Assert.Equal(7, Padelimetro.Variacao(20, 1.0, venceu: true, expectativaDoTime: 0.64));
        Assert.Equal(13, Padelimetro.Variacao(20, 1.0, venceu: true, expectativaDoTime: 0.36));

        // "Quem se arrisca pra cima": 500 contra 600 perde 7 e ganha 13.
        double e = Padelimetro.Expectativa(500, 600);
        Assert.Equal(-7, Padelimetro.Variacao(20, 1.0, venceu: false, expectativaDoTime: e));
        Assert.Equal(13, Padelimetro.Variacao(20, 1.0, venceu: true, expectativaDoTime: e));

        // Média da dupla e as pontas da régua.
        Assert.Equal(550.0, Padelimetro.NivelDaDupla(500, 600), precision: 10);
        Assert.Equal(0, Padelimetro.Acomodar(-5));
        Assert.Equal(1000, Padelimetro.Acomodar(1200));
    }

    [Fact]
    public void Quatro_estreantes_num_6x2_viram_528_e_472()
    {
        // Padelizou.Tests (PadelimetroServiceTests): 4 estreantes a 500, expectativa meio a
        // meio, K de calibração 40, fator do 6x2 = 1,4 → 40 × 1,4 × 0,5 = 28.
        var estreante = NivelNoRanking.Estreante();
        LugarRanqueado[] lugares =
            [Humano("a", estreante), Humano("b", estreante), Humano("c", estreante), Humano("d", estreante)];

        var movimentos = Aplicar(lugares, vencedor: 0, 6, 2);

        Assert.Equal([28, 28, -28, -28], Deltas(movimentos));
        Assert.Equal(528, Mov(movimentos, 0).Depois.Pdz);
        Assert.Equal(472, Mov(movimentos, 3).Depois.Pdz);
        Assert.All(movimentos, m => Assert.Equal(1, m?.Depois.Jogos));
        Assert.Equal(MotivoDoMovimento.Vitoria, Mov(movimentos, 0).Motivo);
        Assert.Equal(MotivoDoMovimento.Derrota, Mov(movimentos, 2).Motivo);
    }

    [Fact]
    public void Estreantes_pela_entrada_da_2a_num_6x4_viram_824()
    {
        // Padelizou.Tests: entrada da 2ª masculina é 800; 6x4 → fator 1,2; K 40 → 24.
        var estreante = NivelNoRanking.Estreante(Faixa("2ª").Entrada);
        Assert.Equal(800, estreante.Pdz);
        LugarRanqueado[] lugares =
            [Humano("a", estreante), Humano("b", estreante), Humano("c", estreante), Humano("d", estreante)];

        var movimentos = Aplicar(lugares, vencedor: 0, 6, 4);

        Assert.Equal([824, 824, 776, 776], movimentos.Select(m => m?.Depois.Pdz ?? -1));
    }

    [Fact]
    public void O_estreante_nasce_no_meio_da_regua_sem_jogos()
    {
        // RANKING.md "Onde o número nasce": sem categoria, a entrada é a NEUTRA do Padelizou (500).
        var novo = NivelNoRanking.Estreante();
        Assert.Equal(500, novo.Pdz);
        Assert.Equal(0, novo.Jogos);
        Assert.Equal("5ª", novo.Rotulo.Rotulo);
        Assert.True(novo.EmCalibracao);

        Assert.Equal("3ª", NivelNoRanking.Estreante(Faixa("3ª").Entrada).Rotulo.Rotulo);
    }

    // ── Três casos calculados à mão a partir do código do Padelizou ───────────────────────

    [Fact]
    public void A_mao_1_dupla_desigual_com_parceiro_em_calibracao()
    {
        // (650 com 12 jogos, 550 com 4 jogos) — média 600 — x (700, 600) — média 650 —, todos
        // os outros estáveis. O time 0 vence 6x3.
        // E0 = 1 / (1 + 10^(50/400)) = 1 / 2,333521 = 0,428537; fator = 1,3.
        //   650 (K 20): 20 × 1,3 × 0,571463 = 14,858 → +15
        //   550 (K 40): 40 × 1,3 × 0,571463 = 29,716 → +30
        //   700 e 600 (K 20): 20 × 1,3 × (0 − 0,571463) = −14,858 → −15
        LugarRanqueado[] lugares =
            [Humano("a", Nivel(650, 12)), Humano("b", Nivel(550, 4)), Humano("c", Nivel(700, 30)), Humano("d", Nivel(600, 30))];

        Assert.Equal([15, 30, -15, -15], Deltas(Aplicar(lugares, vencedor: 0, 6, 3)));
    }

    [Fact]
    public void A_mao_2_a_zebra_de_36_por_cento()
    {
        // (500, 500) x (600, 600), todos com K 20, 6x4 (fator 1,2). E da zebra = 0,359935.
        //   zebra vence:    20 × 1,2 × 0,640065 = 15,36 → ±15
        //   favorito vence: 20 × 1,2 × 0,359935 =  8,64 → ±9
        Assert.Equal([15, 15, -15, -15], Deltas(Aplicar(Quatro(500, 500, 600, 600), vencedor: 0, 6, 4)));
        Assert.Equal([-9, -9, 9, 9], Deltas(Aplicar(Quatro(500, 500, 600, 600), vencedor: 1, 4, 6)));
    }

    [Fact]
    public void A_mao_3_tie_break_entre_favorito_e_azarao_proximos()
    {
        // (820, 780) — média 800 — x (760, 720) — média 740 —, K 20, 7x6 (fator 1,1).
        // E0 = 1 / (1 + 10^(−60/400)) = 1 / 1,707946 = 0,585499.
        //   20 × 1,1 × 0,414501 = 9,119 → +9 / −9
        Assert.Equal([9, 9, -9, -9], Deltas(Aplicar(Quatro(820, 780, 760, 720), vencedor: 0, 7, 6)));
    }

    // ── O que o RANKING.md promete ────────────────────────────────────────────────────────

    [Fact]
    public void Parceiros_com_o_mesmo_resultado_sobem_igual()
    {
        // A expectativa compara as MÉDIAS: o forte e o fraco da mesma dupla movem o mesmo
        // tanto, desde que tenham o mesmo K. (700, 500) é uma dupla de 600.
        var desigual = Deltas(Aplicar(Quatro(700, 500, 600, 600), vencedor: 0, 6, 4));
        var igual = Deltas(Aplicar(Quatro(600, 600, 600, 600), vencedor: 0, 6, 4));

        Assert.Equal(desigual[0], desigual[1]);
        Assert.Equal(12, desigual[0]); // 20 × 1,2 × 0,5
        Assert.Equal(igual, desigual);
    }

    [Fact]
    public void Zebra_move_mais_que_resultado_esperado()
    {
        int zebra = Deltas(Aplicar(Quatro(500, 500, 600, 600), vencedor: 0, 6, 4))[0];
        int favorito = Deltas(Aplicar(Quatro(500, 500, 600, 600), vencedor: 1, 4, 6))[2];

        Assert.True(zebra > favorito, $"zebra {zebra} × favorito {favorito}");
    }

    [Fact]
    public void Em_calibracao_o_numero_anda_o_dobro_e_o_decimo_jogo_encerra_a_calibracao()
    {
        // Mesma dupla, mesmo jogo: um com 9 jogos (K 40), o outro com 10 (K 20). 6x2 entre iguais.
        LugarRanqueado[] lugares =
            [Humano("a", Nivel(600, 9)), Humano("b", Nivel(600, 10)), Humano("c", Nivel(600, 30)), Humano("d", Nivel(600, 30))];

        var movimentos = Aplicar(lugares, vencedor: 0, 6, 2);

        Assert.Equal(28, Mov(movimentos, 0).Delta); // 40 × 1,4 × 0,5
        Assert.Equal(14, Mov(movimentos, 1).Delta); // 20 × 1,4 × 0,5
        Assert.Equal(10, Mov(movimentos, 0).Depois.Jogos);
        Assert.False(Mov(movimentos, 0).Depois.EmCalibracao);
    }

    [Fact]
    public void As_pontas_da_regua_seguram_e_o_delta_e_o_que_de_fato_andou()
    {
        // 4 × 999 em calibração, 6x0: 40 × 1,6 × 0,5 = 32 — mas o teto é 1000.
        var noTopo = Aplicar(Quatro(999, 999, 999, 999, jogos: 0), vencedor: 0, 6, 0);
        Assert.Equal(1000, Mov(noTopo, 0).Depois.Pdz);
        Assert.Equal(1, Mov(noTopo, 0).Delta);
        Assert.Equal(967, Mov(noTopo, 2).Depois.Pdz);

        var noChao = Aplicar(Quatro(5, 5, 5, 5, jogos: 0), vencedor: 0, 6, 0);
        Assert.Equal(0, Mov(noChao, 2).Depois.Pdz);
        Assert.Equal(-5, Mov(noChao, 2).Delta);
    }

    // ── Rótulo com histerese ──────────────────────────────────────────────────────────────

    [Fact]
    public void Em_calibracao_o_rotulo_espera___os_numeros_reais_do_Padelizou()
    {
        // Padelizou.Tests/RotuloDaFaixaNaoSobeNumTorneioSoTests.cs — números de produção.
        // Arthur Guex: 802 com 4 jogos, entrou pela 3ª → continua 3ª.
        var guex = new NivelNoRanking(780, 3, Faixa("3ª"), 3).DepoisDoJogo(+22);
        Assert.Equal(802, guex.Pdz);
        Assert.Equal("3ª", guex.Rotulo.Rotulo);

        // Alexandre Longhi: 600 → 742 em 5 jogos pela 4ª → continua 4ª.
        var longhi = new NivelNoRanking(720, 4, Faixa("4ª"), 4).DepoisDoJogo(+22);
        Assert.Equal(742, longhi.Pdz);
        Assert.Equal("4ª", longhi.Rotulo.Rotulo);

        // Arthur Prass: 700 → 635 em 2 jogos pela 3ª → não é rebaixado na estreia.
        var prass = new NivelNoRanking(670, 1, Faixa("3ª"), 1).DepoisDoJogo(-35);
        Assert.Equal(635, prass.Pdz);
        Assert.Equal("3ª", prass.Rotulo.Rotulo);
    }

    [Fact]
    public void O_rotulo_espera_os_9_primeiros_jogos_e_segue_o_numero_a_partir_do_decimo()
    {
        // RANKING.md "O RÓTULO da tela": "Em calibração (MENOS de 10 jogos), o rótulo é a faixa da
        // categoria". Estreante a 500 perdendo 25 por jogo: 5ª até o 9º jogo, 7ª no 10º — e os jogos
        // da calibração contam como jogos no rótulo (é o que libera a descida já no 10º).
        var nivel = NivelNoRanking.Estreante();
        for (int jogo = 1; jogo <= 9; jogo++)
        {
            nivel = nivel.DepoisDoJogo(-25);
            Assert.Equal("5ª", nivel.Rotulo.Rotulo);
            Assert.Equal(jogo, nivel.JogosNoRotulo);
        }
        Assert.Equal(275, nivel.Pdz);

        nivel = nivel.DepoisDoJogo(-25);
        Assert.Equal(250, nivel.Pdz);
        Assert.Equal(10, nivel.Jogos);
        Assert.Equal("7ª", nivel.Rotulo.Rotulo);
        Assert.Equal(0, nivel.JogosNoRotulo);

        // Subindo, a mesma fronteira: 725 no 9º ainda é 5ª; 750 no 10º já é a faixa crua (2ª).
        var subindo = NivelNoRanking.Estreante();
        for (int jogo = 1; jogo <= 9; jogo++) subindo = subindo.DepoisDoJogo(+25);
        Assert.Equal(725, subindo.Pdz);
        Assert.Equal("5ª", subindo.Rotulo.Rotulo);
        Assert.Equal("2ª", subindo.DepoisDoJogo(+25).Rotulo.Rotulo);
    }

    [Fact]
    public void Os_quatro_numeros_do_Padelizou_ja_valem_no_decimo_jogo()
    {
        // Padelizou.Tests/RotuloDaFaixaNaoSobeNumTorneioSoTests.cs usa `const int dezJogos = 10`:
        // com 10 jogos a calibração acabou e o número manda. Aqui o 10º jogo chega pela calibração
        // inteira (9 jogos a 700 na 3ª, com o JogosNoRotulo contado desde o seed), não montado.
        string RotuloNoDecimoJogo(int pdzDepois)
        {
            var nivel = NivelNoRanking.Estreante(Faixa("3ª").Entrada);
            for (int jogo = 1; jogo <= 9; jogo++) nivel = nivel.DepoisDoJogo(0);
            var decimo = nivel.DepoisDoJogo(pdzDepois - nivel.Pdz);
            Assert.Equal(10, decimo.Jogos);
            return decimo.Rotulo.Rotulo;
        }

        Assert.Equal("3ª", RotuloNoDecimoJogo(799));   // teto + 50 ainda é 3ª
        Assert.Equal("2ª", RotuloNoDecimoJogo(800));   // passou da folga → sobe
        Assert.Equal("3ª", RotuloNoDecimoJogo(600));   // piso − 50 ainda é 3ª
        Assert.Equal("4ª", RotuloNoDecimoJogo(599));   // passou da folga → desce
    }

    [Fact]
    public void Passada_a_calibracao_o_numero_manda_com_folga_de_50_pros_dois_lados()
    {
        // Os quatro números do Padelizou, âncora na 3ª (650–749), BEM passada a calibração (20
        // jogos). A fronteira exata — o 10º jogo — é o teste Os_quatro_numeros_..._no_decimo_jogo.
        Assert.Equal("3ª", new NivelNoRanking(789, 20, Faixa("3ª"), 20).DepoisDoJogo(+10).Rotulo.Rotulo); // 799
        Assert.Equal("2ª", new NivelNoRanking(790, 20, Faixa("3ª"), 20).DepoisDoJogo(+10).Rotulo.Rotulo); // 800
        Assert.Equal("3ª", new NivelNoRanking(610, 20, Faixa("3ª"), 20).DepoisDoJogo(-10).Rotulo.Rotulo); // 600
        Assert.Equal("4ª", new NivelNoRanking(609, 20, Faixa("3ª"), 20).DepoisDoJogo(-10).Rotulo.Rotulo); // 599
    }

    [Fact]
    public void Oscilacao_pequena_na_divisa_nao_troca_o_rotulo()
    {
        // 4ª é 550–649. Um jogador estável sambando em volta do teto CRU (649) não vira "3ª"
        // num jogo e "4ª" no seguinte.
        var nivel = new NivelNoRanking(640, 30, Faixa("4ª"), 30);
        foreach (int delta in new[] { +15, -12, +14, -13, +16, -11, +12 })
        {
            nivel = nivel.DepoisDoJogo(delta);
            Assert.Equal("4ª", nivel.Rotulo.Rotulo);
        }
        // A prova de que a divisa foi cruzada: a faixa CRUA do último número já é outra.
        Assert.Equal(661, nivel.Pdz);
        Assert.Equal("3ª", FaixasDoPadelimetro.DoNivel(nivel.Pdz).Rotulo);
    }

    [Fact]
    public void A_mesma_oscilacao_jogada_de_verdade_tambem_nao_troca()
    {
        // Ponta a ponta: 645 (rótulo 4ª) ganhando e perdendo 6x4 de duplas iguais, ±12 por jogo.
        var eu = new NivelNoRanking(645, 30, Faixa("4ª"), 30);
        for (int i = 0; i < 6; i++)
        {
            bool ganha = i % 2 == 0;
            LugarRanqueado[] lugares =
                [Humano("eu", eu), Humano("p", Nivel(eu.Pdz, 30)), Humano("c", Nivel(eu.Pdz, 30)), Humano("d", Nivel(eu.Pdz, 30))];
            eu = Mov(Aplicar(lugares, vencedor: ganha ? 0 : 1, ganha ? 6 : 4, ganha ? 4 : 6), 0).Depois;
            Assert.Equal("4ª", eu.Rotulo.Rotulo);
        }
        Assert.Equal(645, eu.Pdz);
    }

    [Fact]
    public void Descer_exige_10_jogos_no_rotulo_e_subir_nao_espera()
    {
        // RANKING.md "Subir e descer de faixa": descer só 50 abaixo do piso E com 10 jogos desde
        // a última troca. Com 9 jogos no rótulo, 590 (abaixo de 600) ainda é 3ª.
        var quaseCaindo = new NivelNoRanking(610, 40, Faixa("3ª"), 8).DepoisDoJogo(-20);
        Assert.Equal(590, quaseCaindo.Pdz);
        Assert.Equal(9, quaseCaindo.JogosNoRotulo);
        Assert.Equal("3ª", quaseCaindo.Rotulo.Rotulo);

        var caiu = quaseCaindo.DepoisDoJogo(-5);
        Assert.Equal("4ª", caiu.Rotulo.Rotulo);
        Assert.Equal(0, caiu.JogosNoRotulo);

        // Subir é sem espera: acabou de trocar de rótulo (0 jogos nele) e cruzou teto + 50.
        var subiu = new NivelNoRanking(690, 40, Faixa("4ª"), 0).DepoisDoJogo(+15);
        Assert.Equal("3ª", subiu.Rotulo.Rotulo);
        Assert.Equal(0, subiu.JogosNoRotulo);
    }

    [Fact]
    public void Faltam_X_mede_o_que_muda_o_rotulo()
    {
        // Padelizou: 742 jogando a 3ª (teto 749), fora da calibração → o rótulo vira 2ª em 800: faltam 58.
        Assert.Equal(58, new NivelNoRanking(742, 10, Faixa("3ª"), 10).FaltaPraSubir);
        // Em calibração não há o que dizer; no topo da escada não existe subir.
        Assert.Null(new NivelNoRanking(742, 5, Faixa("4ª"), 5).FaltaPraSubir);
        Assert.Null(new NivelNoRanking(950, 30, Faixa("Open"), 30).FaltaPraSubir);
    }

    // ── Abandono (o Padelizou não tem: decisão do jogo) ───────────────────────────────────

    [Fact]
    public void Quem_abandona_perde_6x0_com_o_parceiro_e_os_adversarios_nao_se_movem()
    {
        // 4 × 600, estáveis. O lugar 0 caiu e não voltou: 20 × 1,6 × (0 − 0,5) = −16 pra ele E pro
        // parceiro. (A primeira versão deixava o parceiro parado, com 0 e 20 jogos; a revisão achou
        // o abandono por procuração — ver O_parceiro_de_quem_abandona_... e Ranqueamento.)
        LugarRanqueado[] lugares =
            [Humano("a", Nivel(600, 20), abandonou: true), Humano("b", Nivel(600, 20)), Humano("c", Nivel(600, 20)), Humano("d", Nivel(600, 20))];

        var movimentos = Aplicar(lugares, vencedor: null, 3, 2);

        Assert.Equal(-16, Mov(movimentos, 0).Delta);
        Assert.Equal(21, Mov(movimentos, 0).Depois.Jogos);
        Assert.Equal(MotivoDoMovimento.Abandonou, Mov(movimentos, 0).Motivo);

        Assert.Equal(-16, Mov(movimentos, 1).Delta);
        Assert.Equal(21, Mov(movimentos, 1).Depois.Jogos);
        Assert.Equal(MotivoDoMovimento.ParceiroAbandonou, Mov(movimentos, 1).Motivo);

        foreach (int adversario in new[] { 2, 3 })
        {
            Assert.Equal(0, Mov(movimentos, adversario).Delta);
            Assert.Equal(20, Mov(movimentos, adversario).Depois.Jogos);
            Assert.Equal(MotivoDoMovimento.AdversarioAbandonou, Mov(movimentos, adversario).Motivo);
        }
    }

    [Fact]
    public void Abandonar_nunca_sai_mais_barato_que_perder_jogando()
    {
        // O que fecha a porta do rage-quit: pra qualquer placar de derrota, sair custa igual ou mais.
        foreach (var (a, b, c, d) in new[] { (600, 600, 600, 600), (500, 500, 600, 600), (700, 700, 500, 500) })
        {
            LugarRanqueado[] saindo =
                [Humano("a", Nivel(a, 20), abandonou: true), Humano("b", Nivel(b, 20)), Humano("c", Nivel(c, 20)), Humano("d", Nivel(d, 20))];
            int abandono = Mov(Aplicar(saindo, vencedor: null, 0, 0), 0).Delta;

            foreach (var (g0, g1) in new[] { (6, 7), (5, 7), (4, 6), (3, 6), (2, 6), (1, 6), (0, 6) })
            {
                int derrota = Mov(Aplicar(Quatro(a, b, c, d), vencedor: 1, g0, g1), 0).Delta;
                Assert.True(abandono <= derrota, $"({a},{b})x({c},{d}) {g0}x{g1}: abandono {abandono} > derrota {derrota}");
            }
        }
    }

    [Fact]
    public void O_placar_da_hora_nao_alivia_quem_abandona()
    {
        // Saiu ganhando de 5x1: a conta é a mesma de quem saiu perdendo.
        LugarRanqueado[] lugares =
            [Humano("a", Nivel(600, 20), abandonou: true), Humano("b", Nivel(600, 20)), Humano("c", Nivel(600, 20)), Humano("d", Nivel(600, 20))];

        Assert.Equal(-16, Mov(Aplicar(lugares, vencedor: null, 5, 1), 0).Delta);
        Assert.Equal(-16, Mov(Aplicar(lugares, vencedor: 0, 5, 1), 0).Delta);
    }

    [Fact]
    public void Um_vencedor_que_venha_junto_com_o_abandono_e_ignorado_por_todos()
    {
        // É esta regra que fecha a fraude do amigo no time adversário: se o vencedor que chega
        // junto com o abandono pagasse alguém, o adversário ganharia ponto com quem saiu. Os quatro
        // movimentos têm que ser os MESMOS com vencedor nulo, 0 ou 1 — dos dois lados da quadra.
        foreach (int quemSai in new[] { 0, 3 })
        {
            LugarRanqueado[] lugares =
                [.. Enumerable.Range(0, 4).Select(i => Humano($"j{i}", Nivel(600, 20), abandonou: i == quemSai))];
            var semVencedor = Aplicar(lugares, vencedor: null, 5, 1);

            foreach (int vencedor in new[] { 0, 1 })
            {
                var comVencedor = Aplicar(lugares, vencedor, 5, 1);
                Assert.Equal(semVencedor, comVencedor);

                foreach (int adversario in quemSai < 2 ? new[] { 2, 3 } : new[] { 0, 1 })
                {
                    Assert.Equal(0, Mov(comVencedor, adversario).Delta);
                    Assert.Equal(20, Mov(comVencedor, adversario).Depois.Jogos);
                    Assert.Equal(MotivoDoMovimento.AdversarioAbandonou, Mov(comVencedor, adversario).Motivo);
                }
            }
        }
    }

    [Fact]
    public void O_parceiro_de_quem_abandona_tambem_nao_sai_mais_barato_que_perder_jogando()
    {
        // Achado da revisão — o abandono POR PROCURAÇÃO: se o parceiro de quem sai fica parado, a
        // dupla escolhe quem absorve a derrota. Quando vai perder, a conta descartável (alt)
        // abandona e a principal não anda; no chão da régua o alt nem sente. A porta do rage-quit só
        // fecha se, pra QUALQUER um do time de quem saiu, sair não for mais barato que perder jogando.
        foreach (var (a, b, c, d) in new[] { (600, 600, 600, 600), (500, 500, 600, 600), (700, 700, 500, 500), (800, 0, 400, 400) })
        {
            LugarRanqueado[] altSai =
                [Humano("a", Nivel(a, 20)), Humano("b", Nivel(b, 20), abandonou: true), Humano("c", Nivel(c, 20)), Humano("d", Nivel(d, 20))];
            int principal = Mov(Aplicar(altSai, vencedor: null, 0, 0), 0).Delta;

            foreach (var (g0, g1) in new[] { (6, 7), (5, 7), (4, 6), (3, 6), (2, 6), (1, 6), (0, 6) })
            {
                int derrota = Mov(Aplicar(Quatro(a, b, c, d), vencedor: 1, g0, g1), 0).Delta;
                Assert.True(principal <= derrota, $"({a},{b})x({c},{d}) {g0}x{g1}: principal {principal} > derrota {derrota}");
            }
        }

        // A sonda da revisão: principal 800 + alt 0 contra (400, 400). Médias iguais, o alt no chão
        // não tem o que perder — e a derrota não some: o principal paga o 6x0, 20 × 1,6 × 0,5 = 16.
        LugarRanqueado[] sonda =
            [Humano("principal", Nivel(800, 20)), Humano("alt", Nivel(0, 20), abandonou: true), Humano("c", Nivel(400, 20)), Humano("d", Nivel(400, 20))];
        Assert.Equal(-16, Mov(Aplicar(sonda, vencedor: null, 0, 0), 0).Delta);
    }

    [Fact]
    public void Abandono_dos_dois_lados_derruba_os_dois_times()
    {
        // Um de cada time saiu (lugares 0 e 3): os dois times perderam por abandono.
        LugarRanqueado[] lugares =
            [Humano("a", Nivel(600, 20), abandonou: true), Humano("b", Nivel(600, 20)), Humano("c", Nivel(600, 20)), Humano("d", Nivel(600, 20), abandonou: true)];

        var movimentos = Aplicar(lugares, vencedor: null, 2, 2);

        Assert.Equal([-16, -16, -16, -16], Deltas(movimentos));
        Assert.Equal(
            [MotivoDoMovimento.Abandonou, MotivoDoMovimento.ParceiroAbandonou, MotivoDoMovimento.ParceiroAbandonou, MotivoDoMovimento.Abandonou],
            movimentos.Select(m => m?.Motivo));
    }

    [Fact]
    public void Abandono_por_procuracao_nao_sobe_o_principal()
    {
        // A outra sonda da revisão: principal e alt a 700 (20 jogos), sempre contra duplas de 700,
        // aproveitamento real de 50%. Vitória é 6x4; na derrota o alt abandona. Com o parceiro
        // parado, o principal ia de 700 a 815 em 20 partidas e o rótulo virava 2ª.
        NivelNoRanking Jornada(bool altAbandonaNaDerrota)
        {
            var principal = Nivel(700, 20);
            var alt = Nivel(700, 20);
            for (int partida = 0; partida < 20; partida++)
            {
                bool vence = partida % 2 == 0;
                bool sai = !vence && altAbandonaNaDerrota;
                LugarRanqueado[] lugares =
                    [Humano("principal", principal), Humano("alt", alt, abandonou: sai), Humano("c", Nivel(700, 20)), Humano("d", Nivel(700, 20))];
                var movimentos = vence ? Aplicar(lugares, vencedor: 0, 6, 4)
                    : sai ? Aplicar(lugares, vencedor: null, 0, 0)
                    : Aplicar(lugares, vencedor: 1, 0, 6);
                principal = Mov(movimentos, 0).Depois;
                alt = Mov(movimentos, 1).Depois;
            }
            return principal;
        }

        var comProcuracao = Jornada(altAbandonaNaDerrota: true);
        var perdendoDe6x0 = Jornada(altAbandonaNaDerrota: false);

        Assert.True(comProcuracao.Pdz <= perdendoDe6x0.Pdz, $"procuração {comProcuracao.Pdz} > jogando até o 0x6 {perdendoDe6x0.Pdz}");
        Assert.True(comProcuracao.Pdz <= 700, $"o principal subiu pra {comProcuracao.Pdz} com 50% e abandono nas derrotas");
        Assert.Equal("3ª", comProcuracao.Rotulo.Rotulo);
    }

    // ── Partida com IA não conta ──────────────────────────────────────────────────────────

    [Fact]
    public void Partida_com_IA_nao_mexe_no_rating_de_ninguem()
    {
        // 3 humanos + 1 IA, e o time com a IA vence: ninguém anda, nem a contagem de jogos.
        LugarRanqueado[] tresEUmaIA =
            [Humano("a", Nivel(600, 20)), LugarRanqueado.DaIA, Humano("c", Nivel(600, 20)), Humano("d", Nivel(600, 20))];
        var movimentos = Aplicar(tresEUmaIA, vencedor: 0, 6, 0);

        Assert.Null(movimentos[1]);
        foreach (int humano in new[] { 0, 2, 3 })
        {
            Assert.Equal(0, Mov(movimentos, humano).Delta);
            Assert.Equal(20, Mov(movimentos, humano).Depois.Jogos);
            Assert.Equal(MotivoDoMovimento.PartidaComIA, Mov(movimentos, humano).Motivo);
        }

        // Dupla humana contra a IA (o coop do sofá, online): também não.
        LugarRanqueado[] contraAIA = [Humano("a", Nivel(600, 20)), Humano("b", Nivel(600, 20)), LugarRanqueado.DaIA, LugarRanqueado.DaIA];
        var coop = Aplicar(contraAIA, vencedor: 0, 6, 0);
        Assert.Equal(0, Mov(coop, 0).Delta);
        Assert.Equal(0, Mov(coop, 1).Delta);
        Assert.Null(coop[2]);
        Assert.Null(coop[3]);
    }

    [Fact]
    public void Abandonar_uma_partida_com_IA_nao_custa_nada()
    {
        // Nunca foi ranqueada: sair do treino contra a IA não é abandono de ranqueada.
        LugarRanqueado[] lugares =
            [Humano("a", Nivel(600, 20), abandonou: true), Humano("b", Nivel(600, 20)), LugarRanqueado.DaIA, LugarRanqueado.DaIA];

        var movimentos = Aplicar(lugares, vencedor: null, 0, 3);

        Assert.Equal(0, Mov(movimentos, 0).Delta);
        Assert.Equal(20, Mov(movimentos, 0).Depois.Jogos);
        Assert.Equal(MotivoDoMovimento.PartidaComIA, Mov(movimentos, 0).Motivo);
    }

    // ── O que mais não move o número ──────────────────────────────────────────────────────

    [Fact]
    public void Partida_sem_resultado_nao_move()
    {
        // Sem vencedor e sem ninguém que abandonou (a sessão caiu pra todos): não há o que medir.
        var movimentos = Aplicar(Quatro(600, 600, 600, 600), vencedor: null, 2, 1);

        Assert.Equal([0, 0, 0, 0], Deltas(movimentos));
        Assert.All(movimentos, m => Assert.Equal(MotivoDoMovimento.SemResultado, m?.Motivo));
        Assert.All(movimentos, m => Assert.Equal(20, m?.Depois.Jogos));
    }

    [Fact]
    public void Mesmo_jogador_em_dois_lugares_nao_move()
    {
        // Padelizou (PadelimetroService.IdsDosJogadores): o mesmo jogador dos dois lados não pode
        // mover o próprio número duas vezes — dado torto não conta.
        LugarRanqueado[] lugares =
            [Humano("a", Nivel(600, 20)), Humano("b", Nivel(600, 20)), Humano("a", Nivel(600, 20)), Humano("d", Nivel(600, 20))];

        var movimentos = Aplicar(lugares, vencedor: 0, 6, 0);

        Assert.Equal([0, 0, 0, 0], Deltas(movimentos));
        Assert.All(movimentos, m => Assert.Equal(MotivoDoMovimento.JogadorRepetido, m?.Motivo));
    }

    [Fact]
    public void Partida_torta_e_recusada_na_porta()
    {
        LugarRanqueado[] tres = [Humano("a", Nivel(600, 20)), Humano("b", Nivel(600, 20)), Humano("c", Nivel(600, 20))];
        Assert.Throws<ArgumentException>(() => Aplicar(tres, vencedor: 0, 6, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Aplicar(Quatro(600, 600, 600, 600), vencedor: 2, 6, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Aplicar(Quatro(600, 600, 600, 600), vencedor: 0, 6, -1));
    }

    // ── Da partida do Core pro ranking ────────────────────────────────────────────────────

    private static void Game(Placar placar, int time)
    {
        for (int i = 0; i < 4; i++) placar.PontoPara(time);
    }

    [Fact]
    public void Do_placar_le_os_games_do_set_encerrado_e_nao_o_zero_a_zero_de_depois()
    {
        // Armadilha: com a partida acabada, Placar.Games volta a [0, 0] — quem lesse dali passaria
        // fator 1,0 pra toda partida. O 6x2 tem que chegar como 6x2.
        var placar = new Placar();
        for (int i = 0; i < 2; i++) { Game(placar, 0); Game(placar, 1); }
        for (int i = 0; i < 4; i++) Game(placar, 0);
        Assert.True(placar.Acabou);
        Assert.Equal([0, 0], placar.Games);

        var partida = PartidaRanqueada.DoPlacar(Quatro(600, 600, 600, 600), placar);

        Assert.Equal(0, partida.TimeVencedor);
        Assert.Equal(6, partida.GamesDoTime0);
        Assert.Equal(2, partida.GamesDoTime1);
        Assert.Equal([14, 14, -14, -14], Deltas(Ranqueamento.Aplicar(partida))); // 20 × 1,4 × 0,5
    }

    [Fact]
    public void Do_placar_soma_os_games_de_todos_os_sets_e_o_set_em_andamento()
    {
        // Melhor de 3: 6-4, 3-6 e 2-1 no terceiro quando alguém caiu → 11x11, sem vencedor.
        var placar = new Placar(setsParaVencer: 2);
        for (int i = 0; i < 4; i++) { Game(placar, 0); Game(placar, 1); }
        Game(placar, 0); Game(placar, 0);                      // 6-4
        for (int i = 0; i < 3; i++) { Game(placar, 0); Game(placar, 1); }
        Game(placar, 1); Game(placar, 1); Game(placar, 1);     // 3-6
        Game(placar, 0); Game(placar, 1); Game(placar, 0);     // 2-1
        Assert.False(placar.Acabou);

        var partida = PartidaRanqueada.DoPlacar(Quatro(600, 600, 600, 600), placar);

        Assert.Null(partida.TimeVencedor);
        Assert.Equal(11, partida.GamesDoTime0);
        Assert.Equal(11, partida.GamesDoTime1);
    }

    // Joga um set inteiro até g0 x g1 (6-x com x ≤ 4, 7-5, ou 7-6 no tie-break): os games do
    // perdedor vão intercalados na frente, pra ninguém fechar o set antes da hora.
    private static void JogarSet(Placar placar, int g0, int g1)
    {
        int vencedor = g0 > g1 ? 0 : 1;
        int gamesDoVencedor = Math.Max(g0, g1), gamesDoPerdedor = Math.Min(g0, g1);
        for (int i = 0; i < gamesDoPerdedor; i++) { Game(placar, vencedor); Game(placar, 1 - vencedor); }
        if (gamesDoVencedor == 7 && gamesDoPerdedor == 6)
            for (int i = 0; i < 7; i++) placar.PontoPara(vencedor); // tie-break 7-0
        else
            for (int i = gamesDoPerdedor; i < gamesDoVencedor; i++) Game(placar, vencedor);
        Assert.Equal([g0, g1], placar.SetsAnteriores[^1].Games);
    }

    private static int[] DeltasDaMelhorDe3(params (int G0, int G1)[] sets)
    {
        var placar = new Placar(setsParaVencer: 2);
        foreach (var (g0, g1) in sets) JogarSet(placar, g0, g1);
        Assert.True(placar.Acabou);
        return Deltas(Ranqueamento.Aplicar(PartidaRanqueada.DoPlacar(Quatro(600, 600, 600, 600), placar)));
    }

    [Fact]
    public void Vencer_no_detalhe_em_melhor_de_3_nunca_vale_mais_que_um_7x6()
    {
        // Achado da revisão: 7-6 0-6 7-6 soma 14x18 — quem VENCEU tem menos games — e o fator de
        // |diferença| lia isso como 1,4, o de um 6x2: quem perdeu tendo mais games era cobrado como
        // quem levou um passeio. 4 × 600, K 20.
        var placar = new Placar(setsParaVencer: 2);
        JogarSet(placar, 7, 6);
        JogarSet(placar, 0, 6);
        JogarSet(placar, 7, 6);
        var partida = PartidaRanqueada.DoPlacar(Quatro(600, 600, 600, 600), placar);
        Assert.Equal(0, partida.TimeVencedor);
        Assert.Equal(14, partida.GamesDoTime0);
        Assert.Equal(18, partida.GamesDoTime1);
        Assert.Equal(3, partida.SetsJogados);

        int seteASeis = Deltas(Aplicar(Quatro(600, 600, 600, 600), vencedor: 0, 7, 6))[0]; // 20 × 1,1 × 0,5 = 11
        var deltas = Deltas(Ranqueamento.Aplicar(partida));
        Assert.True(deltas[0] <= seteASeis, $"7-6 0-6 7-6 moveu {deltas[0]}, mais que um 7x6 ({seteASeis})");
        Assert.Equal([10, 10, -10, -10], deltas); // margem do vencedor negativa → fator 1,0

        // Montada à mão, sem a divisão por sets, a mesma conta: o vencedor com menos games não
        // ganha fator de passeio.
        Assert.Equal([10, 10, -10, -10], Deltas(Aplicar(Quatro(600, 600, 600, 600), vencedor: 0, 14, 18)));
    }

    [Fact]
    public void Em_melhor_de_3_o_fator_e_o_de_um_set_com_a_margem_media_do_vencedor()
    {
        // 4 × 600, K 20: delta = 20 × fator × 0,5 = 10 × fator. Somar os games sem dividir pelos
        // sets inflava tudo: dois 6-3 viravam 12x6, o fator de um 6x0.
        Assert.Equal(16, DeltasDaMelhorDe3((6, 0), (6, 0))[0]);          // margem 6   → 1,6 (= um 6x0)
        Assert.Equal(13, DeltasDaMelhorDe3((6, 3), (6, 3))[0]);          // margem 3   → 1,3 (a soma dava 1,6)
        Assert.Equal(12, DeltasDaMelhorDe3((6, 4), (6, 4))[0]);          // margem 2   → 1,2 (= um 6x4)
        Assert.Equal(11, DeltasDaMelhorDe3((7, 6), (7, 6))[0]);          // margem 1   → 1,1 (= um 7x6)
        Assert.Equal(12, DeltasDaMelhorDe3((6, 0), (6, 7), (7, 6))[0]);  // 19x13: 6/3 → 1,2 (a soma dava 1,6)
        Assert.Equal(10, DeltasDaMelhorDe3((6, 4), (4, 6), (7, 6))[0]);  // 17x16: 1/3 → 10,33 → 10
        Assert.Equal(-12, DeltasDaMelhorDe3((0, 6), (6, 4), (4, 6))[0]); // perdeu 10x16: 6/3 → 1,2

        // Set único é o padrão, e partida sem set nenhum é dado torto.
        Assert.Equal(1, new PartidaRanqueada(Quatro(600, 600, 600, 600), 0, 6, 2).SetsJogados);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Ranqueamento.Aplicar(new PartidaRanqueada(Quatro(600, 600, 600, 600), 0, 6, 2, SetsJogados: 0)));
    }
}
