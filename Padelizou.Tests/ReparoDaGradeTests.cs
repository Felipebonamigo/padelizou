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
