using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using padelizou.Controllers;

namespace Padelizou.Tests;

// Dar cidade a um clube que JÁ EXISTE.
//
// Sem isto, `Clube.CidadeId` só era preenchido na criação, e o passivo não tinha conserto: em
// produção nenhum clube com torneio tinha cidade, então TODA página `/torneios-de-padel-em-...`
// respondia 404 — corretamente, e por isso mesmo em silêncio. O trabalho das páginas de cidade
// estava pronto e inerte por falta de um campo.
public class CidadeDoClubeNoAdminTests
{
    private const int Admin_ = 9, JogadorComum = 1;

    private static AdminController Admin(DbPadelContext ctx, int usuarioLogadoId)
    {
        var controller = new AdminController(
            ctx,
            Substitute.For<IPushNotificationService>(),
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>(),
            Microsoft.Extensions.Options.Options.Create(new RegistroResultadosSettings()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.ToString()) }, "Teste")),
            },
        };
        controller.TempData = new TempDataDictionary(controller.HttpContext, Substitute.For<ITempDataProvider>());
        return controller;
    }

    private static DbPadelContext Cenario()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.AddRange(
            new Jogador { Id = Admin_, Nome = "Admin", Cpf = "9", IsAdminGeral = true },
            new Jogador { Id = JogadorComum, Nome = "Jogador", Cpf = "1" });
        ctx.Cidades.Add(new Cidade { Id = 1, Nome = "Gravataí", Estado = "RS" });
        ctx.Clubes.AddRange(
            new Clube { Id = 1, Nome = "Chakra Padel" },                 // sem cidade: o caso real
            new Clube { Id = 2, Nome = "Arena Central", CidadeId = 1 });
        ctx.SaveChanges();
        return ctx;
    }

    [Fact]
    public async Task O_clube_sem_cidade_ganha_uma()
    {
        var ctx = Cenario();

        await Admin(ctx, Admin_).DefinirCidadeDoClube(1, "Porto Alegre", "RS");

        var clube = await ctx.Clubes.Include(c => c.Cidade).FirstAsync(c => c.Id == 1);
        Assert.Equal("Porto Alegre", clube.Cidade!.Nome);
        Assert.Equal("RS", clube.Cidade.Estado);
    }

    // O ponto que faz esta tela valer a pena: sem passar pelo catálogo, o admin digitando
    // "gravatai" criaria a DÉCIMA grafia de Gravataí — o problema que o CidadesSemRepetir
    // existe pra remendar depois, e que aqui dá pra evitar antes.
    [Theory]
    [InlineData("gravatai")]
    [InlineData("GRAVATAÍ")]
    [InlineData("  Gravataí  ")]
    public async Task Cidade_que_ja_existe_nao_vira_uma_grafia_nova(string digitada)
    {
        var ctx = Cenario();

        await Admin(ctx, Admin_).DefinirCidadeDoClube(1, digitada, "RS");

        Assert.Equal(1, await ctx.Cidades.CountAsync());
        Assert.Equal(1, (await ctx.Clubes.FirstAsync(c => c.Id == 1)).CidadeId);
    }

    // O desfazer de quem escolheu a cidade errada. Sem ele, corrigir um engano voltaria a ser
    // SQL na mão — que é justamente o que esta tela existe pra acabar.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Campo_vazio_tira_a_cidade(string? vazio)
    {
        var ctx = Cenario();

        await Admin(ctx, Admin_).DefinirCidadeDoClube(2, vazio, null);

        Assert.Null((await ctx.Clubes.FirstAsync(c => c.Id == 2)).CidadeId);
    }

    [Fact]
    public async Task Trocar_a_cidade_de_um_clube_nao_mexe_nos_outros()
    {
        var ctx = Cenario();

        await Admin(ctx, Admin_).DefinirCidadeDoClube(1, "Porto Alegre", "RS");

        // O Arena Central continua em Gravataí: dar cidade a um clube não é operação de lote.
        Assert.Equal(1, (await ctx.Clubes.FirstAsync(c => c.Id == 2)).CidadeId);
    }

    [Fact]
    public async Task Quem_nao_e_admin_nao_mexe_na_cidade_de_clube_nenhum()
    {
        var ctx = Cenario();

        var resposta = await Admin(ctx, JogadorComum).DefinirCidadeDoClube(1, "Porto Alegre", "RS");

        Assert.IsType<ForbidResult>(resposta);
        Assert.Null((await ctx.Clubes.FirstAsync(c => c.Id == 1)).CidadeId);
    }

    [Fact]
    public async Task Clube_que_nao_existe_devolve_404()
    {
        var ctx = Cenario();

        Assert.IsType<NotFoundResult>(await Admin(ctx, Admin_).DefinirCidadeDoClube(999, "Porto Alegre", "RS"));
    }

    // O teste que amarra as duas pontas: é a cidade do clube que faz o torneio dele aparecer
    // numa página de cidade. Sem ela, a página não existe — e é assim que a produção estava.
    [Fact]
    public async Task Dar_cidade_ao_clube_faz_a_pagina_da_cidade_NASCER()
    {
        var ctx = Cenario();
        ctx.Torneios.Add(new Torneio
        {
            Id = 1,
            Nome = "THE LAST DANCE",
            Codigo = "TLD",
            ClubeId = 1,
            AprovadoEm = new DateTime(2026, 8, 1),
            Status = "Inscrições Abertas",
            DataInicio = new DateTime(2026, 11, 12),
        });
        await ctx.SaveChangesAsync();

        // Antes: o clube não tem cidade, então não há página nenhuma.
        Assert.Empty(await TorneiosPorCidade.ListarAsync(ctx));

        await Admin(ctx, Admin_).DefinirCidadeDoClube(1, "Porto Alegre", "RS");

        var lugares = await TorneiosPorCidade.ListarAsync(ctx);
        var poa = Assert.Single(lugares);
        Assert.Equal("porto-alegre-rs", poa.Slug);
        Assert.Equal(1, poa.Torneios);
    }
}
