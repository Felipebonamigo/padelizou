using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// "Colocar no ar" é o botão de largada, direto da lista de Jogos: no dia do torneio o
// organizador tem 4 quadras virando ao mesmo tempo e gente esperando na quadra, e abrir a
// tela de placar uma partida por vez era o único atrito que sobrou no fluxo do dia.
//
// Como é ação que GRAVA, vale a regra 0 do projeto: [Authorize] E checagem de organizador.
public class ColocarNoArTests
{
    private static PartidasController NovoController(DbPadelContext ctx, int? usuarioLogadoId) =>
        TestInfra.NovoPartidasController(ctx, usuarioLogadoId);

    private static (Partida partida, Jogador organizador, Jogador estranho) MontarAgendada(DbPadelContext ctx)
    {
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Fase de Grupos");
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).Take(2).ToList();

        var partida = new Partida
        {
            CategoriaId = categoria.Id,
            TorneioId = torneio.Id,
            Codigo = "P1",
            Fase = "Grupo A",
            Dupla1Id = duplas[0].Id,
            Dupla2Id = duplas[1].Id,
            Status = "Agendada",
            HorarioPrevisto = new DateTime(2026, 9, 4, 19, 0, 0),
        };
        ctx.Partidas.Add(partida);

        var estranho = new Jogador { Nome = "Estranho", Cpf = "88888888888" };
        ctx.Jogadores.Add(estranho);
        ctx.SaveChanges();

        return (partida, organizador, estranho);
    }

    [Fact]
    public async Task Organizador_bota_a_partida_no_ar_com_um_clique()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, organizador, _) = MontarAgendada(ctx);

        var resultado = await NovoController(ctx, organizador.Id).ColocarNoAr(partida.Id);

        var salva = await ctx.Partidas.FindAsync(partida.Id);
        Assert.Equal("AoVivo", salva!.Status);
        Assert.NotNull(salva.HorarioInicioReal);       // cronômetro começou
        Assert.Null(salva.HorarioFimReal);

        // Volta pra lista de Jogos do torneio, que é de onde o botão foi clicado.
        var redir = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Jogos", redir.ActionName);
        Assert.Equal("Torneios", redir.ControllerName);
    }

    [Fact]
    public async Task Estranho_nao_bota_no_ar_partida_de_torneio_que_nao_organiza()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, _, estranho) = MontarAgendada(ctx);

        var resultado = await NovoController(ctx, estranho.Id).ColocarNoAr(partida.Id);

        Assert.IsType<ForbidResult>(resultado);
        Assert.Equal("Agendada", (await ctx.Partidas.FindAsync(partida.Id))!.Status);
    }

    [Fact]
    public async Task Anonimo_nao_bota_no_ar()
    {
        using var ctx = TestInfra.NovoContexto();
        var (partida, _, _) = MontarAgendada(ctx);

        var resultado = await NovoController(ctx, null).ColocarNoAr(partida.Id);

        Assert.IsType<ForbidResult>(resultado);
        Assert.Equal("Agendada", (await ctx.Partidas.FindAsync(partida.Id))!.Status);
    }

    [Fact]
    public async Task Clicar_duas_vezes_nao_reinicia_o_cronometro()
    {
        // Dedo grande no celular + 3G lento = toque duplo. Se o segundo toque zerasse o
        // HorarioInicioReal, o tempo de jogo da partida em andamento seria perdido.
        using var ctx = TestInfra.NovoContexto();
        var (partida, organizador, _) = MontarAgendada(ctx);
        var controller = NovoController(ctx, organizador.Id);

        await controller.ColocarNoAr(partida.Id);
        var primeiroInicio = (await ctx.Partidas.FindAsync(partida.Id))!.HorarioInicioReal;

        await controller.ColocarNoAr(partida.Id);
        var depois = await ctx.Partidas.FindAsync(partida.Id);

        Assert.Equal(primeiroInicio, depois!.HorarioInicioReal);
        Assert.Equal("AoVivo", depois.Status);
    }

    [Fact]
    public async Task Partida_inexistente_devolve_nao_encontrado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, organizador, _) = MontarAgendada(ctx);

        Assert.IsType<NotFoundResult>(await NovoController(ctx, organizador.Id).ColocarNoAr(9999));
    }

    [Fact]
    public async Task Corrigir_placar_de_jogo_ja_encerrado_nao_chama_a_proxima_partida_de_novo()
    {
        // Corrigir placar de jogo já encerrado é rotina no meio do torneio. Se o aviso
        // "seu jogo é o próximo" saísse a cada correção, ele chamaria a partida seguinte
        // outra vez — e, como a primeira já está marcada como avisada, chamaria a SEGUINTE
        // da seguinte, tirando da beira da quadra gente que ainda vai demorar a jogar.
        using var ctx = TestInfra.NovoContexto();
        var (partida, organizador, _) = MontarAgendada(ctx);

        var seguinte = new Partida
        {
            CategoriaId = partida.CategoriaId,
            TorneioId = partida.TorneioId,
            Codigo = "P2",
            Fase = "Grupo A",
            Dupla1Id = partida.Dupla1Id,
            Dupla2Id = partida.Dupla2Id,
            Status = "Agendada",
            HorarioPrevisto = partida.HorarioPrevisto!.Value.AddMinutes(50),
        };
        var depoisDela = new Partida
        {
            CategoriaId = partida.CategoriaId,
            TorneioId = partida.TorneioId,
            Codigo = "P3",
            Fase = "Grupo A",
            Dupla1Id = partida.Dupla1Id,
            Dupla2Id = partida.Dupla2Id,
            Status = "Agendada",
            HorarioPrevisto = partida.HorarioPrevisto!.Value.AddMinutes(100),
        };
        ctx.Partidas.AddRange(seguinte, depoisDela);
        await ctx.SaveChangesAsync();

        var controller = NovoController(ctx, organizador.Id);

        // Termina de verdade: a seguinte é avisada.
        await controller.ControlePlacar(partida.Id, "Finalizada", 9, 4, null, null);
        Assert.NotNull((await ctx.Partidas.FindAsync(seguinte.Id))!.AvisoProximoEnviadoEm);
        Assert.Null((await ctx.Partidas.FindAsync(depoisDela.Id))!.AvisoProximoEnviadoEm);

        // Corrige o placar do jogo que já estava encerrado: ninguém mais é chamado.
        await controller.ControlePlacar(partida.Id, "Finalizada", 9, 5, null, null);

        Assert.Null((await ctx.Partidas.FindAsync(depoisDela.Id))!.AvisoProximoEnviadoEm);
    }

    [Fact]
    public async Task Partida_ja_finalizada_volta_pro_ar_sem_perder_o_inicio_original()
    {
        // Caso real: o organizador finaliza sem querer e precisa desfazer na hora, com os
        // jogadores ainda na quadra. O horário de início não pode ser reescrito.
        using var ctx = TestInfra.NovoContexto();
        var (partida, organizador, _) = MontarAgendada(ctx);

        var inicioOriginal = new DateTime(2026, 9, 4, 19, 3, 0);
        var emBanco = await ctx.Partidas.FindAsync(partida.Id);
        emBanco!.Status = "Finalizada";
        emBanco.HorarioInicioReal = inicioOriginal;
        emBanco.HorarioFimReal = new DateTime(2026, 9, 4, 19, 48, 0);
        await ctx.SaveChangesAsync();

        await NovoController(ctx, organizador.Id).ColocarNoAr(partida.Id);

        var salva = await ctx.Partidas.FindAsync(partida.Id);
        Assert.Equal("AoVivo", salva!.Status);
        Assert.Equal(inicioOriginal, salva.HorarioInicioReal);
        Assert.Null(salva.HorarioFimReal);
    }
}
