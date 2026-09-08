using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O LOCAL EXTERNO DENTRO DO ENCAIXE — as três regras novas de 08/09/2026, e o que separa uma
// da outra é a DUREZA:
//
//  • JANELA DA QUADRA — DURA. Quadra fechada não recebe jogo, ponto. Não adianta ceder: o
//    portão do lugar alugado está trancado.
//  • CATEGORIA SEM TRANSBORDO — DURA. O organizador disse que a 5ª não vai pro externo.
//  • SEDE PRINCIPAL PRIMEIRO e EVITAR OS 2 JOGOS LÁ — MOLES. Cedem quando respeitá-las
//    deixaria quadra parada, mesma régua da folga pra trocar de clube. A prioridade declarada
//    do torneio continua sendo "nenhuma quadra fica sem jogo até o final" — e no local alugado
//    por hora ela vale ainda mais, porque a hora vazia foi paga.
public class SedeExtraNaGradeTests
{
    private const int DuracaoDaGrade = 50;
    private const int Principal = 1;
    private const int Externo = 2;
    private static readonly DateTime Sabado = new(2026, 8, 15);

    private static Partida Jogo(int d1, int d2, int categoriaId = 10) => new()
    {
        Codigo = $"{d1}x{d2}", Status = "Agendada", Fase = "Grupo A",
        CategoriaId = categoriaId, Dupla1Id = d1, Dupla2Id = d2,
    };

    private static readonly string[] AsQuadras = { "Central", "Externa" };

    private static SedesDoTorneio Sede(DateTime? de = null, DateTime? ate = null,
        bool categoriaPodeTransbordar = true, bool evitarDoisLa = false) =>
        SedesDoTorneio.Montar(Principal, 0,
            new[]
            {
                new Quadra { Nome = "Central", ClubeId = Principal },
                new Quadra { Nome = "Externa", ClubeId = Externo, DisponivelDe = de, DisponivelAte = ate },
            },
            new[] { new Categoria { Id = 10, Nome = "5ª F", Codigo = "C5F",
                                    PodeJogarNaSedeExtra = categoriaPodeTransbordar } },
            evitarDoisJogosNaSedeExtra: evitarDoisLa);

    // ── A janela: DURA ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Quadra_fechada_nao_recebe_jogo_nem_com_a_grade_apertada()
    {
        // Dois jogos, duas quadras no mesmo horário — mas a Externa só abre às 12h.
        var jogos = new List<Partida> { Jogo(1, 2), Jogo(3, 4) };
        var horarios = new List<DateTime> { Sabado.AddHours(9), Sabado.AddHours(9) };

        GradeDeJogos.Encaixar(jogos, horarios, DuracaoDaGrade, quadras: AsQuadras,
            sedes: Sede(de: Sabado.AddHours(12)));

        Assert.All(jogos, j => Assert.NotEqual("Externa", j.NomeQuadra));
    }

    [Fact]
    public void Depois_de_aberta_a_quadra_do_externo_entra_normalmente()
    {
        var jogos = new List<Partida> { Jogo(1, 2), Jogo(3, 4) };
        var horarios = new List<DateTime> { Sabado.AddHours(13), Sabado.AddHours(13) };

        GradeDeJogos.Encaixar(jogos, horarios, DuracaoDaGrade, quadras: AsQuadras,
            sedes: Sede(de: Sabado.AddHours(12), ate: Sabado.AddHours(18)));

        Assert.Contains(jogos, j => j.NomeQuadra == "Externa");
    }

    // ── O transbordo por categoria: DURO ──────────────────────────────────────────────────

    [Fact]
    public void Categoria_sem_transbordo_nunca_pega_quadra_do_externo()
    {
        var jogos = new List<Partida> { Jogo(1, 2), Jogo(3, 4) };
        var horarios = new List<DateTime> { Sabado.AddHours(9), Sabado.AddHours(9) };

        GradeDeJogos.Encaixar(jogos, horarios, DuracaoDaGrade, quadras: AsQuadras,
            sedes: Sede(categoriaPodeTransbordar: false));

        Assert.All(jogos, j => Assert.NotEqual("Externa", j.NomeQuadra));
    }

    // ── A sede principal primeiro: MOLE ───────────────────────────────────────────────────

    // ⚠️ É ISTO QUE FAZ O LOCAL ALUGADO CUSTAR MENOS: com um jogo só, ele fica na quadra de
    // casa. Antes desta ordem, "a primeira quadra livre" era a ordem do cadastro — e podia ser
    // a alugada.
    [Fact]
    public void Com_vaga_nos_dois_lugares_o_jogo_fica_na_sede_principal()
    {
        var jogo = Jogo(1, 2);

        GradeDeJogos.Encaixar(new List<Partida> { jogo },
            new List<DateTime> { Sabado.AddHours(9) }, DuracaoDaGrade,
            quadras: new[] { "Externa", "Central" },   // externa PRIMEIRO no cadastro, de propósito
            sedes: Sede());

        Assert.Equal("Central", jogo.NomeQuadra);
    }

    [Fact]
    public void Quando_a_principal_enche_o_transbordo_acontece()
    {
        var jogos = new List<Partida> { Jogo(1, 2), Jogo(3, 4) };
        var horarios = new List<DateTime> { Sabado.AddHours(9), Sabado.AddHours(9) };

        GradeDeJogos.Encaixar(jogos, horarios, DuracaoDaGrade, quadras: AsQuadras, sedes: Sede());

        Assert.Contains(jogos, j => j.NomeQuadra == "Central");
        Assert.Contains(jogos, j => j.NomeQuadra == "Externa");
    }

    // ── "Que a dupla jogue apenas UM lá": MOLE ────────────────────────────────────────────
    // 🗣️ Felipe, 08/09/2026: "o Er também me falou que eles não querem q a dupla jogue os 2
    // jogos la, que jogue apenas um, para que ele possa jogar no clube dele também".

    // ⚠️ O CENÁRIO PRECISA FORÇAR A DUPLA PRA FORA DE CASA — a primeira versão deste teste
    // passava com a regra DESLIGADA, porque a ordenação "sede principal primeiro" já mandava a
    // dupla 1 pra Central sozinha e ela nunca chegava a repetir o externo. Um teste que nunca
    // exercita a regra é um teste que confirma o código em vez de travá-lo.
    //
    // Aqui a Central está TOMADA nos dois primeiros horários (jogos já marcados), então a única
    // quadra livre é a do local alugado — que é exatamente a situação em que a regra do Er
    // importa. E há vaga sobrando adiante, senão o encaixe cede por falta de grade.
    private static Partida JaMarcado(int d1, int d2, DateTime quando, string quadra)
    {
        var p = Jogo(d1, d2);
        p.HorarioPrevisto = quando;
        p.NomeQuadra = quadra;
        return p;
    }

    [Fact]
    public void Com_a_opcao_ligada_a_dupla_nao_repete_o_externo_havendo_outro_jogo_pra_por()
    {
        var primeiro = Jogo(1, 2);
        var segundo = Jogo(1, 5);
        var deOutros = Jogo(3, 4);
        var jogos = new List<Partida> { primeiro, segundo, deOutros };

        // Central ocupada às 9h e às 10h; livre às 11h — é lá que o SEGUNDO jogo da dupla 1
        // tem que cair, em vez de repetir o alugado.
        var jaMarcados = new[]
        {
            JaMarcado(20, 21, Sabado.AddHours(9), "Central"),
            JaMarcado(22, 23, Sabado.AddHours(10), "Central"),
        };

        var horarios = new List<DateTime>
        {
            Sabado.AddHours(9), Sabado.AddHours(10), Sabado.AddHours(11), Sabado.AddHours(12),
        };

        GradeDeJogos.Encaixar(jogos, horarios, DuracaoDaGrade, quadras: AsQuadras,
            jaMarcados: jaMarcados, sedes: Sede(evitarDoisLa: true));

        var daDupla1 = jogos.Where(j => j.Dupla1Id == 1 || j.Dupla2Id == 1).ToList();
        Assert.Equal(2, daDupla1.Count);
        var noExterno = daDupla1.Count(j => j.NomeQuadra == "Externa");
        Assert.True(noExterno <= 1, $"a dupla 1 jogou {noExterno} vezes no local externo");
        Assert.All(daDupla1, j => Assert.NotNull(j.HorarioPrevisto));
    }

    [Fact]
    public void Desligada_a_opcao_nao_atrapalha_ninguem()
    {
        var jogos = new List<Partida> { Jogo(1, 2), Jogo(3, 4) };
        var horarios = new List<DateTime> { Sabado.AddHours(9), Sabado.AddHours(9) };

        GradeDeJogos.Encaixar(jogos, horarios, DuracaoDaGrade, quadras: AsQuadras,
            sedes: Sede(evitarDoisLa: false));

        Assert.All(jogos, j => Assert.NotNull(j.HorarioPrevisto));
    }

    // ⚠️ A REGRA CEDE, e é o ponto de ela ser mole: se a ÚNICA vaga que sobra é a do externo e
    // não há outro jogo pra pôr nela, o segundo jogo da dupla entra lá mesmo. Quadra parada no
    // lugar que se está pagando por hora é o desfecho que o organizador não aceita.
    [Fact]
    public void Sem_outro_jogo_pra_por_a_regra_cede_em_vez_de_deixar_a_quadra_parada()
    {
        var primeiro = Jogo(1, 2);
        var segundo = Jogo(1, 5);
        var jogos = new List<Partida> { primeiro, segundo };

        // Só o externo tem vaga: a Central está fechada nestes dois horários (janela invertida).
        var sede = SedesDoTorneio.Montar(Principal, 0,
            new[]
            {
                new Quadra { Nome = "Central", ClubeId = Principal, DisponivelAte = Sabado },
                new Quadra { Nome = "Externa", ClubeId = Externo },
            },
            new[] { new Categoria { Id = 10, Nome = "5ª F", Codigo = "C5F" } },
            evitarDoisJogosNaSedeExtra: true);

        GradeDeJogos.Encaixar(jogos, new List<DateTime> { Sabado.AddHours(9), Sabado.AddHours(10) },
            DuracaoDaGrade, quadras: AsQuadras, sedes: sede);

        Assert.All(jogos, j => Assert.NotNull(j.HorarioPrevisto));
    }

    // ⚠️ A MEMÓRIA VEM DOS JOGOS QUE JÁ ESTAVAM MARCADOS. Sem semear com `jaMarcados`, um
    // "Refazer grade" no meio do torneio esqueceria o jogo de sábado de manhã no alugado e
    // mandaria a mesma dupla pra lá de novo à tarde — a regra valeria só dentro de uma rodada
    // de cálculo, que é o mesmo que não valer.
    [Fact]
    public void O_jogo_ja_marcado_no_externo_conta_como_a_vez_da_dupla()
    {
        var novo = Jogo(1, 5);
        var deOutros = Jogo(3, 4);

        // A dupla 1 JÁ jogou no alugado às 8h. E a Central está tomada às 10h, então a vaga das
        // 10h só tem o alugado — sem a memória, o jogo novo dela cairia lá.
        var jaMarcados = new[]
        {
            JaMarcado(1, 2, Sabado.AddHours(8), "Externa"),
            JaMarcado(20, 21, Sabado.AddHours(10), "Central"),
        };

        var horarios = new List<DateTime>
        {
            Sabado.AddHours(10), Sabado.AddHours(11), Sabado.AddHours(12),
        };

        GradeDeJogos.Encaixar(new List<Partida> { novo, deOutros }, horarios, DuracaoDaGrade,
            quadras: AsQuadras, jaMarcados: jaMarcados, sedes: Sede(evitarDoisLa: true));

        Assert.NotNull(novo.HorarioPrevisto);
        Assert.NotEqual("Externa", novo.NomeQuadra);
    }

    // ── Torneio de uma sede só: nada disto existe ─────────────────────────────────────────

    [Fact]
    public void Torneio_de_uma_sede_so_se_comporta_exatamente_como_antes()
    {
        var jogos = new List<Partida> { Jogo(1, 2), Jogo(3, 4) };
        var horarios = new List<DateTime> { Sabado.AddHours(9), Sabado.AddHours(9) };

        GradeDeJogos.Encaixar(jogos, horarios, DuracaoDaGrade, quadras: AsQuadras);

        Assert.Equal(new[] { "Central", "Externa" }, jogos.Select(j => j.NomeQuadra).OrderBy(n => n));
    }
}
