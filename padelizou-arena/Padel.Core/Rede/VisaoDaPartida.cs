namespace Padel.Core.Rede;

/// <summary>
/// Tudo o que um renderizador precisa pra desenhar um quadro e tocar o som dele — igual no host (a partida dele,
/// sem atraso) e no cliente (os outros interpolados 100 ms no passado, o próprio jogador predito agora).
/// Eventos: os que chegaram à hora de tocar desde o último ParaDesenhar, cada um uma vez só.
/// </summary>
public sealed class VisaoDaPartida
{
    /// <summary>O tick do host que está sendo desenhado (fracionário no cliente: entre dois ticks).</summary>
    public double Tick { get; init; }
    /// <summary>O tick do instantâneo de onde saíram placar, estado e mensagem.</summary>
    public uint TickDoEstado { get; init; }
    /// <summary>O jogador de quem desenha (0 no host; a vaga no cliente).</summary>
    public int IndiceLocal { get; init; } = -1;
    public EstadoDaPartida Estado { get; init; }
    public float Temporizador { get; init; }
    public EstadoDaBola Bola { get; init; }
    public IReadOnlyList<EstadoDoJogador> Jogadores { get; init; } = [];
    public EstadoDoPlacar Placar { get; init; } = EstadoDoPlacar.Inicial;
    public Mensagem? Mensagem { get; init; }
    public Caixa CaixaDoSaque { get; init; }
    public IReadOnlyList<EventoNumerado> Eventos { get; init; } = [];

    public static VisaoDaPartida De(Instantaneo i, double tick, int indiceLocal, IReadOnlyList<EventoNumerado> eventos) => new()
    {
        Tick = tick,
        TickDoEstado = i.Tick,
        IndiceLocal = indiceLocal,
        Estado = i.Estado,
        Temporizador = i.Temporizador,
        Bola = i.Bola,
        Jogadores = i.Jogadores,
        Placar = i.Placar,
        Mensagem = i.Mensagem,
        CaixaDoSaque = i.CaixaDoSaque,
        Eventos = eventos,
    };
}

/// <summary>
/// Uma entrada do próprio jogador confirmada pelo host: onde o cliente tinha predito que ele estaria depois dela e
/// onde o host diz que ele ficou. EstadoNaPredicao é o estado do instantâneo de onde a predição partiu.
/// </summary>
public readonly record struct Reconciliacao(uint Seq, uint Tick, float XPredito, float YPredito, float XAutoritativo, float YAutoritativo,
    EstadoDaPartida EstadoNaPredicao, EstadoDaPartida EstadoConfirmado)
{
    public float Erro => Util.Distancia(XPredito, YPredito, XAutoritativo, YAutoritativo);
}
