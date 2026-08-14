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

// A tela /Admin/Times: criar, apagar e designar quem administra.
//
// Ela existe porque dentro de admin.padelizou.com.br o /Times do site público dá 404 — e era
// lá que ficava o único botão capaz de dar o PRIMEIRO administrador a um time (os 44
// importados do ranking nasceram sem nenhum).
//
// O que estes testes guardam são os dois estragos possíveis: um nome de time DUPLICADO, que
// divide um time em dois em silêncio, e um APAGAR que leve gente junto.
public class TimesNoAdminTests
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
            new Jogador { Id = 9, Nome = "Admin do sistema", Cpf = "9", IsAdminGeral = true },
            new Jogador { Id = 1, Nome = "Rafael", Cpf = "1" },
            new Jogador { Id = 2, Nome = "Bruno", Cpf = "2" });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Nata Padel" });
        ctx.Times.Add(new Time { Id = 10, Nome = "SINDAQUA" });
        ctx.SaveChanges();
        return ctx;
    }

    // ── Criar ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Admin_cria_time_com_clube_sede()
    {
        using var ctx = Cenario();

        await Admin(ctx, usuarioLogadoId: 9).CriarTime("  Nata Padel Team  ", clubeIds: new[] { 1 });

        var criado = ctx.Times.Single(t => t.Nome == "Nata Padel Team");
        Assert.Equal(1, ctx.TimeSedes.Single(s => s.TimeId == criado.Id).ClubeId);
    }

    [Fact]
    public async Task Time_criado_nasce_sem_administrador()
    {
        // Criar não faz do admin do sistema o dono: o comando é designado no passo seguinte.
        using var ctx = Cenario();

        await Admin(ctx, usuarioLogadoId: 9).CriarTime("Time Novo", clubeIds: null);

        var criado = ctx.Times.Single(t => t.Nome == "Time Novo");
        Assert.Empty(ctx.TimeAdministradores.Where(a => a.TimeId == criado.Id));
    }

    [Fact]
    public async Task Nome_que_ja_e_de_outro_time_e_recusado()
    {
        // ⚠️ O estrago aqui é mudo. No cadastro, quem digita o nome de um time existente ENTRA
        // nele (AuthController.DefinirTimeAsync casa por nome, sem diferenciar maiúsculas) —
        // com dois "SINDAQUA" na base, cada pessoa cai num dos dois pelo acaso da consulta.
        using var ctx = Cenario();

        await Admin(ctx, usuarioLogadoId: 9).CriarTime("sindaqua", clubeIds: null);

        Assert.Equal(1, await ctx.Times.CountAsync());
    }

    [Fact]
    public async Task Nome_vazio_nao_cria_time()
    {
        using var ctx = Cenario();

        await Admin(ctx, usuarioLogadoId: 9).CriarTime("   ", clubeIds: null);

        Assert.Equal(1, await ctx.Times.CountAsync());
    }

    [Fact]
    public async Task Clube_sede_inexistente_vira_sem_sede_em_vez_de_estourar()
    {
        using var ctx = Cenario();

        await Admin(ctx, usuarioLogadoId: 9).CriarTime("Time Sem Sede", clubeIds: new[] { 4242 });

        var criado = ctx.Times.Single(t => t.Nome == "Time Sem Sede");
        Assert.Empty(ctx.TimeSedes.Where(s => s.TimeId == criado.Id));
    }

    [Fact]
    public async Task Quem_nao_e_admin_nao_cria_time()
    {
        using var ctx = Cenario();

        var resposta = await Admin(ctx, usuarioLogadoId: 1).CriarTime("Time do Penetra", clubeIds: null);

        Assert.IsType<ForbidResult>(resposta);
        Assert.Equal(1, await ctx.Times.CountAsync());
    }

    // ── Apagar ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Apagar_o_time_deixa_o_jogador_sem_time_mas_nao_apaga_a_conta()
    {
        using var ctx = Cenario();
        var rafael = await ctx.Jogadores.FindAsync(1);
        rafael!.TimeId = 10;
        await ctx.SaveChangesAsync();

        await Admin(ctx, usuarioLogadoId: 9).ExcluirTime(timeId: 10);

        Assert.Empty(ctx.Times);
        Assert.NotNull(await ctx.Jogadores.FindAsync(1));      // a conta segue inteira
        Assert.Null((await ctx.Jogadores.FindAsync(1))!.TimeId); // só perdeu a camisa
    }

    [Fact]
    public async Task Apagar_o_time_nao_apaga_a_historia_do_torneio()
    {
        // A lição do Torneio.ClubeId em cascade: a dupla-time de um torneio antigo perde o
        // escudo na tela, e nada além disso.
        using var ctx = Cenario();
        ctx.Duplas.Add(new Dupla { Id = 77, CategoriaId = 1, Jogador1Id = 1, NomeTime = "SINDAQUA", TimeId = 10 });
        await ctx.SaveChangesAsync();

        await Admin(ctx, usuarioLogadoId: 9).ExcluirTime(timeId: 10);

        var dupla = await ctx.Duplas.FindAsync(77);
        Assert.NotNull(dupla);
        Assert.Null(dupla!.TimeId);
        Assert.Equal("SINDAQUA", dupla.NomeTime);   // o nome escrito no torneio não se mexe
    }

    [Fact]
    public async Task Apagar_o_time_leva_junto_o_cargo_de_quem_administrava()
    {
        using var ctx = Cenario();
        ctx.TimeAdministradores.Add(new TimeAdministrador { TimeId = 10, JogadorId = 1, ConcedidoEm = DateTime.Now });
        await ctx.SaveChangesAsync();

        await Admin(ctx, usuarioLogadoId: 9).ExcluirTime(timeId: 10);

        Assert.Empty(ctx.TimeAdministradores);
    }

    [Fact]
    public async Task Quem_nao_e_admin_nao_apaga_time()
    {
        using var ctx = Cenario();

        var resposta = await Admin(ctx, usuarioLogadoId: 1).ExcluirTime(timeId: 10);

        Assert.IsType<ForbidResult>(resposta);
        Assert.Equal(1, await ctx.Times.CountAsync());
    }

    [Fact]
    public async Task Time_inexistente_nao_quebra_a_tela()
    {
        using var ctx = Cenario();

        Assert.IsType<NotFoundResult>(await Admin(ctx, usuarioLogadoId: 9).ExcluirTime(timeId: 9999));
        Assert.IsType<NotFoundResult>(await Admin(ctx, usuarioLogadoId: 9).IncluirAdministradorDoTime(9999, 1));
    }

    // ── Administrador do time ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Admin_do_sistema_designa_o_primeiro_administrador_e_ele_veste_a_camisa()
    {
        // Exatamente o caso dos 44 importados do ranking, agora resolvível de dentro do painel.
        using var ctx = Cenario();

        await Admin(ctx, usuarioLogadoId: 9).IncluirAdministradorDoTime(timeId: 10, jogadorId: 1);

        Assert.True(await ctx.TimeAdministradores.AnyAsync(a => a.TimeId == 10 && a.JogadorId == 1));
        // Sem isto, administraria um time do qual não faz parte — e sumiria da lista de membros.
        Assert.Equal(10, (await ctx.Jogadores.FindAsync(1))!.TimeId);
    }

    [Fact]
    public async Task Fica_registrado_quem_concedeu()
    {
        // "Quem te colocou aí?" é a primeira pergunta quando alguém reclama de acesso.
        using var ctx = Cenario();

        await Admin(ctx, usuarioLogadoId: 9).IncluirAdministradorDoTime(timeId: 10, jogadorId: 1);

        Assert.Equal(9, ctx.TimeAdministradores.Single().ConcedidoPorId);
    }

    [Fact]
    public async Task Nao_da_pra_incluir_a_mesma_pessoa_duas_vezes()
    {
        using var ctx = Cenario();
        await Admin(ctx, usuarioLogadoId: 9).IncluirAdministradorDoTime(timeId: 10, jogadorId: 1);

        await Admin(ctx, usuarioLogadoId: 9).IncluirAdministradorDoTime(timeId: 10, jogadorId: 1);

        Assert.Equal(1, await ctx.TimeAdministradores.CountAsync(a => a.TimeId == 10));
    }

    [Fact]
    public async Task Admin_do_sistema_remove_ate_o_unico_administrador()
    {
        // A trava de "não deixe o time sem ninguém" é pro administrador do próprio time: quem
        // destrava de volta é o admin do sistema, e é esta tela.
        using var ctx = Cenario();
        ctx.TimeAdministradores.Add(new TimeAdministrador { TimeId = 10, JogadorId = 1, ConcedidoEm = DateTime.Now });
        await ctx.SaveChangesAsync();

        await Admin(ctx, usuarioLogadoId: 9).RemoverAdministradorDoTime(timeId: 10, jogadorId: 1);

        Assert.Empty(ctx.TimeAdministradores);
    }

    [Fact]
    public async Task Remover_o_cargo_nao_expulsa_do_time()
    {
        using var ctx = Cenario();
        await Admin(ctx, usuarioLogadoId: 9).IncluirAdministradorDoTime(timeId: 10, jogadorId: 1);

        await Admin(ctx, usuarioLogadoId: 9).RemoverAdministradorDoTime(timeId: 10, jogadorId: 1);

        Assert.Equal(10, (await ctx.Jogadores.FindAsync(1))!.TimeId);
    }

    [Fact]
    public async Task Quem_nao_e_admin_nao_mexe_em_quem_manda_no_time()
    {
        using var ctx = Cenario();
        ctx.TimeAdministradores.Add(new TimeAdministrador { TimeId = 10, JogadorId = 1, ConcedidoEm = DateTime.Now });
        await ctx.SaveChangesAsync();

        var incluir = await Admin(ctx, usuarioLogadoId: 2).IncluirAdministradorDoTime(timeId: 10, jogadorId: 2);
        var remover = await Admin(ctx, usuarioLogadoId: 2).RemoverAdministradorDoTime(timeId: 10, jogadorId: 1);

        Assert.IsType<ForbidResult>(incluir);
        Assert.IsType<ForbidResult>(remover);
        Assert.Equal(1, await ctx.TimeAdministradores.CountAsync(a => a.TimeId == 10));
    }
}
