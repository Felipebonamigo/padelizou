namespace Padel.Core.Tests;

/// <summary>
/// A saída de parede como intenção (CRONOGRAMA, M1; REALISMO, "golpes que faltam"): a bola funda e rápida que vai quicar e
/// bater no vidro de fundo (ou no lateral, a parede dupla) não se bate colado no vidro, com ela fugindo do corpo — espera-se
/// ela sair da parede e vir de volta. Medido em 06/11 (6 partidas Médio x Médio): a IA batia 132 bolas depois do quique e
/// antes do vidro, a 8,4 m da rede, baixas (0,57 m) e rápidas (22 m/s), e 114 delas sairiam do vidro de volta a 9 m ou menos.
/// Quem espera: a IA Média, a Difícil e o Parceiro (<see cref="PerfilDeIA.EsperaAParede"/>); o Fácil, não — quem começa
/// bate antes do vidro e paga. O humano assistido (Casual/Padrão) é levado ao ponto da saída, porque o alvo do movimento
/// assistido é o plano da IA do time, e o modo Automático também espera.
/// </summary>
public class SaidaDeParedeTests
{
    private const float Passo = 1f / 120f;

    private static Partida Cena(Dificuldade dificuldade, bool[]? humanos = null, ModoDeGolpe modo = ModoDeGolpe.Manual, Assistencia assistencia = Assistencia.Pro)
    {
        var partida = new Partida(new OpcoesDaPartida { Humanos = humanos ?? OpcoesDaPartida.NinguemHumano, Semente = 5, Dificuldade = dificuldade, ModoDeGolpe = modo, Assistencia = assistencia });
        partida.Sacar(Entrada.Vazia);
        return partida;
    }

    private static void Posicionar(Jogador j, float x, float y)
    {
        j.X = x; j.Y = y; j.Vx = 0; j.Vy = 0; j.Cooldown = 0;
    }

    /// <summary>O jogador bate o golpe da posição dele: a bola sai, o árbitro registra e as duas IAs ficam sabendo — como no Golpear da partida.</summary>
    private static void Bater(Partida partida, Jogador quem, Golpe golpe, float z = 1.0f)
    {
        partida.Arbitro.RegistrarGolpe(quem.Time);
        partida.Bola.Posicionar(quem.X, quem.Y, z);
        partida.Bola.Lancar(GolpesEspeciais.VelocidadeDe(golpe, quem.X, quem.Y, z));
        foreach (var ia in partida.IAs) ia.AoGolpear(partida, golpe, quem);
    }

    /// <summary>Bola funda e rápida do time 0 pro fundo do time 1: quica a ~1,3 m do vidro, bate nele e sai de volta.</summary>
    private static Golpe BolaFundaERapida(float alvoX) => new(alvoX, -8.7f, 0.75f, TipoDeGolpe.Ataque, Efeito: new Efeito(1500, 0));
    /// <summary>Bola lenta com slice, batida da rede: quica a 7 m, passa batível a 8,65 m e chega ao vidro a 10 cm do chão — não sai com altura de jogo.</summary>
    private static Golpe BolaFundaELenta(float alvoX) => new(alvoX, -7.0f, 1.0f, TipoDeGolpe.Normal, Efeito: new Efeito(-1200, 0));

    /// <summary>Até onde a bola volta do vidro com altura de jogo (≥ 0,5 m), pelo rastro-fantasma; null se não bate no vidro depois de quicar.</summary>
    private static float? MenorProfundidadeDaSaida(Partida partida, int lado)
    {
        var rastro = RastroFantasma.Prever(partida.Bola, lado, jaQuicou: false);
        if (rastro.PrimeiroDepoisDaParede < 0) return null;
        float menor = float.PositiveInfinity;
        for (int i = rastro.PrimeiroDepoisDaParede; i < rastro.Pontos.Count; i++)
            if (rastro.Pontos[i].Z >= 0.5f) menor = MathF.Min(menor, MathF.Abs(rastro.Pontos[i].Y));
        return menor;
    }

    private sealed record Golpe1(bool DepoisDaParede, float Profundidade, float Z, Jogador Quem);

    /// <summary>Roda até o time 1 bater (ou o ponto acabar): se bateu depois de a bola quicar e bater na parede dele, onde.</summary>
    private static Golpe1? GolpeDoTimeUm(Partida partida)
    {
        bool quicou = false, parede = false;
        Golpe1? golpe = null;
        partida.Evento += ev =>
        {
            switch (ev.Tipo)
            {
                case TipoDeEventoDaPartida.Quique when Quadra.LadoDe(partida.Bola.Y) == -1: quicou = true; break;
                case TipoDeEventoDaPartida.Parede when quicou: parede = true; break;
                case TipoDeEventoDaPartida.Golpe when ev.Time == 1 && golpe is null:
                    golpe = new Golpe1(parede, MathF.Abs(partida.Bola.Y), partida.Bola.Z, ev.Jogador!); break;
            }
        };
        for (int i = 0; i < 480 && golpe is null && partida.Estado == EstadoDaPartida.Rally; i++) partida.Avancar(Passo);
        return golpe;
    }

    [Theory]
    [InlineData(Dificuldade.Medio)]
    [InlineData(Dificuldade.Dificil)]
    public void A_IA_espera_a_saida_de_parede_da_bola_funda_e_rapida(Dificuldade dificuldade)
    {
        var partida = Cena(dificuldade);
        var (a, b) = (partida.Jogadores[2], partida.Jogadores[3]);
        Posicionar(a, -2f, -6.5f);
        Posicionar(b, 2f, -6.5f);
        var casa = partida.Jogadores[0];
        Posicionar(casa, 1.5f, 7.5f);
        Bater(partida, casa, BolaFundaERapida(-1.5f));
        float? volta = MenorProfundidadeDaSaida(partida, -1);
        Assert.True(volta is float v && v <= IA.ProfundidadeMaximaDaSaida, $"a cena precisa de uma bola que sai do vidro de volta a ≤ {IA.ProfundidadeMaximaDaSaida} m (voltou a {volta})");

        // Antes de a bola chegar ao vidro, o plano já é esperar: quem vai nela está marcado como esperando a parede.
        partida.Avancar(Passo);
        var ia = partida.IAs[1];
        Assert.NotNull(ia.Responsavel);
        Assert.True(ia.EsperandoAParede(ia.Responsavel!), "o plano devia ser esperar a saída de parede");

        var golpe = GolpeDoTimeUm(partida);
        Assert.NotNull(golpe);
        Assert.True(golpe!.DepoisDaParede, $"a IA bateu antes do vidro, a {golpe.Profundidade:F2} m da rede e {golpe.Z:F2} m de altura");
        Assert.True(golpe.Profundidade <= IA.ProfundidadeMaximaDaSaida, $"a IA bateu a saída colada no vidro ({golpe.Profundidade:F2} m)");
    }

    [Fact]
    public void A_IA_facil_nao_sabe_esperar_e_bate_antes_do_vidro()
    {
        Assert.False(Perfis.Facil.EsperaAParede);
        Assert.True(Perfis.Medio.EsperaAParede);
        Assert.True(Perfis.Dificil.EsperaAParede);
        Assert.True(Perfis.Parceiro.EsperaAParede);

        var partida = Cena(Dificuldade.Facil);
        Posicionar(partida.Jogadores[2], -2f, -7.5f);
        Posicionar(partida.Jogadores[3], 2f, -7.5f);
        var casa = partida.Jogadores[0];
        Posicionar(casa, 1.5f, 7.5f);
        Bater(partida, casa, BolaFundaERapida(-1.5f));
        var golpe = GolpeDoTimeUm(partida);
        Assert.NotNull(golpe);
        Assert.False(golpe!.DepoisDaParede, $"o Fácil devia bater antes do vidro (bateu a {golpe.Profundidade:F2} m)");
    }

    [Fact]
    public void A_bola_que_morre_no_vidro_e_batida_antes_dele()
    {
        var partida = Cena(Dificuldade.Dificil);
        Posicionar(partida.Jogadores[2], -2f, -6.5f);
        Posicionar(partida.Jogadores[3], 2f, -6.5f);
        var casa = partida.Jogadores[0];
        Posicionar(casa, 1.5f, 3f);
        Bater(partida, casa, BolaFundaELenta(-1.5f));
        float? volta = MenorProfundidadeDaSaida(partida, -1);
        Assert.True(volta is null || volta > IA.ProfundidadeMaximaDaSaida, $"a cena precisa de uma bola que não sai do vidro a mais de 1 m (voltou a {volta})");

        var ia = partida.IAs[1];
        partida.Avancar(Passo);
        Assert.NotNull(ia.Responsavel);
        Assert.False(ia.EsperandoAParede(ia.Responsavel!));
        var golpe = GolpeDoTimeUm(partida);
        Assert.NotNull(golpe);
        Assert.False(golpe!.DepoisDaParede, $"a bola que morre no vidro devia ser batida antes dele (bateu a {golpe.Profundidade:F2} m)");
    }

    /// <summary>A saída de parede dupla: a bola funda no canto bate no vidro de fundo e no lateral; a IA espera as duas.</summary>
    [Fact]
    public void A_IA_espera_a_saida_da_parede_dupla_no_canto()
    {
        var partida = Cena(Dificuldade.Dificil);
        Posicionar(partida.Jogadores[2], -2f, -6.5f);
        Posicionar(partida.Jogadores[3], 2f, -6.5f);
        var casa = partida.Jogadores[0];
        Posicionar(casa, -3f, 7.5f);
        // Cruzada, rumo ao canto: quica a 8,3 m, vidro de fundo e, 0,12 s depois, o lateral a 8,5 m — a saída é das duas.
        Bater(partida, casa, new Golpe(-4.7f, -8.3f, 0.8f, TipoDeGolpe.Ataque, Efeito: new Efeito(1500, 0)));
        int paredes = 0;
        bool quicou = false;
        partida.Evento += ev =>
        {
            if (ev.Tipo == TipoDeEventoDaPartida.Quique && Quadra.LadoDe(partida.Bola.Y) == -1) quicou = true;
            if (ev.Tipo == TipoDeEventoDaPartida.Parede && quicou) paredes++;
        };
        var golpe = GolpeDoTimeUm(partida);
        Assert.NotNull(golpe);
        Assert.True(golpe!.DepoisDaParede, $"a IA bateu antes do vidro, a {golpe.Profundidade:F2} m da rede");
        Assert.True(paredes >= 2, $"a cena precisa da parede dupla (a bola bateu em {paredes} parede(s) antes do golpe)");
    }

    /// <summary>O humano no Casual, com o analógico solto, é levado ao ponto da saída (o alvo do movimento assistido é o plano da IA do time).</summary>
    [Fact]
    public void O_humano_assistido_e_levado_a_saida_de_parede()
    {
        var partida = Cena(Dificuldade.Medio, humanos: [true, false, false, false], assistencia: Assistencia.Casual);
        var humano = partida.Jogadores[0];
        Posicionar(humano, 2f, 6.5f);
        Posicionar(partida.Jogadores[1], -2f, 6.5f);
        var rival = partida.Jogadores[2];
        Posicionar(rival, -1.5f, -7.5f);
        Bater(partida, rival, new Golpe(1.5f, 8.7f, 0.75f, TipoDeGolpe.Ataque, Efeito: new Efeito(1500, 0)));
        float? volta = MenorProfundidadeDaSaida(partida, 1);
        Assert.True(volta is float v && v <= IA.ProfundidadeMaximaDaSaida, $"a cena precisa de uma bola que sai do vidro (voltou a {volta})");

        partida.Avancar(Passo);
        var alvo = partida.IAs[0].AlvoDoCorpo(humano);
        Assert.True(alvo is not null, "a IA do time devia mandar o humano na bola");
        Assert.True(MathF.Abs(alvo!.Value.Y) <= IA.ProfundidadeMaximaDaSaida, $"o alvo do corpo devia ser a saída de parede, não o vidro (y = {alvo.Value.Y:F2})");
        // Com o analógico solto ele vai pra lá e, quando a bola bate no vidro, ainda não passou de 9 m.
        bool parede = false;
        partida.Evento += ev => { if (ev.Tipo == TipoDeEventoDaPartida.Parede) parede = true; };
        for (int i = 0; i < 240 && !parede && partida.Estado == EstadoDaPartida.Rally; i++) partida.Avancar(Passo);
        Assert.True(parede, "a bola devia bater no vidro");
        Assert.True(MathF.Abs(humano.Y) <= IA.ProfundidadeMaximaDaSaida + 0.3f, $"o humano assistido devia estar esperando a saída, não colado no vidro (y = {humano.Y:F2})");
    }

    /// <summary>No modo Automático a raquete bate sozinha (Jogador.PodeBaterAgora) — e, como a IA, espera a saída.</summary>
    [Fact]
    public void No_modo_automatico_o_humano_tambem_espera_a_saida()
    {
        var partida = Cena(Dificuldade.Medio, humanos: [true, false, false, false], modo: ModoDeGolpe.Automatico);
        var humano = partida.Jogadores[0];
        Posicionar(humano, 2f, 6.5f);
        Posicionar(partida.Jogadores[1], -2f, 6.5f);
        var rival = partida.Jogadores[2];
        Posicionar(rival, -1.5f, -7.5f);
        Bater(partida, rival, new Golpe(1.5f, 8.7f, 0.75f, TipoDeGolpe.Ataque, Efeito: new Efeito(1500, 0)));

        bool quicou = false, parede = false;
        (bool depois, float prof)? golpe = null;
        partida.Evento += ev =>
        {
            if (ev.Tipo == TipoDeEventoDaPartida.Quique && Quadra.LadoDe(partida.Bola.Y) == 1) quicou = true;
            if (ev.Tipo == TipoDeEventoDaPartida.Parede && quicou) parede = true;
            if (ev.Tipo == TipoDeEventoDaPartida.Golpe && ev.Jogador == humano && golpe is null) golpe = (parede, MathF.Abs(partida.Bola.Y));
        };
        // O humano corre pro ponto do plano (entrada rumo ao fundo, como o movimento assistido faria) e a raquete decide sozinha.
        for (int i = 0; i < 480 && golpe is null && partida.Estado == EstadoDaPartida.Rally; i++)
        {
            var alvo = partida.IAs[0].AlvoDoCorpo(humano);
            var entrada = Entrada.Vazia;
            if (alvo is (float ax, float ay))
            {
                float dx = ax - humano.X, dy = ay - humano.Y, d = MathF.Sqrt(dx * dx + dy * dy);
                if (d > 0.1f) entrada = new Entrada(Util.Limitar(dx / d, -1, 1), Util.Limitar(dy / d, -1, 1), false, false);
            }
            partida.Avancar(Passo, [entrada, Entrada.Vazia, Entrada.Vazia, Entrada.Vazia]);
        }
        Assert.NotNull(golpe);
        Assert.True(golpe!.Value.depois, $"no Automático o humano bateu antes do vidro, a {golpe.Value.prof:F2} m da rede");
    }
}
