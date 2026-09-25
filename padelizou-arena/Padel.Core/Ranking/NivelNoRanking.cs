namespace Padel.Core.Ranking;

/// <summary>
/// Onde um jogador está na régua: o número (PDZ), quantos jogos já contaram (o K de calibração
/// sai daqui) e o rótulo que a tela mostra, com quantos jogos ele tem nesse rótulo (a histerese de
/// descida sai daqui). É o que a conta do jogador guarda entre partidas.
/// </summary>
/// <remarks>
/// Porte do estado do <c>Jogador</c> do Padelizou (<c>Padelimetro</c> e <c>JogosDePadelimetro</c>,
/// RANKING.md "Implementação") mais o rótulo, que lá é recalculado da categoria jogada e aqui
/// precisa ser guardado porque não há categoria (ver <c>FaixasDoPadelimetro.RotuloDepoisDoJogo</c>).
/// Imutável: a partida devolve um nível NOVO, e o velho fica pra quem quiser desenhar o "antes".
/// </remarks>
public sealed record NivelNoRanking(int Pdz, int Jogos, FaixaDoPadelimetro Rotulo, int JogosNoRotulo)
{
    public bool EmCalibracao => Padelimetro.EmCalibracao(Jogos);

    /// <summary>
    /// Quanto falta pro RÓTULO subir — e não pra cruzar o teto cru da faixa. Nulo em calibração
    /// (o rótulo está esperando os 10 jogos) e no topo da escada.
    /// </summary>
    /// <remarks>
    /// RANKING.md "O RÓTULO da tela": "'Faltam X pra subir de faixa' passa a medir o que muda o
    /// RÓTULO, não a faixa crua" — um "faltam 8" ao lado de um rótulo que não muda em 8 foi um
    /// defeito do site. Porte de <c>FaixasDePadelimetro.FaltaPraMudarDeFaixa</c>, com a âncora no
    /// rótulo atual (o rótulo do jogo sobe com número &gt; teto + 50).
    /// </remarks>
    public int? FaltaPraSubir =>
        EmCalibracao || Rotulo.Teto >= Padelimetro.Maximo
            ? null
            : Rotulo.Teto + FaixasDoPadelimetro.FolgaDoRotulo + 1 - Pdz;

    /// <summary>Quem nunca jogou ranqueada. Sem jogos, o número ainda é só o seed.</summary>
    /// <remarks>
    /// RANKING.md "Onde o número nasce (seed)"; <c>PadelimetroService.SemearSePreciso</c>. No site o
    /// seed é a entrada da categoria da primeira partida; no ranqueado não há categoria, então o
    /// padrão é a entrada NEUTRA (500, <c>FaixasDePadelimetro.EntradaNeutra</c>). Quem já tem número
    /// em outro lugar — a entrada de uma faixa que a pessoa declara jogar
    /// (<c>FaixaDoPadelimetro.Entrada</c>), ou o próprio PDZ do site no ranking cruzado — nasce
    /// nele: é o "quem vem do Quanto Tá entra pela categoria que já joga", e o K de calibração
    /// conserta o exagero. O rótulo nasce na faixa do seed e espera ali a calibração inteira.
    /// </remarks>
    public static NivelNoRanking Estreante(int entrada = Padelimetro.EntradaNeutra)
    {
        int pdz = Padelimetro.Acomodar(entrada);
        return new NivelNoRanking(pdz, 0, FaixasDoPadelimetro.DoNivel(pdz), 0);
    }

    /// <summary>
    /// O nível depois de um jogo que CONTOU: o número andou <paramref name="delta"/> (preso às
    /// pontas da régua), mais um jogo na contagem, e o rótulo reavaliado com histerese.
    /// </summary>
    /// <remarks>
    /// <c>PadelimetroService.Mover</c>: <c>Acomodar(antes + delta)</c> e <c>JogosDePadelimetro++</c>.
    /// O delta que vale é o que de fato andou depois do clamp (<see cref="MovimentoNoRanking.Delta"/>)
    /// — "o extrato nunca mente".
    /// </remarks>
    public NivelNoRanking DepoisDoJogo(int delta)
    {
        int pdz = Padelimetro.Acomodar(Pdz + delta);
        int jogos = Jogos + 1;
        var (rotulo, jogosNoRotulo) = FaixasDoPadelimetro.RotuloDepoisDoJogo(Rotulo, JogosNoRotulo + 1, pdz, jogos);
        return new NivelNoRanking(pdz, jogos, rotulo, jogosNoRotulo);
    }
}
