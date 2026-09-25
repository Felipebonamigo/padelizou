namespace Padel.Core.Tests;

/// <summary>A física de verdade: arrasto, efeito, quique com spin, vidro x grade, inércia do jogador e timing do balanço.</summary>
public class RealismoTests
{
    private static List<EventoDaBola> Simular(Bola b, float segundos, float passo = 1f / 120f)
    {
        var eventos = new List<EventoDaBola>();
        for (float t = 0; t < segundos && b.EmJogo && !b.Parada; t += passo) b.Avancar(passo, eventos);
        return eventos;
    }

    private static EventoDaBola PrimeiroQuique(Bola b, float segundos = 4)
    {
        var eventos = Simular(b, segundos);
        return eventos.First(e => e.Tipo == TipoDeEventoDaBola.Quique);
    }

    [Fact]
    public void O_arrasto_e_quadratico_uma_bola_a_30_m_s_perde_mais_de_15_por_cento_em_10_m()
    {
        var b = new Bola();
        b.Posicionar(0, 9, 1.2f);
        b.Lancar(new Velocidade(0, -30, 4.0f, 0));   // sobe um pouco pra não quicar antes dos 10 m
        float rapidezInicial = b.Rapidez;
        var eventos = new List<EventoDaBola>();
        while (b.Y > -1 && b.EmJogo) b.Avancar(1f / 240f, eventos);
        Assert.DoesNotContain(eventos, e => e.Tipo == TipoDeEventoDaBola.Quique);
        float perda = 1 - b.Rapidez / rapidezInicial;
        Assert.InRange(perda, 0.15f, 0.30f);
    }

    [Fact]
    public void Topspin_cai_antes_e_slice_flutua_mais_longe_que_a_bola_sem_efeito()
    {
        Bola Lancar(float topspinRpm)
        {
            var b = new Bola();
            b.Posicionar(0, 8, 1.0f);
            float w = topspinRpm * 2 * MathF.PI / 60f;
            // Indo em −y, o topspin é o eixo (up × d̂) = (+x): ω = (w, 0, 0).
            b.Lancar(new Velocidade(0, -18, 4.5f, 0, w, 0, 0));
            return b;
        }
        float semEfeito = PrimeiroQuique(Lancar(0)).Y;
        float comTopspin = PrimeiroQuique(Lancar(2500)).Y;
        float comSlice = PrimeiroQuique(Lancar(-2500)).Y;
        Assert.True(comTopspin > semEfeito + 0.5f, $"topspin devia cair mais curto: {comTopspin:F2} vs {semEfeito:F2}");
        Assert.True(comSlice < semEfeito - 0.5f, $"slice devia ir mais longe: {comSlice:F2} vs {semEfeito:F2}");
    }

    [Fact]
    public void Sidespin_curva_a_trajetoria()
    {
        var reta = new Bola();
        reta.Posicionar(0, 8, 1.0f);
        reta.Lancar(new Velocidade(0, -18, 4.5f, 0));
        var curva = new Bola();
        curva.Posicionar(0, 8, 1.0f);
        curva.Lancar(new Velocidade(0, -18, 4.5f, 0, 0, 0, 2000 * 2 * MathF.PI / 60f));
        Assert.InRange(PrimeiroQuique(reta).X, -0.05f, 0.05f);
        Assert.True(MathF.Abs(PrimeiroQuique(curva).X) > 0.4f, "sidespin devia desviar a bola de lado");
    }

    [Fact]
    public void No_quique_o_topspin_sai_mais_rapido_e_o_slice_nao_ganha_velocidade()
    {
        float RapidezDepoisDoQuique(float topspinRpm)
        {
            var b = new Bola();
            b.Posicionar(0, 5, 0.5f);
            float w = topspinRpm * 2 * MathF.PI / 60f;
            b.Lancar(new Velocidade(0, -15, -3, 0, w, 0, 0));
            var eventos = new List<EventoDaBola>();
            while (!eventos.Any(e => e.Tipo == TipoDeEventoDaBola.Quique)) b.Avancar(1f / 240f, eventos);
            return b.RapidezHorizontal;
        }
        float flat = RapidezDepoisDoQuique(0);
        float topspin = RapidezDepoisDoQuique(2500);
        float slice = RapidezDepoisDoQuique(-2500);
        // Topspin chega a rolar cedo e sai mais rápido. Slice e flat deslizam o contato inteiro (atrito no limite),
        // então saem parecidos — a diferença do slice de verdade está na bola mais baixa, não no chão mais lento.
        Assert.True(topspin > flat + 0.5f, $"topspin {topspin:F2} devia sair mais rápido que flat {flat:F2}");
        Assert.True(slice < topspin, $"slice {slice:F2} não pode sair mais rápido que topspin {topspin:F2}");
        Assert.True(MathF.Abs(slice - flat) < 0.8f, $"slice {slice:F2} e flat {flat:F2} deviam sair parecidos");
    }

    [Fact]
    public void O_quique_no_chao_respeita_a_regra_da_bola_de_padel()
    {
        // Regra FIP: solta de 2,54 m, a bola quica entre 1,35 e 1,45 m.
        var b = new Bola();
        b.Posicionar(0, 5, 2.54f);
        b.Lancar(new Velocidade(0, 0, 0, 0));
        var eventos = new List<EventoDaBola>();
        while (!eventos.Any(e => e.Tipo == TipoDeEventoDaBola.Quique)) b.Avancar(1f / 480f, eventos);
        float alturaMaxima = 0;
        while (b.Vz > 0) { b.Avancar(1f / 480f, eventos); alturaMaxima = MathF.Max(alturaMaxima, b.Z); }
        Assert.InRange(alturaMaxima, 1.30f, 1.48f);
    }

    [Fact]
    public void O_vidro_devolve_com_forca_e_a_grade_mata_a_bola()
    {
        float RapidezDepoisDaParede(float altura)
        {
            var b = new Bola();
            b.Posicionar(0, -8.5f, altura);
            b.Lancar(new Velocidade(0, -12, 0, 0));
            var eventos = new List<EventoDaBola>();
            while (!eventos.Any(e => e.Tipo == TipoDeEventoDaBola.Parede)) b.Avancar(1f / 240f, eventos);
            var parede = eventos.First(e => e.Tipo == TipoDeEventoDaBola.Parede);
            Assert.Equal(altura > Quadra.AlturaDoVidro ? Superficie.Grade : Superficie.Vidro, parede.Superficie);
            return b.Vy;
        }
        float vidro = RapidezDepoisDaParede(1.5f);
        float grade = RapidezDepoisDaParede(3.5f);
        Assert.True(vidro > 8, $"o vidro devia devolver forte: {vidro:F2}");
        Assert.True(grade < vidro * 0.6f, $"a grade devia matar a bola: {grade:F2} vs vidro {vidro:F2}");
    }

    [Fact]
    public void O_jogador_acelera_e_freia_em_vez_de_teleportar_a_velocidade()
    {
        var j = new Jogador(0, 0, "teste", humano: true, velocidade: 6.4f);
        j.X = 0; j.Y = 6;
        j.Mover(0, -1, 1f / 120f);
        Assert.True(j.Rapidez < 1, $"num passo a velocidade devia ser bem menor que a máxima: {j.Rapidez:F2}");
        for (int i = 0; i < 120; i++) j.Mover(0, -1, 1f / 120f);   // 1 s acelerando
        Assert.InRange(j.Rapidez, 6.3f, 6.4f);
        float yAoSoltar = j.Y;
        int passos = 0;
        while (j.Rapidez > 0.01f && passos++ < 240) j.Mover(0, 0, 1f / 120f);
        float freada = yAoSoltar - j.Y;
        Assert.InRange(freada, 0.8f, 2.0f);   // v²/(2a) = 6,4²/28 ≈ 1,46 m
        Assert.True(j.TempoParaChegar(1) < j.TempoParaChegar(4));
    }

    [Fact]
    public void No_modo_manual_sem_balanco_a_bola_passa_e_com_balanco_no_tempo_certo_o_golpe_sai_bom()
    {
        // Rival saca; o humano recebe. Sem apertar nada, a bola quica duas vezes e o ponto é dos rivais.
        var parado = Receber(apertarEm: null, semente: 11);
        Assert.Equal(1, parado.ultimoPonto);
        Assert.Equal(0, parado.golpesDoHumano);

        // Apertando no instante certo (quando a bola está a ~0,12 s do ponto confortável), o humano bate e o golpe sai com erro baixo.
        var certo = Receber(apertarEm: 0.12f, semente: 11);
        Assert.Equal(1, certo.golpesDoHumano);
        Assert.True(certo.partida.UltimoErroDoHumano < 0.5f, $"erro do golpe no tempo certo: {certo.partida.UltimoErroDoHumano:F2}");

        // Apertando cedo demais, o balanço acaba antes da bola chegar: raquete no ar.
        var cedo = Receber(apertarEm: 0.5f, semente: 11);
        Assert.Equal(1, cedo.partida.Jogadores[0].BalancosNoAr);
    }

    /// <summary>Monta o cenário: rivais sacam pro humano (parado na posição de recepção) e o humano aperta a ação
    /// `apertarEm` segundos antes de a bola entrar no alcance dele (null = nunca). Devolve quem fez o ponto.</summary>
    private static (Partida partida, int ultimoPonto, int golpesDoHumano) Receber(float? apertarEm, uint semente)
    {
        var partida = new Partida(new OpcoesDaPartida { Semente = semente, Dificuldade = Dificuldade.Medio, Humanos = [true, false, false, false] });
        int ultimoPonto = -1;
        partida.Evento += ev => { if (ev.Tipo is TipoDeEventoDaPartida.Ponto or TipoDeEventoDaPartida.Game) ultimoPonto = ev.Time; };
        // Primeiro game: a casa saca (jogador 0 é o humano). Deixa o saque sair sozinho e o ponto correr até o rival sacar.
        const float passo = 1f / 120f;
        var vazia = new[] { Entrada.Vazia, Entrada.Vazia, Entrada.Vazia, Entrada.Vazia };
        int guarda = 0;
        while (!(partida.Estado == EstadoDaPartida.Saque && partida.Sacador.Time == 1) && guarda++ < 120 * 600) partida.Avancar(passo, vazia);
        Assert.True(partida.Sacador.Time == 1, "o rival devia estar sacando");
        // Coloca o humano exatamente na posição em que a bola vai passar, com a bola vindo pra ele: joga um ponto olhando à frente.
        var humano = partida.Jogadores[0];
        int golpesAntes = humano.Golpes;   // os saques do primeiro game contam como golpe
        ultimoPonto = -1;
        bool apertou = false;
        for (int i = 0; i < 120 * 30 && ultimoPonto == -1; i++)
        {
            // Quanto falta pra bola chegar ao ponto confortável do humano parado onde está (recalculado a cada quadro;
            // a previsão inclui o quique que ainda vai acontecer, como um jogador que lê a bola no ar).
            float falta = partida.Estado == EstadoDaPartida.Rally ? TempoAteOAlcance(partida.Bola, humano) : -1;
            bool apertar = apertarEm is float antes && !apertou && falta >= 0 && falta <= antes;
            if (apertar) apertou = true;
            // O humano anda até onde a bola vai ficar batível — pela Entrada, como um jogador de verdade — e para quando ela está perto.
            var direcao = !apertou && (falta < 0 || falta > 0.6f) ? DirecaoAteABola(partida, humano) : (0f, 0f);
            var entrada = new Entrada(direcao.Item1 * humano.Lado, direcao.Item2 * humano.Lado, apertar, false);   // do mundo pro referencial do jogador
            partida.Avancar(passo, new[] { entrada, Entrada.Vazia, Entrada.Vazia, Entrada.Vazia });
        }
        return (partida, ultimoPonto, humano.Golpes - golpesAntes);
    }

    private static float TempoAteOAlcance(Bola bola, Jogador j)
    {
        var clone = bola.Clonar();
        var eventos = new List<EventoDaBola>();
        for (float t = 0; t < 3; t += 1f / 120f)
        {
            clone.Avancar(1f / 120f, eventos);
            if (j.AlcancaConfortavelmente(clone)) return t;
        }
        return -1;
    }

    /// <summary>Direção (no mundo, módulo até 1) até o primeiro ponto em que a bola vai estar batível do lado do humano.</summary>
    private static (float, float) DirecaoAteABola(Partida partida, Jogador humano)
    {
        var clone = partida.Bola.Clonar();
        var eventos = new List<EventoDaBola>();
        for (float t = 0; t < 3; t += 1f / 60f)
        {
            clone.Avancar(1f / 60f, eventos);
            if (Quadra.LadoDe(clone.Y) == humano.Lado && clone.Z is > 0.4f and < 1.3f && clone.Vz < 0 && eventos.Any(e => e.Tipo == TipoDeEventoDaBola.Quique && e.Lado == humano.Lado))
            {
                float dx = clone.X - humano.X, dy = clone.Y - humano.Y;
                float d = MathF.Sqrt(dx * dx + dy * dy);
                if (d < 0.15f) return (0, 0);
                float forca = MathF.Min(1, d / 0.6f);
                return (dx / d * forca, dy / d * forca);
            }
        }
        return (0, 0);
    }
}
