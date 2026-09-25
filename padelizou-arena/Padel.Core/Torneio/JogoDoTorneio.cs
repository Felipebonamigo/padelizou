using System.Text.Json.Serialization;

namespace Padel.Core.Torneio;

/// <summary>
/// Um jogo do torneio. <see cref="Sets"/> vem do ponto de vista da <see cref="DuplaA"/>
/// (<c>Games[0]</c> é dela) e é <c>null</c> enquanto o jogo não aconteceu.
/// <see cref="Rodada"/> é a rodada do torneio inteiro (grupos e mata-mata numa régua só);
/// jogo de grupo tem <see cref="Grupo"/>, jogo de mata-mata tem a posição na árvore da chave.
/// </summary>
public sealed record JogoDoTorneio(
    int Numero,
    int Rodada,
    string DuplaA,
    string DuplaB,
    string? Grupo = null,
    int RodadaDaChave = -1,
    int PosicaoNaChave = -1,
    IReadOnlyList<SetEncerrado>? Sets = null)
{
    [JsonIgnore] public bool Jogado => Sets is not null;
    [JsonIgnore] public bool DeGrupo => Grupo is not null;
    [JsonIgnore] public int SetsA => Sets?.Count(s => s.Games[0] > s.Games[1]) ?? 0;
    [JsonIgnore] public int SetsB => Sets?.Count(s => s.Games[1] > s.Games[0]) ?? 0;
    [JsonIgnore] public int GamesA => Sets?.Sum(s => s.Games[0]) ?? 0;
    [JsonIgnore] public int GamesB => Sets?.Sum(s => s.Games[1]) ?? 0;

    /// <summary>
    /// Quem venceu: por sets e, se os sets empatarem, por games — a mesma ordem do
    /// <c>QuemVenceu.Da</c> do Padelizou (PorSets ?? PorGames). <c>null</c> = não jogado.
    /// Um placar validado nunca empata; o degrau dos games existe pra a régua ser uma só.
    /// </summary>
    [JsonIgnore]
    public string? Vencedor
    {
        get
        {
            if (!Jogado) return null;
            if (SetsA != SetsB) return SetsA > SetsB ? DuplaA : DuplaB;
            if (GamesA != GamesB) return GamesA > GamesB ? DuplaA : DuplaB;
            return null;
        }
    }

    public bool Envolve(string dupla) => DuplaA == dupla || DuplaB == dupla;

    /// <summary>"6-4 7-6(5)" do ponto de vista da DuplaA, como o <c>Placar.Resumo</c>.</summary>
    public string Resumo() => Sets is null ? "" : PlacarDoJogo.Resumo(Sets);
}

/// <summary>A régua do que é um placar de padel possível — pro resultado que entra de fora.</summary>
public static class PlacarDoJogo
{
    /// <summary>
    /// O motivo de o placar ser impossível, ou <c>null</c> se ele serve. Set válido é o que o
    /// <see cref="Placar"/> fecha: 6-0 a 6-4, 7-5, ou 7-6 com tie-break (a 7, com 2 de vantagem).
    /// A partida acaba no set em que alguém chega a <paramref name="setsParaVencer"/>.
    /// </summary>
    public static string? ProblemaNoPlacar(IReadOnlyList<SetEncerrado>? sets, int setsParaVencer)
    {
        if (sets is null || sets.Count == 0) return "Nenhum set informado.";
        int[] ganhos = [0, 0];
        for (int i = 0; i < sets.Count; i++)
        {
            if (ganhos[0] >= setsParaVencer || ganhos[1] >= setsParaVencer)
                return $"O {i + 1}º set foi jogado depois de a partida estar decidida.";
            var problema = ProblemaNoSet(sets[i]);
            if (problema is not null) return $"{i + 1}º set: {problema}";
            ganhos[sets[i].Games[0] > sets[i].Games[1] ? 0 : 1]++;
        }
        if (Math.Max(ganhos[0], ganhos[1]) != setsParaVencer)
            return $"A partida não terminou: ninguém venceu {setsParaVencer} set(s) ({Resumo(sets)}).";
        return null;
    }

    private static string? ProblemaNoSet(SetEncerrado? set)
    {
        // O arquivo salvo também passa por aqui, então o set pode chegar torto do disco.
        if (set?.Games is not { Length: 2 } g) return "o set precisa de dois números de games.";
        int v = Math.Max(g[0], g[1]), p = Math.Min(g[0], g[1]);
        bool normal = (v == 6 && p >= 0 && p <= 4) || (v == 7 && p == 5);
        bool tieBreak = v == 7 && p == 6;
        if (!normal && !tieBreak)
            return $"{g[0]}-{g[1]} não é placar de set (vale 6-0 a 6-4, 7-5 ou 7-6 com tie-break).";
        if (normal)
            return set.TieBreak is null ? null : $"{g[0]}-{g[1]} não foi a tie-break; só o 7-6 tem.";

        if (set.TieBreak is not { Length: 2 } tb) return "7-6 precisa do placar do tie-break.";
        int tv = Math.Max(tb[0], tb[1]), tp = Math.Min(tb[0], tb[1]);
        if (tp < 0 || tv < 7 || tv - tp < 2 || (tv > 7 && tv - tp != 2))
            return $"tie-break {tb[0]}-{tb[1]} não fecha (vai a 7, com 2 de vantagem).";
        if ((tb[0] > tb[1]) != (g[0] > g[1]))
            return $"quem venceu o tie-break ({tb[0]}-{tb[1]}) tem que vencer o set ({g[0]}-{g[1]}).";
        return null;
    }

    /// <summary>Os mesmos sets vistos do outro lado. Cópia nova: nada aponta pros arrays de quem chamou.</summary>
    public static List<SetEncerrado> Inverter(IEnumerable<SetEncerrado> sets) =>
        sets.Select(s => new SetEncerrado([s.Games[1], s.Games[0]], s.TieBreak is null ? null : [s.TieBreak[1], s.TieBreak[0]]))
            .ToList();

    /// <summary>Cópia funda — o Placar continua dono dos arrays dele.</summary>
    public static List<SetEncerrado> Copiar(IEnumerable<SetEncerrado> sets) =>
        sets.Select(s => new SetEncerrado([s.Games[0], s.Games[1]], s.TieBreak is null ? null : [s.TieBreak[0], s.TieBreak[1]]))
            .ToList();

    public static string Resumo(IEnumerable<SetEncerrado> sets) =>
        string.Join(' ', sets.Select(s =>
        {
            string texto = $"{s.Games[0]}-{s.Games[1]}";
            return s.TieBreak is null ? texto : $"{texto}({Math.Min(s.TieBreak[0], s.TieBreak[1])})";
        }));
}
