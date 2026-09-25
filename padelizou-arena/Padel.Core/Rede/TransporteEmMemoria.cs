namespace Padel.Core.Rede;

/// <summary>
/// Como a rede em memória se comporta, em segundos e probabilidades (0..1). Latência é de ida (150 ms de ida e
/// volta = 0,075). Cada pacote leva Latencia + [0, Jitter) e, com chance Reordenacao, AtrasoDaReordenacao a mais —
/// o que o faz chegar depois de pacotes mandados depois dele. Perda e duplicação só no canal não confiável.
/// </summary>
public sealed record CondicoesDaRede
{
    public double Latencia { get; init; }
    public double Jitter { get; init; }
    public double Perda { get; init; }
    public double Duplicacao { get; init; }
    public double Reordenacao { get; init; }
    public double AtrasoDaReordenacao { get; init; } = 0.03;

    public static readonly CondicoesDaRede Perfeita = new();
}

/// <summary>O que quem escuta a rede (<see cref="RedeEmMemoria.AoEnviar"/>) vê a cada envio: de que ponto, pra qual, por onde e quanto.</summary>
public readonly record struct PacoteNaRede(int De, int Para, Canal Canal, ReadOnlyMemory<byte> Dados);

/// <summary>
/// Uma rede de mentira pra teste: pontos ligados, relógio virtual que só anda quando o teste manda, e
/// latência/jitter/perda/duplicação/reordenação sorteados por um <see cref="Aleatorio"/> com semente — a mesma
/// semente e a mesma sequência de chamadas reproduzem a mesma rede, pacote a pacote.
/// </summary>
public sealed class RedeEmMemoria
{
    private readonly Aleatorio _aleatorio;
    private readonly List<TransporteEmMemoria> _pontos = [];
    private long _ordem;

    public RedeEmMemoria(uint semente, CondicoesDaRede? condicoes = null)
    {
        _aleatorio = new Aleatorio(semente);
        Condicoes = condicoes ?? CondicoesDaRede.Perfeita;
    }

    public CondicoesDaRede Condicoes { get; set; }
    public double Agora { get; private set; }
    public long PacotesEnviados { get; private set; }
    public long PacotesPerdidos { get; private set; }
    public long PacotesDuplicados { get; private set; }
    public event Action<PacoteNaRede>? AoEnviar;

    public void AvancarRelogio(double segundos)
    {
        if (!(segundos >= 0)) throw new ArgumentOutOfRangeException(nameof(segundos), "o relógio não volta");
        Agora += segundos;
    }

    public TransporteEmMemoria NovoPonto()
    {
        var ponto = new TransporteEmMemoria(this, _pontos.Count + 1);
        _pontos.Add(ponto);
        return ponto;
    }

    /// <summary>
    /// Liga um cliente a um host. O host fica sabendo depois de uma latência (o pedido chegou); o cliente, depois
    /// de duas (a resposta voltou) — como no ENet. Devolve o par do host visto pelo cliente e o do cliente visto pelo host.
    /// </summary>
    public (int ParDoHost, int ParDoCliente) Conectar(TransporteEmMemoria cliente, TransporteEmMemoria host)
    {
        if (cliente.Rede != this || host.Rede != this || cliente == host) throw new ArgumentException("os dois pontos precisam ser desta rede e diferentes");
        int parDoHost = cliente.NovoPar();
        int parDoCliente = host.NovoPar();
        var ida = new Ligacao(host, parDoCliente);
        var volta = new Ligacao(cliente, parDoHost);
        cliente.Registrar(parDoHost, ida);
        host.Registrar(parDoCliente, volta);
        double chegada = AgendarConfiavel(ida, new EventoDoTransporte(TipoDeEventoDoTransporte.Conectou, parDoCliente));
        volta.UltimaEntregaConfiavel = chegada + Condicoes.Latencia;
        AgendarNoRemoto(volta, new EventoDoTransporte(TipoDeEventoDoTransporte.Conectou, parDoHost), volta.UltimaEntregaConfiavel);
        return (parDoHost, parDoCliente);
    }

    private double Sorteio() => _aleatorio.Proximo();

    private double Atraso()
    {
        var c = Condicoes;
        double atraso = c.Latencia + Sorteio() * c.Jitter;
        if (Sorteio() < c.Reordenacao) atraso += c.AtrasoDaReordenacao;
        return atraso;
    }

    private void AgendarNoRemoto(Ligacao ligacao, EventoDoTransporte evento, double quando) =>
        ligacao.Remoto.Agendar(evento, quando, _ordem++);

    /// <summary>Confiável: com latência e jitter, mas nunca antes do que foi mandado antes pela mesma ligação.</summary>
    private double AgendarConfiavel(Ligacao ligacao, EventoDoTransporte evento)
    {
        double quando = Math.Max(Agora + Condicoes.Latencia + Sorteio() * Condicoes.Jitter, ligacao.UltimaEntregaConfiavel);
        ligacao.UltimaEntregaConfiavel = quando;
        AgendarNoRemoto(ligacao, evento, quando);
        return quando;
    }

    internal void Postar(TransporteEmMemoria de, Ligacao ligacao, Canal canal, byte[] dados)
    {
        PacotesEnviados++;
        AoEnviar?.Invoke(new PacoteNaRede(de.Id, ligacao.Remoto.Id, canal, dados));
        var evento = new EventoDoTransporte(TipoDeEventoDoTransporte.Pacote, ligacao.ParNoRemoto, canal, dados);
        if (canal == Canal.Confiavel)
        {
            AgendarConfiavel(ligacao, evento);
            return;
        }
        if (Sorteio() < Condicoes.Perda) { PacotesPerdidos++; return; }
        AgendarNoRemoto(ligacao, evento, Agora + Atraso());
        if (Sorteio() < Condicoes.Duplicacao)
        {
            PacotesDuplicados++;
            AgendarNoRemoto(ligacao, evento, Agora + Atraso());
        }
    }

    internal void AvisarDesconexao(Ligacao ligacao) =>
        AgendarConfiavel(ligacao, new EventoDoTransporte(TipoDeEventoDoTransporte.Desconectou, ligacao.ParNoRemoto));
}

/// <summary>Um sentido de uma conexão: pra quem vai e como o outro lado chama quem mandou.</summary>
internal sealed class Ligacao(TransporteEmMemoria remoto, int parNoRemoto)
{
    public TransporteEmMemoria Remoto { get; } = remoto;
    public int ParNoRemoto { get; } = parNoRemoto;
    public bool Aberta { get; set; } = true;
    public double UltimaEntregaConfiavel { get; set; }
}

/// <summary>Um ponto da <see cref="RedeEmMemoria"/> — host ou cliente. Implementa o mesmo <see cref="ITransporte"/> do ENet e da Steam.</summary>
public sealed class TransporteEmMemoria : ITransporte
{
    private readonly Dictionary<int, Ligacao> _pares = [];
    private readonly PriorityQueue<EventoDoTransporte, (double Quando, long Ordem)> _aCaminho = new();
    private readonly Queue<EventoDoTransporte> _recebidos = new();
    private int _ultimoPar;

    internal TransporteEmMemoria(RedeEmMemoria rede, int id)
    {
        Rede = rede;
        Id = id;
    }

    public RedeEmMemoria Rede { get; }
    public int Id { get; }

    internal int NovoPar() => ++_ultimoPar;
    internal void Registrar(int par, Ligacao ligacao) => _pares[par] = ligacao;
    internal void Agendar(EventoDoTransporte evento, double quando, long ordem) => _aCaminho.Enqueue(evento, (quando, ordem));

    public void Enviar(int par, Canal canal, ReadOnlySpan<byte> dados)
    {
        if (!_pares.TryGetValue(par, out var ligacao) || !ligacao.Aberta) return;
        Rede.Postar(this, ligacao, canal, dados.ToArray());
    }

    public void Desconectar(int par)
    {
        if (!_pares.TryGetValue(par, out var ligacao) || !ligacao.Aberta) return;
        ligacao.Aberta = false;
        Rede.AvisarDesconexao(ligacao);
        _recebidos.Enqueue(new EventoDoTransporte(TipoDeEventoDoTransporte.Desconectou, par));
    }

    public void Processar()
    {
        while (_aCaminho.TryPeek(out var evento, out var quando) && quando.Quando <= Rede.Agora)
        {
            _aCaminho.Dequeue();
            if (!_pares.TryGetValue(evento.Par, out var ligacao) || !ligacao.Aberta) continue;   // já desconectado: o que ainda vinha se perde
            if (evento.Tipo == TipoDeEventoDoTransporte.Desconectou) ligacao.Aberta = false;
            _recebidos.Enqueue(evento);
        }
    }

    public bool TentarReceber(out EventoDoTransporte evento) => _recebidos.TryDequeue(out evento);
}
