using Padelizou.Services;

namespace Padelizou.Tests;

// "A PRÓXIMA RODADA É DE QUEM FICOU MAIS TEMPO PARADA" (Felipe, 08/08/2026) — e vale só pro
// Americano, que é o formato em que o mesmo grupo joga várias rodadas seguidas.
//
// O sorteio monta um grupo inteiro de cada vez: as 5 rodadas do A, depois as 5 do B. Criando
// os jogos nessa ordem, o grupo B só entrava em quadra depois de o A ter terminado o torneio
// dele — e, como a grade de horários é posicional, as duas primeiras faixas iam pras rodadas 1
// e 2 DO MESMO grupo: as mesmas pessoas marcadas em duas quadras ao mesmo tempo.
public class FilaDoAmericanoTests
{
    private record Jogo(int Rodada, int Grupo, string Nome);

    private static List<string> Enfileirar(IEnumerable<Jogo> jogos) =>
        FilaDoAmericano.NaOrdemDeJogar(jogos, j => j.Rodada, j => j.Grupo)
            .Select(j => j.Nome).ToList();

    [Fact]
    public void A_rodada_1_de_todos_os_grupos_vem_antes_da_rodada_2_de_qualquer_um()
    {
        // Como o sorteio entrega: grupo A inteiro, depois grupo B inteiro.
        var comoVeioDoSorteio = new[]
        {
            new Jogo(1, 0, "A1"), new Jogo(2, 0, "A2"), new Jogo(3, 0, "A3"),
            new Jogo(1, 1, "B1"), new Jogo(2, 1, "B2"), new Jogo(3, 1, "B3"),
        };

        Assert.Equal(
            new[] { "A1", "B1", "A2", "B2", "A3", "B3" },
            Enfileirar(comoVeioDoSorteio));
    }

    [Fact]
    public void Nenhum_grupo_joga_duas_vezes_antes_de_o_outro_jogar_uma()
    {
        // A propriedade que interessa, escrita como propriedade e não como lista fixa: entre
        // duas rodadas de um mesmo grupo, todo grupo que ainda tem rodada pendente joga.
        var jogos = new List<Jogo>();
        for (int grupo = 0; grupo < 3; grupo++)
            for (int rodada = 1; rodada <= 4; rodada++)
                jogos.Add(new Jogo(rodada, grupo, $"G{grupo}R{rodada}"));

        var fila = FilaDoAmericano.NaOrdemDeJogar(jogos, j => j.Rodada, j => j.Grupo).ToList();

        var ultimaVezQueJogou = new Dictionary<int, int>();
        for (int i = 0; i < fila.Count; i++)
        {
            var grupo = fila[i].Grupo;
            if (ultimaVezQueJogou.TryGetValue(grupo, out var antes))
            {
                // Entre uma aparição e a próxima do MESMO grupo têm que caber os outros dois.
                var noMeio = fila.Skip(antes + 1).Take(i - antes - 1).Select(j => j.Grupo).Distinct();
                Assert.Equal(2, noMeio.Count());
            }
            ultimaVezQueJogou[grupo] = i;
        }
    }

    [Fact]
    public void Dentro_da_mesma_rodada_a_ordem_sorteada_das_quadras_e_preservada()
    {
        // Grupo grande joga mais de uma partida por rodada (uma por quadra). Essa ordem já saiu
        // sorteada em RodadasAmericano e não pode ser reembaralhada aqui.
        var doSorteio = new[]
        {
            new Jogo(1, 0, "quadra que saiu primeiro"),
            new Jogo(1, 0, "quadra que saiu depois"),
            new Jogo(2, 0, "rodada seguinte"),
        };

        Assert.Equal(
            new[] { "quadra que saiu primeiro", "quadra que saiu depois", "rodada seguinte" },
            Enfileirar(doSorteio));
    }

    [Fact]
    public void Grupo_unico_sai_exatamente_como_entrou()
    {
        // Sem divisão em grupos não há fila a intercalar — e o formato de duplas, que é sempre
        // grupo único, não pode ter a ordem mexida por isto.
        var jogos = Enumerable.Range(1, 6).Select(r => new Jogo(r, 0, $"R{r}")).ToArray();

        Assert.Equal(jogos.Select(j => j.Nome), Enfileirar(jogos));
    }
}
