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
    private static PartidasController NovoController(DbPadelContext ctx, int? usuarioLogadoId)
    {
        var c = new PartidasController(ctx, Substitute.For<IPalpiteService>());

        var user = usuarioLogadoId == null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.Value.ToString()) }, "Teste"));

        c.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = user } };
        c.TempData = new TempDataDictionary(c.HttpContext, Substitute.For<ITempDataProvider>());
        return c;
    }

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
