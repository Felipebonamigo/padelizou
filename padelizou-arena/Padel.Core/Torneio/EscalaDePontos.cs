namespace Padel.Core.Torneio;

/// <summary>O peso de uma etapa no ranking — as três categorias do Premier Padel.</summary>
public enum CategoriaDaEtapa { P2, P1, Major }

/// <summary>
/// Pontos de ranking por fase alcançada.
/// </summary>
/// <remarks>
/// <para>De onde saiu: a tabela de pontos da FIP pro Premier Padel na temporada 2025 ("FIP and
/// Premier Padel announce new 2025 FIP ranking points and tournament criteria", padelfip.com,
/// janeiro de 2025), por rodada alcançada:</para>
/// <code>
///          Campeão  Final  Semi  Quartas  Oitavas  32 avos  64 avos
///   Major    2000    1200   720    360      180       90       35
///   P1       1000     600   360    180       90       45        —
///   P2        600     360   180     90       45       22        —
/// </code>
/// <para>⚠️ Os números vieram da página da FIP e de resumos dela vistos por busca em 25/09/2026;
/// a página em si não abriu nesta sessão (rede bloqueada). Conferir na fonte antes de citar no
/// jogo como "oficial" — a escala do jogo não depende disso: é inspirada, não licenciada.</para>
/// <para>O que é NOSSO: o Premier Padel não tem fase de grupos. "Caiu nos grupos" vale o degrau
/// abaixo da primeira rodada: no Major, os 35 dos 64 avos (publicado); no P1 e no P2, que não
/// publicam 64 avos, metade dos 32 avos arredondada pra baixo (22 e 11). E as etapas do jogo
/// têm de 8 a 16 duplas, então a primeira rodada de uma chave de 8 já vale "quartas" — a fase é
/// pelo tamanho do quadro naquela rodada, como no nome da fase do Padelizou.</para>
/// </remarks>
public static class EscalaDePontos
{
    private static readonly IReadOnlyDictionary<CategoriaDaEtapa, int[]> Tabela = new Dictionary<CategoriaDaEtapa, int[]>
    {
        //                          Grupos  1ª rod  Oitavas  Quartas  Semi  Final  Campeão
        [CategoriaDaEtapa.Major] = [35, 90, 180, 360, 720, 1200, 2000],
        [CategoriaDaEtapa.P1] = [22, 45, 90, 180, 360, 600, 1000],
        [CategoriaDaEtapa.P2] = [11, 22, 45, 90, 180, 360, 600],
    };

    public static int Por(CategoriaDaEtapa categoria, FaseAlcancada fase)
    {
        if (!Tabela.TryGetValue(categoria, out var pontos))
            throw new ArgumentOutOfRangeException(nameof(categoria), categoria, "Categoria de etapa desconhecida.");
        int indice = (int)fase;
        if (indice < 0 || indice >= pontos.Length)
            throw new ArgumentOutOfRangeException(nameof(fase), fase, "Fase desconhecida.");
        return pontos[indice];
    }
}
