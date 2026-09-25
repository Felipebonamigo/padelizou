namespace Padel.Core.Torneio;

/// <summary>Uma linha da tabela do grupo, com a campanha somada (espelho do <c>ClassificacaoDeGrupos.Linha</c>).</summary>
public sealed record LinhaDaClassificacao(string Dupla, int Jogos, int Vitorias, int Derrotas, int Saldo, int GamesPro, int GamesContra);

/// <summary>
/// A tabela do grupo na ordem oficial — a MESMA régua do Padelizou
/// (<c>Padelizou/Services/ClassificacaoDeGrupos.cs</c>), reimplementada aqui sem banco:
/// <list type="number">
/// <item>vitórias;</item>
/// <item>saldo de games;</item>
/// <item>games a favor;</item>
/// <item>empate de DUAS: confronto direto;</item>
/// <item>empate de TRÊS ou mais (ou de duas sem confronto decidido): pontos do ranking;</item>
/// <item>sorteio estável.</item>
/// </list>
/// A ordem é TOTAL: não sobra empate nenhum no fim, então a mesma tabela responde sempre a mesma
/// coisa, venha a lista de duplas na ordem que vier.
/// </summary>
public static class ClassificacaoDoGrupo
{
    /// <param name="duplas">As duplas do grupo (a ordem não importa).</param>
    /// <param name="jogos">Jogos; só contam os já jogados entre duas duplas deste grupo.</param>
    /// <param name="pontosDeRanking">Pontos no ranking por nome de dupla; quem não aparece vale 0.</param>
    /// <param name="sementeDoSorteio">A semente do torneio — o último degrau sorteia com ela.</param>
    public static List<LinhaDaClassificacao> Ordenar(
        IReadOnlyList<string> duplas,
        IReadOnlyList<JogoDoTorneio> jogos,
        IReadOnlyDictionary<string, int>? pontosDeRanking = null,
        uint sementeDoSorteio = 0)
    {
        var doGrupo = duplas.ToHashSet(StringComparer.Ordinal);
        var jogados = jogos.Where(j => j.Jogado && doGrupo.Contains(j.DuplaA) && doGrupo.Contains(j.DuplaB)).ToList();
        var pontos = pontosDeRanking ?? new Dictionary<string, int>();
        return Desempatar(PorCampanha(duplas, jogados), jogados, pontos, sementeDoSorteio);
    }

    // Vitórias → saldo → games a favor: a parte que se decide NA QUADRA
    // (ClassificacaoDeGrupos.PorCampanha). A vitória sai do mesmo Vencedor que fecha o jogo — lá
    // é o QuemVenceu.Da, porque duas contas pra mesma pergunta já classificaram a dupla errada.
    // `Jogos` conta os jogos JOGADOS; lá conta os da fase, que no fim dos grupos é o mesmo número
    // (e é quando o bye o lê), e aqui a tabela ao vivo não mostra jogo futuro como disputado.
    private static List<LinhaDaClassificacao> PorCampanha(IReadOnlyList<string> duplas, List<JogoDoTorneio> jogados) =>
        duplas
            .Select(dupla =>
            {
                int jogos = 0, vitorias = 0, derrotas = 0, pro = 0, contra = 0;
                foreach (var jogo in jogados.Where(j => j.Envolve(dupla)))
                {
                    bool ehA = jogo.DuplaA == dupla;
                    jogos++;
                    pro += ehA ? jogo.GamesA : jogo.GamesB;
                    contra += ehA ? jogo.GamesB : jogo.GamesA;
                    var venceu = jogo.Vencedor;
                    if (venceu == dupla) vitorias++;
                    else if (venceu is not null) derrotas++;
                }
                return new LinhaDaClassificacao(dupla, jogos, vitorias, derrotas, pro - contra, pro, contra);
            })
            .OrderByDescending(l => l.Vitorias)
            .ThenByDescending(l => l.Saldo)
            .ThenByDescending(l => l.GamesPro)
            .ToList();

    // Os desempates depois da quadra (ClassificacaoDeGrupos.Desempatar, régua do Felipe de
    // 11/09/2026): separar por TAMANHO do empate é o que resolve a circularidade — entre duas
    // duplas o confronto direto nunca é circular; entre três (A ganhou de B, B de C, C de A) é.
    private static List<LinhaDaClassificacao> Desempatar(
        List<LinhaDaClassificacao> porCampanha, List<JogoDoTorneio> jogados,
        IReadOnlyDictionary<string, int> pontos, uint semente)
    {
        var saida = new List<LinhaDaClassificacao>(porCampanha.Count);
        foreach (var bloco in BlocosEmpatados(porCampanha))
        {
            if (bloco.Count == 1)
            {
                saida.AddRange(bloco);
                continue;
            }

            // DUAS: quem venceu o jogo entre elas. Sem jogo decidido, cai no degrau seguinte em
            // vez de inventar um ganhador.
            if (bloco.Count == 2 && ConfrontoDireto(bloco[0].Dupla, bloco[1].Dupla, jogados) is { } venceu)
            {
                saida.AddRange(bloco.OrderByDescending(l => l.Dupla == venceu));
                continue;
            }

            // TRÊS OU MAIS: o ranking, e o sorteio quando nem ele separa (inclusive quando
            // ninguém tem ponto, o caso que o Felipe nomeou). O nome no fim só existe pra ordem
            // ser total até numa colisão de hash.
            saida.AddRange(bloco
                .OrderByDescending(l => pontos.GetValueOrDefault(l.Dupla))
                .ThenBy(l => Sementes.Sorteio(semente, l.Dupla))
                .ThenBy(l => l.Dupla, StringComparer.Ordinal));
        }
        return saida;
    }

    // Trechos vizinhos com a campanha IDÊNTICA (ClassificacaoDeGrupos.BlocosEmpatados).
    private static List<List<LinhaDaClassificacao>> BlocosEmpatados(List<LinhaDaClassificacao> porCampanha)
    {
        var blocos = new List<List<LinhaDaClassificacao>>();
        int i = 0;
        while (i < porCampanha.Count)
        {
            int inicio = i;
            while (i < porCampanha.Count
                   && porCampanha[i].Vitorias == porCampanha[inicio].Vitorias
                   && porCampanha[i].Saldo == porCampanha[inicio].Saldo
                   && porCampanha[i].GamesPro == porCampanha[inicio].GamesPro) i++;
            blocos.Add(porCampanha.GetRange(inicio, i - inicio));
        }
        return blocos;
    }

    // Quem venceu o jogo entre estas duas; null sem jogo decidido (ClassificacaoDeGrupos.ConfrontoDireto).
    private static string? ConfrontoDireto(string a, string b, List<JogoDoTorneio> jogados)
    {
        foreach (var jogo in jogados)
            if (jogo.Envolve(a) && jogo.Envolve(b) && jogo.Vencedor is { } venceu) return venceu;
        return null;
    }
}
