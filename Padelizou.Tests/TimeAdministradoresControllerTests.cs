using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Tests;

// As regras estão em AdministracaoTime e têm teste próprio. Aqui é o caminho real: a tela
// aplica a regra, grava certo, e — o que mais importa — NÃO deixa quem não pode.
//
// Regra 0 do projeto: ação que grava precisa de [Authorize] E de checagem de dono. Estes
// testes são o "E" da frase.
public class TimeAdministradoresControllerTests
{
    private static (DbPadelContext ctx, Time time) ComTime(string nome = "SINDAQUA")
    {
        var ctx = TestInfra.NovoContexto();
        var time = new Time { Nome = nome, Logo = "/uploads/logos-time/sindaqua2.jpeg" };
        ctx.Times.Add(time);
        ctx.SaveChanges();
        return (ctx, time);
    }

    private static Jogador NovoJogador(DbPadelContext ctx, string nome, int i, bool adminDoSistema = false)
    {
        var j = new Jogador { Nome = nome, Cpf = $"999000000{i:00}", IsAdminGeral = adminDoSistema };
        ctx.Jogadores.Add(j);
        ctx.SaveChanges();
        return j;
    }

    private static void TornaAdministrador(DbPadelContext ctx, int timeId, int jogadorId)
    {
        ctx.TimeAdministradores.Add(new TimeAdministrador
        {
            TimeId = timeId, JogadorId = jogadorId, ConcedidoEm = DateTime.Now,
        });
        ctx.SaveChanges();
    }

    [Fact]
    public async Task Admin_do_padelizou_designa_o_primeiro_administrador()
    {
        // Exatamente o caso dos 44 times importados do ranking: sem ninguém no comando.
        var (ctx, time) = ComTime();
        var felipe = NovoJogador(ctx, "Felipe", 1, adminDoSistema: true);
        var rafael = NovoJogador(ctx, "Rafael", 2);

        var controller = TestInfra.NovoTimesController(ctx, felipe.Id);
        await controller.IncluirAdministrador(time.Id, rafael.Id);

        Assert.True(await ctx.TimeAdministradores.AnyAsync(a => a.TimeId == time.Id && a.JogadorId == rafael.Id));
        // E entra no time, senão administraria um time do qual não faz parte.
        Assert.Equal(time.Id, (await ctx.Jogadores.FindAsync(rafael.Id))!.TimeId);
    }

    [Fact]
    public async Task Administrador_do_time_inclui_um_segundo()
    {
        var (ctx, time) = ComTime();
        var rafael = NovoJogador(ctx, "Rafael", 2);
        var bruno = NovoJogador(ctx, "Bruno", 3);
        TornaAdministrador(ctx, time.Id, rafael.Id);

        var controller = TestInfra.NovoTimesController(ctx, rafael.Id);
        await controller.IncluirAdministrador(time.Id, bruno.Id);

        Assert.Equal(2, await ctx.TimeAdministradores.CountAsync(a => a.TimeId == time.Id));
    }

    [Fact]
    public async Task Membro_comum_do_time_nao_se_promove()
    {
        // O buraco óbvio: entrar no time e sair administrando ele.
        var (ctx, time) = ComTime();
        var rafael = NovoJogador(ctx, "Rafael", 2);
        var penetra = NovoJogador(ctx, "Penetra", 4);
        TornaAdministrador(ctx, time.Id, rafael.Id);

        var penetraDb = await ctx.Jogadores.FindAsync(penetra.Id);
        penetraDb!.TimeId = time.Id;          // veste a camisa, e só
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTimesController(ctx, penetra.Id);
        await controller.IncluirAdministrador(time.Id, penetra.Id);

        Assert.False(await ctx.TimeAdministradores.AnyAsync(a => a.JogadorId == penetra.Id));
        Assert.Equal(1, await ctx.TimeAdministradores.CountAsync(a => a.TimeId == time.Id));
    }

    [Fact]
    public async Task Estranho_nao_assume_time_sem_administrador()
    {
        // Um dos 44 recém-importados, e alguém tentando pegar pra si.
        var (ctx, time) = ComTime();
        var qualquerUm = NovoJogador(ctx, "Qualquer Um", 5);

        var controller = TestInfra.NovoTimesController(ctx, qualquerUm.Id);
        await controller.IncluirAdministrador(time.Id, qualquerUm.Id);

        Assert.Empty(ctx.TimeAdministradores);
    }

    [Fact]
    public async Task Nao_da_pra_incluir_duas_vezes()
    {
        var (ctx, time) = ComTime();
        var rafael = NovoJogador(ctx, "Rafael", 2);
        TornaAdministrador(ctx, time.Id, rafael.Id);

        var controller = TestInfra.NovoTimesController(ctx, rafael.Id);
        await controller.IncluirAdministrador(time.Id, rafael.Id);

        Assert.Equal(1, await ctx.TimeAdministradores.CountAsync(a => a.TimeId == time.Id));
    }

    [Fact]
    public async Task Remover_deixa_a_pessoa_no_time()
    {
        // Perder o cargo não é ser expulso.
        var (ctx, time) = ComTime();
        var rafael = NovoJogador(ctx, "Rafael", 2);
        var bruno = NovoJogador(ctx, "Bruno", 3);
        TornaAdministrador(ctx, time.Id, rafael.Id);
        TornaAdministrador(ctx, time.Id, bruno.Id);

        var brunoDb = await ctx.Jogadores.FindAsync(bruno.Id);
        brunoDb!.TimeId = time.Id;
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTimesController(ctx, rafael.Id);
        await controller.RemoverAdministrador(time.Id, bruno.Id);

        Assert.False(await ctx.TimeAdministradores.AnyAsync(a => a.JogadorId == bruno.Id));
        Assert.Equal(time.Id, (await ctx.Jogadores.FindAsync(bruno.Id))!.TimeId);
    }

    [Fact]
    public async Task O_unico_administrador_nao_consegue_se_remover()
    {
        var (ctx, time) = ComTime();
        var rafael = NovoJogador(ctx, "Rafael", 2);
        TornaAdministrador(ctx, time.Id, rafael.Id);

        var controller = TestInfra.NovoTimesController(ctx, rafael.Id);
        await controller.RemoverAdministrador(time.Id, rafael.Id);

        Assert.Equal(1, await ctx.TimeAdministradores.CountAsync(a => a.TimeId == time.Id));
    }

    [Fact]
    public async Task Time_inexistente_nao_quebra_a_tela()
    {
        var (ctx, _) = ComTime();
        var felipe = NovoJogador(ctx, "Felipe", 1, adminDoSistema: true);

        var controller = TestInfra.NovoTimesController(ctx, felipe.Id);
        var resultado = await controller.IncluirAdministrador(9999, felipe.Id);

        Assert.IsType<NotFoundResult>(resultado);
    }
}
