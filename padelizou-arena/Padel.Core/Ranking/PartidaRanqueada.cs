namespace Padel.Core.Ranking;

/// <summary>
/// Um dos quatro lugares de uma partida, na ordem <c>time * 2 + índice</c> — a mesma de
/// <c>OpcoesDaPartida.Humanos</c>. Ou é um humano com conta e nível, ou é a IA.
/// </summary>
/// <remarks>
/// A IA não tem <see cref="Id"/> nem <see cref="Nivel"/>: é o que a régua usa pra saber que a
/// partida não conta (<c>Ranqueamento</c>). Quem abandonou continua sendo o humano que abandonou,
/// mesmo que a IA tenha assumido o lugar dele pra terminar a partida — a IA que entra no lugar de
/// quem saiu não é a "partida com IA" que não conta.
/// </remarks>
public sealed record LugarRanqueado
{
    /// <summary>A conta do jogador (por exemplo, o SteamID em texto). Nulo na IA.</summary>
    public string? Id { get; }
    /// <summary>O nível de ANTES da partida. Nulo na IA.</summary>
    public NivelNoRanking? Nivel { get; }
    /// <summary>Caiu e não voltou a tempo (a reconexão do M2 é de até 30 s — CRONOGRAMA.md).</summary>
    public bool Abandonou { get; }

    private LugarRanqueado(string? id, NivelNoRanking? nivel, bool abandonou)
    {
        Id = id;
        Nivel = nivel;
        Abandonou = abandonou;
    }

    public static LugarRanqueado Humano(string id, NivelNoRanking nivel, bool abandonou = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(nivel);
        return new LugarRanqueado(id, nivel, abandonou);
    }

    public static LugarRanqueado DaIA { get; } = new(null, null, false);
}

/// <summary>
/// O que a régua precisa saber de uma partida: quem estava em cada lugar, quem venceu, os games
/// de cada time e em quantos sets eles foram jogados. <see cref="TimeVencedor"/> nulo = a partida
/// não terminou (alguém abandonou, ou a sessão caiu pra todos).
/// </summary>
/// <remarks>
/// Os games são o TOTAL da partida, somando todos os sets, e <see cref="SetsJogados"/> diz quantos.
/// No Padelizou o jogo de torneio é de set único e o fator lê o placar dele
/// (<c>Partida.GamesDupla1/2</c>). DECISÃO DO JOGO pra melhor de 3 (<c>Padelimetro.FatorDaPartida</c>):
/// o fator é o de um set com a margem MÉDIA do vencedor — 6-4 6-4 vale um 6-4 (1,2); 7-6 0-6 7-6
/// (14x18: o vencedor tem MENOS games) vale 1,0; 6-0 6-7 7-6 (19x13, decidido no tie-break do 3º
/// set) vale 1,2, e não o 1,6 de um passeio. A soma crua com |diferença| — a primeira versão deste
/// porte — errava os três. Com set único (o padrão do jogo) é exatamente a conta do site.
/// </remarks>
public sealed record PartidaRanqueada(IReadOnlyList<LugarRanqueado> Lugares, int? TimeVencedor, int GamesDoTime0, int GamesDoTime1,
    int SetsJogados = 1)
{
    /// <summary>Monta a partida a partir do placar do Core, acabado ou não.</summary>
    /// <remarks>
    /// ⚠️ Existe por causa de uma armadilha: com a partida acabada, <c>Placar.Games</c> volta a
    /// [0, 0] (o set fechado vai pra <c>SetsAnteriores</c>). Quem lesse os games dali passaria
    /// fator 1,0 pra TODA partida, e nada acusaria o erro. Aqui soma-se os sets fechados mais o
    /// set em andamento (que só tem games se a partida parou no meio). Os sets jogados são os
    /// fechados mais o em andamento, se a partida não acabou — o que garante pelo menos 1.
    /// </remarks>
    public static PartidaRanqueada DoPlacar(IReadOnlyList<LugarRanqueado> lugares, Placar placar)
    {
        ArgumentNullException.ThrowIfNull(placar);
        int games0 = placar.Games[0], games1 = placar.Games[1];
        foreach (var set in placar.SetsAnteriores)
        {
            games0 += set.Games[0];
            games1 += set.Games[1];
        }
        int sets = placar.SetsAnteriores.Count + (placar.Acabou ? 0 : 1);
        return new PartidaRanqueada(lugares, placar.Vencedor, games0, games1, sets);
    }
}
