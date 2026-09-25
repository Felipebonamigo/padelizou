namespace Padel.Core.Torneio;

/// <summary>Um classificado de grupo: <see cref="Posicao"/> 1 é o 1º do grupo; <see cref="Jogos"/> é quantos jogou no grupo.</summary>
public sealed record Classificado(string Dupla, string Grupo, int Vitorias, int Saldo, int Posicao, int Jogos);

/// <summary>Um jogo da primeira rodada do mata-mata.</summary>
public sealed record Confronto(string DuplaA, string DuplaB);

/// <summary>
/// O cruzamento dos grupos pra chave — o motor do Padelizou
/// (<c>Padelizou/Services/ChaveamentoMataMata.cs</c>, <c>MontarPrimeiraFase</c> e a semeadura),
/// reimplementado sem banco. O desenho à mão (<c>CruzamentoDoMataMata</c>) fica de fora: é
/// ferramenta de organizador, e no jogo ninguém organiza.
/// <list type="bullet">
/// <item>TODO classificado entra: o quadro é a menor potência de 2 que cabe todo mundo, e as
/// vagas que sobram viram bye;</item>
/// <item>o bye é dos melhores por POSIÇÃO no grupo, depois de quem jogou menos, depois da ordem
/// do grupo — nunca da campanha (<see cref="OrdemDosByes"/>);</item>
/// <item>quem joga abre "melhor contra pior", e os dois classificados de um grupo caem em METADES
/// opostas da chave — contando quem descansa — pra só se reencontrarem na final.</item>
/// </list>
/// </summary>
public static class CruzamentoDosGrupos
{
    /// <summary>
    /// Quem descansa, do primeiro bye pro último (<c>ChaveamentoMataMata.OrdemDosByes</c>):
    /// posição no grupo; depois quem jogou MENOS (o 1º do grupo de 2); depois o grupo (o A é o
    /// dos cabeças de chave).
    /// </summary>
    public static IEnumerable<Classificado> OrdemDosByes(IEnumerable<Classificado> classificados) =>
        classificados.OrderBy(c => c.Posicao).ThenBy(c => c.Jogos).ThenBy(c => c.Grupo, StringComparer.Ordinal);

    /// <summary>
    /// A primeira rodada do mata-mata: os jogos, na ordem do quadro, e os byes, do melhor pro
    /// pior. É essa ordem que decide as metades da chave (ver <see cref="LadoDaVaga"/>); quem
    /// transforma isso em árvore é <see cref="QuadroDoMataMata.DoCruzamento"/>.
    /// </summary>
    public static (List<Confronto> Confrontos, List<string> Byes) MontarPrimeiraFase(
        IReadOnlyList<Classificado> classificados, int classificadosPorGrupo = 2)
    {
        // Quem joga é semeado pela campanha: posição primeiro (todo 1º antes de qualquer 2º),
        // depois vitórias e saldo — é o que faz "o melhor abre o jogo contra o pior".
        var candidatos = classificados
            .Where(c => c.Posicao >= 1 && c.Posicao <= classificadosPorGrupo)
            .OrderBy(c => c.Posicao)
            .ThenByDescending(c => c.Vitorias).ThenByDescending(c => c.Saldo).ThenBy(c => c.Grupo, StringComparer.Ordinal)
            .ToList();

        if (candidatos.Count < 2) return ([], []);

        int quadro = QuadroDoMataMata.MenorPotenciaDe2APartirDe(candidatos.Count);
        var passamDireto = OrdemDosByes(candidatos).Take(quadro - candidatos.Count).ToList();
        var cabecas = candidatos.Where(c => !passamDireto.Contains(c)).ToList();

        var confrontos = Semear(cabecas, passamDireto)
            .Select(jogo => new Confronto(jogo.Mandante.Dupla, jogo.Adversario.Dupla))
            .ToList();
        return (confrontos, passamDireto.Select(c => c.Dupla).ToList());
    }

    /// <summary>
    /// A metade (0 ou 1) em que cai a vaga <paramref name="vaga"/> de uma rodada com
    /// <paramref name="vagas"/> participantes, pela geometria do Padelizou
    /// (<c>ChaveamentoMataMata.LadoDaVaga</c>): a lista da rodada cruza primeiro × último, e o
    /// vencedor do par (i, n−1−i) ocupa a vaga min(i, n−1−i) da rodada seguinte.
    /// </summary>
    public static int LadoDaVaga(int vagas, int vaga)
    {
        while (vagas > 2)
        {
            vaga = Math.Min(vaga, vagas - 1 - vaga);
            vagas /= 2;
        }
        return vaga;
    }

    private readonly record struct Jogo(Classificado Mandante, Classificado Adversario);

    // ChaveamentoMataMata.Semear: a semeadura de sempre, e a busca quando ela deixa dois do mesmo
    // grupo na mesma metade (o "caso do Er" de 10/09/2026, em que o bye também tem lado).
    private static List<Jogo> Semear(List<Classificado> cabecas, List<Classificado> byes)
    {
        int jogos = cabecas.Count / 2;
        int vagas = jogos + byes.Count;   // a lista da rodada seguinte: vencedores na ordem dos jogos, byes depois

        var ladoDoJogo = new int[jogos];
        for (int k = 0; k < jogos; k++) ladoDoJogo[k] = LadoDaVaga(vagas, k);

        var gruposDosByes = new[] { new HashSet<string>(), new HashSet<string>() };
        for (int b = 0; b < byes.Count; b++)
            gruposDosByes[LadoDaVaga(vagas, jogos + b)].Add(byes[b].Grupo);

        var deSempre = SemearNaOrdem(cabecas, ladoDoJogo, gruposDosByes);
        if (SemReencontroAntesDaFinal(deSempre, ladoDoJogo, gruposDosByes)) return deSempre;
        return ProcurarArranjoPerfeito(cabecas, ladoDoJogo, gruposDosByes) ?? deSempre;
    }

    // ChaveamentoMataMata.SemearNaOrdem: os melhores abrem os jogos, na ordem; cada um pega o pior
    // que cabe no lado dele.
    private static List<Jogo> SemearNaOrdem(List<Classificado> cabecas, int[] ladoDoJogo, HashSet<string>[] gruposDosByes)
    {
        int jogos = cabecas.Count / 2;
        var mandantes = cabecas.Take(jogos).ToList();
        var adversarios = cabecas.Skip(jogos).ToList();   // do melhor pro pior

        var gruposNoLado = new[] { new HashSet<string>(gruposDosByes[0]), new HashSet<string>(gruposDosByes[1]) };
        for (int i = 0; i < jogos; i++) gruposNoLado[ladoDoJogo[i]].Add(mandantes[i].Grupo);

        var confrontos = new List<Jogo>(jogos);
        for (int i = 0; i < jogos; i++)
        {
            var escolhido = EscolherAdversario(adversarios, mandantes[i], gruposNoLado[ladoDoJogo[i]]);
            adversarios.Remove(escolhido);
            gruposNoLado[ladoDoJogo[i]].Add(escolhido.Grupo);
            confrontos.Add(new Jogo(mandantes[i], escolhido));
        }
        return confrontos;
    }

    // ChaveamentoMataMata.EscolherAdversario: do pior pro melhor, (1) sem ninguém do grupo dele
    // nesta metade, (2) de outro grupo que não o do mandante; afrouxa se não sobrar ninguém.
    private static Classificado EscolherAdversario(List<Classificado> disponiveis, Classificado mandante, HashSet<string> gruposDesteLado)
    {
        for (int i = disponiveis.Count - 1; i >= 0; i--)
            if (!gruposDesteLado.Contains(disponiveis[i].Grupo) && disponiveis[i].Grupo != mandante.Grupo)
                return disponiveis[i];
        for (int i = disponiveis.Count - 1; i >= 0; i--)
            if (disponiveis[i].Grupo != mandante.Grupo)
                return disponiveis[i];
        return disponiveis[^1];
    }

    // ChaveamentoMataMata.SemReencontroAntesDaFinal: nenhum jogo entre o mesmo grupo e nenhum
    // grupo com duas duplas na mesma metade, contando os byes.
    private static bool SemReencontroAntesDaFinal(List<Jogo> arranjo, int[] ladoDoJogo, HashSet<string>[] gruposDosByes)
    {
        var gruposNoLado = new[] { new HashSet<string>(gruposDosByes[0]), new HashSet<string>(gruposDosByes[1]) };
        for (int k = 0; k < arranjo.Count; k++)
        {
            var lado = gruposNoLado[ladoDoJogo[k]];
            var (mandante, adversario) = arranjo[k];
            if (mandante.Grupo == adversario.Grupo || !lado.Add(mandante.Grupo) || !lado.Add(adversario.Grupo))
                return false;
        }
        return true;
    }

    // Mesmo teto do Padelizou: dado torto não vira laço longo; estourou, vale a semeadura de sempre.
    private const int TetoDaBusca = 5_000;

    // ChaveamentoMataMata.ProcurarArranjoPerfeito: o melhor ainda sem jogo abre o próximo, contra
    // o pior que ainda cabe, no primeiro jogo em que os dois cabem — voltando atrás quando o
    // caminho não fecha. Null = não existe arranjo sem reencontro (ou a busca estourou o teto).
    private static List<Jogo>? ProcurarArranjoPerfeito(List<Classificado> cabecas, int[] ladoDoJogo, HashSet<string>[] gruposDosByes)
    {
        var quantosPorGrupo = cabecas.Select(c => c.Grupo)
            .Concat(gruposDosByes.SelectMany(g => g))
            .GroupBy(g => g).Select(g => g.Count());
        if (quantosPorGrupo.Any(n => n > 2)) return null;

        int jogos = cabecas.Count / 2;
        var gruposNoLado = new[] { new HashSet<string>(gruposDosByes[0]), new HashSet<string>(gruposDosByes[1]) };
        var jaTemJogo = new bool[cabecas.Count];
        var arranjo = new Jogo?[jogos];
        int passos = 0;

        bool Preencher()
        {
            int x = Array.IndexOf(jaTemJogo, false);
            if (x < 0) return true;
            if (++passos > TetoDaBusca) return false;

            var grupoDeX = cabecas[x].Grupo;
            jaTemJogo[x] = true;
            for (int y = cabecas.Count - 1; y > x; y--)
            {
                var grupoDeY = cabecas[y].Grupo;
                if (jaTemJogo[y] || grupoDeY == grupoDeX) continue;
                for (int jogo = 0; jogo < jogos; jogo++)
                {
                    var lado = gruposNoLado[ladoDoJogo[jogo]];
                    if (arranjo[jogo] is not null || lado.Contains(grupoDeX) || lado.Contains(grupoDeY)) continue;

                    jaTemJogo[y] = true;
                    lado.Add(grupoDeX);
                    lado.Add(grupoDeY);
                    arranjo[jogo] = new Jogo(cabecas[x], cabecas[y]);
                    if (Preencher()) return true;
                    arranjo[jogo] = null;
                    lado.Remove(grupoDeY);
                    lado.Remove(grupoDeX);
                    jaTemJogo[y] = false;
                }
            }
            jaTemJogo[x] = false;
            return false;
        }

        if (!Preencher()) return null;

        var completo = new List<Jogo>(jogos);
        foreach (var jogo in arranjo)
        {
            if (jogo is not Jogo preenchido) return null;   // não acontece: Preencher só devolve true com tudo preenchido
            completo.Add(preenchido);
        }
        return completo;
    }
}
