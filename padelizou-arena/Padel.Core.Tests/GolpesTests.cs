namespace Padel.Core.Tests;

/// <summary>
/// O corpo no golpe (mão, drive x revés, contato ao lado do corpo) e os golpes que faltavam: chiquita, contrapared,
/// remate por 3 e por 4. Tudo o que depende de parede é conferido pelos EVENTOS da Bola simulada (Parede, Saiu) —
/// nenhuma altura de parede é escrita aqui, porque as paredes mudam de desenho (vidro escalonado, grade, portas).
/// </summary>
public class GolpesTests
{
    private const float Passo = 1f / 120f;

    private static List<EventoDaBola> Simular(float x, float y, float z, Velocidade v, float segundos = 4)
    {
        var b = new Bola();
        b.Posicionar(x, y, z);
        b.Lancar(v);
        var eventos = new List<EventoDaBola>();
        for (float t = 0; t < segundos && b.EmJogo && !b.Parada; t += Passo) b.Avancar(Passo, eventos);
        return eventos;
    }

    private static float Rapidez(Velocidade v) => MathF.Sqrt(v.Vx * v.Vx + v.Vy * v.Vy + v.Vz * v.Vz);

    private static Bola BolaEm(float x, float y, float z)
    {
        var b = new Bola();
        b.Posicionar(x, y, z);
        return b;
    }

    private static Golpe Exigir(Golpe? golpe, string contexto)
    {
        if (golpe is Golpe g) return g;
        Assert.Fail($"o solucionador não achou {contexto}");
        return default;
    }

    private static string Descrever(List<EventoDaBola> eventos) =>
        string.Join(" | ", eventos.Take(6).Select(e => $"{e.Tipo}({e.X:F1}; {e.Y:F1}; {e.Z:F2}) lado {e.Lado}"));

    // ───────────── Mão e lado do golpe ─────────────

    [Theory]
    // Time 0 joga no lado +1: a direita de quem olha pra rede é +x.
    [InlineData(0, true, +0.6f, LadoDoGolpe.Drive)]
    [InlineData(0, true, -0.6f, LadoDoGolpe.Reves)]
    [InlineData(0, false, +0.6f, LadoDoGolpe.Reves)]
    [InlineData(0, false, -0.6f, LadoDoGolpe.Drive)]
    // Time 1 joga no lado -1: a direita dele é -x.
    [InlineData(1, true, -0.6f, LadoDoGolpe.Drive)]
    [InlineData(1, true, +0.6f, LadoDoGolpe.Reves)]
    [InlineData(1, false, -0.6f, LadoDoGolpe.Reves)]
    [InlineData(1, false, +0.6f, LadoDoGolpe.Drive)]
    public void Drive_e_a_bola_do_lado_da_raquete_no_referencial_de_quem_bate(int time, bool destro, float dxNoMundo, LadoDoGolpe esperado)
    {
        var j = new Jogador(time, 0, "teste", humano: false, velocidade: 5) { Destro = destro };
        Assert.Equal(esperado, j.LadoDoGolpePara(BolaEm(j.X + dxNoMundo, j.Y, 1.0f)));
    }

    [Fact]
    public void O_jogador_nasce_destro()
    {
        Assert.True(new Jogador(0, 0, "teste", humano: true, velocidade: 6.4f).Destro);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(1, false)]
    public void O_ponto_ideal_de_contato_fica_ao_lado_do_corpo_no_alcance_confortavel(int time, bool destro)
    {
        var j = new Jogador(time, 0, "teste", humano: true, velocidade: 6.4f) { Destro = destro };
        foreach (var lado in new[] { LadoDoGolpe.Drive, LadoDoGolpe.Reves })
        {
            var (x, y) = j.PontoDeContato(lado);
            Assert.Equal(Jogador.DistanciaIdealDoContato, j.DistanciaAte(x, y), 3);
            var bola = BolaEm(x, y, 1.0f);
            Assert.Equal(lado, j.LadoDoGolpePara(bola));
            Assert.True(j.AlcancaConfortavelmente(bola), $"o ponto ideal de {lado} devia estar no alcance confortável");
            Assert.True(j.PodeBaterAgora(bola), $"no ponto ideal de {lado} o golpe sai na hora");
        }
        // No ponto ideal do drive, com a bola na altura da cintura e lenta, o golpe é o mais confortável que existe.
        var (xd, yd) = j.PontoDeContato(LadoDoGolpe.Drive);
        Assert.Equal(0, j.DificuldadeDoGolpe(BolaEm(xd, yd, 1.0f)), 3);
    }

    // ───────────── Contato: bola no corpo, revés, revés alto ─────────────

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void Bola_no_corpo_e_mais_dificil_que_drive_ao_lado(int time, bool destro)
    {
        var j = new Jogador(time, 0, "teste", humano: false, velocidade: 5) { Destro = destro };
        var (xd, yd) = j.PontoDeContato(LadoDoGolpe.Drive);
        float drive = j.DificuldadeDoGolpe(BolaEm(xd, yd, 1.0f));
        // Bola a 5 cm da linha do corpo, 40 cm à frente (rumo à rede): em cima do jogador.
        float noCorpo = j.DificuldadeDoGolpe(BolaEm(j.X + 0.05f * j.Lado, j.Y - 0.4f * j.Lado, 1.0f));
        Assert.True(noCorpo > drive + 0.2f, $"bola no corpo ({noCorpo:F2}) devia ser bem pior que drive ao lado ({drive:F2})");
        // A 0,25 m da linha do corpo ainda é "no corpo", mas menos.
        float quaseNoCorpo = j.DificuldadeDoGolpe(BolaEm(j.X + 0.25f * j.Lado * j.LadoDaRaquete, j.Y, 1.0f));
        Assert.True(quaseNoCorpo > drive && quaseNoCorpo < noCorpo, $"a 0,25 m: {quaseNoCorpo:F2} (drive {drive:F2}, no corpo {noCorpo:F2})");
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(1, false)]
    public void Reves_e_um_pouco_mais_dificil_que_drive_e_reves_alto_bem_mais(int time, bool destro)
    {
        var j = new Jogador(time, 0, "teste", humano: false, velocidade: 5) { Destro = destro };
        var (xd, yd) = j.PontoDeContato(LadoDoGolpe.Drive);
        var (xr, yr) = j.PontoDeContato(LadoDoGolpe.Reves);
        float drive = j.DificuldadeDoGolpe(BolaEm(xd, yd, 1.0f));
        float reves = j.DificuldadeDoGolpe(BolaEm(xr, yr, 1.0f));
        float driveAlto = j.DificuldadeDoGolpe(BolaEm(xd, yd, 2.2f));
        float revesAlto = j.DificuldadeDoGolpe(BolaEm(xr, yr, 2.2f));
        Assert.True(reves > drive, $"revés ({reves:F2}) devia ser mais difícil que drive ({drive:F2})");
        Assert.True(reves - drive < 0.25f, $"revés só um pouco mais difícil: {reves:F2} x {drive:F2}");
        Assert.True(revesAlto > reves + 0.2f, $"revés alto ({revesAlto:F2}) devia ser bem mais difícil que revés ({reves:F2})");
        Assert.True(revesAlto > driveAlto + 0.3f, $"bandeja de revés ({revesAlto:F2}) x de drive ({driveAlto:F2})");
    }

    // ───────────── Onde pôr o corpo (XParaBater) ─────────────

    [Theory]
    [InlineData(0, true)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(1, false)]
    public void O_corpo_vai_pro_lado_da_bola_e_com_a_bola_na_linha_dele_escolhe_o_drive(int time, bool destro)
    {
        foreach (bool bolaAlta in new[] { false, true })
        {
            var j = new Jogador(time, 0, "teste", humano: false, velocidade: 5) { Destro = destro, X = 1.0f };
            float xDaBola = j.X;   // a bola vem na linha do corpo: drive e revés ficam à mesma distância
            float x = j.XParaBater(xDaBola, bolaAlta);
            Assert.Equal(Jogador.DistanciaIdealDoContato, MathF.Abs(x - xDaBola), 3);
            j.X = x;
            Assert.Equal(LadoDoGolpe.Drive, j.LadoDoGolpePara(xDaBola));
            Assert.True(MathF.Abs(j.LateralDe(xDaBola)) >= Jogador.DistanciaDoCorpo, "a bola devia ficar fora do corpo");
        }
    }

    [Fact]
    public void Na_bola_baixa_o_corpo_vai_pro_lado_mais_perto_e_na_alta_anda_mais_pra_bater_de_drive()
    {
        // Destro no lado +1 (drive = bola à direita, +x), parado em x = 0, bola passando em x = -0,5:
        // de drive o corpo iria a -1,1 (1,1 m); de revés, a 0,1 (0,1 m).
        var j = new Jogador(0, 0, "teste", humano: false, velocidade: 5) { X = 0 };
        Assert.Equal(0.1f, j.XParaBater(-0.5f, bolaAlta: false), 3);    // baixa: revés, que é logo ali
        Assert.Equal(-1.1f, j.XParaBater(-0.5f, bolaAlta: true), 3);    // alta: revés alto é o mais difícil — anda 1 m a mais
    }

    [Theory]
    [InlineData(0, -4.6f, LadoDoGolpe.Reves)]   // lado +1, destro: o drive pediria o corpo em -5,2
    [InlineData(0, 4.6f, LadoDoGolpe.Drive)]
    [InlineData(1, 4.6f, LadoDoGolpe.Reves)]    // lado -1, destro: a direita dele é -x; o drive pediria o corpo em 5,2
    [InlineData(1, -4.6f, LadoDoGolpe.Drive)]
    public void Junto_a_parede_lateral_o_corpo_fica_na_quadra_e_bate_do_lado_que_cabe(int time, float xDaBola, LadoDoGolpe esperado)
    {
        foreach (bool bolaAlta in new[] { false, true })
        {
            var j = new Jogador(time, 0, "teste", humano: false, velocidade: 5) { X = 0 };
            float x = j.XParaBater(xDaBola, bolaAlta);
            Assert.InRange(MathF.Abs(x), 0, 4.7f);
            j.X = x;
            Assert.Equal(esperado, j.LadoDoGolpePara(xDaBola));
        }
    }

    /// <summary>
    /// A IA põe o corpo ao lado da bola quando dá tempo: bola vindo reto no corpo de um jogador difícil parado no fundo,
    /// ~1 s até ele. O erro de leitura do perfil é maior com a bola longe e afina enquanto ela chega — o jogador bom
    /// termina o passo lateral no lugar certo. Sem o posicionamento (corpo em cima do x lido da bola) mais da metade dos
    /// contatos sai no corpo; com a leitura congelada no instante do plano, um terço (medido em 25/09: 11 de 30).
    /// </summary>
    [Fact]
    public void Com_tempo_a_IA_poe_o_corpo_ao_lado_da_bola_e_nao_em_cima()
    {
        var laterais = new List<float>();
        for (uint semente = 1; semente <= 30; semente++)
        {
            var partida = new Partida(new OpcoesDaPartida { Humanos = OpcoesDaPartida.NinguemHumano, Semente = semente, Dificuldade = Dificuldade.Dificil });
            partida.Sacar(Entrada.Vazia);
            var casa = partida.Jogadores[0];
            var (fundo, rede) = (partida.Jogadores[2], partida.Jogadores[3]);
            fundo.X = -1.5f; fundo.Y = -7.5f; rede.X = 2.5f; rede.Y = -3.5f;
            foreach (var j in partida.Jogadores) { j.Vx = 0; j.Vy = 0; j.Cooldown = 0; }
            // A casa bate de (−1,5; 5) pro chão em (−1,5; −5,5): quica e sobe reto no corpo de quem está no fundo.
            var golpe = new Golpe(-1.5f, -5.5f, 0.9f, TipoDeGolpe.Normal, Efeito: new Efeito(1200, 0));
            partida.Arbitro.RegistrarGolpe(0);
            partida.Bola.Posicionar(-1.5f, 5f, 1.0f);
            partida.Bola.Lancar(GolpesEspeciais.VelocidadeDe(golpe, -1.5f, 5f, 1.0f));
            foreach (var ia in partida.IAs) ia.AoGolpear(partida, golpe, casa);

            float? lateral = null;
            partida.Evento += ev =>
            {
                if (ev.Tipo == TipoDeEventoDaPartida.Golpe && ev.Jogador is Jogador j && j.Time == 1 && lateral is null)
                    lateral = MathF.Abs(j.LateralDe(partida.Bola.X));
            };
            for (int i = 0; i < 360 && lateral is null && partida.Estado == EstadoDaPartida.Rally; i++) partida.Avancar(Passo);
            Assert.True(lateral is not null, $"semente {semente}: a IA não bateu na bola");
            laterais.Add(lateral ?? 0);
        }
        int noCorpo = laterais.Count(l => l < Jogador.DistanciaDoCorpo);
        laterais.Sort();
        float mediana = laterais[laterais.Count / 2];
        string todas = string.Join(" ", laterais.Select(l => l.ToString("F2")));
        Assert.True(noCorpo <= 3, $"bola no corpo em {noCorpo} de 30 contatos: {todas}");
        Assert.True(mediana >= 0.45f, $"a bola devia passar ao lado do corpo: mediana da lateral {mediana:F2} m ({todas})");
    }

    // ───────────── Chiquita ─────────────

    [Theory]
    [InlineData(1.0f, 8.0f, 0.7f)]
    [InlineData(-2.0f, 8.5f, 0.5f)]
    [InlineData(2.5f, 7.0f, 0.9f)]
    [InlineData(-1.0f, -8.0f, 0.7f)]   // do outro lado da quadra
    public void A_chiquita_cai_curta_passa_baixa_sai_lenta_e_com_topspin(float x, float y, float z)
    {
        int ladoDoAlvo = -Quadra.LadoDe(y);
        var golpe = Exigir(GolpesEspeciais.Chiquita(x, y, z, ladoDoAlvo, alvoX: 0), $"chiquita de ({x}; {y}; {z})");
        Assert.Equal(TipoDeGolpe.Chiquita, golpe.Tipo);
        Assert.True(golpe.Efeito.TopspinRpm > 0, "chiquita sai com topspin");

        var v = GolpesEspeciais.VelocidadeDe(golpe, x, y, z);
        var eventos = Simular(x, y, z, v);
        int q = eventos.FindIndex(e => e.Tipo == TipoDeEventoDaBola.Quique);
        Assert.True(q >= 0, "a chiquita não quicou");
        var antes = eventos.Take(q).ToList();
        var cruzou = Assert.Single(antes);   // só cruzar a rede: nada de rede, nada de parede
        Assert.Equal(TipoDeEventoDaBola.CruzouRede, cruzou.Tipo);
        Assert.True(cruzou.Z <= Quadra.AlturaDaRede + 0.5f, $"a chiquita devia passar baixa: cruzou a {cruzou.Z:F2} m");
        var quique = eventos[q];
        Assert.Equal(ladoDoAlvo, quique.Lado);
        Assert.True(MathF.Abs(quique.Y) <= 3.5f, $"a chiquita devia cair nos pés de quem está na rede: quicou a {MathF.Abs(quique.Y):F2} m da rede");
        var normal = Golpes.Calcular(x, y, z, 0, ladoDoAlvo * 6.8f, 0.8f, efeito: new Efeito(1200, 0));
        Assert.True(Rapidez(v) < Rapidez(normal), $"a chiquita ({Rapidez(v):F1} m/s) devia sair mais lenta que o golpe de fundo ({Rapidez(normal):F1} m/s)");
    }

    // ───────────── Contrapared ─────────────

    [Theory]
    [InlineData(1.0f, 9.4f, 0.8f)]
    [InlineData(-2.5f, 9.6f, 0.5f)]
    [InlineData(3.0f, 9.2f, 1.3f)]
    [InlineData(-1.0f, -9.5f, 0.7f)]   // do outro lado da quadra
    public void A_contrapared_perto_do_proprio_vidro_bate_nele_cruza_a_rede_e_quica_do_outro_lado_sem_falta(float x, float y, float z)
    {
        int lado = Quadra.LadoDe(y);
        int time = lado > 0 ? 0 : 1;
        var golpe = Exigir(GolpesEspeciais.Contrapared(x, y, z, -lado, alvoX: 0), $"contrapared de ({x}; {y}; {z})");
        Assert.Equal(TipoDeGolpe.Contrapared, golpe.Tipo);

        var eventos = Simular(x, y, z, GolpesEspeciais.VelocidadeDe(golpe, x, y, z));
        Assert.True(eventos.Count > 0, "a bola não fez nada");
        var primeiro = eventos[0];
        Assert.True(primeiro.Tipo == TipoDeEventoDaBola.Parede && primeiro.Parede == QualParede.Fundo && primeiro.Lado == lado,
            $"a primeira coisa devia ser a própria parede de fundo: {Descrever(eventos)}");

        var arbitro = new Arbitro();
        arbitro.RegistrarGolpe(time);
        int q = eventos.FindIndex(e => e.Tipo == TipoDeEventoDaBola.Quique);
        Assert.True(q >= 0, "a contrapared não quicou");
        for (int i = 0; i < q; i++) Assert.Null(arbitro.Processar(eventos[i]));   // própria parede e cruzar a rede: segue o jogo
        Assert.Contains(eventos.Take(q), e => e.Tipo == TipoDeEventoDaBola.CruzouRede && e.Para == -lado);
        Assert.Equal(-lado, eventos[q].Lado);
        Assert.Null(arbitro.Processar(eventos[q]));
        Assert.True(arbitro.Cruzou && arbitro.QuicouNoReceptor, "o árbitro devia ver a bola cruzar e quicar do outro lado");
    }

    // ───────────── Remate por 4 e por 3 ─────────────

    [Theory]
    [InlineData(0.6f, 2.5f, 2.6f)]    // a posição ideal: jogador a 2,5 m da rede, bola ao lado dele a 2,6 m
    [InlineData(-1.9f, 2.5f, 2.6f)]
    [InlineData(3.1f, 2.0f, 2.5f)]
    [InlineData(-0.6f, -2.5f, 2.6f)]  // do outro lado da quadra
    public void O_smash_por_4_quica_do_outro_lado_sai_por_cima_do_fundo_e_o_ponto_e_de_quem_bateu(float x, float y, float z)
    {
        int lado = Quadra.LadoDe(y);
        int time = lado > 0 ? 0 : 1;
        var golpe = Exigir(GolpesEspeciais.SmashPor4(x, y, z, -lado, alvoX: 0), $"por 4 de ({x}; {y}; {z})");
        Assert.Equal(TipoDeGolpe.SmashPor4, golpe.Tipo);
        var v = GolpesEspeciais.VelocidadeDe(golpe, x, y, z);
        Assert.InRange(Rapidez(v), 20f, 40f);   // remate de verdade: 70 a 145 km/h, não canhão

        var eventos = Simular(x, y, z, v);
        int q = eventos.FindIndex(e => e.Tipo == TipoDeEventoDaBola.Quique);
        Assert.True(q >= 0, $"não quicou: {Descrever(eventos)}");
        Assert.All(eventos.Take(q), e => Assert.Equal(TipoDeEventoDaBola.CruzouRede, e.Tipo));
        Assert.Equal(-lado, eventos[q].Lado);
        Assert.True(q + 1 < eventos.Count, $"depois do quique a bola devia sair: {Descrever(eventos)}");
        var saiu = eventos[q + 1];
        Assert.Equal(TipoDeEventoDaBola.Saiu, saiu.Tipo);
        Assert.True(MathF.Abs(saiu.Y) >= Quadra.MeioComprimento && Quadra.LadoDe(saiu.Y) == -lado, $"devia sair pelo fundo do outro lado: {Descrever(eventos)}");
        Assert.True(MathF.Abs(saiu.X) < Quadra.MeiaLargura, "saiu pela lateral, não pelo fundo");

        var arbitro = new Arbitro();
        arbitro.RegistrarGolpe(time);
        Decisao? decisao = null;
        foreach (var e in eventos) { decisao = arbitro.Processar(e); if (decisao is not null) break; }
        Assert.Equal(Decisao.Ponto(time, Motivo.Fora), decisao);
    }

    [Theory]
    [InlineData(0f, 8f, 1.2f)]     // do fundo, bola na cintura
    [InlineData(0f, 4.5f, 2.6f)]   // alta, mas longe demais da rede pra descer a bola a tempo
    public void Sem_solucao_de_por_4_o_solucionador_devolve_nulo(float x, float y, float z)
    {
        Assert.Null(GolpesEspeciais.SmashPor4(x, y, z, -Quadra.LadoDe(y), alvoX: 0));
    }

    [Theory]
    [InlineData(1.5f, 1.5f, 2.6f, -1)]
    [InlineData(-1.5f, 1.5f, 2.6f, 1)]
    [InlineData(-1.5f, -1.5f, 2.6f, 1)]    // do outro lado da quadra
    public void O_smash_por_3_quica_do_outro_lado_e_sai_pela_lateral_pedida(float x, float y, float z, int ladoDaSaida)
    {
        int lado = Quadra.LadoDe(y);
        int time = lado > 0 ? 0 : 1;
        var golpe = Exigir(GolpesEspeciais.SmashPor3(x, y, z, -lado, ladoDaSaida), $"por 3 de ({x}; {y}; {z}) pra lateral {ladoDaSaida}");
        Assert.Equal(TipoDeGolpe.SmashPor3, golpe.Tipo);
        var v = GolpesEspeciais.VelocidadeDe(golpe, x, y, z);
        Assert.InRange(Rapidez(v), 20f, 40f);

        var eventos = Simular(x, y, z, v);
        int q = eventos.FindIndex(e => e.Tipo == TipoDeEventoDaBola.Quique);
        Assert.True(q >= 0, $"não quicou: {Descrever(eventos)}");
        Assert.All(eventos.Take(q), e => Assert.Equal(TipoDeEventoDaBola.CruzouRede, e.Tipo));
        Assert.Equal(-lado, eventos[q].Lado);
        Assert.True(q + 1 < eventos.Count, $"depois do quique a bola devia sair: {Descrever(eventos)}");
        var saiu = eventos[q + 1];
        Assert.Equal(TipoDeEventoDaBola.Saiu, saiu.Tipo);
        Assert.True(saiu.X * ladoDaSaida >= Quadra.MeiaLargura, $"devia sair pela lateral {ladoDaSaida}: {Descrever(eventos)}");

        var arbitro = new Arbitro();
        arbitro.RegistrarGolpe(time);
        Decisao? decisao = null;
        foreach (var e in eventos) { decisao = arbitro.Processar(e); if (decisao is not null) break; }
        Assert.Equal(Decisao.Ponto(time, Motivo.Fora), decisao);
    }

    // ───────────── Mapeamento do humano (via Partida) ─────────────

    private static readonly Velocidade Parada = new(0, 0, 0, 0);

    /// <summary>
    /// Monta um rally em que o rival acabou de bater e a bola vem pro humano (jogador 0, lado +1, destro): o humano
    /// fica parado em (jx, jy) e aperta o botão (ação ou lob) no tempo certo — 0,12 s antes de a bola entrar no alcance
    /// confortável —, com a direção (no referencial dele) e segurando a ação se pedido. Devolve o evento Golpe do humano.
    /// </summary>
    private static EventoDaPartida? GolpeDoHumano(float jx, float jy, float bx, float by, float bz, Velocidade v,
        float dx, float dy, bool lob = false, bool segurar = false, bool jaQuicou = false, ModoDeGolpe modo = ModoDeGolpe.Manual) =>
        CenaDoHumano(jx, jy, bx, by, bz, v, dx, dy, lob, segurar, jaQuicou, modo).Golpe;

    /// <summary>A mesma cena do <see cref="GolpeDoHumano"/>, devolvendo também a partida (parada logo depois do golpe) pra seguir simulando.</summary>
    private static (Partida Partida, EventoDaPartida? Golpe) CenaDoHumano(float jx, float jy, float bx, float by, float bz, Velocidade v,
        float dx, float dy, bool lob = false, bool segurar = false, bool jaQuicou = false, ModoDeGolpe modo = ModoDeGolpe.Manual)
    {
        var partida = new Partida(new OpcoesDaPartida { Semente = 21, Humanos = [true, false, false, false], ModoDeGolpe = modo });
        var vazia = Entrada.Vazia;
        partida.Avancar(Passo, [new Entrada(0, 0, true, true), vazia, vazia, vazia]);   // o humano saca: vira rally
        Assert.Equal(EstadoDaPartida.Rally, partida.Estado);
        var humano = partida.Jogadores[0];
        Assert.Equal(1, humano.Lado);

        partida.Arbitro.RegistrarGolpe(1);   // a cena: o rival acabou de bater
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
            // A direção também move o jogador: só entra perto do contato, pra não mudar a cena.
            var entrada = !apertou ? vazia
                : modo == ModoDeGolpe.Automatico ? new Entrada(dx, dy, false, segurar || lob)
                : new Entrada(dx, dy, AcaoPressionada: apertar && !lob, AcaoSegurada: (apertar && !lob) || segurar, LobPressionada: apertar && lob);
            partida.Avancar(Passo, [entrada, vazia, vazia, vazia]);
        }
        return (partida, golpe);
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

    // Bola alta: cai do alto bem no ponto de drive de um destro no lado +1 (0,6 m à direita = +x) e entra no alcance a 2,7 m.
    private static EventoDaPartida? BolaAlta(float jy, float dx, float dy, bool segurar = false, float lateral = 0.6f, ModoDeGolpe modo = ModoDeGolpe.Manual) =>
        CenaBolaAlta(jy, dx, dy, segurar, lateral, modo).Golpe;

    private static (Partida Partida, EventoDaPartida? Golpe) CenaBolaAlta(float jy, float dx, float dy, bool segurar = false, float lateral = 0.6f, ModoDeGolpe modo = ModoDeGolpe.Manual) =>
        CenaDoHumano(2.0f, jy, 2.0f + lateral, jy, 3.3f, Parada, dx, dy, segurar: segurar, modo: modo);

    // Bola baixa no fundo: vem da rede e passa ao lado (drive) do humano a ~1 m de altura.
    private static EventoDaPartida? BolaBaixaNoFundo(float dx, float dy, bool lob, bool segurar = false, ModoDeGolpe modo = ModoDeGolpe.Manual) =>
        GolpeDoHumano(2.0f, 8.0f, 2.6f, 5.5f, 1.0f, new Velocidade(0, 7, 1.5f, 0), dx, dy, lob: lob, segurar: segurar, jaQuicou: true, modo: modo);

    // Bola que passou do humano e está junto ao próprio vidro de fundo (0,6 m atrás dele), entrando no alcance pela lateral.
    private static EventoDaPartida? BolaAtrasJuntoAoVidro(float dx, float dy) =>
        GolpeDoHumano(2.0f, 8.8f, 3.4f, 9.4f, 0.9f, new Velocidade(-4, 0, 0.5f, 0), dx, dy, jaQuicou: true);

    private static TipoDeGolpe? Tipo(EventoDaPartida? e) => e?.Golpe;

    [Fact]
    public void Humano_bola_alta_frente_e_smash_sem_frente_e_bandeja_lado_forte_e_vibora()
    {
        Assert.Equal(TipoDeGolpe.Smash, Tipo(BolaAlta(2.5f, 0, -1)));
        Assert.Equal(TipoDeGolpe.Bandeja, Tipo(BolaAlta(2.5f, 0, 0)));
        Assert.Equal(TipoDeGolpe.Bandeja, Tipo(BolaAlta(2.5f, 0, 1)));
        Assert.Equal(TipoDeGolpe.Vibora, Tipo(BolaAlta(2.5f, 1, 0)));
        Assert.Equal(TipoDeGolpe.Vibora, Tipo(BolaAlta(2.5f, -1, 0)));
    }

    [Fact]
    public void Humano_bola_alta_frente_segurando_a_acao_e_por_4_e_com_lado_forte_e_por_3()
    {
        Assert.Equal(TipoDeGolpe.SmashPor4, Tipo(BolaAlta(2.5f, 0, -1, segurar: true)));
        Assert.Equal(TipoDeGolpe.SmashPor3, Tipo(BolaAlta(1.5f, -1, -1, segurar: true)));
    }

    [Fact]
    public void Humano_pedindo_por_4_de_onde_nao_da_sai_smash_comum()
    {
        // A 4,5 m da rede o remate não desce a tempo de quicar perto e sair pelo fundo: o solucionador devolve nulo.
        Assert.Equal(TipoDeGolpe.Smash, Tipo(BolaAlta(4.5f, 0, -1, segurar: true)));
    }

    [Fact]
    public void Humano_lob_com_frente_e_chiquita_e_sem_frente_e_lob()
    {
        Assert.Equal(TipoDeGolpe.Chiquita, Tipo(BolaBaixaNoFundo(0, -1, lob: true)));
        Assert.Equal(TipoDeGolpe.Lob, Tipo(BolaBaixaNoFundo(0, 0, lob: true)));
        Assert.Equal(TipoDeGolpe.Lob, Tipo(BolaBaixaNoFundo(0, 1, lob: true)));
        // Sem o lob, a mesma bola é golpe de fundo.
        Assert.Equal(TipoDeGolpe.Ataque, Tipo(BolaBaixaNoFundo(0, -1, lob: false)));
        Assert.Equal(TipoDeGolpe.Normal, Tipo(BolaBaixaNoFundo(0, 0, lob: false)));
    }

    [Fact]
    public void Humano_bola_atras_junto_ao_proprio_vidro_com_tras_e_contrapared()
    {
        Assert.Equal(TipoDeGolpe.Contrapared, Tipo(BolaAtrasJuntoAoVidro(0, 1)));
        Assert.NotEqual(TipoDeGolpe.Contrapared, Tipo(BolaAtrasJuntoAoVidro(0, 0)));
        // Bola na frente dele com "trás" não é contrapared: é defesa.
        Assert.Equal(TipoDeGolpe.Defesa, Tipo(BolaBaixaNoFundo(0, 1, lob: false)));
    }

    [Fact]
    public void No_modo_automatico_segurar_a_acao_e_o_lob_e_na_bola_alta_com_frente_e_o_por_4()
    {
        Assert.Equal(TipoDeGolpe.Chiquita, Tipo(BolaBaixaNoFundo(0, -1, lob: false, segurar: true, modo: ModoDeGolpe.Automatico)));
        Assert.Equal(TipoDeGolpe.Lob, Tipo(BolaBaixaNoFundo(0, 0, lob: false, segurar: true, modo: ModoDeGolpe.Automatico)));
        Assert.Equal(TipoDeGolpe.SmashPor4, Tipo(BolaAlta(2.5f, 0, -1, segurar: true, modo: ModoDeGolpe.Automatico)));
    }

    [Fact]
    public void O_evento_do_golpe_do_humano_informa_drive_ou_reves()
    {
        var drive = BolaAlta(2.5f, 0, 0, lateral: 0.6f);    // à direita do destro no lado +1
        var reves = BolaAlta(2.5f, 0, 0, lateral: -0.6f);   // à esquerda
        Assert.Equal(LadoDoGolpe.Drive, drive?.Lado);
        Assert.Equal(LadoDoGolpe.Reves, reves?.Lado);
    }

    // ───────────── Entrada é fronteira de confiança ─────────────

    /// <summary>
    /// O host simula com as entradas que os clientes mandam (DECISOES.md, D2): direção NaN ou infinita — cliente com
    /// defeito ou malicioso — não pode derrubar a simulação nem corromper o jogador. Componente não finita vale 0.
    /// </summary>
    [Theory]
    [InlineData(float.NaN, -1f, true, ModoDeGolpe.Manual, TipoDeGolpe.SmashPor4)]
    [InlineData(float.NaN, -1f, true, ModoDeGolpe.Automatico, TipoDeGolpe.SmashPor4)]
    [InlineData(float.PositiveInfinity, 0f, false, ModoDeGolpe.Manual, TipoDeGolpe.Bandeja)]
    [InlineData(0f, float.NegativeInfinity, false, ModoDeGolpe.Automatico, TipoDeGolpe.Bandeja)]
    public void Direcao_nao_finita_na_entrada_vale_zero_e_nao_derruba_a_partida(float dx, float dy, bool segurar, ModoDeGolpe modo, TipoDeGolpe esperado)
    {
        var golpe = BolaAlta(2.5f, dx, dy, segurar: segurar, modo: modo);
        Assert.Equal(esperado, golpe?.Golpe);
        var j = golpe?.Jogador;
        Assert.True(j is not null && float.IsFinite(j.X) && float.IsFinite(j.Y), "o jogador ficou com posição inválida");
    }

    [Fact]
    public void O_saque_do_humano_com_direcao_nao_finita_sai_como_saque_sem_direcao()
    {
        var partida = new Partida(new OpcoesDaPartida { Semente = 21, Humanos = [true, false, false, false] });
        var caixa = partida.CaixaDoSaque;
        var vazia = Entrada.Vazia;
        partida.Avancar(Passo, [new Entrada(float.NaN, float.PositiveInfinity, true, true), vazia, vazia, vazia]);
        Assert.Equal(EstadoDaPartida.Rally, partida.Estado);
        EventoDaBola? quique = null;
        var bola = partida.Bola.Clonar();
        var eventos = new List<EventoDaBola>();
        for (int i = 0; i < 400 && quique is null && bola.EmJogo; i++)
        {
            eventos.Clear();
            bola.Avancar(Passo, eventos);
            foreach (var e in eventos) if (e.Tipo == TipoDeEventoDaBola.Quique) { quique = e; break; }
        }
        Assert.True(quique is EventoDaBola q && caixa.Contem(q.X, q.Y), $"o saque devia cair na caixa: quique {quique}, bola ({partida.Bola.X}; {partida.Bola.Y}; {partida.Bola.Z})");
    }

    // ───────────── Bola que sai da quadra ─────────────

    /// <summary>
    /// Invariante de <c>SimulacaoTests.JogarAteOFim</c>: a cada passo, |x| ≤ 5,01 e |y| ≤ 10,01. O remate por 3 / por 4
    /// tira a bola da quadra de propósito; o que fica parado na tela até o próximo ponto é o ponto de saída, no plano da
    /// parede — não o sub-passo que a física deu além dele.
    /// </summary>
    /// <summary>A defesa não alcança (a cena é a bola saindo, não se dá pra buscar): os jogadores do time ficam sem poder bater até o fim do ponto.</summary>
    private static void SemDefesa(Partida partida, int time)
    {
        foreach (var j in partida.JogadoresDoTime(time)) j.Cooldown = 99;
    }

    private static string SeguirAteOProximoSaque(Partida partida, Action<EventoDaPartida> aoEvento)
    {
        bool novoSaque = false;
        var historia = new List<string>();
        partida.Evento += ev =>
        {
            aoEvento(ev);
            historia.Add(ev.Golpe is TipoDeGolpe g ? $"{ev.Tipo}:{g}" : $"{ev.Tipo}");
            if (ev.Tipo == TipoDeEventoDaPartida.SaquePreparado) novoSaque = true;
        };
        for (int i = 0; i < 600 && !novoSaque; i++)
        {
            partida.Avancar(Passo);
            var b = partida.Bola;
            Assert.True(MathF.Abs(b.X) <= 5.01f && MathF.Abs(b.Y) <= 10.01f, $"bola fora da quadra em ({b.X:F3}; {b.Y:F3}; {b.Z:F2}) — {string.Join(", ", historia)}");
        }
        Assert.True(novoSaque, $"o ponto não acabou: {string.Join(", ", historia)}");
        return string.Join(", ", historia);
    }

    [Theory]
    [InlineData(2.5f, 0f, TipoDeGolpe.SmashPor4)]    // sai por cima do fundo
    [InlineData(1.5f, -1f, TipoDeGolpe.SmashPor3)]   // sai por cima da lateral
    public void Depois_do_remate_que_sai_a_bola_fica_no_ponto_de_saida_ate_o_proximo_ponto(float jy, float dx, TipoDeGolpe remate)
    {
        var (partida, golpe) = CenaBolaAlta(jy, dx, -1, segurar: true);
        Assert.Equal(remate, golpe?.Golpe);
        SemDefesa(partida, 1);
        bool saiu = false;
        EventoDaPartida? ponto = null;
        string historia = SeguirAteOProximoSaque(partida, ev =>
        {
            if (ev.Tipo == TipoDeEventoDaPartida.Saiu) saiu = true;
            if (ev.Tipo == TipoDeEventoDaPartida.Ponto) ponto = ev;
        });
        Assert.True(saiu, $"a bola devia ter saído da quadra: {historia}");
        Assert.Equal(0, ponto?.Time);
        Assert.Equal(Motivo.Fora, ponto?.Motivo);
    }

    [Fact]
    public void O_remate_pra_fora_da_IA_deixa_a_bola_no_ponto_de_saida_ate_o_proximo_ponto()
    {
        int remates = 0;
        for (uint semente = 1; semente <= 20; semente++)
        {
            var partida = new Partida(new OpcoesDaPartida { Humanos = OpcoesDaPartida.NinguemHumano, Semente = semente, Dificuldade = Dificuldade.Dificil });
            partida.Sacar(Entrada.Vazia);
            partida.Arbitro.RegistrarGolpe(0);   // a cena: a casa acabou de bater
            BolaMuitoAltaNaRede(partida);
            partida.Jogadores[2].Vx = 0; partida.Jogadores[2].Vy = 0; partida.Jogadores[2].Cooldown = 0;

            TipoDeGolpe? primeiro = null;
            partida.Evento += ev => { if (ev.Tipo == TipoDeEventoDaPartida.Golpe && primeiro is null) primeiro = ev.Golpe; };
            partida.Avancar(Passo);
            if (primeiro is not (TipoDeGolpe.SmashPor3 or TipoDeGolpe.SmashPor4)) continue;
            remates++;
            SemDefesa(partida, 0);
            EventoDaPartida? ponto = null;
            SeguirAteOProximoSaque(partida, ev => { if (ev.Tipo == TipoDeEventoDaPartida.Ponto) ponto = ev; });
            Assert.Equal(1, ponto?.Time);
            Assert.Equal(Motivo.Fora, ponto?.Motivo);
        }
        Assert.True(remates >= 2, $"a cena devia render remates da IA: {remates} em 20");
    }

    // ───────────── IA ─────────────

    /// <summary>Chama a IA do time 1 muitas vezes na mesma situação e conta os tipos de golpe (o aleatório anda a cada chamada).</summary>
    private static Dictionary<TipoDeGolpe, int> Escolhas(Dificuldade dificuldade, Action<Partida> montar, int vezes)
    {
        var partida = new Partida(new OpcoesDaPartida { Humanos = OpcoesDaPartida.NinguemHumano, Semente = 77, Dificuldade = dificuldade });
        montar(partida);
        var jogador = partida.Jogadores[2];
        var contagem = new Dictionary<TipoDeGolpe, int>();
        for (int i = 0; i < vezes; i++)
        {
            var g = partida.IAs[1].EscolherGolpe(jogador, partida.Bola, partida);
            contagem[g.Tipo] = contagem.GetValueOrDefault(g.Tipo) + 1;
            Assert.Equal(jogador.LadoDoGolpePara(partida.Bola), g.Lado);   // até o erro sai de drive ou de revés
        }
        return contagem;
    }

    // Rival 1 (time 1, lado -1, destro) a 2,5 m da rede, com a bola a 2,6 m no ponto de drive dele.
    private static void BolaMuitoAltaNaRede(Partida p)
    {
        var j = p.Jogadores[2];
        j.X = -1.5f; j.Y = -2.5f;
        var (x, y) = j.PontoDeContato(LadoDoGolpe.Drive);
        p.Bola.Posicionar(x, y, 2.6f);
        p.Bola.Lancar(new Velocidade(0, 0, -2, 0));
    }

    [Fact]
    public void A_IA_remata_por_3_ou_por_4_com_a_bola_muito_alta_na_rede_mais_quanto_mais_dificil()
    {
        const int vezes = 300;
        float Taxa(Dificuldade d)
        {
            var c = Escolhas(d, BolaMuitoAltaNaRede, vezes);
            return (c.GetValueOrDefault(TipoDeGolpe.SmashPor3) + c.GetValueOrDefault(TipoDeGolpe.SmashPor4)) / (float)vezes;
        }
        float facil = Taxa(Dificuldade.Facil), medio = Taxa(Dificuldade.Medio), dificil = Taxa(Dificuldade.Dificil);
        Assert.InRange(facil, 0.01f, 0.10f);
        Assert.InRange(medio, 0.08f, 0.24f);
        Assert.InRange(dificil, 0.20f, 0.40f);
        Assert.True(facil < medio && medio < dificil, $"fácil {facil:P0}, médio {medio:P0}, difícil {dificil:P0}");
    }

    [Fact]
    public void A_IA_nao_remata_pra_fora_do_fundo_da_quadra()
    {
        var c = Escolhas(Dificuldade.Dificil, p =>
        {
            var j = p.Jogadores[2];
            j.X = -1.5f; j.Y = -8f;
            var (x, y) = j.PontoDeContato(LadoDoGolpe.Drive);
            p.Bola.Posicionar(x, y, 2.4f);
            p.Bola.Lancar(new Velocidade(0, 0, -2, 0));
        }, 100);
        Assert.Equal(0, c.GetValueOrDefault(TipoDeGolpe.SmashPor3) + c.GetValueOrDefault(TipoDeGolpe.SmashPor4));
    }

    private static void FundoComBolaBaixa(Partida p, float yDosAdversarios)
    {
        var j = p.Jogadores[2];
        j.X = -2f; j.Y = -8f;
        var (x, y) = j.PontoDeContato(LadoDoGolpe.Drive);
        p.Bola.Posicionar(x, y, 0.7f);
        p.Bola.Lancar(new Velocidade(0, -3, 1, 0));
        p.Jogadores[0].X = 2.5f; p.Jogadores[0].Y = yDosAdversarios;
        p.Jogadores[1].X = -2.5f; p.Jogadores[1].Y = yDosAdversarios;
    }

    [Fact]
    public void A_IA_joga_chiquita_do_fundo_quando_os_dois_adversarios_estao_na_rede()
    {
        var naRede = Escolhas(Dificuldade.Medio, p => FundoComBolaBaixa(p, 3.0f), 100);
        Assert.True(naRede.GetValueOrDefault(TipoDeGolpe.Chiquita) >= 20, $"chiquitas com os dois na rede: {naRede.GetValueOrDefault(TipoDeGolpe.Chiquita)} em 100");
        var noFundo = Escolhas(Dificuldade.Medio, p => FundoComBolaBaixa(p, 8.0f), 100);
        Assert.Equal(0, noFundo.GetValueOrDefault(TipoDeGolpe.Chiquita));
    }

    [Fact]
    public void A_IA_so_usa_contrapared_como_ultimo_recurso()
    {
        // A bola passou do jogador e morre junto ao próprio vidro: rasteira, devagar, indo pra parede — depois dela não sobra bola.
        var ultimoRecurso = Escolhas(Dificuldade.Medio, p =>
        {
            var j = p.Jogadores[2];
            j.X = -1.5f; j.Y = -8.8f;
            p.Bola.Posicionar(-1.0f, -9.6f, 0.45f);
            p.Bola.Lancar(new Velocidade(0, -1.5f, -0.5f, 0));
        }, 50);
        Assert.True(ultimoRecurso.GetValueOrDefault(TipoDeGolpe.Contrapared) >= 30, $"contraparedes no último recurso: {ultimoRecurso.GetValueOrDefault(TipoDeGolpe.Contrapared)} em 50");

        // A mesma posição, mas a bola vem da rede, na frente dele: nunca é contrapared.
        var bolaNaFrente = Escolhas(Dificuldade.Medio, p =>
        {
            var j = p.Jogadores[2];
            j.X = -1.5f; j.Y = -8.8f;
            var (x, y) = j.PontoDeContato(LadoDoGolpe.Drive);
            p.Bola.Posicionar(x, y + 0.4f, 0.9f);
            p.Bola.Lancar(new Velocidade(0, -6, 1, 0));
        }, 50);
        Assert.Equal(0, bolaNaFrente.GetValueOrDefault(TipoDeGolpe.Contrapared));

        // Atrás dele e junto ao vidro, mas indo firme pro vidro na altura da cintura: volta do vidro batível, então não é o
        // último recurso — espera a bola voltar. Rally de verdade: a casa bateu e a bola já quicou do lado dele.
        var voltaBativel = Escolhas(Dificuldade.Medio, p =>
        {
            var j = p.Jogadores[2];
            j.X = -1.5f; j.Y = -8.8f;
            p.Arbitro.RegistrarGolpe(0);
            p.Arbitro.Processar(new EventoDaBola(TipoDeEventoDaBola.Quique, -1.0f, -7.0f, 0, -1));
            p.Bola.Posicionar(-1.0f, -9.4f, 1.0f);
            p.Bola.Lancar(new Velocidade(0, -5, 0.5f, 0));
            // A cena é a da contrapared (senão o teste não prova nada): bola atrás, junto ao vidro, e o solucionador acha solução.
            Assert.True(j.BolaAtrasJuntoAoVidro(p.Bola), "a bola devia estar atrás dele, junto ao vidro");
            Assert.NotNull(GolpesEspeciais.Contrapared(p.Bola.X, p.Bola.Y, p.Bola.Z, ladoDoAlvo: 1, alvoX: 0));
        }, 50);
        Assert.Equal(0, voltaBativel.GetValueOrDefault(TipoDeGolpe.Contrapared));
    }

    [Fact]
    public void Numa_partida_entre_IAs_todo_golpe_sai_com_o_lado_do_corpo_certo()
    {
        var partida = new Partida(new OpcoesDaPartida { Humanos = OpcoesDaPartida.NinguemHumano, Semente = 5, Dificuldade = Dificuldade.Dificil });
        partida.Jogadores[1].Destro = false;   // um canhoto na casa
        partida.Jogadores[3].Destro = false;   // e um nos rivais
        int golpes = 0, reveses = 0;
        partida.Evento += ev =>
        {
            if (ev.Tipo != TipoDeEventoDaPartida.Golpe || ev.Golpe == TipoDeGolpe.Saque || ev.Jogador is not Jogador j) return;
            golpes++;
            var esperado = j.LadoDoGolpePara(partida.Bola);
            Assert.Equal(esperado, ev.Lado);
            if (esperado == LadoDoGolpe.Reves) reveses++;
        };
        for (float t = 0; t < 240 && !partida.Acabou; t += Passo) partida.Avancar(Passo);
        Assert.True(golpes > 50, $"poucos golpes: {golpes}");
        Assert.True(reveses > golpes / 10, $"quase ninguém bateu de revés: {reveses} em {golpes}");
    }
}
