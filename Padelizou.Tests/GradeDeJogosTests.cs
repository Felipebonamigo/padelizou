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

    // ---- Encaixar: conflito é de PESSOA, não de dupla ----
    // A chave direta pôs cada jogador em DUAS duplas do mesmo torneio (a da categoria dele e
    // a do mata-mata paralelo). Comparando dupla, a grade marcava as duas no mesmo horário em
    // quadras diferentes — e o defeito só apareceria com o nome sendo chamado duas vezes.

    [Fact]
    public void A_mesma_pessoa_em_duas_duplas_nao_joga_em_duas_quadras_ao_mesmo_tempo()
    {
        // Dupla 1 = jogadores 10 e 11 (a categoria dele).
        // Dupla 3 = o jogador 10 DE NOVO, agora com 30 (a chave direta paralela).
        // O terceiro jogo não tem ninguém em comum — é ele que sobe pra vaga disputada.
        var jogos = new List<Partida> { JogoEntre(1, 2), JogoEntre(3, 4), JogoEntre(5, 6) };
        var ocupantes = new Dictionary<int, int[]>
        {
            [1] = new[] { 10, 11 },
            [2] = new[] { 20, 21 },
            [3] = new[] { 10, 30 },   // o jogador 10 outra vez
            [4] = new[] { 40, 41 },
            [5] = new[] { 50, 51 },
            [6] = new[] { 60, 61 },
        };
        var inicio = new DateTime(2026, 8, 1, 18, 0, 0);
        var horarios = GradeDeJogos.Horarios(inicio, new TimeSpan(23, 50, 0), 2, 50, jogos.Count).ToList();

        // Com 2 quadras as duas primeiras vagas são o MESMO horário — é aí que o defeito
        // aparecia: os jogos 1 e 2 caíam juntos e o jogador 10 era chamado em duas quadras.
        Assert.Equal(horarios[0], horarios[1]);

        GradeDeJogos.Encaixar(jogos, horarios, ocupantes);

        Assert.NotEqual(jogos[0].HorarioPrevisto, jogos[1].HorarioPrevisto);
        // E sem buraco na grade: o jogo livre ocupou a vaga que sobrou, em vez de a grade
        // andar um horário só pra fugir do conflito.
        Assert.Equal(jogos[0].HorarioPrevisto, jogos[2].HorarioPrevisto);
    }

    [Fact]
    public void Duplas_sem_ninguem_em_comum_seguem_dividindo_o_horario()
    {
        // O contrário do teste acima: sem pessoa repetida, a grade não pode ficar mais lenta.
        var jogos = new List<Partida> { JogoEntre(1, 2), JogoEntre(3, 4) };
        var ocupantes = new Dictionary<int, int[]>
        {
            [1] = new[] { 10, 11 },
            [2] = new[] { 20, 21 },
            [3] = new[] { 30, 31 },
            [4] = new[] { 40, 41 },
        };
        var inicio = new DateTime(2026, 8, 1, 18, 0, 0);
        var horarios = GradeDeJogos.Horarios(inicio, new TimeSpan(23, 50, 0), 2, 50, jogos.Count).ToList();

        GradeDeJogos.Encaixar(jogos, horarios, ocupantes);

        Assert.Equal(jogos[0].HorarioPrevisto, jogos[1].HorarioPrevisto);
    }

    [Fact]
    public void Time_fora_do_mapa_continua_comparando_por_dupla()
    {
        // Na categoria de TIMES o Jogador1Id é o ORGANIZADOR em todos os times. Se eles
        // entrassem no mapa por pessoa, todo time conflitaria com todo time e a grade
        // inteira viraria uma fila de um jogo por horário.
        var jogos = new List<Partida> { JogoEntre(1, 2), JogoEntre(3, 4) };
        var mapaSoDeDuplasComuns = new Dictionary<int, int[]>();   // nenhum time listado

        var inicio = new DateTime(2026, 8, 1, 18, 0, 0);
        var horarios = GradeDeJogos.Horarios(inicio, new TimeSpan(23, 50, 0), 2, 50, jogos.Count).ToList();

        GradeDeJogos.Encaixar(jogos, horarios, mapaSoDeDuplasComuns);

        Assert.Equal(jogos[0].HorarioPrevisto, jogos[1].HorarioPrevisto);
    }

    // ---- Encaixar: a quadra sai da posição dentro do horário ----
    // A grade sabia ONDE cada jogo cai (é a N-ésima vaga daquele horário) e não estava
    // dizendo: todo jogo nascia "Quadra a definir" mesmo com as cinco quadras cadastradas,
    // e o jogador ficava com a hora sem o lugar.

    [Fact]
    public void Cada_jogo_do_mesmo_horario_cai_numa_quadra_diferente()
    {
        var jogos = new List<Partida> { JogoEntre(1, 2), JogoEntre(3, 4), JogoEntre(5, 6), JogoEntre(7, 8) };
        var inicio = new DateTime(2026, 8, 5, 20, 0, 0);
        var quadras = new[] { "Quadra A", "Quadra B" };
        var horarios = GradeDeJogos.Horarios(inicio, new TimeSpan(23, 0, 0), 2, 12, jogos.Count).ToList();

        GradeDeJogos.Encaixar(jogos, horarios, null, quadras);

        Assert.All(jogos, j => Assert.NotNull(j.NomeQuadra));
        foreach (var doHorario in jogos.GroupBy(j => j.HorarioPrevisto))
        {
            var nomes = doHorario.Select(j => j.NomeQuadra).ToList();
            Assert.Equal(nomes.Count, nomes.Distinct().Count());
        }
    }

    [Fact]
    public void Torneio_sem_quadra_cadastrada_segue_sem_nome()
    {
        // Inventar "Quadra 1" onde o clube chama de "Central" seria pior que não dizer nada.
        var jogos = new List<Partida> { JogoEntre(1, 2), JogoEntre(3, 4) };
        var inicio = new DateTime(2026, 8, 5, 20, 0, 0);
        var horarios = GradeDeJogos.Horarios(inicio, new TimeSpan(23, 0, 0), 2, 12, jogos.Count).ToList();

        GradeDeJogos.Encaixar(jogos, horarios);

        Assert.All(jogos, j => Assert.Null(j.NomeQuadra));
    }

    [Fact]
    public void Id_de_dupla_nao_se_confunde_com_id_de_jogador()
    {
        // A dupla fora do mapa cai no Id NEGADO justamente pra isso: se caísse no Id positivo,
        // a dupla 10 ocuparia o mesmo espaço que o JOGADOR 10 e as duas se repeliriam sem motivo.
        var jogos = new List<Partida> { JogoEntre(10, 2), JogoEntre(3, 4) };
        var ocupantes = new Dictionary<int, int[]>
        {
            [2] = new[] { 20, 21 },
            [3] = new[] { 10, 11 },   // jogador 10, homônimo do Id da dupla 10
            [4] = new[] { 40, 41 },
        };
        var inicio = new DateTime(2026, 8, 1, 18, 0, 0);
        var horarios = GradeDeJogos.Horarios(inicio, new TimeSpan(23, 50, 0), 2, 50, jogos.Count).ToList();

        GradeDeJogos.Encaixar(jogos, horarios, ocupantes);

        Assert.Equal(jogos[0].HorarioPrevisto, jogos[1].HorarioPrevisto);
    }

    // ---- Quando a fase seguinte abre ----
    // Assim que a anterior TERMINA — nunca no mesmo horário dela (os dois finalistas saem da
    // semifinal e não podem estar em duas quadras ao mesmo tempo), e nunca mais tarde.
    //
    // ⚠️ Já foi uma rodada inteira de folga, pra ninguém jogar dois jogos seguidos. O
    // organizador corrigiu: evitar jogo seguido só faz sentido se houver OUTRO jogo pra pôr
    // no meio. Com quadra sobrando a folga não vira descanso, vira quadra parada. Quando as
    // quadras são poucas, a própria lotação intercala — é o horário cheio que empurra a fase
    // seguinte pra frente.

    [Fact]
    public void A_proxima_fase_abre_quando_a_anterior_termina()
    {
        // Último jogo da semi às 20h, jogos de 30 min: ela termina 20h30 e a final abre 20h30.
        var ultimoDaSemi = new DateTime(2026, 8, 15, 20, 0, 0);

        var abertura = GradeDeJogos.AberturaDaProximaFase(
            ultimoDaSemi, Ate22h, new TimeSpan(8, 0, 0), 30);

        Assert.Equal(new DateTime(2026, 8, 15, 20, 30, 0), abertura);
    }

    [Fact]
    public void Nunca_devolve_o_horario_do_proprio_jogo_anterior()
    {
        // O defeito que trouxe esta regra: Semifinal e Final apareciam na MESMA hora — a final
        // antes de existirem os dois finalistas.
        foreach (var duracao in new[] { 12, 30, 40, 50, 60 })
        {
            var ultimo = new DateTime(2026, 8, 15, 9, 0, 0);
            var abertura = GradeDeJogos.AberturaDaProximaFase(ultimo, Ate22h, new TimeSpan(8, 0, 0), duracao);

            Assert.Equal(ultimo.AddMinutes(duracao), abertura);
        }
    }

    // ---- Emendar nas vagas livres do que JÁ está marcado ----
    // Sem saber o que já tem hora, o encaixe começava do zero e achava o torneio inteiro
    // vago: marcava a rodada nova na Quadra A das 22h onde já havia jogo. O remendo era
    // empurrar toda rodada nova pro fim de TUDO — e aí, com 5 quadras e 5 categorias, cada
    // semifinal esperava a semifinal alheia e quatro quadras ficavam paradas.

    [Fact]
    public void Rodada_nova_nao_repete_a_quadra_de_um_jogo_ja_marcado()
    {
        var quadras = new[] { "Quadra A", "Quadra B", "Quadra C" };
        var doze = new DateTime(2026, 8, 15, 12, 0, 0);

        var jaMarcado = JogoEntre(9, 10);
        jaMarcado.HorarioPrevisto = doze;
        jaMarcado.NomeQuadra = "Quadra A";

        var jogos = new List<Partida> { JogoEntre(1, 2), JogoEntre(3, 4) };
        var horarios = GradeDeJogos.Descontando(
            GradeDeJogos.Horarios(doze, Ate22h, 3, 50, 12), new[] { doze });

        GradeDeJogos.Encaixar(jogos, horarios, null, quadras, new[] { jaMarcado });

        Assert.All(jogos, j => Assert.Equal(doze, j.HorarioPrevisto));
        Assert.Equal(new[] { "Quadra B", "Quadra C" }, jogos.Select(j => j.NomeQuadra));
    }

    [Fact]
    public void Quem_ja_esta_escalado_no_horario_nao_entra_de_novo_nele()
    {
        // A dupla 1 já joga às 12h por outra categoria. A rodada nova a empurra pro horário
        // seguinte em vez de chamá-la pra duas quadras.
        var doze = new DateTime(2026, 8, 15, 12, 0, 0);

        var jaMarcado = JogoEntre(1, 9);
        jaMarcado.HorarioPrevisto = doze;
        jaMarcado.NomeQuadra = "Quadra A";

        var jogos = new List<Partida> { JogoEntre(1, 2) };
        var horarios = GradeDeJogos.Descontando(
            GradeDeJogos.Horarios(doze, Ate22h, 3, 50, 12), new[] { doze });

        GradeDeJogos.Encaixar(jogos, horarios, null, new[] { "Quadra A", "Quadra B", "Quadra C" },
            new[] { jaMarcado });

        Assert.Equal(doze.AddMinutes(50), jogos[0].HorarioPrevisto);
    }

    [Fact]
    public void Vaga_que_ja_tem_dono_sai_da_lista_de_horarios()
    {
        var doze = new DateTime(2026, 8, 15, 12, 0, 0);
        var horarios = GradeDeJogos.Horarios(doze, Ate22h, 3, 50, 6).ToList();

        // Duas das três vagas das 12h já estão ocupadas.
        var sobram = GradeDeJogos.Descontando(horarios, new[] { doze, doze });

        Assert.Equal(1, sobram.Count(h => h == doze));
        Assert.Equal(3, sobram.Count(h => h == doze.AddMinutes(50)));
    }

    [Fact]
    public void Virou_o_dia_e_o_descanso_ja_aconteceu_a_noite_inteira()
    {
        // Empurrar mais uma rodada aqui só atrasaria a abertura do dia seguinte sem dar folga
        // nenhuma a ninguém: entre 22h40 e as 8h da manhã cabe descanso de sobra.
        var ultimo = new DateTime(2026, 8, 15, 21, 40, 0);

        var abertura = GradeDeJogos.AberturaDaProximaFase(ultimo, Ate22h, new TimeSpan(8, 0, 0), 40);

        Assert.Equal(new DateTime(2026, 8, 16, 8, 0, 0), abertura);
    }
}
