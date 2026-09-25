namespace Padel.Core.Rede;

public enum FaseDoCliente { Conectando, AguardandoResposta, NaSala, Jogando, Recusado, Desconectado }

/// <summary>
/// O cliente (D2): conecta, faz o aperto de mão e, jogando, a cada tick de 1/120 s manda a entrada (as 8 últimas,
/// com os apertos como contadores) e guarda os instantâneos do host. Pra desenhar:
/// - a bola e os outros jogadores vêm <see cref="AtrasoDeInterpolacao"/> no passado, entre dois instantâneos. A bola
///   não é interpolada em linha reta: é simulada com a mesma <see cref="Bola"/> do host a partir do último marco
///   (instantâneo ou descontinuidade), então quique, parede e efeito saem na trajetória exata;
/// - o próprio jogador é PREDITO agora: parte da última posição autoritativa e reaplica as entradas que o host ainda
///   não confirmou, com o mesmo <see cref="Jogador.Mover"/> e a mesma conversão de referencial da Partida.
/// O tick do servidor é estimado pelos instantâneos que chegam (os mais rápidos da janela de 2 s).
/// </summary>
public sealed class ClienteDaPartida
{
    private const int TamanhoDoHistorico = 256;          // entradas guardadas (≈ 2,1 s) pra reaplicar
    private const int MaximoDeReaplicacoes = 200;         // teto da predição: além disso o host sumiu de vez
    private const uint TicksNoBuffer = 2 * Protocolo.TicksPorSegundo;
    private const double JanelaDoRelogio = 2.0;           // segundos de amostras pra estimar o tick do servidor
    private const double SaltoDoRelogio = 12;             // erro (ticks) acima do qual o relógio salta em vez de deslizar
    private const double DeslizeDoRelogio = 0.05;         // até 5 % mais rápido ou mais devagar por tick
    private const int MaximoDeTicksSimulados = 60;         // extrapolação da bola sem instantâneo novo: até 0,5 s
    private const int MaximoDeEventosPendentes = 512;
    /// <summary>Correndo no máximo ~7 m/s: andar mais que isso entre dois instantâneos é teletransporte (ponto novo).</summary>
    private const float RapidezMaximaPlausivel = 8f;

    private readonly ITransporte _transporte;
    private readonly string _nome;
    private int? _parDoHost;
    private string[] _nomes = ["", "", "", ""];
    private bool[] _humanos = new bool[Protocolo.Jogadores];

    // Entradas mandadas, por seq % TamanhoDoHistorico.
    private readonly EntradaDeRede[] _enviadas = new EntradaDeRede[TamanhoDoHistorico];
    private readonly bool[] _apertouAcao = new bool[TamanhoDoHistorico];
    private readonly bool[] _apertouLob = new bool[TamanhoDoHistorico];
    private readonly double[] _enviadaEm = new double[TamanhoDoHistorico];
    private readonly (uint Seq, float X, float Y, EstadoDaPartida Estado)[] _preditas = new (uint, float, float, EstadoDaPartida)[TamanhoDoHistorico];
    private uint _seq;
    private byte _contadorDeAcao, _contadorDeLob;
    private uint _maiorConfirmada;

    // Instantâneos em ordem de tick, e os marcos da bola (instantâneos + descontinuidades).
    private readonly List<Instantaneo> _instantaneos = [];
    private readonly SortedList<uint, EstadoDaBola> _marcosDaBola = [];
    private readonly Bola _bolaDeTrabalho = new();
    private readonly List<EventoDaBola> _eventosDaBolaDeTrabalho = [];

    // Eventos: vistos (pra descartar a repetição) e esperando a hora de tocar.
    private readonly HashSet<uint> _eventosVistos = [];
    private readonly SortedList<uint, EventoNumerado> _eventosPendentes = [];
    private uint _pisoDosEventos, _maiorEventoVisto;

    // Relógio.
    private long _passos;
    private readonly Queue<(double Relogio, double Amostra)> _amostrasDoRelogio = new();
    private double _deslocamento;
    private bool _temRelogio;

    // Predição.
    private Jogador? _local;
    private EstadoDoJogador _predito;
    private bool _temPredito;

    public ClienteDaPartida(ITransporte transporte, string nome, float atrasoDeInterpolacao = 0.1f)
    {
        _transporte = transporte;
        _nome = nome;
        AtrasoDeInterpolacao = atrasoDeInterpolacao;
    }

    public FaseDoCliente Fase { get; private set; } = FaseDoCliente.Conectando;
    /// <summary>A vaga deste cliente (2, 1 ou 3); -1 antes do BemVindo.</summary>
    public int Indice { get; private set; } = -1;
    public OpcoesDaPartida? Opcoes { get; private set; }
    public uint Semente { get; private set; }
    public MotivoDaRecusa? MotivoDaRecusa { get; private set; }
    public byte? VersaoDoHost { get; private set; }
    public IReadOnlyList<string> Nomes => _nomes;
    public IReadOnlyList<bool> Humanos => _humanos;
    /// <summary>Quanto atrás do host os outros jogadores e a bola são desenhados, em segundos (padrão 0,1).</summary>
    public float AtrasoDeInterpolacao { get; set; }
    /// <summary>Segundos de relógio local (um Passo = 1/120 s).</summary>
    public double Relogio => _passos * (double)Protocolo.Passo;
    /// <summary>O instantâneo mais novo que chegou (por tick, não por ordem de chegada).</summary>
    public Instantaneo? UltimoInstantaneo => _instantaneos.Count > 0 ? _instantaneos[^1] : null;
    /// <summary>O tick do host agora, visto daqui: o dos instantâneos mais rápidos da janela, andando com o relógio local.</summary>
    public double TickEstimadoDoServidor => Relogio * Protocolo.TicksPorSegundo + _deslocamento;
    /// <summary>Ida e volta, em segundos, de uma entrada até a confirmação dela num instantâneo (média móvel).</summary>
    public double Ping { get; private set; }
    /// <summary>Cada entrada própria confirmada pelo host, com o erro da predição — pra teste e pro gráfico de rede.</summary>
    public event Action<Reconciliacao>? Reconciliou;

    /// <summary>O tick do host que se desenha agora: o estimado menos o atraso de interpolação. alfa = fração do próximo Passo.</summary>
    public double TickDeDesenho(float alfa = 0) => TickEstimadoDoServidor + alfa - AtrasoDeInterpolacao * Protocolo.TicksPorSegundo;

    /// <summary>Um tick de 1/120 s: rede e, jogando, a entrada deste tick (no referencial do jogador: Dy &lt; 0 é rumo à rede).</summary>
    public void Passo(Entrada entrada = default)
    {
        _passos++;
        ProcessarRede();
        if (Fase != FaseDoCliente.Jogando) return;
        AjustarRelogio();
        EnviarEntrada(entrada);
        Predizer();
    }

    /// <summary>Sai da sala ou da partida (o host libera a vaga ou põe a IA no lugar).</summary>
    public void Sair()
    {
        if (_parDoHost is int par) _transporte.Desconectar(par);
        if (Fase != FaseDoCliente.Recusado) Fase = FaseDoCliente.Desconectado;
    }

    // ───── rede ─────

    private void ProcessarRede()
    {
        _transporte.Processar();
        while (_transporte.TentarReceber(out var evento))
        {
            switch (evento.Tipo)
            {
                case TipoDeEventoDoTransporte.Conectou when _parDoHost is null:
                    _parDoHost = evento.Par;
                    _transporte.Enviar(evento.Par, Canal.Confiavel, Protocolo.EscreverOla(new Ola(_nome)));
                    Fase = FaseDoCliente.AguardandoResposta;
                    break;
                case TipoDeEventoDoTransporte.Desconectou when evento.Par == _parDoHost:
                    if (Fase != FaseDoCliente.Recusado) Fase = FaseDoCliente.Desconectado;
                    break;
                case TipoDeEventoDoTransporte.Desconectou when _parDoHost is null && Fase == FaseDoCliente.Conectando:
                    // O transporte desistiu antes de conectar: ninguém hospedando no endereço, ou o host recusou no
                    // nível do transporte (vagas cheias). Sem isto o cliente ficava "Conectando" pra sempre.
                    Fase = FaseDoCliente.Desconectado;
                    break;
                case TipoDeEventoDoTransporte.Pacote when evento.Par == _parDoHost:
                    Receber(evento.Par, evento.Dados.Span);
                    break;
                default:
                    break;
            }
        }
    }

    private void Receber(int par, ReadOnlySpan<byte> dados)
    {
        if (!Protocolo.TentarLerTipo(dados, out var tipo)) return;
        switch (tipo)
        {
            case TipoDeMensagem.Instantaneo when Fase == FaseDoCliente.Jogando && Protocolo.TentarLerInstantaneo(dados, out var instantaneo):
                ReceberInstantaneo(instantaneo);
                break;
            case TipoDeMensagem.BemVindo when Protocolo.TentarLerBemVindo(dados, out var bemVindo):
                Indice = bemVindo.Indice;
                Opcoes = bemVindo.Opcoes;
                Semente = bemVindo.Semente;
                if (Fase == FaseDoCliente.AguardandoResposta) Fase = FaseDoCliente.NaSala;
                break;
            case TipoDeMensagem.Recusado when Protocolo.TentarLerRecusado(dados, out var recusado):
                MotivoDaRecusa = recusado.Motivo;
                VersaoDoHost = recusado.VersaoDoHost;
                Fase = FaseDoCliente.Recusado;
                _transporte.Desconectar(par);
                break;
            case TipoDeMensagem.Sala when Protocolo.TentarLerSala(dados, out var sala):
                _nomes = sala.Nomes;
                break;
            case TipoDeMensagem.Comecou when Protocolo.TentarLerComecou(dados, out var comecou):
                _nomes = comecou.Nomes;
                _humanos = comecou.Humanos;
                Fase = FaseDoCliente.Jogando;
                break;
            default:
                break;
        }
    }

    private void ReceberInstantaneo(Instantaneo instantaneo)
    {
        // Relógio: quanto o tick do host está à frente do relógio local, pelo que acabou de chegar.
        _amostrasDoRelogio.Enqueue((Relogio, instantaneo.Tick - Relogio * Protocolo.TicksPorSegundo));
        while (_amostrasDoRelogio.Peek().Relogio < Relogio - JanelaDoRelogio) _amostrasDoRelogio.Dequeue();
        if (!_temRelogio) { _deslocamento = AlvoDoRelogio(); _temRelogio = true; }

        // Eventos de qualquer instantâneo, mesmo atrasado: id novo entra, id visto é repetição e sai.
        foreach (var e in instantaneo.Eventos)
        {
            if (e.Id <= _pisoDosEventos || !_eventosVistos.Add(e.Id)) continue;
            _maiorEventoVisto = Math.Max(_maiorEventoVisto, e.Id);
            _eventosPendentes[e.Id] = e;
        }
        PodarEventos();

        // Buffer por tick; repetido (pacote duplicado) ou velho demais não entra.
        int i = _instantaneos.Count;
        while (i > 0 && _instantaneos[i - 1].Tick > instantaneo.Tick) i--;
        if (i > 0 && _instantaneos[i - 1].Tick == instantaneo.Tick) return;
        uint maisNovo = Math.Max(instantaneo.Tick, UltimoInstantaneo?.Tick ?? 0);
        if (instantaneo.Tick + TicksNoBuffer < maisNovo) return;
        bool eOMaisNovo = i == _instantaneos.Count;   // decidido antes da poda, que desloca os índices
        _instantaneos.Insert(i, instantaneo);
        while (_instantaneos[0].Tick + TicksNoBuffer < maisNovo) _instantaneos.RemoveAt(0);

        _marcosDaBola[instantaneo.Tick] = instantaneo.Bola;
        foreach (var d in instantaneo.Descontinuidades) _marcosDaBola[d.Tick] = d.Bola;
        while (_marcosDaBola.Count > 0 && _marcosDaBola.Keys[0] + TicksNoBuffer < maisNovo) _marcosDaBola.RemoveAt(0);

        if (eOMaisNovo) Reconciliar(instantaneo);
    }

    private void PodarEventos()
    {
        // atalho: ninguém chamando ParaDesenhar (cliente sem tela) — guarda só os 512 mais recentes.
        while (_eventosPendentes.Count > MaximoDeEventosPendentes) _eventosPendentes.RemoveAt(0);
        if (_eventosVistos.Count <= 4096) return;
        _pisoDosEventos = _maiorEventoVisto > 1024 ? _maiorEventoVisto - 1024 : 0;   // ids abaixo do piso já saíram da janela de repetição há muito
        _eventosVistos.RemoveWhere(id => id <= _pisoDosEventos);
    }

    /// <summary>A entrada que o host acabou de confirmar: onde a predição tinha posto o jogador e onde ele ficou.</summary>
    private void Reconciliar(Instantaneo instantaneo)
    {
        if (Indice < 0 || instantaneo.SeqConfirmada(Indice) is not uint seq || seq <= _maiorConfirmada) return;
        _maiorConfirmada = seq;
        int i = (int)(seq % TamanhoDoHistorico);
        if (_enviadas[i].Seq == seq)
        {
            double idaEVolta = Relogio - _enviadaEm[i];
            Ping = Ping == 0 ? idaEVolta : Ping + (idaEVolta - Ping) * 0.1;
        }
        var predita = _preditas[i];
        if (predita.Seq != seq) return;
        var autoritativo = instantaneo.Jogadores[Indice];
        Reconciliou?.Invoke(new Reconciliacao(seq, instantaneo.Tick, predita.X, predita.Y, autoritativo.X, autoritativo.Y, predita.Estado, instantaneo.Estado));
    }

    // ───── relógio ─────

    private double AlvoDoRelogio()
    {
        double maior = double.NegativeInfinity;
        foreach (var (_, amostra) in _amostrasDoRelogio) maior = Math.Max(maior, amostra);
        return maior;
    }

    /// <summary>Desliza o tick estimado rumo ao alvo (no máximo 5 % de velocidade) — o desenho não dá solavanco; erro grande salta.</summary>
    private void AjustarRelogio()
    {
        if (!_temRelogio || _amostrasDoRelogio.Count == 0) return;
        double diferenca = AlvoDoRelogio() - _deslocamento;
        _deslocamento += Math.Abs(diferenca) > SaltoDoRelogio ? diferenca : Math.Clamp(diferenca, -DeslizeDoRelogio, DeslizeDoRelogio);
    }

    // ───── entrada e predição ─────

    private void EnviarEntrada(Entrada entrada)
    {
        if (_parDoHost is not int par) return;
        _seq++;
        if (entrada.AcaoPressionada) _contadorDeAcao++;
        if (entrada.LobPressionada) _contadorDeLob++;
        int i = (int)(_seq % TamanhoDoHistorico);
        _enviadas[i] = new EntradaDeRede(_seq, EntradaDeRede.Quantizar(entrada.Dx), EntradaDeRede.Quantizar(entrada.Dy), entrada.AcaoSegurada, _contadorDeAcao, _contadorDeLob);
        _apertouAcao[i] = entrada.AcaoPressionada;
        _apertouLob[i] = entrada.LobPressionada;
        _enviadaEm[i] = Relogio;

        int n = (int)Math.Min(_seq, (uint)Protocolo.EntradasPorPacote);
        Span<EntradaDeRede> ultimas = stackalloc EntradaDeRede[n];
        for (int k = 0; k < n; k++) ultimas[k] = _enviadas[(int)((_seq - (uint)(n - 1 - k)) % TamanhoDoHistorico)];
        _transporte.Enviar(par, Canal.NaoConfiavel, Protocolo.EscreverEntradas(ultimas));
    }

    /// <summary>
    /// Parte do último estado autoritativo do próprio jogador e reaplica as entradas que o host ainda não confirmou,
    /// fazendo o que a Partida faz com um humano em cada tick: AvancarTempo; no saque, o temporizador e o aperto do
    /// sacador soltam o saque (o jogador só anda a partir do tick seguinte); no rally, Mover no referencial do mundo
    /// (entrada × Lado) e, no modo manual, raquete no ar e início do balanço. Golpe e fim de ponto dependem da bola
    /// e não são preditos: a próxima confirmação corrige.
    /// </summary>
    private void Predizer()
    {
        var baseDaPredicao = UltimoInstantaneo;
        _temPredito = false;
        if (baseDaPredicao is null || Indice < 0) return;
        var autoritativo = baseDaPredicao.Jogadores[Indice];
        if (!autoritativo.Humano) return;   // o host pôs a IA no lugar: nada a predizer

        var j = _local ??= new Jogador(Indice / 2, Indice % 2, _nome, humano: true, Jogador.VelocidadeDoHumano);
        CopiarEstado(j, autoritativo);
        var estado = baseDaPredicao.Estado;
        float temporizador = baseDaPredicao.Temporizador;
        var sacador = baseDaPredicao.Placar.Sacador;
        bool souOSacador = sacador.Time * 2 + sacador.Jogador == Indice;
        bool manual = (Opcoes?.ModoDeGolpe ?? ModoDeGolpe.Manual) == ModoDeGolpe.Manual;
        const float dt = Protocolo.Passo;

        uint confirmada = baseDaPredicao.SeqConfirmada(Indice) ?? 0;
        uint primeira = Math.Max(confirmada + 1, _seq > MaximoDeReaplicacoes ? _seq - MaximoDeReaplicacoes + 1 : 1);
        for (uint s = primeira; s <= _seq; s++)
        {
            int i = (int)(s % TamanhoDoHistorico);
            var e = _enviadas[i];
            if (e.Seq != s) continue;
            j.AvancarTempo(dt);
            switch (estado)
            {
                case EstadoDaPartida.Saque:
                    temporizador -= dt;
                    if ((souOSacador && _apertouAcao[i]) || temporizador <= 0) estado = EstadoDaPartida.Rally;
                    break;
                case EstadoDaPartida.Rally:
                    j.Mover(e.DirecaoX * j.Lado, e.DirecaoY * j.Lado, dt);   // do referencial do jogador pro mundo, como a Partida
                    if (!manual) break;
                    if (j.BalancoTerminouNoAr) j.Cooldown = 0.45f;   // o mesmo recomposto de Partida.Rally
                    if ((_apertouAcao[i] || _apertouLob[i]) && !j.Balancando && j.Cooldown <= 0) j.IniciarBalanco(lob: _apertouLob[i]);
                    break;
                default:
                    break;   // fim de ponto e fim de partida: humano parado
            }
        }
        _preditas[(int)(_seq % TamanhoDoHistorico)] = (_seq, j.X, j.Y, baseDaPredicao.Estado);
        _predito = EstadoDoJogador.De(j);
        _temPredito = true;
    }

    /// <summary>Põe o jogador local no estado autoritativo. O balanço só se ajusta pelos métodos públicos: começa e avança.</summary>
    private static void CopiarEstado(Jogador j, EstadoDoJogador a)
    {
        j.X = a.X; j.Y = a.Y; j.Vx = a.Vx; j.Vy = a.Vy;
        j.AvancarTempo(0);   // limpa o "balanço terminou no ar" da reaplicação anterior
        j.IniciarBalanco(a.BalancoDeLob);
        if (a.Balancando) j.AvancarTempo(MathF.Max(0, Jogador.DuracaoDoBalanco - a.Balanco));
        else j.EncerrarBalanco();
        j.Cooldown = a.Cooldown;
    }

    // ───── desenho ─────

    /// <summary>
    /// A partida no tick de servidor dado, reconstruída dos instantâneos: bola simulada a partir do último marco,
    /// jogadores interpolados (teletransporte de ponto novo não é arrastado), placar/estado/mensagem do instantâneo
    /// anterior. Não mexe em nada (não entrega eventos, não usa a predição). Null antes do primeiro instantâneo.
    /// </summary>
    public VisaoDaPartida? Visao(double tickDoServidor)
    {
        if (_instantaneos.Count == 0) return null;
        int iB = 0;
        while (iB < _instantaneos.Count && _instantaneos[iB].Tick <= tickDoServidor) iB++;
        var a = iB > 0 ? _instantaneos[iB - 1] : _instantaneos[0];
        var b = iB > 0 && iB < _instantaneos.Count ? _instantaneos[iB] : null;
        var jogadores = new EstadoDoJogador[Protocolo.Jogadores];
        for (int k = 0; k < Protocolo.Jogadores; k++) jogadores[k] = JogadorNo(a, b, k, tickDoServidor);
        return new VisaoDaPartida
        {
            Tick = tickDoServidor,
            TickDoEstado = a.Tick,
            IndiceLocal = Indice,
            Estado = a.Estado,
            Temporizador = a.Temporizador,
            Bola = BolaNo(tickDoServidor),
            Jogadores = jogadores,
            Placar = a.Placar,
            Mensagem = a.Mensagem,
            CaixaDoSaque = a.CaixaDoSaque,
        };
    }

    /// <summary>O quadro de agora: os outros no <see cref="TickDeDesenho"/>, o próprio jogador predito, e os eventos que chegaram à hora de tocar.</summary>
    public VisaoDaPartida? ParaDesenhar(float alfa = 0)
    {
        double tick = TickDeDesenho(alfa);
        var visao = Visao(tick);
        if (visao is null) return null;
        var eventos = new List<EventoNumerado>();
        while (_eventosPendentes.Count > 0 && _eventosPendentes.Values[0].Tick <= tick)
        {
            eventos.Add(_eventosPendentes.Values[0]);
            _eventosPendentes.RemoveAt(0);
        }
        var jogadores = visao.Jogadores;
        if (_temPredito && Indice >= 0)
        {
            var copia = jogadores.ToArray();
            copia[Indice] = _predito with { X = _predito.X + _predito.Vx * alfa * Protocolo.Passo, Y = _predito.Y + _predito.Vy * alfa * Protocolo.Passo };
            jogadores = copia;
        }
        return new VisaoDaPartida
        {
            Tick = visao.Tick,
            TickDoEstado = visao.TickDoEstado,
            IndiceLocal = visao.IndiceLocal,
            Estado = visao.Estado,
            Temporizador = visao.Temporizador,
            Bola = visao.Bola,
            Jogadores = jogadores,
            Placar = visao.Placar,
            Mensagem = visao.Mensagem,
            CaixaDoSaque = visao.CaixaDoSaque,
            Eventos = eventos,
        };
    }

    /// <summary>A bola no tick t: parte do último marco até t (instantâneo ou descontinuidade) e simula com a física do host.</summary>
    private EstadoDaBola BolaNo(double t)
    {
        var marcos = _marcosDaBola;
        int i = marcos.Count - 1;
        while (i > 0 && marcos.Keys[i] > t) i--;
        uint tickDoMarco = marcos.Keys[i];
        var marco = marcos.Values[i];
        if (tickDoMarco >= t) return marco;
        marco.Aplicar(_bolaDeTrabalho);
        double decorrido = t - tickDoMarco;
        int inteiros = (int)Math.Floor(decorrido);
        double fracao = decorrido - inteiros;
        if (inteiros >= MaximoDeTicksSimulados) { inteiros = MaximoDeTicksSimulados; fracao = 0; }
        for (int k = 0; k < inteiros; k++)
        {
            _bolaDeTrabalho.Avancar(Protocolo.Passo, _eventosDaBolaDeTrabalho);
            _eventosDaBolaDeTrabalho.Clear();
        }
        if (fracao > 1e-6) _bolaDeTrabalho.Avancar((float)(fracao * Protocolo.Passo), _eventosDaBolaDeTrabalho);
        _eventosDaBolaDeTrabalho.Clear();
        return EstadoDaBola.De(_bolaDeTrabalho);
    }

    private static EstadoDoJogador JogadorNo(Instantaneo a, Instantaneo? b, int k, double t)
    {
        var ja = a.Jogadores[k];
        if (b is null || t <= a.Tick)
        {
            // Sem o instantâneo seguinte: segue a velocidade por até 0,25 s e para.
            double adiante = Math.Clamp(t - a.Tick, 0, Protocolo.TicksPorSegundo / 4) / Protocolo.TicksPorSegundo;
            return ja with
            {
                X = ja.X + ja.Vx * (float)adiante,
                Y = ja.Y + ja.Vy * (float)adiante,
                Balanco = MathF.Max(0, ja.Balanco - (float)adiante),
                TempoNoBalanco = ja.Balancando ? ja.TempoNoBalanco + MathF.Min((float)adiante, ja.Balanco) : ja.TempoNoBalanco,
            };
        }
        var jb = b.Jogadores[k];
        double intervalo = b.Tick - a.Tick;
        float s = (float)((t - a.Tick) / intervalo);
        float distancia = Util.Distancia(ja.X, ja.Y, jb.X, jb.Y);
        if (distancia > RapidezMaximaPlausivel * intervalo / Protocolo.TicksPorSegundo + 0.3f) return s < 0.5f ? ja : jb;

        float faltam = (float)((b.Tick - t) / Protocolo.TicksPorSegundo);
        float passaram = (float)((t - a.Tick) / Protocolo.TicksPorSegundo);
        float balanco, tempo;
        bool lob;
        if (jb.Balancando && jb.TempoNoBalanco >= faltam)
        {
            // O balanço que aparece em b já tinha começado em t: conta pra trás a partir de b.
            balanco = jb.Balanco + faltam;
            tempo = jb.TempoNoBalanco - faltam;
            lob = jb.BalancoDeLob;
        }
        else
        {
            balanco = MathF.Max(0, ja.Balanco - passaram);
            tempo = ja.Balancando ? ja.TempoNoBalanco + MathF.Min(passaram, ja.Balanco) : ja.TempoNoBalanco;
            lob = ja.BalancoDeLob;
        }
        return new EstadoDoJogador(
            ja.X + (jb.X - ja.X) * s, ja.Y + (jb.Y - ja.Y) * s,
            ja.Vx + (jb.Vx - ja.Vx) * s, ja.Vy + (jb.Vy - ja.Vy) * s,
            balanco, tempo, lob, ja.Humano, MathF.Max(0, ja.Cooldown - passaram));
    }
}
