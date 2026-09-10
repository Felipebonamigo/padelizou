using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A PASSADA DE REPARO: o que o encaixe guloso não consegue enxergar.
//
// 🗣️ Felipe, 10/09/2026, depois de arrumar horário na mão a noite inteira: *"temos q pensar melhor
// esse botão q ele seja mais inteligente, por que hoje ele refaz tudo e as vezes deixa impedimentos
// ainda [...] tem algo que o sistema esta fazendo que esta deixando a desejar no sorteio"*.
//
// 🕳️ POR QUE O GULOSO DEIXA IMPEDIMENTO PASSAR. `GradeDeJogos.Encaixar` anda vaga por vaga e nunca
// volta atrás: quando as vagas que sobram são menos que os jogos da fila, ele cai no ÚLTIMO RECURSO
// e marca o jogo onde der — inclusive dentro do impedimento da dupla. No instante em que isso
// acontece a decisão é correta (um jogo ruim é melhor que um jogo sem horário), mas dez vagas
// atrás havia um jogo SEM restrição nenhuma que teria cabido ali. O guloso não pode saber disso; o
// reparo pode, porque olha a grade inteira DEPOIS de pronta.
//
// A régua do reparo é a MESMA da tela (AuditoriaDaGrade.Conferir): ele minimiza exatamente os
// pontos que o Conferir grade mostra. Duas coisas não podem sair diferentes.
//
// ⚠️ A ORDEM DE QUEM CEDE É DO FELIPE: *"a prioridade é impedimento por que é pago, aquele de 'os 2
// jogos na sexta a noite' é só se der"*. Impedimento (e pessoa em dois jogos ao mesmo tempo) são
// DUROS: o reparo nunca aceita uma troca que aumente esses. Concentração é MOLE alta, jogos
// seguidos é MOLE baixa — conforto.
public class ReparoDaGradeTests
{
    private static readonly DateTime Sexta = new(2026, 10, 9);
    private static readonly DateTime Sabado = new(2026, 10, 10);

    private static Torneio Torneio() => new()
    {
        Id = 1, Nome = "T", Codigo = "T1",
        DataInicio = Sexta.AddHours(18),
        DataFim = Sabado.AddDays(1),
        QuantidadeQuadras = 2,
        TempoPrevistoPartidaMinutos = 50,
        HoraInicioDoDia = new TimeSpan(18, 0, 0),
        HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
        HoraFimDoDia = new TimeSpan(23, 0, 0),
    };

    private static readonly Categoria Cat = new() { Id = 1, Nome = "3ª", Codigo = "C3" };

    private static Dupla Dupla(int id, int j1, int j2) =>
        new() { Id = id, Jogador1Id = j1, Jogador2Id = j2, Categoria = Cat };

    private static Partida Jogo(int id, int d1, int d2, DateTime quando, string fase = "Grupo A") =>
        new()
        {
            Id = id, TorneioId = 1, Codigo = $"J{id}", Status = "Agendada",
            Fase = fase, CategoriaId = 1, Categoria = Cat,
            Dupla1Id = d1, Dupla2Id = d2, HorarioPrevisto = quando,
        };

    private static int Quantos(Torneio torneio, IReadOnlyCollection<Partida> jogos,
        IReadOnlyCollection<Dupla> duplas, string regra) =>
        AuditoriaDaGrade.Conferir(torneio, jogos, duplas, SedesDoTorneio.Nenhuma).Count(a => a.Regra == regra);

    // Um TIME: `NomeTime` preenchido, `Jogador2Id` nulo e o ORGANIZADOR no `Jogador1Id` — é assim
    // que TorneiosController.Times grava, e é por isso que time fica fora do mapa de pessoas
    // (comparar por pessoa faria todo time brigar com todo time).
    private static Dupla TimeDe(int id, int organizadorId) => new()
    {
        Id = id, Jogador1Id = organizadorId, Jogador2Id = null, NomeTime = $"Time {id}", Categoria = Cat,
    };

    // 🕳️ O REPARO CHAMAVA O MESMO TIME PRA DUAS QUADRAS NO MESMO HORÁRIO (10/09/2026).
    //
    // A régua do reparo é `AuditoriaDaGrade.Conferir`, e ela PULAVA quem não está no mapa de
    // pessoas — ou seja, todo time — em vez de cair na identidade do próprio time, que é o que
    // `GradeDeJogos.Encaixar` faz (`new[] { -duplaId }`). Time em dois jogos ao mesmo tempo pesava
    // ZERO: o reparo trocava de graça um impedimento de 20.000 por um choque que ele não
    // enxergava, e o "Conferir grade" saía dizendo "nada fora do lugar".
    //
    // Medido antes da correção: `ChaveDiretaNoSorteioTests.Torneio_completo_com_categorias_times_e_
    // chave_direta_na_mesma_grade` falhava ~1 em 60 sorteios, e o choque NUNCA existia antes do
    // reparo — era ele quem o criava. Aqui os confrontos são FIXOS: número que sai do `GerarChaves`
    // mede sorte, não código.
    [Fact]
    public void Nao_troca_criando_o_mesmo_time_em_duas_quadras_ao_mesmo_tempo()
    {
        const int organizador = 777;
        var impedida = Dupla(5, 50, 51);
        impedida.ImpedimentoSextaNoite = true;
        var duplas = new[]
        {
            TimeDe(1, organizador), TimeDe(2, organizador), TimeDe(3, organizador),
            impedida, Dupla(6, 60, 61),
        };

        var jogos = new List<Partida>
        {
            Jogo(1, 1, 2, Sabado.AddHours(9)),          // Time 1 aqui…
            Jogo(2, 1, 3, Sexta.AddHours(20)),          // …e aqui
            Jogo(3, 5, 6, Sexta.AddHours(20)),          // dentro do impedimento: é o doente
        };

        // A isca: trocar o jogo 3 com o jogo 1 apaga o impedimento (20.000) — e joga o Time 1 pra
        // cima do próprio Time 1 das 20h de sexta.
        Assert.Equal(1, Quantos(Torneio(), jogos, duplas, AuditoriaDaGrade.Impedimento));
        Assert.Equal(0, Quantos(Torneio(), jogos, duplas, AuditoriaDaGrade.PessoaEmDoisJogos));

        ReparoDaGrade.Reparar(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma);

        Assert.Equal(0, Quantos(Torneio(), jogos, duplas, AuditoriaDaGrade.PessoaEmDoisJogos));
        // E dito de frente, sem depender da auditoria: os dois jogos do Time 1 em horários
        // diferentes. Um jogo dentro do impedimento é ruim; o time chamado pra duas quadras ao
        // mesmo tempo é pior, e é o único desfecho que a Mesa não consegue remendar no dia.
        Assert.NotEqual(jogos[0].HorarioPrevisto, jogos[1].HorarioPrevisto);
    }

    // O caso do Er: um jogo caiu dentro do impedimento e existe, na grade, um jogo sem restrição
    // nenhuma que pode ficar com aquele horário.
    [Fact]
    public void Tira_o_jogo_de_dentro_do_impedimento_trocando_com_um_jogo_livre()
    {
        var impedida = Dupla(1, 10, 11);
        impedida.ImpedimentoSextaNoite = true;
        var duplas = new[] { impedida, Dupla(2, 20, 21), Dupla(3, 30, 31), Dupla(4, 40, 41) };

        var jogos = new List<Partida>
        {
            Jogo(1, 1, 2, Sexta.AddHours(20)),          // dentro do impedimento
            Jogo(2, 3, 4, Sabado.AddHours(9)),          // ninguém aqui tem restrição
        };

        Assert.Equal(1, Quantos(Torneio(), jogos, duplas, AuditoriaDaGrade.Impedimento));

        ReparoDaGrade.Reparar(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma);

        Assert.Equal(0, Quantos(Torneio(), jogos, duplas, AuditoriaDaGrade.Impedimento));
        Assert.Equal(Sabado.AddHours(9), jogos[0].HorarioPrevisto);
        Assert.Equal(Sexta.AddHours(20), jogos[1].HorarioPrevisto);
    }

    // ⚠️ A contrapartida, e é a regra do Felipe: o reparo NÃO compra um impedimento pra pagar um
    // conforto. Aqui a única troca que tiraria os jogos seguidos põe a dupla impedida na sexta.
    [Fact]
    public void Nao_cria_impedimento_pra_consertar_jogo_seguido()
    {
        var impedida = Dupla(1, 10, 11);
        impedida.ImpedimentoSextaNoite = true;
        var duplas = new[] { impedida, Dupla(2, 20, 21), Dupla(3, 30, 31) };

        // A dupla 2 joga 09:00 e 09:50 no sábado — seguidos. O único outro slot é a sexta 20:00,
        // e quem está lá é a dupla impedida... que não pode ir pra lá de novo, nem ceder o lugar
        // a quem a jogaria junto de outro jogo.
        var jogos = new List<Partida>
        {
            Jogo(1, 1, 3, Sabado.AddHours(20)),
            Jogo(2, 2, 3, Sabado.AddHours(9)),
            Jogo(3, 2, 1, Sabado.AddHours(9).AddMinutes(50)),
        };

        var antes = jogos.ToDictionary(j => j.Id, j => j.HorarioPrevisto);

        ReparoDaGrade.Reparar(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma);

        Assert.Equal(0, Quantos(Torneio(), jogos, duplas, AuditoriaDaGrade.Impedimento));
        Assert.Equal(0, Quantos(Torneio(), jogos, duplas, AuditoriaDaGrade.PessoaEmDoisJogos));
        // E nenhum jogo foi parar na sexta à noite, que é onde a impedida não joga.
        Assert.DoesNotContain(jogos, j => j.HorarioPrevisto!.Value.Date == Sexta.Date
                                       && (j.Dupla1Id == 1 || j.Dupla2Id == 1));
    }

    // Concentração cede pra impedimento: *"aquele de 'os 2 jogos na sexta a noite' é só se der"*.
    [Fact]
    public void Impedimento_vence_concentracao_quando_os_dois_nao_cabem()
    {
        var impedida = Dupla(1, 10, 11);
        impedida.ImpedimentoSextaNoite = true;
        var concentrada = Dupla(2, 20, 21);
        concentrada.ConcentrarJogosEm = TurnoDeConcentracao.SextaNoite;
        var duplas = new[] { impedida, concentrada, Dupla(3, 30, 31), Dupla(4, 40, 41) };

        // A impedida está na sexta (errado) e a concentrada no sábado (errado). A troca conserta
        // os DOIS de uma vez — é o reparo fazendo o que o guloso não fez.
        var jogos = new List<Partida>
        {
            Jogo(1, 1, 3, Sexta.AddHours(20)),
            Jogo(2, 2, 4, Sabado.AddHours(9)),
        };

        ReparoDaGrade.Reparar(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma);

        Assert.Equal(0, Quantos(Torneio(), jogos, duplas, AuditoriaDaGrade.Impedimento));
        Assert.Equal(0, Quantos(Torneio(), jogos, duplas, AuditoriaDaGrade.Concentracao));
    }

    // Grade limpa não é mexida: o reparo não troca por trocar — o organizador reconhece a grade
    // dele depois de apertar o botão.
    [Fact]
    public void Grade_sem_achado_nenhum_fica_intacta()
    {
        var duplas = new[] { Dupla(1, 10, 11), Dupla(2, 20, 21), Dupla(3, 30, 31), Dupla(4, 40, 41) };
        var jogos = new List<Partida>
        {
            Jogo(1, 1, 2, Sabado.AddHours(9)),
            Jogo(2, 3, 4, Sabado.AddHours(14)),
        };
        var antes = jogos.ToDictionary(j => j.Id, j => j.HorarioPrevisto);

        var resultado = ReparoDaGrade.Reparar(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma);

        Assert.Equal(0, resultado.Trocas);
        Assert.All(jogos, j => Assert.Equal(antes[j.Id], j.HorarioPrevisto));
    }

    // Jogo já jogado ou em quadra é história: o reparo não o move (mesma régua da troca na mão).
    [Fact]
    public void Nao_mexe_em_jogo_que_ja_comecou()
    {
        var impedida = Dupla(1, 10, 11);
        impedida.ImpedimentoSextaNoite = true;
        var duplas = new[] { impedida, Dupla(2, 20, 21), Dupla(3, 30, 31), Dupla(4, 40, 41) };

        var jogos = new List<Partida>
        {
            Jogo(1, 1, 2, Sexta.AddHours(20)),
            Jogo(2, 3, 4, Sabado.AddHours(9)),
        };
        jogos[1].Status = "Finalizada";                 // o único destino possível está travado

        ReparoDaGrade.Reparar(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma);

        Assert.Equal(Sabado.AddHours(9), jogos[1].HorarioPrevisto);
        Assert.Equal(Sexta.AddHours(20), jogos[0].HorarioPrevisto);
    }

    // O que o botão conta pro organizador: quantos pontos caíram.
    //
    // ⚠️ SÃO TRÊS JOGOS, e o terceiro não é enfeite: com um jogo na sexta e UM no sábado, o de
    // sábado é retardatário (menos de uma rodada depois do buraco, ver OrdemDasFases.FimDoBloco) e
    // a grade nasceria com 2 achados em vez de 1 — a conta que este teste afirma ficaria ambígua.
    // Dois jogos no mesmo horário de sábado fecham uma rodada das 2 quadras e o incidental some.
    [Fact]
    public void Conta_quantos_pontos_caiu()
    {
        var impedida = Dupla(1, 10, 11);
        impedida.ImpedimentoSextaNoite = true;
        var duplas = new[] { impedida, Dupla(2, 20, 21), Dupla(3, 30, 31), Dupla(4, 40, 41),
                             Dupla(5, 50, 51), Dupla(6, 60, 61) };
        var jogos = new List<Partida>
        {
            Jogo(1, 1, 2, Sexta.AddHours(20)),
            Jogo(2, 3, 4, Sabado.AddHours(9)),
            Jogo(3, 5, 6, Sabado.AddHours(9)),
        };

        var resultado = ReparoDaGrade.Reparar(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma);

        Assert.Equal(1, resultado.Trocas);
        Assert.Equal(1, resultado.AchadosAntes);
        Assert.Equal(0, resultado.AchadosDepois);
    }

    // 🕳️ O REPARO DO SORTEIO NÃO TROCAVA NADA (10/09/2026, achado pela intermitência de
    // GradeDoErMedidaTests.Refazer_grade_sem_nada_mudado_reproduz_a_grade_do_sorteio: 3 falhas em 8
    // rodadas, sozinho, sem paralelismo). No GerarChaves o reparo roda ANTES do AddRange, quando
    // todo jogo novo ainda tem Id 0 — e `TrocaDeHorario.MotivoParaNaoTrocar` lia `a.Id == b.Id` como
    // "é o mesmo jogo" e recusava TODA troca. O sorteio saía sem reparo; o Refazer, com os Ids
    // gravados, reparava. Mesmas entradas, mesmo motor, grade diferente — de novo.
    //
    // O mesmo cenário de Conta_quantos_pontos_caiu, só que ninguém tem Id ainda.
    [Fact]
    public void Jogos_recem_sorteados_ainda_sem_Id_tambem_sao_trocados()
    {
        var impedida = Dupla(1, 10, 11);
        impedida.ImpedimentoSextaNoite = true;
        var duplas = new[] { impedida, Dupla(2, 20, 21), Dupla(3, 30, 31), Dupla(4, 40, 41),
                             Dupla(5, 50, 51), Dupla(6, 60, 61) };
        var jogos = new List<Partida>
        {
            Jogo(0, 1, 2, Sexta.AddHours(20)),
            Jogo(0, 3, 4, Sabado.AddHours(9)),
            Jogo(0, 5, 6, Sabado.AddHours(9)),
        };
        jogos[0].Codigo = "AAA"; jogos[1].Codigo = "BBB"; jogos[2].Codigo = "CCC";

        var resultado = ReparoDaGrade.Reparar(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma);

        Assert.Equal(1, resultado.Trocas);
        Assert.Equal(0, Quantos(Torneio(), jogos, duplas, AuditoriaDaGrade.Impedimento));
    }

    // 🕳️ ZERO DE FOLGA NÃO É "UM PONTO" (10/09/2026, achado no minuto em que o reparo do sorteio
    // passou a funcionar: DescansoNaFaseDeGruposTests e UmRoboSoDeChaveamentoTests caíram). O
    // "Jogos seguidos" pesava 10 tanto pra "1 horário de descanso" quanto pra "0" — e o reparo,
    // que conta pontos, EMENDAVA dois jogos de uma dupla (0 de folga) pra apagar dois avisos de
    // "1 de folga" de duplas diferentes: 8 pontos → 7 → 6, e três pessoas jogando sem sair da
    // quadra. É o contrário do pedido (🗣️ *"tem jogos seguidos dos mesmos jogadores, isso temos q
    // evitar"*): faltar DOIS horários é mais que o dobro de faltar um.
    //
    // Duas duplas com um horário de descanso cada; a única troca que "tira um ponto" emenda uma
    // delas. Fica como está.
    [Fact]
    public void Nao_emenda_dois_jogos_de_uma_dupla_pra_tirar_um_ponto_de_um_horario_de_descanso()
    {
        var duplas = Enumerable.Range(1, 8).Select(i => Dupla(i, i * 10, i * 10 + 1)).ToArray();
        var jogos = new List<Partida>
        {
            Jogo(1, 1, 2, Sexta.AddHours(18)),
            Jogo(2, 5, 6, Sexta.AddHours(18).AddMinutes(50), "Grupo B"),
            Jogo(3, 1, 3, Sexta.AddHours(19).AddMinutes(40)),                 // dupla 1: 1 de folga
            Jogo(4, 5, 7, Sexta.AddHours(20).AddMinutes(30), "Grupo B"),      // dupla 5: 1 de folga
        };
        var antes = jogos.ToDictionary(j => j.Id, j => j.HorarioPrevisto);

        var resultado = ReparoDaGrade.Reparar(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma);

        Assert.Equal(0, resultado.Trocas);
        Assert.All(jogos, j => Assert.Equal(antes[j.Id], j.HorarioPrevisto));
        Assert.DoesNotContain(AuditoriaDaGrade.Conferir(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma),
            a => a.Regra == AuditoriaDaGrade.JogosSeguidos && a.Descricao.Contains("0 horário"));
    }

    // O espelho: uma dupla emendada (0 de folga) e uma troca que lhe dá pelo menos um horário. O
    // reparo faz — antes ele via "1 ponto → 1 ponto" e deixava a emenda de pé.
    [Fact]
    public void Prefere_um_horario_de_descanso_a_nenhum()
    {
        var duplas = Enumerable.Range(1, 8).Select(i => Dupla(i, i * 10, i * 10 + 1)).ToArray();
        var jogos = new List<Partida>
        {
            Jogo(1, 1, 2, Sexta.AddHours(18)),
            Jogo(2, 1, 3, Sexta.AddHours(18).AddMinutes(50)),                 // dupla 1 emendada
            Jogo(3, 5, 6, Sexta.AddHours(19).AddMinutes(40), "Grupo B"),
            Jogo(4, 7, 8, Sexta.AddHours(20).AddMinutes(30), "Grupo B"),
        };

        var resultado = ReparoDaGrade.Reparar(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma);

        Assert.True(resultado.Trocas >= 1);
        Assert.DoesNotContain(AuditoriaDaGrade.Conferir(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma),
            a => a.Regra == AuditoriaDaGrade.JogosSeguidos && a.Descricao.Contains("0 horário"));
    }

    // 🕳️ A SEMIFINAL QUE SUBIU PRO MEIO DOS GRUPOS (10/09/2026, achado por
    // AberturaDoMataMataPorCategoriaTests no minuto em que a emenda passou a pesar 40). O reparo
    // trocou a semifinal (emendada com a final) com um jogo de grupo de duas horas antes. Pra
    // régua da tela isso NÃO é "fase fora de ordem": o jogo de grupo que foi parar depois vira
    // "retardatário" (5 pontos, o bloco dos grupos "fecha" sem ele), e a semifinal fica de pé no
    // horário que era dele. 40 − 5: negócio da China — e o torneio jogando uma eliminatória antes
    // de fechar as chaves, que é o que o Felipe mandou nunca fazer.
    //
    // A régua: o reparo troca jogos DO MESMO POSTO de fase. Grupo com grupo, semifinal com
    // semifinal. Assim o conjunto de horários de cada posto não muda, e a ordem das fases fica
    // exatamente como o encaixe deixou.
    [Fact]
    public void Nao_troca_uma_eliminatoria_com_um_jogo_de_grupo()
    {
        var duplas = Enumerable.Range(1, 11).Select(i => Dupla(i, i * 10, i * 10 + 1)).ToArray();
        var jogos = new List<Partida>
        {
            Jogo(1, 1, 2, Sexta.AddHours(18)),
            Jogo(2, 3, 4, Sexta.AddHours(18)),
            Jogo(3, 8, 9, Sexta.AddHours(18).AddMinutes(50)),
            Jogo(4, 10, 11, Sexta.AddHours(18).AddMinutes(50)),
            Jogo(5, 5, 6, Sexta.AddHours(20).AddMinutes(30), "Semifinal"),
            Jogo(6, 5, 7, Sexta.AddHours(21).AddMinutes(20), "Final"),   // dupla 5 emendada
        };

        var resultado = ReparoDaGrade.Reparar(Torneio(), jogos, duplas, SedesDoTorneio.Nenhuma);

        var ultimoGrupo = jogos.Where(j => FasesTorneio.EhFaseDeGrupos(j.Fase)).Max(j => j.HorarioPrevisto);
        var primeiraEliminatoria = jogos.Where(j => !FasesTorneio.EhFaseDeGrupos(j.Fase)).Min(j => j.HorarioPrevisto);
        Assert.True(primeiraEliminatoria > ultimoGrupo,
            $"eliminatória às {primeiraEliminatoria:HH:mm} com jogo de grupo às {ultimoGrupo:HH:mm}");
        Assert.Equal(0, resultado.Trocas);     // a única troca que ajudaria a dupla 5 é a proibida
    }

    // ── A FIAÇÃO DO BOTÃO ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Quem_nao_organiza_nao_ajusta_horarios()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2);
        var intruso = new Jogador { Nome = "Intruso", Cpf = "99900000077" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, intruso.Id).AjustarHorarios(torneio.Id);

        Assert.IsType<ForbidResult>(resultado);
    }

    // O botão manso NÃO desmarca nada: é o que o separa do "Recalcular horários".
    [Fact]
    public async Task Ajustar_horarios_nao_deixa_jogo_sem_horario()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        var antes = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id)
            .Select(p => new { p.Id, p.HorarioPrevisto }).ToListAsync();
        Assert.NotEmpty(antes);

        await controller.AjustarHorarios(torneio.Id);

        var depois = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();
        Assert.All(depois, p => Assert.NotNull(p.HorarioPrevisto));
        Assert.Equal(antes.Count, depois.Count);
    }
}
