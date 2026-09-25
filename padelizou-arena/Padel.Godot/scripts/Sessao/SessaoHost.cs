using Padel.Core;
using Padel.Core.Rede;

namespace Padel.Godot;

/// <summary>
/// Online, hospedando: a partida autoritativa roda aqui (ServidorDaPartida), o host joga como jogador 0 com a
/// entrada local, e os clientes entram pela rede. Espera na sala até completar ou até o tempo de espera acabar;
/// vagas vazias ficam com a IA.
/// </summary>
public sealed class SessaoHost : ISessao
{
    private readonly TransporteEnet _transporte;
    private readonly ServidorDaPartida _servidor;
    private readonly RetratoDaVisao _mapa;
    private readonly double _esperar;
    private readonly int _porta;
    private double _naSala;
    private double _acumulado;
    private static readonly int[] Locais = [0];

    public SessaoHost(int porta, OpcoesDaPartida opcoes, string nome, double esperarSegundos)
    {
        _porta = porta;
        _esperar = esperarSegundos;
        _transporte = TransporteEnet.Hospedar(porta);
        _servidor = new ServidorDaPartida(_transporte, nome, opcoes);
        _mapa = new RetratoDaVisao(Retrato);
        Retrato.Mensagem = $"Sala aberta na porta {porta} — esperando jogadores";
        Retrato.MensagemSuave = true;
    }

    public TransporteEnet Transporte => _transporte;
    public ServidorDaPartida Servidor => _servidor;
    public IReadOnlyList<int> JogadoresLocais => Locais;
    public RetratoDaPartida Retrato { get; } = new();
    public bool Acabou => _servidor.Partida?.Acabou == true;

    /// <summary>A sala fechou e a partida nasceu — antes do primeiro passo dela (quem coleta estatística assina aqui).</summary>
    public event Action<Partida>? PartidaIniciada;

    public void Avancar(double delta, ReadOnlySpan<Entrada> entradas)
    {
        if (_servidor.Fase == FaseDoServidor.Sala)
        {
            _naSala += delta;
            int naSala = _servidor.Nomes.Count(n => !string.IsNullOrEmpty(n));
            Retrato.Mensagem = $"Sala aberta na porta {_porta} — {naSala} de 4 na sala, começa em {Math.Max(0, _esperar - _naSala):F0} s";
            if (_naSala >= _esperar || naSala >= 4)
            {
                var partida = _servidor.Iniciar();
                PartidaIniciada?.Invoke(partida);
                global::Godot.GD.Print($"Rede: partida iniciada com {naSala} na sala ({string.Join(", ", _servidor.Nomes.Where(n => !string.IsNullOrEmpty(n)))})");
            }
        }
        // Passo fixo do protocolo (1/120 s), independente do quadro.
        _acumulado += delta;
        var entradaDoHost = entradas.Length > 0 ? entradas[0] : Entrada.Vazia;
        bool primeiro = true;
        while (_acumulado >= Protocolo.Passo - 1e-6)
        {
            _servidor.Passo(primeiro ? entradaDoHost : entradaDoHost with { AcaoPressionada = false, LobPressionada = false });
            primeiro = false;
            _acumulado -= Protocolo.Passo;
        }
        if (_servidor.ParaDesenhar() is VisaoDaPartida visao) _mapa.Preencher(visao, _servidor.Nomes, null);
    }

    public EstadoVisivel? EstadoParaOBot() => _servidor.Partida is Partida p ? EstadoVisivel.De(p) : null;

    public string ResumoParaLog()
    {
        var p = _servidor.Partida;
        string placar = p is null ? "sem partida" : $"placar={p.Placar.Resumo()} {p.Placar.TextoDosPontos(0)}-{p.Placar.TextoDosPontos(1)} pontos={p.Estatisticas.Pontos} golpes={p.Estatisticas.Golpes}";
        string humanos = p is null ? "" : string.Join(" ", p.Jogadores.Select((j, i) => $"{i}:{(j.Humano ? "humano" : "IA")}/{j.Golpes}golpes"));
        return $"host tick={_servidor.Tick} {placar} jogadores={humanos} enviados={_transporte.PacotesEnviados} ({_transporte.BytesEnviados / 1024.0:F0} KiB) recebidos={_transporte.PacotesRecebidos}";
    }

    public void Dispose() => _transporte.Dispose();
}
