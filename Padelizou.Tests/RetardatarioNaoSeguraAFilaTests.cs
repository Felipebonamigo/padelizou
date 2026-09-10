using Padelizou.Models;
using Padelizou.Services;
using static Padelizou.Services.ProximasFasesDaChave;

namespace Padelizou.Tests;

// UM JOGO DE GRUPO RETARDATÁRIO NÃO SEGURA AS ELIMINATÓRIAS DO TORNEIO INTEIRO.
//
// 🗣️ Felipe, 09/09/2026, no print do Er em produção: *"os jogos estao terminando no sabado 19:40
// por que? nao deveria, é pra ir ate as 23h"*. A fase de grupos fechava 19h40 de sábado, UM jogo
// de grupo (3ª Feminina, Grupo B) caía em 08h de domingo — impedimento ou concentração da dupla —
// e a barreira de posto era o `Max` dos horários: TODAS as eliminatórias esperavam esse jogo, e a
// noite de sábado ficava vazia. É o oposto do que ele pediu ao criar a ordem das fases: *"a menos
// que fique horario vazio"*.
//
// A régua nova: o fim de um posto é o fim do BLOCO CHEIO dele — o primeiro horário da grade
// inteiro sem jogo do posto encerra o bloco, e o que vem depois é retardatário. Os três lugares
// que calculam a barreira (LevasDaGrade, ProximasFasesDaChave, AuditoriaDaGrade) leem a mesma
// função, OrdemDasFases.FimDoBloco.
public class RetardatarioNaoSeguraAFilaTests
{
    private static readonly DateTime Sexta = new(2026, 9, 11);
    private static readonly DateTime Sabado = new(2026, 9, 12);
    private static readonly DateTime Domingo = new(2026, 9, 13);
    private static readonly TimeSpan FimDoDia = new(23, 0, 0);
    private static readonly TimeSpan Abertura = new(8, 0, 0);
    private const int Duracao = 50;

    private static DateTime Seguinte(DateTime h) => GradeDeJogos.DepoisDe(h, FimDoDia, Abertura, Duracao);

    // Os horários de um dia, de `de` até `ate` inclusive, de 50 em 50 minutos.
    private static IEnumerable<DateTime> Rodadas(DateTime de, DateTime ate)
    {
        for (var h = de; h <= ate; h = h.AddMinutes(Duracao)) yield return h;
    }

    private static DateTime UltimoDeSabado => Sabado.AddHours(19).AddMinutes(40);

    // ── A função ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Fim_do_bloco_para_no_primeiro_horario_vazio()
    {
        var horarios = Rodadas(Sexta.AddHours(18), Sexta.AddHours(22).AddMinutes(10))
            .Concat(Rodadas(Sabado.AddHours(8), UltimoDeSabado))
            .Append(Domingo.AddHours(8))               // o retardatário
            .ToList();

        Assert.Equal(UltimoDeSabado, OrdemDasFases.FimDoBloco(horarios, Seguinte, 5));
    }

    [Fact]
    public void Sem_buraco_o_fim_do_bloco_e_o_ultimo_jogo()
    {
        var horarios = Rodadas(Sabado.AddHours(8), UltimoDeSabado).ToList();

        Assert.Equal(UltimoDeSabado, OrdemDasFases.FimDoBloco(horarios, Seguinte, 5));
    }

    // A virada de dia não é buraco: depois das 23h de sexta (o último horário de começar jogo,
    // Torneio.HoraFimDoDia) a grade abre às 8h de sábado.
    [Fact]
    public void Virada_de_dia_nao_e_buraco()
    {
        var horarios = Rodadas(Sexta.AddHours(18), Sexta.AddHours(23))
            .Append(Sabado.AddHours(8))
            .ToList();

        Assert.Equal(Sabado.AddHours(8), OrdemDasFases.FimDoBloco(horarios, Seguinte, 5));
    }

    // Jogo mexido na mão pra um minuto quebrado (20h13) não abre buraco: só um horário INTEIRO
    // sem jogo conta, senão toda troca de horário do organizador partiria o bloco.
    [Fact]
    public void Minuto_quebrado_nao_e_buraco()
    {
        var horarios = Rodadas(Sabado.AddHours(8), UltimoDeSabado)
            .Append(Sabado.AddHours(20).AddMinutes(13))
            .Append(Sabado.AddHours(21).AddMinutes(20))
            .ToList();

        Assert.Equal(Sabado.AddHours(21).AddMinutes(20), OrdemDasFases.FimDoBloco(horarios, Seguinte, 5));
    }

    // ⚠️ O outro lado da régua: um horário vazio NO MEIO da fase (o encaixe não achou jogo que
    // coubesse nele) não é o fim dela — depois dele vêm dezenas de jogos. Cortar ali mandaria as
    // eliminatórias pro meio dos grupos, que é a queixa original de 09/09.
    [Fact]
    public void Um_buraco_no_meio_da_fase_nao_e_o_fim_dela()
    {
        var horarios = Rodadas(Sexta.AddHours(18), Sexta.AddHours(22).AddMinutes(10))   // 23h de sexta vazio
            .Concat(Rodadas(Sabado.AddHours(8), UltimoDeSabado).SelectMany(h => Enumerable.Repeat(h, 5)))
            .ToList();

        Assert.Equal(UltimoDeSabado, OrdemDasFases.FimDoBloco(horarios, Seguinte, 5));
    }

    // Uma rodada INTEIRA depois do buraco é a fase seguindo, não retardatário — mesmo três dias
    // depois (a grade que transbordou pro dia 15, no Er de 09/09).
    [Fact]
    public void Uma_rodada_cheia_depois_do_buraco_e_a_fase_seguindo()
    {
        var horarios = Rodadas(Sabado.AddHours(8), UltimoDeSabado)
            .Concat(Enumerable.Repeat(Domingo.AddDays(2).AddHours(20).AddMinutes(30), 5))
            .ToList();

        Assert.Equal(Domingo.AddDays(2).AddHours(20).AddMinutes(30), OrdemDasFases.FimDoBloco(horarios, Seguinte, 5));
    }

    [Fact]
    public void Mais_de_um_retardatario_cai_fora_junto()
    {
        var horarios = Rodadas(Sabado.AddHours(8), UltimoDeSabado)
            .Append(Domingo.AddHours(8))
            .Append(Domingo.AddHours(8))
            .Append(Domingo.AddHours(10).AddMinutes(30))
            .ToList();

        Assert.Equal(UltimoDeSabado, OrdemDasFases.FimDoBloco(horarios, Seguinte, 5));
    }

    [Fact]
    public void Sem_jogo_nao_ha_fim()
    {
        Assert.Null(OrdemDasFases.FimDoBloco(Array.Empty<DateTime>(), Seguinte, 5));
    }

    // ── A prévia ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_previa_nao_espera_o_retardatario()
    {
        // Jogos de GRUPO reais: sábado inteiro até 19h40, e um só em 08h de domingo.
        var reais = Rodadas(Sabado.AddHours(8), UltimoDeSabado)
            .Select(h => new VagaOcupada(h, null, "Grupo A"))
            .Append(new VagaOcupada(Domingo.AddHours(8), null, "Grupo B"))
            .ToList();

        // A "Rápida" fechou os grupos dela às 17h10 de sábado.
        var cadeia = ProximasFasesDaChave.MontarDosGrupos(
            new[] { "Grupo A", "Grupo B", "Grupo C", "Grupo D" }, 2,
            Sabado.AddHours(17).AddMinutes(10), "Rápida");

        var jogos = ProximasFasesDaChave.Agendar(
            new[] { cadeia },
            new ConfiguracaoDaGrade(Duracao, 5, Array.Empty<string>(), FimDoDia, Abertura),
            reais);

        Assert.NotEmpty(jogos);
        var primeiro = jogos.Min(j => j.Horario)!.Value;

        Assert.True(primeiro < Domingo.AddHours(8),
            $"a fase de grupos fechou {UltimoDeSabado:dd/MM HH:mm} e só UM jogo caiu no domingo; a prévia "
            + $"deixou a noite de sábado vazia e começou {primeiro:dd/MM HH:mm}");
        Assert.True(primeiro >= UltimoDeSabado,
            $"a prévia começou {primeiro:dd/MM HH:mm}, antes de a fase de grupos fechar ({UltimoDeSabado:dd/MM HH:mm})");
    }

    // ── A grade de verdade ────────────────────────────────────────────────────────────────

    private static Torneio Torneio() => new()
    {
        Nome = "Er", Codigo = "ER",
        DataInicio = Sexta.AddHours(18), DataFim = Domingo,
        QuantidadeQuadras = 2,
        TempoPrevistoPartidaMinutos = Duracao,
        HoraInicioDoDia = new TimeSpan(18, 0, 0),
        HoraInicioDiasSeguintes = Abertura,
        HoraFimDoDia = FimDoDia,
    };

    private static Partida Jogo(int categoria, int d1, int d2, DateTime? quando, string fase, string? quadra = null) =>
        new() { Codigo = "X", Fase = fase, CategoriaId = categoria, Dupla1Id = d1, Dupla2Id = d2,
                HorarioPrevisto = quando, NomeQuadra = quadra, Status = "Agendada" };

    [Fact]
    public void A_grade_de_verdade_nao_espera_o_retardatario()
    {
        var torneio = Torneio();

        // Categoria 1 (a "Lenta"): grupos o sábado inteiro na Q1, e o retardatário em 08h de domingo.
        var jaMarcados = Rodadas(Sabado.AddHours(8), UltimoDeSabado)
            .Select(h => Jogo(1, 11, 12, h, "Grupo A", "Q1"))
            .Append(Jogo(1, 13, 14, Domingo.AddHours(8), "Grupo B", "Q1"))
            // Categoria 2 (a "Rápida"): fechou os grupos dela às 17h10 de sábado, na Q2.
            .Append(Jogo(2, 21, 22, Sabado.AddHours(17).AddMinutes(10), "Grupo A", "Q2"))
            .ToList();

        var oitavas = new List<Partida>
        {
            Jogo(2, 21, 23, null, "Oitavas de Final"),
            Jogo(2, 22, 24, null, "Oitavas de Final"),
        };

        var ocupantes = new Dictionary<int, int[]>
        {
            [11] = new[] { 111, 112 }, [12] = new[] { 121, 122 }, [13] = new[] { 131, 132 }, [14] = new[] { 141, 142 },
            [21] = new[] { 211, 212 }, [22] = new[] { 221, 222 }, [23] = new[] { 231, 232 }, [24] = new[] { 241, 242 },
        };

        LevasDaGrade.Encaixar(torneio, oitavas, Sexta.AddHours(18), jaMarcados,
            new LevasDaGrade.Restricoes(ocupantes, new[] { "Q1", "Q2" }));

        Assert.All(oitavas, j => Assert.NotNull(j.HorarioPrevisto));

        var noDomingo = oitavas
            .Where(j => j.HorarioPrevisto >= Domingo.AddHours(8))
            .Select(j => $"{j.Fase} às {j.HorarioPrevisto:dd/MM HH:mm}")
            .ToList();

        Assert.True(noDomingo.Count == 0,
            $"a fase de grupos fechou {UltimoDeSabado:dd/MM HH:mm} e só UM jogo caiu no domingo; a grade "
            + $"deixou a noite de sábado vazia: {string.Join(", ", noDomingo)}");
        Assert.All(oitavas, j => Assert.True(j.HorarioPrevisto >= UltimoDeSabado,
            $"eliminatória às {j.HorarioPrevisto:dd/MM HH:mm}, antes de a fase de grupos fechar"));
    }

    // ── O Conferir grade ──────────────────────────────────────────────────────────────────

    private static Dupla Dupla(int id, int j1, int j2) =>
        new() { Id = id, Jogador1Id = j1, Jogador2Id = j2, Categoria = new Categoria { Id = 1, Nome = "3ª", Codigo = "C3" } };

    [Fact]
    public void Conferir_grade_nao_acusa_a_eliminatoria_que_entrou_no_lugar_vazio()
    {
        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21), Dupla(3, 30, 31), Dupla(4, 40, 41) };

        var jogos = Rodadas(Sabado.AddHours(8), UltimoDeSabado)
            .Select(h => Jogo(1, 1, 2, h, "Grupo A"))
            .Append(Jogo(1, 3, 4, Domingo.AddHours(8), "Grupo B"))                       // retardatário
            .Append(Jogo(1, 1, 3, Sabado.AddHours(20).AddMinutes(30), "Quartas de Final")) // no lugar vazio
            .ToList();

        var achados = AuditoriaDaGrade.Conferir(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma);

        Assert.DoesNotContain(achados, a => a.Regra == AuditoriaDaGrade.FaseForaDeOrdem);
    }

    // E o retardatário em si é apontado — é o jogo que o organizador vai querer mexer na mão.
    [Fact]
    public void Conferir_grade_aponta_o_retardatario()
    {
        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21), Dupla(3, 30, 31), Dupla(4, 40, 41) };

        var jogos = Rodadas(Sabado.AddHours(8), UltimoDeSabado)
            .Select(h => Jogo(1, 1, 2, h, "Grupo A"))
            .Append(Jogo(1, 3, 4, Domingo.AddHours(8), "Grupo B"))
            .ToList();

        var achados = AuditoriaDaGrade.Conferir(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma);

        var achado = Assert.Single(achados, a => a.Regra == AuditoriaDaGrade.Retardatario);
        Assert.Contains("13/09", achado.Descricao);
        Assert.Contains("12/09 às 19:40", achado.Descricao);
    }

    // A contrapartida: eliminatória ANTES do fim do bloco continua sendo acusada.
    [Fact]
    public void Conferir_grade_continua_acusando_a_eliminatoria_antes_do_bloco()
    {
        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21), Dupla(3, 30, 31), Dupla(4, 40, 41) };

        var jogos = Rodadas(Sabado.AddHours(8), UltimoDeSabado)
            .Select(h => Jogo(1, 1, 2, h, "Grupo A"))
            .Append(Jogo(1, 1, 3, Sabado.AddHours(15), "Quartas de Final"))
            .ToList();

        var achados = AuditoriaDaGrade.Conferir(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma);

        Assert.Contains(achados, a => a.Regra == AuditoriaDaGrade.FaseForaDeOrdem);
    }
}
