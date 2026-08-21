using Padelizou.Models;
using Padelizou.Services;

// O que estes testes protegem: a conta entre VAGAS e NOMES. A grade abre uma vaga por
// quadra em cada horário; se sobrar vaga sem nome, o jogo nasce com hora e sem quadra.
public class NomesDeQuadraTests
{
    [Fact]
    public void SemJogosMarcados_UsaOCadastro()
    {
        var lista = NomesDeQuadra.Disponiveis(
            Array.Empty<string>(), new[] { "Quadra 1", "Quadra 2" }, 2);

        Assert.Equal(new[] { "Quadra 1", "Quadra 2" }, lista);
    }

    [Fact]
    public void QuadraAcrescentadaDepoisDoSorteio_EntraPraCompletarAGrade()
    {
        // O torneio passou a ter 5 quadras, mas os jogos só conhecem 4 nomes.
        var lista = NomesDeQuadra.Disponiveis(
            new[] { "Quadra 1", "Quadra 2", "Quadra 3", "Quadra 4" },
            new[] { "Quadra 1", "Quadra 2", "Quadra 3", "Quadra 4", "Quadra 5" },
            quantidadeQuadras: 5);

        Assert.Equal(5, lista.Count);
        Assert.Contains("Quadra 5", lista);
        // As antigas vêm primeiro: quadra nova só é usada quando as outras encheram.
        Assert.Equal("Quadra 5", lista[^1]);
    }

    [Fact]
    public void QuadrasRenomeadasDepoisDoSorteio_NaoDuplicamNaLista()
    {
        // Os jogos guardam os nomes do dia do sorteio; o cadastro foi renomeado depois.
        // Já existe nome pra toda vaga, então o cadastro não entra — senão a mesma quadra
        // apareceria com dois nomes e metade dos jogadores iria pro lugar errado.
        var lista = NomesDeQuadra.Disponiveis(
            new[] { "Quadra A", "Quadra B", "Quadra C", "Quadra D", "Quadra E" },
            new[] { "Quadra 1", "Quadra 2", "Quadra 3", "Quadra 4", "Quadra 5" },
            quantidadeQuadras: 5);

        Assert.Equal(5, lista.Count);
        Assert.All(lista, n => Assert.StartsWith("Quadra ", n));
        Assert.DoesNotContain("Quadra 1", lista);
    }

    // O teste que reproduz o sintoma: com menos nome do que vaga, o Encaixar deixa um jogo
    // com horário e sem quadra. É o que pôs uma semifinal ao vivo sem lugar em 05/08/2026.
    [Fact]
    public void NomeParaCadaVaga_NenhumJogoFicaSemQuadra()
    {
        var inicio = new DateTime(2026, 8, 5, 20, 0, 0);
        var horarios = GradeDeJogos.Horarios(inicio, new TimeSpan(23, 50, 0), 5, 50, 5).ToList();

        var nomes = NomesDeQuadra.Disponiveis(
            new[] { "Quadra 1", "Quadra 2", "Quadra 3", "Quadra 4" },
            new[] { "Quadra 1", "Quadra 2", "Quadra 3", "Quadra 4", "Quadra 5" },
            quantidadeQuadras: 5);

        var jogos = Enumerable.Range(1, 5)
            .Select(i => new Partida { Dupla1Id = i * 10, Dupla2Id = i * 10 + 1 })
            .ToList();

        GradeDeJogos.Encaixar(jogos, horarios, 50, null, nomes, null);

        Assert.All(jogos, j => Assert.False(string.IsNullOrEmpty(j.NomeQuadra)));
        Assert.Equal(5, jogos.Select(j => j.NomeQuadra).Distinct().Count());
    }
}
