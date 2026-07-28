using Padelizou.Services;

namespace Padelizou.Tests;

// A agenda do professor mostra uma fatia do tempo por vez (dia, semana ou mês). Esse tipo
// de conta erra em silêncio — semana que começa no dia errado, mês que perde o dia 31, a
// aula das 00:00 que some na virada — e o erro só aparece quando alguém perde uma aula.
public class PeriodoAgendaTests
{
    // 2026-07-27 é uma SEGUNDA-feira.
    private static readonly DateTime Segunda = new(2026, 7, 27);

    // ── Janela ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Dia_vai_de_meia_noite_a_meia_noite()
    {
        var (inicio, fim) = PeriodoAgenda.Janela(PeriodoAgenda.Dia, Segunda.AddHours(15));

        Assert.Equal(new DateTime(2026, 7, 27), inicio);
        Assert.Equal(new DateTime(2026, 7, 28), fim);
    }

    [Fact]
    public void Semana_comeca_no_domingo()
    {
        // Calendário brasileiro começa no domingo. Com a segunda-feira como âncora, a
        // semana tem que voltar um dia, não avançar seis.
        var (inicio, fim) = PeriodoAgenda.Janela(PeriodoAgenda.Semana, Segunda);

        Assert.Equal(DayOfWeek.Sunday, inicio.DayOfWeek);
        Assert.Equal(new DateTime(2026, 7, 26), inicio);
        Assert.Equal(new DateTime(2026, 8, 2), fim);
    }

    [Fact]
    public void Domingo_pertence_a_propria_semana_e_nao_a_anterior()
    {
        var domingo = new DateTime(2026, 7, 26);

        var (inicio, _) = PeriodoAgenda.Janela(PeriodoAgenda.Semana, domingo);

        Assert.Equal(domingo, inicio);
    }

    [Fact]
    public void Mes_pega_do_dia_1_ao_ultimo()
    {
        var (inicio, fim) = PeriodoAgenda.Janela(PeriodoAgenda.Mes, new DateTime(2026, 7, 27));

        Assert.Equal(new DateTime(2026, 7, 1), inicio);
        Assert.Equal(new DateTime(2026, 8, 1), fim);   // exclusivo: 31/07 23:59 ainda entra
    }

    [Fact]
    public void Fim_e_exclusivo_pra_nao_perder_nem_duplicar_a_virada()
    {
        var (_, fim) = PeriodoAgenda.Janela(PeriodoAgenda.Dia, Segunda);
        var (inicioSeguinte, _) = PeriodoAgenda.Janela(PeriodoAgenda.Dia, Segunda.AddDays(1));

        // O fim de um dia é exatamente o início do outro: nada cai no vão, nada aparece duas vezes.
        Assert.Equal(fim, inicioSeguinte);
    }

    [Fact]
    public void Periodo_desconhecido_cai_na_semana()
    {
        // URL editada na mão não pode quebrar a tela.
        var (inicio, fim) = PeriodoAgenda.Janela("ano-inteiro", Segunda);

        Assert.Equal(new DateTime(2026, 7, 26), inicio);
        Assert.Equal(new DateTime(2026, 8, 2), fim);
        Assert.Equal(PeriodoAgenda.Semana, PeriodoAgenda.Normalizar(null));
    }

    // ── Navegação ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Seta_da_semana_anda_7_dias()
    {
        Assert.Equal(new DateTime(2026, 8, 3), PeriodoAgenda.Deslocar(PeriodoAgenda.Semana, Segunda, 1));
        Assert.Equal(new DateTime(2026, 7, 20), PeriodoAgenda.Deslocar(PeriodoAgenda.Semana, Segunda, -1));
    }

    [Fact]
    public void Seta_do_mes_nao_pula_fevereiro_por_causa_do_dia_31()
    {
        // 31/01 + 1 mês, feito ingenuamente, daria 28/02 — e outro passo voltaria pra 28/03,
        // pulando fevereiro na ida e perdendo março na volta. Ancorar no dia 1º resolve.
        var trintaEUm = new DateTime(2026, 1, 31);

        var fev = PeriodoAgenda.Deslocar(PeriodoAgenda.Mes, trintaEUm, 1);
        var mar = PeriodoAgenda.Deslocar(PeriodoAgenda.Mes, fev, 1);

        Assert.Equal(new DateTime(2026, 2, 1), fev);
        Assert.Equal(new DateTime(2026, 3, 1), mar);
    }

    [Fact]
    public void Ida_e_volta_devolve_o_mesmo_lugar()
    {
        foreach (var periodo in new[] { PeriodoAgenda.Dia, PeriodoAgenda.Semana, PeriodoAgenda.Mes })
        {
            var ida = PeriodoAgenda.Deslocar(periodo, new DateTime(2026, 3, 15), 1);
            var volta = PeriodoAgenda.Deslocar(periodo, ida, -1);

            var (a, _) = PeriodoAgenda.Janela(periodo, volta);
            var (b, _) = PeriodoAgenda.Janela(periodo, new DateTime(2026, 3, 15));
            Assert.Equal(b, a);
        }
    }

    // ── Grade do mês ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Grade_do_mes_tem_semanas_inteiras()
    {
        var grade = PeriodoAgenda.GradeDoMes(new DateTime(2026, 7, 15));

        Assert.Equal(0, grade.Count % 7);                       // sem buraco na tabela
        Assert.Equal(DayOfWeek.Sunday, grade[0].DayOfWeek);
        Assert.Equal(DayOfWeek.Saturday, grade[^1].DayOfWeek);
    }

    [Fact]
    public void Grade_do_mes_contem_todos_os_dias_do_mes()
    {
        // O teste que pega o erro clássico: o dia 31 ficar de fora da última linha.
        var grade = PeriodoAgenda.GradeDoMes(new DateTime(2026, 7, 1));

        for (int d = 1; d <= 31; d++)
        {
            Assert.Contains(new DateTime(2026, 7, d), grade);
        }
    }

    [Fact]
    public void Grade_de_fevereiro_que_comeca_no_domingo_nao_ganha_semana_vazia()
    {
        // Fevereiro de 2026 começa num domingo e tem 28 dias: cabe em 4 semanas exatas.
        var grade = PeriodoAgenda.GradeDoMes(new DateTime(2026, 2, 10));

        Assert.Equal(28, grade.Count);
        Assert.Equal(new DateTime(2026, 2, 1), grade[0]);
        Assert.Equal(new DateTime(2026, 2, 28), grade[^1]);
    }

    // ── Título ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Titulo_do_mes_sai_em_portugues()
    {
        // Escrito à mão de propósito: o servidor não tem cultura pt-BR garantida, e
        // "July" no meio da agenda seria o usuário descobrindo, não nós.
        Assert.Equal("julho de 2026", PeriodoAgenda.Titulo(PeriodoAgenda.Mes, Segunda));
        Assert.Equal("março de 2026", PeriodoAgenda.Titulo(PeriodoAgenda.Mes, new DateTime(2026, 3, 9)));
    }

    [Fact]
    public void Titulo_da_semana_so_repete_o_mes_quando_ele_vira()
    {
        Assert.Equal("26 de julho – 1 de agosto de 2026", PeriodoAgenda.Titulo(PeriodoAgenda.Semana, Segunda));
        Assert.Equal("5 – 11 de julho de 2026", PeriodoAgenda.Titulo(PeriodoAgenda.Semana, new DateTime(2026, 7, 8)));
    }

    [Fact]
    public void Titulo_do_dia_diz_o_dia_da_semana()
    {
        Assert.Equal("seg, 27 de julho de 2026", PeriodoAgenda.Titulo(PeriodoAgenda.Dia, Segunda));
    }

    // ── Faixa de horas da grade ───────────────────────────────────────────────────────

    [Fact]
    public void Sem_aula_a_grade_mostra_o_horario_comercial_do_padel()
    {
        var (primeira, ultima) = PeriodoAgenda.FaixaDeHoras(Array.Empty<DateTime>());

        Assert.Equal(6, primeira);
        Assert.Equal(22, ultima);
    }

    [Fact]
    public void Aula_fora_do_horario_comum_estica_a_grade()
    {
        // Sem isto, a aula das 5h da manhã simplesmente não apareceria na tela.
        var (primeira, ultima) = PeriodoAgenda.FaixaDeHoras(new[]
        {
            new DateTime(2026, 7, 27, 5, 0, 0),
            new DateTime(2026, 7, 27, 23, 30, 0),
        });

        Assert.Equal(5, primeira);
        Assert.Equal(23, ultima);
    }

    [Fact]
    public void Aulas_dentro_do_horario_comum_nao_encolhem_a_grade()
    {
        var (primeira, ultima) = PeriodoAgenda.FaixaDeHoras(new[] { new DateTime(2026, 7, 27, 10, 0, 0) });

        Assert.Equal(6, primeira);
        Assert.Equal(22, ultima);
    }
}
