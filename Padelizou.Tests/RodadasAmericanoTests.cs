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
    [InlineData(28)]
    [InlineData(32)]
    [InlineData(36)]   // 36 não tem tabela de whist embutida: exercita o caminho antigo
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

    private static Dictionary<(int, int), int> ContarConfrontos(List<List<RodadasAmericano.Confronto>> rodadas)
    {
        var conta = new Dictionary<(int, int), int>();
        foreach (var c in rodadas.SelectMany(r => r))
            foreach (var x in new[] { c.A1, c.A2 })
                foreach (var y in new[] { c.B1, c.B2 })
                {
                    var k = Par(x, y);
                    conta[k] = conta.GetValueOrDefault(k) + 1;
                }
        return conta;
    }

    [Theory]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(24)]
    [InlineData(28)]
    [InlineData(32)]
    public void Cada_jogador_enfrenta_cada_um_dos_outros_exatamente_duas_vezes(int n)
    {
        // Este teste já foi mais humilde: até 28/07 ele aceitava que com 8 jogadores alguém
        // enfrentasse o mesmo rival em 4 das 7 rodadas, e anotava que o caminho pra melhorar
        // era um "whist design". O caminho foi feito em 29/07: as tabelas de whist foram
        // ENCONTRADAS por busca (fora do sistema) e estão embutidas no serviço como dado.
        //
        // "Exatamente 2" não é meta, é o único número possível num torneio justo: cada
        // rodada te dá 2 adversários, são n-1 rodadas e n-1 rivais — 2(n-1) encontros pra
        // n-1 pessoas fecha só com 2 pra cada. Qualquer 3 força um 1 em outro lugar.
        //
        // De quebra este teste confere a INTEGRIDADE das tabelas embutidas: um dígito errado
        // numa base cíclica quebra a conta pra alguma dupla, e ele acusa.
        var conta = ContarConfrontos(RodadasAmericano.Montar(Jogadores(n), new Random(7)));

        Assert.Equal(n * (n - 1) / 2, conta.Count);          // todo mundo cruza com todo mundo
        Assert.All(conta, p => Assert.Equal(2, p.Value));    // e exatamente 2 vezes
    }

    [Fact]
    public void Sorteios_diferentes_dao_tabelas_diferentes()
    {
        // O desenho do whist é fixo; o sorteio decide quem veste qual número. Dois torneios
        // com os mesmos inscritos não podem sair iguais — senão o "sorteio" da tela é teatro.
        var a = RodadasAmericano.Montar(Jogadores(8), new Random(1));
        var b = RodadasAmericano.Montar(Jogadores(8), new Random(2));

        var textoA = string.Join("|", a.SelectMany(r => r).Select(c => $"{c.A1},{c.A2}x{c.B1},{c.B2}"));
        var textoB = string.Join("|", b.SelectMany(r => r).Select(c => $"{c.A1},{c.A2}x{c.B1},{c.B2}"));
        Assert.NotEqual(textoA, textoB);
    }

    [Fact]
    public void Tamanho_sem_tabela_embutida_continua_com_parceiros_perfeitos_e_rivais_controlados()
    {
        // 36 jogadores não tem tabela de whist embutida (e um Americano desse tamanho é
        // teórico) — cai no método antigo. Parceiro segue perfeito por construção; pros
        // adversários o compromisso documentado é "ninguém preso no mesmo rival o torneio
        // inteiro": o pior caso tem que ficar bem abaixo das 35 rodadas.
        var rodadas = RodadasAmericano.Montar(Jogadores(36), new Random(7));
        var conta = ContarConfrontos(rodadas);

        Assert.Equal(35, rodadas.Count);
        Assert.True(conta.Values.Max() <= 6,
            $"alguém enfrentou o mesmo rival {conta.Values.Max()} vezes em 35 rodadas");
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
