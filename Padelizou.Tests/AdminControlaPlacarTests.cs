using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Tests;

// O admin da plataforma manda em qualquer torneio — decisão de 31/07/2026, pra socorrer
// organizador travado sem depender dele. Todo o sistema já dava essa passagem (Torneios,
// Duplas), MENOS o controle de placar.
//
// O efeito era o pior possível: a Mesa de Controle ABRIA pro admin (ela usa o outro critério)
// com todos os botões à vista, e aí "Começar o jogo" e "Salvar placar" respondiam 403 — no
// meio do torneio, no clube, com a quadra esperando.
public class AdminControlaPlacarTests
{
    private static async Task<(DbPadelContext ctx, Torneio torneio, Partida jogo, Jogador admin)>
        MontarAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        // Admin da plataforma que NÃO organiza este torneio — o caso do dono do Padelizou
        // rodando o torneio de outra pessoa.
        var admin = new Jogador { Nome = "Dona do Padelizou", Cpf = "99900000097", IsAdminGeral = true };
        ctx.Jogadores.Add(admin);
        await ctx.SaveChangesAsync();

        var jogo = await ctx.Partidas.FirstAsync(p => p.TorneioId == torneio.Id);
        return (ctx, torneio, jogo, admin);
    }

    [Fact]
    public async Task Admin_que_nao_organiza_consegue_colocar_o_jogo_no_ar()
    {
        var (ctx, torneio, jogo, admin) = await MontarAsync();
        using var _ = ctx;

        Assert.False(await ctx.Set<TorneioOrganizador>()
            .AnyAsync(o => o.TorneioId == torneio.Id && o.JogadorId == admin.Id));

        var controller = TestInfra.NovoPartidasController(ctx, admin.Id);
        var resposta = await controller.ColocarNoAr(jogo.Id);

        Assert.IsNotType<Microsoft.AspNetCore.Mvc.ForbidResult>(resposta);
        Assert.Equal("AoVivo", (await ctx.Partidas.FindAsync(jogo.Id))!.Status);
    }

    [Fact]
    public async Task Admin_que_nao_organiza_consegue_salvar_o_placar()
    {
        var (ctx, torneio, jogo, admin) = await MontarAsync();
        using var _ = ctx;

        var controller = TestInfra.NovoPartidasController(ctx, admin.Id);
        var resposta = await controller.ControlePlacar(jogo.Id, "AoVivo", 4, 2, null, null);

        Assert.IsNotType<Microsoft.AspNetCore.Mvc.ForbidResult>(resposta);

        var depois = await ctx.Partidas.FindAsync(jogo.Id);
        Assert.Equal(4, depois!.GamesDupla1);
        Assert.Equal(2, depois.GamesDupla2);
    }

    [Fact]
    public async Task Quem_nao_e_admin_nem_organizador_continua_recusado()
    {
        // A contrapartida: abrir pro admin não pode abrir pra qualquer um logado.
        var (ctx, _, jogo, _) = await MontarAsync();
        using var _1 = ctx;

        var xereta = new Jogador { Nome = "Xereta", Cpf = "99900000096" };
        ctx.Jogadores.Add(xereta);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoPartidasController(ctx, xereta.Id);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(await controller.ColocarNoAr(jogo.Id));
        Assert.Equal("Agendada", (await ctx.Partidas.FindAsync(jogo.Id))!.Status);
    }

    [Fact]
    public async Task Deslogado_nao_mexe_em_placar_nenhum()
    {
        var (ctx, _, jogo, _) = await MontarAsync();
        using var _1 = ctx;

        var controller = TestInfra.NovoPartidasController(ctx, null);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(await controller.ColocarNoAr(jogo.Id));
    }
}
