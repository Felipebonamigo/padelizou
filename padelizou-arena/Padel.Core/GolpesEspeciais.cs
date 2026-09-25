namespace Padel.Core;

/// <summary>
/// Os golpes que dependem de parede, de rede baixa ou de a bola sair da quadra: chiquita, contrapared, remate por 3 e
/// por 4. Nenhum solucionador sabe a altura das paredes (elas mudam de desenho: vidro escalonado, grade, portas): cada um
/// lança candidatos — pelo <see cref="Golpes.Calcular"/> ou com velocidade direta —, SIMULA a <see cref="Bola"/> e confere
/// a sequência de eventos (CruzouRede, Quique, Parede, Saiu). Uma solução só vale se sobreviver a pequenas variações
/// (passo de simulação maior, bola um pouco mais fraca ou mais forte, um pouco mais alta ou mais baixa): acertar por um
/// fio é sorte, não golpe. Sem solução daquela posição, devolvem null e quem chamou escolhe outro golpe.
/// O Golpe devolvido carrega a velocidade simulada em <see cref="Golpe.Lancamento"/> e, em AlvoX/AlvoY/TempoDeVoo,
/// onde e quando a bola quica do outro lado.
/// </summary>
public static class GolpesEspeciais
{
    private const float Passo = 1f / 120f;
    private const float PassoGrosso = 1f / 60f;

    /// <summary>A chiquita cai a até isso da rede (nos pés de quem está lá).</summary>
    public const float AlcanceDaChiquita = 3.5f;
    /// <summary>A chiquita passa a no máximo isso acima da rede.</summary>
    public const float FolgaDaChiquitaSobreARede = 0.5f;
    /// <summary>Remate de verdade: até ~145 km/h. Acima disso é canhão, não padel.</summary>
    public const float RapidezMaximaDoRemate = 40f;
    /// <summary>A contrapared sai de posição ruim (bola atrás, junto ao vidro): não dá pra bater com tudo.</summary>
    public const float RapidezMaximaDaContrapared = 22f;

    /// <summary>A velocidade com que um golpe sai: a do solucionador, se veio; senão, a do Golpes.Calcular pelo alvo. É o que a Partida usa.</summary>
    public static Velocidade VelocidadeDe(Golpe golpe, float x0, float y0, float z0) =>
        golpe.Lancamento ?? Golpes.Calcular(x0, y0, z0, golpe.AlvoX, golpe.AlvoY, golpe.TempoDeVoo, ignorarRede: golpe.IgnorarRede, efeito: golpe.Efeito);

    private readonly record struct Marca(EventoDaBola Evento, float T);

    /// <summary>Simula até a bola parar, sair, dar maximoDeEventos eventos ou passar o tempo. Devolve os eventos com o instante de cada um.</summary>
    private static List<Marca> Simular(float x0, float y0, float z0, Velocidade v, float segundos, int maximoDeEventos, float passo = Passo)
    {
        var bola = new Bola();
        bola.Posicionar(x0, y0, z0);
        bola.Lancar(v);
        var eventos = new List<EventoDaBola>(4);
        var marcas = new List<Marca>(maximoDeEventos + 2);
        for (float t = 0; t < segundos && bola.EmJogo && !bola.Parada && marcas.Count < maximoDeEventos;)
        {
            eventos.Clear();
            bola.Avancar(passo, eventos);
            t += passo;
            foreach (var e in eventos) marcas.Add(new Marca(e, t));
        }
        return marcas;
    }

    private static Velocidade Escalar(Velocidade v, float f) => v with { Vx = v.Vx * f, Vy = v.Vy * f, Vz = v.Vz * f };
    private static float Rapidez(Velocidade v) => MathF.Sqrt(v.Vx * v.Vx + v.Vy * v.Vy + v.Vz * v.Vz);

    /// <summary>A margem de força: golpe de potência aguenta 3 % e 0,4 m/s de tremida; golpe de toque (chiquita), 2 % e 0,2 m/s.</summary>
    private readonly record struct Margem(float Rapidez, float Vz)
    {
        public static readonly Margem DePotencia = new(0.03f, 0.4f);
        public static readonly Margem DeToque = new(0.02f, 0.2f);
    }

    /// <summary>
    /// A solução vale se continua valendo com o passo de simulação do jogo a 60 Hz, com a bola um pouco mais fraca e mais
    /// forte e saindo um pouco mais alta e mais baixa — a margem de um golpe bem dado, sem a qual qualquer tremida muda o resultado.
    /// </summary>
    private static bool Robusta(float x0, float y0, float z0, Velocidade v, float segundos, int maximoDeEventos, Margem margem, Func<List<Marca>, bool> vale)
    {
        return vale(Simular(x0, y0, z0, v, segundos, maximoDeEventos, PassoGrosso))
            && vale(Simular(x0, y0, z0, Escalar(v, 1 - margem.Rapidez), segundos, maximoDeEventos))
            && vale(Simular(x0, y0, z0, Escalar(v, 1 + margem.Rapidez), segundos, maximoDeEventos))
            && vale(Simular(x0, y0, z0, v with { Vz = v.Vz - margem.Vz }, segundos, maximoDeEventos))
            && vale(Simular(x0, y0, z0, v with { Vz = v.Vz + margem.Vz }, segundos, maximoDeEventos));
    }

    // ───────────── Chiquita ─────────────

    /// <summary>
    /// Chiquita: bola baixa e lenta nos pés de quem está na rede — cai a até <see cref="AlcanceDaChiquita"/> da rede do
    /// lado do alvo, passa a no máximo <see cref="FolgaDaChiquitaSobreARede"/> acima da rede, com topspin (que a derruba
    /// depois da rede). Entre as que servem, a mais lenta. ladoDoAlvo: o lado da quadra onde ela deve quicar (+1/-1).
    /// </summary>
    public static Golpe? Chiquita(float x0, float y0, float z0, int ladoDoAlvo, float alvoX)
    {
        alvoX = Util.Limitar(alvoX, -Quadra.MeiaLargura + 0.5f, Quadra.MeiaLargura - 0.5f);
        bool Vale(List<Marca> m) =>
            m.Count >= 2
            && m[0].Evento.Tipo == TipoDeEventoDaBola.CruzouRede && m[0].Evento.Para == ladoDoAlvo
            && m[0].Evento.Z <= Quadra.AlturaDaRede + FolgaDaChiquitaSobreARede
            && m[1].Evento.Tipo == TipoDeEventoDaBola.Quique && m[1].Evento.Lado == ladoDoAlvo
            && MathF.Abs(m[1].Evento.Y) <= AlcanceDaChiquita;

        // atalho: 42 chamadas ao Golpes.Calcular (cada uma simula até 12 vezes), ~6–10 ms. A saída, se pesar, é a mesma dos remates.
        Golpe? melhor = null;
        float menorRapidez = float.PositiveInfinity;
        foreach (float distancia in new[] { 3.0f, 2.5f, 2.0f })
        foreach (float tempo in new[] { 0.6f, 0.7f, 0.8f, 0.9f, 1.0f, 1.1f, 1.2f })
        foreach (float topspin in new[] { 800f, 1500f })
        {
            var efeito = new Efeito(topspin, 0);
            var v = Golpes.Calcular(x0, y0, z0, alvoX, ladoDoAlvo * distancia, tempo, efeito: efeito);
            float rapidez = Rapidez(v);
            if (rapidez >= menorRapidez) continue;
            var m = Simular(x0, y0, z0, v, 3, 2);
            if (!Vale(m) || !Robusta(x0, y0, z0, v, 3, 2, Margem.DeToque, Vale)) continue;
            menorRapidez = rapidez;
            var quique = m[1];
            melhor = new Golpe(quique.Evento.X, quique.Evento.Y, quique.T, TipoDeGolpe.Chiquita, Efeito: efeito, Lancamento: v);
        }
        return melhor;
    }

    // ───────────── Contrapared ─────────────

    /// <summary>
    /// Contrapared: a bola passou e está junto ao próprio vidro de fundo; bate-se NELE (é permitido: a própria parede antes
    /// de cruzar) pra ela voltar por cima, cruzar a rede e quicar do outro lado antes de qualquer parede de lá. Último recurso.
    /// Entre as que servem, a que cai mais perto de ~5 m da rede (funda o bastante pra não ser bola de matar), até
    /// <see cref="RapidezMaximaDaContrapared"/>. ladoDoAlvo: o lado onde ela deve quicar; a própria parede é a do outro lado.
    /// </summary>
    public static Golpe? Contrapared(float x0, float y0, float z0, int ladoDoAlvo, float alvoX)
    {
        int meuLado = -ladoDoAlvo;
        bool Vale(List<Marca> m) =>
            m.Count >= 3
            && m[0].Evento.Tipo == TipoDeEventoDaBola.Parede && m[0].Evento.Parede == QualParede.Fundo && m[0].Evento.Lado == meuLado
            && m[1].Evento.Tipo == TipoDeEventoDaBola.CruzouRede && m[1].Evento.Para == ladoDoAlvo
            && m[2].Evento.Tipo == TipoDeEventoDaBola.Quique && m[2].Evento.Lado == ladoDoAlvo;

        // A bola vai à parede e volta: ~1,3 s até quicar do outro lado. Mira o x do alvo com isso; a simulação diz onde cai.
        float vx = Util.Limitar(alvoX, -Quadra.MeiaLargura + 0.5f, Quadra.MeiaLargura - 0.5f) - x0;
        vx /= 1.3f;
        Golpe? melhor = null;
        float melhorNota = float.PositiveInfinity;
        for (float vy = 6; vy <= 20; vy += 2)
        for (float vz = 4; vz <= 13; vz += 1)
        {
            var v = new Velocidade(vx, meuLado * vy, vz, 0);
            float rapidez = Rapidez(v);
            if (rapidez > RapidezMaximaDaContrapared) continue;
            var m = Simular(x0, y0, z0, v, 4, 3);
            if (!Vale(m)) continue;
            var quique = m[2];
            float nota = MathF.Abs(MathF.Abs(quique.Evento.Y) - 5f) + 0.05f * rapidez;
            if (nota >= melhorNota || !Robusta(x0, y0, z0, v, 4, 3, Margem.DePotencia, Vale)) continue;
            melhorNota = nota;
            melhor = new Golpe(quique.Evento.X, quique.Evento.Y, quique.T, TipoDeGolpe.Contrapared, Lancamento: v);
        }
        return melhor;
    }

    // ───────────── Remate por 3 e por 4 ─────────────

    /// <summary>
    /// Remate por 4: plano, forte e pra baixo, quica do outro lado e sai por cima da parede de fundo de lá (ponto de quem
    /// bateu). Só existe perto da rede e com a bola alta — de longe o remate não desce a tempo de quicar perto e subir.
    /// Entre as que servem, a mais lenta; empatando, a que sai mais perto de alvoX.
    /// </summary>
    public static Golpe? SmashPor4(float x0, float y0, float z0, int ladoDoAlvo, float alvoX)
    {
        float distanciaAoFundo = MathF.Max(1f, Quadra.MeioComprimento + MathF.Abs(y0));
        float preferido = MathF.Atan2(alvoX - x0, distanciaAoFundo);
        var azimutes = new List<float>();
        foreach (float graus in new[] { 0f, -8f, 8f, -16f, 16f, -24f, 24f }) azimutes.Add(preferido + graus * MathF.PI / 180f);
        return Remate(x0, y0, z0, ladoDoAlvo, TipoDeGolpe.SmashPor4, azimutes,
            saiu => Quadra.LadoDe(saiu.Y) == ladoDoAlvo && MathF.Abs(saiu.Y) >= Quadra.MeioComprimento && MathF.Abs(saiu.X) < Quadra.MeiaLargura);
    }

    /// <summary>
    /// Remate por 3: quica do outro lado e sai pela lateral (por cima dela ou por onde a quadra deixar) do lado ladoDaSaida
    /// (sinal de x NO MUNDO: +1 sai por x = +5, -1 por x = -5). Ponto de quem bateu. Entre as que servem, a mais lenta;
    /// empatando, a menos cruzada.
    /// </summary>
    public static Golpe? SmashPor3(float x0, float y0, float z0, int ladoDoAlvo, int ladoDaSaida)
    {
        int sinal = ladoDaSaida >= 0 ? 1 : -1;
        var azimutes = new List<float>();
        for (float graus = 10; graus <= 60; graus += 5) azimutes.Add(sinal * graus * MathF.PI / 180f);
        return Remate(x0, y0, z0, ladoDoAlvo, TipoDeGolpe.SmashPor3, azimutes,
            saiu => saiu.X * sinal >= Quadra.MeiaLargura);
    }

    /// <summary>
    /// Busca comum dos remates: rapidez crescente (a primeira que dá é a escolhida), elevação de -8° a -40°, os azimutes
    /// dados em ordem de preferência (0 = reto pro outro lado; positivo gira pra +x). Remate plano, sem efeito.
    /// Vale: cruza a rede, quica do outro lado antes de qualquer parede e a próxima coisa é sair do jeito pedido.
    /// </summary>
    private static Golpe? Remate(float x0, float y0, float z0, int ladoDoAlvo, TipoDeGolpe tipo, List<float> azimutes, Func<EventoDaBola, bool> saidaCerta)
    {
        bool Vale(List<Marca> m) =>
            m.Count >= 3
            && m[0].Evento.Tipo == TipoDeEventoDaBola.CruzouRede && m[0].Evento.Para == ladoDoAlvo
            && m[1].Evento.Tipo == TipoDeEventoDaBola.Quique && m[1].Evento.Lado == ladoDoAlvo
            && m[2].Evento.Tipo == TipoDeEventoDaBola.Saiu && saidaCerta(m[2].Evento);

        // atalho: força bruta — até ~1.900 simulações curtas (~20 ms) quando NÃO há solução (4–17 ms quando há). Cabe porque
        // só roda quando o humano pede ou a IA sorteia o remate; se pesar (servidor com muitas partidas), a saída é um filtro
        // analítico antes de simular (descartar elevações que não passam a rede ou quicam longe) ou memorizar por posição quantizada.
        for (float rapidez = 22; rapidez <= RapidezMaximaDoRemate; rapidez += 2)
        {
            foreach (float azimute in azimutes)
            for (float graus = -8; graus >= -40; graus -= 2)
            {
                float elevacao = graus * MathF.PI / 180f;
                float horizontal = rapidez * MathF.Cos(elevacao);
                var v = new Velocidade(horizontal * MathF.Sin(azimute), ladoDoAlvo * horizontal * MathF.Cos(azimute), rapidez * MathF.Sin(elevacao), 0);
                var m = Simular(x0, y0, z0, v, 2.5f, 3);
                if (!Vale(m) || !Robusta(x0, y0, z0, v, 2.5f, 3, Margem.DePotencia, Vale)) continue;
                var quique = m[1];
                return new Golpe(quique.Evento.X, quique.Evento.Y, quique.T, tipo, Lancamento: v);
            }
        }
        return null;
    }
}
