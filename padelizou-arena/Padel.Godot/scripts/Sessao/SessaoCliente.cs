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
    private readonly PassoFixo _passoFixo = new(Protocolo.TicksPorSegundo);
    private int _instantaneos;
    private FaseDoCliente _faseAnterior = FaseDoCliente.Conectando;
    private readonly string _endereco;
    private double _esperandoResposta;
    private string? _motivoDoFim;

    /// <summary>Quanto o cliente espera o host responder (conexão + aperto de mão) antes de desistir.</summary>
    public const double PrazoDeConexao = 10;

    public SessaoCliente(string endereco, int porta, string nome)
    {
        _endereco = $"{endereco}:{porta}";
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
    /// <summary>
    /// Pelo que está DESENHADO (100 ms atrás do último instantâneo): acabar pelo instantâneo mais novo mostrava a tela de
    /// fim com o placar de antes do último ponto ("Partida encerrada", sem o set final).
    /// </summary>
    public bool Acabou => _mapa.Ultima?.Placar.Acabou == true || _cliente.Fase is FaseDoCliente.Recusado or FaseDoCliente.Desconectado;

    public string? MotivoDoFim => _mapa.Ultima?.Placar.Acabou == true ? null : _motivoDoFim;

    public void Avancar(double delta, ReadOnlySpan<Entrada> entradas)
    {
        int indice = _cliente.Indice;
        var minha = indice >= 0 && indice < entradas.Length ? entradas[indice] : Entrada.Vazia;
        int passos = _passoFixo.Quadro(delta, minha);   // sem deriva, e o aperto de quadro sem passo vai no próximo passo
        for (int i = 0; i < passos; i++) _cliente.Passo(_passoFixo.EntradaDoPasso(i));
        if (_cliente.Fase is FaseDoCliente.Conectando or FaseDoCliente.AguardandoResposta)
        {
            _esperandoResposta += delta;
            if (_esperandoResposta > PrazoDeConexao) _cliente.Sair();   // vira Desconectado: o motivo sai abaixo
        }
        if (_cliente.Fase != _faseAnterior)
        {
            _motivoDoFim ??= _cliente.Fase switch
            {
                FaseDoCliente.Recusado => $"o host recusou a entrada: {TextoDaRecusa()}",
                FaseDoCliente.Desconectado when _faseAnterior is FaseDoCliente.Conectando or FaseDoCliente.AguardandoResposta
                    => $"o host não respondeu em {_endereco}",
                FaseDoCliente.Desconectado => "a conexão com o host caiu",
                _ => null,
            };
            if (_motivoDoFim is string motivo && _cliente.Fase is FaseDoCliente.Recusado or FaseDoCliente.Desconectado)
                global::Godot.GD.Print($"Sala: {motivo}");
            global::Godot.GD.Print($"Rede: cliente {_faseAnterior} → {_cliente.Fase}{(_cliente.Indice >= 0 ? $" (vaga {_cliente.Indice})" : "")}{(_cliente.MotivoDaRecusa is MotivoDaRecusa m ? $" — recusado: {m}" : "")}");
            _faseAnterior = _cliente.Fase;
        }
        switch (_cliente.Fase)
        {
            case FaseDoCliente.NaSala:
                Retrato.Mensagem = $"Na sala do host — {_cliente.Nomes.Count(n => !string.IsNullOrEmpty(n))} de 4. Esperando começar…";
                break;
            case FaseDoCliente.Recusado:
            case FaseDoCliente.Desconectado:
                if (_motivoDoFim is string texto) Retrato.Mensagem = char.ToUpperInvariant(texto[0]) + texto[1..];
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

    private string TextoDaRecusa() => _cliente.MotivoDaRecusa switch
    {
        MotivoDaRecusa.VersaoIncompativel => "ele usa outra versão do jogo",
        MotivoDaRecusa.SalaCheia => "a sala está cheia",
        _ => "a partida já começou",
    };

    public EstadoVisivel? EstadoParaOBot() => _cliente.Fase == FaseDoCliente.Jogando ? _mapa.ParaOBot() : null;

    public string ResumoParaLog()
    {
        var u = _cliente.UltimoInstantaneo;
        string placar = u is null ? "sem instantâneo" : $"placar={u.Placar.Resumo()} {u.Placar.TextoDosPontos(0)}-{u.Placar.TextoDosPontos(1)} tickDoHost={u.Tick}";
        return $"cliente fase={_cliente.Fase} vaga={_cliente.Indice} {placar} ping={_cliente.Ping * 1000:F0} ms quadros={_instantaneos} enviados={_transporte.PacotesEnviados} recebidos={_transporte.PacotesRecebidos} ({_transporte.BytesRecebidos / 1024.0:F0} KiB) {_transporte.ResumoDosProblemas()}";
    }

    public void Dispose()
    {
        _cliente.Sair();
        _transporte.Processar();
        _transporte.Dispose();
    }
}
