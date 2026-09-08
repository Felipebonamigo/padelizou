using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A SUB-ABA "ELIMINATÓRIAS" — o organizador declara, POR CATEGORIA, se ainda vai ter jogo de
// eliminatória no sábado à noite (pedido do Felipe, 08/09/2026).
//
// A régua (que janela nasce, e que ela só vale fora da fase de grupos) está em
// EliminatoriaNoSabadoTests e ConcentracaoNaGradeTests. Aqui é a FIAÇÃO: quem pode gravar,
// e o que acontece com categoria de outro torneio.
public class EliminatoriaDaCategoriaPeloOrganizadorTests
{
    [Fact]
    public async Task Organizador_desliga_o_sabado_a_noite_da_categoria()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .AlterarEliminatoriaNoSabado(categoria.Id, false);

        Assert.False((await ctx.Categorias.FindAsync(categoria.Id))!.EliminatoriaNoSabadoANoite);
    }

    [Fact]
    public async Task E_liga_de_volta()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        categoria.EliminatoriaNoSabadoANoite = false;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id)
            .AlterarEliminatoriaNoSabado(categoria.Id, true);

        Assert.True((await ctx.Categorias.FindAsync(categoria.Id))!.EliminatoriaNoSabadoANoite);
    }

    [Fact]
    public async Task So_organizador_altera()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        var intruso = new Jogador { Nome = "Intruso", Cpf = "99900000066" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, intruso.Id)
            .AlterarEliminatoriaNoSabado(categoria.Id, false);

        Assert.IsType<ForbidResult>(resultado);
        Assert.True((await ctx.Categorias.FindAsync(categoria.Id))!.EliminatoriaNoSabadoANoite);
    }

    // ⚠️ A checagem de dono é sobre o TORNEIO DA CATEGORIA, não sobre um torneio que o POST
    // mande junto: sem isso, quem organiza o torneio A mexeria na categoria do torneio B só
    // trocando o id no formulário.
    [Fact]
    public async Task Organizador_de_outro_torneio_nao_alcanca_a_categoria()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, categoriaDoOutro, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        var (_, _, orgDoMeu) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);

        var resultado = await TestInfra.NovoTorneiosController(ctx, orgDoMeu.Id)
            .AlterarEliminatoriaNoSabado(categoriaDoOutro.Id, false);

        Assert.IsType<ForbidResult>(resultado);
        Assert.True((await ctx.Categorias.FindAsync(categoriaDoOutro.Id))!.EliminatoriaNoSabadoANoite);
    }

    [Fact]
    public async Task Categoria_inexistente_nao_estoura()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);

        var resultado = await TestInfra.NovoTorneiosController(ctx, org.Id)
            .AlterarEliminatoriaNoSabado(99999, false);

        Assert.IsType<NotFoundResult>(resultado);
    }
}
