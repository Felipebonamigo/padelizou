using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Tests;

// 🗣️ Rafael (Treta, 27/11/2026): "não consegui excluir essas categorias testes" — criadas com o
// torneio já sorteado. Vazia de verdade sai em qualquer fase; a sorteada continua presa (a ação
// pergunta ao BANCO se há grupo ou jogo, não à navegação).
public class RemoverCategoriaVaziaDepoisDoSorteioTests
{
    private static async Task<(DbPadelContext ctx, Torneio torneio, Categoria sorteada, Categoria vazia, Jogador org)> MontarAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, sorteada, org) = TestInfra.MontarTorneio(ctx, 6);
        var sabado = DateTime.Today.AddDays(14);
        while (sabado.DayOfWeek != DayOfWeek.Saturday) sabado = sabado.AddDays(1);
        torneio.DataInicio = sabado.AddHours(9);
        torneio.DataFim = sabado.AddDays(1);
        torneio.QuantidadeQuadras = 1;
        torneio.TempoPrevistoPartidaMinutos = 30;
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Q1" });
        await ctx.SaveChangesAsync();
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var vazia = new Categoria { Nome = "Teste", Codigo = "TESTE", TorneioId = torneio.Id };
        ctx.Categorias.Add(vazia);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
        return (ctx, torneio, sorteada, vazia, org);
    }

    [Fact]
    public async Task Categoria_vazia_criada_depois_do_sorteio_sai()
    {
        var (ctx, torneio, _, vazia, org) = await MontarAsync();
        using var _ = ctx;
        Assert.NotEqual("Inscrições Abertas", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);

        await TestInfra.NovoTorneiosController(ctx, org.Id).RemoverCategoria(vazia.Id);

        Assert.False(await ctx.Categorias.AnyAsync(c => c.Id == vazia.Id));
    }

    [Fact]
    public async Task Categoria_sorteada_sem_inscrito_lido_continua_presa()
    {
        // Mesmo que alguém tire as duplas por fora, grupo e jogo seguram a categoria.
        var (ctx, _, sorteada, _, org) = await MontarAsync();
        using var _ = ctx;
        ctx.Duplas.RemoveRange(await ctx.Duplas.Where(d => d.CategoriaId == sorteada.Id).ToListAsync());
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();

        await TestInfra.NovoTorneiosController(ctx, org.Id).RemoverCategoria(sorteada.Id);

        Assert.True(await ctx.Categorias.AnyAsync(c => c.Id == sorteada.Id));
    }
}
