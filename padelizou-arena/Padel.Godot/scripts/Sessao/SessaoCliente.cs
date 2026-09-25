using Padel.Core;
using Padel.Core.Rede;

namespace Padel.Godot;

/// <summary>
/// Online, entrando na sala de alguém: manda a entrada local a cada passo e desenha o que o ClienteDaPartida
/// monta — os outros interpolados 100 ms no passado, o próprio jogador predito agora.
/// </summary>
public sealed class SessaoCliente : ISessao
{
    private readonly TransporteEnet _transporte;
    private readonly ClienteDaPartida _cliente;
    private readonly RetratoDaVisao _mapa;
    private double _acumulado;
    private int _instantaneos;
    private FaseDoCliente _faseAnterior = FaseDoCliente.Conectando;

    public SessaoCliente(string endereco, int porta, string nome)
    {
        _transporte = TransporteEnet.Conectar(endereco, porta);
        _cliente = new ClienteDaPartida(_transporte, nome);
        _mapa = new RetratoDaVisao(Retrato);
        Retrato.Mensagem = $"Conectando em {endereco}:{porta}…";
        Retrato.MensagemSuave = true;
    }

    public TransporteEnet Transporte => _transporte;
    public ClienteDaPartida Cliente => _cliente;
    public IReadOnlyList<int> JogadoresLocais => _cliente.Indice >= 0 ? [_cliente.Indice] : [];
    public RetratoDaPartida Retrato { get; } = new();
    public bool Acabou => _cliente.UltimoInstantaneo?.Placar.Acabou == true || _cliente.Fase is FaseDoCliente.Recusado or FaseDoCliente.Desconectado;

    public void Avancar(double delta, ReadOnlySpan<Entrada> entradas)
    {
        int indice = _cliente.Indice;
        var minha = indice >= 0 && indice < entradas.Length ? entradas[indice] : Entrada.Vazia;
        _acumulado += delta;
        bool primeiro = true;
        while (_acumulado >= Protocolo.Passo - 1e-6)
        {
            _cliente.Passo(primeiro ? minha : minha with { AcaoPressionada = false, LobPressionada = false });
            primeiro = false;
            _acumulado -= Protocolo.Passo;
        }
        if (_cliente.Fase != _faseAnterior)
        {
            global::Godot.GD.Print($"Rede: cliente {_faseAnterior} → {_cliente.Fase}{(_cliente.Indice >= 0 ? $" (vaga {_cliente.Indice})" : "")}{(_cliente.MotivoDaRecusa is MotivoDaRecusa m ? $" — recusado: {m}" : "")}");
            _faseAnterior = _cliente.Fase;
        }
        switch (_cliente.Fase)
        {
            case FaseDoCliente.NaSala:
                Retrato.Mensagem = $"Na sala do host — {_cliente.Nomes.Count(n => !string.IsNullOrEmpty(n))} de 4. Esperando começar…";
                break;
            case FaseDoCliente.Recusado:
                Retrato.Mensagem = _cliente.MotivoDaRecusa switch
                {
                    MotivoDaRecusa.VersaoIncompativel => "O host usa outra versão do jogo",
                    MotivoDaRecusa.SalaCheia => "A sala está cheia",
                    _ => "A partida já começou",
                };
                break;
            case FaseDoCliente.Desconectado:
                Retrato.Mensagem = "Conexão com o host perdida";
                break;
            case FaseDoCliente.Jogando:
                if (_cliente.ParaDesenhar() is VisaoDaPartida visao)
                {
                    _instantaneos++;
                    _mapa.Preencher(visao, _cliente.Nomes, (int)Math.Round(_cliente.Ping * 1000));
                }
                break;
        }
    }

    public EstadoVisivel? EstadoParaOBot() => _cliente.Fase == FaseDoCliente.Jogando ? _mapa.ParaOBot() : null;

    public string ResumoParaLog()
    {
        var u = _cliente.UltimoInstantaneo;
        string placar = u is null ? "sem instantâneo" : $"placar={u.Placar.Resumo()} {u.Placar.TextoDosPontos(0)}-{u.Placar.TextoDosPontos(1)} tickDoHost={u.Tick}";
        return $"cliente fase={_cliente.Fase} vaga={_cliente.Indice} {placar} ping={_cliente.Ping * 1000:F0} ms quadros={_instantaneos} enviados={_transporte.PacotesEnviados} recebidos={_transporte.PacotesRecebidos} ({_transporte.BytesRecebidos / 1024.0:F0} KiB)";
    }

    public void Dispose()
    {
        _cliente.Sair();
        _transporte.Processar();
        _transporte.Dispose();
    }
}
