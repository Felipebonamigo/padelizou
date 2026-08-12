using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A quadra preferida de uma categoria: o pedido é "a quadra boa é da 1ª categoria", e o que
// estes testes protegem são os três degraus da escolha (preferida → neutra → qualquer) e o
// preço que NÃO se aceita pagar por eles — quadra parada e jogo sem quadra.
public class QuadraPreferidaDaCategoriaTests
{
    private const int Primeira = 1;
    private const int Sexta = 2;

    private static readonly string[] Quadras = { "Central", "Quadra 2", "Quadra 3" };

    private static Dictionary<int, string[]> CentralDaPrimeira() =>
        new() { [Primeira] = new[] { "Central" } };

    private static Partida Jogo(int categoriaId, int dupla1, int dupla2) =>
        new() { CategoriaId = categoriaId, Dupla1Id = dupla1, Dupla2Id = dupla2 };

    // ── A regra, sem grade nenhuma ────────────────────────────────────────────────────────

    [Fact]
    public void A_categoria_dona_pega_a_quadra_dela_mesmo_nao_sendo_a_primeira()
    {
        var escolhida = PreferenciaDeQuadra.Escolher(
            new[] { "Quadra 2", "Central" },
            daCategoria: new[] { "Central" },
            comDono: new[] { "Central" });

        Assert.Equal("Central", escolhida);
    }

    [Fact]
    public void Quem_nao_pediu_nada_fica_com_a_neutra_e_deixa_a_reservada_de_pe()
    {
        var escolhida = PreferenciaDeQuadra.Escolher(
            new[] { "Central", "Quadra 3" },
            daCategoria: Array.Empty<string>(),
            comDono: new[] { "Central" });

        Assert.Equal("Quadra 3", escolhida);
    }

    // O degrau 3: preferência não é reserva. Quadra parada atrasa o torneio inteiro.
    [Fact]
    public void Sem_neutra_livre_a_reservada_recebe_jogo_de_outra_categoria()
    {
        var escolhida = PreferenciaDeQuadra.Escolher(
            new[] { "Central" },
            daCategoria: Array.Empty<string>(),
            comDono: new[] { "Central" });

        Assert.Equal("Central", escolhida);
    }

    [Fact]
    public void Sem_quadra_livre_nenhuma_a_escolha_e_nula()
    {
        var escolhida = PreferenciaDeQuadra.Escolher(
            Array.Empty<string>(), Array.Empty<string>(), new[] { "Central" });

        Assert.Null(escolhida);
    }

    // ── A regra dentro da grade ───────────────────────────────────────────────────────────

    [Fact]
    public void Na_grade_os_jogos_da_categoria_dona_caem_na_quadra_dela()
    {
        var inicio = new DateTime(2026, 8, 15, 8, 0, 0);
        var horarios = GradeDeJogos.Horarios(inicio, new TimeSpan(23, 50, 0), 3, 50, 12).ToList();

        // Duas categorias, duplas distintas em cada jogo pra ninguém conflitar por pessoa.
        // Dois jogos da dona pra duas rodadas: a Central é UMA, então mais jogos dela do que
        // rodadas transbordaria pras outras quadras — e é isso mesmo que tem que acontecer.
        var jogos = new List<Partida>
        {
            Jogo(Sexta, 10, 11), Jogo(Sexta, 12, 13),
            Jogo(Primeira, 20, 21), Jogo(Primeira, 22, 23),
        };

        GradeDeJogos.Encaixar(jogos, horarios, null, Quadras, null, CentralDaPrimeira());

        Assert.All(jogos.Where(j => j.CategoriaId == Primeira),
            j => Assert.Equal("Central", j.NomeQuadra));
        Assert.All(jogos.Where(j => j.CategoriaId == Sexta),
            j => Assert.NotEqual("Central", j.NomeQuadra));
    }

    // A Central é uma só: a categoria dona com mais jogos do que rodadas transborda pras
    // outras quadras em vez de esperar a vez na quadra dela.
    [Fact]
    public void Mais_jogos_da_dona_do_que_rodadas_transborda_em_vez_de_esperar()
    {
        var inicio = new DateTime(2026, 8, 15, 8, 0, 0);
        var horarios = GradeDeJogos.Horarios(inicio, new TimeSpan(23, 50, 0), 3, 50, 12).ToList();

        var jogos = Enumerable.Range(1, 3).Select(i => Jogo(Primeira, i * 10, i * 10 + 1)).ToList();

        GradeDeJogos.Encaixar(jogos, horarios, null, Quadras, null, CentralDaPrimeira());

        Assert.All(jogos, j => Assert.Equal(inicio, j.HorarioPrevisto));
        Assert.Single(jogos, j => j.NomeQuadra == "Central");
        Assert.Equal(3, jogos.Select(j => j.NomeQuadra).Distinct().Count());
    }

    // O caso que fazia a preferência valer por sorte: a 6ª está na frente da fila e, com as
    // outras duas quadras já tomadas, ia parar na Central. Quem entra na vaga é a 1ª.
    [Fact]
    public void Com_so_a_reservada_livre_quem_entra_e_um_jogo_da_dona()
    {
        var inicio = new DateTime(2026, 8, 15, 8, 0, 0);
        // Vagas com folga, como todo chamador pede (ver GradeDeJogos.MargemDeHorarios): quem
        // é preterido numa vaga precisa ter a seguinte pra onde ir.
        var horarios = GradeDeJogos.Horarios(inicio, new TimeSpan(23, 50, 0), 3, 50, 6).ToList();

        var daSexta = new[] { Jogo(Sexta, 10, 11), Jogo(Sexta, 12, 13), Jogo(Sexta, 14, 15) };
        var daPrimeira = Jogo(Primeira, 20, 21);
        var jogos = daSexta.Append(daPrimeira).ToList();

        GradeDeJogos.Encaixar(jogos, horarios, null, Quadras, null, CentralDaPrimeira());

        Assert.Equal("Central", daPrimeira.NomeQuadra);
        Assert.Equal(inicio, daPrimeira.HorarioPrevisto);

        // O preterido não some: ele pega a vaga seguinte, no horário seguinte.
        var empurrado = daSexta.Single(j => j.HorarioPrevisto != inicio);
        Assert.Equal(inicio.AddMinutes(50), empurrado.HorarioPrevisto);
    }

    // A preferência não pode custar quadra parada: se a dona não tem jogo pra pôr ali, a
    // Central recebe quem estiver na fila.
    [Fact]
    public void Sem_jogo_da_dona_esperando_a_reservada_nao_fica_vazia()
    {
        var inicio = new DateTime(2026, 8, 15, 8, 0, 0);
        var horarios = GradeDeJogos.Horarios(inicio, new TimeSpan(23, 50, 0), 3, 50, 3).ToList();

        var jogos = new List<Partida> { Jogo(Sexta, 10, 11), Jogo(Sexta, 12, 13), Jogo(Sexta, 14, 15) };

        GradeDeJogos.Encaixar(jogos, horarios, null, Quadras, null, CentralDaPrimeira());

        Assert.All(jogos, j => Assert.Equal(inicio, j.HorarioPrevisto));
        Assert.Equal(3, jogos.Select(j => j.NomeQuadra).Distinct().Count());
        Assert.Contains(jogos, j => j.NomeQuadra == "Central");
    }

    // Torneio que não escolheu nada — a esmagadora maioria — tem que sair exatamente como
    // saía antes desta opção existir: primeira quadra livre, na ordem da fila.
    [Fact]
    public void Sem_preferencia_nenhuma_a_grade_nao_muda()
    {
        var inicio = new DateTime(2026, 8, 15, 8, 0, 0);

        List<Partida> Sortear(IReadOnlyDictionary<int, string[]>? preferencia)
        {
            var horarios = GradeDeJogos.Horarios(inicio, new TimeSpan(23, 50, 0), 3, 50, 9).ToList();
            var jogos = Enumerable.Range(1, 6).Select(i => Jogo(Sexta, i * 10, i * 10 + 1)).ToList();
            GradeDeJogos.Encaixar(jogos, horarios, null, Quadras, null, preferencia);
            return jogos;
        }

        var semMapa = Sortear(null);
        var mapaVazio = Sortear(new Dictionary<int, string[]>());

        Assert.Equal(
            semMapa.Select(j => (j.Dupla1Id, j.HorarioPrevisto, j.NomeQuadra)),
            mapaVazio.Select(j => (j.Dupla1Id, j.HorarioPrevisto, j.NomeQuadra)));

        // E é a grade de sempre: três quadras cheias no primeiro horário, na ordem da fila.
        Assert.Equal("Central", semMapa[0].NomeQuadra);
        Assert.Equal("Quadra 2", semMapa[1].NomeQuadra);
        Assert.Equal("Quadra 3", semMapa[2].NomeQuadra);
    }

    // A mesma quadra preferida por duas categorias é o caso REAL do torneio: a final da 1ª
    // masculina e a da 1ª feminina querem a central, em horas diferentes. Nenhuma das duas
    // pode ser tratada como intrusa na quadra que ela também pediu.
    [Fact]
    public void Duas_categorias_podem_preferir_a_mesma_quadra()
    {
        var inicio = new DateTime(2026, 8, 15, 8, 0, 0);
        var horarios = GradeDeJogos.Horarios(inicio, new TimeSpan(23, 50, 0), 3, 50, 6).ToList();

        var masculina = Jogo(Primeira, 20, 21);
        var feminina = Jogo(3, 30, 31);
        var jogos = new List<Partida> { Jogo(Sexta, 10, 11), masculina, feminina };

        GradeDeJogos.Encaixar(jogos, horarios, null, Quadras, null,
            new Dictionary<int, string[]> { [Primeira] = new[] { "Central" }, [3] = new[] { "Central" } });

        // A central é de quem chegar primeiro entre as duas donas; a outra não fica sem lugar.
        Assert.Contains(new[] { masculina, feminina }, j => j.NomeQuadra == "Central");
        Assert.All(jogos, j => Assert.False(string.IsNullOrEmpty(j.NomeQuadra)));
    }
}
