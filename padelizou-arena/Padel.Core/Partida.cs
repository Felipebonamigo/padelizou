namespace Padel.Core;

public enum EstadoDaPartida { Saque, Rally, FimDoPonto, Fim }
public enum TipoDeEventoDaPartida { SaquePreparado, Balanco, Errou, Golpe, Quique, Parede, Rede, CruzouRede, Saiu, Ponto, Game, Set, Partida, Falta, Let, Fim }
/// <summary>Manual: o humano aperta pra balançar e o timing decide a qualidade. Automatico: bate sozinho ao alcance (assistência).</summary>
public enum ModoDeGolpe { Manual, Automatico }

/// <summary>Um acontecimento da partida. Em Golpe: o tipo do golpe e o lado do corpo (drive ou revés) — o que a animação precisa.</summary>
public sealed record EventoDaPartida(TipoDeEventoDaPartida Tipo, Jogador? Jogador = null, int Time = -1, Motivo? Motivo = null, TipoDeGolpe? Golpe = null, LadoDoGolpe? Lado = null);
public sealed record Mensagem(string Texto, bool Destaque = false, bool Suave = false);
public sealed record GolpeDado(Jogador Jogador, TipoDeGolpe Tipo, float Em, LadoDoGolpe Lado = LadoDoGolpe.Drive);

public sealed record OpcoesDaPartida
{
    public Dificuldade Dificuldade { get; init; } = Dificuldade.Medio;
    public bool PontoDeOuro { get; init; } = true;
    public int SetsParaVencer { get; init; } = 1;
    /// <summary>Por jogador (time*2 + índice): quem é humano. Padrão: só o da casa, metade direita.</summary>
    public bool[] Humanos { get; init; } = [true, false, false, false];
    public ModoDeGolpe ModoDeGolpe { get; init; } = ModoDeGolpe.Manual;
    public uint? Semente { get; init; }

    public static readonly bool[] NinguemHumano = [false, false, false, false];
}

public sealed class Estatisticas
{
    public int Pontos, Golpes, MaiorRally, Faltas, Lets;
    public Dictionary<Motivo, int> Motivos { get; } = new();
}

/// <summary>
/// A partida: junta placar, árbitro, bola, jogadores e IA numa máquina de estados
/// (saque → rally → fim do ponto → saque… → fim). Não desenha nem lê teclado: recebe a entrada dos
/// humanos por parâmetro e avisa por evento. Roda igual no Godot, num servidor sem engine e no teste.
/// </summary>
public sealed class Partida
{
    public const float EsperaDoSaqueHumano = 8f;   // segundos até o saque sair sozinho
    public const float EsperaDoSaqueDaIA = 1.1f;

    public static readonly IReadOnlyDictionary<Motivo, string> Motivos = new Dictionary<Motivo, string>
    {
        [Motivo.DoisQuiques] = "dois quiques",
        [Motivo.Rede] = "na rede",
        [Motivo.NaoPassou] = "não passou",
        [Motivo.ParedeSemQuicar] = "no vidro sem quicar",
        [Motivo.ParedePropria] = "na própria parede",
        [Motivo.Fora] = "pra fora da quadra",
        [Motivo.VoltouPeloVidro] = "voltou pelo vidro",
        [Motivo.DuplaFalta] = "dupla falta",
        [Motivo.ForaDaCaixa] = "saque fora da caixa",
        [Motivo.BolaMorta] = "bola morta",
    };

    public OpcoesDaPartida Opcoes { get; }
    public Aleatorio Aleatorio { get; }
    public Placar Placar { get; }
    public Arbitro Arbitro { get; } = new();
    public Bola Bola { get; } = new();
    public Jogador[] Jogadores { get; }
    public IA[] IAs { get; }
    public EstadoDaPartida Estado { get; private set; }
    public float Temporizador { get; private set; }
    public Mensagem? Mensagem { get; private set; }
    public GolpeDado? UltimoGolpe { get; private set; }
    public float TempoDeJogo { get; private set; }
    public int RallyAtual { get; private set; }
    public Estatisticas Estatisticas { get; } = new();
    public Caixa CaixaDoSaque { get; private set; }
    /// <summary>Erro (0 = perfeito, 1,6 = péssimo) do último golpe de um humano — pra HUD e pra teste.</summary>
    public float UltimoErroDoHumano { get; private set; }
    public event Action<EventoDaPartida>? Evento;

    private float _rolandoHa;
    private readonly Entrada[] _entradas = new Entrada[4];

    public Partida(OpcoesDaPartida? opcoes = null)
    {
        Opcoes = opcoes ?? new OpcoesDaPartida();
        if (Opcoes.Humanos.Length != 4) throw new ArgumentException("Humanos precisa ter 4 posições", nameof(opcoes));
        uint semente = Opcoes.Semente ?? (uint)Random.Shared.Next();
        Aleatorio = new Aleatorio(semente);
        Placar = new Placar(Opcoes.PontoDeOuro, Opcoes.SetsParaVencer, timeQueSaca: 0);
        var rival = Perfis.Por(Opcoes.Dificuldade);
        bool[] h = Opcoes.Humanos;
        Jogadores =
        [
            new Jogador(0, 0, "Você", h[0], h[0] ? Jogador.VelocidadeDoHumano : Perfis.Parceiro.Velocidade),
            new Jogador(0, 1, "Parceiro", h[1], h[1] ? Jogador.VelocidadeDoHumano : Perfis.Parceiro.Velocidade),
            new Jogador(1, 0, "Rival 1", h[2], h[2] ? Jogador.VelocidadeDoHumano : rival.Velocidade),
            new Jogador(1, 1, "Rival 2", h[3], h[3] ? Jogador.VelocidadeDoHumano : rival.Velocidade),
        ];
        IAs = [new IA(0, Perfis.Parceiro, Aleatorio), new IA(1, rival, Aleatorio)];
        IniciarPonto();
    }

    public Jogador[] JogadoresDoTime(int time) => time == 0 ? [Jogadores[0], Jogadores[1]] : [Jogadores[2], Jogadores[3]];
    public Jogador Sacador => Jogadores[Placar.Sacador.Time * 2 + Placar.Sacador.Jogador];
    public bool Acabou => Estado == EstadoDaPartida.Fim;

    private void Emitir(EventoDaPartida evento) => Evento?.Invoke(evento);

    public void IniciarPonto()
    {
        var sacador = Sacador;
        int lado = sacador.Lado;
        bool direita = Placar.LadoDoSaque == LadoDoSaque.Direita;
        int sinalDoSacador = direita ? lado : -lado;   // metade em que o sacador está
        int timeReceptor = 1 - sacador.Time;
        var (receptor, parceiroDoReceptor) = QuemRecebe(timeReceptor, direita);
        var parceiroDoSacador = JogadoresDoTime(sacador.Time).First(j => j != sacador);

        sacador.X = sinalDoSacador * 2.6f; sacador.Y = lado * 8.3f;
        parceiroDoSacador.X = -sinalDoSacador * 2.3f; parceiroDoSacador.Y = lado * 3.6f;
        receptor.X = -sinalDoSacador * 2.8f; receptor.Y = -lado * 8.0f;
        parceiroDoReceptor.X = sinalDoSacador * 2.3f; parceiroDoReceptor.Y = -lado * 4.2f;
        foreach (var j in Jogadores) j.Cooldown = 0;

        Bola.Reiniciar();
        Bola.Posicionar(sacador.X, sacador.Y, 0.8f);
        CaixaDoSaque = Quadra.CaixaDeSaque(-lado, direita);
        RallyAtual = 0;
        _rolandoHa = 0;
        UltimoGolpe = null;
        Estado = EstadoDaPartida.Saque;
        Temporizador = sacador.Humano ? EsperaDoSaqueHumano : EsperaDoSaqueDaIA;
        bool segundo = Arbitro.Faltas > 0;
        Mensagem = sacador.Humano
            ? new Mensagem(segundo ? "Segundo saque — aperte pra sacar" : "Seu saque — aperte pra sacar", Suave: true)
            : null;
        Emitir(new EventoDaPartida(TipoDeEventoDaPartida.SaquePreparado, sacador, sacador.Time));
    }

    /// <summary>Quem recebe é o jogador da metade diagonal ao sacador; o parceiro dele fica na rede.</summary>
    private (Jogador, Jogador) QuemRecebe(int timeReceptor, bool direita)
    {
        var dupla = JogadoresDoTime(timeReceptor);
        var receptor = direita ? dupla[0] : dupla[1];   // índice 0 = metade direita do time = diagonal do saque à direita
        return (receptor, receptor == dupla[0] ? dupla[1] : dupla[0]);
    }

    public void Sacar(Entrada entrada)
    {
        entrada = Saneada(entrada);
        var sacador = Sacador;
        var caixa = CaixaDoSaque;
        float alvoX, alvoY;
        if (sacador.Humano)
        {
            alvoX = Util.Limitar(caixa.CentroX + entrada.Dx * sacador.Lado * 1.8f + Aleatorio.Gaussiana() * 0.35f, caixa.XMin + 0.3f, caixa.XMax - 0.3f);
            alvoY = caixa.CentroY - sacador.Lado * 0.5f;
        }
        else
        {
            float ruido = IAs[sacador.Time].Perfil.ErroDeMira * 0.55f;
            alvoX = caixa.CentroX + Aleatorio.Gaussiana() * ruido;
            alvoY = caixa.CentroY + Aleatorio.Gaussiana() * ruido;
        }
        Bola.Lancar(Golpes.Calcular(Bola.X, Bola.Y, Bola.Z, alvoX, alvoY, tempoDeVoo: 1.0f, efeito: new Efeito(-600, 0)));   // saque por baixo, com slice
        Arbitro.IniciarSaque(sacador.Time, caixa);
        sacador.Cooldown = 0.5f;
        sacador.Golpes += 1;
        UltimoGolpe = new GolpeDado(sacador, TipoDeGolpe.Saque, TempoDeJogo);
        Estado = EstadoDaPartida.Rally;
        Mensagem = null;
        RallyAtual = 1;
        Estatisticas.Golpes += 1;
        foreach (var ia in IAs) ia.AoSacar(this, sacador.Time);
        Emitir(new EventoDaPartida(TipoDeEventoDaPartida.Golpe, sacador, sacador.Time, Golpe: TipoDeGolpe.Saque, Lado: LadoDoGolpe.Drive));   // saque por baixo, de drive
    }

    /// <summary>
    /// Avança a simulação. entradas: uma por jogador (time*2 + índice); faltando, vale Entrada.Vazia. A entrada vem do
    /// cliente (DECISOES.md, D2: o host simula com o que os clientes mandam) e passa por <see cref="Saneada"/>.
    /// </summary>
    public void Avancar(float dt, ReadOnlySpan<Entrada> entradas = default)
    {
        if (!(dt > 0)) return;
        for (int i = 0; i < 4; i++) _entradas[i] = i < entradas.Length ? Saneada(entradas[i]) : Entrada.Vazia;
        TempoDeJogo += dt;
        foreach (var j in Jogadores) j.AvancarTempo(dt);
        switch (Estado)
        {
            case EstadoDaPartida.Saque:
            {
                Temporizador -= dt;
                var sacador = Sacador;
                var entrada = EntradaDe(sacador);
                if ((sacador.Humano && entrada.AcaoPressionada) || Temporizador <= 0) Sacar(entrada);
                break;
            }
            case EstadoDaPartida.Rally:
                Rally(dt);
                break;
            case EstadoDaPartida.FimDoPonto:
                Temporizador -= dt;
                if (Temporizador <= 0)
                {
                    if (Placar.Acabou) { Estado = EstadoDaPartida.Fim; Emitir(new EventoDaPartida(TipoDeEventoDaPartida.Fim, Time: Placar.Vencedor!.Value)); }
                    else IniciarPonto();
                }
                break;
            default:
                break;
        }
    }

    private Entrada EntradaDe(Jogador j) => _entradas[j.Time * 2 + j.Indice];

    /// <summary>
    /// Fronteira de confiança: cliente com defeito ou malicioso não derruba nem corrompe o host. Componente de direção não
    /// finita (NaN, ±infinito) vale 0 — sem direção —, e cada componente fica em [-1, 1] (módulo 1 = velocidade máxima).
    /// </summary>
    private static Entrada Saneada(Entrada e) => e with { Dx = Componente(e.Dx), Dy = Componente(e.Dy) };
    private static float Componente(float v) => float.IsFinite(v) ? Util.Limitar(v, -1, 1) : 0;

    private void Rally(float dt)
    {
        bool manual = Opcoes.ModoDeGolpe == ModoDeGolpe.Manual;
        foreach (var j in Jogadores)
        {
            if (!j.Humano) continue;
            var e = EntradaDe(j);
            j.Mover(e.Dx * j.Lado, e.Dy * j.Lado, dt);   // do referencial do jogador pro mundo
            if (!manual) continue;
            if (j.BalancoTerminouNoAr)
            {
                // O balanço acabou sem tocar na bola: raquete no ar, e um instante pra se recompor.
                j.BalancosNoAr += 1;
                j.Cooldown = 0.45f;
                Emitir(new EventoDaPartida(TipoDeEventoDaPartida.Errou, j, j.Time));
            }
            if ((e.AcaoPressionada || e.LobPressionada) && !j.Balancando && j.Cooldown <= 0)
            {
                j.IniciarBalanco(lob: e.LobPressionada);
                Emitir(new EventoDaPartida(TipoDeEventoDaPartida.Balanco, j, j.Time));
            }
        }
        foreach (var ia in IAs) ia.Reagir(dt, this);

        var bola = Bola;
        if (bola.EmJogo && !bola.Rolando)
        {
            foreach (var j in Jogadores)
            {
                if (j.Cooldown > 0 || !Arbitro.PodeGolpear(j.Time)) continue;
                if (j.Humano && manual && !j.Balancando) continue;   // no modo manual, sem balanço a bola passa
                if (!j.PodeBaterAgora(bola)) continue;
                Golpear(j);
                break;
            }
        }

        var eventos = new List<EventoDaBola>(4);
        bola.Avancar(dt, eventos);
        bool rebateu = false;
        foreach (var evento in eventos)
        {
            if (evento.Tipo == TipoDeEventoDaBola.Saiu) FixarNoPontoDeSaida();
            Emitir(new EventoDaPartida(TraduzirEvento(evento.Tipo)));
            var decisao = Arbitro.Processar(evento);
            if (decisao is Decisao d) { Decidir(d); return; }
            if (evento.Tipo is TipoDeEventoDaBola.Quique or TipoDeEventoDaBola.Parede) rebateu = true;
        }
        if (rebateu) foreach (var ia in IAs) ia.AoRebater(this);

        if (bola.Rolando)
        {
            _rolandoHa += dt;
            if (_rolandoHa > 0.6f)
            {
                // Bola rolando é bola que já quicou e não vai quicar de novo: conta como o quique seguinte.
                var decisao = Arbitro.Processar(new EventoDaBola(TipoDeEventoDaBola.Quique, bola.X, bola.Y, 0, Quadra.LadoDe(bola.Y)))
                    ?? Decisao.Ponto(Arbitro.Receptor ?? 0, Motivo.BolaMorta);
                Decidir(decisao);
            }
        }
        else
        {
            _rolandoHa = 0;
        }
        if (Estado == EstadoDaPartida.Rally && !bola.EmJogo)
        {
            Decidir(Decisao.Ponto(Arbitro.Receptor ?? 0, Motivo.Fora));
        }
    }

    /// <summary>
    /// A Bola marca a saída um sub-passo além do plano da parede (|x| &gt; 5 ou |y| &gt; 10, alguns centímetros); o que fica
    /// parado na tela até o próximo ponto é o ponto de saída, no plano da parede — e a quadra segue sendo o limite de tudo o
    /// que a partida mostra (é o invariante das partidas simuladas; o remate por 3 / por 4 tira a bola de propósito).
    /// atalho: projeção no plano, não interpolação na trajetória — erro de poucos centímetros na outra coordenada. O lugar
    /// natural disto é Bola.Sair; fica aqui porque Bola.cs está com a tarefa das paredes. Saída: mover pra lá quando ela entrar.
    /// </summary>
    private void FixarNoPontoDeSaida()
    {
        Bola.X = Util.Limitar(Bola.X, -Quadra.MeiaLargura, Quadra.MeiaLargura);
        Bola.Y = Util.Limitar(Bola.Y, -Quadra.MeioComprimento, Quadra.MeioComprimento);
    }

    private static TipoDeEventoDaPartida TraduzirEvento(TipoDeEventoDaBola tipo) => tipo switch
    {
        TipoDeEventoDaBola.Quique => TipoDeEventoDaPartida.Quique,
        TipoDeEventoDaBola.Parede => TipoDeEventoDaPartida.Parede,
        TipoDeEventoDaBola.Rede => TipoDeEventoDaPartida.Rede,
        TipoDeEventoDaBola.CruzouRede => TipoDeEventoDaPartida.CruzouRede,
        _ => TipoDeEventoDaPartida.Saiu,
    };

    private void Golpear(Jogador jogador)
    {
        var bola = Bola;
        var golpe = jogador.Humano ? GolpeDoHumano(jogador, EntradaDe(jogador)) : IAs[jogador.Time].EscolherGolpe(jogador, bola, this);
        bola.Lancar(GolpesEspeciais.VelocidadeDe(golpe, bola.X, bola.Y, bola.Z));
        jogador.EncerrarBalanco();
        jogador.Cooldown = 0.4f;
        jogador.Golpes += 1;
        Arbitro.RegistrarGolpe(jogador.Time);
        UltimoGolpe = new GolpeDado(jogador, golpe.Tipo, TempoDeJogo, golpe.Lado);
        RallyAtual += 1;
        Estatisticas.Golpes += 1;
        foreach (var ia in IAs) ia.AoGolpear(this, golpe, jogador);
        Emitir(new EventoDaPartida(TipoDeEventoDaPartida.Golpe, jogador, jogador.Time, Golpe: golpe.Tipo, Lado: golpe.Lado));
    }

    /// <summary>Direção lateral "forte" (|Dx| a partir disso): víbora na bola alta, por 3 no remate segurado.</summary>
    public const float LimiarDoLadoForte = 0.7f;
    /// <summary>Contato pior que isso (erro de timing + corpo) não tem potência pra tirar a bola da quadra: o por 3 / por 4 vira smash comum.</summary>
    public const float ErroMaximoDoRemateForte = 0.6f;
    private const float AlturaDaBolaAlta = 1.5f;

    /// <summary>
    /// Golpe do humano, no referencial dele (Dy &lt; 0 = frente, rumo à rede; Dx &gt; 0 = direita), com os dois botões — ação e
    /// lob — mais a direção. <b>Lob</b> = o balanço começou no botão de lob (no modo Automático: segurar a ação).
    /// <b>Ação segurada</b> = o botão de ação ainda apertado no instante do contato (no Automático, o mesmo segurar).
    /// <b>Bola alta</b> = acima de 1,5 m. <b>Frente</b>/<b>trás</b> = |Dy| &gt; 0,3. <b>Lado forte</b> = |Dx| ≥ <see cref="LimiarDoLadoForte"/>.
    /// Em ordem de prioridade (a primeira que casa):
    /// <list type="table">
    /// <listheader><term>entrada</term><description>golpe</description></listheader>
    /// <item><term>bola alta + frente + ação segurada</term><description><b>SmashPor4</b>; com lado forte, <b>SmashPor3</b> pra aquele
    ///   lado. Se o solucionador não acha (longe da rede, bola baixa) ou o contato foi ruim (erro ≥ <see cref="ErroMaximoDoRemateForte"/>): Smash.</description></item>
    /// <item><term>lob + frente</term><description><b>Chiquita</b> (sem solução daquela posição: Lob).</description></item>
    /// <item><term>lob (sem frente)</term><description><b>Lob</b>.</description></item>
    /// <item><term>bola atrás do corpo junto ao próprio vidro + trás</term><description><b>Contrapared</b> (sem solução: segue a tabela).</description></item>
    /// <item><term>bola alta + frente</term><description><b>Smash</b>.</description></item>
    /// <item><term>bola alta + lado forte (sem frente)</term><description><b>Víbora</b> pra aquele lado.</description></item>
    /// <item><term>bola alta (sem frente)</term><description><b>Bandeja</b>.</description></item>
    /// <item><term>frente / trás / nada</term><description>Ataque (curto, topspin) / Defesa (fundo, slice) / Normal.</description></item>
    /// </list>
    /// Esquerda/direita escolhem o canto. O golpe sai de drive ou de revés pelo lado do corpo em que a bola está. No modo
    /// manual o timing do balanço e o corpo decidem o erro: cedo demais é bola no ar; tarde é bola em cima do corpo;
    /// esticado, baixo, rápido, no corpo ou de revés (alto, principalmente) piora.
    /// </summary>
    private Golpe GolpeDoHumano(Jogador jogador, Entrada entrada) =>
        EscolherGolpeDoHumano(jogador, entrada) with { Lado = jogador.LadoDoGolpePara(Bola) };

    private Golpe EscolherGolpeDoHumano(Jogador jogador, Entrada entrada)
    {
        var bola = Bola;
        int lado = jogador.Lado, ladoDoAlvo = -lado;
        bool manual = Opcoes.ModoDeGolpe == ModoDeGolpe.Manual;
        float erroDeTiming = manual ? Util.Limitar(MathF.Abs(jogador.TempoNoBalanco - Jogador.MomentoIdealDoBalanco) / 0.15f, 0, 1) : 0;
        float erro = Util.Limitar(0.6f * jogador.DificuldadeDoGolpe(bola) + erroDeTiming, 0, 1.6f);
        UltimoErroDoHumano = erro;
        if (erro > 0.95f && Aleatorio.Proximo() < 0.35f + (erro - 0.95f))
        {
            // Golpe muito ruim: metade na rede, metade no vidro sem quicar.
            return Aleatorio.Proximo() < 0.5f
                ? new Golpe((Aleatorio.Proximo() - 0.5f) * 6, ladoDoAlvo * 0.3f, 0.45f, TipoDeGolpe.Erro, IgnorarRede: true)
                : new Golpe((Aleatorio.Proximo() - 0.5f) * 6, ladoDoAlvo * 11.5f, 0.55f, TipoDeGolpe.Erro, IgnorarRede: true);
        }
        float sigma = 0.25f + 0.8f * erro;
        float Ruido() => Aleatorio.Gaussiana() * sigma;
        float x = entrada.Dx < -0.3f ? -3.3f * lado : entrada.Dx > 0.3f ? 3.3f * lado : jogador.X * 0.4f;
        x = Util.Limitar(x + Ruido(), -4.5f, 4.5f);
        bool frente = entrada.Dy < -0.3f, tras = entrada.Dy > 0.3f;
        bool ladoForte = MathF.Abs(entrada.Dx) >= LimiarDoLadoForte;
        int paraOLado = (entrada.Dx < 0 ? -1 : 1) * lado;   // o lado forte, no mundo (sem MathF.Sign: ele lança exceção com NaN)
        bool bolaAlta = bola.Z > AlturaDaBolaAlta;
        bool lob = manual ? jogador.BalancoDeLob : entrada.AcaoSegurada;
        bool acaoSegurada = manual ? entrada.AcaoSegurada && !jogador.BalancoDeLob : entrada.AcaoSegurada;

        if (bolaAlta && frente && acaoSegurada)
        {
            if (erro < ErroMaximoDoRemateForte)
            {
                var forte = ladoForte
                    ? GolpesEspeciais.SmashPor3(bola.X, bola.Y, bola.Z, ladoDoAlvo, paraOLado)
                    : GolpesEspeciais.SmashPor4(bola.X, bola.Y, bola.Z, ladoDoAlvo, x);
                if (forte is Golpe remate) return remate;
            }
            return Smash();
        }
        if (lob)
        {
            if (frente && GolpesEspeciais.Chiquita(bola.X, bola.Y, bola.Z, ladoDoAlvo, x) is Golpe chiquita) return chiquita;
            return new Golpe(x, ladoDoAlvo * (8.2f + Ruido() * 0.5f), 1.6f, TipoDeGolpe.Lob, Efeito: new Efeito(-400, 0));
        }
        if (tras && jogador.BolaAtrasJuntoAoVidro(bola) && GolpesEspeciais.Contrapared(bola.X, bola.Y, bola.Z, ladoDoAlvo, x) is Golpe contrapared)
            return contrapared;
        if (bolaAlta)
        {
            if (frente) return Smash();
            if (ladoForte)
                return new Golpe(Util.Limitar(3.8f * paraOLado + Ruido(), -4.5f, 4.5f), ladoDoAlvo * (6.5f + Ruido()), 0.7f, TipoDeGolpe.Vibora, Efeito: new Efeito(-800, 1800 * paraOLado * ladoDoAlvo));
            return new Golpe(x, ladoDoAlvo * (7.5f + Ruido()), 0.95f, TipoDeGolpe.Bandeja, Efeito: new Efeito(-1500, 500));
        }
        if (frente) return new Golpe(x, ladoDoAlvo * (4.5f + Ruido()), 0.6f, TipoDeGolpe.Ataque, Efeito: new Efeito(2200, 0));
        if (tras) return new Golpe(x, ladoDoAlvo * (7.8f + Ruido()), 1.05f, TipoDeGolpe.Defesa, Efeito: new Efeito(-1200, 0));
        return new Golpe(x, ladoDoAlvo * (6.6f + Ruido()), 0.8f, TipoDeGolpe.Normal, Efeito: new Efeito(1200, 0));

        Golpe Smash() => new(x, ladoDoAlvo * (4.2f + Ruido()), 0.4f, TipoDeGolpe.Smash, Efeito: new Efeito(1500, 0));
    }

    private void Decidir(Decisao decisao)
    {
        if (decisao.Tipo == TipoDeDecisao.Ponto) { EncerrarPonto(decisao.Para, decisao.Motivo); return; }
        Bola.EmJogo = false;
        Estado = EstadoDaPartida.FimDoPonto;
        Temporizador = 1.3f;
        if (decisao.Tipo == TipoDeDecisao.Falta)
        {
            Estatisticas.Faltas += 1;
            Mensagem = new Mensagem($"Falta — {Motivos[decisao.Motivo]}. Segundo saque");
            Emitir(new EventoDaPartida(TipoDeEventoDaPartida.Falta, Motivo: decisao.Motivo));
        }
        else
        {
            Estatisticas.Lets += 1;
            Mensagem = new Mensagem("Let — a bola tocou a rede. Repete o saque");
            Emitir(new EventoDaPartida(TipoDeEventoDaPartida.Let));
        }
    }

    private void EncerrarPonto(int para, Motivo motivo)
    {
        var evento = Placar.PontoPara(para);
        Estatisticas.Pontos += 1;
        Estatisticas.Motivos[motivo] = Estatisticas.Motivos.GetValueOrDefault(motivo) + 1;
        Estatisticas.MaiorRally = Math.Max(Estatisticas.MaiorRally, RallyAtual);
        Bola.EmJogo = false;
        Arbitro.NovoPonto();
        Mensagem = new Mensagem(TextoDoPonto(para, motivo, evento), Destaque: evento.Tipo != TipoDeEventoDoPlacar.Ponto);
        Estado = EstadoDaPartida.FimDoPonto;
        Temporizador = evento.Tipo == TipoDeEventoDoPlacar.Ponto ? 1.5f : 2.4f;
        var tipo = evento.Tipo switch
        {
            TipoDeEventoDoPlacar.Game => TipoDeEventoDaPartida.Game,
            TipoDeEventoDoPlacar.Set => TipoDeEventoDaPartida.Set,
            TipoDeEventoDoPlacar.Partida => TipoDeEventoDaPartida.Partida,
            _ => TipoDeEventoDaPartida.Ponto,
        };
        Emitir(new EventoDaPartida(tipo, Time: para, Motivo: motivo));
    }

    private string TextoDoPonto(int para, Motivo motivo, EventoDoPlacar evento)
    {
        string descricao = Motivos[motivo];
        bool casa = para == 0;
        return evento.Tipo switch
        {
            TipoDeEventoDoPlacar.Game => $"{(casa ? "Game da casa" : "Game dos rivais")} — {descricao}",
            TipoDeEventoDoPlacar.Set => $"{(casa ? "Set da casa!" : "Set dos rivais.")} {Placar.Resumo()}",
            TipoDeEventoDoPlacar.Partida => $"{(casa ? "A casa venceu!" : "Os rivais venceram.")} {Placar.Resumo()}",
            _ => $"{(casa ? "Ponto da casa" : "Ponto dos rivais")} — {descricao}",
        };
    }
}
