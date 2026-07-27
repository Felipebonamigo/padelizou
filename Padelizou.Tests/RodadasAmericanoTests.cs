using Padelizou.Services;

namespace Padelizou.Tests;

// A regra do Americano: cada jogador faz dupla com CADA um dos outros pelo menos uma vez.
// O sorteio antigo era guloso e só olhava 4 jogadores por vez — num ensaio real de 8
// jogadores, 4 parcerias saíram repetidas e 4 nunca aconteceram. Estes testes existem pra
// isso não voltar sem ninguém ver.
public class RodadasAmericanoTests
{
    private static List<int> Jogadores(int n) => Enumerable.Range(1, n).ToList();

    private static (int, int) Par(int a, int b) => a < b ? (a, b) : (b, a);

    private static Dictionary<(int, int), int> ContarParcerias(List<List<RodadasAmericano.Confronto>> rodadas)
    {
        var conta = new Dictionary<(int, int), int>();
        foreach (var rodada in rodadas)
            foreach (var c in rodada)
                foreach (var dupla in new[] { Par(c.A1, c.A2), Par(c.B1, c.B2) })
                    conta[dupla] = conta.GetValueOrDefault(dupla) + 1;
        return conta;
    }

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(24)]
    public void Cada_jogador_faz_dupla_com_cada_um_dos_outros_exatamente_uma_vez(int n)
    {
        var rodadas = RodadasAmericano.Montar(Jogadores(n), new Random(7));
        var parcerias = ContarParcerias(rodadas);

        // Todos os pares possíveis existem...
        int paresPossiveis = n * (n - 1) / 2;
        Assert.Equal(paresPossiveis, parcerias.Count);

        // ...e nenhum se repete. Repetir é injusto: quem calha de repetir o parceiro forte
        // soma games com vantagem.
        Assert.All(parcerias, p => Assert.Equal(1, p.Value));
    }

    [Fact]
    public void O_caso_que_falhou_no_ensaio_de_8_jogadores()
    {
        // Ensaio de 27/07/2026: 28 duplas geradas, só 24 pares distintos.
        var rodadas = RodadasAmericano.Montar(Jogadores(8), new Random(7));
        var parcerias = ContarParcerias(rodadas);

        Assert.Equal(7, rodadas.Count);                       // n-1 rodadas
        Assert.All(rodadas, r => Assert.Equal(2, r.Count));   // 8 jogadores = 2 quadras
        Assert.Equal(28, parcerias.Count);                    // as 28 parcerias possíveis
        Assert.DoesNotContain(parcerias, p => p.Value > 1);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(16)]
    public void Ninguem_joga_duas_vezes_na_mesma_rodada(int n)
    {
        var rodadas = RodadasAmericano.Montar(Jogadores(n), new Random(7));

        foreach (var rodada in rodadas)
        {
            var emQuadra = rodada.SelectMany(c => new[] { c.A1, c.A2, c.B1, c.B2 }).ToList();

            // Todo mundo entra em quadra, e ninguém duas vezes — as partidas da rodada são
            // simultâneas.
            Assert.Equal(n, emQuadra.Count);
            Assert.Equal(n, emQuadra.Distinct().Count());
        }
    }

    [Theory]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(16)]
    public void Ninguem_e_parceiro_de_si_mesmo_nem_adversario_de_si_mesmo(int n)
    {
        var rodadas = RodadasAmericano.Montar(Jogadores(n), new Random(7));

        foreach (var c in rodadas.SelectMany(r => r))
        {
            Assert.NotEqual(c.A1, c.A2);
            Assert.NotEqual(c.B1, c.B2);
            Assert.Equal(4, new[] { c.A1, c.A2, c.B1, c.B2 }.Distinct().Count());
        }
    }

    [Fact]
    public void Ninguem_fica_preso_enfrentando_sempre_o_mesmo_rival()
    {
        // O círculo garante o PARCEIRO; o adversário é o que sobra. Com 8 jogadores são 56
        // encontros pra 28 pares — o ideal seria 2 pra cada, e existe um desenho matemático
        // que consegue isso (whist tournament). O que temos aqui não chega lá: mesmo
        // testando 200 ordens diferentes, o pior caso fica em 4 dos 7 jogos contra o mesmo
        // rival. É aceitável (a regra que o organizador pediu é sobre parceiros, e essa
        // está perfeita), mas é o teto conhecido — se um dia virar reclamação, o caminho é
        // trocar o círculo por um whist design.
        var rodadas = RodadasAmericano.Montar(Jogadores(8), new Random(7));
        var conta = new Dictionary<(int, int), int>();

        foreach (var c in rodadas.SelectMany(r => r))
            foreach (var x in new[] { c.A1, c.A2 })
                foreach (var y in new[] { c.B1, c.B2 })
                {
                    var k = Par(x, y);
                    conta[k] = conta.GetValueOrDefault(k) + 1;
                }

        Assert.Equal(56, conta.Values.Sum());
        Assert.True(conta.Count >= 24, $"só {conta.Count} pares distintos se enfrentaram");
        Assert.True(conta.Values.Max() <= 4,
            $"alguém enfrentou o mesmo rival {conta.Values.Max()} vezes");
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 0)]
    [InlineData(5, 4)]
    [InlineData(7, 4)]
    [InlineData(10, 8)]
    [InlineData(13, 12)]
    public void Quem_nao_fecha_quadra_de_quatro_fica_de_fora(int inscritos, int esperado)
    {
        // Regra que já existia: o Americano joga 2 contra 2, então o total precisa fechar
        // em quadras de 4. A tela avisa quantos ficaram de fora.
        Assert.Equal(esperado, RodadasAmericano.Aproveitaveis(inscritos));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(10)]
    public void Numero_que_nao_fecha_quadra_nao_gera_rodada_nenhuma(int n)
    {
        // Montar recebe já a lista aproveitável; se vier torta, sai vazio em vez de gerar
        // rodada quebrada.
        Assert.Empty(RodadasAmericano.Montar(Jogadores(n), new Random(7)));
    }

    [Theory]
    [InlineData(4, 3)]
    [InlineData(8, 7)]
    [InlineData(12, 11)]
    public void Sao_sempre_n_menos_1_rodadas(int n, int rodadas)
    {
        Assert.Equal(rodadas, RodadasAmericano.Rodadas(n));
        Assert.Equal(rodadas, RodadasAmericano.Montar(Jogadores(n), new Random(7)).Count);
    }
}
