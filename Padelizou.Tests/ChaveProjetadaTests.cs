using Padelizou.Services;

namespace Padelizou.Tests;

// A prévia do mata-mata por COLOCAÇÃO ("1º do Grupo A x 2º do Grupo C"), mostrada enquanto a
// fase de grupos ainda está rolando. Ela passa pelo mesmo motor do sorteio de verdade — o
// risco é justamente esse deixar de valer e a tela prometer um cruzamento que não acontece.
public class ChaveProjetadaTests
{
    [Fact]
    public void Quatro_grupos_projetam_oito_vagas_em_quatro_jogos_sem_bye()
    {
        var (fase, confrontos, byes) = ChaveProjetada.Montar(new[] { "Grupo A", "Grupo B", "Grupo C", "Grupo D" });

        Assert.Equal("Quartas de Final", fase);
        Assert.Equal(4, confrontos.Count);
        Assert.Empty(byes);   // 8 classificados enchem o quadro de 8 — ninguém descansa

        // Toda vaga aparece uma vez só: 4 grupos x 2 classificados = 8 lugares no quadro.
        var vagas = confrontos.SelectMany(c => new[] { c.Lado1.Rotulo, c.Lado2.Rotulo }).ToList();
        Assert.Equal(8, vagas.Count);
        Assert.Equal(8, vagas.Distinct().Count());
    }

    [Fact]
    public void Tres_grupos_todos_avancam_e_os_melhores_primeiros_descansam()
    {
        // A regra do Felipe (05/08): passam 1º e 2º SEMPRE. 6 classificados → quadro de 8:
        // os 2 melhores 1ºs pulam direto pra fase seguinte, os 4 restantes jogam antes.
        // A régua antiga cortava dois 2ºs sem mata-mata nenhum — era o erro do feminino.
        var (fase, confrontos, byes) = ChaveProjetada.Montar(new[] { "Grupo A", "Grupo B", "Grupo C" });

        Assert.Equal("Quartas de Final", fase);
        Assert.Equal(2, confrontos.Count);
        Assert.Equal(2, byes.Count);
        Assert.All(byes, b => Assert.Equal(1, b.Posicao));   // o descanso é de 1º colocado

        // Ninguém cortado: 6 vagas entre jogos e byes, sem repetição.
        var vagas = confrontos.SelectMany(c => new[] { c.Lado1.Rotulo, c.Lado2.Rotulo })
            .Concat(byes.Select(b => b.Rotulo)).ToList();
        Assert.Equal(6, vagas.Count);
        Assert.Equal(6, vagas.Distinct().Count());
    }

    [Fact]
    public void Primeiro_nunca_cruza_com_primeiro_na_estreia()
    {
        var (_, confrontos, _) = ChaveProjetada.Montar(new[] { "Grupo A", "Grupo B", "Grupo C", "Grupo D" });

        Assert.All(confrontos, c =>
            Assert.False(c.Lado1.Posicao == 1 && c.Lado2.Posicao == 1,
                $"{c.Lado1.Rotulo} x {c.Lado2.Rotulo} põe dois primeiros na estreia"));
    }

    [Fact]
    public void Um_grupo_so_fecha_em_final_direta()
    {
        var (fase, confrontos, byes) = ChaveProjetada.Montar(new[] { "Grupo A" });

        Assert.Equal("Final", fase);
        Assert.Empty(byes);
        var jogo = Assert.Single(confrontos);
        Assert.Equal("1º do Grupo A", jogo.Lado1.Rotulo);
        Assert.Equal("2º do Grupo A", jogo.Lado2.Rotulo);
    }

    [Fact]
    public void Categoria_de_times_projeta_com_o_numero_que_o_organizador_definiu()
    {
        var (fase, confrontos, byes) = ChaveProjetada.Montar(new[] { "Grupo A", "Grupo B" }, classificadosPorGrupo: 4);

        // 2 grupos x 4 = 8 classificados, quadro de 8 cheio — Quartas, sem bye.
        Assert.Equal("Quartas de Final", fase);
        Assert.Equal(4, confrontos.Count);
        Assert.Empty(byes);

        var posicoes = confrontos.SelectMany(c => new[] { c.Lado1.Posicao, c.Lado2.Posicao }).Distinct().Order();
        Assert.Equal(new[] { 1, 2, 3, 4 }, posicoes);
    }

    [Fact]
    public void Sem_grupo_nenhum_nao_ha_o_que_projetar()
    {
        var (_, confrontos, _) = ChaveProjetada.Montar(Array.Empty<string>());
        Assert.Empty(confrontos);
    }

    // A REGRA MAIS IMPORTANTE DO CHAVEAMENTO: dois classificados do mesmo grupo só podem se
    // reencontrar NA FINAL — eles acabaram de se enfrentar na fase de grupos.
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public void Dois_do_mesmo_grupo_so_se_reencontram_na_final(int quantosGrupos)
    {
        var grupos = Enumerable.Range(0, quantosGrupos)
            .Select(i => $"Grupo {(char)('A' + i)}").ToList();

        var (_, confrontos, _) = ChaveProjetada.Montar(grupos);

        int jogos = confrontos.Count;
        int rodadasAteAFinal = TotalDeRodadas(jogos);

        foreach (var grupo in grupos)
        {
            var ondeCairam = confrontos
                .Select((c, indice) => (c, indice))
                .Where(x => x.c.Lado1.Grupo == grupo || x.c.Lado2.Grupo == grupo)
                .Select(x => x.indice)
                .ToList();

            Assert.Equal(2, ondeCairam.Count);

            int encontro = RodadaDoEncontro(ondeCairam[0], ondeCairam[1], jogos);
            Assert.True(encontro == rodadasAteAFinal,
                $"{grupo}: os dois classificados se encontrariam na rodada {encontro} de {rodadasAteAFinal}");
        }
    }

    [Fact]
    public void Ninguem_reedita_na_estreia_o_jogo_que_acabou_de_fazer_no_grupo()
    {
        foreach (int quantos in new[] { 2, 3, 4, 5, 6, 8 })
        {
            var grupos = Enumerable.Range(0, quantos)
                .Select(i => $"Grupo {(char)('A' + i)}").ToList();

            var (_, confrontos, _) = ChaveProjetada.Montar(grupos);

            Assert.All(confrontos, c =>
                Assert.False(c.Lado1.Grupo == c.Lado2.Grupo,
                    $"{quantos} grupos: {c.Lado1.Rotulo} x {c.Lado2.Rotulo} reedita jogo de grupo"));
        }
    }

    // ---- O caminho inteiro até a final ----

    [Fact]
    public void Quatro_grupos_mapeiam_quartas_semifinal_e_final()
    {
        var rodadas = ChaveProjetada.MontarCompleta(new[] { "Grupo A", "Grupo B", "Grupo C", "Grupo D" });

        Assert.Equal(new[] { "Quartas de Final", "Semifinal", "Final" },
                     rodadas.Select(r => r.Fase));
        Assert.Equal(new[] { 4, 2, 1 }, rodadas.Select(r => r.Jogos.Count));
    }

    [Fact]
    public void Tres_grupos_mapeiam_o_caminho_com_os_byes_entrando_na_semifinal()
    {
        // 6 classificados: 2 jogos de abertura + 2 byes → semifinal com 4 (2 vencedores +
        // 2 que passaram direto) → final. O bye aparece NOMEADO na semi, porque o jogador
        // que descansou precisa se achar no mapa.
        var rodadas = ChaveProjetada.MontarCompleta(new[] { "Grupo A", "Grupo B", "Grupo C" });

        Assert.Equal(new[] { "Quartas de Final", "Semifinal", "Final" },
                     rodadas.Select(r => r.Fase));
        Assert.Equal(new[] { 2, 2, 1 }, rodadas.Select(r => r.Jogos.Count));

        var ladosDaSemi = rodadas[1].Jogos.SelectMany(j => new[] { j.Lado1, j.Lado2 }).ToList();
        Assert.Equal(2, ladosDaSemi.Count(l => l.Contains("passou direto")));
        Assert.Equal(2, ladosDaSemi.Count(l => l.StartsWith("Vencedor do jogo")));

        // O melhor bye pega o pior vencedor — pareamento primeiro x último do robô.
        Assert.Equal("Vencedor do jogo 1", rodadas[1].Jogos[0].Lado1);
        Assert.Contains("passou direto", rodadas[1].Jogos[0].Lado2);
    }

    [Fact]
    public void Da_segunda_rodada_em_diante_o_lado_e_o_vencedor_de_um_jogo_numerado()
    {
        var rodadas = ChaveProjetada.MontarCompleta(new[] { "Grupo A", "Grupo B", "Grupo C", "Grupo D" });

        Assert.Equal(new[] { 1, 2, 3, 4 }, rodadas[0].Jogos.Select(j => j.Numero));
        Assert.Equal(new[] { 5, 6 }, rodadas[1].Jogos.Select(j => j.Numero));
        Assert.Equal(new[] { 7 }, rodadas[2].Jogos.Select(j => j.Numero));

        Assert.Equal("Vencedor do jogo 1", rodadas[1].Jogos[0].Lado1);
        Assert.Equal("Vencedor do jogo 4", rodadas[1].Jogos[0].Lado2);
        Assert.Equal("Vencedor do jogo 5", rodadas[2].Jogos[0].Lado1);
        Assert.Equal("Vencedor do jogo 6", rodadas[2].Jogos[0].Lado2);
    }

    [Fact]
    public void Os_dois_lados_da_chave_so_se_encontram_na_final()
    {
        var rodadas = ChaveProjetada.MontarCompleta(new[] { "Grupo A", "Grupo B", "Grupo C", "Grupo D" });

        var semi1 = rodadas[1].Jogos[0];
        var semi2 = rodadas[1].Jogos[1];
        var deCima = new[] { semi1.Lado1, semi1.Lado2 };
        var deBaixo = new[] { semi2.Lado1, semi2.Lado2 };

        Assert.Empty(deCima.Intersect(deBaixo));
        Assert.Single(rodadas[^1].Jogos);
        Assert.Equal("Final", rodadas[^1].Fase);
    }

    [Fact]
    public void Um_grupo_so_mapeia_a_final_e_para()
    {
        var rodadas = ChaveProjetada.MontarCompleta(new[] { "Grupo A" });

        var rodada = Assert.Single(rodadas);
        Assert.Equal("Final", rodada.Fase);
        Assert.Single(rodada.Jogos);
    }

    // Em que rodada os vencedores de dois jogos se encontrariam. Reproduz o cruzamento do
    // robô (jogo j contra jogo N+1-j) sem usar a conta de lados da produção — se as duas
    // divergirem, é aqui que aparece.
    private static int RodadaDoEncontro(int jogoA, int jogoB, int jogos)
    {
        int a = jogoA, b = jogoB, n = jogos, rodada = 1;

        while (n > 1)
        {
            rodada++;
            a = Math.Min(a, n - 1 - a);
            b = Math.Min(b, n - 1 - b);
            n /= 2;
            if (a == b) return rodada;
        }

        return rodada;
    }

    private static int TotalDeRodadas(int jogos)
    {
        int rodadas = 1;
        while (jogos > 1) { jogos /= 2; rodadas++; }
        return rodadas;
    }
}
