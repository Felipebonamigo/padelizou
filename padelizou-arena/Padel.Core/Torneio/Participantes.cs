namespace Padel.Core.Torneio;

/// <summary>
/// Os dois formatos do Padelizou: grupos (todos contra todos) seguidos de mata-mata, e a chave
/// direta. O Americano e as categorias de times ficam de fora — não são modo do jogo.
/// </summary>
public enum FormatoDoTorneio { GruposEMataMata, ChaveDireta }

/// <summary>
/// Até onde a dupla chegou. A ordem do enum É a ordem esportiva (quem chega mais longe vale mais),
/// e é ela que a escala de pontos segue. <see cref="Final"/> é quem chegou à final e não venceu
/// (ou ainda não jogou): o vice.
/// </summary>
public enum FaseAlcancada { FaseDeGrupos, PrimeiraRodada, Oitavas, Quartas, Semifinal, Final, Campeao }

/// <summary>
/// Uma dupla inscrita. <see cref="Forca"/> (0–100) é o nível: decide a semeadura e, entre duas
/// IAs, o resultado simulado. <see cref="Humana"/>: o jogo roda a Partida de verdade e informa o
/// placar de fora — o torneio nunca simula um jogo dela. O nome é a identidade dentro do torneio
/// e da carreira (é por ele que o ranking soma), então precisa ser único.
/// </summary>
public sealed record DuplaParticipante
{
    public DuplaParticipante(string nome, int forca, bool humana = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        if (forca is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(forca), forca, $"A força da dupla \"{nome}\" vai de 0 a 100.");
        Nome = nome;
        Forca = forca;
        Humana = humana;
    }

    public string Nome { get; }
    public int Forca { get; }
    public bool Humana { get; }
}

/// <summary>
/// As regras de um torneio. <see cref="ClassificadosPorGrupo"/> vale 2 como no Padelizou
/// (<c>ClassificacaoDeGrupos.VagasPadrao</c>); <see cref="SetsParaVencer"/> 1 é um set só (o
/// padrão de <c>OpcoesDaPartida</c>), 2 é melhor de 3. O jogo usa estas regras pra montar a
/// Partida da dupla humana.
/// </summary>
public sealed record RegrasDoTorneio(
    FormatoDoTorneio Formato = FormatoDoTorneio.GruposEMataMata,
    int ClassificadosPorGrupo = 2,
    int SetsParaVencer = 1,
    bool PontoDeOuro = true);

/// <summary>Um grupo: nome ("A", "B"…) e as duplas na ordem da semeadura.</summary>
public sealed record GrupoDoTorneio(string Nome, IReadOnlyList<string> Duplas);
