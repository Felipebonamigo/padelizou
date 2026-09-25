using Padel.Core;

namespace Padel.Godot.Interface;

/// <summary>Um set que já acabou: os games de cada dupla e, se foi decidido no tie-break, os pontos dele.</summary>
public readonly record struct SetAnterior(int GamesCasa, int GamesRivais, int? TieBreakCasa = null, int? TieBreakRivais = null)
{
    /// <summary>0 casa, 1 rivais, null se empatado (não acontece num set encerrado).</summary>
    public int? Vencedor => GamesCasa > GamesRivais ? 0 : GamesRivais > GamesCasa ? 1 : null;

    /// <summary>Os pontos de tie-break de quem perdeu o set — é o número pequeno que a TV mostra: 7-6⁵.</summary>
    public int? TieBreakDoPerdedor => Vencedor switch
    {
        0 => TieBreakRivais,
        1 => TieBreakCasa,
        _ => null,
    };
}

/// <summary>
/// Tudo que o placar de TV mostra, só com primitivos — quem monta pode ser a partida local, o cliente de rede
/// ou uma cena de teste. Time 0 é a casa, 1 os rivais.
/// </summary>
/// <param name="PontosCasa">"0", "15", "30", "40", "AD" ou o número do tie-break.</param>
/// <param name="TimeQueSaca">0 ou 1; outro valor esconde a marca de saque.</param>
/// <param name="PontoDecisivo">40-40 com ponto de ouro: quem ganhar leva o game.</param>
/// <param name="MensagemSuave">Dica (ex.: "aperte pra sacar"), mostrada mais discreta que o anúncio de ponto.</param>
/// <param name="PingMs">Latência do online; null esconde o indicador.</param>
/// <param name="PartidaEncerrada">Some com games e pontos do set em andamento: sobram os sets.</param>
public sealed record DadosDoPlacar(
    string DuplaCasa,
    string DuplaRivais,
    IReadOnlyList<SetAnterior> SetsAnteriores,
    int GamesCasa,
    int GamesRivais,
    string PontosCasa,
    string PontosRivais,
    int TimeQueSaca,
    bool EmTieBreak = false,
    bool PontoDecisivo = false,
    string? Mensagem = null,
    bool MensagemEmDestaque = false,
    bool MensagemSuave = false,
    int? PingMs = null,
    bool PartidaEncerrada = false)
{
    /// <summary>
    /// Monta a partir do placar do Core e da mensagem da partida — o atalho pra quem tem o Core na mão
    /// (partida local ou host). O cliente de rede monta pelos primitivos que recebeu.
    /// </summary>
    public static DadosDoPlacar De(Placar placar, string duplaCasa, string duplaRivais, Mensagem? mensagem = null, int? pingMs = null) => new(
        duplaCasa,
        duplaRivais,
        placar.SetsAnteriores.Select(s => new SetAnterior(s.Games[0], s.Games[1], s.TieBreak?[0], s.TieBreak?[1])).ToArray(),
        placar.Games[0],
        placar.Games[1],
        placar.TextoDosPontos(0),
        placar.TextoDosPontos(1),
        placar.Acabou ? -1 : placar.Sacador.Time,
        placar.EmTieBreak,
        placar.EmPontoDecisivo,
        mensagem?.Texto,
        mensagem?.Destaque ?? false,
        mensagem?.Suave ?? false,
        pingMs,
        placar.Acabou);

    /// <summary>Igualdade de verdade, com os sets comparados item a item (a do record compara a referência da lista).</summary>
    public bool MesmoConteudo(DadosDoPlacar? outro) =>
        outro is not null
        && this with { SetsAnteriores = outro.SetsAnteriores } == outro
        && SetsAnteriores.SequenceEqual(outro.SetsAnteriores);
}
