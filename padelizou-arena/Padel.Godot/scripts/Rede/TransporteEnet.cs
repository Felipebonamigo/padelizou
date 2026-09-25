using System.Diagnostics;
using Godot;
using Padel.Core;
using Padel.Core.Rede;

namespace Padel.Godot;

/// <summary>
/// O transporte da rede de verdade no desenvolvimento (D2/D5): ENet do Godot em baixo nível — ENetConnection e
/// ENetPacketPeer, sem MultiplayerAPI nem RPC. O protocolo é o do Padel.Core.Rede; aqui só se entrega byte.
/// Canal 0 = confiável (sala, início), canal 1 = não confiável (entradas, instantâneos).
/// Condições de teste opcionais (latência, perda) atrasam e descartam pacotes na SAÍDA, pra provar o jogo com rede ruim
/// sem depender de ferramenta do sistema.
/// </summary>
public sealed class TransporteEnet : ITransporte, IDisposable
{
    public const int CanalConfiavel = 0;
    public const int CanalNaoConfiavel = 1;
    private const int Canais = 2;

    private readonly ENetConnection _conexao = new();
    private readonly Dictionary<ulong, int> _parPorPeer = [];
    private readonly Dictionary<int, ENetPacketPeer> _peerPorPar = [];
    private readonly Queue<EventoDoTransporte> _recebidos = new();
    private readonly List<(long quando, int par, Canal canal, byte[] dados)> _atrasados = [];
    private readonly Stopwatch _relogio = Stopwatch.StartNew();
    private readonly Random _sorteio = new(12345);
    private int _proximoPar = 1;

    /// <summary>Latência artificial de ida (ms) e perda artificial (0..1) dos pacotes não confiáveis. Só pra teste.</summary>
    public int LatenciaDeTesteMs { get; set; }
    public double PerdaDeTeste { get; set; }

    public long BytesEnviados { get; private set; }
    public long BytesRecebidos { get; private set; }
    public long PacotesEnviados { get; private set; }
    public long PacotesRecebidos { get; private set; }
    public long PacotesDescartadosNoTeste { get; private set; }
    /// <summary>
    /// Envios que o ENet recusou a um par conectado. Deve ficar em zero (o par saindo nem chega ao Send): a conferência
    /// vigia, o log avisa (limitado) e o ResumoParaLog das sessões mostra.
    /// </summary>
    public long EnviosRecusados => _enviosRecusados.Ocorrencias;
    /// <summary>Vezes que o Service do ENet devolveu erro — falha do socket (a rede caiu, endereço que o sistema não alcança).</summary>
    public long ErrosDoEnet => _errosDoEnet.Ocorrencias;

    /// <summary>Os contadores de problema, pro "Saindo após" das sessões denunciar o que o log só avisa.</summary>
    public string ResumoDosProblemas() => $"recusados={EnviosRecusados} errosDoEnet={ErrosDoEnet} descartadosNoTeste={PacotesDescartadosNoTeste}";

    // Enquanto duram, esses erros se repetem a cada quadro: um aviso na hora e depois no máximo um a cada 5 s.
    private const long IntervaloDosAvisosMs = 5000;
    private readonly AvisoLimitado _enviosRecusados = new("Rede: o ENet recusou um envio a um par conectado", IntervaloDosAvisosMs);
    private readonly AvisoLimitado _errosDoEnet = new("Rede: o Service do ENet devolveu erro (falha do socket — a rede caiu?)", IntervaloDosAvisosMs);

    private TransporteEnet() { }

    /// <summary>
    /// Pares que o ENet do host aceita: as 3 vagas de cliente e uma folga pra quem sobra. Com exatamente 3, o ENet
    /// ignorava em silêncio o 4º pedido, e quem sobrava nunca ouvia o Recusado "sala cheia" (ou "a partida já
    /// começou") do ServidorDaPartida — esperava o prazo e saía com "o host não respondeu".
    /// atalho: até 4 pedidos de sobra AO MESMO TEMPO ouvem a recusa (cada recusado sai e libera o par); do 5º em diante o
    /// ENet volta a ignorar e o cliente sai pelo prazo dele. Saída: o lobby da Steam (M2) recusa antes de conectar.
    /// </summary>
    private const int ParesNoHost = Protocolo.Jogadores - 1 + 4;

    /// <summary>Host: escuta na porta (UDP) pra 3 clientes jogarem (e a folga de <see cref="ParesNoHost"/> pra recusar).</summary>
    public static TransporteEnet Hospedar(int porta)
    {
        var t = new TransporteEnet();
        var erro = t._conexao.CreateHostBound("*", porta, ParesNoHost, Canais);
        if (erro != Error.Ok) throw new InvalidOperationException($"não deu pra abrir a sala na porta {porta} (UDP) — ela já está em uso? ({erro})");
        return t;
    }

    /// <summary>
    /// Cliente: começa a conectar no host; o Conectou chega pelo TentarReceber quando o aperto do ENet termina — ou o
    /// Desconectou, se ninguém responder. prazoDoEnetMs: só pra teste — quanto o ENet insiste sem resposta antes de
    /// desistir (o padrão dele passa de 30 s; o jogo não depende disso, o SessaoCliente desiste antes).
    /// </summary>
    public static TransporteEnet Conectar(string endereco, int porta, int? prazoDoEnetMs = null)
    {
        var t = new TransporteEnet();
        var erro = t._conexao.CreateHost(1, Canais);
        if (erro != Error.Ok) throw new InvalidOperationException($"não deu pra criar o cliente ENet: {erro}");
        var peer = t._conexao.ConnectToHost(endereco, porta, Canais);
        if (peer is null) throw new InvalidOperationException($"não achei a sala em {endereco}:{porta} — o endereço não existe ou não resolve");
        if (prazoDoEnetMs is int prazo) peer.SetTimeout(LimiteDeTentativasDoEnet, prazo, prazo);
        t.Registrar(peer);
        return t;
    }

    /// <summary>O "timeout" do ENetPacketPeer.SetTimeout no valor padrão do ENet (só os prazos mudam no teste).</summary>
    private const int LimiteDeTentativasDoEnet = 32;

    private int Registrar(ENetPacketPeer peer)
    {
        ulong id = peer.GetInstanceId();
        if (_parPorPeer.TryGetValue(id, out int par)) return par;
        par = _proximoPar++;
        _parPorPeer[id] = par;
        _peerPorPar[par] = peer;
        return par;
    }

    public void Enviar(int par, Canal canal, ReadOnlySpan<byte> dados)
    {
        if (!_peerPorPar.ContainsKey(par)) return;
        if (canal == Canal.NaoConfiavel && PerdaDeTeste > 0 && _sorteio.NextDouble() < PerdaDeTeste) { PacotesDescartadosNoTeste++; return; }
        if (LatenciaDeTesteMs > 0)
        {
            _atrasados.Add((_relogio.ElapsedMilliseconds + LatenciaDeTesteMs, par, canal, dados.ToArray()));
            return;
        }
        EnviarAgora(par, canal, dados);
    }

    private void EnviarAgora(int par, Canal canal, ReadOnlySpan<byte> dados)
    {
        if (!_peerPorPar.TryGetValue(par, out var peer)) return;
        // Par saindo (Desconectar já pedido) ou ainda conectando: o ENet recusa com "Invalid channel". Não é envio.
        if (peer.GetState() != ENetPacketPeer.PeerState.Connected) return;
        var erro = canal == Canal.Confiavel
            ? peer.Send(CanalConfiavel, dados, (int)ENetPacketPeer.FlagReliable)
            : peer.Send(CanalNaoConfiavel, dados, (int)ENetPacketPeer.FlagUnsequenced);
        if (erro != Error.Ok)
        {
            // O protocolo tolera (é um pacote a menos), mas não devia acontecer: aparece no log e no ResumoDosProblemas.
            if (_enviosRecusados.Registrar(_relogio.ElapsedMilliseconds) is string aviso) GD.PushWarning($"{aviso}: {erro}");
            return;
        }
        BytesEnviados += dados.Length;
        PacotesEnviados++;
        _conexao.Flush();   // sai já, sem esperar o próximo Service (menos ~8 ms de atraso)
    }

    public void Desconectar(int par)
    {
        // O que a latência de teste ainda segurava pra esse par não sai mais: ele está indo embora.
        _atrasados.RemoveAll(a => a.par == par);
        if (_peerPorPar.TryGetValue(par, out var peer)) peer.PeerDisconnect();
    }

    public void Processar()
    {
        // Pacotes com atraso de teste que já venceram (a ordem de envio é a ordem da lista).
        if (_atrasados.Count > 0)
        {
            long agora = _relogio.ElapsedMilliseconds;
            int vencidos = 0;
            while (vencidos < _atrasados.Count && _atrasados[vencidos].quando <= agora) vencidos++;
            for (int i = 0; i < vencidos; i++) EnviarAgora(_atrasados[i].par, _atrasados[i].canal, _atrasados[i].dados);
            _atrasados.RemoveRange(0, vencidos);
        }

        for (int guarda = 0; guarda < 512; guarda++)
        {
            var evento = _conexao.Service(0);
            var tipo = (ENetConnection.EventType)(int)evento[0];
            if (tipo == ENetConnection.EventType.Error)
            {
                if (_errosDoEnet.Registrar(_relogio.ElapsedMilliseconds) is string aviso) GD.PushWarning(aviso);
                break;   // o Service não tem mais o que dar neste quadro; o próximo Processar tenta de novo
            }
            if (tipo == ENetConnection.EventType.None) break;
            var peer = evento[1].As<ENetPacketPeer>();
            if (peer is null) continue;
            switch (tipo)
            {
                case ENetConnection.EventType.Connect:
                    _recebidos.Enqueue(new EventoDoTransporte(TipoDeEventoDoTransporte.Conectou, Registrar(peer)));
                    break;
                case ENetConnection.EventType.Disconnect:
                {
                    int par = Registrar(peer);
                    _recebidos.Enqueue(new EventoDoTransporte(TipoDeEventoDoTransporte.Desconectou, par));
                    _peerPorPar.Remove(par);
                    _parPorPeer.Remove(peer.GetInstanceId());
                    break;
                }
                case ENetConnection.EventType.Receive:
                {
                    int canal = (int)evento[3];
                    byte[] dados = peer.GetPacket();
                    BytesRecebidos += dados.Length;
                    PacotesRecebidos++;
                    _recebidos.Enqueue(new EventoDoTransporte(TipoDeEventoDoTransporte.Pacote, Registrar(peer),
                        canal == CanalConfiavel ? Canal.Confiavel : Canal.NaoConfiavel, dados));
                    break;
                }
            }
        }
    }

    public bool TentarReceber(out EventoDoTransporte evento) => _recebidos.TryDequeue(out evento);

    /// <summary>Ida e volta medida pelo próprio ENet até o primeiro par (ms), ou null.</summary>
    public int? PingDoEnetMs => _peerPorPar.Values.FirstOrDefault() is ENetPacketPeer p ? (int)p.GetStatistic(ENetPacketPeer.PeerStatistic.RoundTripTime) : null;

    public void Dispose()
    {
        foreach (var peer in _peerPorPar.Values) peer.PeerDisconnectNow();
        _conexao.Destroy();
    }
}
