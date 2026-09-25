using System.Text.Json.Serialization;

namespace Padel.Core.Rodizio;

/// <summary>Uma dupla de uma rodada — no rodízio ela dura um jogo só.</summary>
public sealed record DuplaDoRodizio(string Jogador1, string Jogador2)
{
    public bool Tem(string jogador) => Jogador1 == jogador || Jogador2 == jogador;

    public override string ToString() => $"{Jogador1} + {Jogador2}";
}

/// <summary>
/// Um jogo de pontos corridos. <see cref="Rodada"/> começa em 0; <see cref="Quadra"/> em 1 (no
/// Mexicano a quadra 1 é a do topo da tabela). Os pontos são <c>null</c> enquanto o jogo não
/// aconteceu — os dois juntos, sempre.
/// </summary>
public sealed record JogoDoRodizio(
    int Numero,
    int Rodada,
    int Quadra,
    DuplaDoRodizio DuplaA,
    DuplaDoRodizio DuplaB,
    int? PontosA = null,
    int? PontosB = null)
{
    [JsonIgnore] public bool Jogado => PontosA is not null && PontosB is not null;

    [JsonIgnore]
    public IReadOnlyList<string> Jogadores => [DuplaA.Jogador1, DuplaA.Jogador2, DuplaB.Jogador1, DuplaB.Jogador2];

    public bool Envolve(string jogador) => DuplaA.Tem(jogador) || DuplaB.Tem(jogador);

    /// <summary>
    /// O motivo de o placar ser impossível, ou <c>null</c> se ele serve: pontos não negativos e a
    /// soma igual ao total do jogo. Empate vale (24 pode acabar 12-12).
    /// </summary>
    public static string? ProblemaNoPlacar(int pontosA, int pontosB, int pontosPorJogo)
    {
        if (pontosA < 0 || pontosB < 0) return $"pontos não podem ser negativos ({pontosA} a {pontosB}).";
        if (pontosA + pontosB != pontosPorJogo)
            return $"o jogo vale {pontosPorJogo} pontos corridos, e {pontosA} + {pontosB} = {pontosA + pontosB}.";
        return null;
    }
}
