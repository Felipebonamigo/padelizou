namespace Padelizou.Services;

// Sorteio das rodadas do Torneio Americano.
//
// A regra do Americano (Felipe, 27/07/2026): todos contra todos, cada jogador faz dupla
// com CADA um dos outros pelo menos uma vez, e no fim vence quem somar mais games.
//
// Desde 29/07/2026 o sorteio cumpre também a metade que faltava: os ADVERSÁRIOS. Cada
// jogador enfrenta cada um dos outros EXATAMENTE duas vezes — nem preso no mesmo rival,
// nem sem cruzar com alguém. Isso tem nome na matemática: torneio de whist, estudado
// desde os anos 1890 exatamente pra este problema.
//
// Não se calcula um whist na hora: as tabelas foram ENCONTRADAS por busca (feita fora do
// sistema, uma vez) e ficam embutidas aqui como dado, já conferidas pelos testes. Pra
// 12..32 jogadores a tabela é "cíclica": uma rodada-base boa o suficiente pra que girar
// os números gere o torneio inteiro perfeito. Pra 8 não existe base cíclica (foi testado
// exaustivamente: nenhum dos 3 agrupamentos serve) e a tabela vai completa, rodada a
// rodada. O sorteio de verdade continua existindo: quem é o "jogador 3" muda a cada
// torneio — o desenho é fixo, as pessoas dentro dele não.
//
// Tamanho sem tabela (36+) cai no método antigo: círculo pros parceiros (perfeito) e
// otimização por rodada pros adversários (bom, não perfeito).
public static class RodadasAmericano
{
    // Uma partida: dupla A (A1+A2) contra dupla B (B1+B2).
    public record Confronto(int A1, int A2, int B1, int B2);

    // Quantos jogadores são efetivamente usados: o Americano precisa fechar quadras de 4.
    public static int Aproveitaveis(int inscritos) => inscritos - (inscritos % 4);

    // Quantas rodadas o torneio terá com N jogadores aproveitados.
    public static int Rodadas(int jogadores) => jogadores >= 4 ? jogadores - 1 : 0;

    // As rodadas, na ordem. Cada rodada tem jogadores/4 partidas, todas simultâneas —
    // ninguém joga duas vezes na mesma rodada.
    //
    // `sorteio` existe pra o resultado ser reproduzível nos testes. Em produção vem null e
    // cada sorteio sai diferente — dois torneios com os mesmos inscritos não podem gerar a
    // mesma tabela.
    public static List<List<Confronto>> Montar(IReadOnlyList<int> jogadores, Random? sorteio = null)
    {
        int n = jogadores.Count;
        if (n < 4 || n % 4 != 0) return new List<List<Confronto>>();

        var rng = sorteio ?? new Random();

        var rodadasDeRotulos =
            n == 8 ? TabelaWh8.Select(r => r.Select(m => (int[])m.Clone()).ToList()).ToList()
            : BasesCiclicas.TryGetValue(n, out var baseCiclica) ? ExpandirBase(n, baseCiclica)
            : null;

        if (rodadasDeRotulos == null) return MontarPorCirculo(jogadores, rng);

        // O desenho é fixo; o SORTEIO é decidir qual pessoa veste qual número. Embaralhar
        // esse mapa dá n! tabelas diferentes, todas igualmente perfeitas.
        var mapa = jogadores.OrderBy(_ => rng.Next()).ToList();

        var rodadas = new List<List<Confronto>>();
        foreach (var rodada in rodadasDeRotulos)
        {
            var confrontos = rodada
                .Select(m => new Confronto(mapa[m[0]], mapa[m[1]], mapa[m[2]], mapa[m[3]]))
                .OrderBy(_ => rng.Next())   // ordem das quadras também sorteada
                .ToList();
            rodadas.Add(confrontos);
        }
        return rodadas;
    }

    // ----------------------------------------------------------------------------------
    // As tabelas de whist
    // ----------------------------------------------------------------------------------

    // Rodada-base cíclica pra n jogadores: rótulos são o "fixo" (-1, que os testes chamam
    // de ∞) e 0..n-2. A rodada r soma r a todo rótulo que não é o fixo, módulo n-1.
    //
    // O que faz uma base ser válida (e foi conferido na busca E é conferido nos testes):
    // as duplas cobrem cada "classe de diferença" exatamente 1x (parceiros) e os
    // cruzamentos de adversário cobrem cada classe exatamente 2x.
    private static readonly Dictionary<int, int[][]> BasesCiclicas = new()
    {
        // n=4 é o caso trivial: 1 quadra por rodada, e girar já dá o whist perfeito.
        [4] = new[] { new[] { -1, 0, 1, 2 } },
        [12] = new[]
        {
            new[] { -1, 0, 4, 6 }, new[] { 1, 7, 8, 9 }, new[] { 2, 5, 3, 10 },
        },
        [16] = new[]
        {
            new[] { -1, 0, 7, 13 }, new[] { 1, 8, 3, 4 }, new[] { 2, 14, 5, 9 },
            new[] { 6, 11, 10, 12 },
        },
        [20] = new[]
        {
            new[] { -1, 0, 9, 12 }, new[] { 1, 6, 10, 14 }, new[] { 2, 8, 5, 7 },
            new[] { 3, 13, 11, 18 }, new[] { 4, 15, 16, 17 },
        },
        [24] = new[]
        {
            new[] { -1, 0, 13, 22 }, new[] { 1, 21, 5, 12 }, new[] { 2, 8, 6, 17 },
            new[] { 3, 4, 9, 19 }, new[] { 7, 15, 18, 20 }, new[] { 10, 14, 11, 16 },
        },
        [28] = new[]
        {
            new[] { -1, 0, 3, 24 }, new[] { 1, 26, 6, 21 }, new[] { 2, 25, 12, 15 },
            new[] { 4, 23, 8, 19 }, new[] { 5, 22, 13, 14 }, new[] { 7, 20, 9, 18 },
            new[] { 10, 17, 11, 16 },
        },
        [32] = new[]
        {
            new[] { -1, 0, 17, 26 }, new[] { 1, 13, 22, 29 }, new[] { 2, 5, 9, 11 },
            new[] { 3, 21, 4, 18 }, new[] { 6, 12, 10, 30 }, new[] { 7, 23, 15, 25 },
            new[] { 8, 16, 27, 28 }, new[] { 14, 19, 20, 24 },
        },
    };

    // 8 jogadores não tem base cíclica — os 3 agrupamentos possíveis da rodada-base foram
    // testados e nenhum fecha as contas. A tabela vai completa (rótulos 0..7).
    private static readonly int[][][] TabelaWh8 =
    {
        new[] { new[] { 0, 5, 4, 1 }, new[] { 2, 3, 7, 6 } },
        new[] { new[] { 0, 2, 6, 1 }, new[] { 3, 5, 4, 7 } },
        new[] { new[] { 0, 4, 3, 6 }, new[] { 1, 5, 2, 7 } },
        new[] { new[] { 0, 7, 2, 4 }, new[] { 1, 3, 6, 5 } },
        new[] { new[] { 0, 6, 7, 5 }, new[] { 1, 2, 3, 4 } },
        new[] { new[] { 0, 3, 5, 2 }, new[] { 1, 7, 4, 6 } },
        new[] { new[] { 0, 1, 7, 3 }, new[] { 2, 6, 4, 5 } },
    };

    private static List<List<int[]>> ExpandirBase(int n, int[][] baseCiclica)
    {
        int m = n - 1;
        var rodadas = new List<List<int[]>>();
        for (int r = 0; r < m; r++)
        {
            var rodada = baseCiclica
                .Select(mesa => mesa.Select(x => x == -1 ? m : (x + r) % m).ToArray())
                .ToList();
            rodadas.Add(rodada);
        }
        return rodadas;
    }

    // ----------------------------------------------------------------------------------
    // Método antigo (círculo + otimização por rodada) — só pra tamanho sem tabela (36+).
    // Parceiros perfeitos garantidos; adversários bons, não perfeitos.
    // ----------------------------------------------------------------------------------

    private const int Tentativas = 200;

    private static List<List<Confronto>> MontarPorCirculo(IReadOnlyList<int> jogadores, Random rng)
    {
        List<List<Confronto>>? melhor = null;
        (int PiorRepeticao, int Total) melhorNota = (int.MaxValue, int.MaxValue);

        for (int t = 0; t < Tentativas; t++)
        {
            var ordem = jogadores.OrderBy(_ => rng.Next()).ToList();
            var tentativa = MontarNaOrdem(ordem);
            var nota = Avaliar(tentativa);

            if (nota.PiorRepeticao < melhorNota.PiorRepeticao
                || (nota.PiorRepeticao == melhorNota.PiorRepeticao && nota.Total < melhorNota.Total))
            {
                melhor = tentativa;
                melhorNota = nota;
            }
        }

        return melhor!;
    }

    // Quão bem os ADVERSÁRIOS ficaram distribuídos: primeiro o pior caso (quantas vezes
    // alguém reencontra o mesmo rival), depois o total de reencontros.
    private static (int PiorRepeticao, int Total) Avaliar(List<List<Confronto>> rodadas)
    {
        var conta = new Dictionary<(int, int), int>();
        foreach (var c in rodadas.SelectMany(r => r))
            foreach (var x in new[] { c.A1, c.A2 })
                foreach (var y in new[] { c.B1, c.B2 })
                {
                    var k = Chave(x, y);
                    conta[k] = conta.GetValueOrDefault(k) + 1;
                }

        return (conta.Values.DefaultIfEmpty(0).Max(),
                conta.Values.Where(v => v > 1).Sum(v => v - 1));
    }

    private static List<List<Confronto>> MontarNaOrdem(IReadOnlyList<int> jogadores)
    {
        var rodadas = new List<List<Confronto>>();
        int n = jogadores.Count;

        var fixo = jogadores[0];
        var giram = jogadores.Skip(1).ToList();   // n-1 posições no círculo
        int m = giram.Count;

        // Quantas vezes cada par de jogadores já se ENFRENTOU. Os parceiros o círculo já
        // resolve sozinho; os adversários ficam por conta de uma escolha gulosa.
        var confrontosAnteriores = new Dictionary<(int, int), int>();

        for (int r = 0; r < m; r++)
        {
            var duplas = new List<(int, int)> { (fixo, giram[r]) };

            for (int i = 1; i < n / 2; i++)
            {
                var a = giram[(r + i) % m];
                var b = giram[((r - i) % m + m) % m];
                duplas.Add((a, b));
            }

            rodadas.Add(Emparelhar(duplas, confrontosAnteriores));
        }

        return rodadas;
    }

    // Acima disto a busca exaustiva explode ((k-1)!! emparelhamentos). 12 duplas = 24
    // jogadores dão 10.395 combinações por rodada: instantâneo. 20 duplas dariam 654
    // milhões, e aí o guloso resolve.
    private const int MaximoParaBuscaExaustiva = 12;

    // Junta as duplas da rodada em partidas, preferindo adversários que ainda não se
    // enfrentaram — o círculo garante o PARCEIRO, não o adversário.
    private static List<Confronto> Emparelhar(
        List<(int, int)> duplas, Dictionary<(int, int), int> anteriores)
    {
        var escolhido = duplas.Count <= MaximoParaBuscaExaustiva
            ? MelhorEmparelhamento(duplas, anteriores)
            : EmparelhamentoGuloso(duplas, anteriores);

        foreach (var (a, b) in escolhido) Registrar(a, b, anteriores);

        return escolhido
            .Select(p => new Confronto(p.Item1.Item1, p.Item1.Item2, p.Item2.Item1, p.Item2.Item2))
            .ToList();
    }

    // Testa todas as formas de dividir as duplas da rodada em partidas e fica com a de
    // menor custo (menos reencontros de adversários já vistos).
    private static List<((int, int), (int, int))> MelhorEmparelhamento(
        List<(int, int)> duplas, Dictionary<(int, int), int> anteriores)
    {
        List<((int, int), (int, int))>? campeao = null;
        int menorCusto = int.MaxValue;

        void Buscar(List<(int, int)> restantes, List<((int, int), (int, int))> atual, int custo)
        {
            if (custo >= menorCusto) return;            // já é pior que o melhor conhecido
            if (restantes.Count == 0)
            {
                menorCusto = custo;
                campeao = new List<((int, int), (int, int))>(atual);
                return;
            }

            // A primeira dupla sempre entra: o que muda é contra quem ela joga.
            var primeira = restantes[0];
            for (int i = 1; i < restantes.Count; i++)
            {
                var sobra = new List<(int, int)>(restantes);
                var segunda = sobra[i];
                sobra.RemoveAt(i);
                sobra.RemoveAt(0);

                atual.Add((primeira, segunda));
                Buscar(sobra, atual, custo + Custo(primeira, segunda, anteriores));
                atual.RemoveAt(atual.Count - 1);
            }
        }

        Buscar(duplas, new List<((int, int), (int, int))>(), 0);
        return campeao ?? EmparelhamentoGuloso(duplas, anteriores);
    }

    private static List<((int, int), (int, int))> EmparelhamentoGuloso(
        List<(int, int)> duplas, Dictionary<(int, int), int> anteriores)
    {
        var restantes = new List<(int, int)>(duplas);
        var pares = new List<((int, int), (int, int))>();

        while (restantes.Count >= 2)
        {
            var primeira = restantes[0];
            restantes.RemoveAt(0);

            int escolhida = 0, menorCusto = int.MaxValue;
            for (int i = 0; i < restantes.Count; i++)
            {
                int custo = Custo(primeira, restantes[i], anteriores);
                if (custo < menorCusto) { menorCusto = custo; escolhida = i; }
            }

            var segunda = restantes[escolhida];
            restantes.RemoveAt(escolhida);
            pares.Add((primeira, segunda));
        }

        return pares;
    }

    private static int Custo((int, int) a, (int, int) b, Dictionary<(int, int), int> anteriores)
    {
        int total = 0;
        foreach (var x in new[] { a.Item1, a.Item2 })
            foreach (var y in new[] { b.Item1, b.Item2 })
                total += anteriores.GetValueOrDefault(Chave(x, y));
        return total;
    }

    private static void Registrar((int, int) a, (int, int) b, Dictionary<(int, int), int> anteriores)
    {
        foreach (var x in new[] { a.Item1, a.Item2 })
            foreach (var y in new[] { b.Item1, b.Item2 })
            {
                var k = Chave(x, y);
                anteriores[k] = anteriores.GetValueOrDefault(k) + 1;
            }
    }

    private static (int, int) Chave(int a, int b) => a < b ? (a, b) : (b, a);
}
