namespace Padel.Core;

/// <summary>
/// Transforma o tempo de cada quadro (o delta da física do Godot, em segundos) em passos fixos de simulação — o laço
/// das sessões online, que rodam o protocolo a 120 passos por segundo (D2). Duas garantias, com teste
/// (PassoFixoTests):
/// - sem deriva: o acumulado é contado EM PASSOS (delta × passos por segundo, tudo em double), e um resto rente a um
///   inteiro vira o inteiro. Somar segundos e tirar Protocolo.Passo (float, 4,3e-10 s maior que o 1/120 double do
///   Godot) esgotava a folga no quadro 2301 (~19 s): aquele quadro ficava sem passo nenhum.
/// - sem aperto perdido: ação e lob valem um quadro só; se o quadro não dá passo (física mais rápida que o protocolo,
///   ou o quadro 2301 acima), o aperto espera o próximo passo em vez de sumir. Num quadro com vários passos, só o
///   primeiro leva o aperto. Direção e botão segurado são sempre os do quadro mais novo.
/// </summary>
public sealed class PassoFixo
{
    /// <summary>Um resto a menos disto de um inteiro (em passos; ≈ 8 ns a 120 Hz) é o inteiro: é o que mata a deriva.</summary>
    private const double Folga = 1e-6;

    /// <summary>
    /// atalho: um quadro vale no máximo 1 s de jogo; o tempo além disso é descartado. O Godot já entrega delta fixo (e
    /// limita os passos de física por quadro), então só um delta absurdo chega aqui — sem o teto, ele travaria o jogo
    /// num laço de passos (o acumulador antigo travava com delta infinito). Saída, se um dia a sessão receber tempo de
    /// relógio: o chamador decide entre pular e recuperar o tempo, e divide o delta.
    /// </summary>
    private const double MaximoPorQuadro = 1.0;

    private readonly int _passosPorSegundo;
    private double _resto;   // fração de passo que sobrou dos quadros anteriores, em [0, 1)
    private bool _acaoPendente, _lobPendente;
    private Entrada _primeira;
    private int _passosDoQuadro;

    public PassoFixo(int passosPorSegundo)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(passosPorSegundo);
        _passosPorSegundo = passosPorSegundo;
    }

    /// <summary>
    /// Um quadro de <paramref name="delta"/> segundos com a entrada lida nele: devolve quantos passos rodar agora (0, 1
    /// ou mais). Pra cada passo i, a entrada é <see cref="EntradaDoPasso"/>(i). Delta zero, negativo ou não finito não
    /// dá passo (como a Partida.Avancar), e o aperto dele fica guardado pro próximo passo.
    /// </summary>
    public int Quadro(double delta, Entrada entrada)
    {
        _acaoPendente |= entrada.AcaoPressionada;
        _lobPendente |= entrada.LobPressionada;
        _passosDoQuadro = 0;
        if (!(delta > 0) || !double.IsFinite(delta)) return 0;

        _resto += Math.Min(delta, MaximoPorQuadro) * _passosPorSegundo;
        int passos = (int)Math.Floor(_resto + Folga);
        _resto -= passos;
        if (Math.Abs(_resto) < Folga) _resto = 0;   // rente ao inteiro: é o inteiro, e o erro de arredondamento não soma
        if (passos == 0) return 0;

        _primeira = entrada with { AcaoPressionada = _acaoPendente, LobPressionada = _lobPendente };
        _acaoPendente = _lobPendente = false;
        _passosDoQuadro = passos;
        return passos;
    }

    /// <summary>A entrada do passo i (0 = o primeiro) do último <see cref="Quadro"/>: só o primeiro leva os apertos.</summary>
    public Entrada EntradaDoPasso(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(i, _passosDoQuadro);
        return i == 0 ? _primeira : _primeira with { AcaoPressionada = false, LobPressionada = false };
    }
}
