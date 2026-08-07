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

    // ⚠️ TODO INSCRITO JOGA, de 4 pra cima (Felipe, 06/08/2026).
    //
    // Até esta data o sorteio cortava pro múltiplo de 4 abaixo e quem sobrava ficava de FORA
    // DO TORNEIO INTEIRO — 10 inscritos viravam 8, e duas pessoas que pagaram não entravam em
    // jogo nenhum, avisadas só por uma frase no fim da mensagem do organizador. Pior: 5, 9, 13
    // e 17 fecham a conta perfeitamente e eram recusados à toa.
    //
    // Agora a quadra é que fecha em 4: quem não cabe numa rodada DESCANSA, e o descanso
    // reveza — ninguém descansa duas vezes antes de todo mundo descansar uma.
    public static int Aproveitaveis(int inscritos) => inscritos >= 4 ? inscritos : 0;

    // Quantas duplas o torneio precisa formar pra cada um jogar com cada um.
    private static int DuplasAFormar(int n) => n * (n - 1) / 2;

    // Quantas rodadas isso dá. Não é sempre n-1: com gente descansando, o que manda é quantas
    // duplas cabem por rodada.
    public static int Rodadas(int jogadores)
    {
        if (jogadores < 4) return 0;
        int duplasPorRodada = 2 * (jogadores / 4);
        return (int)Math.Ceiling(DuplasAFormar(jogadores) / (double)duplasPorRodada);
    }

    // "Cada um com cada um" só FECHA CERTINHO quando o número de duplas a formar é par —
    // ou seja, quando n é múltiplo de 4 ou múltiplo de 4 mais 1 (4, 5, 8, 9, 12, 13...).
    //
    // Com 6 pessoas são 15 duplas a formar e cada jogo forma 2: daria 7,5 jogos. Não existe
    // meio jogo, então alguém repete parceiro. Isso é aritmética, não limitação do sistema —
    // e a tela precisa dizer isso ao organizador em vez de fingir que ficou perfeito.
    public static bool FechaCertinho(int jogadores) =>
        jogadores >= 4 && DuplasAFormar(jogadores) % 2 == 0;

    // As rodadas, na ordem. Cada rodada tem jogadores/4 partidas, todas simultâneas —
    // ninguém joga duas vezes na mesma rodada.
    //
    // `sorteio` existe pra o resultado ser reproduzível nos testes. Em produção vem null e
    // cada sorteio sai diferente — dois torneios com os mesmos inscritos não podem gerar a
    // mesma tabela.
    public static List<List<Confronto>> Montar(IReadOnlyList<int> jogadores, Random? sorteio = null)
    {
        int n = jogadores.Count;
        if (n < 4) return new List<List<Confronto>>();

        var rng = sorteio ?? new Random();

        // Múltiplo de 4 com tabela de whist conhecida: ninguém descansa e o desenho é ÓTIMO
        // (parceiro 1x, adversário 2x, provado). Não se troca isso por busca.
        if (n % 4 == 0)
        {
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

        return MontarComRevezamento(jogadores, rng);
    }

    // ----------------------------------------------------------------------------------
    // Tamanho que não é múltiplo de 4: todo mundo joga, revezando o descanso
    // ----------------------------------------------------------------------------------
    //
    // Não há tabela pronta pra estes tamanhos, então é busca: monta rodada a rodada com
    // escolhas gulosas e repete o sorteio inteiro várias vezes, ficando com o melhor.
    //
    // O objetivo, na ordem: (1) todo mundo joga com todo mundo — é a obrigação do formato;
    // (2) menos rodadas; (3) menos parceria repetida; (4) menos reencontro de adversário.
    private const int TentativasComRevezamento = 40;

    private static List<List<Confronto>> MontarComRevezamento(IReadOnlyList<int> jogadores, Random rng)
    {
        List<List<Confronto>>? melhor = null;
        (int Rodadas, int Repeticoes, int PiorAdversario, int Total) melhorNota =
            (int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);

        for (int t = 0; t < TentativasComRevezamento; t++)
        {
            var tentativa = UmaTentativaComRevezamento(jogadores, rng);
            if (tentativa == null) continue;

            var adversarios = Avaliar(tentativa);
            var nota = (tentativa.Count, ParceriasRepetidas(tentativa), adversarios.PiorRepeticao, adversarios.Total);

            if (nota.CompareTo(melhorNota) < 0)
            {
                melhor = tentativa;
                melhorNota = nota;
            }
        }

        return melhor ?? new List<List<Confronto>>();
    }

    private static List<List<Confronto>>? UmaTentativaComRevezamento(
        IReadOnlyList<int> jogadores, Random rng)
    {
        int n = jogadores.Count;
        int porRodada = (n / 4) * 4;   // quantos entram em quadra por rodada
        if (porRodada < 4) return null;

        // Toda dupla que ainda falta acontecer. É a condição de parada: o torneio acaba
        // quando este conjunto esvazia, não num número fixo de rodadas.
        var pendentes = new HashSet<(int, int)>();
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                pendentes.Add(Chave(jogadores[i], jogadores[j]));

        // Quantas duplas cada um ainda tem por fazer. Mantido incremental de propósito:
        // recontar isso dentro da ordenação de cada rodada é O(pares) por jogador e sozinho
        // levava o sorteio de 40 pessoas a segundos — dentro do POST do organizador.
        var faltamPra = jogadores.ToDictionary(j => j, _ => n - 1);

        var descansos = jogadores.ToDictionary(j => j, _ => 0);
        var adversariosAnteriores = new Dictionary<(int, int), int>();
        var rodadas = new List<List<Confronto>>();

        // Trava de segurança: sem ela, um caso patológico giraria pra sempre dentro de uma
        // requisição HTTP. O teto é folgado — o pior tamanho real fecha muito antes.
        int limite = 4 * n + 8;

        while (pendentes.Count > 0 && rodadas.Count < limite)
        {
            // Joga quem DESCANSOU MAIS até agora; empate vai pra quem ainda tem mais duplas
            // por fazer. É isso que mantém o revezamento parelho e o torneio curto.
            var ordem = jogadores
                .OrderByDescending(j => descansos[j])
                .ThenByDescending(j => faltamPra[j])
                .ThenBy(_ => rng.Next())
                .ToList();

            var jogam = ordem.Take(porRodada).ToList();
            foreach (var fora in ordem.Skip(porRodada)) descansos[fora]++;

            var duplas = FormarDuplas(jogam, pendentes, rng);
            if (duplas == null) return null;

            foreach (var (a, b) in duplas)
            {
                if (pendentes.Remove(Chave(a, b)))
                {
                    faltamPra[a]--;
                    faltamPra[b]--;
                }
            }

            rodadas.Add(Emparelhar(duplas, adversariosAnteriores));
        }

        return pendentes.Count == 0 ? rodadas : null;
    }

    // Junta em duplas quem vai jogar a rodada, preferindo pares que ainda não jogaram juntos.
    //
    // Começa por quem tem MENOS opções pendentes dentro do grupo: deixar o mais difícil por
    // último é o que faz a última rodada não fechar.
    private static List<(int, int)>? FormarDuplas(
        List<int> jogam, HashSet<(int, int)> pendentes, Random rng)
    {
        var restantes = new List<int>(jogam);
        var duplas = new List<(int, int)>();

        int OpcoesDe(int quem, List<int> onde) =>
            onde.Count(outro => outro != quem && pendentes.Contains(Chave(quem, outro)));

        while (restantes.Count >= 2)
        {
            var a = restantes
                .OrderBy(x => OpcoesDe(x, restantes))
                .ThenBy(_ => rng.Next())
                .First();
            restantes.Remove(a);

            var aindaFaltam = restantes.Where(b => pendentes.Contains(Chave(a, b))).ToList();

            // Sem par pendente disponível, repete uma parceria: nos tamanhos em que a conta
            // não fecha (6, 7, 10, 11...) isso é inevitável — ver FechaCertinho.
            var b2 = (aindaFaltam.Count > 0 ? aindaFaltam : restantes)
                .OrderBy(x => OpcoesDe(x, restantes))
                .ThenBy(_ => rng.Next())
                .First();

            restantes.Remove(b2);
            duplas.Add((a, b2));
        }

        return restantes.Count == 0 ? duplas : null;
    }

    // Quantas parcerias aconteceram mais de uma vez. Zero é o ideal, e é alcançável só nos
    // tamanhos em que FechaCertinho é verdade.
    private static int ParceriasRepetidas(List<List<Confronto>> rodadas)
    {
        var conta = new Dictionary<(int, int), int>();
        foreach (var c in rodadas.SelectMany(r => r))
        {
            foreach (var par in new[] { Chave(c.A1, c.A2), Chave(c.B1, c.B2) })
                conta[par] = conta.GetValueOrDefault(par) + 1;
        }
        return conta.Values.Where(v => v > 1).Sum(v => v - 1);
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
