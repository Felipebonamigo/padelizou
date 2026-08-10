using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using padelizou.Controllers;

namespace Padelizou.Tests;

// Renomear clube em /Admin/Clubes. O clube nasce do que o jogador digitou no cadastro
// (CatalogoLocais), então a lista real tem "Batata" e "Rogérinho." ao lado dos nomes certos.
//
// O que estes testes guardam é o LIMITE do botão: renomear arruma a grafia, e NÃO junta dois
// clubes. Como o catálogo casa clube por NOME (sem diferenciar maiúsculas), deixar dois com o
// mesmo nome faria o próximo cadastro cair num dos dois pelo acaso da consulta — e metade dos
// jogadores ficaria pendurada no clube errado, calada.
public class RenomearClubeTests
{
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
            new Jogador { Id = 9, Nome = "Admin", Cpf = "9", IsAdminGeral = true },
            new Jogador { Id = 1, Nome = "Jogador comum", Cpf = "1" });
        ctx.Clubes.AddRange(
            new Clube { Id = 1, Nome = "Batata" },
            new Clube { Id = 2, Nome = "Nata Padel" });
        ctx.SaveChanges();
        return ctx;
    }

    [Fact]
    public async Task Admin_arruma_o_nome_do_clube()
    {
        using var ctx = Cenario();

        await Admin(ctx, usuarioLogadoId: 9).RenomearClube(clubeId: 1, nome: "  Batata Padel  ");

        // Espaço na ponta some: o nome vai virar chave de comparação no catálogo.
        Assert.Equal("Batata Padel", ctx.Clubes.Find(1)!.Nome);
    }

    [Fact]
    public async Task Nome_que_ja_e_de_outro_clube_e_recusado()
    {
        using var ctx = Cenario();

        // Maiúscula diferente é o mesmo nome pro catálogo — se passasse aqui, passaria o
        // problema inteiro.
        var resposta = await Admin(ctx, usuarioLogadoId: 9).RenomearClube(clubeId: 1, nome: "nata padel");

        Assert.IsType<RedirectToActionResult>(resposta);
        Assert.Equal("Batata", ctx.Clubes.Find(1)!.Nome);
    }

    [Fact]
    public async Task Nome_vazio_nao_apaga_o_nome_do_clube()
    {
        using var ctx = Cenario();

        await Admin(ctx, usuarioLogadoId: 9).RenomearClube(clubeId: 1, nome: "   ");

        Assert.Equal("Batata", ctx.Clubes.Find(1)!.Nome);
    }

    [Fact]
    public async Task Salvar_o_mesmo_nome_de_novo_nao_bate_contra_ele_mesmo()
    {
        using var ctx = Cenario();

        await Admin(ctx, usuarioLogadoId: 9).RenomearClube(clubeId: 1, nome: "Batata");

        Assert.Equal("Batata", ctx.Clubes.Find(1)!.Nome);
    }

    [Fact]
    public async Task Quem_nao_e_admin_nao_renomeia()
    {
        using var ctx = Cenario();

        var resposta = await Admin(ctx, usuarioLogadoId: 1).RenomearClube(clubeId: 1, nome: "Meu Clube");

        Assert.IsType<ForbidResult>(resposta);
        Assert.Equal("Batata", ctx.Clubes.Find(1)!.Nome);
    }
}
