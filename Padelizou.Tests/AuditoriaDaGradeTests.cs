using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O BOTÃO "CONFERIR A GRADE" — 🗣️ Felipe, 09/09/2026: "faz esse botão e sobe".
//
// Nasceu de um beco: o Felipe pediu duas vezes que eu conferisse a grade do torneio dele em
// `dev`, e esta sessão NÃO ALCANÇA o `dev` (o proxy recusa o CONNECT com 403). A saída não é
// pedir print — é virar a auditoria em tela, pra ele apertar e ver, em qualquer torneio.
//
// ⚠️ O PONTO DE ARQUITETURA: a TELA e o TESTE chamam o MESMO serviço. Uma auditoria escrita
// duas vezes é uma auditoria que um dia diz "tudo certo" sobre uma regra que mudou no outro
// lado — e ela seria acreditada, porque é justamente ela que existe pra ser acreditada.
public class AuditoriaDaGradeTests
{
    private static readonly DateTime Sexta = new(2026, 10, 9);
    private static readonly DateTime Sabado = new(2026, 10, 10);

    private static Torneio Torneio() => new()
    {
        Nome = "T", Codigo = "T1",
        DataInicio = Sexta.AddHours(18),
        QuantidadeQuadras = 2,
        TempoPrevistoPartidaMinutos = 50,
        HoraInicioDoDia = new TimeSpan(18, 0, 0),
        HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
        HoraFimDoDia = new TimeSpan(23, 0, 0),
    };

    private static Dupla Dupla(int id, int j1, int j2) =>
        new() { Id = id, Jogador1Id = j1, Jogador2Id = j2, Categoria = new Categoria { Id = 1, Nome = "3ª", Codigo = "C3" } };

    private static Partida Jogo(int d1, int d2, DateTime? quando, string fase = "Grupo A", string? quadra = null) =>
        new() { Codigo = "X", Fase = fase, CategoriaId = 1, Dupla1Id = d1, Dupla2Id = d2, HorarioPrevisto = quando, NomeQuadra = quadra };

    // ⚠️ 🗣️ Felipe, 09/09/2026: *"e ali esta marcando dia 15, como assim? tem q rever isso, torneio
    // termina no domingo dia 13"*. `Torneio.DataFim` existia desde sempre e o motor NUNCA a leu —
    // era um aviso na tela de previsão e mais nada. Agora a conferência a lê.
    [Fact]
    public void Jogo_marcado_depois_do_fim_do_torneio_e_acusado()
    {
        var torneio = Torneio();
        torneio.DataFim = Sabado;                       // acaba no sábado 10/10

        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21) };
        var jogos = new[] { Jogo(1, 2, Sabado.AddDays(2).AddHours(19)) };   // segunda 12/10

        var achados = AuditoriaDaGrade.Conferir(torneio, jogos, duplas, SedesDoTorneio.Nenhuma);

        var achado = Assert.Single(achados, a => a.Regra == AuditoriaDaGrade.DepoisDoFim);
        Assert.Contains("12/10", achado.Descricao);
        Assert.Contains("10/10", achado.Descricao);
    }

    // A contrapartida: o jogo que COMEÇA no último dia e varre a madrugada é o normal do torneio,
    // não um estouro. A comparação é por DIA — mesma leitura de PrevisaoGradeVM.EstouraOPrazo.
    [Fact]
    public void Jogo_no_ultimo_dia_do_torneio_nao_e_acusado()
    {
        var torneio = Torneio();
        torneio.DataFim = Sabado;

        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21) };
        var jogos = new[] { Jogo(1, 2, Sabado.AddHours(23).AddMinutes(50)) };

        var achados = AuditoriaDaGrade.Conferir(torneio, jogos, duplas, SedesDoTorneio.Nenhuma);

        Assert.DoesNotContain(achados, a => a.Regra == AuditoriaDaGrade.DepoisDoFim);
    }

    // Sem prazo marcado não há o que estourar — e o torneio sem `DataFim` é a maioria.
    [Fact]
    public void Sem_DataFim_nada_e_acusado_de_passar_do_fim()
    {
        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21) };
        var jogos = new[] { Jogo(1, 2, Sabado.AddDays(30)) };

        var achados = AuditoriaDaGrade.Conferir(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma);

        Assert.DoesNotContain(achados, a => a.Regra == AuditoriaDaGrade.DepoisDoFim);
    }

    // ⚠️ A ORDEM DAS FASES VIRA ACHADO (09/09/2026). 🗣️ Felipe, depois de duas correções: *"como
    // que ele nao ta respeitando a ordem que eu tinha solicitado de nao jogar chaves no final?"*.
    //
    // Enquanto a conferência não sabia olhar isso, a única forma de responder era eu ler o print —
    // e print mostra um pedaço da lista. A régua do posto mora em Services/OrdemDasFases; aqui ela
    // vira uma pergunta que o organizador faz sozinho, em qualquer torneio.
    [Fact]
    public void Eliminatoria_antes_do_fim_da_fase_de_grupos_e_acusada()
    {
        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21) };
        var jogos = new[]
        {
            Jogo(1, 2, Sabado.AddHours(20), "Quartas de Final"),   // eliminatória às 20h
            Jogo(1, 2, Sabado.AddHours(22), "Grupo A"),            // e ainda tem grupo às 22h
        };

        var achados = AuditoriaDaGrade.Conferir(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma);

        var achado = Assert.Single(achados, a => a.Regra == AuditoriaDaGrade.FaseForaDeOrdem);
        Assert.Contains("Quartas de Final", achado.Descricao);
    }

    // A contrapartida, e ela importa tanto quanto: dividir o MESMO horário é permitido — é o
    // "a menos que fique horario vazio" do pedido. Só vir ANTES é que não pode.
    [Fact]
    public void Eliminatoria_no_mesmo_horario_do_ultimo_grupo_nao_e_acusada()
    {
        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21) };
        var jogos = new[]
        {
            Jogo(1, 2, Sabado.AddHours(22), "Grupo A"),
            Jogo(1, 2, Sabado.AddHours(22), "Quartas de Final"),
        };

        var achados = AuditoriaDaGrade.Conferir(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma);

        Assert.DoesNotContain(achados, a => a.Regra == AuditoriaDaGrade.FaseForaDeOrdem);
    }

    [Fact]
    public void Final_depois_de_tudo_nao_e_acusada()
    {
        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21) };
        var jogos = new[]
        {
            Jogo(1, 2, Sabado.AddHours(9), "Grupo A"),
            Jogo(1, 2, Sabado.AddHours(11), "Quartas de Final"),
            Jogo(1, 2, Sabado.AddHours(13), "Semifinal"),
            Jogo(1, 2, Sabado.AddHours(15), "Final"),
        };

        var achados = AuditoriaDaGrade.Conferir(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma);

        Assert.DoesNotContain(achados, a => a.Regra == AuditoriaDaGrade.FaseForaDeOrdem);
    }

    // Grade limpa não inventa achado — é o caso que o organizador vai ver na maioria das vezes,
    // e uma tela que sempre acha alguma coisa deixa de ser lida.
    [Fact]
    public void Grade_limpa_nao_tem_achado()
    {
        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21) };
        var jogos = new[] { Jogo(1, 2, Sabado.AddHours(9)) };

        Assert.Empty(AuditoriaDaGrade.Conferir(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma));
    }

    [Fact]
    public void Acusa_jogo_dentro_do_impedimento_da_dupla()
    {
        var comImpedimento = Dupla(1, 10, 11);
        comImpedimento.ImpedimentoSabadoManha = true;
        var duplas = new[] { comImpedimento, Dupla(2, 20, 21) };

        var achados = AuditoriaDaGrade.Conferir(Torneio(),
            new[] { Jogo(1, 2, Sabado.AddHours(9)) }, duplas, SedesDoTorneio.Nenhuma);

        var achado = Assert.Single(achados);
        Assert.Equal(AuditoriaDaGrade.Impedimento, achado.Regra);
        Assert.Contains("Sábado de manhã", achado.Descricao);
    }

    // ⚠️ O ACHADO MAIS GRAVE DOS SEIS: a pessoa não se divide. Foi ele que apareceu na
    // auditoria de hoje com 4 quadras, e é o que o organizador descobre sendo chamado duas
    // vezes no microfone.
    [Fact]
    public void Acusa_a_mesma_pessoa_em_dois_jogos_no_mesmo_horario()
    {
        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21), Dupla(3, 10, 30) };
        var jogos = new[]
        {
            Jogo(1, 2, Sabado.AddHours(9)),
            Jogo(3, 2, Sabado.AddHours(9)),   // o jogador 10 está nas duplas 1 e 3
        };

        var achados = AuditoriaDaGrade.Conferir(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma);

        Assert.Contains(achados, a => a.Regra == AuditoriaDaGrade.PessoaEmDoisJogos);
    }

    [Fact]
    public void Acusa_concentracao_nao_atendida_na_fase_de_grupos()
    {
        var concentrada = Dupla(1, 10, 11);
        concentrada.ConcentrarJogosEm = TurnoDeConcentracao.SextaNoite;
        var duplas = new[] { concentrada, Dupla(2, 20, 21) };

        var achados = AuditoriaDaGrade.Conferir(Torneio(),
            new[] { Jogo(1, 2, Sabado.AddHours(9)) }, duplas, SedesDoTorneio.Nenhuma);

        Assert.Contains(achados, a => a.Regra == AuditoriaDaGrade.Concentracao);
    }

    // ⚠️ E NÃO ACUSA NA ELIMINATÓRIA: a concentração vale só na fase de grupos (decisão do
    // Felipe). Acusar aqui encheria a tela de achado que não é problema — e tela que grita
    // demais ninguém lê.
    [Fact]
    public void Nao_acusa_concentracao_fora_da_fase_de_grupos()
    {
        var concentrada = Dupla(1, 10, 11);
        concentrada.ConcentrarJogosEm = TurnoDeConcentracao.SextaNoite;
        var duplas = new[] { concentrada, Dupla(2, 20, 21) };

        var achados = AuditoriaDaGrade.Conferir(Torneio(),
            new[] { Jogo(1, 2, Sabado.AddHours(9), fase: "Semifinal") }, duplas, SedesDoTorneio.Nenhuma);

        Assert.DoesNotContain(achados, a => a.Regra == AuditoriaDaGrade.Concentracao);
    }

    [Fact]
    public void Acusa_eliminatoria_no_sabado_a_noite_de_categoria_que_pediu_pra_nao_ter()
    {
        var torneio = Torneio();
        var categoria = new Categoria { Id = 1, Nome = "5ª Feminina", Codigo = "C5F", EliminatoriaNoSabadoANoite = false };
        torneio.Categorias.Add(categoria);

        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21) };

        var achados = AuditoriaDaGrade.Conferir(torneio,
            new[] { Jogo(1, 2, Sabado.AddHours(21), fase: "Semifinal") }, duplas, SedesDoTorneio.Nenhuma);

        Assert.Contains(achados, a => a.Regra == AuditoriaDaGrade.NoiteDeSabado);
    }

    [Fact]
    public void Acusa_quadra_usada_fora_da_janela_do_local_alugado()
    {
        var sedes = SedesDoTorneio.Montar(1, 0,
            new[]
            {
                new Quadra { Nome = "Casa", ClubeId = 1 },
                new Quadra { Nome = "Alugada", ClubeId = 2, DisponivelDe = Sabado.AddHours(8), DisponivelAte = Sabado.AddHours(12) },
            },
            Array.Empty<Categoria>());

        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21) };

        var achados = AuditoriaDaGrade.Conferir(Torneio(),
            new[] { Jogo(1, 2, Sabado.AddHours(15), quadra: "Alugada") }, duplas, sedes);

        Assert.Contains(achados, a => a.Regra == AuditoriaDaGrade.QuadraFechada);
    }

    [Fact]
    public void Acusa_jogo_sem_horario()
    {
        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21) };

        var achados = AuditoriaDaGrade.Conferir(Torneio(),
            new[] { Jogo(1, 2, null) }, duplas, SedesDoTorneio.Nenhuma);

        Assert.Contains(achados, a => a.Regra == AuditoriaDaGrade.SemHorario);
    }

    // O organizador precisa saber ONDE olhar: o achado carrega a hora e o nome de quem está
    // envolvido, senão vira "tem algo errado em algum lugar", que não resolve nada.
    [Fact]
    public void O_achado_diz_quando_e_quem()
    {
        var comImpedimento = Dupla(1, 10, 11);
        comImpedimento.ImpedimentoSabadoManha = true;
        comImpedimento.NomeTime = "Fulano e Sicrano";   // NomeDeExibicao sai daqui

        var achados = AuditoriaDaGrade.Conferir(Torneio(),
            new[] { Jogo(1, 2, Sabado.AddHours(9)) },
            new[] { comImpedimento, Dupla(2, 20, 21) }, SedesDoTorneio.Nenhuma);

        var achado = Assert.Single(achados);
        Assert.Equal(Sabado.AddHours(9), achado.Quando);
        Assert.Contains("Fulano e Sicrano", achado.Descricao);
    }

    // ── As duas guardas de 09/09/2026: a auditoria recriava a régua do motor em vez de usá-la ──
    //
    // ⚠️ É o defeito que o cabeçalho deste serviço proíbe com todas as letras ("ESTE SERVIÇO É A
    // ÚNICA CÓPIA DA AUDITORIA"). O bloco "mesma pessoa em dois jogos" tinha escrito à mão o que
    // `RoboDoChaveamento.OcupantesPorDupla` e `GradeDeJogos.Encaixar` já decidem — e divergia dos
    // dois em direções opostas: falso POSITIVO em torneio de times, falso NEGATIVO em grade
    // desalinhada. Achado numa revisão adversarial antes de publicar.

    [Fact]
    public void Em_torneio_de_times_nao_acusa_o_organizador_em_todos_os_jogos()
    {
        // 🕳️ FALSO POSITIVO EM MASSA. Todo TIME é uma Dupla com `Jogador1Id` = organizador (a
        // coluna é NOT NULL), então comparar pessoa na mão enxerga a MESMA pessoa em todos os
        // times e acusa a grade inteira, um achado por horário.
        //
        // O motor não cai nisso: `OcupantesPorDupla` filtra `!d.EhTime`, e o comentário dele diz
        // por quê — "comparar por pessoa faria todo time conflitar com todo time, empurrando a
        // grade inteira pra frente". A auditoria tem que enxergar a grade como o motor a montou.
        var organizador = 777;
        var times = new[]
        {
            TimeDe(1, organizador), TimeDe(2, organizador),
            TimeDe(3, organizador), TimeDe(4, organizador),
        };
        var jogos = new[]
        {
            Jogo(1, 2, Sabado.AddHours(18), quadra: "Quadra 1"),
            Jogo(3, 4, Sabado.AddHours(18), quadra: "Quadra 2"),
        };

        var achados = AuditoriaDaGrade.Conferir(Torneio(), jogos, times, SedesDoTorneio.Nenhuma);

        Assert.DoesNotContain(achados, a => a.Regra == AuditoriaDaGrade.PessoaEmDoisJogos);
    }

    [Fact]
    public void Acusa_a_mesma_pessoa_em_jogos_que_se_sobrepoem_sem_comecar_no_mesmo_minuto()
    {
        // 🕳️ FALSO NEGATIVO. O choque era medido por INSTANTE EXATO (`GroupBy(HorarioPrevisto)`),
        // mas o motor mede por INTERVALO desde 21/08/2026 (`CruzaComAPessoa`: a distância entre
        // os dois é menor que a duração da partida).
        //
        // ⚠️ E a grade desalinhada NÃO é hipótese: `AberturaDoRecalculo` parte de `DateTime.Now`
        // sempre que há jogo já em quadra, então um "Refazer grade" às 20h13 produz jogos em
        // 20:13 enquanto os antigos seguem em 20:00. Era exatamente aí que a tela dizia "Nada
        // fora do lugar" — e é exatamente aí que o organizador aperta o botão.
        var mesmaPessoa = 42;
        var duplas = new[] { Dupla(1, mesmaPessoa, 11), Dupla(2, 20, 21), Dupla(3, mesmaPessoa, 31), Dupla(4, 40, 41) };
        var jogos = new[]
        {
            Jogo(1, 2, Sabado.AddHours(20), quadra: "Quadra 1"),
            Jogo(3, 4, Sabado.AddHours(20).AddMinutes(13), quadra: "Quadra 2"),
        };

        var achados = AuditoriaDaGrade.Conferir(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma);

        Assert.Contains(achados, a => a.Regra == AuditoriaDaGrade.PessoaEmDoisJogos);
    }

    [Fact]
    public void Jogos_distantes_mais_que_a_duracao_nao_sao_choque()
    {
        // O outro lado da guarda acima: passar a medir por intervalo não pode virar acusação em
        // rodada seguinte. 50 minutos de partida, jogos a 50 minutos de distância — a grade
        // normal do torneio, e ela está certa.
        var mesmaPessoa = 42;
        var duplas = new[] { Dupla(1, mesmaPessoa, 11), Dupla(2, 20, 21), Dupla(3, mesmaPessoa, 31), Dupla(4, 40, 41) };
        var jogos = new[]
        {
            Jogo(1, 2, Sabado.AddHours(20), quadra: "Quadra 1"),
            Jogo(3, 4, Sabado.AddHours(20).AddMinutes(50), quadra: "Quadra 1"),
        };

        var achados = AuditoriaDaGrade.Conferir(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma);

        Assert.DoesNotContain(achados, a => a.Regra == AuditoriaDaGrade.PessoaEmDoisJogos);
    }

    // Um TIME: `NomeTime` preenchido e `Jogador2Id` nulo, com o organizador no `Jogador1Id` —
    // exatamente como TorneiosController.Times grava.
    private static Dupla TimeDe(int id, int organizadorId) => new()
    {
        Id = id, Jogador1Id = organizadorId, Jogador2Id = null, NomeTime = $"Time {id}",
        Categoria = new Categoria { Id = 1, Nome = "3ª", Codigo = "C3" },
    };
}
