using Microsoft.EntityFrameworkCore;
using Padelizou.Services;
using static Padelizou.Services.ChaveamentoMataMata;

namespace Padelizou.Tests;

// C3 do ensaio do torneio do Er (10/09/2026): a SEMIFINAL juntou duas duplas do MESMO grupo
// tendo alternativa — 3ª Masc #83 = 5 (2º C) × 8 (1º C); 6ª Masc #84 = 29 (2º C) × 27 (1º C).
//
// A promessa do cabeçalho de ChaveamentoMataMata é que dois classificados do mesmo grupo caem
// em metades opostas do quadro e só se reencontram NA FINAL. Com bye (8 duplas → 3 grupos →
// 6 classificados num quadro de 8) a semeadura punha só os JOGOS num lado: os byes entram na
// semifinal pela ordem de AvancoDaChave, cruzados primeiro × último por ParearVencedores — a
// vaga deles TEM lado, mas a semeadura não olhava pra ele, e o 1º C descansado esperava o 2º C
// do outro lado do mesmo jogo.
public class SemifinalNaoJuntaOMesmoGrupoTests
{
    // ---- O motor puro, com os grupos EXATOS das duas categorias do ensaio ----

    public static IEnumerable<object[]> CategoriasDoEr()
    {
        // 3ª Masc — A: 1 (1V,+4) > 6 (0V,-4); B: 3 (2V,+5) > 2 (1V,+1); C: 8 (2V,+7) > 5 (1V,+3).
        // Byes 8 e 3; Quartas 1×2 e 5×6; Semi #83 = 5 × 8, os dois do Grupo C.
        yield return new object[]
        {
            "3ª Masc",
            new List<Classificado>
            {
                new(1, "Grupo A", 1, 4, 1), new(6, "Grupo A", 0, -4, 2),
                new(3, "Grupo B", 2, 5, 1), new(2, "Grupo B", 1, 1, 2),
                new(8, "Grupo C", 2, 7, 1), new(5, "Grupo C", 1, 3, 2),
            },
        };

        // 6ª Masc — A: 31 (1V,+5) > 26 (0V,-5); B: 25 (2V,+12) > 28 (1V,+6); C: 27 (2V,+9) > 29 (1V,-6).
        // Byes 25 e 27; Quartas 31×29 e 28×26; Semi #84 = 29 × 27, os dois do Grupo C.
        yield return new object[]
        {
            "6ª Masc",
            new List<Classificado>
            {
                new(31, "Grupo A", 1, 5, 1), new(26, "Grupo A", 0, -5, 2),
                new(25, "Grupo B", 2, 12, 1), new(28, "Grupo B", 1, 6, 2),
                new(27, "Grupo C", 2, 9, 1), new(29, "Grupo C", 1, -6, 2),
            },
        };
    }

    [Theory]
    [MemberData(nameof(CategoriasDoEr))]
    public void No_torneio_do_Er_nenhuma_semifinal_junta_duas_duplas_do_mesmo_grupo(
        string categoria, List<Classificado> classificados)
    {
        var (fase, quartas, byes) = MontarPrimeiraFase(classificados);

        Assert.Equal("Quartas de Final", fase);
        Assert.Equal(2, quartas.Count);
        Assert.Equal(2, byes.Count);

        var grupoDe = classificados.ToDictionary(c => c.DuplaId, c => c.Grupo);

        // Seja quem for que vença cada jogo das Quartas, a semifinal que o robô monta —
        // vencedores na ordem dos jogos + byes, primeiro × último (AvancoDaChave +
        // ParearVencedores) — nunca pode juntar duas duplas do mesmo grupo.
        foreach (var vencedores in TodosOsResultados(quartas))
        {
            var semis = ParearVencedores(vencedores.Concat(byes).ToList());

            Assert.All(semis, s => Assert.False(grupoDe[s.Dupla1Id] == grupoDe[s.Dupla2Id],
                $"{categoria}: a semifinal {s.Dupla1Id} × {s.Dupla2Id} reúne duas duplas do {grupoDe[s.Dupla1Id]} " +
                $"(Quartas {string.Join(", ", quartas.Select(q => $"{q.Dupla1Id}×{q.Dupla2Id}"))}; byes {string.Join(", ", byes)})"));
        }
    }

    // Todas as combinações de vencedores de uma rodada (2^n).
    private static IEnumerable<List<int>> TodosOsResultados(List<Confronto> jogos)
    {
        for (int mascara = 0; mascara < 1 << jogos.Count; mascara++)
            yield return jogos.Select((j, k) => (mascara & (1 << k)) == 0 ? j.Dupla1Id : j.Dupla2Id).ToList();
    }

    // ---- A regra em geral: seja qual for a campanha, os dois do grupo ficam em metades opostas ----
    //
    // Formas em que SEMPRE existe arranjo perfeito (os byes são 1ºs de grupos distintos e cabe
    // um de cada grupo em cada metade): 2, 3, 4, 6, 7 e 8 grupos com 2 classificados. 5 grupos
    // fica de fora: lá o 6º bye é o melhor 2º, e a metade dele sai da campanha — pode cair do
    // lado do próprio 1º sem que a semeadura tenha o que fazer.
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void Dois_do_mesmo_grupo_caem_em_metades_opostas_seja_qual_for_a_campanha(int grupos)
    {
        for (int semente = 0; semente < 40; semente++)
        {
            var campanha = new Random(semente);
            var classificados = new List<Classificado>();
            for (int g = 0; g < grupos; g++)
            {
                string grupo = $"Grupo {(char)('A' + g)}";
                classificados.Add(new(g * 2 + 1, grupo, campanha.Next(1, 3), campanha.Next(0, 12), 1));
                classificados.Add(new(g * 2 + 2, grupo, campanha.Next(0, 2), campanha.Next(-12, 6), 2));
            }
            var grupoDe = classificados.ToDictionary(c => c.DuplaId, c => c.Grupo);

            var (_, jogos, byes) = MontarPrimeiraFase(classificados);

            // A vaga de cada dupla na rodada seguinte: quem joga herda a do jogo, quem descansa
            // vem depois dos vencedores, na ordem dos byes (a ordem de AvancoDaChave).
            var lado = LadoDeCadaVaga(jogos.Count + byes.Count);
            var ladoDe = new Dictionary<int, int>();
            for (int k = 0; k < jogos.Count; k++)
            {
                ladoDe[jogos[k].Dupla1Id] = lado[k];
                ladoDe[jogos[k].Dupla2Id] = lado[k];
            }
            for (int b = 0; b < byes.Count; b++) ladoDe[byes[b]] = lado[jogos.Count + b];

            foreach (var grupo in classificados.GroupBy(c => c.Grupo))
            {
                var lados = grupo.Select(c => ladoDe[c.DuplaId]).ToList();
                Assert.True(lados[0] != lados[1],
                    $"{grupos} grupos, semente {semente}: os dois do {grupo.Key} caíram na mesma metade " +
                    $"(jogos {string.Join(", ", jogos.Select(j => $"{j.Dupla1Id}×{j.Dupla2Id}"))}; byes {string.Join(", ", byes)})");
            }

            Assert.All(jogos, j => Assert.False(grupoDe[j.Dupla1Id] == grupoDe[j.Dupla2Id],
                $"{grupos} grupos, semente {semente}: {j.Dupla1Id} × {j.Dupla2Id} reedita o jogo de grupo"));
        }
    }

    // A metade da chave em que cada vaga da rodada seguinte cai, derivada da régua REAL: o
    // ParearVencedores aplicado rodada a rodada sobre os índices, até sobrarem os dois lados
    // da final. Uma conta paralela aqui provaria a conta paralela, não o motor.
    private static int[] LadoDeCadaVaga(int vagas)
    {
        var ondeEstou = Enumerable.Range(0, vagas).ToArray();
        int nestaRodada = vagas;
        while (nestaRodada > 2)
        {
            var pares = ParearVencedores(Enumerable.Range(0, nestaRodada).ToList());
            var jogoDe = new int[nestaRodada];
            for (int j = 0; j < pares.Count; j++)
            {
                jogoDe[pares[j].Dupla1Id] = j;
                jogoDe[pares[j].Dupla2Id] = j;
            }
            for (int v = 0; v < vagas; v++) ondeEstou[v] = jogoDe[ondeEstou[v]];
            nestaRodada = pares.Count;
        }
        return ondeEstou;
    }

    // ---- O fluxo de verdade: sorteio, grupos, robô das Quartas, robô das Semis ----

    [Fact]
    public async Task O_robo_monta_as_semifinais_do_Er_sem_juntar_duplas_do_mesmo_grupo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 8);   // 3 grupos: 3/3/2
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        // Fase de grupos: vence sempre a dupla de menor Id, 9x3. A classificação sai determinada
        // (1º = menor Id de cada grupo) e os 1ºs dos dois grupos de 3 (2 vitórias) descansam as
        // Quartas, como no Er — o 1º do grupo de 2 (1 vitória) joga.
        var jogosDeGrupo = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id).OrderBy(p => p.Id).ToListAsync();
        Assert.Equal(7, jogosDeGrupo.Count);
        foreach (var jogo in jogosDeGrupo)
        {
            bool venceA1 = jogo.Dupla1Id < jogo.Dupla2Id;
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, venceA1 ? 9 : 3, venceA1 ? 3 : 9);
        }

        var quartas = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Quartas de Final")
            .OrderBy(p => p.Id).ToListAsync();
        Assert.Equal(2, quartas.Count);

        var byes = await AvancoDaChave.ByesDaCategoriaAsync(ctx, categoria.Id);
        Assert.Equal(2, byes.Count);

        var grupoDe = (await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToListAsync())
            .ToDictionary(d => d.Id, d => d.Grupo ?? "");
        var gruposQueDescansam = byes.Select(b => grupoDe[b]).ToHashSet();

        // O que a prévia promete pra semifinal ANTES de as Quartas serem jogadas.
        var previa = ProximasFasesDaChave.Montar(
            quartas.Select(q => new ProximasFasesDaChave.PartidaDaChave(
                q.Id, q.Fase, q.Dupla1Id.ToString(), q.Dupla2Id.ToString(), q.HorarioPrevisto)).ToList(),
            byes.Select(b => b.ToString()).ToList());
        var semisPrevistas = previa.Rodadas.Single(r => r.Fase == "Semifinal").Confrontos;
        Assert.Equal(2, semisPrevistas.Count);

        // Nas Quartas vence a dupla mais PERIGOSA pra regra: a que tem um colega de grupo descansando.
        foreach (var q in quartas)
        {
            bool dupla1Perigosa = gruposQueDescansam.Contains(grupoDe[q.Dupla1Id]);
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, q, dupla1Perigosa ? 9 : 3, dupla1Perigosa ? 3 : 9);
        }

        var semis = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Semifinal")
            .OrderBy(p => p.Id).ToListAsync();
        Assert.Equal(2, semis.Count);

        Assert.All(semis, s => Assert.False(grupoDe[s.Dupla1Id] == grupoDe[s.Dupla2Id],
            $"Semifinal {s.Dupla1Id} × {s.Dupla2Id}: as duas são do {grupoDe[s.Dupla1Id]} " +
            $"(Quartas {string.Join(", ", quartas.Select(q => $"{q.Dupla1Id}×{q.Dupla2Id}"))}; byes {string.Join(", ", byes)})"));

        // E o quadro real fez o que a prévia prometeu: a Semifinal k é o vencedor das Quartas k
        // contra o bye que a prévia pôs ali.
        for (int k = 0; k < 2; k++)
        {
            Assert.Equal($"Vencedor Quartas de Final {k + 1}", semisPrevistas[k].Lado1.Rotulo);
            int byePrevisto = int.Parse(semisPrevistas[k].Lado2.Rotulo);

            Assert.NotNull(quartas[k].VencedorId);
            int vencedor = quartas[k].VencedorId.GetValueOrDefault();

            Assert.Equal(new HashSet<int> { vencedor, byePrevisto },
                         new HashSet<int> { semis[k].Dupla1Id, semis[k].Dupla2Id });
        }
    }

    // Categoria de 4 duplas (2 grupos de 2, sem bye): a semifinal é 1º A × 2º B e 1º B × 2º A,
    // como sempre foi — a correção do bye não pode mexer na chave cheia.
    [Fact]
    public async Task Com_dois_grupos_de_dois_a_semifinal_continua_1o_de_um_contra_2o_do_outro()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.GerarChaves(torneio.Id);

        var jogosDeGrupo = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id).OrderBy(p => p.Id).ToListAsync();
        Assert.Equal(2, jogosDeGrupo.Count);
        foreach (var jogo in jogosDeGrupo)
        {
            bool venceA1 = jogo.Dupla1Id < jogo.Dupla2Id;
            await TestInfra.FinalizarComPlacarAsync(ctx, controller, jogo, venceA1 ? 9 : 3, venceA1 ? 3 : 9);
        }

        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToListAsync();
        var classificados = ClassificacaoDeGrupos.Calcular(duplas, jogosDeGrupo).ToDictionary(c => c.DuplaId);
        Assert.Equal(4, classificados.Count);

        var semis = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Semifinal")
            .OrderBy(p => p.Id).ToListAsync();
        Assert.Equal(2, semis.Count);

        foreach (var semi in semis)
        {
            var (c1, c2) = (classificados[semi.Dupla1Id], classificados[semi.Dupla2Id]);
            Assert.NotEqual(c1.Grupo, c2.Grupo);
            Assert.NotEqual(c1.Posicao, c2.Posicao);   // um 1º contra um 2º
        }
    }
}
