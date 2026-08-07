using Padelizou.Services;

namespace Padelizou.Tests;

// O Americano de QUALQUER tamanho, de 4 pra cima (Felipe, 06/08/2026).
//
// Até então o sorteio cortava pro múltiplo de 4 abaixo, e quem sobrava ficava de fora do
// torneio inteiro — 10 inscritos viravam 8 e duas pessoas que pagaram não jogavam nada. Pior:
// 5, 9, 13 e 17 fecham a conta perfeitamente e eram recusados à toa.
//
// A obrigação do formato: cada um joga com CADA UM dos outros. Onde a aritmética permite, sem
// nenhuma parceria repetida; onde não permite, com o mínimo possível.
public class AmericanoDeQualquerTamanhoTests
{
    private static List<int> Gente(int n) => Enumerable.Range(1, n).ToList();

    // Semente fixa: um teste de sorteio que muda de resposta a cada execução não serve de trava.
    //
    // E guardado: são 9 asserções varrendo 37 tamanhos, e sortear de novo em cada uma fazia o
    // arquivo sozinho levar quase dois minutos no CI. Com a semente fixa o resultado é o
    // mesmo, então reaproveitar é seguro.
    private static readonly Dictionary<(int, int), List<List<RodadasAmericano.Confronto>>> Guardados = new();
    private static readonly object Tranca = new();

    private static List<List<RodadasAmericano.Confronto>> Sortear(int n, int semente = 20260806)
    {
        lock (Tranca)
        {
            if (!Guardados.TryGetValue((n, semente), out var sorteio))
            {
                sorteio = RodadasAmericano.Montar(Gente(n), new Random(semente));
                Guardados[(n, semente)] = sorteio;
            }
            return sorteio;
        }
    }

    private static (int, int) Par(int a, int b) => a < b ? (a, b) : (b, a);

    private static Dictionary<(int, int), int> Parcerias(List<List<RodadasAmericano.Confronto>> rodadas)
    {
        var conta = new Dictionary<(int, int), int>();
        foreach (var c in rodadas.SelectMany(r => r))
            foreach (var p in new[] { Par(c.A1, c.A2), Par(c.B1, c.B2) })
                conta[p] = conta.GetValueOrDefault(p) + 1;
        return conta;
    }

    private static Dictionary<int, int> JogosPorPessoa(List<List<RodadasAmericano.Confronto>> rodadas)
    {
        var conta = new Dictionary<int, int>();
        foreach (var c in rodadas.SelectMany(r => r))
            foreach (var j in new[] { c.A1, c.A2, c.B1, c.B2 })
                conta[j] = conta.GetValueOrDefault(j) + 1;
        return conta;
    }

    // Todos os tamanhos de 4 a 40. É a varredura que pega o caso que ninguém pensou em testar.
    public static IEnumerable<object[]> Tamanhos() =>
        Enumerable.Range(4, 37).Select(n => new object[] { n });

    // ── A OBRIGAÇÃO DO FORMATO ────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Tamanhos))]
    public void Cada_um_joga_com_CADA_UM_dos_outros(int n)
    {
        var parcerias = Parcerias(Sortear(n));

        for (int a = 1; a <= n; a++)
        {
            for (int b = a + 1; b <= n; b++)
            {
                Assert.True(parcerias.ContainsKey((a, b)),
                    $"com {n} jogadores, {a} e {b} nunca jogaram juntos");
            }
        }
    }

    // ── NINGUÉM FICA DE FORA ──────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Tamanhos))]
    public void Todo_inscrito_entra_em_quadra(int n)
    {
        var jogos = JogosPorPessoa(Sortear(n));

        Assert.Equal(n, jogos.Count);
        Assert.All(Gente(n), j => Assert.True(jogos.GetValueOrDefault(j) > 0,
            $"com {n} jogadores, o jogador {j} não entrou em jogo nenhum"));
    }

    // O DEFEITO que originou tudo isto: 10 inscritos viravam 8.
    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(13)]
    public void Tamanho_que_antes_era_cortado_agora_joga_inteiro(int n)
    {
        Assert.Equal(n, RodadasAmericano.Aproveitaveis(n));
        Assert.Equal(n, JogosPorPessoa(Sortear(n)).Count);
    }

    // ── O DESCANSO REVEZA ─────────────────────────────────────────────────────────────────

    // Ninguém pode jogar muito mais que os outros: a classificação é por SOMA DE GAMES, então
    // jogar dois jogos a mais que o vizinho é vantagem que não se recupera.
    [Theory]
    [MemberData(nameof(Tamanhos))]
    public void O_descanso_reveza_parelho(int n)
    {
        var jogos = JogosPorPessoa(Sortear(n));
        int menos = jogos.Values.Min(), mais = jogos.Values.Max();

        Assert.True(mais - menos <= 1,
            $"com {n} jogadores, alguém jogou {mais} e alguém {menos} — diferença de {mais - menos}");
    }

    // ── AS QUADRAS FECHAM ─────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(Tamanhos))]
    public void Ninguem_joga_duas_vezes_na_mesma_rodada(int n)
    {
        foreach (var (rodada, i) in Sortear(n).Select((r, i) => (r, i)))
        {
            var pessoas = rodada.SelectMany(c => new[] { c.A1, c.A2, c.B1, c.B2 }).ToList();
            Assert.Equal(pessoas.Count, pessoas.Distinct().Count());
        }
    }

    [Theory]
    [MemberData(nameof(Tamanhos))]
    public void Toda_partida_tem_quatro_pessoas_diferentes(int n)
    {
        foreach (var c in Sortear(n).SelectMany(r => r))
        {
            Assert.Equal(4, new[] { c.A1, c.A2, c.B1, c.B2 }.Distinct().Count());
        }
    }

    // Cabe nas quadras: cada rodada usa no máximo o que o tamanho permite.
    [Theory]
    [MemberData(nameof(Tamanhos))]
    public void Cada_rodada_enche_as_quadras_possiveis(int n)
    {
        Assert.All(Sortear(n), rodada => Assert.Equal(n / 4, rodada.Count));
    }

    // ── ONDE A ARITMÉTICA PERMITE, É PERFEITO ─────────────────────────────────────────────

    // 4, 5, 8, 9, 12, 13, 16, 17, 20... — o número de duplas a formar é par, então dá pra
    // fechar sem NENHUMA parceria repetida.
    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(20)]
    public void Onde_fecha_certinho_ninguem_repete_parceiro(int n)
    {
        Assert.True(RodadasAmericano.FechaCertinho(n));
        Assert.All(Parcerias(Sortear(n)).Values, vezes => Assert.Equal(1, vezes));
    }

    // 6, 7, 10, 11, 14, 15... — com 6 pessoas são 15 duplas a formar e cada jogo forma 2:
    // daria 7,5 jogos. Não existe meio jogo, então alguém repete parceiro.
    //
    // O quanto repete NÃO é livre: as rodadas fecham quadras de 4, então o torneio gera
    // `rodadas × duplasPorRodada` parcerias, e tudo que passar das C(n,2) necessárias é
    // repetição forçada. Com 10 pessoas são 12 rodadas × 4 = 48 parcerias pra 45 duplas → 3
    // repetições, e não há como fazer menos.
    //
    // O teste exige EXATAMENTE esse mínimo: uma a mais seria o sorteio jogando fora uma
    // parceria que cabia — e é justamente esse desperdício que o guloso pode cometer.
    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(18)]
    [InlineData(19)]
    public void Onde_nao_fecha_a_repeticao_e_o_minimo_possivel(int n)
    {
        Assert.False(RodadasAmericano.FechaCertinho(n));

        int duplasAFormar = n * (n - 1) / 2;
        int duplasPorRodada = 2 * (n / 4);
        int minimoInevitavel = RodadasAmericano.Rodadas(n) * duplasPorRodada - duplasAFormar;

        int repetidas = Parcerias(Sortear(n)).Values.Where(v => v > 1).Sum(v => v - 1);

        Assert.Equal(minimoInevitavel, repetidas);
    }

    // ── ADVERSÁRIOS ───────────────────────────────────────────────────────────────────────

    // "O maior número possível de adversários diferentes, evitando que se enfrentem muitas
    // vezes". Ninguém pode ficar preso no mesmo rival a torneio inteiro.
    [Theory]
    [MemberData(nameof(Tamanhos))]
    public void Ninguem_fica_preso_no_mesmo_adversario(int n)
    {
        var conta = new Dictionary<(int, int), int>();
        foreach (var c in Sortear(n).SelectMany(r => r))
            foreach (var x in new[] { c.A1, c.A2 })
                foreach (var y in new[] { c.B1, c.B2 })
                {
                    var k = Par(x, y);
                    conta[k] = conta.GetValueOrDefault(k) + 1;
                }

        // Onde existe TABELA DE WHIST (múltiplo de 4 até 32) o número é exatamente 2, provado.
        // Acima de 32 o sorteio cai no método do círculo, que é bom e não perfeito — e nos
        // tamanhos com descanso a busca é gulosa. Nesses dois casos o teto é folgado, mas
        // existe: sem teto nenhum, "misturar adversários" não quer dizer nada.
        bool temTabelaPerfeita = n % 4 == 0 && n <= 32;
        int teto = temTabelaPerfeita ? 2 : 5;
        var pior = conta.Values.Max();
        Assert.True(pior <= teto, $"com {n} jogadores, alguém enfrentou o mesmo rival {pior} vezes");
    }

    // ── O SORTEIO É SORTEIO ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(13)]
    public void Dois_torneios_com_a_mesma_gente_nao_saem_iguais(int n)
    {
        var a = Sortear(n, semente: 1);
        var b = Sortear(n, semente: 2);

        var textoA = string.Join("|", a.SelectMany(r => r).Select(c => $"{c.A1},{c.A2}x{c.B1},{c.B2}"));
        var textoB = string.Join("|", b.SelectMany(r => r).Select(c => $"{c.A1},{c.A2}x{c.B1},{c.B2}"));
        Assert.NotEqual(textoA, textoB);
    }

    // ── LIMITES ───────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void Menos_de_quatro_nao_da_torneio(int n)
    {
        Assert.Empty(RodadasAmericano.Montar(Gente(n), new Random(1)));
        Assert.Equal(0, RodadasAmericano.Aproveitaveis(n));
    }

    // A previsão de rodadas mostrada ao organizador tem que bater com o que o sorteio faz —
    // é dela que sai a conta de quanto tempo o torneio vai durar.
    [Theory]
    [MemberData(nameof(Tamanhos))]
    public void A_previsao_de_rodadas_bate_com_o_sorteio(int n)
    {
        Assert.Equal(RodadasAmericano.Rodadas(n), Sortear(n).Count);
    }
}
