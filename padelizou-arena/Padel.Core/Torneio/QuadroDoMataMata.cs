namespace Padel.Core.Torneio;

/// <summary>
/// O quadro do mata-mata como ÁRVORE: uma lista de vagas de tamanho potência de 2 (a primeira
/// rodada), <c>null</c> = vaga vazia (o adversário de quem pegou bye). O jogo k de uma rodada
/// junta as vagas 2k e 2k+1, e o vencedor dele ocupa a vaga k da rodada seguinte — é a chave
/// desenhada em papel, e é o que a tela vai desenhar.
/// </summary>
public static class QuadroDoMataMata
{
    /// <summary>A menor potência de 2 que cabe todo mundo (<c>ChaveamentoMataMata.MenorPotenciaDe2APartirDe</c>).</summary>
    public static int MenorPotenciaDe2APartirDe(int minimo)
    {
        int p = 1;
        while (p < minimo) p *= 2;
        return p;
    }

    /// <summary>
    /// O nome da rodada pelo tamanho do QUADRO naquela rodada (<c>ChaveamentoMataMata.NomeFase</c>):
    /// com bye, a rodada tem menos jogos do que o nome promete.
    /// </summary>
    public static string NomeDaFase(int vagasNaRodada) => vagasNaRodada switch
    {
        >= 32 => "Primeira Rodada",
        16 => "Oitavas de Final",
        8 => "Quartas de Final",
        4 => "Semifinal",
        _ => "Final",
    };

    public static FaseAlcancada FaseDaRodada(int vagasNaRodada) => vagasNaRodada switch
    {
        >= 32 => FaseAlcancada.PrimeiraRodada,
        16 => FaseAlcancada.Oitavas,
        8 => FaseAlcancada.Quartas,
        4 => FaseAlcancada.Semifinal,
        _ => FaseAlcancada.Final,
    };

    /// <summary>
    /// A CHAVE DIRETA com cabeças de chave: a semeadura clássica (1 × último, e os dois primeiros
    /// só se cruzam na final), com as vagas que sobram virando bye dos melhores.
    /// </summary>
    /// <remarks>
    /// ⚠️ AQUI O JOGO DIVERGE DO PADELIZOU, de propósito. Lá a chave direta NÃO tem cabeça
    /// (<c>ChaveamentoMataMata.MontarChaveDireta</c>: "numa chave direta de duplas remontadas
    /// ninguém tem campanha, e ranking fingido seria pior que sorteio limpo") — os byes saem do
    /// começo da lista sorteada e a geometria é "primeiro × último da lista". No jogo cada dupla
    /// TEM nível (a força), e a tarefa pede cabeças de chave; com cabeças, aquela geometria
    /// cruzaria o 1º e o 2º já na semifinal com 5 ou 7 duplas. O que continua igual ao Padelizou:
    /// quadro = menor potência de 2 que cabe todo mundo, e o bye é dos melhores.
    /// </remarks>
    public static List<string?> Semeado(IReadOnlyList<string> duplasNaOrdemDaSemeadura)
    {
        int n = duplasNaOrdemDaSemeadura.Count;
        int tamanho = MenorPotenciaDe2APartirDe(Math.Max(2, n));
        return OrdemDasCabecas(tamanho)
            .Select(cabeca => cabeca <= n ? duplasNaOrdemDaSemeadura[cabeca - 1] : null)
            .ToList();
    }

    /// <summary>
    /// A árvore que reproduz o cruzamento do Padelizou (<see cref="CruzamentoDosGrupos.MontarPrimeiraFase"/>):
    /// a lista [jogos na ordem, byes do melhor pro pior] cruzada primeiro × último, rodada após
    /// rodada (<c>ChaveamentoMataMata.ParearVencedores</c> + <c>AvancoDaChave</c>), vira posições
    /// fixas na árvore. Assim as metades que a semeadura de lá calculou são as metades daqui.
    /// </summary>
    public static List<string?> DoCruzamento(IReadOnlyList<Confronto> confrontos, IReadOnlyList<string> byes)
    {
        int entradas = confrontos.Count + byes.Count;
        if (entradas == 0) return [];
        if (entradas != MenorPotenciaDe2APartirDe(entradas))
            throw new ArgumentException($"{confrontos.Count} jogo(s) e {byes.Count} bye(s) não fecham um quadro.");

        var quadro = new string?[2 * entradas];
        for (int e = 0; e < entradas; e++)
        {
            int jogo = PosicaoNaArvore(entradas, e);
            if (e < confrontos.Count)
            {
                quadro[2 * jogo] = confrontos[e].DuplaA;
                quadro[2 * jogo + 1] = confrontos[e].DuplaB;
            }
            else
            {
                quadro[2 * jogo] = byes[e - confrontos.Count];
            }
        }
        return quadro.ToList();
    }

    /// <summary>
    /// Semeadura clássica: [1, 2] e, dobrando, cada cabeça s ganha como vizinho o (2n+1−s).
    /// 8 → 1, 8, 4, 5, 2, 7, 3, 6.
    /// </summary>
    internal static int[] OrdemDasCabecas(int tamanho)
    {
        int[] ordem = [1];
        while (ordem.Length < tamanho)
        {
            int dobro = ordem.Length * 2;
            ordem = ordem.SelectMany(s => new[] { s, dobro + 1 - s }).ToArray();
        }
        return ordem;
    }

    /// <summary>
    /// Onde, na árvore, cai a entrada <paramref name="entrada"/> de uma lista de
    /// <paramref name="entradas"/> que cruza primeiro × último: o par (i, n−1−i) é um jogo da
    /// rodada seguinte, na posição da vaga min(i, n−1−i) — recursivo até sobrar a final.
    /// </summary>
    internal static int PosicaoNaArvore(int entradas, int entrada)
    {
        if (entradas <= 1) return 0;
        int par = Math.Min(entrada, entradas - 1 - entrada);
        return 2 * PosicaoNaArvore(entradas / 2, par) + (entrada == par ? 0 : 1);
    }
}
