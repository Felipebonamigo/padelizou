using System.Text.Json.Nodes;

namespace Padel.Core.Tests;

// O using mora DENTRO do namespace de propósito: assim um tipo de mesmo nome que outra tarefa
// venha a pôr direto em Padel.Core (um "Confronto", um "Classificado") não sequestra os nomes daqui.
using Padel.Core.Torneio;

/// <summary>
/// O modo Torneio/Carreira sem tela: classificação de grupo e desempates, cruzamento dos grupos
/// pra chave, chave direta com bye, avanço rodada a rodada, partida IA x IA simulada sem física,
/// pontos de ranking e o arquivo da carreira.
/// </summary>
public class TorneioTests
{
    // ── Ajudantes ────────────────────────────────────────────────────────────────────────

    private static SetEncerrado S(int a, int b) => new([a, b], null);
    private static SetEncerrado TB(int a, int b, int tbA, int tbB) => new([a, b], [tbA, tbB]);

    private static JogoDoTorneio J(string a, string b, params SetEncerrado[] sets) =>
        new(0, 0, a, b, Grupo: "X", Sets: sets);

    /// <summary>D1 é a mais forte, D2 a segunda… — a semeadura esperada é a ordem do nome.</summary>
    private static List<DuplaParticipante> PorForca(int n) =>
        Enumerable.Range(1, n).Select(i => new DuplaParticipante($"D{i}", 100 - 3 * i)).ToList();

    private static string[] Nomes(IEnumerable<LinhaDaClassificacao> linhas) => linhas.Select(l => l.Dupla).ToArray();

    private static string Texto(IReadOnlyList<SetEncerrado> sets) =>
        string.Join(" ", sets.Select(s => s.TieBreak is null
            ? $"{s.Games[0]}-{s.Games[1]}"
            : $"{s.Games[0]}-{s.Games[1]}({s.TieBreak[0]}-{s.TieBreak[1]})"));

    /// <summary>
    /// Conferência INDEPENDENTE do motor: o que é um placar de padel válido, escrito de novo aqui,
    /// e não chamando a validação da implementação (que se confirmaria sozinha).
    /// </summary>
    private static void AssertPartidaValida(JogoDoTorneio jogo, int setsParaVencer)
    {
        Assert.NotNull(jogo.Sets);
        int[] ganhos = [0, 0];
        foreach (var set in jogo.Sets)
        {
            Assert.True(ganhos[0] < setsParaVencer && ganhos[1] < setsParaVencer,
                $"jogo {jogo.Numero}: set jogado depois da partida decidida ({Texto(jogo.Sets)})");
            int a = set.Games[0], b = set.Games[1], v = Math.Max(a, b), p = Math.Min(a, b);
            bool normal = (v == 6 && p >= 0 && p <= 4) || (v == 7 && p == 5);
            bool tie = v == 7 && p == 6;
            Assert.True(normal || tie, $"jogo {jogo.Numero}: {a}-{b} não é placar de set");
            if (tie)
            {
                Assert.NotNull(set.TieBreak);
                int tv = Math.Max(set.TieBreak[0], set.TieBreak[1]), tp = Math.Min(set.TieBreak[0], set.TieBreak[1]);
                Assert.True(tv >= 7 && tv - tp >= 2 && (tv == 7 || tv - tp == 2),
                    $"jogo {jogo.Numero}: tie-break {set.TieBreak[0]}-{set.TieBreak[1]} não fecha");
                Assert.Equal(a > b, set.TieBreak[0] > set.TieBreak[1]);
            }
            else
            {
                Assert.Null(set.TieBreak);
            }
            ganhos[a > b ? 0 : 1]++;
        }
        Assert.Equal(setsParaVencer, Math.Max(ganhos[0], ganhos[1]));
        Assert.Equal(ganhos[0] > ganhos[1] ? jogo.DuplaA : jogo.DuplaB, jogo.Vencedor);
    }

    /// <summary>Um Placar de verdade, fechado: o time <paramref name="time"/> vence cada set por 6 a <paramref name="gamesDoPerdedor"/>.</summary>
    private static Placar PlacarVencidoPor(int time, int gamesDoPerdedor, int sets = 1)
    {
        var placar = new Placar(setsParaVencer: sets);
        for (int s = 0; s < sets; s++)
        {
            for (int g = 0; g < gamesDoPerdedor; g++) for (int i = 0; i < 4; i++) placar.PontoPara(1 - time);
            for (int g = 0; g < 6; g++) for (int i = 0; i < 4; i++) placar.PontoPara(time);
        }
        return placar;
    }

    /// <summary>O "jogo" do humano, de mentira: resultado pela força, com uma semente fora da do torneio.</summary>
    private static void InformarPendentes(TorneioDeDuplas t)
    {
        foreach (var jogo in t.JogosPendentes)
        {
            var sets = SimuladorDePartida.Simular(t.Dupla(jogo.DuplaA).Forca, t.Dupla(jogo.DuplaB).Forca,
                t.Regras.SetsParaVencer, new Aleatorio(1000u + (uint)jogo.Numero));
            t.InformarResultado(jogo.Numero, sets);
        }
    }

    private static void JogarEtapaComHumano(TorneioDeDuplas t)
    {
        for (int guarda = 0; !t.Encerrado; guarda++)
        {
            Assert.True(guarda < 50, "a etapa não termina");
            InformarPendentes(t);
            t.AvancarRodada();
        }
    }

    /// <summary>
    /// A fase de uma dupla num torneio ENCERRADO, lida dos jogos e SEM passar pelo
    /// <see cref="TorneioDeDuplas.FaseDaDupla"/> (comparar com ele seria o motor conferindo a si
    /// mesmo): o campeão é campeão; quem perdeu um jogo de mata-mata parou na fase daquele jogo,
    /// pelo nome que a tela mostra; quem não perdeu no mata-mata sem ser campeão nunca entrou
    /// nele — caiu nos grupos.
    /// </summary>
    private static FaseAlcancada FaseLidaDosJogos(TorneioDeDuplas t, string dupla)
    {
        Assert.True(t.Encerrado, "a fase lida dos jogos só vale com o torneio acabado");
        if (t.Campeao?.Nome == dupla) return FaseAlcancada.Campeao;
        var derrota = t.Jogos.SingleOrDefault(j => j.Grupo is null && j.Envolve(dupla) && j.Vencedor != dupla);
        if (derrota is null) return FaseAlcancada.FaseDeGrupos;
        return t.NomeDaFase(derrota) switch
        {
            "Final" => FaseAlcancada.Final,
            "Semifinal" => FaseAlcancada.Semifinal,
            "Quartas de Final" => FaseAlcancada.Quartas,
            "Oitavas de Final" => FaseAlcancada.Oitavas,
            "Primeira Rodada" => FaseAlcancada.PrimeiraRodada,
            var outra => throw new InvalidOperationException($"fase sem nome conhecido: {outra}"),
        };
    }

    // ── Classificação do grupo: cada critério de desempate, isolado ────────────────────

    [Fact]
    public void Classificacao_vitorias_vem_antes_do_saldo()
    {
        // A: 2 vitórias e saldo +2. B: 1 vitória e saldo +5. Se o saldo viesse antes, B passaria A.
        var jogos = new[]
        {
            J("A", "B", TB(7, 6, 7, 5)),
            J("A", "C", TB(7, 6, 7, 5)),
            J("B", "C", S(6, 0)),
        };
        Assert.Equal(["A", "B", "C"], Nomes(ClassificacaoDoGrupo.Ordenar(["C", "B", "A"], jogos)));
    }

    [Fact]
    public void Classificacao_saldo_de_games_vem_antes_dos_games_a_favor()
    {
        // Ciclo (1 vitória cada). A: saldo +4 com 11 games. C: saldo +1 com 13. B: saldo −5.
        var jogos = new[]
        {
            J("A", "B", S(6, 0)),
            J("B", "C", TB(7, 6, 7, 3)),
            J("C", "A", S(7, 5)),
        };
        var tabela = ClassificacaoDoGrupo.Ordenar(["B", "C", "A"], jogos);
        Assert.Equal(["A", "C", "B"], Nomes(tabela));
        Assert.Equal([4, 1, -5], tabela.Select(l => l.Saldo));
        Assert.Equal([11, 13, 7], tabela.Select(l => l.GamesPro));
    }

    [Fact]
    public void Classificacao_games_a_favor_desempatam_vitorias_e_saldo_iguais()
    {
        // Ciclo com saldo 0 pra todo mundo: A e B fazem 11 games, C faz 10 — C é a última,
        // mesmo sendo a primeira do ranking (o ranking só entra depois da quadra).
        var jogos = new[]
        {
            J("A", "B", S(7, 5)),
            J("B", "C", S(6, 4)),
            J("C", "A", S(6, 4)),
        };
        var ranking = new Dictionary<string, int> { ["C"] = 5000 };
        var tabela = ClassificacaoDoGrupo.Ordenar(["C", "A", "B"], jogos, ranking);
        Assert.Equal("C", tabela[2].Dupla);
        Assert.All(tabela, l => Assert.Equal(0, l.Saldo));
    }

    [Fact]
    public void Classificacao_empate_de_duas_decide_no_confronto_direto_e_nao_no_ranking_nem_no_sorteio()
    {
        // A e B empatam em tudo (1 vitória, saldo 0, 11 games); A venceu o jogo entre elas.
        var jogos = new[]
        {
            J("A", "B", S(7, 5)),
            J("B", "C", S(6, 4)),
            J("C", "A", S(6, 4)),
        };
        var ranking = new Dictionary<string, int> { ["B"] = 5000 };
        for (uint semente = 1; semente <= 20; semente++)
            Assert.Equal(["A", "B", "C"], Nomes(ClassificacaoDoGrupo.Ordenar(["B", "A", "C"], jogos, ranking, semente)));
    }

    [Fact]
    public void Classificacao_empate_de_tres_decide_no_ranking()
    {
        // Três 6-4 em ciclo: vitórias, saldo e games iguais, e confronto direto circular.
        var jogos = new[]
        {
            J("A", "B", S(6, 4)),
            J("B", "C", S(6, 4)),
            J("C", "A", S(6, 4)),
        };
        var ranking = new Dictionary<string, int> { ["C"] = 300, ["A"] = 200, ["B"] = 100 };
        for (uint semente = 1; semente <= 20; semente++)
            Assert.Equal(["C", "A", "B"], Nomes(ClassificacaoDoGrupo.Ordenar(["A", "B", "C"], jogos, ranking, semente)));
    }

    [Fact]
    public void Classificacao_empate_total_sem_ranking_vai_pro_sorteio_estavel_que_nao_e_a_ordem_de_inscricao()
    {
        var jogos = new[]
        {
            J("A", "B", S(6, 4)),
            J("B", "C", S(6, 4)),
            J("C", "A", S(6, 4)),
        };
        string[][] ordensDeInscricao =
        [
            ["A", "B", "C"], ["A", "C", "B"], ["B", "A", "C"],
            ["B", "C", "A"], ["C", "A", "B"], ["C", "B", "A"],
        ];
        var primeiros = new HashSet<string>();
        for (uint semente = 1; semente <= 30; semente++)
        {
            var referencia = Nomes(ClassificacaoDoGrupo.Ordenar(ordensDeInscricao[0], jogos, null, semente));
            foreach (var ordem in ordensDeInscricao)
                Assert.Equal(referencia, Nomes(ClassificacaoDoGrupo.Ordenar(ordem, jogos, null, semente)));
            primeiros.Add(referencia[0]);
        }
        Assert.True(primeiros.Count > 1, "o sorteio deu sempre a mesma dupla em 1º — não é sorteio");
    }

    [Fact]
    public void No_torneio_o_empate_de_tres_no_grupo_e_decidido_pelo_ranking_que_o_torneio_recebeu_e_congelou()
    {
        // O degrau "ranking" testado no TORNEIO, e não só na ClassificacaoDoGrupo solta: é o
        // torneio que tem de passar adiante os pontos que recebeu (na carreira, o acumulado).
        // Três duplas humanas — o jogo informa os três placares — de mesma força: um grupo só, e
        // um ciclo de 6-4 que empata vitórias, saldo e games, com confronto direto circular.
        var venceDe = new Dictionary<string, string> { ["A"] = "B", ["B"] = "C", ["C"] = "A" };
        for (uint semente = 1; semente <= 20; semente++)
        {
            var duplas = new[] { "A", "B", "C" }.Select(n => new DuplaParticipante(n, 50, humana: true)).ToList();
            var ranking = new Dictionary<string, int> { ["C"] = 300, ["A"] = 200, ["B"] = 100 };
            var t = new TorneioDeDuplas(duplas, new RegrasDoTorneio(FormatoDoTorneio.GruposEMataMata), semente, ranking);
            ranking["B"] = 999;   // congelado: mexer no dicionário de quem chamou, depois, não muda a tabela

            var grupo = Assert.Single(t.Grupos);
            for (int guarda = 0; t.EmFaseDeGrupos; guarda++)
            {
                Assert.True(guarda < 5, "os grupos não fecham");
                foreach (var jogo in t.JogosPendentes)
                    t.InformarResultado(jogo.Numero, [venceDe[jogo.DuplaA] == jogo.DuplaB ? S(6, 4) : S(4, 6)]);
                Assert.True(t.AvancarRodada());
            }

            var tabela = t.Classificacao(grupo.Nome);
            Assert.All(tabela, l => Assert.Equal((1, 0, 10), (l.Vitorias, l.Saldo, l.GamesPro)));
            Assert.Equal(["C", "A", "B"], Nomes(tabela));
            // E a tabela manda no resto: C e A seguem, B cai no grupo.
            Assert.DoesNotContain("B", t.Quadro);
            Assert.Equal(FaseAlcancada.FaseDeGrupos, t.FaseDaDupla("B"));
        }
    }

    // ── Composição dos grupos (régua do Padelizou) ─────────────────────────────────────

    [Theory]
    [InlineData(7, "D1 D4|D2 D3|D5 D6 D7")]
    [InlineData(8, "D1 D2|D3 D6 D8|D4 D5 D7")]
    [InlineData(9, "D1 D6 D9|D2 D5 D8|D3 D4 D7")]
    public void Grupos_saem_de_tres_por_faixas_com_os_melhores_em_grupo_de_dois_quando_sobra(int n, string esperado)
    {
        var t = new TorneioDeDuplas(PorForca(n), new RegrasDoTorneio(FormatoDoTorneio.GruposEMataMata), semente: 1);
        Assert.Equal(esperado, string.Join("|", t.Grupos.Select(g => string.Join(" ", g.Duplas))));
        Assert.Equal(["A", "B", "C"], t.Grupos.Select(g => g.Nome));
        foreach (var g in t.Grupos)
        {
            var doGrupo = t.Jogos.Where(j => j.Grupo == g.Nome).ToList();
            Assert.Equal(g.Duplas.Count * (g.Duplas.Count - 1) / 2, doGrupo.Count);
            Assert.Equal(doGrupo.Count, doGrupo.Select(j => j.Rodada).Distinct().Count());
        }
    }

    // ── Cruzamento dos grupos pra chave ────────────────────────────────────────────────

    private static List<Classificado> PrimeirosESegundos(params string[] grupos) =>
        grupos.Select(g => new Classificado($"1{g}", g, Vitorias: 2, Saldo: 6, Posicao: 1, Jogos: 2))
            .Concat(grupos.Select(g => new Classificado($"2{g}", g, Vitorias: 1, Saldo: 0, Posicao: 2, Jogos: 2)))
            .ToList();

    private static void AssertIrmaosEmMetadesOpostas(IReadOnlyList<string?> quadro, IEnumerable<string> grupos)
    {
        int metade = quadro.Count / 2;
        foreach (var g in grupos)
        {
            int primeiro = quadro.ToList().IndexOf($"1{g}"), segundo = quadro.ToList().IndexOf($"2{g}");
            Assert.True(primeiro >= 0 && segundo >= 0);
            Assert.NotEqual(primeiro < metade, segundo < metade);
        }
    }

    [Fact]
    public void Cruzamento_de_quatro_grupos_poe_cada_1o_contra_um_2o_de_outro_grupo_e_irmaos_em_metades_opostas()
    {
        var (confrontos, byes) = CruzamentoDosGrupos.MontarPrimeiraFase(PrimeirosESegundos("A", "B", "C", "D"));
        Assert.Empty(byes);
        Assert.Equal(
            [new Confronto("1A", "2C"), new Confronto("1B", "2D"), new Confronto("1C", "2A"), new Confronto("1D", "2B")],
            confrontos);

        var quadro = QuadroDoMataMata.DoCruzamento(confrontos, byes);
        Assert.Equal(["1A", "2C", "1D", "2B", "1B", "2D", "1C", "2A"], quadro);
        AssertIrmaosEmMetadesOpostas(quadro, ["A", "B", "C", "D"]);
    }

    [Fact]
    public void Cruzamento_de_tres_grupos_da_bye_aos_dois_primeiros_e_o_bye_tambem_respeita_a_metade()
    {
        // O "caso do Er" do Padelizou: a semeadura de sempre deixaria o 1º C descansado esperando
        // o 2º C do outro lado; a busca acha o arranjo sem reencontro antes da final.
        var (confrontos, byes) = CruzamentoDosGrupos.MontarPrimeiraFase(PrimeirosESegundos("A", "B", "C"));
        Assert.Equal(["1A", "1B"], byes);
        Assert.Equal([new Confronto("2A", "2C"), new Confronto("1C", "2B")], confrontos);

        var quadro = QuadroDoMataMata.DoCruzamento(confrontos, byes);
        Assert.Equal(["2A", "2C", "1B", null, "1C", "2B", "1A", null], quadro);
        AssertIrmaosEmMetadesOpostas(quadro, ["A", "B", "C"]);
    }

    [Fact]
    public void O_bye_vai_por_posicao_depois_pra_quem_jogou_menos_depois_pela_ordem_do_grupo_nunca_pela_campanha()
    {
        var classificados = new List<Classificado>
        {
            new("1A", "A", Vitorias: 2, Saldo: 9, Posicao: 1, Jogos: 2),
            new("2A", "A", Vitorias: 2, Saldo: 9, Posicao: 2, Jogos: 1),
            new("1C", "C", Vitorias: 1, Saldo: 1, Posicao: 1, Jogos: 1),
            new("1B", "B", Vitorias: 2, Saldo: 3, Posicao: 1, Jogos: 2),
        };
        Assert.Equal(["1C", "1A", "1B", "2A"], CruzamentoDosGrupos.OrdemDosByes(classificados).Select(c => c.Dupla));
    }

    [Theory]
    [InlineData(12)]
    [InlineData(9)]
    public void No_torneio_os_dois_classificados_de_um_grupo_so_se_reencontram_na_final(int n)
    {
        for (uint semente = 1; semente <= 10; semente++)
        {
            var t = new TorneioDeDuplas(PorForca(n), new RegrasDoTorneio(FormatoDoTorneio.GruposEMataMata), semente);
            while (t.EmFaseDeGrupos) Assert.True(t.AvancarRodada());
            int metade = t.Quadro.Count / 2;
            foreach (var g in t.Grupos)
            {
                var tabela = t.Classificacao(g.Nome);
                int primeiro = t.Quadro.ToList().IndexOf(tabela[0].Dupla), segundo = t.Quadro.ToList().IndexOf(tabela[1].Dupla);
                Assert.True(primeiro >= 0 && segundo >= 0, $"grupo {g.Nome}: classificado fora do quadro");
                Assert.NotEqual(primeiro < metade, segundo < metade);
                Assert.DoesNotContain(tabela[2].Dupla, t.Quadro);
            }
        }
    }

    // ── Chave direta: byes e cabeças de chave pela força ───────────────────────────────

    [Theory]
    [InlineData(5, "D1 - D4 D5 D2 - D3 -", "D1 D2 D3", "D4xD5")]
    [InlineData(6, "D1 - D4 D5 D2 - D3 D6", "D1 D2", "D4xD5 D3xD6")]
    [InlineData(8, "D1 D8 D4 D5 D2 D7 D3 D6", "", "D1xD8 D4xD5 D2xD7 D3xD6")]
    [InlineData(12, "D1 - D8 D9 D4 - D5 D12 D2 - D7 D10 D3 - D6 D11", "D1 D2 D3 D4", "D8xD9 D5xD12 D7xD10 D6xD11")]
    public void Chave_direta_semeia_pela_forca_e_da_os_byes_aos_cabecas(int n, string quadro, string byes, string jogos)
    {
        var embaralhadas = PorForca(n).OrderBy(d => d.Nome, StringComparer.Ordinal).Reverse().ToList();   // a ordem de inscrição não manda
        var t = new TorneioDeDuplas(embaralhadas, new RegrasDoTorneio(FormatoDoTorneio.ChaveDireta), semente: 1);
        Assert.Equal(quadro, string.Join(" ", t.Quadro.Select(v => v ?? "-")));
        Assert.Equal(byes, string.Join(" ", t.ByesDaPrimeiraRodada));
        Assert.Equal(jogos, string.Join(" ", t.JogosDaRodadaAtual.Select(j => $"{j.DuplaA}x{j.DuplaB}")));
        Assert.Equal(QuadroDoMataMata.MenorPotenciaDe2APartirDe(n) - n, t.ByesDaPrimeiraRodada.Count);
    }

    [Fact]
    public void Com_5_duplas_o_1o_cabeca_pega_o_vencedor_do_unico_jogo_e_o_2o_pega_o_3o_na_semifinal()
    {
        var t = new TorneioDeDuplas(PorForca(5), new RegrasDoTorneio(FormatoDoTorneio.ChaveDireta), semente: 9);
        var abertura = Assert.Single(t.JogosDaRodadaAtual);
        Assert.Equal("Quartas de Final", t.NomeDaFase(abertura));
        Assert.True(t.AvancarRodada());
        var semis = t.JogosDaRodadaAtual;
        Assert.Equal(2, semis.Count);
        Assert.Equal("D1", semis[0].DuplaA);
        Assert.Equal(abertura.Vencedor, semis[0].DuplaB);
        Assert.Equal("D2", semis[1].DuplaA);
        Assert.Equal("D3", semis[1].DuplaB);
        Assert.Equal("Semifinal", t.NomeDaFase(semis[0]));
    }

    // ── Partida IA x IA sem física ─────────────────────────────────────────────────────

    [Fact]
    public void A_partida_simulada_tem_placar_de_set_coerente_e_a_mesma_semente_repete_o_placar()
    {
        bool viuTieBreak = false, viu75 = false, viu60 = false;
        foreach (var (fa, fb) in new[] { (50, 50), (90, 20), (20, 90), (100, 0), (0, 0) })
        {
            for (int sets = 1; sets <= 3; sets++)
            {
                for (uint s = 1; s <= 100; s++)
                {
                    var r1 = SimuladorDePartida.Simular(fa, fb, sets, new Aleatorio(s));
                    var r2 = SimuladorDePartida.Simular(fa, fb, sets, new Aleatorio(s));
                    AssertPartidaValida(new JogoDoTorneio(1, 0, "A", "B", Sets: r1), sets);
                    Assert.Equal(Texto(r1), Texto(r2));
                    viuTieBreak |= r1.Any(x => x.TieBreak is not null);
                    viu75 |= r1.Any(x => Math.Max(x.Games[0], x.Games[1]) == 7 && Math.Min(x.Games[0], x.Games[1]) == 5);
                    viu60 |= r1.Any(x => Math.Min(x.Games[0], x.Games[1]) == 0);
                }
            }
        }
        Assert.True(viuTieBreak && viu75 && viu60, "a simulação não produz toda a gama de placares de set");
    }

    [Fact]
    public void Na_partida_simulada_a_forca_manda_e_o_ponto_e_simetrico()
    {
        Assert.Equal(0.5f, SimuladorDePartida.ChanceDoPonto(60, 60), 4);
        Assert.Equal(1f, SimuladorDePartida.ChanceDoPonto(70, 40) + SimuladorDePartida.ChanceDoPonto(40, 70), 4);
        Assert.True(SimuladorDePartida.ChanceDoPonto(70, 40) > SimuladorDePartida.ChanceDoPonto(60, 40));

        int vitoriasDoForte = 0, vitoriasDoPoucoMaisForte = 0;
        for (uint s = 1; s <= 400; s++)
        {
            var forte = SimuladorDePartida.Simular(85, 35, 1, new Aleatorio(s));
            if (forte[0].Games[0] > forte[0].Games[1]) vitoriasDoForte++;
            var parelho = SimuladorDePartida.Simular(55, 50, 1, new Aleatorio(s));
            if (parelho[0].Games[0] > parelho[0].Games[1]) vitoriasDoPoucoMaisForte++;
        }
        Assert.True(vitoriasDoForte > 360, $"85 x 35 venceu só {vitoriasDoForte} de 400");
        Assert.InRange(vitoriasDoPoucoMaisForte, 201, 300);
    }

    // ── Torneio inteiro ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(FormatoDoTorneio.GruposEMataMata, 2)]
    [InlineData(FormatoDoTorneio.GruposEMataMata, 3)]
    [InlineData(FormatoDoTorneio.GruposEMataMata, 4)]
    [InlineData(FormatoDoTorneio.GruposEMataMata, 5)]
    [InlineData(FormatoDoTorneio.GruposEMataMata, 6)]
    [InlineData(FormatoDoTorneio.GruposEMataMata, 7)]
    [InlineData(FormatoDoTorneio.GruposEMataMata, 8)]
    [InlineData(FormatoDoTorneio.GruposEMataMata, 9)]
    [InlineData(FormatoDoTorneio.GruposEMataMata, 12)]
    [InlineData(FormatoDoTorneio.GruposEMataMata, 13)]
    [InlineData(FormatoDoTorneio.GruposEMataMata, 16)]
    [InlineData(FormatoDoTorneio.GruposEMataMata, 20)]
    [InlineData(FormatoDoTorneio.GruposEMataMata, 32)]
    [InlineData(FormatoDoTorneio.ChaveDireta, 2)]
    [InlineData(FormatoDoTorneio.ChaveDireta, 3)]
    [InlineData(FormatoDoTorneio.ChaveDireta, 5)]
    [InlineData(FormatoDoTorneio.ChaveDireta, 6)]
    [InlineData(FormatoDoTorneio.ChaveDireta, 8)]
    [InlineData(FormatoDoTorneio.ChaveDireta, 12)]
    [InlineData(FormatoDoTorneio.ChaveDireta, 17)]
    [InlineData(FormatoDoTorneio.ChaveDireta, 32)]
    public void Torneio_inteiro_de_IA_termina_com_um_campeao_e_so_partidas_validas(FormatoDoTorneio formato, int n)
    {
        for (uint semente = 1; semente <= 5; semente++)
        {
            int sets = 1 + (int)(semente % 2);
            var t = new TorneioDeDuplas(PorForca(n), new RegrasDoTorneio(formato, SetsParaVencer: sets), semente);
            for (int rodadas = 0; t.AvancarRodada(); rodadas++) Assert.True(rodadas < 20, "o torneio não termina");

            Assert.True(t.Encerrado);
            Assert.NotNull(t.Campeao);
            Assert.Equal(FaseAlcancada.Campeao, t.FaseDaDupla(t.Campeao.Nome));
            foreach (var jogo in t.Jogos) AssertPartidaValida(jogo, sets);

            foreach (var rodada in t.Jogos.GroupBy(j => j.Rodada))
            {
                var nomes = rodada.SelectMany(j => new[] { j.DuplaA, j.DuplaB }).ToList();
                Assert.Equal(nomes.Count, nomes.Distinct().Count());
            }

            // Mata-mata é eliminação simples: n entram, n − 1 jogos, ninguém perde duas vezes.
            var mataMata = t.Jogos.Where(j => j.Grupo is null).ToList();
            int entraram = t.Quadro.Count(v => v is not null);
            Assert.Equal(entraram - 1, mataMata.Count);
            var perdedores = mataMata.Select(j => j.Vencedor == j.DuplaA ? j.DuplaB : j.DuplaA).ToList();
            Assert.Equal(perdedores.Count, perdedores.Distinct().Count());
            Assert.DoesNotContain(t.Campeao.Nome, perdedores);
            Assert.Single(mataMata, j => t.NomeDaFase(j) == "Final");

            foreach (var g in t.Grupos)
                Assert.Equal(g.Duplas.Count * (g.Duplas.Count - 1) / 2, t.Jogos.Count(j => j.Grupo == g.Nome));
            if (formato == FormatoDoTorneio.ChaveDireta) Assert.Equal(n, entraram);
            else Assert.Equal(t.Grupos.Sum(g => Math.Min(2, g.Duplas.Count)), entraram);
        }
    }

    [Fact]
    public void A_mesma_semente_reproduz_o_torneio_e_outra_semente_muda()
    {
        // Forças repetidas de propósito: o desempate da semeadura também tem que ser reproduzível.
        var duplas = Enumerable.Range(1, 12).Select(i => new DuplaParticipante($"D{i}", 40 + 10 * (i % 4))).ToList();
        string Rodar(uint semente)
        {
            var t = new TorneioDeDuplas(duplas, new RegrasDoTorneio(), semente);
            t.JogarAteOFim();
            return string.Join("\n", t.Jogos.Select(j => $"{j.Numero} {j.DuplaA} x {j.DuplaB} {Texto(j.Sets ?? [])}"));
        }
        Assert.Equal(Rodar(77), Rodar(77));
        Assert.NotEqual(Rodar(77), Rodar(78));

        // Com forças todas diferentes a chave é a mesma pra qualquer semente — aí só os placares
        // podem mudar, e têm que mudar: senão toda etapa com o mesmo sorteio repetiria o resultado.
        string Placares(uint semente)
        {
            var t = new TorneioDeDuplas(PorForca(12), new RegrasDoTorneio(), semente);
            return string.Join(" ", t.JogosDaRodadaAtual.Select(j => $"{j.DuplaA}x{j.DuplaB} {j.Resumo()}"));
        }
        string Chave(uint semente) =>
            string.Join(" ", new TorneioDeDuplas(PorForca(12), new RegrasDoTorneio(), semente).JogosDaRodadaAtual.Select(j => $"{j.DuplaA}x{j.DuplaB}"));
        Assert.Equal(Chave(77), Chave(78));
        Assert.NotEqual(Placares(77), Placares(78));
    }

    [Fact]
    public void A_dupla_mais_forte_vence_mais_em_200_torneios_simulados()
    {
        var duplas = new[] { 95, 85, 75, 65, 55, 45, 35, 25 }.Select(f => new DuplaParticipante($"F{f}", f)).ToList();
        var titulos = duplas.ToDictionary(d => d.Nome, _ => 0);
        for (uint s = 1; s <= 200; s++)
        {
            var formato = s % 2 == 0 ? FormatoDoTorneio.ChaveDireta : FormatoDoTorneio.GruposEMataMata;
            var t = new TorneioDeDuplas(duplas, new RegrasDoTorneio(formato), s);
            t.JogarAteOFim();
            Assert.NotNull(t.Campeao);
            titulos[t.Campeao.Nome]++;
        }
        var resumo = string.Join(", ", titulos.Select(kv => $"{kv.Key}: {kv.Value}"));
        Assert.All(titulos.Where(kv => kv.Key != "F95"), kv => Assert.True(titulos["F95"] > kv.Value, resumo));
        Assert.True(titulos["F25"] < titulos["F95"] / 4, resumo);
    }

    // ── A dupla humana: o resultado vem de fora ───────────────────────────────────────

    [Fact]
    public void A_partida_da_dupla_humana_espera_o_jogo_e_entra_pelo_placar_da_partida()
    {
        var duplas = new List<DuplaParticipante>
        {
            new("Nós", 50, humana: true), new("B", 60), new("C", 40), new("D", 70),
        };
        var t = new TorneioDeDuplas(duplas, new RegrasDoTorneio(FormatoDoTorneio.ChaveDireta), semente: 3);

        // Semeadura: D(70) 1º, B(60) 2º, Nós(50) 3º, C(40) 4º → D x C e B x Nós.
        var meu = Assert.Single(t.JogosPendentes);
        Assert.Equal("B", meu.DuplaA);
        Assert.Equal("Nós", meu.DuplaB);
        Assert.False(t.AvancarRodada());   // sem o resultado do humano, a rodada não fecha

        var naoAcabou = new Placar();
        naoAcabou.PontoPara(0);
        Assert.Throws<ArgumentException>(() => t.InformarResultado(meu.Numero, naoAcabou, duplaNoTime0: "Nós"));

        // O humano jogou de casa (time 0) e venceu 6-3: no jogo, a DuplaA é B, então o placar vira.
        t.InformarResultado(meu.Numero, PlacarVencidoPor(time: 0, gamesDoPerdedor: 3), duplaNoTime0: "Nós");
        var jogado = t.Jogos.Single(j => j.Numero == meu.Numero);
        Assert.Equal("Nós", jogado.Vencedor);
        Assert.NotNull(jogado.Sets);
        Assert.Equal([3, 6], jogado.Sets[0].Games);
        Assert.Throws<InvalidOperationException>(() => t.InformarResultado(meu.Numero, [S(6, 0)]));

        Assert.True(t.AvancarRodada());
        var final = Assert.Single(t.JogosPendentes);
        Assert.Contains("Nós", new[] { final.DuplaA, final.DuplaB });
        Assert.Equal(FaseAlcancada.Final, t.FaseDaDupla("Nós"));
        Assert.Equal(FaseAlcancada.Semifinal, t.FaseDaDupla("B"));

        // Perdeu a final jogando de visitante (time 1): quem venceu foi o time 0, o adversário.
        string adversario = final.DuplaA == "Nós" ? final.DuplaB : final.DuplaA;
        t.InformarResultado(final.Numero, PlacarVencidoPor(time: 0, gamesDoPerdedor: 4), duplaNoTime0: adversario);
        Assert.True(t.Encerrado);
        Assert.NotNull(t.Campeao);
        Assert.Equal(adversario, t.Campeao.Nome);
        Assert.Equal(FaseAlcancada.Final, t.FaseDaDupla("Nós"));
        Assert.False(t.AvancarRodada());
    }

    [Fact]
    public void Placar_impossivel_e_recusado_com_o_motivo()
    {
        var duplas = new List<DuplaParticipante> { new("Nós", 50, humana: true), new("B", 60) };
        var t = new TorneioDeDuplas(duplas, new RegrasDoTorneio(FormatoDoTorneio.ChaveDireta, SetsParaVencer: 2), semente: 1);
        int n = Assert.Single(t.JogosPendentes).Numero;

        IReadOnlyList<SetEncerrado>[] impossiveis =
        [
            [],
            [S(6, 4)],                              // melhor de 3: um set só não fecha
            [S(6, 5), S(6, 4)],                     // 6-5 não fecha set
            [S(8, 6), S(6, 4)],
            [S(7, 6), S(6, 4)],                     // 7-6 sem tie-break
            [TB(7, 6, 7, 6), S(6, 4)],              // tie-break sem 2 de vantagem
            [TB(7, 6, 5, 7), S(6, 4)],              // venceu o set e perdeu o tie-break
            [S(6, 4), S(6, 4), S(6, 4)],            // set depois da partida decidida
            [new SetEncerrado([6, 4], [7, 5]), S(6, 4)], // tie-break num set que não foi a 6-6
        ];
        foreach (var sets in impossiveis)
        {
            var erro = Assert.Throws<ArgumentException>(() => t.InformarResultado(n, sets));
            Assert.False(string.IsNullOrWhiteSpace(erro.Message));
        }
        Assert.False(t.Jogos.Single(j => j.Numero == n).Jogado);

        // Do ponto de vista da DuplaA, que é B (a cabeça 1): perdeu o 1º no tie-break, venceu o 2º, perdeu o 3º.
        Assert.Equal("B", t.Jogos.Single(j => j.Numero == n).DuplaA);
        t.InformarResultado(n, [TB(6, 7, 10, 12), S(6, 2), S(5, 7)]);
        Assert.Equal("Nós", t.Jogos.Single(j => j.Numero == n).Vencedor);
    }

    // ── Carreira ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Pontos_por_fase_seguem_a_escala_do_circuito_profissional()
    {
        Assert.Equal(2000, EscalaDePontos.Por(CategoriaDaEtapa.Major, FaseAlcancada.Campeao));
        Assert.Equal(1200, EscalaDePontos.Por(CategoriaDaEtapa.Major, FaseAlcancada.Final));
        Assert.Equal(360, EscalaDePontos.Por(CategoriaDaEtapa.P1, FaseAlcancada.Semifinal));
        Assert.Equal(600, EscalaDePontos.Por(CategoriaDaEtapa.P2, FaseAlcancada.Campeao));

        var fases = Enum.GetValues<FaseAlcancada>();
        var categorias = new[] { CategoriaDaEtapa.P2, CategoriaDaEtapa.P1, CategoriaDaEtapa.Major };
        foreach (var c in categorias)
            for (int i = 1; i < fases.Length; i++)
                Assert.True(EscalaDePontos.Por(c, fases[i]) > EscalaDePontos.Por(c, fases[i - 1]), $"{c}: {fases[i]}");
        foreach (var f in fases)
            for (int i = 1; i < categorias.Length; i++)
                Assert.True(EscalaDePontos.Por(categorias[i], f) > EscalaDePontos.Por(categorias[i - 1], f), $"{f}: {categorias[i]}");
    }

    [Fact]
    public void O_circuito_padrao_tem_6_etapas_de_forca_crescente_e_a_mesma_semente_gera_o_mesmo_circuito()
    {
        var c = Carreira.Nova(new DuplaParticipante("Nós", 60, humana: true), semente: 3);
        Assert.Equal(6, c.Etapas.Count);
        double anterior = double.MinValue;
        for (int i = 0; i < c.Etapas.Count; i++)
        {
            var participantes = c.ParticipantesDaEtapa(i);
            Assert.Equal(c.Etapas[i].Duplas, participantes.Count);
            Assert.Contains(c.DuplaDoJogador, participantes);
            Assert.Equal(participantes.Count, participantes.Select(d => d.Nome).Distinct().Count());
            double media = participantes.Where(d => !d.Humana).Average(d => d.Forca);
            Assert.True(media > anterior, $"etapa {i + 1}: força média {media:F1} não cresceu");
            anterior = media;
        }
        Assert.Equal(c.Salvar(), Carreira.Nova(new DuplaParticipante("Nós", 60, humana: true), semente: 3).Salvar());
    }

    [Fact]
    public void A_carreira_soma_os_pontos_da_fase_alcancada_em_cada_etapa()
    {
        var c = Carreira.Nova(new DuplaParticipante("Nós", 70, humana: true), semente: 5);
        while (!c.Concluida)
        {
            var t = c.IniciarEtapa();
            Assert.Throws<InvalidOperationException>(() => c.IniciarEtapa());
            Assert.Throws<InvalidOperationException>(() => c.FecharEtapa());
            // O ranking do começo da etapa, de cada participante, entra no torneio — e é o que
            // desempata grupo lá dentro (o desempate em si: No_torneio_o_empate_de_tres_no_grupo…).
            var acumulado = c.PontosPorDupla();
            Assert.Equal(t.Duplas.Count, t.PontosDeRanking.Count);
            foreach (var d in t.Duplas) Assert.Equal(acumulado[d.Nome], t.PontosDeRanking[d.Nome]);

            JogarEtapaComHumano(t);
            var r = c.FecharEtapa();
            Assert.Equal(t.Duplas.Count, r.Pontuacoes.Count);
            foreach (var p in r.Pontuacoes)
            {
                Assert.Equal(FaseLidaDosJogos(t, p.Dupla), p.Fase);
                Assert.Equal(EscalaDePontos.Por(r.Categoria, p.Fase), p.Pontos);
            }
            Assert.NotNull(t.Campeao);
            Assert.Equal(t.Campeao.Nome, r.Campeao);
        }

        Assert.Equal(6, c.Resultados.Count);
        Assert.Null(c.EtapaEmAndamento);
        Assert.Throws<InvalidOperationException>(() => c.IniciarEtapa());

        var ranking = c.Ranking();
        foreach (var linha in ranking)
        {
            Assert.Equal(c.Resultados.SelectMany(r => r.Pontuacoes).Where(p => p.Dupla == linha.Dupla).Sum(p => p.Pontos), linha.Pontos);
            Assert.Equal(c.Resultados.Count(r => r.Campeao == linha.Dupla), linha.Titulos);
        }
        Assert.Equal(Enumerable.Range(1, ranking.Count), ranking.Select(l => l.Posicao));
        Assert.True(ranking.Zip(ranking.Skip(1)).All(par => par.First.Pontos >= par.Second.Pontos));
        Assert.Contains(ranking, l => l.Dupla == "Nós");
    }

    [Fact]
    public void Quem_cai_nos_grupos_fica_na_fase_de_grupos_e_leva_o_degrau_mais_baixo_da_escala()
    {
        // O degrau "caiu nos grupos" é NOSSO (o Premier Padel não tem grupo) — ver EscalaDePontos.
        Assert.Equal(
            [11, 22, 35],
            new[] { CategoriaDaEtapa.P2, CategoriaDaEtapa.P1, CategoriaDaEtapa.Major }
                .Select(c => EscalaDePontos.Por(c, FaseAlcancada.FaseDeGrupos)));

        // A primeira etapa do circuito padrão: P2, grupos e mata-mata, 8 duplas → grupos de 2, 3 e 3.
        // O 3º de cada grupo de 3 não entra no quadro; é ele que fica na fase de grupos, com 11.
        var c = Carreira.Nova(new DuplaParticipante("Nós", 70, humana: true), semente: 5);
        var t = c.IniciarEtapa();
        JogarEtapaComHumano(t);
        var r = c.FecharEtapa();
        Assert.Equal(CategoriaDaEtapa.P2, r.Categoria);

        var terceiros = t.Grupos.Where(g => g.Duplas.Count == 3).Select(g => t.Classificacao(g.Nome)[2].Dupla).ToList();
        Assert.Equal(2, terceiros.Count);
        Assert.Equal(terceiros.Order(StringComparer.Ordinal),
            r.Pontuacoes.Where(p => p.Fase == FaseAlcancada.FaseDeGrupos).Select(p => p.Dupla).Order(StringComparer.Ordinal));
        foreach (var nome in terceiros)
        {
            Assert.DoesNotContain(nome, t.Quadro);
            Assert.Equal(FaseAlcancada.FaseDeGrupos, t.FaseDaDupla(nome));
            Assert.Equal(11, r.Pontuacoes.Single(p => p.Dupla == nome).Pontos);
        }
        // Quem passou do grupo pontua mais que quem caiu nele — até quem perdeu logo na estreia.
        Assert.All(r.Pontuacoes.Where(p => !terceiros.Contains(p.Dupla)), p => Assert.True(p.Pontos > 11, $"{p.Dupla}: {p.Fase} {p.Pontos}"));
    }

    [Fact]
    public void A_carreira_salva_e_carregada_e_igual_entre_etapas_e_no_meio_de_uma()
    {
        var c = Carreira.Nova(new DuplaParticipante("Nós", 70, humana: true), semente: 21);
        Assert.Equal(c.Salvar(), Carreira.Carregar(c.Salvar()).Salvar());

        JogarEtapaComHumano(c.IniciarEtapa());
        c.FecharEtapa();
        var entreEtapas = Carreira.Carregar(c.Salvar());
        Assert.Equal(c.Salvar(), entreEtapas.Salvar());
        Assert.Equal(c.Ranking(), entreEtapas.Ranking());

        // No meio da etapa 2 (chave direta, rodada 1): a rodada não fecha sem o jogo do humano;
        // informado o resultado, salva com a rodada ainda aberta. A etapa de GRUPOS salva em cada
        // rodada está em A_carreira_salva_em_qualquer_rodada_da_etapa_continua_igual….
        var t = c.IniciarEtapa();
        Assert.False(t.AvancarRodada());
        InformarPendentes(t);
        string json = c.Salvar();
        var c2 = Carreira.Carregar(json);
        Assert.Equal(json, c2.Salvar());
        Assert.Equal(c.Ranking(), c2.Ranking());
        Assert.Equal(c.IndiceDaProximaEtapa, c2.IndiceDaProximaEtapa);

        // E as duas continuam iguais até o fim da etapa.
        Assert.NotNull(c.EtapaEmAndamento);
        Assert.NotNull(c2.EtapaEmAndamento);
        Assert.Equal(c.EtapaEmAndamento.RodadaAtual, c2.EtapaEmAndamento.RodadaAtual);
        JogarEtapaComHumano(c.EtapaEmAndamento);
        JogarEtapaComHumano(c2.EtapaEmAndamento);
        var r1 = c.FecharEtapa();
        var r2 = c2.FecharEtapa();
        Assert.Equal(r1.Campeao, r2.Campeao);
        Assert.Equal(r1.Pontuacoes, r2.Pontuacoes);
        Assert.Equal(c.Salvar(), c2.Salvar());
    }

    [Theory]
    [InlineData(FormatoDoTorneio.GruposEMataMata, 8)]    // grupos de 2, 3 e 3; mata-mata de 6 com 2 byes
    [InlineData(FormatoDoTorneio.GruposEMataMata, 12)]   // 4 grupos de 3; mata-mata de 8
    [InlineData(FormatoDoTorneio.ChaveDireta, 12)]       // 4 byes na primeira rodada
    public void A_carreira_salva_em_qualquer_rodada_da_etapa_continua_igual_a_que_nao_parou(FormatoDoTorneio formato, int n)
    {
        // O arquivo guarda os jogos, não as contas feitas deles: quantas rodadas de grupo houve e
        // qual jogo ocupa qual vaga da árvore são refeitos ao carregar. É no meio dos grupos e no
        // meio do mata-mata de uma etapa de grupos que essas contas pesam — então a foto é tirada
        // em TODA rodada, antes e depois do resultado do humano, e cada foto é jogada até o fim.
        EtapaDoCircuito[] circuito = [new("Aberto de Teste", CategoriaDaEtapa.P1, n, formato)];
        var c = Carreira.Nova(new DuplaParticipante("Nós", 90, humana: true), semente: 4, circuito);
        var t = c.IniciarEtapa();

        var fotos = new List<(string Json, bool NosGrupos, int Rodada)>();
        for (int guarda = 0; !t.Encerrado; guarda++)
        {
            Assert.True(guarda < 50, "a etapa não termina");
            fotos.Add((c.Salvar(), t.EmFaseDeGrupos, t.RodadaAtual));
            InformarPendentes(t);
            if (!t.Encerrado) fotos.Add((c.Salvar(), t.EmFaseDeGrupos, t.RodadaAtual));
            t.AvancarRodada();
        }
        string encerradaSemParar = c.Salvar();
        c.FecharEtapa();
        string fechadaSemParar = c.Salvar();

        // As fotos cobrem o que o teste promete.
        int ultimaRodada = t.Jogos.Max(j => j.Rodada);
        Assert.Contains(fotos, f => !f.NosGrupos && f.Rodada < ultimaRodada);   // mata-mata com rodada pela frente
        if (formato == FormatoDoTorneio.GruposEMataMata)
        {
            int rodadasDeGrupo = t.Jogos.Where(j => j.Grupo is not null).Max(j => j.Rodada) + 1;
            Assert.True(rodadasDeGrupo > 1);
            Assert.Contains(fotos, f => f.NosGrupos && f.Rodada < rodadasDeGrupo - 1);   // grupo com rodada pela frente
            Assert.Contains(fotos, f => f.NosGrupos && f.Rodada == rodadasDeGrupo - 1);  // a rodada que fecha os grupos
        }

        foreach (var (json, nosGrupos, rodada) in fotos)
        {
            var retomada = Carreira.Carregar(json);
            Assert.Equal(json, retomada.Salvar());
            var etapa = retomada.EtapaEmAndamento;
            Assert.NotNull(etapa);
            Assert.Equal((nosGrupos, rodada), (etapa.EmFaseDeGrupos, etapa.RodadaAtual));

            JogarEtapaComHumano(etapa);
            Assert.True(encerradaSemParar == retomada.Salvar(),
                $"salva na rodada {rodada + 1} ({(nosGrupos ? "grupos" : "mata-mata")}): a continuação divergiu da etapa que não parou");
            retomada.FecharEtapa();
            Assert.Equal(fechadaSemParar, retomada.Salvar());
        }
    }

    [Fact]
    public void Arquivo_de_carreira_de_versao_desconhecida_e_recusado_com_erro_claro()
    {
        string json = Carreira.Nova(new DuplaParticipante("Nós", 70, humana: true), semente: 1).Salvar();
        var raiz = JsonNode.Parse(json) as JsonObject;
        Assert.NotNull(raiz);
        Assert.Equal(Carreira.VersaoDoArquivo, (int?)raiz["Versao"]);

        raiz["Versao"] = 99;
        var erro = Assert.Throws<InvalidDataException>(() => Carreira.Carregar(raiz.ToJsonString()));
        Assert.Contains("99", erro.Message);
        Assert.Contains($"{Carreira.VersaoDoArquivo}", erro.Message);
        Assert.Contains("versão", erro.Message);

        raiz.Remove("Versao");
        erro = Assert.Throws<InvalidDataException>(() => Carreira.Carregar(raiz.ToJsonString()));
        Assert.Contains("Versao", erro.Message);

        erro = Assert.Throws<InvalidDataException>(() => Carreira.Carregar("{ isto não é json"));
        Assert.Contains("JSON", erro.Message);

        raiz["Versao"] = Carreira.VersaoDoArquivo;
        raiz["Rivais"] = null;
        Assert.Throws<InvalidDataException>(() => Carreira.Carregar(raiz.ToJsonString()));
    }
}
