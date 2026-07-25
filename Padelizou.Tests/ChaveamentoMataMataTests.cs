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

    [Fact]
    public void Tres_grupos_montam_semifinal_com_o_melhor_segundo()
    {
        // 1ºs: A(2v), B(2v), C(2v). 2ºs: A(1v,+3) é o melhor; B(1v,+1); C(0v).
        var classificados = new List<Classificado>
        {
            C(1, "Grupo A", 1, 2, 6), C(2, "Grupo A", 2, 1, 3),
            C(3, "Grupo B", 1, 2, 5), C(4, "Grupo B", 2, 1, 1),
            C(5, "Grupo C", 1, 2, 4), C(6, "Grupo C", 2, 0, -2),
        };

        var (fase, confrontos) = MontarPrimeiraFase(classificados);

        Assert.Equal("Semifinal", fase);
        Assert.Equal(2, confrontos.Count);
        var ids = confrontos.SelectMany(c => new[] { c.Dupla1Id, c.Dupla2Id }).ToHashSet();
        Assert.Equal(new HashSet<int> { 1, 3, 5, 2 }, ids); // 3 primeiros + o MELHOR 2º (dupla 2)
    }

    [Fact]
    public void Cinco_grupos_montam_quartas_com_tres_melhores_segundos()
    {
        var classificados = new List<Classificado>();
        for (int g = 0; g < 5; g++)
        {
            classificados.Add(C(g * 2 + 1, $"Grupo {(char)('A' + g)}", 1, 2, 5 - g));
            classificados.Add(C(g * 2 + 2, $"Grupo {(char)('A' + g)}", 2, 1, 5 - g)); // 2ºs: A>B>C>D>E
        }

        var (fase, confrontos) = MontarPrimeiraFase(classificados);

        Assert.Equal("Quartas de Final", fase);
        Assert.Equal(4, confrontos.Count);
        var ids = confrontos.SelectMany(c => new[] { c.Dupla1Id, c.Dupla2Id }).ToHashSet();
        Assert.Equal(8, ids.Count);
        // Todos os 5 primeiros dentro; os 2ºs de D e E (piores) fora.
        Assert.All(new[] { 1, 3, 5, 7, 9 }, id => Assert.Contains(id, ids));
        Assert.DoesNotContain(8, ids);
        Assert.DoesNotContain(10, ids);
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

        var (_, confrontos) = MontarPrimeiraFase(classificados);

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
        var (fase, confrontos) = MontarPrimeiraFase(new List<Classificado>
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
}
