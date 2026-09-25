namespace Padel.Core.Tests;

/// <summary>
/// O contato acontece no INSTANTE de contato do balanço (<see cref="Jogador.MomentoIdealDoBalanco"/> depois do aperto), e não
/// quando a bola entra no alcance confortável: a qualidade sai de onde a bola está naquele instante em relação ao ponto ideal
/// de contato, ao lado do corpo (<see cref="Jogador.PontoDeContato"/>). Cedo = a bola ainda longe (esticado; fora do alcance,
/// raquete no ar); tarde = a bola já passou do ponto. A IA e o modo Automático batem com a bola no ponto mais perto do
/// contato ideal, não na borda do alcance.
/// </summary>
public class ContatoDoBalancoTests
{
    private const float Passo = 1f / 120f;
    /// <summary>A cena: a bola já quicou do lado do humano e vem da rede na linha do ponto ideal de drive, a 8 m/s, de 3 m à frente.</summary>
    private const float RapidezDaBola = 8f, DistanciaInicial = 3f;

    /// <summary>Z: altura da bola no contato. QuiquesAntes: quiques da bola desde o começo da cena até o contato.</summary>
    private sealed record Contato(float TempoNoBalanco, float DistanciaDoCorpo, float DistanciaDoPontoIdeal, float Frontal, float Erro, float Z = 0, int QuiquesAntes = 0);

    private sealed class Resultado
    {
        public Contato? Contato;
        /// <summary>Depois do instante de contato, ainda balançando, a bola esteve ao alcance (a cena testa a decisão da raquete que já passou).</summary>
        public bool EntrouNoAlcanceDepoisDoInstante;
        /// <summary>A bola passou da linha do corpo com o ponto ainda em jogo.</summary>
        public bool BolaPassou;
        public int BalancosNoAr;
        public int Quiques;
        /// <summary>Por que o ponto acabou, se acabou durante a cena.</summary>
        public Motivo? FimDoPonto;
    }

    private static float DistanciaDoPontoIdeal(Jogador j, Bola b)
    {
        var (x, y) = j.PontoDeContato(j.LadoDoGolpePara(b));
        return Util.Distancia(x, y, b.X, b.Y);
    }

    /// <summary>Segundos até a bola chegar o mais perto possível do ponto ideal de contato do jogador parado onde está (-1 se não chega).</summary>
    private static float TempoAteOPontoIdeal(Bola bola, Jogador j)
    {
        var clone = bola.Clonar();
        var eventos = new List<EventoDaBola>();
        float melhor = float.PositiveInfinity, quando = -1;
        const float passo = 1f / 240f;
        for (float t = 0; t < 2 && clone.EmJogo; t += passo)
        {
            float d = DistanciaDoPontoIdeal(j, clone);
            if (d < melhor) { melhor = d; quando = t; }
            else if (d > melhor + 0.5f) break;
            clone.Avancar(passo, eventos);
        }
        return quando;
    }

    /// <summary>
    /// Humano no jogador (time*2), parado a 1 m do centro, 7 m da rede. O outro time acabou de bater e a bola já quicou do
    /// lado do humano: vem da rede, a 1 m de altura, na linha do ponto ideal de drive dele. O parceiro fica fora da cena.
    /// jaQuicou falso: a bola ainda não quicou (o árbitro não viu quique depois do golpe do outro time). frontal, z e vz: de
    /// onde (metros antes do ponto ideal, altura) e com que velocidade vertical ela vem.
    /// </summary>
    private static (Partida Partida, Jogador Humano) Montar(int time, bool destro, ModoDeGolpe modo = ModoDeGolpe.Manual,
        bool jaQuicou = true, float frontal = DistanciaInicial, float z = 1.0f, float vz = 1.5f)
    {
        bool[] humanos = [false, false, false, false];
        humanos[time * 2] = true;
        var partida = new Partida(new OpcoesDaPartida { Semente = 21, Humanos = humanos, ModoDeGolpe = modo });
        partida.Sacar(Entrada.Vazia);   // vira rally; a cena começa agora
        Assert.Equal(EstadoDaPartida.Rally, partida.Estado);
        var humano = partida.Jogadores[time * 2];
        humano.Destro = destro;
        partida.Jogadores[time * 2 + 1].Cooldown = 99;   // o parceiro não entra na cena
        partida.Arbitro.RegistrarGolpe(1 - time);
        if (jaQuicou) partida.Arbitro.Processar(new EventoDaBola(TipoDeEventoDaBola.Quique, 0, humano.Lado * 5f, 0, humano.Lado));
        humano.X = humano.Lado * 1.0f; humano.Y = humano.Lado * 7f; humano.Vx = 0; humano.Vy = 0; humano.Cooldown = 0;
        var (ix, iy) = humano.PontoDeContato(LadoDoGolpe.Drive);
        partida.Bola.Posicionar(ix, iy - humano.Lado * frontal, z);
        partida.Bola.Lancar(new Velocidade(0, humano.Lado * RapidezDaBola, vz, 0));
        return (partida, humano);
    }

    /// <summary>
    /// Joga a cena. antecedencia: o humano aperta a ação quando faltam esses segundos pra bola chegar ao ponto ideal
    /// (null = não aperta; no modo Automático ninguém aperta).
    /// </summary>
    private static Resultado Jogar(Partida partida, Jogador humano, float? antecedencia)
    {
        var r = new Resultado();
        partida.Evento += ev =>
        {
            if (r.Contato is not null) return;
            if (ev.Tipo == TipoDeEventoDaPartida.Quique) r.Quiques++;
            if (ev.Motivo is Motivo m) r.FimDoPonto = m;
            if (ev.Tipo != TipoDeEventoDaPartida.Golpe || ev.Jogador != humano) return;
            var b = partida.Bola;
            r.Contato = new Contato(humano.TempoNoBalanco, humano.DistanciaAte(b.X, b.Y), DistanciaDoPontoIdeal(humano, b), humano.FrontalDe(b.Y), partida.UltimoErroDoHumano,
                b.Z, r.Quiques);
        };
        var entradas = new Entrada[4];
        int indice = humano.Time * 2 + humano.Indice;
        bool apertou = false;
        for (int i = 0; i < 180 && r.Contato is null && partida.Estado == EstadoDaPartida.Rally; i++)
        {
            bool apertar = false;
            if (antecedencia is float a && !apertou)
            {
                float falta = TempoAteOPontoIdeal(partida.Bola, humano);
                apertar = apertou = falta >= 0 && falta <= a;
            }
            if (humano.Balancando && humano.TempoNoBalanco > Jogador.MomentoIdealDoBalanco && humano.Alcanca(partida.Bola))
                r.EntrouNoAlcanceDepoisDoInstante = true;
            if (humano.FrontalDe(partida.Bola.Y) < 0) r.BolaPassou = true;
            entradas[indice] = new Entrada(0, 0, apertar, apertar);
            partida.Avancar(Passo, entradas);
        }
        r.BalancosNoAr = humano.BalancosNoAr;
        return r;
    }

    private static Contato Exigir(Contato? c, string cena) =>
        c ?? throw new Xunit.Sdk.XunitException($"{cena}: o humano não bateu na bola");

    private static string Descrever(Contato c) =>
        $"TempoNoBalanco {c.TempoNoBalanco:F3} s, {c.DistanciaDoCorpo:F2} m do corpo, {c.DistanciaDoPontoIdeal:F2} m do ponto ideal, frontal {c.Frontal:F2}, erro {c.Erro:F2}";

    // ───────────── Humano, modo manual ─────────────

    [Theory]
    [InlineData(0, true)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(1, false)]
    public void Com_o_aperto_no_tempo_ideal_o_contato_sai_no_instante_do_balanco_ao_lado_do_corpo_e_bom(int time, bool destro)
    {
        var (partida, humano) = Montar(time, destro);
        var c = Exigir(Jogar(partida, humano, Jogador.MomentoIdealDoBalanco).Contato, "aperto no tempo ideal");
        Assert.InRange(c.TempoNoBalanco, Jogador.MomentoIdealDoBalanco - 0.02f, Jogador.MomentoIdealDoBalanco + 0.02f);
        Assert.True(MathF.Abs(c.DistanciaDoCorpo - Jogador.DistanciaIdealDoContato) <= 0.15f, $"o contato devia sair a ~{Jogador.DistanciaIdealDoContato} m do corpo: {Descrever(c)}");
        Assert.True(c.DistanciaDoPontoIdeal <= 0.1f, $"a bola devia estar no ponto ideal: {Descrever(c)}");
        Assert.True(c.Erro < 0.1f, $"no tempo e no lugar certos o golpe sai limpo: {Descrever(c)}");
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(1, false)]
    public void Apertando_um_decimo_de_segundo_cedo_a_bola_ainda_esta_longe_e_o_contato_sai_pior(int time, bool destro)
    {
        var (pp, hp) = Montar(time, destro);
        var perfeito = Exigir(Jogar(pp, hp, Jogador.MomentoIdealDoBalanco).Contato, "aperto no tempo ideal");
        var (partida, humano) = Montar(time, destro);
        var cedo = Jogar(partida, humano, Jogador.MomentoIdealDoBalanco + 0.1f).Contato;
        if (cedo is null) return;   // raquete no ar também é "pior"
        Assert.True(cedo.Frontal > 0.3f, $"cedo, a bola ainda devia estar à frente do corpo: {Descrever(cedo)}");
        Assert.True(cedo.DistanciaDoCorpo > Jogador.DistanciaIdealDoContato + 0.15f, $"cedo, a bola ainda devia estar longe: {Descrever(cedo)}");
        Assert.True(cedo.Erro > perfeito.Erro + 0.15f, $"cedo ({Descrever(cedo)}) devia sair pior que no tempo ({Descrever(perfeito)})");
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(1, false)]
    public void Apertando_um_decimo_de_segundo_tarde_a_bola_ja_passou_do_ponto_e_o_contato_sai_pior(int time, bool destro)
    {
        var (pp, hp) = Montar(time, destro);
        var perfeito = Exigir(Jogar(pp, hp, Jogador.MomentoIdealDoBalanco).Contato, "aperto no tempo ideal");
        var (partida, humano) = Montar(time, destro);
        var tarde = Jogar(partida, humano, Jogador.MomentoIdealDoBalanco - 0.1f).Contato;
        if (tarde is null) return;   // a bola já tinha ido embora: também é "pior"
        Assert.True(tarde.Frontal < 0, $"tarde, a bola já devia ter passado da linha do corpo: {Descrever(tarde)}");
        Assert.True(tarde.Erro > perfeito.Erro + 0.15f, $"tarde ({Descrever(tarde)}) devia sair pior que no tempo ({Descrever(perfeito)})");
    }

    /// <summary>
    /// Com o mesmo desvio de tempo (0,06 s, ~0,45 m nesta bola), tarde sai pior que cedo: a bola passada é a que a raquete
    /// pega atrasada. As duas ficam à mesma distância do corpo — a DificuldadeDoGolpe sozinha, simétrica, não separa.
    /// </summary>
    [Theory]
    [InlineData(0, true)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(1, false)]
    public void Com_o_mesmo_desvio_de_tempo_tarde_sai_pior_que_cedo(int time, bool destro)
    {
        var (pc, hc) = Montar(time, destro);
        var cedo = Exigir(Jogar(pc, hc, Jogador.MomentoIdealDoBalanco + 0.06f).Contato, "0,06 s cedo");
        var (pt, ht) = Montar(time, destro);
        var tarde = Exigir(Jogar(pt, ht, Jogador.MomentoIdealDoBalanco - 0.06f).Contato, "0,06 s tarde");
        Assert.True(cedo.Frontal > 0 && tarde.Frontal < 0, $"a cena devia pôr a bola antes e depois do ponto: cedo {Descrever(cedo)}; tarde {Descrever(tarde)}");
        Assert.True(MathF.Abs(cedo.DistanciaDoCorpo - tarde.DistanciaDoCorpo) < 0.1f, $"as duas deviam estar à mesma distância do corpo: cedo {Descrever(cedo)}; tarde {Descrever(tarde)}");
        Assert.True(tarde.Erro > cedo.Erro + 0.2f, $"tarde ({Descrever(tarde)}) devia sair bem pior que cedo ({Descrever(cedo)})");
    }

    /// <summary>
    /// A raquete passa pelo ponto de contato uma vez só. Apertando 0,35 s antes, no instante de contato a bola ainda está a
    /// ~1,9 m (fora do alcance); ela entra no alcance antes do fim do balanço, mas a raquete já passou: não há golpe, a bola
    /// segue, e o balanço acaba no ar.
    /// </summary>
    [Theory]
    [InlineData(0, true)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(1, false)]
    public void Bola_fora_do_alcance_no_instante_de_contato_passa_mesmo_que_entre_no_alcance_antes_do_fim_do_balanco(int time, bool destro)
    {
        var (partida, humano) = Montar(time, destro);
        var r = Jogar(partida, humano, 0.35f);
        Assert.True(r.EntrouNoAlcanceDepoisDoInstante, "a cena devia pôr a bola ao alcance depois do instante de contato, ainda no balanço");
        Assert.Null(r.Contato);
        Assert.Equal(1, r.BalancosNoAr);
        Assert.True(r.BolaPassou, "a bola devia seguir e passar do jogador");
    }

    // ───────────── Modo Automático ─────────────

    [Theory]
    [InlineData(0, true)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(1, false)]
    public void No_modo_automatico_a_raquete_bate_com_a_bola_no_ponto_ideal_e_nao_na_borda_do_alcance(int time, bool destro)
    {
        var (partida, humano) = Montar(time, destro, ModoDeGolpe.Automatico);
        var c = Exigir(Jogar(partida, humano, antecedencia: null).Contato, "modo Automático");
        Assert.True(MathF.Abs(c.DistanciaDoCorpo - Jogador.DistanciaIdealDoContato) <= 0.15f, $"o contato devia sair a ~{Jogador.DistanciaIdealDoContato} m do corpo: {Descrever(c)}");
        Assert.True(c.DistanciaDoPontoIdeal <= 0.15f, $"a bola devia estar no ponto ideal: {Descrever(c)}");
    }

    /// <summary>
    /// Na partida, e não só na conta do PodeBaterAgora: a bola que JÁ quicou do lado do jogador e desce rumo ao segundo quique
    /// é batida antes de ficar baixa — a partida tem que passar o que o árbitro sabe (<see cref="Arbitro.QuicouNoReceptor"/>).
    /// Com "ainda não quicou" no lugar, a raquete esperava o ponto ideal e a bola quicava de novo: ponto perdido por dois
    /// quiques (IA x IA, 12 partidas do Médio: 152 → 299 pontos por DoisQuiques, medido em 25/09). A cena: a 1,1 m do ponto
    /// ideal, a 0,35 m de altura, descendo a 2,5 m/s — toca o chão uns 0,2 m antes do ponto.
    /// </summary>
    [Theory]
    [InlineData(0, true)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(1, false)]
    public void Na_partida_a_bola_que_ja_quicou_e_batida_antes_do_segundo_quique_e_antes_de_ficar_baixa(int time, bool destro)
    {
        var (partida, humano) = Montar(time, destro, ModoDeGolpe.Automatico, jaQuicou: true, frontal: 1.1f, z: 0.35f, vz: -2.5f);
        var r = Jogar(partida, humano, antecedencia: null);
        var c = Exigir(r.Contato, $"bola descendo pro segundo quique (o ponto acabou por {r.FimDoPonto?.ToString() ?? "nada"})");
        Assert.True(c.QuiquesAntes == 0, $"a bola quicou {c.QuiquesAntes} vez(es) antes do contato: {Descrever(c)}");
        Assert.True(c.Z >= Jogador.AlturaDaBolaBaixa - 0.05f, $"a bola devia ser batida antes de ficar baixa: z = {c.Z:F2} m; {Descrever(c)}");
    }

    /// <summary>
    /// E a bola que ainda NÃO quicou é deixada quicar, mesmo baixa e descendo: o primeiro quique a devolve pra cima, e a
    /// raquete espera ela subir. Trava as duas formas do defeito da revisão de 25/09: a previsão que tratava o chão como saída
    /// do alcance (batia a 1–4 cm do chão, antes do quique) e a partida passando "já quicou" sempre (batia a ~0,3 m, descendo,
    /// antes do quique: voleio baixo de 1,5 % pra 4,4 % dos golpes no Médio). A cena: a 2,2 m do ponto ideal, a 1 m de altura,
    /// descendo a 3 m/s — quica uns 0,3 m antes do ponto e volta a subir.
    /// </summary>
    [Theory]
    [InlineData(0, true)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(1, false)]
    public void Na_partida_a_bola_baixa_que_ainda_nao_quicou_e_deixada_quicar(int time, bool destro)
    {
        var (partida, humano) = Montar(time, destro, ModoDeGolpe.Automatico, jaQuicou: false, frontal: 2.2f, z: 1.0f, vz: -3f);
        var r = Jogar(partida, humano, antecedencia: null);
        var c = Exigir(r.Contato, $"bola que ainda vai quicar (o ponto acabou por {r.FimDoPonto?.ToString() ?? "nada"})");
        Assert.True(c.QuiquesAntes == 1, $"a bola devia quicar uma vez antes do contato, quicou {c.QuiquesAntes}: z = {c.Z:F2} m; {Descrever(c)}");
        Assert.True(c.Z >= Jogador.AlturaDaBolaBaixa - 0.05f, $"depois do quique, a bola devia ser batida já acima da bola baixa: z = {c.Z:F2} m; {Descrever(c)}");
    }

    // ───────────── IA ─────────────

    /// <summary>A bola vem na linha do ponto ideal de drive de um jogador parado (lado +1, destro, IA): x e y no mundo, a 8 m/s rumo a ele.</summary>
    private static (Jogador J, Bola B) BolaNaLinhaDoPontoIdeal(float frontal, float z = 1.0f, float vz = 0)
    {
        var j = new Jogador(0, 0, "teste", humano: false, velocidade: 5) { X = 1.0f, Y = 7f };
        var (ix, iy) = j.PontoDeContato(LadoDoGolpe.Drive);
        var b = new Bola();
        b.Posicionar(ix, iy - frontal, z);
        b.Lancar(new Velocidade(0, RapidezDaBola, vz, 0));
        return (j, b);
    }

    [Fact]
    public void A_IA_espera_a_bola_chegar_ao_ponto_ideal_em_vez_de_bater_na_borda_do_alcance_confortavel()
    {
        // A 0,5 m à frente do ponto ideal a bola já está no alcance confortável (0,78 m do corpo), mas ainda chegando.
        var (j, chegando) = BolaNaLinhaDoPontoIdeal(frontal: 0.5f);
        Assert.True(j.AlcancaConfortavelmente(chegando), "a cena devia pôr a bola no alcance confortável");
        Assert.False(j.PodeBaterAgora(chegando), "com a bola ainda chegando ao ponto ideal, a IA espera");
        // No ponto ideal, e logo depois dele, bate.
        Assert.True(j.PodeBaterAgora(BolaNaLinhaDoPontoIdeal(frontal: 0).B), "no ponto ideal a IA bate");
        Assert.True(j.PodeBaterAgora(BolaNaLinhaDoPontoIdeal(frontal: -0.1f).B), "passou do ponto ideal: bate já");
    }

    /// <summary>
    /// O primeiro quique do nosso lado não tira a bola do alcance: ela volta a subir no mesmo lugar. Trava um defeito da
    /// revisão de 25/09 — a previsão de um passo via a bola abaixo do chão, tratava isso como "saiu do alcance" e mandava
    /// bater a 1–4 cm do chão (IA x IA: de 0,5 % pra 6 % dos golpes; bola na rede com o último golpe rente ao chão, de 12
    /// pra 62 pontos no Médio).
    /// </summary>
    [Fact]
    public void Antes_do_primeiro_quique_a_IA_deixa_a_bola_quicar_em_vez_de_bater_rente_ao_chao()
    {
        // Chegando ao ponto ideal, a um triz do chão e descendo, sem ter quicado: vai quicar e voltar a subir ali mesmo.
        var (j, rente) = BolaNaLinhaDoPontoIdeal(frontal: 0.5f, z: 0.01f, vz: -3f);
        Assert.False(j.PodeBaterAgora(rente, Passo, jaQuicou: false), "o primeiro quique não encerra nada: a IA espera a bola subir");
    }

    [Fact]
    public void Antes_do_segundo_quique_a_IA_bate_na_ultima_chance()
    {
        // A mesma bola, mas já quicou do nosso lado: o próximo chão encerra o ponto.
        var (j, rente) = BolaNaLinhaDoPontoIdeal(frontal: 0.5f, z: 0.01f, vz: -3f);
        Assert.True(j.PodeBaterAgora(rente, Passo, jaQuicou: true), "a bola vai quicar pela segunda vez no passo seguinte: a IA bate agora");
        // Sem saber se já quicou, a IA supõe que sim: esperar um quique que fosse o segundo entrega o ponto.
        Assert.True(j.PodeBaterAgora(rente), "sem a informação do quique, a última chance vale");
    }

    /// <summary>
    /// A última chance vale no alcance inteiro, não só no confortável. Antes do contato no ponto ideal, a IA só batia com a
    /// bola no alcance confortável (0,85 m) ou já indo embora: a bola que vinha pela faixa de fora (0,85–1,1 m), ainda
    /// chegando, quicava a segunda vez sem a raquete sair. Nas partidas, era o saque que ninguém devolvia (ver
    /// <see cref="Nas_partidas_entre_IAs_nenhum_saque_passa_ao_alcance_de_quem_recebe_sem_ser_devolvido"/>).
    /// </summary>
    [Fact]
    public void Antes_do_segundo_quique_a_IA_bate_tambem_a_bola_que_chega_pela_faixa_de_fora_do_alcance()
    {
        // Já quicou (a sobrecarga sem o quique supõe que sim), a 1,0 m do corpo e ainda chegando, rente ao chão e descendo.
        var (j, rente) = BolaNaLinhaDoPontoIdeal(frontal: 0.8f, z: 0.01f, vz: -3f);
        float distancia = j.DistanciaAte(rente.X, rente.Y);
        Assert.True(distancia > j.AlcanceConfortavel && j.Alcanca(rente), $"a cena devia pôr a bola entre o alcance confortável e o total: {distancia:F2} m");
        Assert.True(j.PodeBaterAgora(rente), "a bola vai quicar pela segunda vez no passo seguinte: a IA bate agora, mesmo fora do alcance confortável");
    }

    /// <summary>
    /// Depois do quique, a bola que desce rumo ao segundo chão fica cada vez mais baixa: esperar o ponto ideal com ela
    /// descendo era chegar lá a 1–4 cm do chão, na última chance (revisão de 25/09: 43 pontos do Médio acabando na rede
    /// com o último golpe a menos de 10 cm do chão, contra 12 antes do contato no ponto ideal). A IA bate antes de a bola
    /// ficar baixa (abaixo de 0,3 m, onde a DificuldadeDoGolpe cobra a bola baixa), mesmo ainda longe do ponto ideal.
    /// </summary>
    [Fact]
    public void Depois_do_quique_a_IA_bate_antes_de_a_bola_que_desce_ficar_baixa()
    {
        // Já quicou, desce a 4 m/s e está a 0,32 m: no passo seguinte fica abaixo de 0,3 m, ainda chegando ao ponto ideal.
        var (j, descendo) = BolaNaLinhaDoPontoIdeal(frontal: 0.5f, z: 0.32f, vz: -4f);
        Assert.True(j.PodeBaterAgora(descendo, Passo, jaQuicou: true), "a bola só vai ficar mais baixa até o segundo quique: a IA bate agora");
        // Antes do primeiro quique a mesma bola ainda quica e sobe de novo: a espera segue pelo ponto ideal.
        Assert.False(j.PodeBaterAgora(descendo, Passo, jaQuicou: false), "antes do primeiro quique a IA espera");
        // Alta, descendo, longe de ficar baixa: espera o ponto ideal.
        var (_, alta) = BolaNaLinhaDoPontoIdeal(frontal: 0.5f, z: 1.0f, vz: -4f);
        Assert.False(j.PodeBaterAgora(alta, Passo, jaQuicou: true), "com a bola alta a IA espera o ponto ideal");
    }

    /// <summary>
    /// A bola que acabou de quicar sobe rente ao chão: bater ali é pegar a bola a 1–2 cm do piso (o golpe mais difícil que
    /// há, e na física a bola ainda encosta no chão). A IA espera ela subir acima de 0,3 m — a menos que seja a última chance.
    /// </summary>
    [Fact]
    public void Depois_do_quique_a_IA_espera_a_bola_subir_antes_de_bater()
    {
        // Quicou no ponto ideal e sobe a 4 m/s, a 5 cm do chão: no ponto mais perto do ideal, mas rente ao chão.
        var (j, subindo) = BolaNaLinhaDoPontoIdeal(frontal: 0, z: 0.05f, vz: 4f);
        Assert.False(j.PodeBaterAgora(subindo, Passo, jaQuicou: true), "a bola rente ao chão, subindo: a IA espera ela subir");
        // Já acima da altura da bola baixa, no ponto ideal: bate.
        var (_, acima) = BolaNaLinhaDoPontoIdeal(frontal: 0, z: 0.35f, vz: 3f);
        Assert.True(j.PodeBaterAgora(acima, Passo, jaQuicou: true), "no ponto ideal, acima da bola baixa, a IA bate");
    }

    [Fact]
    public void A_bola_rente_ao_chao_subindo_que_vai_sair_do_alcance_e_batida_mesmo_assim()
    {
        // Subindo rente ao chão, mas na borda do alcance e indo embora pelo lado: é a última chance.
        var j = new Jogador(0, 0, "teste", humano: false, velocidade: 5) { X = 1.0f, Y = 7f };
        var b = new Bola();
        b.Posicionar(j.X + 1.09f, j.Y, 0.05f);
        b.Lancar(new Velocidade(8f, 0, 4f, 0));
        Assert.True(j.PodeBaterAgora(b, Passo, jaQuicou: true), "a bola sai do alcance no passo seguinte: a IA bate agora");
    }

    /// <summary>
    /// As duas travas nas partidas: golpes com a bola a menos de 10 cm do chão (antes ou depois do quique). Antes do contato
    /// no ponto ideal eram 15 de 540 (2,8 %) no Médio; esperando o ponto ideal sem olhar a altura, 61 de 872 (7,0 %).
    /// </summary>
    [Fact]
    public void Nas_partidas_entre_IAs_quase_nenhum_golpe_sai_rente_ao_chao()
    {
        int golpes = 0, rentes = 0;
        foreach (uint semente in new uint[] { 42, 43, 44 })
        {
            var partida = new Partida(new OpcoesDaPartida { Humanos = OpcoesDaPartida.NinguemHumano, Semente = semente, Dificuldade = Dificuldade.Medio });
            partida.Evento += ev =>
            {
                if (ev.Tipo != TipoDeEventoDaPartida.Golpe || ev.Golpe == TipoDeGolpe.Saque) return;
                golpes++;
                if (partida.Bola.Z < 0.1f) rentes++;
            };
            for (float t = 0; t < 240 && !partida.Acabou; t += Passo) partida.Avancar(Passo);
        }
        Assert.True(golpes > 100, $"poucos golpes: {golpes}");
        Assert.True(rentes <= 0.025f * golpes, $"{rentes} de {golpes} golpes a menos de 10 cm do chão");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_IA_nao_espera_a_bola_que_vai_sair_do_alcance_pelo_lado(bool jaQuicou)
    {
        // A bola passa na borda do alcance (1,1 m da IA) e vai embora pelo lado, longe do ponto ideal: última chance.
        var j = new Jogador(0, 0, "teste", humano: false, velocidade: 5) { X = 1.0f, Y = 7f };
        var b = new Bola();
        b.Posicionar(j.X + 1.09f, j.Y, 1.0f);
        b.Lancar(new Velocidade(8f, 0, 0, 0));
        Assert.True(j.PodeBaterAgora(b, Passo, jaQuicou), "a bola sai do alcance no passo seguinte: a IA bate agora");
    }

    /// <summary>
    /// A mesma trava nas partidas: com o primeiro quique tratado como saída do alcance, 186 de 3.201 golpes do Médio (5,8 %)
    /// saíam antes do quique com a bola a menos de 5 cm do chão; antes do contato no ponto ideal eram 11 de 2.628 (0,4 %).
    /// </summary>
    [Fact]
    public void Nas_partidas_entre_IAs_a_IA_nao_bate_rente_ao_chao_a_bola_que_ainda_vai_quicar()
    {
        int golpes = 0, rentesAntesDoQuique = 0;
        foreach (uint semente in new uint[] { 42, 43, 44 })
        {
            var partida = new Partida(new OpcoesDaPartida { Humanos = OpcoesDaPartida.NinguemHumano, Semente = semente, Dificuldade = Dificuldade.Medio });
            bool quicou = false;
            partida.Evento += ev =>
            {
                if (ev.Tipo == TipoDeEventoDaPartida.Quique) quicou = true;
                if (ev.Tipo != TipoDeEventoDaPartida.Golpe) return;
                if (ev.Golpe != TipoDeGolpe.Saque)
                {
                    golpes++;
                    if (!quicou && partida.Bola.Z < 0.05f) rentesAntesDoQuique++;
                }
                quicou = false;
            };
            for (float t = 0; t < 240 && !partida.Acabou; t += Passo) partida.Avancar(Passo);
        }
        Assert.True(golpes > 100, $"poucos golpes: {golpes}");
        Assert.True(rentesAntesDoQuique <= 0.015f * golpes, $"{rentesAntesDoQuique} de {golpes} golpes antes do quique a menos de 5 cm do chão");
    }

    /// <summary>
    /// O saque que passa ao alcance de quem recebe é devolvido. Medido em 25/09, IA x IA, 12 partidas de 1 set (sementes
    /// 42–53), antes do contato no ponto ideal: 15 / 47 / 80 saques (Fácil / Médio / Difícil) quicaram duas vezes depois de
    /// ~0,3 s dentro do alcance de quem recebia (mediana de 31 a 37 passos), sem a raquete sair — 3,5 / 8,3 / 11,9 % dos
    /// pontos. É parte de os ralis terem ficado mais longos: não é equilíbrio a devolver, é defeito corrigido. Agora, zero.
    /// </summary>
    [Fact]
    public void Nas_partidas_entre_IAs_nenhum_saque_passa_ao_alcance_de_quem_recebe_sem_ser_devolvido()
    {
        // Folga pra dentro do alcance: a partida decide depois de a IA mover o corpo no passo (até 6,6 m/s × 1/120 s ≈ 5,5 cm),
        // e aqui a posição é lida antes.
        const float Folga = 0.06f;
        int saques = 0, passaram = 0;
        foreach (uint semente in new uint[] { 42, 43, 44 })
        {
            var partida = new Partida(new OpcoesDaPartida { Humanos = OpcoesDaPartida.NinguemHumano, Semente = semente, Dificuldade = Dificuldade.Medio });
            int recebe = -1, passosAoAlcance = 0;   // recebe: o time que devolve o saque em jogo (-1: nenhum)
            partida.Evento += ev =>
            {
                if (ev.Tipo == TipoDeEventoDaPartida.Golpe)
                {
                    recebe = ev.Golpe == TipoDeGolpe.Saque ? 1 - ev.Time : -1;
                    passosAoAlcance = 0;
                    if (ev.Golpe == TipoDeGolpe.Saque) saques++;
                    return;
                }
                bool fimDoPonto = ev.Tipo is TipoDeEventoDaPartida.Ponto or TipoDeEventoDaPartida.Game or TipoDeEventoDaPartida.Set or TipoDeEventoDaPartida.Partida;
                if (fimDoPonto && recebe >= 0 && ev.Motivo == Motivo.DoisQuiques && passosAoAlcance >= 3) passaram++;
                if (fimDoPonto || ev.Tipo is TipoDeEventoDaPartida.Falta or TipoDeEventoDaPartida.Let) recebe = -1;
            };
            for (float t = 0; t < 240 && !partida.Acabou; t += Passo)
            {
                var bola = partida.Bola;
                if (recebe >= 0 && partida.Arbitro.PodeGolpear(recebe)
                    && partida.JogadoresDoTime(recebe).Any(j => j.Cooldown <= 0 && j.Alcanca(bola) && j.DistanciaAte(bola.X, bola.Y) <= j.Alcance - Folga))
                    passosAoAlcance++;
                partida.Avancar(Passo);
            }
        }
        Assert.True(saques >= 30, $"poucos saques: {saques}");
        Assert.True(passaram == 0, $"{passaram} de {saques} saques quicaram duas vezes depois de passar 3 passos ou mais ao alcance de quem recebia ({Folga} m pra dentro dele)");
    }

    private static float Mediana(List<float> v)
    {
        var s = v.OrderBy(x => x).ToList();
        return s[s.Count / 2];
    }

    /// <summary>
    /// Medido em 25/09, antes do contato no instante do balanço: em partidas IA x IA a mediana do contato era 0,83 m do corpo
    /// (a borda do alcance confortável, 0,85) e 0,63 m do ponto ideal. Agora a IA bate com a bola no ponto mais perto do ideal.
    /// </summary>
    [Fact]
    public void Nas_partidas_entre_IAs_o_contato_sai_perto_do_ponto_ideal_e_nao_na_borda_do_alcance()
    {
        var corpo = new List<float>();
        var ideal = new List<float>();
        foreach (uint semente in new uint[] { 42, 43, 44 })
        {
            var partida = new Partida(new OpcoesDaPartida { Humanos = OpcoesDaPartida.NinguemHumano, Semente = semente, Dificuldade = Dificuldade.Medio });
            partida.Evento += ev =>
            {
                if (ev.Tipo != TipoDeEventoDaPartida.Golpe || ev.Golpe == TipoDeGolpe.Saque || ev.Jogador is not Jogador j) return;
                corpo.Add(j.DistanciaAte(partida.Bola.X, partida.Bola.Y));
                ideal.Add(DistanciaDoPontoIdeal(j, partida.Bola));
            };
            for (float t = 0; t < 240 && !partida.Acabou; t += Passo) partida.Avancar(Passo);
        }
        Assert.True(corpo.Count > 100, $"poucos golpes: {corpo.Count}");
        float medianaDoCorpo = Mediana(corpo), medianaDoIdeal = Mediana(ideal);
        Assert.True(MathF.Abs(medianaDoCorpo - Jogador.DistanciaIdealDoContato) <= 0.15f, $"mediana do contato a {medianaDoCorpo:F2} m do corpo (o ideal é {Jogador.DistanciaIdealDoContato})");
        Assert.True(medianaDoIdeal <= 0.4f, $"mediana do contato a {medianaDoIdeal:F2} m do ponto ideal (antes: 0,63)");
    }

    // ───────────── Humano simulado ─────────────

    /// <summary>
    /// Profissional com timing quase perfeito contra a IA Médio: contatos (fora o saque) e balanços no ar. Os dois testes
    /// que usam isto leem a mesma partida — determinística, então jogada uma vez só.
    /// </summary>
    private static readonly Lazy<(List<(float Corpo, float Ideal, float? Momento)> Contatos, int NoAr)> ComTimingQuasePerfeito = new(JogarComTimingQuasePerfeito);

    /// <summary>
    /// Por contato: distância da bola ao corpo e ao ponto ideal, e o momento do balanço (TempoNoBalanco) em que a bola passa
    /// pelo ponto ideal — o do contato menos quanto ela já tinha passado dele (+) ou ainda faltava (−), em tempo: a projeção
    /// de (bola − ponto ideal) na velocidade da bola em relação ao corpo, no chão, antes do golpe. null com a bola quase na
    /// vertical (menos de 1 m/s no chão), que não tem antes nem depois.
    /// </summary>
    private static (List<(float Corpo, float Ideal, float? Momento)> Contatos, int NoAr) JogarComTimingQuasePerfeito()
    {
        var perfil = PerfilDeHumano.Profissional with { DesvioDoTempo = 0.001f };
        var contatos = new List<(float, float, float?)>();
        int noAr = 0;
        foreach (uint semente in new uint[] { 1000, 1001 })
        {
            var partida = new Partida(new OpcoesDaPartida { Semente = semente, Dificuldade = Dificuldade.Medio, Humanos = [true, false, false, false] });
            var humano = new HumanoSimulado(0, perfil, new Aleatorio(semente * 31u + 7u));
            var j = partida.Jogadores[0];
            float vxAntes = 0, vyAntes = 0;   // a bola já sai relançada no evento do golpe
            partida.Evento += ev =>
            {
                if (ev.Tipo != TipoDeEventoDaPartida.Golpe || ev.Jogador != j || ev.Golpe == TipoDeGolpe.Saque) return;
                var b = partida.Bola;
                var (px, py) = j.PontoDeContato(j.LadoDoGolpePara(b));
                float vx = vxAntes - j.Vx, vy = vyAntes - j.Vy, v2 = vx * vx + vy * vy;
                float? momento = v2 >= 1f ? j.TempoNoBalanco - ((b.X - px) * vx + (b.Y - py) * vy) / v2 : null;
                contatos.Add((j.DistanciaAte(b.X, b.Y), DistanciaDoPontoIdeal(j, b), momento));
            };
            var entradas = new Entrada[4];
            for (float t = 0; t < 300 && !partida.Acabou; t += Passo)
            {
                entradas[0] = humano.Decidir(EstadoVisivel.De(partida), Passo);
                vxAntes = partida.Bola.Vx; vyAntes = partida.Bola.Vy;
                partida.Avancar(Passo, entradas);
            }
            noAr += j.BalancosNoAr;
        }
        return (contatos, noAr);
    }

    /// <summary>
    /// Com o contato num instante só, o humano simulado cronometra o aperto contando com o corpo ainda correndo pro lugar e
    /// segue correndo até o instante — senão a bola chega ao ponto planejado e o corpo não (medido em 25/09: 98 balanços no
    /// ar pra 185 contatos em 6 partidas; antes do contato no instante, 39 pra 234).
    /// </summary>
    [Fact]
    public void O_humano_simulado_com_timing_quase_perfeito_quase_nao_balanca_no_ar()
    {
        var (contatos, noAr) = ComTimingQuasePerfeito.Value;
        int balancos = contatos.Count + noAr;
        Assert.True(balancos >= 20, $"poucos balanços pra medir: {balancos}");
        Assert.True(noAr <= 0.2f * balancos, $"{noAr} balanços no ar em {balancos}");
    }

    /// <summary>
    /// O humano simulado com timing quase perfeito aperta pra bola chegar ao ponto ideal no instante de contato: o contato
    /// sai a ~0,6 m do corpo, e não na borda do alcance confortável (antes: mediana de 0,83 m do corpo e 0,67 m do ideal).
    /// </summary>
    [Fact]
    public void O_humano_simulado_com_timing_quase_perfeito_bate_com_a_bola_no_ponto_ideal()
    {
        var (contatos, _) = ComTimingQuasePerfeito.Value;
        Assert.True(contatos.Count >= 20, $"poucos golpes pra medir: {contatos.Count}");
        float medianaDoCorpo = Mediana(contatos.Select(c => c.Corpo).ToList()), medianaDoIdeal = Mediana(contatos.Select(c => c.Ideal).ToList());
        Assert.True(MathF.Abs(medianaDoCorpo - Jogador.DistanciaIdealDoContato) <= 0.15f, $"mediana do contato a {medianaDoCorpo:F2} m do corpo (o ideal é {Jogador.DistanciaIdealDoContato})");
        Assert.True(medianaDoIdeal <= 0.3f, $"mediana do contato a {medianaDoIdeal:F2} m do ponto ideal");
    }

    /// <summary>
    /// O sentido de HumanoSimuladoTests.Com_timing_quase_perfeito_o_contato_sai_perto_do_momento_ideal_do_balanco, no
    /// mecanismo novo, com a mesma folga (±0,04 s). Aquele mede o TempoNoBalanco no contato — que agora é o mesmo passo em todo
    /// contato do modo manual (0,1167 s a 120 Hz), então passa com qualquer timing do humano simulado (revisão de 25/09: com o
    /// aperto 80 ms adiantado ou 50 ms atrasado, passava). Este mede o momento do balanço em que a BOLA passa pelo ponto ideal
    /// de contato: com timing quase perfeito, é o momento ideal. Visto morder com os mesmos dois desvios.
    /// </summary>
    [Fact]
    public void O_humano_simulado_com_timing_quase_perfeito_poe_a_bola_no_ponto_ideal_no_momento_ideal_do_balanco()
    {
        var (contatos, _) = ComTimingQuasePerfeito.Value;
        var momentos = contatos.Where(c => c.Momento is not null).Select(c => c.Momento ?? 0).ToList();
        Assert.True(momentos.Count >= 20, $"poucos golpes pra medir: {momentos.Count}");
        float mediana = Mediana(momentos);
        Assert.InRange(mediana, Jogador.MomentoIdealDoBalanco - 0.04f, Jogador.MomentoIdealDoBalanco + 0.04f);
    }
}
