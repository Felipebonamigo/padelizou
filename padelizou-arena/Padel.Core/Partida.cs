namespace Padel.Core;

public enum EstadoDaPartida { Saque, Rally, FimDoPonto, Fim }
public enum TipoDeEventoDaPartida { SaquePreparado, Balanco, Errou, Golpe, Quique, Parede, Rede, CruzouRede, Saiu, Ponto, Game, Set, Partida, Falta, Let, Fim }
/// <summary>
/// Manual: o humano aperta pra balançar e o contato sai no instante do balanço (<see cref="Jogador.NoInstanteDoContato"/>) —
/// o timing decide onde a bola está e daí a qualidade. Automatico: bate sozinho com a bola no ponto mais perto do ideal
/// (<see cref="Jogador.PodeBaterAgora(Bola, float, bool)"/>, como a IA) — assistência.
/// </summary>
public enum ModoDeGolpe { Manual, Automatico }

/// <summary>
/// DoisBotoes (o do Arena): ação e lob, e o analógico escolhe o golpe com a situação (Partida.GolpeDoHumano).
/// QuatroBotoes (o da fatia no Unreal): o botão que começa o balanço escolhe a família do golpe e o analógico só a direção.
/// </summary>
public enum Controle { DoisBotoes, QuatroBotoes }

/// <summary>
/// O nível de assistência do humano (padelizou-game, docs/ASSISTENCIA.md), de cada jogador e trocável a qualquer hora
/// (<see cref="Jogador.Assistencia"/>). Hoje muda a janela do timing (<see cref="Partida.JanelaDoContato"/>): no Pro, o
/// jogo de sempre, a raquete só pega a bola no instante do balanço; no Padrão e no Casual, no melhor passo de uma janela
/// em volta dele. Só vale no modo Manual (no Automático a bola já é batida no melhor passo).
/// </summary>
public enum Assistencia { Pro, Padrao, Casual }

/// <summary>Um acontecimento da partida. Em Golpe: o tipo do golpe e o lado do corpo (drive ou revés) — o que a animação precisa.</summary>
public sealed record EventoDaPartida(TipoDeEventoDaPartida Tipo, Jogador? Jogador = null, int Time = -1, Motivo? Motivo = null, TipoDeGolpe? Golpe = null, LadoDoGolpe? Lado = null);
public sealed record Mensagem(string Texto, bool Destaque = false, bool Suave = false);
public sealed record GolpeDado(Jogador Jogador, TipoDeGolpe Tipo, float Em, LadoDoGolpe Lado = LadoDoGolpe.Drive);

public sealed record OpcoesDaPartida
{
    public Dificuldade Dificuldade { get; init; } = Dificuldade.Medio;
    public bool PontoDeOuro { get; init; } = true;
    public int SetsParaVencer { get; init; } = 1;
    /// <summary>Games pra fechar o set; o empate nele vai ao tie-break (<see cref="Placar.GamesPorSet"/>). O formato curto da
    /// fatia no Unreal é 4. Só no jogo local: ver <see cref="ValeOnline"/>.</summary>
    public int GamesPorSet { get; init; } = 6;
    /// <summary>Pontos do tie-break (<see cref="Placar.PontosDoTieBreak"/>); o super tie-break do formato curto é 10.</summary>
    public int PontosDoTieBreak { get; init; } = 7;
    /// <summary>Por jogador (time*2 + índice): quem é humano. Padrão: só o da casa, metade direita.</summary>
    public bool[] Humanos { get; init; } = [true, false, false, false];
    public ModoDeGolpe ModoDeGolpe { get; init; } = ModoDeGolpe.Manual;
    /// <summary>O esquema de botões do humano (<see cref="Padel.Core.Controle"/>). Quatro botões só no modo Manual.</summary>
    public Controle Controle { get; init; } = Controle.DoisBotoes;
    /// <summary>O nível de assistência com que os humanos começam (<see cref="Padel.Core.Assistencia"/>); cada um muda o seu em <see cref="Jogador.Assistencia"/>.</summary>
    public Assistencia Assistencia { get; init; } = Assistencia.Pro;
    public uint? Semente { get; init; }
    /// <summary>
    /// Pontos corridos (Americano/Mexicano): o total que a soma dos pontos precisa alcançar, ex. 24 ou 32 — a partida fica
    /// sem games nem sets, e <see cref="PontoDeOuro"/> e <see cref="SetsParaVencer"/> não valem (<see cref="Placar.DePontosCorridos"/>).
    /// <c>null</c> (o padrão) = padel de games e sets. Só no jogo local: ver <see cref="ValeOnline"/>.
    /// </summary>
    public int? PontosCorridos { get; init; }

    /// <summary>
    /// Estas opções servem pra uma sala online? Pontos corridos, set fora do padrão (6 games, tie-break a 7), quatro botões e
    /// assistência não: o protocolo (<c>Padel.Core.Rede</c>: as opções do BemVindo, o placar do instantâneo e a
    /// EntradaDeRede) só carrega ponto de ouro, sets e os botões A e B, e um cliente veria outra partida ou perderia botões.
    /// O <see cref="Rede.ServidorDaPartida"/> recusa criar a sala quando é <c>false</c>.
    /// </summary>
    public bool ValeOnline => PontosCorridos is null && GamesPorSet == 6 && PontosDoTieBreak == 7
        && Controle == Controle.DoisBotoes && Assistencia == Assistencia.Pro;

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

    /// <summary>
    /// Meia largura (s) da janela do timing em volta de <see cref="Jogador.MomentoIdealDoBalanco"/>: Pro 0 (o instante),
    /// Padrão ±20 ms, Casual ±40 ms. Medido em 15/10 (docs/ASSISTENCIA.md do padelizou-game): o ganho em pontos do
    /// Iniciante contra a Médio para por volta de ±40 ms.
    /// </summary>
    public static float JanelaDoContato(Assistencia nivel) => nivel switch
    {
        Assistencia.Padrao => 0.02f,
        Assistencia.Casual => 0.04f,
        _ => 0,
    };
    public event Action<EventoDaPartida>? Evento;

    private float _rolandoHa;
    private readonly Entrada[] _entradas = new Entrada[4];

    public Partida(OpcoesDaPartida? opcoes = null)
    {
        Opcoes = opcoes ?? new OpcoesDaPartida();
        if (Opcoes.Humanos.Length != 4) throw new ArgumentException("Humanos precisa ter 4 posições", nameof(opcoes));
        if (Opcoes.Controle == Controle.QuatroBotoes && Opcoes.ModoDeGolpe != ModoDeGolpe.Manual)
            throw new ArgumentException("O controle de quatro botões só vale no modo Manual: o botão do balanço é que escolhe o golpe", nameof(opcoes));
        uint semente = Opcoes.Semente ?? (uint)Random.Shared.Next();
        Aleatorio = new Aleatorio(semente);
        Placar = Opcoes.PontosCorridos is int total
            ? Placar.DePontosCorridos(total, timeQueSaca: 0)
            : new Placar(Opcoes.PontoDeOuro, Opcoes.SetsParaVencer, timeQueSaca: 0, Opcoes.GamesPorSet, Opcoes.PontosDoTieBreak);
        var rival = Perfis.Por(Opcoes.Dificuldade);
        bool[] h = Opcoes.Humanos;
        Jogadores =
        [
            new Jogador(0, 0, "Você", h[0], h[0] ? Jogador.VelocidadeDoHumano : Perfis.Parceiro.Velocidade),
            new Jogador(0, 1, "Parceiro", h[1], h[1] ? Jogador.VelocidadeDoHumano : Perfis.Parceiro.Velocidade),
            new Jogador(1, 0, "Rival 1", h[2], h[2] ? Jogador.VelocidadeDoHumano : rival.Velocidade),
            new Jogador(1, 1, "Rival 2", h[3], h[3] ? Jogador.VelocidadeDoHumano : rival.Velocidade),
        ];
        foreach (var j in Jogadores) if (j.Humano) j.Assistencia = Opcoes.Assistencia;
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
                    // Fim: Time = o vencedor, ou −1 no empate dos pontos corridos (Placar.Empate).
                    if (Placar.Acabou) { Estado = EstadoDaPartida.Fim; Emitir(new EventoDaPartida(TipoDeEventoDaPartida.Fim, Time: Placar.Vencedor ?? -1)); }
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
            if (IntencaoDoAperto(e) is IntencaoDoGolpe intencao && !j.Balancando && j.Cooldown <= 0)
            {
                j.IniciarBalanco(intencao);
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
                if (j.Humano && manual)
                {
                    // A raquete passa pelo ponto de contato uma vez por balanço: sem balanço, ou fora desse instante, a bola passa.
                    // Bola que só chega ao alcance depois do instante encontra a raquete já passada (raquete no ar no fim do balanço).
                    // Com assistência, o instante vira uma janela, e o contato sai no passo dela com a bola mais perto do ideal.
                    if (!j.NaJanelaDoContato || !j.Alcanca(bola)) continue;
                    if (!j.FimDaJanelaDoContato && !j.PodeBaterAgora(bola, dt, Arbitro.QuicouNoReceptor)) continue;
                }
                else if (!j.PodeBaterAgora(bola, dt, Arbitro.QuicouNoReceptor)) continue;
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
    /// <summary>Contato pior que isso (onde a bola estava no instante do contato) não tem potência pra tirar a bola da quadra: o por 3 / por 4 vira smash comum.</summary>
    public const float ErroMaximoDoRemateForte = 0.6f;
    /// <summary>
    /// Erro do golpe do humano = PesoDoCorpoNoErro × <see cref="Jogador.DificuldadeDoGolpe"/> + PesoDoAtraso × <see cref="Jogador.AtrasoDoContato"/>
    /// (por metro), os dois no instante do contato. PesoDoAtraso = 1/m é o |Δt|/0,15 s de antes com a bola a ~6,5 m/s (a
    /// mediana no contato, medida em 25/09): 30 cm de atraso custam o que 45 ms custavam — e mais na bola mais rápida.
    /// </summary>
    private const float PesoDoCorpoNoErro = 1.0f, PesoDoAtraso = 1.0f;
    private const float AlturaDaBolaAlta = 1.5f;

    /// <summary>
    /// O botão apertado neste passo que começa um balanço, se houver. Lob antes da ação, como sempre foi; no de quatro
    /// botões, Y antes de X e os dois antes do A. No de dois botões, X e Y não existem.
    /// </summary>
    private IntencaoDoGolpe? IntencaoDoAperto(Entrada e)
    {
        if (e.LobPressionada) return IntencaoDoGolpe.Lob;
        if (Opcoes.Controle == Controle.QuatroBotoes)
        {
            if (e.AtaquePressionado) return IntencaoDoGolpe.Ataque;
            if (e.CortadoPressionado) return IntencaoDoGolpe.Cortado;
        }
        return e.AcaoPressionada ? IntencaoDoGolpe.Chapado : null;
    }

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
    /// Esquerda/direita escolhem o canto. O golpe sai de drive ou de revés pelo lado do corpo em que a bola está. O erro sai
    /// de onde a bola está no instante do contato: cedo demais é bola longe (esticada, ou fora do alcance: raquete no ar);
    /// tarde é bola em cima do corpo ou já passada do ponto (<see cref="Jogador.AtrasoDoContato"/>); baixo, rápido ou de
    /// revés (alto, principalmente) piora.
    /// <para>
    /// No controle de quatro botões (<see cref="Controle.QuatroBotoes"/>, a fatia no Unreal) a tabela acima não vale: o botão
    /// que começou o balanço escolhe a família e o analógico só o canto. <b>A</b>: Normal; segurado no contato, Ataque.
    /// <b>B</b>: Lob (sem chiquita). <b>X</b>: Bandeja na bola alta, Defesa (o slice) na baixa. <b>Y</b>: Ataque na bola
    /// baixa; na alta, Víbora com o RB segurado, SmashPor3/SmashPor4 com o Y segurado (como acima), senão Smash. O erro é o
    /// mesmo, e o golpe muito ruim também.
    /// </para>
    /// </summary>
    private Golpe GolpeDoHumano(Jogador jogador, Entrada entrada) =>
        EscolherGolpeDoHumano(jogador, entrada) with { Lado = jogador.LadoDoGolpePara(Bola) };

    private Golpe EscolherGolpeDoHumano(Jogador jogador, Entrada entrada)
    {
        var bola = Bola;
        int lado = jogador.Lado, ladoDoAlvo = -lado;
        bool manual = Opcoes.ModoDeGolpe == ModoDeGolpe.Manual;
        // Sem termo de tempo à parte: o timing está em onde a bola está no instante do contato. Cedo = ainda longe (esticado,
        // na DificuldadeDoGolpe); tarde = no corpo (idem) ou já passada do ponto — e isso a DificuldadeDoGolpe, simétrica, não
        // vê: o atraso em metros é o único termo a mais.
        float erro = Util.Limitar(PesoDoCorpoNoErro * jogador.DificuldadeDoGolpe(bola) + PesoDoAtraso * jogador.AtrasoDoContato(bola), 0, 1.6f);
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

        if (Opcoes.Controle == Controle.QuatroBotoes)
        {
            // O botão do balanço escolhe a família; a situação (bola alta ou baixa) escolhe o golpe dela; o analógico só mira.
            return jogador.IntencaoDoBalanco switch
            {
                IntencaoDoGolpe.Lob => Lob(),
                IntencaoDoGolpe.Cortado => bolaAlta ? Bandeja() : Defesa(),
                IntencaoDoGolpe.Ataque when !bolaAlta => Ataque(),
                IntencaoDoGolpe.Ataque when entrada.ModificadorSegurado => Vibora(),
                IntencaoDoGolpe.Ataque => (entrada.AtaqueSegurado ? RemateForte() : null) ?? Smash(),
                _ => entrada.AcaoSegurada ? Ataque() : Normal(),
            };
        }

        bool lob = manual ? jogador.BalancoDeLob : entrada.AcaoSegurada;
        bool acaoSegurada = manual ? entrada.AcaoSegurada && !jogador.BalancoDeLob : entrada.AcaoSegurada;

        if (bolaAlta && frente && acaoSegurada) return RemateForte() ?? Smash();
        if (lob)
        {
            if (frente && GolpesEspeciais.Chiquita(bola.X, bola.Y, bola.Z, ladoDoAlvo, x) is Golpe chiquita) return chiquita;
            return Lob();
        }
        if (tras && jogador.BolaAtrasJuntoAoVidro(bola) && GolpesEspeciais.Contrapared(bola.X, bola.Y, bola.Z, ladoDoAlvo, x) is Golpe contrapared)
            return contrapared;
        if (bolaAlta)
        {
            if (frente) return Smash();
            if (ladoForte) return Vibora();
            return Bandeja();
        }
        if (frente) return Ataque();
        if (tras) return Defesa();
        return Normal();

        // Cada golpe sorteia o ruído na ordem em que os argumentos aparecem (o PadelSim faz na mesma ordem).
        Golpe Smash() => new(x, ladoDoAlvo * (4.2f + Ruido()), 0.4f, TipoDeGolpe.Smash, Efeito: new Efeito(1500, 0));
        Golpe Lob() => new(x, ladoDoAlvo * (8.2f + Ruido() * 0.5f), 1.6f, TipoDeGolpe.Lob, Efeito: new Efeito(-400, 0));
        Golpe Vibora() => new(Util.Limitar(3.8f * paraOLado + Ruido(), -4.5f, 4.5f), ladoDoAlvo * (6.5f + Ruido()), 0.7f, TipoDeGolpe.Vibora, Efeito: new Efeito(-800, 1800 * paraOLado * ladoDoAlvo));
        Golpe Bandeja() => new(x, ladoDoAlvo * (7.5f + Ruido()), 0.95f, TipoDeGolpe.Bandeja, Efeito: new Efeito(-1500, 500));
        Golpe Ataque() => new(x, ladoDoAlvo * (4.5f + Ruido()), 0.6f, TipoDeGolpe.Ataque, Efeito: new Efeito(2200, 0));
        Golpe Defesa() => new(x, ladoDoAlvo * (7.8f + Ruido()), 1.05f, TipoDeGolpe.Defesa, Efeito: new Efeito(-1200, 0));
        Golpe Normal() => new(x, ladoDoAlvo * (6.6f + Ruido()), 0.8f, TipoDeGolpe.Normal, Efeito: new Efeito(1200, 0));
        // Por 4; com lado forte, por 3 pra aquele lado. Nulo se o contato foi ruim ou se o solucionador não acha (longe da rede).
        Golpe? RemateForte()
        {
            if (erro >= ErroMaximoDoRemateForte) return null;
            return ladoForte
                ? GolpesEspeciais.SmashPor3(bola.X, bola.Y, bola.Z, ladoDoAlvo, paraOLado)
                : GolpesEspeciais.SmashPor4(bola.X, bola.Y, bola.Z, ladoDoAlvo, x);
        }
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
            // Pelo Vencedor, não por quem ganhou o ponto: nos pontos corridos o último ponto pode ser de quem perdeu (13-10 → 13-11).
            TipoDeEventoDoPlacar.Partida => Placar.Vencedor switch
            {
                0 => $"A casa venceu! {Placar.Resumo()}",
                1 => $"Os rivais venceram. {Placar.Resumo()}",
                _ => $"Empate! {Placar.Resumo()}",
            },
            _ => $"{(casa ? "Ponto da casa" : "Ponto dos rivais")} — {descricao}",
        };
    }
}
