namespace Padel.Core.Rede;

public enum FaseDoServidor { Sala, Jogando }

/// <summary>
/// O host (D2): dono da única Partida de verdade. Na sala aceita Ola e distribui as vagas — o host é sempre o
/// jogador 0; os clientes ocupam 2, 1 e 3 nessa ordem (rival primeiro: 1x1 online é o caso mais comum). Iniciar()
/// cria a partida com humano onde há gente e IA no resto. Jogando, cada Passo é um tick de 1/120 s: aplica a entrada
/// de cada humano remoto (<see cref="FilaDeEntradas"/>), avança a partida, numera os eventos, marca as
/// descontinuidades da bola e, a cada 4 ticks (30 Hz), manda um instantâneo pelo canal não confiável. Um par que
/// some por 3 s (ou desconecta) vira IA e a partida segue.
/// </summary>
public sealed class ServidorDaPartida
{
    private static readonly int[] OrdemDasVagas = [2, 1, 3];
    private const int TamanhoMaximoDoNome = 32;

    private sealed class Remoto(int par, int indice, string nome)
    {
        public int Par { get; } = par;
        public int Indice { get; } = indice;
        public string Nome { get; } = nome;
        public FilaDeEntradas Fila { get; } = new();
        public double UltimoPacote { get; set; }
        public bool Conectado { get; set; } = true;
    }

    /// <summary>Eventos guardados pro desenho do próprio host se ninguém chamar ParaDesenhar (servidor sem tela).</summary>
    private const int MaximoDeEventosParaODesenho = 512;

    private readonly ITransporte _transporte;
    private readonly Dictionary<int, Remoto> _porPar = [];
    private readonly Remoto?[] _porVaga = new Remoto?[Protocolo.Jogadores];
    private readonly string[] _nomes = ["", "", "", ""];
    private readonly Entrada[] _entradas = new Entrada[Protocolo.Jogadores];
    private readonly List<EventoNumerado> _eventosRecentes = [];
    private readonly List<EventoNumerado> _eventosParaODesenho = [];
    private readonly List<Descontinuidade> _descontinuidades = [];
    private readonly Bola _sombra = new();
    private readonly List<EventoDaBola> _eventosDaSombra = [];
    private double _relogio;
    private uint _ultimoIdDeEvento;
    private uint _tickEmCurso;

    public ServidorDaPartida(ITransporte transporte, string nomeDoHost, OpcoesDaPartida? opcoes = null)
    {
        _transporte = transporte;
        var o = opcoes ?? new OpcoesDaPartida();
        Semente = o.Semente ?? (uint)Random.Shared.Next();   // sorteada aqui, e não na Partida, pra ir no BemVindo
        Opcoes = o with { Semente = Semente };
        _nomes[0] = NomeLimpo(nomeDoHost, 0);
    }

    public FaseDoServidor Fase { get; private set; } = FaseDoServidor.Sala;
    public OpcoesDaPartida Opcoes { get; }
    public uint Semente { get; }
    public Partida? Partida { get; private set; }
    /// <summary>Nome em cada vaga; "" = vaga livre (vira IA).</summary>
    public IReadOnlyList<string> Nomes => _nomes;
    /// <summary>Ticks de 1/120 s desde o Iniciar.</summary>
    public uint Tick { get; private set; }

    /// <summary>A cada tick, a entrada que cada jogador humano (host incluído) deu à partida — com apertos já reconstruídos.</summary>
    public event Action<int, Entrada>? EntradaAplicada;
    /// <summary>Cada evento da partida, já numerado, no tick em que aconteceu.</summary>
    public event Action<EventoNumerado>? EventoRegistrado;
    /// <summary>Um jogador remoto sumiu (3 s sem pacote ou desconexão) e a IA assumiu.</summary>
    public event Action<int>? JogadorVirouIA;

    private static string NomeLimpo(string nome, int vaga)
    {
        string limpo = new string(nome.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (limpo.Length > TamanhoMaximoDoNome) limpo = limpo[..TamanhoMaximoDoNome].TrimEnd();
        return limpo.Length > 0 ? limpo : $"Jogador {vaga + 1}";
    }

    /// <summary>Cria a partida com quem está na sala (vaga vazia = IA) e avisa os clientes.</summary>
    public Partida Iniciar()
    {
        if (Fase != FaseDoServidor.Sala) throw new InvalidOperationException("a partida já começou");
        bool[] humanos = [true, _porVaga[1] is not null, _porVaga[2] is not null, _porVaga[3] is not null];
        var partida = new Partida(Opcoes with { Humanos = humanos });
        Partida = partida;
        Fase = FaseDoServidor.Jogando;
        Tick = 0;
        _tickEmCurso = 0;
        foreach (var r in _porPar.Values) r.UltimoPacote = _relogio;
        partida.Evento += Registrar;
        // O construtor da Partida já emitiu o SaquePreparado do primeiro ponto, antes de alguém poder assinar o
        // evento: registra aqui o mesmo evento que IniciarPonto emite, pra o primeiro saque ter som e animação.
        var sacador = partida.Sacador;
        Registrar(new EventoDaPartida(TipoDeEventoDaPartida.SaquePreparado, sacador, sacador.Time));
        EstadoDaBola.De(partida.Bola).Aplicar(_sombra);
        EnviarATodos(Canal.Confiavel, Protocolo.EscreverComecou(new Comecou(humanos, [.. _nomes])));
        return partida;
    }

    /// <summary>Um tick de 1/120 s: rede, e — jogando — a partida.</summary>
    public void Passo(Entrada entradaDoHost = default)
    {
        _relogio += Protocolo.Passo;
        ProcessarRede();
        if (Fase != FaseDoServidor.Jogando || Partida is not Partida partida) return;

        ConferirSilencio(partida);
        _entradas[0] = entradaDoHost;
        for (int vaga = 1; vaga < Protocolo.Jogadores; vaga++)
        {
            var remoto = _porVaga[vaga];
            _entradas[vaga] = remoto is not null && partida.Jogadores[vaga].Humano ? remoto.Fila.Proxima() : Entrada.Vazia;
        }
        _tickEmCurso = Tick + 1;
        partida.Avancar(Protocolo.Passo, _entradas);
        Tick = _tickEmCurso;
        for (int i = 0; i < Protocolo.Jogadores; i++)
            if (partida.Jogadores[i].Humano) EntradaAplicada?.Invoke(i, _entradas[i]);

        MarcarDescontinuidade(partida.Bola);
        _eventosRecentes.RemoveAll(e => e.Tick + Protocolo.TicksDeRepeticaoDeEventos <= Tick);
        _descontinuidades.RemoveAll(d => d.Tick + Protocolo.TicksDeRepeticaoDeEventos <= Tick);
        if (Tick % Protocolo.TicksPorInstantaneo == 0) EnviarInstantaneo(partida);
    }

    /// <summary>Como estão chegando as entradas do jogador remoto desta vaga (pro gráfico de rede e pra teste); null se a vaga é da IA.</summary>
    public EstatisticasDeEntrada? EntradasDe(int indice)
    {
        if (indice is < 1 or >= Protocolo.Jogadores || _porVaga[indice] is not Remoto remoto) return null;
        var f = remoto.Fila;
        return new EstatisticasDeEntrada(f.Acumuladas, f.TicksSemEntrada, f.EntradasPerdidas, f.EntradasPuladas, f.UltimaSeqProcessada);
    }

    /// <summary>O quadro do próprio host: a partida agora, sem atraso, e os eventos desde a última chamada.</summary>
    public VisaoDaPartida? ParaDesenhar()
    {
        if (Partida is not Partida partida) return null;
        var eventos = _eventosParaODesenho.ToArray();
        _eventosParaODesenho.Clear();
        return VisaoDaPartida.De(Instantaneo.Capturar(partida, Tick), Tick, indiceLocal: 0, eventos);
    }

    private void Registrar(EventoDaPartida evento)
    {
        var numerado = EventoNumerado.De(evento, ++_ultimoIdDeEvento, _tickEmCurso);
        _eventosRecentes.Add(numerado);
        _eventosParaODesenho.Add(numerado);
        // atalho: host sem tela não chama ParaDesenhar; guarda só os últimos 512 (≈ 20 s de jogo) pra não crescer sem fim.
        if (_eventosParaODesenho.Count > MaximoDeEventosParaODesenho) _eventosParaODesenho.RemoveAt(0);
        EventoRegistrado?.Invoke(numerado);
    }

    /// <summary>
    /// Compara a bola com uma sombra que só obedeceu à física: se diferem, algo de fora mexeu nela neste tick
    /// (golpe, saque, bola recolocada ou morta) e o estado novo vira marco pro cliente simular a partir dele.
    /// </summary>
    private void MarcarDescontinuidade(Bola bola)
    {
        _sombra.Avancar(Protocolo.Passo, _eventosDaSombra);
        _eventosDaSombra.Clear();
        var real = EstadoDaBola.De(bola);
        var sombra = EstadoDaBola.De(_sombra);
        bool igual = real.EmJogo == sombra.EmJogo && real.Rolando == sombra.Rolando && real.Parada == sombra.Parada
            && real.DistanciaAte(sombra) < 1e-4f
            && MathF.Abs(real.Vx - sombra.Vx) + MathF.Abs(real.Vy - sombra.Vy) + MathF.Abs(real.Vz - sombra.Vz) < 1e-3f
            && MathF.Abs(real.Wx - sombra.Wx) + MathF.Abs(real.Wy - sombra.Wy) + MathF.Abs(real.Wz - sombra.Wz) < 1e-2f;
        if (!igual)
        {
            _descontinuidades.Add(new Descontinuidade(Tick, real));
            // teto: 3 marcos por 0,5 s (golpe, fim do ponto, recolocação). Um 4º empurra o mais velho pra fora; sem ele o
            // cliente simula a partir do instantâneo seguinte, e o erro fica limitado aos até 4 ticks entre os dois.
            if (_descontinuidades.Count > Protocolo.MaximoDeDescontinuidades) _descontinuidades.RemoveAt(0);
        }
        real.Aplicar(_sombra);
    }

    private void EnviarInstantaneo(Partida partida)
    {
        var instantaneo = Instantaneo.Capturar(partida, Tick);
        foreach (var remoto in _porVaga)
            if (remoto is not null && remoto.Fila.RecebeuAlguma) instantaneo.Confirmacoes.Add(new Confirmacao(remoto.Indice, remoto.Fila.UltimaSeqProcessada));
        // teto: 32 eventos por 0,5 s (um rally gera menos de 10). Acima disso os mais velhos saem antes das ~15
        // repetições e podem se perder com perda de pacote; a saída é subir MaximoDeEventos (custa 7 B cada).
        int pular = Math.Max(0, _eventosRecentes.Count - Protocolo.MaximoDeEventos);
        instantaneo.Eventos.AddRange(_eventosRecentes.Skip(pular));
        instantaneo.Descontinuidades.AddRange(_descontinuidades);
        EnviarATodos(Canal.NaoConfiavel, Protocolo.EscreverInstantaneo(instantaneo));
    }

    /// <summary>Quem desconectou ou está calado há 3 s vira IA: a partida não para por causa da rede de um.</summary>
    // atalho: quem vira IA não volta, mesmo que os pacotes voltem a chegar — reconexão (Ola de novo com um token da
    // vaga) entra se o playtest pedir. E o jogador assumido fica com o Alcance de humano (1,35 m): Alcance só se
    // define no construtor do Jogador, que não é desta pasta; a saída é um setter lá.
    private void ConferirSilencio(Partida partida)
    {
        foreach (var remoto in _porVaga)
        {
            if (remoto is null) continue;
            var jogador = partida.Jogadores[remoto.Indice];
            if (!jogador.Humano) continue;
            if (remoto.Conectado && _relogio - remoto.UltimoPacote < Protocolo.SegundosAteVirarIA) continue;
            jogador.Humano = false;
            jogador.Velocidade = partida.IAs[jogador.Time].Perfil.Velocidade;
            JogadorVirouIA?.Invoke(remoto.Indice);
        }
    }

    private void EnviarATodos(Canal canal, byte[] pacote)
    {
        foreach (var r in _porPar.Values) if (r.Conectado) _transporte.Enviar(r.Par, canal, pacote);
    }

    private void ProcessarRede()
    {
        _transporte.Processar();
        while (_transporte.TentarReceber(out var evento))
        {
            switch (evento.Tipo)
            {
                case TipoDeEventoDoTransporte.Desconectou:
                    Saiu(evento.Par);
                    break;
                case TipoDeEventoDoTransporte.Pacote:
                    Receber(evento.Par, evento.Dados.Span);
                    break;
                default:
                    break;   // Conectou: espera o Ola
            }
        }
    }

    private void Receber(int par, ReadOnlySpan<byte> dados)
    {
        if (!Protocolo.TentarLerTipo(dados, out var tipo)) return;   // corrompido: a rede é assim, segue
        _porPar.TryGetValue(par, out var remoto);
        if (remoto is not null) remoto.UltimoPacote = _relogio;
        switch (tipo)
        {
            case TipoDeMensagem.Ola when Protocolo.TentarLerOla(dados, out var ola):
                ReceberOla(par, ola);
                break;
            case TipoDeMensagem.Entradas when remoto is not null && Protocolo.TentarLerEntradas(dados, out var entradas):
                remoto.Fila.Receber(entradas);
                break;
            default:
                break;
        }
    }

    private void ReceberOla(int par, Ola ola)
    {
        if (ola.Versao != Protocolo.Versao) { Recusar(par, MotivoDaRecusa.VersaoIncompativel); return; }
        if (_porPar.TryGetValue(par, out var jaNaSala)) { EnviarBemVindo(jaNaSala); return; }   // Ola repetido: a mesma vaga
        if (Fase != FaseDoServidor.Sala) { Recusar(par, MotivoDaRecusa.PartidaEmAndamento); return; }
        int vaga = -1;
        foreach (int v in OrdemDasVagas) if (_porVaga[v] is null) { vaga = v; break; }
        if (vaga < 0) { Recusar(par, MotivoDaRecusa.SalaCheia); return; }
        var remoto = new Remoto(par, vaga, NomeLimpo(ola.Nome, vaga)) { UltimoPacote = _relogio };
        _porPar[par] = remoto;
        _porVaga[vaga] = remoto;
        _nomes[vaga] = remoto.Nome;
        EnviarBemVindo(remoto);
        EnviarATodos(Canal.Confiavel, Protocolo.EscreverSala(new Sala([.. _nomes])));
    }

    private void EnviarBemVindo(Remoto remoto) =>
        _transporte.Enviar(remoto.Par, Canal.Confiavel, Protocolo.EscreverBemVindo(new BemVindo(remoto.Indice, Opcoes, Semente)));

    private void Recusar(int par, MotivoDaRecusa motivo) =>
        _transporte.Enviar(par, Canal.Confiavel, Protocolo.EscreverRecusado(new Recusado(motivo)));

    private void Saiu(int par)
    {
        if (!_porPar.TryGetValue(par, out var remoto)) return;
        remoto.Conectado = false;
        if (Fase == FaseDoServidor.Sala)
        {
            _porPar.Remove(par);
            _porVaga[remoto.Indice] = null;
            _nomes[remoto.Indice] = "";
            EnviarATodos(Canal.Confiavel, Protocolo.EscreverSala(new Sala([.. _nomes])));
        }
    }
}
