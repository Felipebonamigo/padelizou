using Padelizou.Services;
using static Padelizou.Services.ChaveamentoMataMata;

namespace Padelizou.Tests;

// Lógica pura do motor de chaveamento (quadro, fases, melhores segundos, semeadura).
public class ChaveamentoMataMataTests
{
    private static Classificado C(int duplaId, string grupo, int pos, int vitorias = 0, int saldo = 0)
        => new(duplaId, grupo, vitorias, saldo, pos);

    [Theory]
    [InlineData(16, "Oitavas de Final")]
    [InlineData(8, "Quartas de Final")]
    [InlineData(4, "Semifinal")]
    [InlineData(2, "Final")]
    public void Nome_da_fase_pelo_tamanho_do_quadro(int quadro, string nome)
        => Assert.Equal(nome, NomeFase(quadro));

    [Fact]
    public void Encadeamento_das_fases()
    {
        Assert.Equal("Quartas de Final", ProximaFase("Oitavas de Final"));
        Assert.Equal("Semifinal", ProximaFase("Quartas de Final"));
        Assert.Equal("Final", ProximaFase("Semifinal"));
        Assert.Null(ProximaFase("Final"));
        Assert.Null(ProximaFase("Grupo A"));
        Assert.Null(ProximaFase("Americano Rodada 1"));
        Assert.Null(ProximaFase(null));
    }

    [Theory]
    [InlineData(2, 2)]   // 1 grupo → sem cruzamento (tratado à parte)
    [InlineData(4, 4)]
    [InlineData(6, 4)]   // 3 grupos → quadro de 4
    [InlineData(10, 8)]  // 5 grupos → quadro de 8
    [InlineData(12, 8)]  // 6 grupos → quadro de 8
    [InlineData(16, 16)] // 8 grupos → quadro de 16
    public void Quadro_e_a_maior_potencia_de_2_que_cabe(int classificados, int quadroEsperado)
        => Assert.Equal(quadroEsperado, MaiorPotenciaDe2Ate(classificados));

    // A REGRA (decisão do Felipe, 05/08/2026): todo classificado avança. O quadro cresce
    // pra caber todo mundo, e as vagas que sobram viram BYE dos melhores — quem fez a
    // melhor campanha pula a primeira rodada, os piores ranqueados jogam uma etapa a mais.
    // A régua antiga cortava os piores 2ºs sem mata-mata nenhum.
    [Fact]
    public void Tres_grupos_ninguem_e_cortado_e_os_dois_melhores_descansam()
    {
        // 1ºs: A(2v,+6), B(2v,+5), C(2v,+4). 2ºs: A é o melhor; C o pior.
        var classificados = new List<Classificado>
        {
            C(1, "Grupo A", 1, 2, 6), C(2, "Grupo A", 2, 1, 3),
            C(3, "Grupo B", 1, 2, 5), C(4, "Grupo B", 2, 1, 1),
            C(5, "Grupo C", 1, 2, 4), C(6, "Grupo C", 2, 0, -2),
        };

        var (fase, confrontos, byes) = MontarPrimeiraFase(classificados);

        // 6 classificados → quadro de 8 (Quartas): 2 jogos + 2 byes. NINGUÉM cortado.
        Assert.Equal("Quartas de Final", fase);
        Assert.Equal(2, confrontos.Count);

        // O bye é dos dois MELHORES 1ºs (duplas 1 e 3) — o Grupo A é a maior cabeça de chave.
        Assert.Equal(new[] { 1, 3 }, byes);

        // E toda dupla classificada aparece exatamente uma vez: ou joga, ou descansa.
        var citados = confrontos.SelectMany(c => new[] { c.Dupla1Id, c.Dupla2Id }).Concat(byes).ToList();
        Assert.Equal(6, citados.Count);
        Assert.Equal(6, citados.Distinct().Count());
    }

    [Fact]
    public void Cinco_grupos_todos_avancam_com_seis_byes()
    {
        var classificados = new List<Classificado>();
        for (int g = 0; g < 5; g++)
        {
            classificados.Add(C(g * 2 + 1, $"Grupo {(char)('A' + g)}", 1, 2, 5 - g));
            classificados.Add(C(g * 2 + 2, $"Grupo {(char)('A' + g)}", 2, 1, 5 - g)); // 2ºs: A>B>C>D>E
        }

        var (fase, confrontos, byes) = MontarPrimeiraFase(classificados);

        // 10 classificados → quadro de 16 (Oitavas): 6 byes e 2 jogos. Antes os 2ºs de D e E
        // eram eliminados sem jogar; agora jogam a rodada extra.
        Assert.Equal("Oitavas de Final", fase);
        Assert.Equal(2, confrontos.Count);
        Assert.Equal(6, byes.Count);
        Assert.Equal(new[] { 1, 3, 5, 7, 9, 2 }, byes);   // os 5 primeiros + o melhor 2º

        var citados = confrontos.SelectMany(c => new[] { c.Dupla1Id, c.Dupla2Id }).Concat(byes).ToList();
        Assert.Equal(10, citados.Distinct().Count());
    }

    [Fact]
    public void Primeira_fase_evita_confronto_do_mesmo_grupo_quando_possivel()
    {
        // 2 grupos → semifinal. Sem a troca, o 1º do A poderia pegar o 2º do A.
        var classificados = new List<Classificado>
        {
            C(1, "Grupo A", 1, 2, 9), C(2, "Grupo A", 2, 1, 8),
            C(3, "Grupo B", 1, 2, 1), C(4, "Grupo B", 2, 1, 0),
        };

        var (_, confrontos, _) = MontarPrimeiraFase(classificados);

        Assert.Equal(2, confrontos.Count);
        foreach (var c in confrontos)
        {
            var grupo1 = classificados.First(x => x.DuplaId == c.Dupla1Id).Grupo;
            var grupo2 = classificados.First(x => x.DuplaId == c.Dupla2Id).Grupo;
            Assert.NotEqual(grupo1, grupo2);
        }
    }

    [Fact]
    public void Um_grupo_gera_final_direta_entre_1o_e_2o()
    {
        var (fase, confrontos, _) = MontarPrimeiraFase(new List<Classificado>
        {
            C(1, "Grupo A", 1, 2, 4), C(2, "Grupo A", 2, 1, 0),
        });

        Assert.Equal("Final", fase);
        var confronto = Assert.Single(confrontos);
        Assert.Equal(1, confronto.Dupla1Id);
        Assert.Equal(2, confronto.Dupla2Id);
    }

    [Fact]
    public void Parear_vencedores_cruza_primeiro_com_ultimo()
    {
        var confrontos = ParearVencedores(new[] { 10, 20, 30, 40 });
        Assert.Equal(2, confrontos.Count);
        Assert.Equal((10, 40), (confrontos[0].Dupla1Id, confrontos[0].Dupla2Id));
        Assert.Equal((20, 30), (confrontos[1].Dupla1Id, confrontos[1].Dupla2Id));
    }

    // ---- Classificados por grupo configurável (categoria de TIMES) ----
    // A régua histórica dos "melhores 2ºs" generalizada: o organizador decide quantos
    // passam, e a mesma comparação de campanha vale pra 3ºs e 4ºs.

    [Fact]
    public void Com_um_classificado_por_grupo_so_os_campeoes_avancam()
    {
        // 4 grupos, 1 por grupo → semifinal só com os 1ºs; nenhum 2º entra.
        var classificados = new List<Classificado>();
        for (int g = 0; g < 4; g++)
        {
            classificados.Add(C(g * 2 + 1, $"Grupo {(char)('A' + g)}", 1, 2, 4 - g));
            classificados.Add(C(g * 2 + 2, $"Grupo {(char)('A' + g)}", 2, 1, 4 - g));
        }

        var (fase, confrontos, _) = MontarPrimeiraFase(classificados, classificadosPorGrupo: 1);

        Assert.Equal("Semifinal", fase);
        var ids = confrontos.SelectMany(c => new[] { c.Dupla1Id, c.Dupla2Id }).ToHashSet();
        Assert.Equal(new HashSet<int> { 1, 3, 5, 7 }, ids);
    }

    [Fact]
    public void Com_quatro_classificados_por_grupo_o_quadro_vira_oitavas()
    {
        // 4 grupos de 4, todos passam → 16 no quadro.
        var classificados = new List<Classificado>();
        int id = 1;
        for (int g = 0; g < 4; g++)
            for (int pos = 1; pos <= 4; pos++)
                classificados.Add(C(id++, $"Grupo {(char)('A' + g)}", pos, 4 - pos, 4 - pos));

        var (fase, confrontos, _) = MontarPrimeiraFase(classificados, classificadosPorGrupo: 4);

        Assert.Equal("Oitavas de Final", fase);
        Assert.Equal(8, confrontos.Count);
        Assert.Equal(16, confrontos.SelectMany(c => new[] { c.Dupla1Id, c.Dupla2Id }).Distinct().Count());
    }

    [Fact]
    public void Um_grupo_unico_com_quatro_classificados_gera_semifinal()
    {
        var classificados = new List<Classificado>
        {
            C(1, "Grupo A", 1, 3, 9), C(2, "Grupo A", 2, 2, 4),
            C(3, "Grupo A", 3, 1, -2), C(4, "Grupo A", 4, 0, -11),
        };

        var (fase, confrontos, _) = MontarPrimeiraFase(classificados, classificadosPorGrupo: 4);

        Assert.Equal("Semifinal", fase);
        Assert.Equal(2, confrontos.Count);
        // Semeadura 1º x último: 1x4 e 2x3.
        Assert.Equal((1, 4), (confrontos[0].Dupla1Id, confrontos[0].Dupla2Id));
        Assert.Equal((2, 3), (confrontos[1].Dupla1Id, confrontos[1].Dupla2Id));
    }

    [Fact]
    public void Conta_que_nao_fecha_ninguem_e_cortado_e_o_bye_e_dos_primeiros()
    {
        // Configuração torta (3 grupos × 2 = 6, não é potência de 2): o quadro sobe pra 8 e
        // TODOS avançam — dois 1ºs descansam, ninguém vai embora sem jogar mata-mata.
        var classificados = new List<Classificado>
        {
            C(1, "Grupo A", 1, 2, 1), C(2, "Grupo A", 2, 1, 9),
            C(3, "Grupo B", 1, 2, 2), C(4, "Grupo B", 2, 1, 8),
            C(5, "Grupo C", 1, 2, 3), C(6, "Grupo C", 2, 1, 7),
        };

        var (fase, confrontos, byes) = MontarPrimeiraFase(classificados, classificadosPorGrupo: 2);

        Assert.Equal("Quartas de Final", fase);
        Assert.Equal(new[] { 5, 3 }, byes);   // os dois 1ºs de melhor campanha (saldo 3 e 2)

        var todos = confrontos.SelectMany(c => new[] { c.Dupla1Id, c.Dupla2Id }).Concat(byes).ToHashSet();
        Assert.Equal(new HashSet<int> { 1, 2, 3, 4, 5, 6 }, todos);
    }
}
