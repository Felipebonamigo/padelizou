namespace Padel.Core.Tests;

/// <summary>
/// O controle de quatro botões da fatia no Unreal (design do padelizou-game, "Controle"): o botão que começa o balanço
/// escolhe a família do golpe — A chapado (segurado: ataque), B lob, X cortado (bandeja na bola alta, slice na baixa),
/// Y ataque (smash na alta, por 3 / por 4 segurado, víbora com o RB; ataque na baixa) — e o analógico só escolhe a
/// direção. O Arena segue no de dois botões (o padrão), onde X e Y não existem.
/// </summary>
public class ControleDeQuatroBotoesTests
{
    private const float Passo = 1f / 120f;
    private static readonly Velocidade Parada = new(0, 0, 0, 0);

    private enum Botao { A, B, X, Y }

    /// <summary>
    /// A cena do GolpesTests.CenaDoHumano: o rival acabou de bater e a bola vem pro humano (jogador 0, lado +1, destro),
    /// que fica parado em (jx, jy) e aperta o botão 0,12 s antes de a bola entrar no alcance confortável. Segurar =
    /// o botão (A ou Y) ainda apertado no contato; rb = o modificador segurado do aperto ao contato.
    /// </summary>
    private static EventoDaPartida? GolpeDoHumano(float jx, float jy, float bx, float by, float bz, Velocidade v, Botao botao,
        float dx, float dy, bool segurar = false, bool rb = false, bool jaQuicou = false, Controle controle = Controle.QuatroBotoes)
    {
        var partida = new Partida(new OpcoesDaPartida { Semente = 21, Humanos = [true, false, false, false], Controle = controle });
        var vazia = Entrada.Vazia;
        partida.Avancar(Passo, [new Entrada(0, 0, true, true), vazia, vazia, vazia]);   // o humano saca (com o A): vira rally
        Assert.Equal(EstadoDaPartida.Rally, partida.Estado);
        var humano = partida.Jogadores[0];

        partida.Arbitro.RegistrarGolpe(1);
        if (jaQuicou) partida.Arbitro.Processar(new EventoDaBola(TipoDeEventoDaBola.Quique, bx, by, 0, 1));
        humano.X = jx; humano.Y = jy; humano.Vx = 0; humano.Vy = 0; humano.Cooldown = 0;
        partida.Bola.Posicionar(bx, by, bz);
        partida.Bola.Lancar(v);

        EventoDaPartida? golpe = null;
        partida.Evento += ev => { if (ev.Tipo == TipoDeEventoDaPartida.Golpe && ev.Jogador == humano && golpe is null) golpe = ev; };
        bool apertou = false;
        for (int i = 0; i < 240 && golpe is null && partida.Estado == EstadoDaPartida.Rally; i++)
        {
            float falta = TempoAteOAlcance(partida.Bola, humano);
            bool apertar = !apertou && falta >= 0 && falta <= Jogador.MomentoIdealDoBalanco;
            if (apertar) apertou = true;
            var entrada = !apertou ? vazia : new Entrada(dx, dy,
                AcaoPressionada: apertar && botao == Botao.A, AcaoSegurada: botao == Botao.A && (apertar || segurar),
                LobPressionada: apertar && botao == Botao.B,
                CortadoPressionado: apertar && botao == Botao.X,
                AtaquePressionado: apertar && botao == Botao.Y, AtaqueSegurado: botao == Botao.Y && (apertar || segurar),
                ModificadorSegurado: rb);
            partida.Avancar(Passo, [entrada, vazia, vazia, vazia]);
        }
        return golpe;
    }

    private static float TempoAteOAlcance(Bola bola, Jogador j)
    {
        var clone = bola.Clonar();
        var eventos = new List<EventoDaBola>();
        for (float t = 0; t < 3; t += Passo)
        {
            if (j.AlcancaConfortavelmente(clone)) return t;
            clone.Avancar(Passo, eventos);
        }
        return -1;
    }

    // As duas bolas do GolpesTests: a alta cai do alto no ponto de drive; a baixa vem da rede a ~1 m de altura, já quicada.
    private static TipoDeGolpe? BolaAlta(float jy, Botao botao, float dx = 0, float dy = 0, bool segurar = false, bool rb = false, Controle controle = Controle.QuatroBotoes) =>
        GolpeDoHumano(2.0f, jy, 2.6f, jy, 3.3f, Parada, botao, dx, dy, segurar, rb, controle: controle)?.Golpe;

    private static TipoDeGolpe? BolaBaixa(Botao botao, float dx = 0, float dy = 0, bool segurar = false, bool rb = false, Controle controle = Controle.QuatroBotoes) =>
        GolpeDoHumano(2.0f, 8.0f, 2.6f, 5.5f, 1.0f, new Velocidade(0, 7, 1.5f, 0), botao, dx, dy, segurar, rb, jaQuicou: true, controle: controle)?.Golpe;

    [Fact]
    public void Na_bola_alta_X_e_bandeja_Y_e_smash_Y_com_RB_e_vibora_e_o_analogico_nao_muda_o_golpe()
    {
        Assert.Equal(TipoDeGolpe.Bandeja, BolaAlta(2.5f, Botao.X));
        Assert.Equal(TipoDeGolpe.Bandeja, BolaAlta(2.5f, Botao.X, dy: -1));     // no de dois botões, frente na bola alta é smash
        Assert.Equal(TipoDeGolpe.Bandeja, BolaAlta(2.5f, Botao.X, dx: 1));      // e lado forte é víbora
        Assert.Equal(TipoDeGolpe.Smash, BolaAlta(2.5f, Botao.Y));
        Assert.Equal(TipoDeGolpe.Smash, BolaAlta(2.5f, Botao.Y, dy: 1));
        Assert.Equal(TipoDeGolpe.Vibora, BolaAlta(2.5f, Botao.Y, rb: true));
        Assert.Equal(TipoDeGolpe.Vibora, BolaAlta(2.5f, Botao.Y, dx: -1, rb: true));
        Assert.Equal(TipoDeGolpe.Normal, BolaAlta(2.5f, Botao.A, dy: -1));
        Assert.Equal(TipoDeGolpe.Lob, BolaAlta(2.5f, Botao.B));
    }

    [Fact]
    public void Na_bola_baixa_A_e_chapado_A_segurado_e_ataque_X_e_slice_Y_e_ataque_B_e_lob()
    {
        Assert.Equal(TipoDeGolpe.Normal, BolaBaixa(Botao.A));
        Assert.Equal(TipoDeGolpe.Normal, BolaBaixa(Botao.A, dy: 1));      // no de dois botões, trás é defesa
        Assert.Equal(TipoDeGolpe.Normal, BolaBaixa(Botao.A, dy: -1));     // e frente é ataque
        Assert.Equal(TipoDeGolpe.Ataque, BolaBaixa(Botao.A, segurar: true));
        Assert.Equal(TipoDeGolpe.Defesa, BolaBaixa(Botao.X));
        Assert.Equal(TipoDeGolpe.Ataque, BolaBaixa(Botao.Y));
        Assert.Equal(TipoDeGolpe.Ataque, BolaBaixa(Botao.Y, rb: true));   // víbora é golpe de bola alta
        Assert.Equal(TipoDeGolpe.Lob, BolaBaixa(Botao.B));
        Assert.Equal(TipoDeGolpe.Lob, BolaBaixa(Botao.B, dy: -1));        // no de dois botões, lob com frente é chiquita
    }

    [Fact]
    public void Y_segurado_na_bola_alta_perto_da_rede_e_por_4_e_com_lado_forte_por_3_e_de_longe_smash()
    {
        Assert.Equal(TipoDeGolpe.SmashPor4, BolaAlta(2.5f, Botao.Y, dy: -1, segurar: true));
        Assert.Equal(TipoDeGolpe.SmashPor3, BolaAlta(1.5f, Botao.Y, dx: -1, dy: -1, segurar: true));
        Assert.Equal(TipoDeGolpe.Smash, BolaAlta(4.5f, Botao.Y, dy: -1, segurar: true));
        // O RB pede víbora: segurar o Y não vira remate.
        Assert.Equal(TipoDeGolpe.Vibora, BolaAlta(2.5f, Botao.Y, dy: -1, segurar: true, rb: true));
    }

    [Fact]
    public void No_controle_de_dois_botoes_X_e_Y_nao_comecam_balanco()
    {
        Assert.Null(BolaAlta(2.5f, Botao.X, controle: Controle.DoisBotoes));
        Assert.Null(BolaAlta(2.5f, Botao.Y, controle: Controle.DoisBotoes));
        // E A e B seguem a tabela de sempre (frente na bola alta é smash).
        Assert.Equal(TipoDeGolpe.Smash, BolaAlta(2.5f, Botao.A, dy: -1, controle: Controle.DoisBotoes));
    }

    [Fact]
    public void O_padrao_e_o_controle_de_dois_botoes_do_Arena() =>
        Assert.Equal(Controle.DoisBotoes, new OpcoesDaPartida().Controle);

    [Fact]
    public void Quatro_botoes_so_vale_no_modo_manual()
    {
        var erro = Assert.Throws<ArgumentException>(() =>
            new Partida(new OpcoesDaPartida { Controle = Controle.QuatroBotoes, ModoDeGolpe = ModoDeGolpe.Automatico }));
        Assert.Contains("Manual", erro.Message);
    }

    [Fact]
    public void Aperto_de_X_ou_Y_num_quadro_sem_passo_vale_no_passo_seguinte_e_so_no_primeiro()
    {
        var passo = new PassoFixo(120);
        const double umQuadro = 1.0 / 120;
        Assert.Equal(0, passo.Quadro(umQuadro / 2, new Entrada(0, 0, false, false, CortadoPressionado: true)));
        Assert.Equal(0, passo.Quadro(umQuadro / 4, new Entrada(0, 0, false, false, AtaquePressionado: true, AtaqueSegurado: true)));
        Assert.Equal(2, passo.Quadro(umQuadro * 1.25, new Entrada(0, 0, false, false, AtaqueSegurado: true, ModificadorSegurado: true)));
        var primeira = passo.EntradaDoPasso(0);
        Assert.True(primeira.CortadoPressionado, "o X do quadro sem passo se perdeu");
        Assert.True(primeira.AtaquePressionado, "o Y do quadro sem passo se perdeu");
        Assert.True(primeira.AtaqueSegurado && primeira.ModificadorSegurado, "o segurado é o do quadro mais novo");
        var segunda = passo.EntradaDoPasso(1);
        Assert.False(segunda.CortadoPressionado || segunda.AtaquePressionado, "só o primeiro passo leva o aperto");
        Assert.True(segunda.AtaqueSegurado && segunda.ModificadorSegurado);
    }

    /// <summary>
    /// O humano simulado também joga de quatro botões (é ele que grava a partida do gabarito e mede o equilíbrio): sai
    /// golpe de toda família do design, e nenhum dos que ficaram fora da fatia (chiquita, contrapared).
    /// </summary>
    [Fact]
    public void O_humano_simulado_joga_de_quatro_botoes_com_os_golpes_do_design()
    {
        var tipos = new Dictionary<TipoDeGolpe, int>();
        for (uint semente = 1; semente <= 24; semente++)   // a víbora é rara (~1 em 40 golpes do humano): 6 sementes não davam nenhuma
        {
            var partida = new Partida(new OpcoesDaPartida
            {
                Semente = semente, Humanos = [true, false, false, false], Controle = Controle.QuatroBotoes, Dificuldade = Dificuldade.Medio,
            });
            var humano = new HumanoSimulado(0, PerfilDeHumano.Avancado, new Aleatorio(semente + 500), controle: Controle.QuatroBotoes);
            partida.Evento += ev =>
            {
                if (ev.Tipo == TipoDeEventoDaPartida.Golpe && ev.Jogador == partida.Jogadores[0] && ev.Golpe is TipoDeGolpe t)
                    tipos[t] = tipos.GetValueOrDefault(t) + 1;
            };
            var entradas = new Entrada[4];
            for (int i = 0; i < 120 * 150 && !partida.Acabou; i++)
            {
                entradas[0] = humano.Decidir(EstadoVisivel.De(partida), Passo);
                partida.Avancar(Passo, entradas);
            }
        }
        string visto = string.Join(", ", tipos.OrderBy(k => k.Key).Select(k => $"{k.Key} {k.Value}"));
        foreach (var t in new[] { TipoDeGolpe.Normal, TipoDeGolpe.Ataque, TipoDeGolpe.Defesa, TipoDeGolpe.Lob, TipoDeGolpe.Bandeja, TipoDeGolpe.Smash, TipoDeGolpe.Vibora })
            Assert.True(tipos.ContainsKey(t), $"o humano simulado nunca deu {t}: {visto}");
        Assert.False(tipos.ContainsKey(TipoDeGolpe.Chiquita) || tipos.ContainsKey(TipoDeGolpe.Contrapared), $"golpe fora da fatia: {visto}");
    }
}
