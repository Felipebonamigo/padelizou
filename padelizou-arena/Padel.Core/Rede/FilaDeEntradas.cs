namespace Padel.Core.Rede;

/// <summary>Retrato de uma <see cref="FilaDeEntradas"/>: buffer atual, ticks sem entrada, entradas perdidas de vez e puladas pra encolher o buffer.</summary>
public readonly record struct EstatisticasDeEntrada(int Acumuladas, int TicksSemEntrada, int EntradasPerdidas, int EntradasPuladas, uint UltimaSeqProcessada);

/// <summary>
/// O lado do host das entradas de UM jogador remoto: guarda o que chegou (pacotes repetidos, velhos ou fora de
/// ordem não atrapalham — cada seq entra uma vez) e, a cada tick do host, entrega a <see cref="Entrada"/> daquele tick.
///
/// Regras:
/// - Uma entrada do cliente por tick do host, em ordem de seq. Não chegou a próxima ainda: repete a direção da
///   última e não inventa aperto (<see cref="TicksSemEntrada"/>) — é isso que monta, sozinho, um buffer do tamanho
///   do jitter da rede, sem relógio sincronizado.
/// - Seq que não vem mais (8+ pacotes seguidos perdidos: já chegou coisa mais nova) ocupa o tick dela com a direção
///   da última (<see cref="EntradasPerdidas"/>).
/// - Aperto = o contador subiu desde a última entrada processada. A diferença entra numa fila de apertos pendentes e
///   sai UM por tick: nenhum se perde (o contador de uma entrada posterior carrega o aperto de uma perdida) e nenhum
///   duplica (pacote repetido tem o mesmo contador).
/// - Se durante um segundo inteiro sempre sobraram 3+ entradas esperando, o buffer encolhe: uma por segundo quando a
///   sobra é pequena (até 4 a mais), todas as que passam de 2 de uma vez depois de um pico — fundidas num tick só
///   (<see cref="EntradasPuladas"/>): a latência cai de volta quando o pico de rede passa. Fundir perde só a direção
///   dos ticks pulados; os apertos delas vão pra fila de pendentes.
/// </summary>
public sealed class FilaDeEntradas
{
    /// <summary>Quantas entradas à frente o host guarda. 256 ticks ≈ 2,1 s de cliente adiantado.</summary>
    public const int Capacidade = 256;
    /// <summary>A janela em que se mede a sobra mínima antes de encolher o buffer.</summary>
    public const int TicksDaJanela = Protocolo.TicksPorSegundo;
    /// <summary>Sobra que fica depois de encolher: 1 entrada esperando além da do tick (folga pro jitter).</summary>
    private const int SobraMinima = 2;
    /// <summary>Até esta sobra a mais, o buffer encolhe uma entrada por janela; acima, tudo de uma vez.</summary>
    private const int ExcessoDoAjusteSuave = 4;

    private readonly EntradaDeRede[] _anel = new EntradaDeRede[Capacidade];
    private readonly bool[] _ocupado = new bool[Capacidade];
    private byte _contadorDeAcao, _contadorDeLob;
    private int _acoesPendentes, _lobsPendentes;
    private float _dx, _dy;
    private bool _segurada;
    private int _menorSobraNaJanela = int.MaxValue;
    private int _ticksNaJanela;

    public uint UltimaSeqProcessada { get; private set; }
    public uint MaiorSeqRecebida { get; private set; }
    public bool RecebeuAlguma { get; private set; }
    /// <summary>Entradas que já chegaram (ou deviam ter chegado) e esperam o tick delas.</summary>
    public int Acumuladas => (int)(MaiorSeqRecebida - UltimaSeqProcessada);
    public int TicksSemEntrada { get; private set; }
    public int EntradasPerdidas { get; private set; }
    public int EntradasPuladas { get; private set; }
    /// <summary>A última <see cref="Proxima"/> usou uma entrada que veio do cliente (e não uma repetição).</summary>
    public bool UltimaTeveEntrada { get; private set; }

    public void Receber(ReadOnlySpan<EntradaDeRede> entradas)
    {
        foreach (var e in entradas)
        {
            if (e.Seq <= UltimaSeqProcessada) continue;   // velha ou repetida
            if (e.Seq - UltimaSeqProcessada >= Capacidade)
            {
                // O cliente está mais de 2 s à frente (o host travou?): pula pra perto dele. Os apertos do que
                // ficou pra trás não se perdem — o contador das entradas seguintes carrega todos.
                PularAte(e.Seq - Capacidade / 2);
            }
            int i = (int)(e.Seq % Capacidade);
            _anel[i] = e;
            _ocupado[i] = true;
            if (e.Seq > MaiorSeqRecebida) MaiorSeqRecebida = e.Seq;
            RecebeuAlguma = true;
        }
    }

    public void Receber(EntradaDeRede[] entradas) => Receber(entradas.AsSpan());

    private void PularAte(uint seq)
    {
        // O anel só tem Capacidade posições: um salto maior que isso limpa o anel inteiro, em tempo constante —
        // varrer seq por seq deixava um cliente bugado (ou malicioso) travar o host mandando uma seq de bilhões.
        if (seq - UltimaSeqProcessada >= Capacidade) Array.Clear(_ocupado);
        else for (uint s = UltimaSeqProcessada + 1; s <= seq; s++) _ocupado[(int)(s % Capacidade)] = false;
        UltimaSeqProcessada = seq;
        if (MaiorSeqRecebida < seq) MaiorSeqRecebida = seq;
    }

    /// <summary>Consome a próxima seq: a entrada dela se chegou, a direção anterior se ela se perdeu. Devolve se havia entrada.</summary>
    private bool Consumir()
    {
        uint seq = UltimaSeqProcessada + 1;
        int i = (int)(seq % Capacidade);
        UltimaSeqProcessada = seq;
        if (!_ocupado[i] || _anel[i].Seq != seq)
        {
            EntradasPerdidas++;
            return false;
        }
        _ocupado[i] = false;
        var e = _anel[i];
        _acoesPendentes += (byte)(e.ContadorDeAcao - _contadorDeAcao);   // mod 256: o contador dá a volta
        _lobsPendentes += (byte)(e.ContadorDeLob - _contadorDeLob);
        _contadorDeAcao = e.ContadorDeAcao;
        _contadorDeLob = e.ContadorDeLob;
        _dx = e.DirecaoX;
        _dy = e.DirecaoY;
        _segurada = e.AcaoSegurada;
        return true;
    }

    /// <summary>A entrada deste tick do host.</summary>
    public Entrada Proxima()
    {
        int disponiveis = Acumuladas;
        _menorSobraNaJanela = Math.Min(_menorSobraNaJanela, disponiveis);
        int consumir = disponiveis > 0 ? 1 : 0;
        if (++_ticksNaJanela >= TicksDaJanela)
        {
            int excesso = _menorSobraNaJanela - SobraMinima;
            if (excesso > 0)
            {
                // Cada entrada pulada é um tick de movimento que o host não simula e a predição do cliente simulou
                // (~5 cm a toda velocidade). Sobra pequena encolhe uma por segundo; pico grande sai de uma vez.
                int pular = excesso <= ExcessoDoAjusteSuave ? 1 : excesso;
                consumir += pular;
                EntradasPuladas += pular;
            }
            _ticksNaJanela = 0;
            _menorSobraNaJanela = int.MaxValue;
        }

        bool teveEntrada = false;
        for (int k = 0; k < consumir; k++) teveEntrada |= Consumir();
        UltimaTeveEntrada = teveEntrada;
        if (consumir == 0) TicksSemEntrada++;

        bool acao = _acoesPendentes > 0, lob = _lobsPendentes > 0;
        if (acao) _acoesPendentes--;
        if (lob) _lobsPendentes--;
        return new Entrada(_dx, _dy, acao, _segurada, lob);
    }
}
