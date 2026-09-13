using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using padelizou.Models;
using Xunit;

namespace Padelizou.Tests;

// AS SETAS ↑↓ DA ABA JOGOS: MOVER UM JOGO UMA LINHA (10/09/2026).
//
// 🗣️ Felipe: *"quando eu altero um jogo, no mesmo horario, ele nao esta trocando a ordem na linha,
// tem q trocar tambem para q eu possa colocar a ordem que eu quiser"*; *"por padrão, se tem
// semifinal 1 e semifinal 2 no mesmo horario, siga a ordem automatica de a 1 vir antes da 2, mas
// permita q o usuario edite"*; e *"só cuide q se colocar o jogo pra cima, ele mude o horario e
// quadra tb se tiver, e avise se atrapalhar algo com ficar 2 jogos seguidos pra alguem"*.
//
// A seta é UMA coisa só: trocar com a linha de cima (ou de baixo). Quando o vizinho está em OUTRO
// horário, o que troca é o slot inteiro — hora, quadra e clube, a máquina do ⇄ que já existia.
// Quando está no MESMO horário, o que troca é a posição dentro dele (Services/OrdemNoHorario).
public class MoverJogoNaLinhaTests
{
    private static readonly DateTime Sabado = new(2026, 9, 12);
    private static DateTime As(string hora) => DateTime.Parse($"2026-09-12 {hora}");

    private sealed record Cenario(DbPadelContext Ctx, Torneio Torneio, Jogador Organizador, Jogador Estranho,
        Categoria Categoria, Partida A, Partida B, Partida C, Partida Tarde);

    private static Cenario Montar()
    {
        var ctx = TestInfra.NovoContexto();
        var er = new Clube { Nome = "Er Padel" };
        ctx.Clubes.Add(er);
        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        var estranho = new Jogador { Nome = "Estranho", Cpf = "99900000098" };
        ctx.Jogadores.AddRange(organizador, estranho);
        ctx.SaveChanges();

        var torneio = new Torneio
        {
            Nome = "2ª Etapa ER PADEL TOUR", Codigo = "EPT2", Status = "Fase de Grupos", ClubeId = er.Id,
            DataInicio = Sabado, DataFim = Sabado.AddDays(1),
            HoraInicioDoDia = new TimeSpan(8, 0, 0), HoraInicioDiasSeguintes = new TimeSpan(8, 0, 0),
            HoraFimDoDia = new TimeSpan(23, 0, 0), QuantidadeQuadras = 3, TempoPrevistoPartidaMinutos = 50,
            SemHorarioPrevisto = true,   // o torneio do Er: a Mesa chama, a quadra é do balcão
        };
        ctx.Torneios.Add(torneio);
        ctx.SaveChanges();
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });
        var categoria = new Categoria { Nome = "5ª Masculina", Codigo = "C5M", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        int proximo = 1;
        Dupla Dupla()
        {
            var j1 = TestInfra.NovoJogador(proximo++);
            var j2 = TestInfra.NovoJogador(proximo++);
            ctx.Jogadores.AddRange(j1, j2);
            var dupla = new Dupla { Categoria = categoria, Jogador1 = j1, Jogador2 = j2 };
            ctx.Duplas.Add(dupla);
            return dupla;
        }
        Partida Jogo(string codigo, DateTime hora) => new()
        {
            TorneioId = torneio.Id, Categoria = categoria, Dupla1 = Dupla(), Dupla2 = Dupla(),
            Codigo = codigo, Status = "Agendada", Fase = "Grupo A",
            HorarioPrevisto = hora, ClubeId = er.Id,
        };
        // Três no mesmo horário (é o caso do pedido) e um mais tarde.
        var a = Jogo("AAA", As("10:40"));
        var b = Jogo("BBB", As("10:40"));
        var c = Jogo("CCC", As("10:40"));
        var tarde = Jogo("TARDE", As("11:30"));
        ctx.Partidas.AddRange(a, b, c, tarde);
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();
        return new Cenario(ctx, torneio, organizador, estranho, categoria, a, b, c, tarde);
    }

    private static Padelizou.Controllers.TorneiosController Controller(Cenario c, int? quem = null)
    {
        c.Ctx.ChangeTracker.Clear();
        return TestInfra.NovoTorneiosController(c.Ctx, quem ?? c.Organizador.Id);
    }

    private static Task<Partida> Recarregar(Cenario c, Partida jogo) =>
        c.Ctx.Partidas.AsNoTracking().SingleAsync(p => p.Id == jogo.Id);

    // ═══ O DEFEITO DO PEDIDO: dentro do mesmo horário, a troca não mudava nada ═══
    [Fact]
    public async Task Subir_dentro_do_mesmo_horario_troca_a_posicao_com_o_de_cima()
    {
        var c = Montar();
        var controller = Controller(c);

        await controller.MoverNoHorario(c.Torneio.Id, c.B.Id.ToString(), "cima");

        Assert.Null(controller.TempData["Erro"]);
        var a = await Recarregar(c, c.A);
        var b = await Recarregar(c, c.B);
        var terceiro = await Recarregar(c, c.C);

        // A hora não muda — os dois já estavam nela. O que muda é a posição.
        Assert.Equal(As("10:40"), b.HorarioPrevisto);
        Assert.Equal(As("10:40"), a.HorarioPrevisto);
        Assert.Equal(1, b.OrdemNoHorario);
        Assert.Equal(2, a.OrdemNoHorario);
        // ⚠️ O de baixo, que ninguém tocou, continua no automático.
        Assert.Null(terceiro.OrdemNoHorario);
    }

    [Fact]
    public async Task Descer_desfaz_o_subir()
    {
        var c = Montar();
        await Controller(c).MoverNoHorario(c.Torneio.Id, c.B.Id.ToString(), "cima");
        await Controller(c).MoverNoHorario(c.Torneio.Id, c.B.Id.ToString(), "baixo");

        var fila = OrdemNoHorario.Ordenar(
            await c.Ctx.Partidas.AsNoTracking().Where(p => p.TorneioId == c.Torneio.Id).ToListAsync(),
            Array.Empty<ProximasFasesDaChave.JogoQueVem>(), new Dictionary<(int, int), DateTime>());

        Assert.Equal(new[] { "AAA", "BBB", "CCC", "TARDE" }, fila.Select(l => l.Jogo!.Codigo));
    }

    // 🗣️ *"se colocar o jogo pra cima, ele mude o horario e quadra tb se tiver"* — quando o vizinho
    // de cima está em OUTRO horário, o que troca é o slot inteiro.
    [Fact]
    public async Task Subir_o_primeiro_de_um_horario_leva_a_hora_do_vizinho_de_cima()
    {
        var c = Montar();
        var controller = Controller(c);

        await controller.MoverNoHorario(c.Torneio.Id, c.Tarde.Id.ToString(), "cima");

        Assert.Null(controller.TempData["Erro"]);
        Assert.Equal(As("10:40"), (await Recarregar(c, c.Tarde)).HorarioPrevisto);
        Assert.Equal(As("11:30"), (await Recarregar(c, c.C)).HorarioPrevisto);
    }

    [Fact]
    public async Task Subir_o_primeiro_da_lista_avisa_e_nao_muda_nada()
    {
        var c = Montar();
        var controller = Controller(c);

        await controller.MoverNoHorario(c.Torneio.Id, c.A.Id.ToString(), "cima");

        Assert.NotNull(controller.TempData["Erro"]);
        Assert.Null((await Recarregar(c, c.A)).OrdemNoHorario);
        Assert.Equal(As("10:40"), (await Recarregar(c, c.A)).HorarioPrevisto);
    }

    [Fact]
    public async Task Descer_o_ultimo_da_lista_avisa_e_nao_muda_nada()
    {
        var c = Montar();
        var controller = Controller(c);

        await controller.MoverNoHorario(c.Torneio.Id, c.Tarde.Id.ToString(), "baixo");

        Assert.NotNull(controller.TempData["Erro"]);
        Assert.Equal(As("11:30"), (await Recarregar(c, c.Tarde)).HorarioPrevisto);
    }

    // Regra 0 do CLAUDE.md: ação que grava dado confere o dono.
    [Fact]
    public async Task Quem_nao_organiza_nao_move()
    {
        var c = Montar();
        var resposta = await Controller(c, c.Estranho.Id).MoverNoHorario(c.Torneio.Id, c.B.Id.ToString(), "cima");

        Assert.IsType<ForbidResult>(resposta);
        Assert.Null((await Recarregar(c, c.B)).OrdemNoHorario);
    }

    [Fact]
    public async Task Direcao_que_nao_existe_nao_move_nada()
    {
        var c = Montar();
        var controller = Controller(c);

        await controller.MoverNoHorario(c.Torneio.Id, c.B.Id.ToString(), "pro lado");

        Assert.NotNull(controller.TempData["Erro"]);
        Assert.Null((await Recarregar(c, c.B)).OrdemNoHorario);
    }

    // 🗣️ *"avise se atrapalhar algo com ficar 2 jogos seguidos pra alguem ou algo assim"*. A conta
    // é a de Services/ImpactoDaTroca, a mesma do modal do ⇄ — três lugares discordando sobre a
    // mesma troca seria pior que não avisar.
    [Fact]
    public async Task O_aviso_diz_o_que_a_mudanca_fez_com_o_Conferir_grade()
    {
        var c = Montar();
        // A mesma dupla do jogo das 11:30 também joga às 10:40: subir o TARDE põe a mesma
        // gente em dois jogos no mesmo horário — o achado mais duro da auditoria.
        var tarde = await c.Ctx.Partidas.SingleAsync(p => p.Id == c.Tarde.Id);
        var a = await c.Ctx.Partidas.SingleAsync(p => p.Id == c.A.Id);
        tarde.Dupla1Id = a.Dupla1Id;
        await c.Ctx.SaveChangesAsync();

        var controller = Controller(c);
        await controller.MoverNoHorario(c.Torneio.Id, c.Tarde.Id.ToString(), "cima");

        var aviso = ((string?)controller.TempData["Sucesso"] ?? "") + ((string?)controller.TempData["Erro"] ?? "");
        Assert.Contains(AuditoriaDaGrade.PessoaEmDoisJogos, aviso);
    }

    // O ⇄ ENTRE DOIS JOGOS DO MESMO HORÁRIO, que era o no-op reportado: hora, quadra e clube são
    // iguais dos dois lados, então a única coisa que existe pra trocar é a posição.
    [Fact]
    public async Task Trocar_horario_entre_dois_jogos_do_mesmo_horario_troca_a_posicao()
    {
        var c = Montar();
        var controller = Controller(c);

        await controller.TrocarHorario(c.Torneio.Id, c.A.Id.ToString(), c.B.Id.ToString());

        Assert.Null(controller.TempData["Erro"]);
        Assert.Equal(2, (await Recarregar(c, c.A)).OrdemNoHorario);
        Assert.Equal(1, (await Recarregar(c, c.B)).OrdemNoHorario);
    }

    // A aba Jogos entrega os agendados JÁ NA FILA — a tela desenha o que o servidor ordenou, e é
    // a mesma régua que as setas usam pra achar o vizinho.
    [Fact]
    public async Task A_aba_Jogos_entrega_os_agendados_na_ordem_da_fila()
    {
        var c = Montar();
        await Controller(c).MoverNoHorario(c.Torneio.Id, c.B.Id.ToString(), "cima");

        var controller = Controller(c);
        Assert.IsType<ViewResult>(await controller.Jogos(c.Torneio.Id, null, null));

        var agendadas = (List<Partida>)controller.ViewBag.Agendadas;
        Assert.Equal(new[] { "BBB", "AAA", "CCC", "TARDE" }, agendadas.Select(p => p.Codigo));
    }

    // 🗣️ O botão vermelho promete "desfaz as trocas feitas na mão" — a ordem manual é uma delas.
    // Deixá-la de pé faria a fila obedecer a uma escolha feita sobre uma grade que não existe mais.
    [Fact]
    public async Task Recalcular_a_grade_apaga_a_ordem_manual()
    {
        var c = Montar();
        await Controller(c).MoverNoHorario(c.Torneio.Id, c.B.Id.ToString(), "cima");
        Assert.Equal(1, (await Recarregar(c, c.B)).OrdemNoHorario);

        await Controller(c).RefazerGrade(c.Torneio.Id);

        var todos = await c.Ctx.Partidas.AsNoTracking().Where(p => p.TorneioId == c.Torneio.Id).ToListAsync();
        Assert.All(todos, p => Assert.Null(p.OrdemNoHorario));
    }

    [Fact]
    public async Task Sem_impacto_nenhum_o_aviso_nao_inventa_estrago()
    {
        var c = Montar();
        var controller = Controller(c);

        await controller.MoverNoHorario(c.Torneio.Id, c.B.Id.ToString(), "cima");

        var sucesso = (string?)controller.TempData["Sucesso"] ?? "";
        Assert.DoesNotContain("piora", sucesso);
    }
}
