using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Tests;

// CATEGORIA COM NOME PRÓPRIO (25/11/2026). 🗣️ Rafael (Treta), com o print da gestão: "não tem
// branco, preto e verde"; Felipe: "acho melhor um 'categoria fora do padrão' [...] para ele
// categorizar como quiser nesse tipo de torneio".
//
// ⚠️ SÓ EM TORNEIO FORA DO RANKING: o ranking por categoria agrupa pelo NOME
// (EstatisticasService), e um nome livre num torneio que conta criaria uma "categoria" de
// ranking que não existe no catálogo. No torneio-festa não há ranking pra bagunçar.
public class CategoriaComNomeProprioTests
{
    private static (DbPadelContext ctx, Torneio torneio, Jogador org) Montar(bool foraDoRanking)
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0, status: "Inscrições Abertas");
        torneio.ForaDoRanking = foraDoRanking;
        ctx.SaveChanges();
        ctx.ChangeTracker.Clear();
        return (ctx, torneio, org);
    }

    [Fact]
    public async Task No_torneio_fora_do_ranking_o_organizador_cria_Branco()
    {
        var (ctx, torneio, org) = Montar(foraDoRanking: true);
        using var _ = ctx;

        await TestInfra.NovoTorneiosController(ctx, org.Id).AdicionarCategoriaComNomeProprio(torneio.Id, "  Branco ", 8);

        var branco = await ctx.Categorias.AsNoTracking().SingleAsync(c => c.TorneioId == torneio.Id && c.Nome == "Branco");
        Assert.Equal(8, branco.LimiteDuplas);
        Assert.False(string.IsNullOrWhiteSpace(branco.Codigo));
    }

    [Fact]
    public async Task No_torneio_que_conta_no_ranking_e_recusado()
    {
        var (ctx, torneio, org) = Montar(foraDoRanking: false);
        using var _ = ctx;

        await TestInfra.NovoTorneiosController(ctx, org.Id).AdicionarCategoriaComNomeProprio(torneio.Id, "Branco", null);

        Assert.False(await ctx.Categorias.AnyAsync(c => c.Nome == "Branco"));
    }

    [Fact]
    public async Task Nome_repetido_ou_vazio_e_recusado()
    {
        var (ctx, torneio, org) = Montar(foraDoRanking: true);
        using var _ = ctx;
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.AdicionarCategoriaComNomeProprio(torneio.Id, "Branco", null);
        ctx.ChangeTracker.Clear();
        await TestInfra.NovoTorneiosController(ctx, org.Id).AdicionarCategoriaComNomeProprio(torneio.Id, "branco", null);
        await TestInfra.NovoTorneiosController(ctx, org.Id).AdicionarCategoriaComNomeProprio(torneio.Id, "   ", null);

        Assert.Equal(1, await ctx.Categorias.CountAsync(c => c.TorneioId == torneio.Id && c.Nome.ToLower() == "branco"));
        Assert.False(await ctx.Categorias.AnyAsync(c => c.TorneioId == torneio.Id && c.Nome.Trim() == ""));
    }

    [Fact]
    public async Task Quem_nao_organiza_nao_cria()
    {
        var (ctx, torneio, _) = Montar(foraDoRanking: true);
        using var _ = ctx;
        var intruso = TestInfra.NovoJogador(77);
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var r = await TestInfra.NovoTorneiosController(ctx, intruso.Id).AdicionarCategoriaComNomeProprio(torneio.Id, "Branco", null);

        Assert.IsType<ForbidResult>(r);
        Assert.False(await ctx.Categorias.AnyAsync(c => c.Nome == "Branco"));
    }
}
