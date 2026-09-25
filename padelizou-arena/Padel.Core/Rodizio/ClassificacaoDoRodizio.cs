namespace Padel.Core.Rodizio;

/// <summary>
/// Uma linha da classificação individual. <see cref="Pontos"/> é o que ordena: os pontos que as
/// duplas do jogador fizeram (<see cref="PontosPro"/>) mais os da folga (<see cref="PontosDeFolga"/>).
/// Saldo, vitórias, empates e derrotas vêm só dos jogos jogados.
/// </summary>
public sealed record LinhaDoRodizio(
    int Posicao,
    string Jogador,
    int Pontos,
    int Jogos,
    int Vitorias,
    int Empates,
    int Derrotas,
    int PontosPro,
    int PontosContra,
    int Folgas,
    int PontosDeFolga)
{
    public int Saldo => PontosPro - PontosContra;
}

/// <summary>
/// A classificação individual do Americano e do Mexicano, na ordem:
/// <list type="number">
/// <item>pontos (os feitos em quadra mais os de folga);</item>
/// <item>jogos vencidos (empate não é vitória);</item>
/// <item>saldo de pontos (feitos − sofridos, só em quadra);</item>
/// <item>empate de DOIS jogadores: confronto direto — quem venceu mais jogos em que os dois
/// estiveram em lados opostos; empate aí, ou nunca se enfrentaram, passa pro degrau seguinte;</item>
/// <item>nome (ordem ordinal), pra ordem ser total e estável.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para><b>Fonte.</b> A ordem é a especificada pro jogo (tarefa O3A). Guias de Americano e Mexicano
/// consultados em 25/09/2026 — racketrise.co.uk, hostatourney.com, padelfast.com, padelmix.app,
/// americano-padel.app; só o resumo da busca, porque as páginas estavam bloqueadas pela rede daqui
/// — concordam no primeiro degrau (o total de pontos individual) e em que o desempate é escolha do
/// organizador, anunciada antes do primeiro jogo; os degraus citados são jogos vencidos, saldo de
/// pontos e confronto direto. Não há federação que fixe essa ordem.</para>
/// <para><b>Confronto direto só entre dois</b>, como no desempate de grupo do Padelizou
/// (<c>ClassificacaoDoGrupo</c>): entre três, A ganha de B, B de C e C de A, e o degrau não decide
/// nada. No rodízio o "confronto" é individual: os dois em duplas opostas, com parceiros diferentes
/// a cada vez.</para>
/// <para><b>Folga.</b> Quem folga recebe, por folga, a MÉDIA DOS SEUS PRÓPRIOS pontos por jogo,
/// arredondada (metade pra cima) — é o costume de clube que a especificação cita. Há clube que dá
/// a média da rodada (que, com pontos corridos, é sempre metade do total) e há quem não dê nada;
/// "nada" pune quem folgou mais, e a diferença de folgas pode ser 1 quando as rodadas não fecham o
/// ciclo. Enquanto o jogador não jogou nenhum jogo, a média dele não existe e a folga vale metade
/// do total — a média da rodada. A folga é recalculada a cada consulta com a média do momento:
/// no fim do evento, toda folga do jogador vale a média final dele.</para>
/// </remarks>
public static class ClassificacaoDoRodizio
{
    /// <summary>
    /// Quanto vale UMA folga: a média de pontos por jogo arredondada, metade pra cima (12,5 → 13);
    /// sem jogo ainda, metade do total (24 → 12; 21 → 11). Conta inteira, sem float.
    /// </summary>
    public static int PontosDaFolga(int pontosJogados, int jogos, int pontosPorJogo)
    {
        if (pontosJogados < 0) throw new ArgumentOutOfRangeException(nameof(pontosJogados), pontosJogados, "Pontos não são negativos.");
        if (jogos < 0) throw new ArgumentOutOfRangeException(nameof(jogos), jogos, "Jogos não são negativos.");
        if (pontosPorJogo < 1) throw new ArgumentOutOfRangeException(nameof(pontosPorJogo), pontosPorJogo, "O jogo vale pelo menos 1 ponto.");
        return jogos == 0 ? (pontosPorJogo + 1) / 2 : (2 * pontosJogados + jogos) / (2 * jogos);
    }

    /// <param name="jogadores">Quem entra na tabela (a ordem não importa).</param>
    /// <param name="jogos">Só contam os jogados; jogador fora de <paramref name="jogadores"/> é ignorado.</param>
    /// <param name="pontosPorJogo">O total do jogo — é dele que sai o valor da folga de quem ainda não jogou.</param>
    /// <param name="folgas">Folgas de cada jogador nas rodadas já fechadas; quem não aparece folgou 0.</param>
    public static List<LinhaDoRodizio> Ordenar(
        IReadOnlyList<string> jogadores,
        IReadOnlyList<JogoDoRodizio> jogos,
        int pontosPorJogo,
        IReadOnlyDictionary<string, int>? folgas = null)
    {
        ArgumentNullException.ThrowIfNull(jogadores);
        ArgumentNullException.ThrowIfNull(jogos);
        var jogados = jogos.Where(j => j.Jogado).ToList();

        var campanhas = jogadores
            .Select(nome => Campanha(nome, jogados, pontosPorJogo, folgas?.GetValueOrDefault(nome) ?? 0))
            .OrderByDescending(l => l.Pontos)
            .ThenByDescending(l => l.Vitorias)
            .ThenByDescending(l => l.Saldo)
            .ToList();

        var ordem = new List<LinhaDoRodizio>(campanhas.Count);
        foreach (var bloco in BlocosEmpatados(campanhas))
        {
            if (bloco.Count == 2 && ConfrontoDireto(bloco[0].Jogador, bloco[1].Jogador, jogados) is { } venceu)
            {
                ordem.AddRange(bloco.OrderByDescending(l => l.Jogador == venceu));
                continue;
            }
            ordem.AddRange(bloco.OrderBy(l => l.Jogador, StringComparer.Ordinal));
        }
        return ordem.Select((l, i) => l with { Posicao = i + 1 }).ToList();
    }

    private static LinhaDoRodizio Campanha(string nome, List<JogoDoRodizio> jogados, int pontosPorJogo, int folgas)
    {
        int jogos = 0, vitorias = 0, empates = 0, derrotas = 0, pro = 0, contra = 0;
        foreach (var jogo in jogados)
        {
            if (!jogo.Envolve(nome) || jogo.PontosA is not { } a || jogo.PontosB is not { } b) continue;
            bool ehA = jogo.DuplaA.Tem(nome);
            int feitos = ehA ? a : b, sofridos = ehA ? b : a;
            jogos++;
            pro += feitos;
            contra += sofridos;
            if (feitos > sofridos) vitorias++;
            else if (feitos == sofridos) empates++;
            else derrotas++;
        }
        int deFolga = folgas * PontosDaFolga(pro, jogos, pontosPorJogo);
        return new LinhaDoRodizio(0, nome, pro + deFolga, jogos, vitorias, empates, derrotas, pro, contra, folgas, deFolga);
    }

    // Trechos vizinhos com pontos, vitórias e saldo idênticos.
    private static List<List<LinhaDoRodizio>> BlocosEmpatados(List<LinhaDoRodizio> linhas)
    {
        var blocos = new List<List<LinhaDoRodizio>>();
        int i = 0;
        while (i < linhas.Count)
        {
            int inicio = i;
            while (i < linhas.Count
                   && linhas[i].Pontos == linhas[inicio].Pontos
                   && linhas[i].Vitorias == linhas[inicio].Vitorias
                   && linhas[i].Saldo == linhas[inicio].Saldo) i++;
            blocos.Add(linhas.GetRange(inicio, i - inicio));
        }
        return blocos;
    }

    // Quem venceu mais jogos com os dois em lados opostos; null se ninguém (empate ou nunca se enfrentaram).
    private static string? ConfrontoDireto(string x, string y, List<JogoDoRodizio> jogados)
    {
        int deX = 0, deY = 0;
        foreach (var jogo in jogados)
        {
            bool xEmA = jogo.DuplaA.Tem(x), yEmA = jogo.DuplaA.Tem(y);
            bool opostos = (xEmA && jogo.DuplaB.Tem(y)) || (yEmA && jogo.DuplaB.Tem(x));
            if (!opostos || jogo.PontosA is not { } a || jogo.PontosB is not { } b || a == b) continue;
            bool venceuA = a > b;
            if (venceuA == xEmA) deX++;
            else deY++;
        }
        return deX > deY ? x : deY > deX ? y : null;
    }
}
