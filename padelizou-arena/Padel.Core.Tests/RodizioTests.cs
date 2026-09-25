using System.Diagnostics;
using System.Text.Json.Nodes;

namespace Padel.Core.Tests;

// O using mora DENTRO do namespace, como no TorneioTests: um tipo de mesmo nome que outra tarefa
// ponha direto em Padel.Core não sequestra os nomes daqui.
using Padel.Core.Rodizio;

/// <summary>
/// Os formatos sociais — Americano e Mexicano — sem tela: a tabela de rodadas (duplas, adversários
/// e folgas), o jogo de pontos corridos, a classificação individual com os desempates, o Mexicano
/// montando a rodada pela tabela, e o arquivo com versão.
/// </summary>
/// <remarks>
/// As contagens daqui (quantas vezes cada par foi dupla, se enfrentou, quem folgou) são feitas
/// DE NOVO no teste, a partir dos jogos, e não pedidas à implementação — senão o teste só
/// confirmaria a conta que ela mesma fez.
/// </remarks>
public class RodizioTests
{
    // ── Ajudantes ────────────────────────────────────────────────────────────────────────

    private static readonly uint[] Sementes = [1, 7, 2026, 90210];

    private static List<JogadorDoRodizio> Jogadores(int n, bool humanos = false, int forca = 50) =>
        Enumerable.Range(1, n).Select(i => new JogadorDoRodizio($"J{i:00}", forca, humanos)).ToList();

    private static IEnumerable<(int I, int J)> Pares(int n)
    {
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                yield return (i, j);
    }

    private static void Somar(int[,] contagem, int a, int b)
    {
        contagem[a, b]++;
        contagem[b, a]++;
    }

    /// <summary>
    /// Confere a forma de cada rodada (N/4 jogos; cada jogador exatamente uma vez, em jogo ou na
    /// folga) e conta, par a par, quantas vezes foram dupla e quantas se enfrentaram.
    /// </summary>
    private static (int[,] Parceiros, int[,] Adversarios) Contar(int n, IReadOnlyList<RodadaDaTabela> rodadas)
    {
        var parceiros = new int[n, n];
        var adversarios = new int[n, n];
        for (int r = 0; r < rodadas.Count; r++)
        {
            var rodada = rodadas[r];
            Assert.Equal(n / 4, rodada.Jogos.Count);
            var presentes = new List<int>(rodada.Folgas);
            foreach (var jogo in rodada.Jogos)
            {
                presentes.AddRange([jogo.A1, jogo.A2, jogo.B1, jogo.B2]);
                Somar(parceiros, jogo.A1, jogo.A2);
                Somar(parceiros, jogo.B1, jogo.B2);
                foreach (int a in new[] { jogo.A1, jogo.A2 })
                    foreach (int b in new[] { jogo.B1, jogo.B2 })
                        Somar(adversarios, a, b);
            }
            presentes.Sort();
            Assert.True(presentes.SequenceEqual(Enumerable.Range(0, n)),
                $"N={n}, rodada {r + 1}: cada jogador aparece uma vez só (em jogo ou folgando) — veio {string.Join(",", presentes)}");
        }
        return (parceiros, adversarios);
    }

    /// <summary>O mesmo que <see cref="Contar"/>, mas pelos jogos do evento (por nome): duas vezes na mesma rodada reprova.</summary>
    private static void AssertNinguemEmDoisJogosNaMesmaRodada(TorneioDeRodizio t)
    {
        foreach (var rodada in t.Jogos.GroupBy(j => j.Rodada))
        {
            var nomes = rodada.SelectMany(j => new[] { j.DuplaA.Jogador1, j.DuplaA.Jogador2, j.DuplaB.Jogador1, j.DuplaB.Jogador2 }).ToList();
            Assert.True(nomes.Count == nomes.Distinct().Count(),
                $"Rodada {rodada.Key + 1}: alguém está em dois jogos ({string.Join(", ", nomes)})");
        }
    }

    private static JogoDoRodizio J(int numero, string a1, string a2, string b1, string b2, int pontosA, int pontosB) =>
        new(numero, numero - 1, 1, new DuplaDoRodizio(a1, a2), new DuplaDoRodizio(b1, b2), pontosA, pontosB);

    private static string[] Ordem(IEnumerable<LinhaDoRodizio> linhas) => linhas.Select(l => l.Jogador).ToArray();

    private static void AssertDuplas(JogoDoRodizio jogo, (string, string) duplaA, (string, string) duplaB)
    {
        static HashSet<string> Conjunto((string X, string Y) d) => [d.X, d.Y];
        Assert.True(Conjunto(duplaA).SetEquals([jogo.DuplaA.Jogador1, jogo.DuplaA.Jogador2]),
            $"Jogo {jogo.Numero}: a dupla A devia ser {duplaA}, é {jogo.DuplaA}");
        Assert.True(Conjunto(duplaB).SetEquals([jogo.DuplaB.Jogador1, jogo.DuplaB.Jogador2]),
            $"Jogo {jogo.Numero}: a dupla B devia ser {duplaB}, é {jogo.DuplaB}");
    }

    /// <summary>Joga até o fim informando o placar de todo jogo pendente (o do humano) — sempre o mesmo, pra comparar dois eventos.</summary>
    private static void JogarAteOFimInformando(TorneioDeRodizio t, int pontosDaDuplaA = 18)
    {
        while (!t.Encerrado)
        {
            foreach (var jogo in t.JogosPendentes)
                t.InformarResultado(jogo.Numero, pontosDaDuplaA, t.Regras.PontosPorJogo - pontosDaDuplaA);
            if (!t.Encerrado) Assert.True(t.AvancarRodada());
        }
    }

    // ── A tabela do Americano ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(4, 3)]
    [InlineData(5, 5)]
    [InlineData(6, 6)]
    [InlineData(7, 7)]
    [InlineData(8, 7)]
    [InlineData(9, 9)]
    [InlineData(10, 10)]
    [InlineData(11, 11)]
    [InlineData(12, 11)]
    [InlineData(13, 13)]
    [InlineData(14, 14)]
    [InlineData(15, 15)]
    [InlineData(16, 15)]
    public void Rodadas_padrao_sao_N_menos_1_no_multiplo_de_4_e_N_no_resto(int n, int esperado)
    {
        Assert.Equal(esperado, TabelaDoAmericano.RodadasPadrao(n));
        Assert.Equal(esperado, TabelaDoAmericano.Montar(n, 1).Count);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(16)]
    public void Americano_multiplo_de_4_em_N_menos_1_rodadas_cada_par_e_dupla_exatamente_uma_vez(int n)
    {
        foreach (uint semente in Sementes)
        {
            var tabela = TabelaDoAmericano.Montar(n, semente);
            Assert.Equal(n - 1, tabela.Count);
            Assert.All(tabela, r => Assert.Empty(r.Folgas));
            var (parceiros, _) = Contar(n, tabela);
            foreach (var (i, j) in Pares(n))
                Assert.True(parceiros[i, j] == 1, $"N={n}, semente {semente}: {i} e {j} foram dupla {parceiros[i, j]} vezes");
        }
    }

    /// <summary>
    /// Os N ≡ 1 (mod 4) também fecham perfeito: em N rodadas, com um folgando por vez, cada par é
    /// dupla uma vez e cada jogador folga uma vez (o torneio de whist de 4n+1 jogadores).
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(9)]
    [InlineData(13)]
    public void Americano_de_4n_mais_1_em_N_rodadas_cada_par_e_dupla_uma_vez_e_cada_um_folga_uma_vez(int n)
    {
        foreach (uint semente in Sementes)
        {
            var tabela = TabelaDoAmericano.Montar(n, semente);
            var (parceiros, _) = Contar(n, tabela);
            foreach (var (i, j) in Pares(n))
                Assert.True(parceiros[i, j] == 1, $"N={n}, semente {semente}: {i} e {j} foram dupla {parceiros[i, j]} vezes");
            var folgas = tabela.SelectMany(r => r.Folgas).ToList();
            Assert.Equal(Enumerable.Range(0, n), folgas.Order());
        }
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    public void No_ciclo_padrao_nenhuma_dupla_se_repete(int n)
    {
        foreach (uint semente in Sementes)
        {
            var (parceiros, _) = Contar(n, TabelaDoAmericano.Montar(n, semente));
            foreach (var (i, j) in Pares(n))
                Assert.True(parceiros[i, j] <= 1, $"N={n}, semente {semente}: {i} e {j} foram dupla {parceiros[i, j]} vezes no ciclo padrão");
        }
    }

    /// <summary>
    /// O limite de adversários que a tabela GARANTE no ciclo padrão: quantas vezes cada par de
    /// jogadores se enfrenta, no mínimo e no máximo. 4, 5, 8, 9, 12, 13 e 16 são torneios de whist:
    /// todo par se enfrenta exatamente 2 vezes (espalhamento 0). Nos outros a média fica entre 1 e 2
    /// e o melhor possível é {1, 2} (espalhamento 1) — atingido em 6, 11, 14 e 15; em 7 e 10 a
    /// tabela não passa de 2, mas alguns pares nunca se enfrentam (espalhamento 2).
    /// </summary>
    [Theory]
    [InlineData(4, 2, 2)]
    [InlineData(5, 2, 2)]
    [InlineData(6, 1, 2)]
    [InlineData(7, 0, 2)]
    [InlineData(8, 2, 2)]
    [InlineData(9, 2, 2)]
    [InlineData(10, 0, 2)]
    [InlineData(11, 1, 2)]
    [InlineData(12, 2, 2)]
    [InlineData(13, 2, 2)]
    [InlineData(14, 1, 2)]
    [InlineData(15, 1, 2)]
    [InlineData(16, 2, 2)]
    public void Adversarios_no_ciclo_padrao_ficam_dentro_do_limite_garantido(int n, int minimo, int maximo)
    {
        foreach (uint semente in Sementes)
        {
            var (_, adversarios) = Contar(n, TabelaDoAmericano.Montar(n, semente));
            foreach (var (i, j) in Pares(n))
                Assert.True(adversarios[i, j] >= minimo && adversarios[i, j] <= maximo,
                    $"N={n}, semente {semente}: {i} e {j} se enfrentaram {adversarios[i, j]} vezes (limite {minimo}–{maximo})");
        }
    }

    /// <summary>
    /// Quando N não é múltiplo de 4, em TODA rodada (não só no fim): folgas com diferença de no
    /// máximo 1 entre jogadores, jogos idem, e ninguém folga duas rodadas seguidas — inclusive na
    /// virada do ciclo, quando a tabela recomeça.
    /// </summary>
    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void Fora_do_multiplo_de_4_folgas_e_jogos_ficam_equilibrados_em_toda_rodada(int n)
    {
        int ciclo = TabelaDoAmericano.RodadasPadrao(n);
        foreach (uint semente in Sementes)
            foreach (int rodadas in new[] { 1, 3, ciclo - 1, ciclo, ciclo + 2, 2 * ciclo, TabelaDoAmericano.MaximoDeRodadas })
            {
                var tabela = TabelaDoAmericano.Montar(n, semente, rodadas);
                Assert.Equal(rodadas, tabela.Count);
                Contar(n, tabela);
                var folgas = new int[n];
                var jogos = new int[n];
                IReadOnlyList<int> anterior = [];
                for (int r = 0; r < tabela.Count; r++)
                {
                    Assert.Equal(n % 4, tabela[r].Folgas.Count);
                    Assert.True(!tabela[r].Folgas.Intersect(anterior).Any(),
                        $"N={n}, semente {semente}: {string.Join(",", tabela[r].Folgas.Intersect(anterior))} folgou nas rodadas {r} e {r + 1} seguidas");
                    anterior = tabela[r].Folgas;
                    foreach (int f in tabela[r].Folgas) folgas[f]++;
                    foreach (var jogo in tabela[r].Jogos)
                        foreach (int p in new[] { jogo.A1, jogo.A2, jogo.B1, jogo.B2 }) jogos[p]++;
                    Assert.True(folgas.Max() - folgas.Min() <= 1, $"N={n}, semente {semente}, rodada {r + 1}: folgas {string.Join(",", folgas)}");
                    Assert.True(jogos.Max() - jogos.Min() <= 1, $"N={n}, semente {semente}, rodada {r + 1}: jogos {string.Join(",", jogos)}");
                }
                if (rodadas % ciclo == 0)
                {
                    // Ciclo inteiro: todo mundo folgou e jogou exatamente o mesmo tanto.
                    Assert.All(folgas, f => Assert.Equal(rodadas / ciclo * (n % 4), f));
                    Assert.All(jogos, j => Assert.Equal(rodadas / ciclo * (n - n % 4), j));
                }
            }
    }

    /// <summary>
    /// Com N ≡ 0 ou 1 (mod 4) o ciclo já usa todo par como dupla, então a repetição é inevitável:
    /// a tabela recomeça, e cada par repete uma vez antes de qualquer um repetir duas.
    /// </summary>
    [Fact]
    public void Passando_do_ciclo_com_N_multiplo_de_4_ou_4n_mais_1_a_tabela_recomeca_e_cada_par_repete_uma_vez()
    {
        foreach (int n in new[] { 4, 5, 8, 9, 12, 13, 16 })
        {
            int ciclo = TabelaDoAmericano.RodadasPadrao(n);
            var tabela = TabelaDoAmericano.Montar(n, 31, 2 * ciclo);
            var (primeiro, _) = Contar(n, tabela.Take(ciclo).ToList());
            var (tudo, _) = Contar(n, tabela);
            foreach (var (i, j) in Pares(n))
            {
                Assert.Equal(1, primeiro[i, j]);
                Assert.Equal(2, tudo[i, j]);
            }
        }
    }

    private static IEnumerable<(int X, int Y)> DuplasDa(RodadaDaTabela rodada) =>
        rodada.Jogos.SelectMany(j => new[] { (j.A1, j.A2), (j.B1, j.B2) });

    private static (string, string) Chave(DuplaDoRodizio d) =>
        string.CompareOrdinal(d.Jogador1, d.Jogador2) < 0 ? (d.Jogador1, d.Jogador2) : (d.Jogador2, d.Jogador1);

    /// <summary>
    /// Achado da revisão: com N ≡ 2, 3 (mod 4) sobram duplas inéditas no fim do ciclo, e a tabela
    /// recomeçava do começo repetindo dupla na rodada N+1 (2 duplas com 6 jogadores, 4 com 10 e 11,
    /// 6 com 14) — quando dava pra montar a rodada só com inéditas e folgas justas (com 6: folgam 0
    /// e 3, duplas 1+2 e 4+5). Com 7 e 15 nenhuma rodada justa é toda inédita (o teste de baixo
    /// prova por força bruta), e a tabela repete uma dupla só.
    /// </summary>
    [Theory]
    [InlineData(6, 0)]
    [InlineData(10, 0)]
    [InlineData(11, 0)]
    [InlineData(14, 0)]
    [InlineData(7, 1)]
    [InlineData(15, 1)]
    public void Passando_do_ciclo_a_rodada_seguinte_usa_as_duplas_ineditas_que_sobraram(int n, int repeticoes)
    {
        int ciclo = TabelaDoAmericano.RodadasPadrao(n);
        foreach (uint semente in new uint[] { 12345, 31 })
        {
            var tabela = TabelaDoAmericano.Montar(n, semente, ciclo + 1);
            var (doCiclo, _) = Contar(n, tabela.Take(ciclo).ToList());
            var repetidas = DuplasDa(tabela[ciclo]).Where(d => doCiclo[d.X, d.Y] > 0).ToList();
            Assert.True(repetidas.Count == repeticoes,
                $"N={n}, semente {semente}: a rodada {ciclo + 1} repete {repetidas.Count} dupla(s) do ciclo (esperado {repeticoes}): {string.Join(", ", repetidas)}");
        }

        // O mesmo pelo evento — o caminho de RegrasDoRodizio(Rodadas: …) —, contando por nome.
        var t = new TorneioDeRodizio(Jogadores(n), new RegrasDoRodizio(Rodadas: ciclo + 1), semente: 12345);
        t.JogarAteOFim();
        var duplasDoCiclo = t.Jogos.Where(j => j.Rodada < ciclo).SelectMany(j => new[] { j.DuplaA, j.DuplaB }).Select(Chave).ToHashSet();
        var ultima = t.Jogos.Where(j => j.Rodada == ciclo).SelectMany(j => new[] { j.DuplaA, j.DuplaB }).Select(Chave).ToList();
        Assert.Equal(n / 4 * 2, ultima.Count);
        Assert.Equal(repeticoes, ultima.Count(duplasDoCiclo.Contains));
    }

    /// <summary>
    /// A regra 2(c) cobrada rodada a rodada até o teto de rodadas: se a rodada tem uma dupla que já
    /// jogou junta c vezes, NÃO havia rodada justa — folgas com diferença ≤ 1 depois dela e ninguém
    /// folgando duas seguidas — em que todas as duplas tivessem jogado juntas menos de c vezes. Com
    /// c = 1 é "dupla repetida só quando não há alternativa"; com c = 2, ninguém chega à 3ª parceria
    /// enquanto dava pra ficar na 2ª. A busca é força bruta do próprio teste (todo conjunto de folgas
    /// justo × emparelhamento perfeito dos que jogam), não a da tabela.
    /// </summary>
    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(14)]
    [InlineData(15)]
    public void Passando_do_ciclo_dupla_so_repete_quando_nenhuma_rodada_justa_evita(int n)
    {
        foreach (uint semente in new uint[] { 12345, 7 })
        {
            var tabela = TabelaDoAmericano.Montar(n, semente, TabelaDoAmericano.MaximoDeRodadas);
            Contar(n, tabela);
            var parceiros = new int[n, n];
            var folgas = new int[n];
            IReadOnlyList<int> anterior = [];
            for (int r = 0; r < tabela.Count; r++)
            {
                int pior = DuplasDa(tabela[r]).Max(d => parceiros[d.X, d.Y]);
                if (pior > 0 && RodadaJustaComDuplasAbaixoDe(n, pior, parceiros, folgas, anterior) is { } alternativa)
                    Assert.Fail($"N={n}, semente {semente}, rodada {r + 1}: tem dupla na {pior + 1}ª parceria, "
                        + $"mas havia rodada justa com todas abaixo disso ({alternativa}).");
                foreach (var (x, y) in DuplasDa(tabela[r])) Somar(parceiros, x, y);
                foreach (int p in tabela[r].Folgas) folgas[p]++;
                anterior = tabela[r].Folgas;
            }
        }
    }

    /// <summary>
    /// O que dois ciclos dão com N ≡ 2, 3 (mod 4), medido em 25/09/2026 e travado aqui: todo par é
    /// dupla 1 ou 2 vezes (o recomeço antigo deixava pares que nunca jogavam juntos), e o espalhamento
    /// de adversários fica em 2 (1 com 15). Adversário só desempata na continuação, então este é o
    /// limite medido, não um ótimo provado.
    /// </summary>
    [Theory]
    [InlineData(6, 2, 4)]
    [InlineData(7, 2, 4)]
    [InlineData(10, 3, 5)]
    [InlineData(11, 2, 4)]
    [InlineData(14, 3, 5)]
    [InlineData(15, 3, 4)]
    public void Em_dois_ciclos_todo_par_e_dupla_1_ou_2_vezes_e_adversarios_ficam_no_limite_medido(int n, int minimo, int maximo)
    {
        int ciclo = TabelaDoAmericano.RodadasPadrao(n);
        foreach (uint semente in Sementes)
        {
            var (parceiros, adversarios) = Contar(n, TabelaDoAmericano.Montar(n, semente, 2 * ciclo));
            foreach (var (i, j) in Pares(n))
            {
                Assert.True(parceiros[i, j] is 1 or 2, $"N={n}, semente {semente}: {i} e {j} foram dupla {parceiros[i, j]} vezes em dois ciclos");
                Assert.True(adversarios[i, j] >= minimo && adversarios[i, j] <= maximo,
                    $"N={n}, semente {semente}: {i} e {j} se enfrentaram {adversarios[i, j]} vezes em dois ciclos (limite {minimo}–{maximo})");
            }
        }
    }

    /// <summary>
    /// A continuação é a única parte da tabela que busca. O pior caso (15 jogadores, 64 rodadas:
    /// 49 rodadas de busca) mediu ~195 ms em Debug e ~80 ms em Release; o limite tem folga de 5× e
    /// existe pra pegar a busca que perdeu a memória e virou exponencial, não pra medir milissegundo.
    /// </summary>
    [Fact]
    public void A_continuacao_mais_longa_monta_em_menos_de_1_segundo()
    {
        foreach (int n in new[] { 14, 15 })
        {
            var relogio = Stopwatch.StartNew();
            var tabela = TabelaDoAmericano.Montar(n, 99, TabelaDoAmericano.MaximoDeRodadas);
            relogio.Stop();
            Assert.Equal(TabelaDoAmericano.MaximoDeRodadas, tabela.Count);
            Assert.True(relogio.ElapsedMilliseconds < 1000, $"{TabelaDoAmericano.MaximoDeRodadas} rodadas de {n} levaram {relogio.ElapsedMilliseconds} ms");
        }
    }

    /// <summary>
    /// Uma rodada justa em que toda dupla jogou junta menos de <paramref name="limite"/> vezes, ou
    /// <c>null</c> se não há: tenta todo conjunto de N mod 4 folgas que não repete folga da rodada
    /// anterior e deixa as folgas com diferença ≤ 1, e procura emparelhamento perfeito do resto só
    /// com pares abaixo do limite.
    /// </summary>
    private static string? RodadaJustaComDuplasAbaixoDe(int n, int limite, int[,] parceiros, int[] folgas, IReadOnlyList<int> anterior)
    {
        int f = n % 4;
        var memo = new Dictionary<int, bool>();
        bool Emparelha(int resto)
        {
            if (resto == 0) return true;
            if (memo.TryGetValue(resto, out bool sabido)) return sabido;
            int i = System.Numerics.BitOperations.TrailingZeroCount(resto);
            bool da = false;
            for (int j = i + 1; j < n && !da; j++)
                if ((resto >> j & 1) == 1 && parceiros[i, j] < limite)
                    da = Emparelha(resto & ~(1 << i) & ~(1 << j));
            memo[resto] = da;
            return da;
        }

        int todos = (1 << n) - 1;
        for (int conjunto = 0; conjunto <= todos; conjunto++)
        {
            if (System.Numerics.BitOperations.PopCount((uint)conjunto) != f) continue;
            var folgam = Enumerable.Range(0, n).Where(p => (conjunto >> p & 1) == 1).ToList();
            if (folgam.Intersect(anterior).Any()) continue;
            var depois = folgas.Select((c, p) => folgam.Contains(p) ? c + 1 : c).ToList();
            if (depois.Max() - depois.Min() > 1) continue;
            if (Emparelha(todos & ~conjunto)) return $"folgando {string.Join(" e ", folgam)}";
        }
        return null;
    }

    [Fact]
    public void A_tabela_e_deterministica_pela_semente()
    {
        static string Texto(IReadOnlyList<RodadaDaTabela> t) =>
            string.Join(" | ", t.Select(r => string.Join(" ", r.Jogos.Select(j => $"{j.A1}{j.A2}x{j.B1}{j.B2}")) + " f" + string.Join(",", r.Folgas)));

        foreach (int n in new[] { 6, 8, 11, 16 })
        {
            Assert.Equal(Texto(TabelaDoAmericano.Montar(n, 42)), Texto(TabelaDoAmericano.Montar(n, 42)));
            Assert.NotEqual(Texto(TabelaDoAmericano.Montar(n, 42)), Texto(TabelaDoAmericano.Montar(n, 43)));
            // Passando do ciclo também: a continuação é busca, sem acaso.
            Assert.Equal(Texto(TabelaDoAmericano.Montar(n, 42, 40)), Texto(TabelaDoAmericano.Montar(n, 42, 40)));
        }
    }

    [Fact]
    public void Montar_a_tabela_de_16_jogadores_leva_menos_de_100_ms()
    {
        var relogio = Stopwatch.StartNew();
        var tabela = TabelaDoAmericano.Montar(16, 123_456);
        relogio.Stop();
        Assert.Equal(15, tabela.Count);
        Assert.True(relogio.ElapsedMilliseconds < 100, $"A tabela de 16 levou {relogio.ElapsedMilliseconds} ms");

        relogio.Restart();
        var t = new TorneioDeRodizio(Jogadores(16), semente: 654_321);
        relogio.Stop();
        Assert.Equal(60, t.Jogos.Count);
        Assert.True(relogio.ElapsedMilliseconds < 100, $"O Americano de 16 levou {relogio.ElapsedMilliseconds} ms pra montar");
    }

    [Fact]
    public void Tabela_fora_da_faixa_e_recusada()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TabelaDoAmericano.Montar(3, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => TabelaDoAmericano.Montar(17, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => TabelaDoAmericano.Montar(8, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TabelaDoAmericano.Montar(8, 1, TabelaDoAmericano.MaximoDeRodadas + 1));
    }

    // ── O evento: Americano ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(16)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(13)]
    public void No_evento_ninguem_joga_dois_jogos_na_mesma_rodada_e_toda_dupla_e_inedita(int n)
    {
        var t = new TorneioDeRodizio(Jogadores(n), semente: 8);
        t.JogarAteOFim();
        Assert.True(t.Encerrado);
        AssertNinguemEmDoisJogosNaMesmaRodada(t);
        Assert.Equal(TabelaDoAmericano.RodadasPadrao(n) * (n / 4), t.Jogos.Count);
        var duplas = t.Jogos.SelectMany(j => new[] { j.DuplaA, j.DuplaB })
            .Select(d => string.CompareOrdinal(d.Jogador1, d.Jogador2) < 0 ? (d.Jogador1, d.Jogador2) : (d.Jogador2, d.Jogador1))
            .ToList();
        Assert.Equal(duplas.Count, duplas.Distinct().Count());
        foreach (var rodada in t.Jogos.GroupBy(j => j.Rodada))
            Assert.Equal(Enumerable.Range(1, n / 4), rodada.Select(j => j.Quadra).Order());
    }

    [Fact]
    public void Cada_jogador_soma_os_pontos_que_a_sua_dupla_fez()
    {
        var t = new TorneioDeRodizio(Jogadores(4, humanos: true), new RegrasDoRodizio(PontosPorJogo: 24), semente: 3);
        var pro = new Dictionary<string, int>();
        var contra = new Dictionary<string, int>();
        var vitorias = new Dictionary<string, int>();
        var empates = new Dictionary<string, int>();
        void Anotar(string nome, int feitos, int sofridos)
        {
            pro[nome] = pro.GetValueOrDefault(nome) + feitos;
            contra[nome] = contra.GetValueOrDefault(nome) + sofridos;
            if (feitos > sofridos) vitorias[nome] = vitorias.GetValueOrDefault(nome) + 1;
            if (feitos == sofridos) empates[nome] = empates.GetValueOrDefault(nome) + 1;
        }

        int[] daDuplaA = [15, 12, 7];
        for (int r = 0; r < 3; r++)
        {
            var jogo = Assert.Single(t.JogosPendentes);
            int a = daDuplaA[r], b = 24 - a;
            t.InformarResultado(jogo.Numero, a, b);
            Anotar(jogo.DuplaA.Jogador1, a, b);
            Anotar(jogo.DuplaA.Jogador2, a, b);
            Anotar(jogo.DuplaB.Jogador1, b, a);
            Anotar(jogo.DuplaB.Jogador2, b, a);
            if (r < 2) Assert.True(t.AvancarRodada());
        }

        Assert.True(t.Encerrado);
        Assert.False(t.AvancarRodada());
        var tabela = t.Classificacao();
        Assert.Equal(4, tabela.Count);
        Assert.Equal([1, 2, 3, 4], tabela.Select(l => l.Posicao));
        foreach (var linha in tabela)
        {
            Assert.Equal(pro[linha.Jogador], linha.PontosPro);
            Assert.Equal(pro[linha.Jogador], linha.Pontos);
            Assert.Equal(contra[linha.Jogador], linha.PontosContra);
            Assert.Equal(pro[linha.Jogador] - contra[linha.Jogador], linha.Saldo);
            Assert.Equal(3, linha.Jogos);
            Assert.Equal(vitorias.GetValueOrDefault(linha.Jogador), linha.Vitorias);
            Assert.Equal(empates.GetValueOrDefault(linha.Jogador), linha.Empates);
            Assert.Equal(3 - linha.Vitorias - linha.Empates, linha.Derrotas);
            Assert.Equal(0, linha.Folgas);
        }
        Assert.Equal(3 * 2 * 24, tabela.Sum(l => l.Pontos));
        Assert.True(tabela.Zip(tabela.Skip(1)).All(p => p.First.Pontos >= p.Second.Pontos));
        Assert.Equal(tabela[0].Jogador, t.Campeao?.Nome);
    }

    [Fact]
    public void Placar_que_nao_fecha_o_total_de_pontos_e_recusado_com_o_motivo()
    {
        var t = new TorneioDeRodizio(Jogadores(4, humanos: true), new RegrasDoRodizio(PontosPorJogo: 24), semente: 1);
        var jogo = Assert.Single(t.JogosPendentes);

        var erro = Assert.Throws<ArgumentException>(() => t.InformarResultado(jogo.Numero, 13, 10));
        Assert.Contains("24", erro.Message);
        Assert.Contains("23", erro.Message);
        Assert.Throws<ArgumentException>(() => t.InformarResultado(jogo.Numero, 25, -1));
        Assert.Throws<ArgumentException>(() => t.InformarResultado(999, 12, 12));
        Assert.Null(t.Jogos.Single(j => j.Numero == jogo.Numero).PontosA);

        var outraRodada = t.Jogos.First(j => j.Rodada == 1);
        Assert.Throws<InvalidOperationException>(() => t.InformarResultado(outraRodada.Numero, 12, 12));

        t.InformarResultado(jogo.Numero, 12, 12);   // empate vale: 24 pode terminar 12-12
        Assert.Throws<InvalidOperationException>(() => t.InformarResultado(jogo.Numero, 12, 12));
    }

    [Fact]
    public void O_placar_do_humano_sem_numero_vai_pro_unico_jogo_pendente()
    {
        var jogadores = Jogadores(8);
        jogadores[0] = new JogadorDoRodizio("Eu", 60, humano: true);
        var t = new TorneioDeRodizio(jogadores, semente: 11);

        var pendente = Assert.Single(t.JogosPendentes);
        Assert.True(pendente.Envolve("Eu"));
        Assert.False(t.AvancarRodada());
        Assert.All(t.JogosDaRodadaAtual.Where(j => !j.Envolve("Eu")), j => Assert.True(j.Jogado));

        t.InformarResultado(14, 10);
        var informado = t.Jogos.Single(j => j.Numero == pendente.Numero);
        Assert.Equal(14, informado.PontosA);
        Assert.Equal(10, informado.PontosB);
        Assert.Empty(t.JogosPendentes);
        Assert.Throws<InvalidOperationException>(() => t.InformarResultado(12, 12));

        // Dois jogos pendentes: sem o número, não dá pra saber qual — recusa em vez de chutar.
        var todosHumanos = new TorneioDeRodizio(Jogadores(8, humanos: true), semente: 11);
        Assert.Equal(2, todosHumanos.JogosPendentes.Count);
        Assert.Throws<InvalidOperationException>(() => todosHumanos.InformarResultado(12, 12));
    }

    [Fact]
    public void Jogo_entre_IAs_e_simulado_ponto_a_ponto_e_fecha_o_total()
    {
        foreach (int pontos in new[] { 16, 21, 24, 32 })
        {
            var t = new TorneioDeRodizio(Jogadores(12), new RegrasDoRodizio(PontosPorJogo: pontos), semente: 5);
            t.JogarAteOFim();
            Assert.All(t.Jogos, j => Assert.Equal(pontos, j.PontosA + j.PontosB));
            Assert.True(t.Jogos.Select(j => j.PontosA).Distinct().Count() > 3, $"com {pontos} pontos os placares não variam");
        }

        // A força decide a chance do ponto pela MÉDIA da dupla: iguais dividem, +30 leva mais.
        int somaIguais = 0, somaMaisForte = 0;
        for (uint s = 1; s <= 2000; s++)
        {
            var (a, b) = JogoDePontosCorridos.Simular(50, 50, 24, new Aleatorio(s));
            Assert.Equal(24, a + b);
            somaIguais += a;
            var (f, _) = JogoDePontosCorridos.Simular(80, 50, 24, new Aleatorio(s + 10_000));
            somaMaisForte += f;
        }
        Assert.InRange(somaIguais / 2000.0, 11.5, 12.5);
        Assert.True(somaMaisForte / 2000.0 > 13.0, $"a dupla 30 pontos mais forte fez {somaMaisForte / 2000.0:F2} de 24 em média");
        Assert.Equal(JogoDePontosCorridos.Simular(70, 40, 32, new Aleatorio(9)), JogoDePontosCorridos.Simular(70, 40, 32, new Aleatorio(9)));
    }

    [Fact]
    public void O_jogador_mais_forte_termina_acima_em_media_em_200_Americanos()
    {
        // Um claramente mais forte (90) e o resto de 70 a 40. A diferença no PONTO é pequena de
        // propósito (SimuladorDePartida.EscalaDaForca: +20 na média da dupla ≈ 55 %) e o parceiro
        // muda a cada rodada — é a soma de 7 jogos que separa.
        int[] forcas = [90, 70, 65, 60, 55, 50, 45, 40];
        var jogadores = forcas.Select(f => new JogadorDoRodizio($"F{f}", f)).ToList();
        var somaDaPosicao = forcas.ToDictionary(f => $"F{f}", _ => 0);
        for (uint s = 1; s <= 200; s++)
        {
            var t = new TorneioDeRodizio(jogadores, semente: s);
            t.JogarAteOFim();
            foreach (var linha in t.Classificacao()) somaDaPosicao[linha.Jogador] += linha.Posicao;
        }
        var media = somaDaPosicao.ToDictionary(kv => kv.Key, kv => kv.Value / 200.0);
        string resumo = string.Join(", ", media.Select(kv => $"{kv.Key}: {kv.Value:F2}"));
        Assert.True(media.Where(kv => kv.Key != "F90").All(kv => media["F90"] < kv.Value), resumo);
        Assert.True(media["F90"] < 3.5, resumo);   // bem acima do meio da tabela (4,5)
        Assert.True(media["F90"] < media["F40"] - 2, resumo);
    }

    [Fact]
    public void O_evento_e_deterministico_pela_semente()
    {
        foreach (var formato in new[] { FormatoDoRodizio.Americano, FormatoDoRodizio.Mexicano })
        {
            var regras = new RegrasDoRodizio(formato);
            var a = new TorneioDeRodizio(Jogadores(10), regras, semente: 17);
            var b = new TorneioDeRodizio(Jogadores(10), regras, semente: 17);
            var c = new TorneioDeRodizio(Jogadores(10), regras, semente: 18);
            a.JogarAteOFim();
            b.JogarAteOFim();
            c.JogarAteOFim();
            Assert.Equal(a.Salvar(), b.Salvar());
            Assert.NotEqual(a.Salvar(), c.Salvar());
        }
    }

    [Fact]
    public void Evento_com_jogadores_invalidos_e_recusado()
    {
        Assert.Throws<ArgumentException>(() => new TorneioDeRodizio(Jogadores(3)));
        Assert.Throws<ArgumentException>(() => new TorneioDeRodizio(Jogadores(17)));
        var repetido = Jogadores(4);
        repetido[3] = new JogadorDoRodizio("J01", 40);
        Assert.Throws<ArgumentException>(() => new TorneioDeRodizio(repetido));
        Assert.Throws<ArgumentOutOfRangeException>(() => new JogadorDoRodizio("X", 101));
        Assert.Throws<ArgumentException>(() => new JogadorDoRodizio(" ", 50));
        Assert.Throws<ArgumentException>(() => new TorneioDeRodizio(Jogadores(8), new RegrasDoRodizio(PontosPorJogo: 2)));
        Assert.Throws<ArgumentException>(() => new TorneioDeRodizio(Jogadores(8), new RegrasDoRodizio(Rodadas: 0)));
    }

    // ── Mexicano ─────────────────────────────────────────────────────────────────────────

    /// <summary>P1 é o mais forte, P8 o mais fraco; a lista chega embaralhada de propósito.</summary>
    private static List<JogadorDoRodizio> OitoPorForca() =>
        new[] { 5, 2, 8, 1, 7, 3, 6, 4 }.Select(i => new JogadorDoRodizio($"P{i}", 90 - 10 * i, humano: true)).ToList();

    [Fact]
    public void Mexicano_primeira_rodada_por_forca_junta_1_e_4_contra_2_e_3()
    {
        var t = new TorneioDeRodizio(OitoPorForca(),
            new RegrasDoRodizio(FormatoDoRodizio.Mexicano, PrimeiraRodada: PrimeiraRodadaDoMexicano.PorForca), semente: 3);
        var rodada = t.JogosDaRodadaAtual.OrderBy(j => j.Quadra).ToList();
        Assert.Equal(2, rodada.Count);
        AssertDuplas(rodada[0], ("P1", "P4"), ("P2", "P3"));
        AssertDuplas(rodada[1], ("P5", "P8"), ("P6", "P7"));
        // O Mexicano monta uma rodada por vez: a próxima não existe antes de esta acabar.
        Assert.All(t.Jogos, j => Assert.Equal(0, j.Rodada));
    }

    [Fact]
    public void Mexicano_da_segunda_rodada_em_diante_monta_pela_classificacao_1_e_4_contra_2_e_3()
    {
        var t = new TorneioDeRodizio(OitoPorForca(),
            new RegrasDoRodizio(FormatoDoRodizio.Mexicano, PrimeiraRodada: PrimeiraRodadaDoMexicano.PorForca), semente: 3);
        var rodada = t.JogosDaRodadaAtual.OrderBy(j => j.Quadra).ToList();
        // Quadra 1: P1+P4 x P2+P3 (15 a 9); quadra 2: P5+P8 x P6+P7 (20 a 4).
        t.InformarResultado(rodada[0].Numero, rodada[0].DuplaA.Tem("P1") ? 15 : 9, rodada[0].DuplaA.Tem("P1") ? 9 : 15);
        t.InformarResultado(rodada[1].Numero, rodada[1].DuplaA.Tem("P5") ? 20 : 4, rodada[1].DuplaA.Tem("P5") ? 4 : 20);
        Assert.True(t.AvancarRodada());

        // A tabela: P5 e P8 com 20 (empate total, nunca se enfrentaram → nome), P1 e P4 com 15,
        // P2 e P3 com 9, P6 e P7 com 4.
        Assert.Equal(["P5", "P8", "P1", "P4", "P2", "P3", "P6", "P7"], Ordem(t.Classificacao()));
        var segunda = t.JogosDaRodadaAtual.OrderBy(j => j.Quadra).ToList();
        Assert.Equal(2, segunda.Count);
        AssertDuplas(segunda[0], ("P5", "P4"), ("P8", "P1"));
        AssertDuplas(segunda[1], ("P2", "P7"), ("P3", "P6"));
    }

    [Fact]
    public void Mexicano_primeira_rodada_sorteada_depende_da_semente()
    {
        var regras = new RegrasDoRodizio(FormatoDoRodizio.Mexicano);
        static string Rodada(TorneioDeRodizio t) => string.Join(" | ", t.JogosDaRodadaAtual.Select(j => $"{j.DuplaA} x {j.DuplaB}"));
        var textos = Enumerable.Range(1, 6).Select(s => Rodada(new TorneioDeRodizio(Jogadores(12), regras, (uint)s))).ToList();
        Assert.Equal(textos[0], Rodada(new TorneioDeRodizio(Jogadores(12), regras, 1)));
        Assert.True(textos.Distinct().Count() > 1);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(10)]
    [InlineData(15)]
    public void Mexicano_com_folga_roda_com_a_mesma_justica_do_Americano(int n)
    {
        var t = new TorneioDeRodizio(Jogadores(n), new RegrasDoRodizio(FormatoDoRodizio.Mexicano), semente: 4);
        t.JogarAteOFim();
        AssertNinguemEmDoisJogosNaMesmaRodada(t);
        var nomes = t.Jogadores.Select(j => j.Nome).ToList();
        var folgas = nomes.ToDictionary(x => x, _ => 0);
        HashSet<string> anterior = [];
        for (int r = 0; r < t.TotalDeRodadas; r++)
        {
            var jogam = t.Jogos.Where(j => j.Rodada == r).SelectMany(j => new[] { j.DuplaA.Jogador1, j.DuplaA.Jogador2, j.DuplaB.Jogador1, j.DuplaB.Jogador2 }).ToHashSet();
            var folgaram = nomes.Where(x => !jogam.Contains(x)).ToHashSet();
            Assert.Equal(n % 4, folgaram.Count);
            Assert.Empty(folgaram.Intersect(anterior));
            anterior = folgaram;
            foreach (var x in folgaram) folgas[x]++;
            Assert.True(folgas.Values.Max() - folgas.Values.Min() <= 1, $"rodada {r + 1}: folgas {string.Join(",", folgas.Values)}");
        }
        Assert.All(t.Classificacao(), l => Assert.Equal(folgas[l.Jogador], l.Folgas));
    }

    // ── Folga e classificação ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(25, 2, 24, 13)]   // 12,5 → 13: arredonda a metade pra cima
    [InlineData(23, 2, 24, 12)]   // 11,5 → 12
    [InlineData(34, 3, 24, 11)]   // 11,33 → 11
    [InlineData(0, 0, 24, 12)]    // sem jogo ainda: metade do jogo
    [InlineData(0, 0, 21, 11)]    // 10,5 → 11
    [InlineData(64, 2, 32, 32)]
    public void A_folga_vale_a_media_arredondada_dos_proprios_pontos_por_jogo(int pontos, int jogos, int pontosPorJogo, int esperado)
    {
        Assert.Equal(esperado, ClassificacaoDoRodizio.PontosDaFolga(pontos, jogos, pontosPorJogo));
    }

    [Fact]
    public void Quem_folga_recebe_a_media_na_classificacao_sem_mexer_no_saldo()
    {
        var jogos = new List<JogoDoRodizio>
        {
            J(1, "Ana", "Bia", "Caio", "Davi", 13, 11),
            J(2, "Ana", "Caio", "Bia", "Davi", 12, 12),
        };
        var folgas = new Dictionary<string, int> { ["Ana"] = 1, ["Eva"] = 1 };
        var linhas = ClassificacaoDoRodizio.Ordenar(["Ana", "Bia", "Caio", "Davi", "Eva"], jogos, 24, folgas);

        var ana = linhas.Single(l => l.Jogador == "Ana");
        Assert.Equal(25, ana.PontosPro);
        Assert.Equal(13, ana.PontosDeFolga);
        Assert.Equal(38, ana.Pontos);
        Assert.Equal(2, ana.Saldo);
        Assert.Equal(1, ana.Folgas);

        var eva = linhas.Single(l => l.Jogador == "Eva");
        Assert.Equal(0, eva.Jogos);
        Assert.Equal(12, eva.PontosDeFolga);
        Assert.Equal(12, eva.Pontos);
        Assert.Equal(0, eva.Saldo);
    }

    [Fact]
    public void No_evento_a_folga_so_conta_quando_a_rodada_fecha()
    {
        var t = new TorneioDeRodizio(Jogadores(5, humanos: true), semente: 2);
        var folgou = t.Jogadores.Select(j => j.Nome).Single(nome => !t.JogosDaRodadaAtual.Any(j => j.Envolve(nome)));
        Assert.Equal(0, t.Classificacao().Single(l => l.Jogador == folgou).Folgas);

        var jogo = Assert.Single(t.JogosPendentes);
        t.InformarResultado(jogo.Numero, 13, 11);
        var linha = t.Classificacao().Single(l => l.Jogador == folgou);
        Assert.Equal(1, linha.Folgas);
        Assert.Equal(12, linha.PontosDeFolga);   // ainda sem jogo: metade dos 24
    }

    [Fact]
    public void Desempate_1_pontos_totais_vencem_mais_vitorias()
    {
        var jogos = new List<JogoDoRodizio>
        {
            J(1, "Zeca", "X1", "X2", "X3", 20, 4),
            J(2, "Zeca", "X4", "X5", "X6", 10, 14),
            J(3, "Ana", "X1", "X2", "X3", 13, 11),
            J(4, "Ana", "X4", "X5", "X6", 13, 11),
        };
        Assert.Equal(["Zeca", "Ana"], Ordem(ClassificacaoDoRodizio.Ordenar(["Ana", "Zeca"], jogos, 24)));
    }

    [Fact]
    public void Desempate_2_com_pontos_iguais_vence_quem_ganhou_mais_jogos()
    {
        var jogos = new List<JogoDoRodizio>
        {
            J(1, "Zeca", "X1", "X2", "X3", 13, 11),
            J(2, "Zeca", "X4", "X5", "X6", 13, 11),
            J(3, "Zeca", "X7", "X8", "X9", 4, 20),
            J(4, "Ana", "X1", "X2", "X3", 20, 4),
            J(5, "Ana", "X4", "X5", "X6", 5, 19),
            J(6, "Ana", "X7", "X8", "X9", 5, 19),
        };
        var linhas = ClassificacaoDoRodizio.Ordenar(["Ana", "Zeca"], jogos, 24);
        Assert.Equal(linhas[0].Pontos, linhas[1].Pontos);
        Assert.Equal(linhas[0].Saldo, linhas[1].Saldo);
        Assert.Equal(["Zeca", "Ana"], Ordem(linhas));
    }

    [Fact]
    public void Desempate_3_com_pontos_e_vitorias_iguais_vence_o_melhor_saldo()
    {
        // Zeca jogou 3; Ana jogou 2 e folgou 1 (média 12,5 → 13 de folga). Os dois somam 38, uma
        // vitória cada; o saldo (só dos jogos) é +4 de Zeca e +2 de Ana.
        var jogos = new List<JogoDoRodizio>
        {
            J(1, "Zeca", "X1", "X2", "X3", 20, 4),
            J(2, "Zeca", "X4", "X5", "X6", 9, 15),
            J(3, "Zeca", "X7", "X8", "X9", 9, 15),
            J(4, "Ana", "X1", "X2", "X3", 20, 4),
            J(5, "Ana", "X4", "X5", "X6", 5, 19),
        };
        var linhas = ClassificacaoDoRodizio.Ordenar(["Ana", "Zeca"], jogos, 24, new Dictionary<string, int> { ["Ana"] = 1 });
        Assert.All(linhas, l => Assert.Equal(38, l.Pontos));
        Assert.All(linhas, l => Assert.Equal(1, l.Vitorias));
        Assert.Equal(["Zeca", "Ana"], Ordem(linhas));
        Assert.Equal([4, 2], linhas.Select(l => l.Saldo));
    }

    [Fact]
    public void Desempate_4_com_tudo_igual_vence_o_confronto_direto()
    {
        // Zeca e Ana: 24 pontos, 1 vitória, saldo 0. No único jogo em lados opostos, Zeca venceu.
        var jogos = new List<JogoDoRodizio>
        {
            J(1, "Zeca", "X1", "Ana", "X2", 13, 11),
            J(2, "Zeca", "X3", "X4", "X5", 11, 13),
            J(3, "Ana", "X6", "X7", "X8", 13, 11),
        };
        var linhas = ClassificacaoDoRodizio.Ordenar(["Ana", "Zeca"], jogos, 24);
        Assert.Equal(linhas[0].Pontos, linhas[1].Pontos);
        Assert.Equal(linhas[0].Vitorias, linhas[1].Vitorias);
        Assert.Equal(linhas[0].Saldo, linhas[1].Saldo);
        Assert.Equal(["Zeca", "Ana"], Ordem(linhas));
    }

    [Fact]
    public void Desempate_5_sem_confronto_decidido_vale_o_nome()
    {
        // Nunca se enfrentaram.
        var semConfronto = new List<JogoDoRodizio>
        {
            J(1, "Zeca", "X1", "X2", "X3", 13, 11),
            J(2, "Ana", "X4", "X5", "X6", 13, 11),
        };
        Assert.Equal(["Ana", "Zeca"], Ordem(ClassificacaoDoRodizio.Ordenar(["Zeca", "Ana"], semConfronto, 24)));

        // Enfrentaram-se duas vezes, uma vitória pra cada: o confronto não decide.
        var umAUm = new List<JogoDoRodizio>
        {
            J(1, "Zeca", "X1", "Ana", "X2", 13, 11),
            J(2, "Ana", "X3", "Zeca", "X4", 13, 11),
        };
        Assert.Equal(["Ana", "Zeca"], Ordem(ClassificacaoDoRodizio.Ordenar(["Zeca", "Ana"], umAUm, 24)));

        // Três empatados: confronto direto entre três é circular — vai direto pro nome.
        var tres = new List<JogoDoRodizio>
        {
            J(1, "Zeca", "X1", "Ana", "X2", 13, 11),
            J(2, "Ana", "X3", "Maria", "X4", 13, 11),
            J(3, "Maria", "X5", "Zeca", "X6", 13, 11),
        };
        Assert.Equal(["Ana", "Maria", "Zeca"], Ordem(ClassificacaoDoRodizio.Ordenar(["Zeca", "Maria", "Ana"], tres, 24)));
    }

    // ── O arquivo ────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(FormatoDoRodizio.Americano)]
    [InlineData(FormatoDoRodizio.Mexicano)]
    public void Salvo_e_carregado_no_meio_e_igual_e_continua_igual(FormatoDoRodizio formato)
    {
        var jogadores = Jogadores(9);
        jogadores[2] = new JogadorDoRodizio("Eu", 70, humano: true);
        var t = new TorneioDeRodizio(jogadores, new RegrasDoRodizio(formato, PontosPorJogo: 32, Rodadas: 6), semente: 77);
        Assert.Equal(t.Salvar(), TorneioDeRodizio.Carregar(t.Salvar()).Salvar());

        // Duas rodadas jogadas e a terceira aberta, com o jogo do humano (se houver) pendente.
        for (int r = 0; r < 2; r++)
        {
            foreach (var jogo in t.JogosPendentes) t.InformarResultado(jogo.Numero, 20, 12);
            Assert.True(t.AvancarRodada());
        }
        string json = t.Salvar();
        var c = TorneioDeRodizio.Carregar(json);
        Assert.Equal(json, c.Salvar());
        Assert.Equal(t.RodadaAtual, c.RodadaAtual);
        Assert.Equal(t.Regras, c.Regras);
        Assert.Equal(t.Jogadores, c.Jogadores);
        Assert.Equal(t.Jogos, c.Jogos);
        Assert.Equal(t.Classificacao(), c.Classificacao());

        JogarAteOFimInformando(t);
        JogarAteOFimInformando(c);
        Assert.Equal(t.Salvar(), c.Salvar());
        Assert.Equal(t.Classificacao(), c.Classificacao());
    }

    [Fact]
    public void Arquivo_de_versao_desconhecida_e_recusado_com_erro_claro()
    {
        string json = new TorneioDeRodizio(Jogadores(8), semente: 2).Salvar();
        var raiz = JsonNode.Parse(json) as JsonObject;
        Assert.NotNull(raiz);
        Assert.Equal(TorneioDeRodizio.VersaoDoArquivo, (int?)raiz["Versao"]);

        raiz["Versao"] = 99;
        var erro = Assert.Throws<InvalidDataException>(() => TorneioDeRodizio.Carregar(raiz.ToJsonString()));
        Assert.Contains("99", erro.Message);
        Assert.Contains($"{TorneioDeRodizio.VersaoDoArquivo}", erro.Message);
        Assert.Contains("versão", erro.Message);

        raiz.Remove("Versao");
        erro = Assert.Throws<InvalidDataException>(() => TorneioDeRodizio.Carregar(raiz.ToJsonString()));
        Assert.Contains("Versao", erro.Message);

        erro = Assert.Throws<InvalidDataException>(() => TorneioDeRodizio.Carregar("{ isto não é json"));
        Assert.Contains("JSON", erro.Message);
    }

    [Fact]
    public void Arquivo_torto_e_recusado_em_vez_de_virar_evento_pela_metade()
    {
        var t = new TorneioDeRodizio(Jogadores(8), semente: 2);
        t.JogarAteOFim();
        string json = t.Salvar();

        JsonObject Raiz() => Assert.IsType<JsonObject>(JsonNode.Parse(json));
        JsonObject PrimeiroJogo(JsonObject raiz) => Assert.IsType<JsonObject>(Assert.IsType<JsonArray>(raiz["Jogos"])[0]);

        // Placar que não fecha o total.
        var raiz = Raiz();
        PrimeiroJogo(raiz)["PontosA"] = 30;
        Assert.Throws<InvalidDataException>(() => TorneioDeRodizio.Carregar(raiz.ToJsonString()));

        // Jogador que não está no evento.
        raiz = Raiz();
        Assert.IsType<JsonObject>(PrimeiroJogo(raiz)["DuplaA"])["Jogador1"] = "Fantasma";
        Assert.Throws<InvalidDataException>(() => TorneioDeRodizio.Carregar(raiz.ToJsonString()));

        // Sem a lista de jogos.
        raiz = Raiz();
        raiz["Jogos"] = null;
        Assert.Throws<InvalidDataException>(() => TorneioDeRodizio.Carregar(raiz.ToJsonString()));

        // Rodada atual fora do evento.
        raiz = Raiz();
        raiz["RodadaAtual"] = 99;
        Assert.Throws<InvalidDataException>(() => TorneioDeRodizio.Carregar(raiz.ToJsonString()));
    }
}
