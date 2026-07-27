namespace Padelizou.Services;

// Sorteio das rodadas do Torneio Americano.
//
// A regra do Americano (Felipe, 27/07/2026): todos contra todos, cada jogador faz dupla
// com CADA um dos outros pelo menos uma vez, e no fim vence quem somar mais games.
//
// O sorteio antigo não cumpria isso. Ele embaralhava os jogadores a cada rodada, cortava de
// 4 em 4 e, dentro de cada quarteto, escolhia entre as 3 divisões possíveis a que menos
// repetia parceiro — uma heurística que só enxerga 4 jogadores por vez, nunca o quadro
// inteiro. Medido num ensaio de 8 jogadores: das 28 parcerias possíveis, 4 aconteceram
// DUAS vezes e 4 não aconteceram nenhuma. Além de furar a regra, é injusto: quem calha de
// repetir o parceiro mais forte soma games com vantagem.
//
// A solução é conhecida há mais de um século — o "método do círculo" do round-robin. Um
// jogador fica parado e os outros giram uma cadeira por rodada. Em n-1 rodadas cada jogador
// passa por cada um dos outros exatamente uma vez. Não é heurística: é construção.
public static class RodadasAmericano
{
    // Uma partida: dupla A (A1+A2) contra dupla B (B1+B2).
    public record Confronto(int A1, int A2, int B1, int B2);

    // Quantos jogadores são efetivamente usados: o Americano precisa fechar quadras de 4.
    public static int Aproveitaveis(int inscritos) => inscritos - (inscritos % 4);

    // Quantas rodadas o torneio terá com N jogadores aproveitados.
    public static int Rodadas(int jogadores) => jogadores >= 4 ? jogadores - 1 : 0;

    // Quantas ordens diferentes testar antes de escolher a melhor. O círculo garante as
    // parcerias em QUALQUER ordem, então toda tentativa é válida pela regra principal — o
    // que muda entre elas é só como os adversários se distribuem. 200 rodadas de sorteio
    // são instantâneas e derrubam bem o pior caso.
    private const int Tentativas = 200;

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
    //
    // Escolher gulosamente (pega a primeira dupla, acha a melhor adversária, repete) decide
    // cedo demais: medido com 8 jogadores, alguém acabava enfrentando o mesmo rival 4 das 7
    // rodadas. Olhar a rodada inteira e escolher o conjunto de menor custo derruba isso.
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
