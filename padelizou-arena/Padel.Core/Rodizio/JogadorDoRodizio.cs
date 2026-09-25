namespace Padel.Core.Rodizio;

/// <summary>
/// Um jogador do rodízio — INDIVIDUAL, não dupla: no Americano e no Mexicano a dupla muda a cada
/// rodada e cada um soma os pontos que a sua dupla fez. <see cref="Forca"/> (0–100) é o nível: entre
/// IAs decide o jogo simulado (pela média da dupla) e, no Mexicano "por força", a primeira rodada.
/// <see cref="Humano"/>: o jogo roda a Partida de verdade e informa o placar de fora — o rodízio
/// nunca simula um jogo em que ele esteja. O nome é a identidade no evento, então é único.
/// </summary>
public sealed record JogadorDoRodizio
{
    public JogadorDoRodizio(string nome, int forca, bool humano = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        if (forca is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(forca), forca, $"A força de \"{nome}\" vai de 0 a 100.");
        Nome = nome;
        Forca = forca;
        Humano = humano;
    }

    public string Nome { get; }
    public int Forca { get; }
    public bool Humano { get; }
}
