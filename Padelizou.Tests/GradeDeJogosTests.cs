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
        // 1 quadra, 60 min, das 8h às 22h = 14 jogos cabem no dia.
        var grade = Grade(quadras: 1, duracao: 60, quantidade: 40);

        Assert.All(grade, h => Assert.InRange(h.TimeOfDay, new TimeSpan(8, 0, 0), new TimeSpan(21, 0, 0)));
    }

    [Fact]
    public void O_que_nao_cabe_vai_pro_dia_seguinte_no_horario_de_abertura()
    {
        var grade = Grade(quadras: 1, duracao: 60, quantidade: 16);

        // 14 jogos no sábado (8h..21h), o 15º abre o domingo às 8h.
        Assert.Equal(Sabado8h.Date, grade[13].Date);
        Assert.Equal(Sabado8h.Date.AddDays(1).AddHours(8), grade[14]);
        Assert.Equal(Sabado8h.Date.AddDays(1).AddHours(9), grade[15]);
    }

    [Fact]
    public void O_jogo_precisa_caber_inteiro_no_expediente()
    {
        // Das 8h às 9h, jogo de 50 min: só o das 8h cabe — o das 8h50 terminaria 9h40.
        var grade = Grade(quadras: 1, duracao: 50, quantidade: 2, fim: new TimeSpan(9, 0, 0));

        Assert.Equal(Sabado8h, grade[0]);
        Assert.Equal(Sabado8h.Date.AddDays(1).AddHours(8), grade[1]);
    }

    [Fact]
    public void Expediente_invalido_nao_trava_o_sorteio()
    {
        // Fim antes do início seria laço infinito procurando um dia que cabe. Vira dia aberto.
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

        Assert.Equal(DateTime.Today.AddHours(8), torneio.AberturaDaGrade);
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
    public void Ultimo_jogo_do_dia_e_o_ultimo_que_cabe_INTEIRO()
    {
        // 19h-23h com jogo de 50 min: 21h30 é o último que termina dentro (22h20).
        // 22h20 até caberia começar, mas terminaria 23h10 — fora do expediente.
        var ultimo = GradeDeJogos.UltimoInicioDoDia(new TimeSpan(19, 0, 0), new TimeSpan(23, 0, 0), 50);

        Assert.Equal(new TimeSpan(21, 30, 0), ultimo);
    }

    [Fact]
    public void A_dica_bate_com_o_horario_que_a_grade_realmente_marca()
    {
        var abertura = new TimeSpan(19, 0, 0);
        var fim = new TimeSpan(23, 0, 0);
        var sabado19h = Sabado8h.Date.Add(abertura);

        // 2 quadras, 8 jogos = 4 rodadas, exatamente o que cabe no dia.
        var grade = Grade(quadras: 2, duracao: 50, quantidade: 8, inicio: sabado19h, fim: fim);

        Assert.Equal(sabado19h.Date.Add(GradeDeJogos.UltimoInicioDoDia(abertura, fim, 50)!.Value), grade.Last());
        Assert.Equal(4, GradeDeJogos.RodadasPorDia(abertura, fim, 50));
    }

    [Fact]
    public void O_jogo_seguinte_ao_ultimo_ja_cai_no_dia_seguinte()
    {
        var abertura = new TimeSpan(19, 0, 0);
        var fim = new TimeSpan(23, 0, 0);
        var sabado19h = Sabado8h.Date.Add(abertura);

        // Um jogo a mais que o dia comporta: 2 quadras x 4 rodadas = 8, então o 9º vira o dia.
        var grade = Grade(quadras: 2, duracao: 50, quantidade: 9, inicio: sabado19h, fim: fim);

        Assert.Equal(sabado19h.AddDays(1), grade.Last());
    }

    [Fact]
    public void Dia_sem_hora_pra_acabar_nao_tem_ultimo_jogo()
    {
        // Fim antes ou igual ao início = expediente aberto; nada a avisar.
        Assert.Null(GradeDeJogos.UltimoInicioDoDia(new TimeSpan(19, 0, 0), new TimeSpan(19, 0, 0), 50));
        Assert.Null(GradeDeJogos.RodadasPorDia(new TimeSpan(19, 0, 0), new TimeSpan(8, 0, 0), 50));
    }

    [Fact]
    public void Janela_menor_que_um_jogo_nao_comporta_nem_o_primeiro()
    {
        // 19h-19h30 com jogo de 50 min: a tela precisa avisar em vez de fingir um horário.
        Assert.Equal(0, GradeDeJogos.RodadasPorDia(new TimeSpan(19, 0, 0), new TimeSpan(19, 30, 0), 50));
        Assert.Null(GradeDeJogos.UltimoInicioDoDia(new TimeSpan(19, 0, 0), new TimeSpan(19, 30, 0), 50));
    }

    [Fact]
    public void Janela_exata_de_um_jogo_comporta_um_jogo()
    {
        Assert.Equal(1, GradeDeJogos.RodadasPorDia(new TimeSpan(19, 0, 0), new TimeSpan(19, 50, 0), 50));
        Assert.Equal(new TimeSpan(19, 0, 0),
            GradeDeJogos.UltimoInicioDoDia(new TimeSpan(19, 0, 0), new TimeSpan(19, 50, 0), 50));
    }
}
