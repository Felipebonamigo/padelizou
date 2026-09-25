namespace Padel.Core;

public enum Motivo { DoisQuiques, Rede, NaoPassou, ParedeSemQuicar, ParedePropria, Fora, VoltouPeloVidro, DuplaFalta, ForaDaCaixa, BolaMorta }
public enum TipoDeDecisao { Ponto, Falta, Let }

public readonly record struct Decisao(TipoDeDecisao Tipo, int Para, Motivo Motivo)
{
    public static Decisao Ponto(int para, Motivo motivo) => new(TipoDeDecisao.Ponto, para, motivo);
    public static Decisao Falta(Motivo motivo) => new(TipoDeDecisao.Falta, -1, motivo);
    public static Decisao Let() => new(TipoDeDecisao.Let, -1, Motivo.Rede);
}

/// <summary>
/// Transforma os eventos da física em decisão de ponto, pelas regras do padel:
/// a bola precisa cruzar a rede e quicar no chão do outro lado ANTES de tocar qualquer parede de lá;
/// quicou do lado de quem bateu é ponto contra; bater nas próprias paredes antes de cruzar é permitido;
/// depois de quicar, paredes à vontade e o segundo quique encerra; saiu por cima: a favor de quem
/// bateu se já tinha quicado, senão contra; saque na caixa diagonal, duas faltas é ponto, let se tocou a rede
/// e caiu na caixa; quem recebe o saque não pode voleiar; o mesmo time não bate duas vezes seguidas.
/// </summary>
public sealed class Arbitro
{
    public int Faltas { get; private set; }
    public bool EmSaque { get; private set; }
    /// <summary>Time que bateu por último; null antes do saque.</summary>
    public int? Golpeador { get; private set; }
    public bool QuicouNoReceptor { get; private set; }
    public bool TocouARede { get; private set; }
    public bool Cruzou { get; private set; }
    private Caixa _caixa;

    public int? Receptor => Golpeador is int g ? 1 - g : null;

    private void ZerarRally()
    {
        EmSaque = false; Golpeador = null; QuicouNoReceptor = false; TocouARede = false; Cruzou = false;
    }

    public void NovoPonto() { Faltas = 0; ZerarRally(); }

    public void IniciarSaque(int time, Caixa caixa)
    {
        ZerarRally();
        EmSaque = true; Golpeador = time; _caixa = caixa;
    }

    public void RegistrarGolpe(int time)
    {
        ZerarRally();
        Golpeador = time;
    }

    public bool PodeGolpear(int time)
    {
        if (Golpeador is null || Golpeador == time) return false;
        if (EmSaque && !QuicouNoReceptor) return false;
        return true;
    }

    /// <summary>null = segue o jogo.</summary>
    public Decisao? Processar(EventoDaBola evento)
    {
        if (Golpeador is not int golpeador) return null;
        int ladoDoGolpeador = Quadra.LadoDoTime(golpeador);
        switch (evento.Tipo)
        {
            case TipoDeEventoDaBola.CruzouRede:
                if (evento.Para != ladoDoGolpeador) { Cruzou = true; return null; }
                return QuicouNoReceptor ? Decisao.Ponto(golpeador, Motivo.VoltouPeloVidro) : null;
            case TipoDeEventoDaBola.Rede:
                TocouARede = true;
                return null;
            case TipoDeEventoDaBola.Quique:
                if (evento.Lado == ladoDoGolpeador) return ContraOGolpeador(TocouARede ? Motivo.Rede : Motivo.NaoPassou);
                if (QuicouNoReceptor) return Decisao.Ponto(golpeador, Motivo.DoisQuiques);
                QuicouNoReceptor = true;
                if (EmSaque)
                {
                    if (!_caixa.Contem(evento.X, evento.Y)) return ContraOGolpeador(Motivo.ForaDaCaixa);
                    if (TocouARede) return Decisao.Let();
                }
                return null;
            case TipoDeEventoDaBola.Parede:
                if (evento.Lado == ladoDoGolpeador) return EmSaque ? ContraOGolpeador(Motivo.ParedePropria) : null;
                return QuicouNoReceptor ? null : ContraOGolpeador(Motivo.ParedeSemQuicar);
            case TipoDeEventoDaBola.Saiu:
                return QuicouNoReceptor ? Decisao.Ponto(golpeador, Motivo.Fora) : ContraOGolpeador(Motivo.Fora);
            default:
                return null;
        }
    }

    private Decisao ContraOGolpeador(Motivo motivo)
    {
        int receptor = Receptor!.Value;
        if (!EmSaque) return Decisao.Ponto(receptor, motivo);
        Faltas += 1;
        return Faltas >= 2 ? Decisao.Ponto(receptor, Motivo.DuplaFalta) : Decisao.Falta(motivo);
    }
}
