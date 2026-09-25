using System.Diagnostics;
using System.Numerics;
using Padel.Core.Torneio;

namespace Padel.Core.Rodizio;

/// <summary>Um jogo da tabela, por ÍNDICE na lista de jogadores: A1 e A2 contra B1 e B2.</summary>
public sealed record JogoDaTabela(int A1, int A2, int B1, int B2);

/// <summary>Uma rodada da tabela: os jogos (um por quadra) e quem folga, por índice.</summary>
public sealed record RodadaDaTabela(IReadOnlyList<JogoDaTabela> Jogos, IReadOnlyList<int> Folgas);

/// <summary>
/// A tabela de rodadas do Americano, de 4 a 16 jogadores: N/4 quadras por rodada, quem sobra folga.
/// Ela sai de um DESENHO fixo por N (abaixo) com os jogadores embaralhados pela semente — trocar
/// os nomes de lugar não mexe em nenhuma propriedade do desenho, então toda semente tem as mesmas
/// garantias, e a mesma semente dá a mesma tabela.
/// </summary>
/// <remarks>
/// <para>O que cada desenho garante no ciclo padrão (N−1 rodadas se N é múltiplo de 4; N nos outros),
/// conferido pelos testes para TODO N de 4 a 16:</para>
/// <list type="bullet">
/// <item>nenhuma dupla se repete; com N ≡ 0 ou 1 (mod 4), todo par é dupla exatamente uma vez;</item>
/// <item>adversários: com N ≡ 0 ou 1 (mod 4), todo par se enfrenta exatamente 2 vezes (espalhamento
/// 0). Nos outros a média fica entre 1 e 2 e o ideal é cada par se enfrentar 1 ou 2 vezes —
/// atingido em 6, 11, 14 e 15; em 7 e 10 ninguém se enfrenta mais de 2 vezes, mas alguns pares
/// (3 de 21 em 7; 5 de 45 em 10) nunca se enfrentam;</item>
/// <item>folgas: a cada rodada (não só no fim) a diferença de folgas entre dois jogadores é no máximo
/// 1, os jogos idem, e ninguém folga em duas rodadas seguidas — inclusive na virada do ciclo;</item>
/// <item>no ciclo inteiro todo mundo folga exatamente N mod 4 vezes e joga o mesmo número de jogos.</item>
/// </list>
/// <para><b>Os desenhos.</b> Múltiplo de 4 é um <i>torneio de whist</i> Wh(N): N−1 rodadas, cada par
/// é dupla uma vez e adversário duas. Com 4n+1 jogadores também existe Wh(N), com um folgando por
/// rodada. A existência de Wh(v) para todo v ≡ 0, 1 (mod 4) é de R. D. Baker, "Whist tournaments"
/// (6th Southeastern Conference on Combinatorics, 1975); a teoria e as construções cíclicas estão em
/// I. Anderson, <i>Combinatorial Designs and Tournaments</i> (Oxford University Press, 1997), no
/// capítulo sobre whist. Aqui eles são Z-cíclicos: uma rodada inicial "desenvolvida" somando i a
/// cada jogador (mod m) na rodada i. As rodadas iniciais abaixo foram achadas por busca exaustiva
/// (25/09/2026) e quem as prende é o teste, não a fonte.</para>
/// <list type="bullet">
/// <item>4, 8, 12, 16: Z<sub>N−1</sub> ∪ {∞} — o ∞ (índice N−1) fica parado e os outros giram.</item>
/// <item>5, 13: Z<sub>N</sub>, o 0 folga na rodada inicial (na rodada i folga o i).</item>
/// <item>9: não existe Wh(9) Z<sub>9</sub>-cíclico (a busca exaustiva não acha); sobre Z<sub>3</sub>×Z<sub>3</sub> existe.</item>
/// <item>11 e 15: Z<sub>N</sub>-cíclico com folgas fixas {0, 4, 8} e {0, 5, 10} — espaçadas por igual,
/// o que dá o equilíbrio de folgas em toda rodada.</item>
/// <item>10 e 14: duas órbitas de Z<sub>N/2</sub> (jogador = (x, lado)), rodadas A0 B0 A1 B1…, folgas
/// {(0,0),(0,1)} em A e {(c,0),(c,1)} em B, c = (N/2+1)/2 — cada um folga a N/2 rodadas de
/// distância. Com 10, nenhum desenho dessa forma deixa todo par se enfrentando 1 ou 2 vezes (busca
/// exaustiva): ou algum par nunca se enfrenta, ou algum se enfrenta 3 vezes. Ficou o primeiro —
/// reencontrar o mesmo adversário pela terceira vez se nota mais do que nunca cruzar com alguém.</item>
/// <item>6 e 7: tabela literal, achada por busca local (late acceptance hill climbing) minimizando
/// duplas repetidas e a soma dos quadrados dos confrontos. Em 7, nenhum desenho Z<sub>7</sub>-cíclico
/// com folgas espaçadas por igual faz todo par se enfrentar (a menos de translação são só três
/// conjuntos de folga — {0,2,4}, {0,2,5}, {0,3,5} — e a busca exaustiva fecha os três); a busca
/// local fora dos cíclicos também não achou, mas isso não prova que não existe.</item>
/// </list>
/// <para><b>Passando do ciclo.</b> Com N ≡ 0, 1 (mod 4) todo par já foi dupla, então repetir é
/// inevitável: a tabela recomeça (a rodada r usa a r mod ciclo), cada par repete uma vez antes de
/// qualquer um repetir duas e os adversários seguem o whist. Com N ≡ 2, 3 sobram duplas inéditas
/// no fim do ciclo (3 em 6, 5 em 10, 7 em 14; 7 em 7, 11 em 11, 15 em 15), e cada rodada seguinte
/// sai de uma busca exata (<see cref="Continuacao"/>): entre as rodadas justas — folgas com
/// diferença ≤ 1 e ninguém folgando duas seguidas —, nunca uma com dupla na (c+1)ª parceria se
/// havia uma só com duplas de até c. Com c = 0: dupla repetida só quando nenhuma rodada justa
/// evita (a rodada N+1 é toda inédita com 6, 10, 11 e 14; com 7 e 15 nenhuma rodada justa é, e ela
/// repete uma dupla só). Em dois ciclos todo par é dupla 1 ou 2 vezes e o espalhamento de
/// adversários fica em 2 (1 com 15) — medido pelos testes, não provado ótimo.</para>
/// </remarks>
public static class TabelaDoAmericano
{
    public const int MinimoDeJogadores = 4;
    public const int MaximoDeJogadores = 16;

    /// <summary>
    /// Teto de sanidade: 4 ciclos do maior evento (16 jogadores, 15 rodadas) com folga. Existe pra
    /// um arquivo ou regra torta não pedir um milhão de rodadas, não por limite do desenho.
    /// </summary>
    public const int MaximoDeRodadas = 64;

    // O acaso da tabela deriva da semente do evento por esta "parte" (Sementes.Misturar), longe dos
    // números de jogo (1 a 256), que o evento usa pra simular cada jogo.
    private const uint ParteDaTabela = 0x7AB1E;

    /// <summary>
    /// O ciclo: N−1 rodadas se N é múltiplo de 4 (todo par é dupla uma vez); N nos outros — o
    /// menor número de rodadas, a partir de N−1, em que todos jogam o mesmo número de jogos (cada
    /// um folga N mod 4 vezes).
    /// </summary>
    public static int RodadasPadrao(int jogadores)
    {
        ValidarJogadores(jogadores);
        return jogadores % 4 == 0 ? jogadores - 1 : jogadores;
    }

    /// <param name="jogadores">De 4 a 16.</param>
    /// <param name="semente">Embaralha quem é quem no desenho: mesma semente, mesma tabela.</param>
    /// <param name="rodadas">De 1 a <see cref="MaximoDeRodadas"/>; <c>null</c> é <see cref="RodadasPadrao"/>.</param>
    public static IReadOnlyList<RodadaDaTabela> Montar(int jogadores, uint semente, int? rodadas = null)
    {
        int total = rodadas ?? RodadasPadrao(jogadores);
        ValidarJogadores(jogadores);
        if (total is < 1 or > MaximoDeRodadas)
            throw new ArgumentOutOfRangeException(nameof(rodadas), total, $"O Americano tem de 1 a {MaximoDeRodadas} rodadas.");

        int[][] modelos = Modelos(jogadores, total);
        int[] quem = Embaralhar(jogadores, new Aleatorio(Sementes.Misturar(semente, ParteDaTabela)));
        var tabela = new List<RodadaDaTabela>(total);
        for (int r = 0; r < total; r++)
        {
            int[] modelo = modelos[r];
            var jogos = new List<JogoDaTabela>(modelo.Length / 4);
            var joga = new bool[jogadores];
            for (int k = 0; k < modelo.Length; k += 4)
            {
                jogos.Add(new JogoDaTabela(quem[modelo[k]], quem[modelo[k + 1]], quem[modelo[k + 2]], quem[modelo[k + 3]]));
                for (int i = k; i < k + 4; i++) joga[quem[modelo[i]]] = true;
            }
            var folgas = Enumerable.Range(0, jogadores).Where(p => !joga[p]).ToList();
            tabela.Add(new RodadaDaTabela(jogos, folgas));
        }
        return tabela;
    }

    /// <summary>
    /// As <paramref name="total"/> rodadas do desenho, antes do embaralhamento. Até o fim do ciclo é
    /// o desenho. Depois: com N ≡ 0, 1 (mod 4) o ciclo recomeça; com N ≡ 2, 3 cada rodada vem da
    /// <see cref="Continuacao"/>, que enxerga tudo o que já foi jogado.
    /// </summary>
    private static int[][] Modelos(int n, int total)
    {
        int[][] ciclo = Desenho(n);
        bool recomeca = n % 4 is 0 or 1;
        var modelos = new int[total][];
        Continuacao? continuacao = null;
        for (int r = 0; r < total; r++)
        {
            if (r < ciclo.Length || recomeca)
            {
                modelos[r] = ciclo[r % ciclo.Length];
                continue;
            }
            continuacao ??= new Continuacao(n, ciclo);
            modelos[r] = continuacao.Proxima();
        }
        return modelos;
    }

    private static void ValidarJogadores(int jogadores)
    {
        if (jogadores is < MinimoDeJogadores or > MaximoDeJogadores)
            throw new ArgumentOutOfRangeException(nameof(jogadores), jogadores,
                $"O Americano é de {MinimoDeJogadores} a {MaximoDeJogadores} jogadores.");
    }

    /// <summary>Fisher–Yates com o <see cref="Aleatorio"/> do evento: posição do desenho → índice do jogador.</summary>
    private static int[] Embaralhar(int n, Aleatorio aleatorio)
    {
        var quem = Enumerable.Range(0, n).ToArray();
        for (int i = n - 1; i > 0; i--)
        {
            int j = (int)(aleatorio.Proximo() * (i + 1));
            (quem[i], quem[j]) = (quem[j], quem[i]);
        }
        return quem;
    }

    // ── Os desenhos ──────────────────────────────────────────────────────────────────────
    // Cada rodada é um vetor com os jogos em sequência, 4 posições por jogo: A1 A2 B1 B2. Quem não
    // aparece folga. As rodadas "iniciais" viram o ciclo inteiro pelo Desenvolver.

    private static int[][] Desenho(int n) => n switch
    {
        4 => Desenvolver(3, ComInfinito(3), [0, 3, 1, 2]),
        8 => Desenvolver(7, ComInfinito(7), [0, 2, 1, 4, 3, 7, 5, 6]),
        12 => Desenvolver(11, ComInfinito(11), [0, 3, 1, 8, 2, 7, 4, 6, 5, 11, 9, 10]),
        16 => Desenvolver(15, ComInfinito(15), [0, 2, 1, 6, 3, 7, 10, 13, 4, 11, 8, 9, 5, 14, 15, 12]),
        5 => Desenvolver(5, Ciclico(5), [1, 4, 2, 3]),
        9 => Desenvolver(9, Z3xZ3, [1, 2, 3, 6, 4, 8, 5, 7]),
        13 => Desenvolver(13, Ciclico(13), [1, 4, 2, 7, 3, 12, 6, 8, 5, 11, 9, 10]),
        11 => Desenvolver(11, Ciclico(11), [1, 5, 2, 7, 3, 6, 9, 10]),
        15 => Desenvolver(15, Ciclico(15), [1, 4, 2, 9, 3, 12, 6, 8, 7, 11, 13, 14]),
        10 => Desenvolver(5, DoisLados(5), [1, 3, 2, 9, 4, 7, 6, 8], [0, 4, 2, 6, 1, 7, 5, 9]),
        14 => Desenvolver(7, DoisLados(7),
            [1, 3, 2, 5, 4, 10, 8, 13, 6, 9, 11, 12],
            [0, 6, 7, 10, 1, 9, 3, 8, 2, 13, 5, 12]),
        6 => Desenvolver(1, Parado,
            [0, 3, 2, 1], [3, 1, 4, 5], [5, 0, 4, 2], [2, 3, 5, 1], [1, 4, 2, 0], [4, 0, 3, 5]),
        7 => Desenvolver(1, Parado,
            [2, 4, 5, 3], [1, 2, 6, 0], [5, 1, 4, 3], [6, 4, 0, 2], [1, 3, 5, 6], [0, 4, 3, 2], [0, 5, 6, 1]),
        _ => throw new ArgumentOutOfRangeException(nameof(n), n, "Sem desenho de Americano para esse número de jogadores."),
    };

    /// <summary>
    /// O ciclo a partir das rodadas iniciais: para i = 0…ordem−1, cada rodada inicial com cada
    /// jogador p trocado por <c>mover(p, i)</c>, na ordem em que as iniciais vieram.
    /// </summary>
    private static int[][] Desenvolver(int ordem, Func<int, int, int> mover, params int[][] iniciais)
    {
        var ciclo = new int[ordem * iniciais.Length][];
        for (int i = 0; i < ordem; i++)
            for (int k = 0; k < iniciais.Length; k++)
                ciclo[i * iniciais.Length + k] = iniciais[k].Select(p => mover(p, i)).ToArray();
        return ciclo;
    }

    /// <summary>Z<sub>m</sub> ∪ {∞}, com o ∞ codificado como m.</summary>
    private static Func<int, int, int> ComInfinito(int m) => (p, i) => p == m ? m : (p + i) % m;

    private static Func<int, int, int> Ciclico(int m) => (p, i) => (p + i) % m;

    /// <summary>Z<sub>3</sub>×Z<sub>3</sub>, com (a, b) codificado como 3a + b.</summary>
    private static int Z3xZ3(int p, int i) => (p / 3 + i / 3) % 3 * 3 + (p % 3 + i % 3) % 3;

    /// <summary>Z<sub>m</sub> × {0, 1}, com (x, lado) codificado como x + m·lado: gira x, mantém o lado.</summary>
    private static Func<int, int, int> DoisLados(int m) => (p, i) => (p % m + i) % m + m * (p / m);

    private static int Parado(int p, int i) => p;

    /// <summary>
    /// As rodadas depois do ciclo quando N ≡ 2, 3 (mod 4), uma por vez, por busca EXATA sobre o
    /// desenho (antes do embaralhamento, então a garantia vale pra toda semente).
    /// </summary>
    /// <remarks>
    /// <para><b>Rodada justa</b>: f = N mod 4 folgas, ninguém que folgou na rodada anterior, e depois
    /// dela a diferença de folgas entre dois jogadores continua ≤ 1. Sempre existe, porque aqui
    /// N ≥ 2f (6 e 2, 7 e 3, … 15 e 3) e as rodadas anteriores também foram justas. Se alguém que
    /// folgou na anterior ainda está no mínimo de folgas, é porque aquela rodada deu folga a TODO o
    /// mínimo de então; o mínimo de agora tem então N − f ≥ f jogadores que não folgaram, e f deles
    /// servem. Senão, ninguém do mínimo (L jogadores) folgou na anterior: se L ≥ f, f deles servem;
    /// se L &lt; f, folgam os L e mais f − L do mínimo + 1 que não folgaram na anterior, que são
    /// N − L − f ≥ f − L.</para>
    /// <para><b>A escolha</b>, entre todas as rodadas justas (todo conjunto de folgas × todo
    /// agrupamento dos outros em jogos), é a de menor custo. Cada dupla custa
    /// <see cref="Base"/>^(parcerias anteriores − o menor número de parcerias entre todos os pares);
    /// como a base passa do número de duplas de uma rodada, UMA dupla na (c+1)ª parceria custa mais
    /// que todas as duplas da rodada na c-ésima juntas. Então a rodada escolhida nunca põe dupla na
    /// (c+1)ª parceria se havia rodada justa só com duplas de até c — com c = 0, dupla repetida só
    /// quando nenhuma rodada justa é toda de duplas inéditas. Os adversários entram com peso menor
    /// que a dupla mais barata (<see cref="PesoDasDuplas"/>), então só desempatam: cada confronto
    /// custa 2a + 1, com a = vezes que o par já se enfrentou — o quanto ele aumenta a soma dos
    /// quadrados dos confrontos.</para>
    /// <para><b>Como</b>: programação dinâmica sobre subconjuntos (máscara de bits). O melhor jogo
    /// de 4 jogadores não depende do resto, então custo(S) = mínimo, sobre os jogos J que contêm o
    /// menor jogador de S, de melhorJogo(J) + custo(S − J), com memória por S. Com 15 jogadores são
    /// ~250 mil passos por rodada, e a memória é compartilhada entre todos os conjuntos de folga.
    /// Atalho de desempenho: nada é guardado entre chamadas, então quem pede a tabela longa paga a
    /// busca inteira (e o TorneioDeRodizio remonta a tabela ao carregar). Teto medido: 15 jogadores e
    /// 64 rodadas levam ~80 ms em Release, ~195 ms em Debug; a tabela do tamanho padrão não busca
    /// nada. A saída, se pesar, é guardar as rodadas da continuação no arquivo do evento.</para>
    /// <para>Atalho: a busca é gulosa ENTRE rodadas — cada uma é a melhor dado o passado, sem olhar
    /// as seguintes. É o que a regra pede ("repetir só quando não há alternativa" rodada a rodada),
    /// mas uma escolha diferente numa rodada poderia deixar a seguinte melhor. A saída, se um dia
    /// importar, é olhar duas rodadas à frente (o custo sobe ao quadrado).</para>
    /// </remarks>
    private sealed class Continuacao
    {
        /// <summary>Maior que as 6 duplas de uma rodada (N ≤ 15 fora do múltiplo de 4).</summary>
        private const long Base = 16;

        /// <summary>
        /// Teto do expoente, só contra estouro de <see cref="long"/> (16^10 · 2^16 · 6 &lt; 2^63): com
        /// a escada, as parcerias ficam a uma ou duas do mínimo, nunca a 10.
        /// </summary>
        private const int ExpoenteMaximo = 10;

        /// <summary>
        /// Maior que o custo de adversários de uma rodada inteira (3 jogos × 4 confrontos ×
        /// (2 · 64 + 1) = 1548, com 64 = <see cref="MaximoDeRodadas"/>): adversário só desempata.
        /// </summary>
        private const long PesoDasDuplas = 1 << 16;

        private readonly int _n;
        private readonly int _folgasPorRodada;
        private readonly int[] _parceiros;      // n × n
        private readonly int[] _adversarios;    // n × n
        private readonly int[] _folgas;
        private int _folgaramNaAnterior;        // máscara

        // Custos da rodada em preparo (n × n) e as memórias da busca, por máscara de jogadores.
        private readonly long[] _dupla;
        private readonly long[] _confronto;
        private readonly long[] _custo;
        private readonly int[] _jogoEscolhido;
        private readonly long[] _custoDoJogo;
        private readonly byte[] _divisaoDoJogo;

        public Continuacao(int n, IEnumerable<int[]> jaJogadas)
        {
            _n = n;
            _folgasPorRodada = n % 4;
            _parceiros = new int[n * n];
            _adversarios = new int[n * n];
            _folgas = new int[n];
            _dupla = new long[n * n];
            _confronto = new long[n * n];
            _custo = new long[1 << n];
            _jogoEscolhido = new int[1 << n];
            _custoDoJogo = new long[1 << n];
            _divisaoDoJogo = new byte[1 << n];
            foreach (var rodada in jaJogadas) Registrar(rodada);
        }

        public int[] Proxima()
        {
            PrepararCustos();
            Array.Fill(_custo, -1);
            Array.Fill(_custoDoJogo, -1);

            int todos = (1 << _n) - 1;
            long melhor = long.MaxValue;
            int folgam = -1;
            for (int conjunto = 0; conjunto <= todos; conjunto++)
            {
                if (!FolgaJusta(conjunto)) continue;
                long custo = Melhor(todos & ~conjunto);
                if (custo < melhor)
                {
                    melhor = custo;
                    folgam = conjunto;
                }
            }
            if (folgam < 0)
                throw new UnreachableException($"Sem rodada justa para {_n} jogadores — a prova no comentário da Continuacao diz que sempre há.");

            var rodada = new List<int>(_n);
            for (int resto = todos & ~folgam; resto != 0;)
            {
                int jogo = _jogoEscolhido[resto];
                rodada.AddRange(Dividir(jogo, _divisaoDoJogo[jogo]));
                resto &= ~jogo;
            }
            int[] modelo = rodada.ToArray();
            Registrar(modelo);
            return modelo;
        }

        private bool FolgaJusta(int conjunto)
        {
            if (BitOperations.PopCount((uint)conjunto) != _folgasPorRodada || (conjunto & _folgaramNaAnterior) != 0) return false;
            int minimo = int.MaxValue, maximo = int.MinValue;
            for (int p = 0; p < _n; p++)
            {
                int depois = _folgas[p] + (conjunto >> p & 1);
                minimo = Math.Min(minimo, depois);
                maximo = Math.Max(maximo, depois);
            }
            return maximo - minimo <= 1;
        }

        private void PrepararCustos()
        {
            int menor = int.MaxValue;
            for (int i = 0; i < _n; i++)
                for (int j = i + 1; j < _n; j++)
                    menor = Math.Min(menor, _parceiros[i * _n + j]);
            for (int i = 0; i < _n; i++)
                for (int j = 0; j < _n; j++)
                {
                    int expoente = Math.Min(_parceiros[i * _n + j] - menor, ExpoenteMaximo);
                    long custo = 1;
                    for (int e = 0; e < expoente; e++) custo *= Base;
                    _dupla[i * _n + j] = custo;
                    _confronto[i * _n + j] = 2L * _adversarios[i * _n + j] + 1;
                }
        }

        /// <summary>O menor custo de agrupar os jogadores de <paramref name="jogadores"/> (máscara, múltiplo de 4) em jogos.</summary>
        private long Melhor(int jogadores)
        {
            if (jogadores == 0) return 0;
            if (_custo[jogadores] >= 0) return _custo[jogadores];

            int primeiro = BitOperations.TrailingZeroCount(jogadores);
            Span<int> outros = stackalloc int[_n];
            int k = 0;
            for (int resto = jogadores & ~(1 << primeiro); resto != 0; resto &= resto - 1)
                outros[k++] = BitOperations.TrailingZeroCount(resto);

            long melhor = long.MaxValue;
            int escolhido = 0;
            for (int a = 0; a < k; a++)
                for (int b = a + 1; b < k; b++)
                    for (int c = b + 1; c < k; c++)
                    {
                        int jogo = 1 << primeiro | 1 << outros[a] | 1 << outros[b] | 1 << outros[c];
                        long custo = CustoDoJogo(jogo) + Melhor(jogadores & ~jogo);
                        if (custo < melhor)
                        {
                            melhor = custo;
                            escolhido = jogo;
                        }
                    }
            _custo[jogadores] = melhor;
            _jogoEscolhido[jogadores] = escolhido;
            return melhor;
        }

        /// <summary>O melhor dos 3 jeitos de dividir 4 jogadores em duas duplas; guarda qual foi.</summary>
        private long CustoDoJogo(int jogo)
        {
            if (_custoDoJogo[jogo] >= 0) return _custoDoJogo[jogo];
            long melhor = long.MaxValue;
            byte escolhida = 0;
            for (byte divisao = 0; divisao < 3; divisao++)
            {
                int[] d = Dividir(jogo, divisao);
                long custo = PesoDasDuplas * (_dupla[d[0] * _n + d[1]] + _dupla[d[2] * _n + d[3]])
                    + _confronto[d[0] * _n + d[2]] + _confronto[d[0] * _n + d[3]]
                    + _confronto[d[1] * _n + d[2]] + _confronto[d[1] * _n + d[3]];
                if (custo < melhor)
                {
                    melhor = custo;
                    escolhida = divisao;
                }
            }
            _custoDoJogo[jogo] = melhor;
            _divisaoDoJogo[jogo] = escolhida;
            return melhor;
        }

        /// <summary>Os 4 jogadores p &lt; q &lt; r &lt; s como A1 A2 B1 B2: p+q × r+s, p+r × q+s ou p+s × q+r.</summary>
        private static int[] Dividir(int jogo, byte divisao)
        {
            Span<int> j = stackalloc int[4];
            int k = 0;
            for (int resto = jogo; resto != 0; resto &= resto - 1) j[k++] = BitOperations.TrailingZeroCount(resto);
            return divisao switch
            {
                0 => [j[0], j[1], j[2], j[3]],
                1 => [j[0], j[2], j[1], j[3]],
                _ => [j[0], j[3], j[1], j[2]],
            };
        }

        private void Registrar(int[] rodada)
        {
            int jogam = 0;
            for (int k = 0; k < rodada.Length; k += 4)
            {
                int a1 = rodada[k], a2 = rodada[k + 1], b1 = rodada[k + 2], b2 = rodada[k + 3];
                Somar(_parceiros, a1, a2);
                Somar(_parceiros, b1, b2);
                foreach (int a in (ReadOnlySpan<int>)[a1, a2])
                    foreach (int b in (ReadOnlySpan<int>)[b1, b2])
                        Somar(_adversarios, a, b);
                jogam |= 1 << a1 | 1 << a2 | 1 << b1 | 1 << b2;
            }
            _folgaramNaAnterior = ((1 << _n) - 1) & ~jogam;
            for (int p = 0; p < _n; p++)
                if ((_folgaramNaAnterior >> p & 1) == 1) _folgas[p]++;
        }

        private void Somar(int[] contagem, int a, int b)
        {
            contagem[a * _n + b]++;
            contagem[b * _n + a]++;
        }
    }
}
