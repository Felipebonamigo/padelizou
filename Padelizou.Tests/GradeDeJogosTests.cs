using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A grade antiga somava a duração de um jogo por vez a partir da data de início, ignorando
// quadras e expediente: 24 jogos de 50 min viravam 20 horas seguidas, com jogo marcado às
// 3h40 da manhã.
public class GradeDeJogosTests
{
    private static readonly DateTime Sabado8h = new(2026, 8, 15, 8, 0, 0);
    private static readonly TimeSpan Ate22h = new(22, 0, 0);

    private static List<DateTime> Grade(int quadras, int duracao, int quantidade,
        DateTime? inicio = null, TimeSpan? fim = null) =>
        GradeDeJogos.Horarios(inicio ?? Sabado8h, fim ?? Ate22h, quadras, duracao, quantidade).ToList();

    [Fact]
    public void Jogos_simultaneos_ocupam_as_quadras_no_mesmo_horario()
    {
        // 3 quadras: os três primeiros jogos começam juntos.
        var grade = Grade(quadras: 3, duracao: 50, quantidade: 3);

        Assert.All(grade, h => Assert.Equal(Sabado8h, h));
    }

    [Fact]
    public void O_relogio_so_anda_quando_as_quadras_enchem()
    {
        var grade = Grade(quadras: 2, duracao: 50, quantidade: 5);

        Assert.Equal(Sabado8h, grade[0]);
        Assert.Equal(Sabado8h, grade[1]);
        Assert.Equal(Sabado8h.AddMinutes(50), grade[2]);
        Assert.Equal(Sabado8h.AddMinutes(50), grade[3]);
        Assert.Equal(Sabado8h.AddMinutes(100), grade[4]);
    }

    [Fact]
    public void Uma_quadra_so_e_a_fila_antiga_um_jogo_por_vez()
    {
        var grade = Grade(quadras: 1, duracao: 30, quantidade: 3);

        Assert.Equal(new[] { Sabado8h, Sabado8h.AddMinutes(30), Sabado8h.AddMinutes(60) }, grade);
    }

    [Fact]
    public void Nada_e_marcado_depois_do_fim_do_expediente()
    {
        // 1 quadra, 60 min, abrindo 8h com teto de 22h: os jogos começam de 8h a 22h.
        var grade = Grade(quadras: 1, duracao: 60, quantidade: 40);

        Assert.All(grade, h => Assert.InRange(h.TimeOfDay, new TimeSpan(8, 0, 0), new TimeSpan(22, 0, 0)));
    }

    [Fact]
    public void O_que_nao_cabe_vai_pro_dia_seguinte_no_horario_de_abertura()
    {
        var grade = Grade(quadras: 1, duracao: 60, quantidade: 17);

        // 15 jogos no sábado (8h..22h), o 16º abre o domingo às 8h.
        Assert.Equal(Sabado8h.Date, grade[14].Date);
        Assert.Equal(new TimeSpan(22, 0, 0), grade[14].TimeOfDay);
        Assert.Equal(Sabado8h.Date.AddDays(1).AddHours(8), grade[15]);
        Assert.Equal(Sabado8h.Date.AddDays(1).AddHours(9), grade[16]);
    }

    // O padrão real do torneio de fim de semana (descrito pelo Felipe em 27/07/2026):
    // sexta começa 18h e os últimos jogos são 23h / 23h50; sábado e domingo começam 8h.
    [Fact]
    public void O_limite_e_a_hora_de_COMECAR__o_jogo_pode_virar_a_madrugada()
    {
        // Teto às 23h50 com jogo de 50 min: o das 23h50 acontece e termina 0h40 — normal
        // numa sexta. O seguinte já é do dia seguinte, na abertura do sábado.
        var sexta18h = new DateTime(2026, 8, 21, 18, 0, 0);
        var grade = GradeDeJogos.Horarios(sexta18h, new TimeSpan(23, 50, 0), 1, 50, 9,
            aberturaDiasSeguintes: new TimeSpan(8, 0, 0)).ToList();

        Assert.Equal(new DateTime(2026, 8, 21, 23, 50, 0), grade[7]);
        Assert.Equal(new DateTime(2026, 8, 22, 8, 0, 0), grade[8]);
    }

    [Fact]
    public void O_primeiro_dia_abre_num_horario_e_os_demais_em_outro()
    {
        var sexta18h = new DateTime(2026, 8, 21, 18, 0, 0);
        var grade = GradeDeJogos.Horarios(sexta18h, new TimeSpan(23, 50, 0), 2, 50, 40,
            aberturaDiasSeguintes: new TimeSpan(8, 0, 0)).ToList();

        // Sexta: 8 rodadas de 18h a 23h50, 2 jogos por rodada = 16 jogos.
        Assert.Equal(16, grade.Count(h => h.Date == new DateTime(2026, 8, 21)));
        // Sábado abre às 8h, não às 18h — e nada cai de madrugada.
        Assert.Equal(new DateTime(2026, 8, 22, 8, 0, 0), grade[16]);
        Assert.DoesNotContain(grade, h => h.Date > new DateTime(2026, 8, 21)
                                       && h.TimeOfDay < new TimeSpan(8, 0, 0));
    }

    [Fact]
    public void Expediente_invalido_nao_trava_o_sorteio()
    {
        // Teto antes da abertura seria laço infinito procurando um dia que cabe. Vira dia aberto.
        var grade = Grade(quadras: 2, duracao: 50, quantidade: 6, fim: new TimeSpan(6, 0, 0));

        Assert.Equal(6, grade.Count);
        Assert.All(grade, h => Assert.Equal(Sabado8h.Date, h.Date));
    }

    [Fact]
    public void Sem_jogos_a_grade_sai_vazia()
    {
        Assert.Empty(Grade(quadras: 3, duracao: 50, quantidade: 0));
    }

    [Fact]
    public void Quadras_ou_duracao_zeradas_caem_num_padrao_utilizavel()
    {
        // Configuração torta não pode gerar divisão por zero nem jogos no mesmo instante.
        var grade = Grade(quadras: 0, duracao: 0, quantidade: 3);

        Assert.Equal(Sabado8h, grade[0]);
        Assert.Equal(Sabado8h.AddMinutes(50), grade[1]);
        Assert.Equal(Sabado8h.AddMinutes(100), grade[2]);
    }

    [Fact]
    public void A_grade_abre_no_horario_configurado_e_nao_a_meia_noite()
    {
        // DataInicio guarda só a data: sem a hora de abertura a grade começava 00:00 e
        // marcava jogo de madrugada.
        var torneio = new Padelizou.Models.Torneio
        {
            Nome = "T",
            Codigo = "T1",
            DataInicio = new DateTime(2026, 8, 15),
            HoraInicioDoDia = new TimeSpan(19, 0, 0),
        };

        Assert.Equal(new DateTime(2026, 8, 15, 19, 0, 0), torneio.AberturaDaGrade);
    }

    [Fact]
    public void Sem_data_marcada_a_grade_ainda_abre_num_horario_valido()
    {
        var torneio = new Padelizou.Models.Torneio { Nome = "T", Codigo = "T1", DataInicio = null };

        // 18h é o padrão do primeiro dia: torneio costuma abrir numa sexta à noite.
        Assert.Equal(DateTime.Today.AddHours(18), torneio.AberturaDaGrade);
        Assert.Equal(new TimeSpan(8, 0, 0), torneio.HoraInicioDiasSeguintes);
    }

    [Fact]
    public void Torneio_real_de_16_duplas_cabe_em_um_sabado()
    {
        // 4 grupos de 4 duplas = 24 jogos. Com 3 quadras e 50 min, são 8 rodadas: 8h -> 14h40.
        var grade = Grade(quadras: 3, duracao: 50, quantidade: 24);

        Assert.Equal(Sabado8h, grade.First());
        Assert.Equal(Sabado8h.AddMinutes(50 * 7), grade.Last());
        Assert.All(grade, h => Assert.Equal(Sabado8h.Date, h.Date));
    }

    // ── A dica que a tela de criar torneio mostra ──────────────────────────────────────
    // O organizador escolhe "das 19h às 23h" sem calcular onde isso cai. Se a tela disser um
    // horário e a grade marcar outro, a dica vira mentira — por isso a conta é a mesma.

    [Fact]
    public void O_ultimo_jogo_e_o_ultimo_que_a_CADENCIA_alcanca()
    {
        // Teto de 23h50 com jogos de 50 min a partir das 18h: 23h50 é alcançado na régua.
        Assert.Equal(new TimeSpan(23, 50, 0),
            GradeDeJogos.UltimoInicioDoDia(new TimeSpan(18, 0, 0), new TimeSpan(23, 50, 0), 50));

        // Mesmo teto, jogos de 1h: a régua para em 23h — 23h50 não é múltiplo da cadência.
        // É exatamente isso que o organizador não calcula de cabeça.
        Assert.Equal(new TimeSpan(23, 0, 0),
            GradeDeJogos.UltimoInicioDoDia(new TimeSpan(18, 0, 0), new TimeSpan(23, 50, 0), 60));
    }

    [Fact]
    public void A_dica_bate_com_o_horario_que_a_grade_realmente_marca()
    {
        var abertura = new TimeSpan(18, 0, 0);
        var teto = new TimeSpan(23, 50, 0);
        var sexta18h = new DateTime(2026, 8, 21, 18, 0, 0);

        // 8 rodadas cabem na sexta; com 2 quadras são 16 jogos.
        Assert.Equal(8, GradeDeJogos.RodadasPorDia(abertura, teto, 50));

        var grade = GradeDeJogos.Horarios(sexta18h, teto, 2, 50, 16,
            aberturaDiasSeguintes: new TimeSpan(8, 0, 0)).ToList();

        Assert.Equal(sexta18h.Date.Add(GradeDeJogos.UltimoInicioDoDia(abertura, teto, 50)!.Value), grade.Last());
    }

    [Fact]
    public void O_jogo_seguinte_ao_ultimo_ja_cai_no_dia_seguinte()
    {
        var sexta18h = new DateTime(2026, 8, 21, 18, 0, 0);

        // Um jogo a mais que a sexta comporta: 2 quadras x 8 rodadas = 16, o 17º vira o dia.
        var grade = GradeDeJogos.Horarios(sexta18h, new TimeSpan(23, 50, 0), 2, 50, 17,
            aberturaDiasSeguintes: new TimeSpan(8, 0, 0)).ToList();

        Assert.Equal(new DateTime(2026, 8, 22, 8, 0, 0), grade.Last());
    }

    [Fact]
    public void Dia_sem_hora_pra_acabar_nao_tem_ultimo_jogo()
    {
        // Teto antes ou igual à abertura = dia aberto; nada a avisar.
        Assert.Null(GradeDeJogos.UltimoInicioDoDia(new TimeSpan(19, 0, 0), new TimeSpan(19, 0, 0), 50));
        Assert.Null(GradeDeJogos.RodadasPorDia(new TimeSpan(19, 0, 0), new TimeSpan(8, 0, 0), 50));
    }

    [Fact]
    public void Teto_apertado_ainda_comporta_o_jogo_de_abertura()
    {
        // Abre 19h, teto 19h30, jogo de 50 min: o das 19h começa e termina 19h50. É 1 jogo,
        // não zero — o teto é a hora de COMEÇAR.
        Assert.Equal(1, GradeDeJogos.RodadasPorDia(new TimeSpan(19, 0, 0), new TimeSpan(19, 30, 0), 50));
        Assert.Equal(new TimeSpan(19, 0, 0),
            GradeDeJogos.UltimoInicioDoDia(new TimeSpan(19, 0, 0), new TimeSpan(19, 30, 0), 50));
    }

    // ---- Encaixar: o mesmo inscrito nunca em duas quadras ao mesmo tempo ----
    // Achado ao testar a categoria de times: os jogos de um grupo saem em sequência
    // ((A,B), (A,C), (B,C)) e com 2 quadras os horários vêm em pares — o encaixe posicional
    // punha (A,B) e (A,C) no MESMO horário, com A em duas quadras. Valia igual pras duplas.

    private static Partida JogoEntre(int dupla1, int dupla2) => new()
    {
        Codigo = $"{dupla1}x{dupla2}",
        Status = "Agendada",
        Dupla1Id = dupla1,
        Dupla2Id = dupla2,
    };

    [Fact]
    public void Grupo_de_tres_com_duas_quadras_nao_poe_ninguem_em_duas_quadras_ao_mesmo_tempo()
    {
        // O caso real que apareceu no teste de times: 2 grupos de 3, 2 quadras.
        var jogos = new List<Partida>
        {
            JogoEntre(1, 2), JogoEntre(1, 3), JogoEntre(2, 3),   // grupo A
            JogoEntre(4, 5), JogoEntre(4, 6), JogoEntre(5, 6),   // grupo B
        };
        var inicio = new DateTime(2026, 8, 1, 18, 0, 0);
        var horarios = GradeDeJogos.Horarios(inicio, new TimeSpan(23, 50, 0), 2, 50, jogos.Count).ToList();

        GradeDeJogos.Encaixar(jogos, horarios);

        // Ninguém joga duas vezes no mesmo horário.
        foreach (var grupoDeHorario in jogos.GroupBy(j => j.HorarioPrevisto))
        {
            var envolvidos = grupoDeHorario.SelectMany(j => new[] { j.Dupla1Id, j.Dupla2Id }).ToList();
            Assert.Equal(envolvidos.Count, envolvidos.Distinct().Count());
        }

        // E a grade continua cheia: todos os horários oferecidos foram usados.
        Assert.All(jogos, j => Assert.NotNull(j.HorarioPrevisto));
        Assert.Equal(horarios.OrderBy(h => h), jogos.Select(j => j.HorarioPrevisto!.Value).OrderBy(h => h));
    }

    [Fact]
    public void Conflito_inevitavel_nao_trava_a_grade()
    {
        // 1 grupo de 3 com 2 quadras: o segundo jogo do horário SEMPRE repete alguém.
        // Melhor aceitar o conflito do que deixar buraco — e o organizador tem a troca
        // de horários pra ajustar.
        var jogos = new List<Partida> { JogoEntre(1, 2), JogoEntre(1, 3), JogoEntre(2, 3) };
        var inicio = new DateTime(2026, 8, 1, 18, 0, 0);
        var horarios = GradeDeJogos.Horarios(inicio, new TimeSpan(23, 50, 0), 2, 50, jogos.Count).ToList();

        GradeDeJogos.Encaixar(jogos, horarios);

        Assert.All(jogos, j => Assert.NotNull(j.HorarioPrevisto));
    }

    [Fact]
    public void Sem_conflito_o_encaixe_e_posicional_como_sempre_foi()
    {
        var jogos = new List<Partida> { JogoEntre(1, 2), JogoEntre(3, 4), JogoEntre(5, 6) };
        var inicio = new DateTime(2026, 8, 1, 18, 0, 0);
        var horarios = GradeDeJogos.Horarios(inicio, new TimeSpan(23, 50, 0), 2, 50, jogos.Count).ToList();

        GradeDeJogos.Encaixar(jogos, horarios);

        Assert.Equal(horarios[0], jogos[0].HorarioPrevisto);
        Assert.Equal(horarios[1], jogos[1].HorarioPrevisto);
        Assert.Equal(horarios[2], jogos[2].HorarioPrevisto);
    }
}
