using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Xunit;

namespace Padelizou.Tests;

// 26/10/2026 — SALVAR O LIMITE DE UMA CATEGORIA NÃO PODE JOGAR A TELA PRO TOPO.
//
// 🗣️ Felipe, com o print do card "Categorias do torneio": *"ao alterar um e salvar, ele volta
// para o inicio, ele deve se manter aonde esta a tela"*. O card mora no fim da aba Gerenciar; o
// redirect ia pro Details puro, que abre na aba padrão e no topo — quem ajusta o limite de cinco
// categorias seguidas rolava a página cinco vezes. Agora o redirect carrega a hash da LINHA mexida
// (`#categoria-N`) ou do card (`#categoriasDoTorneio`), e o script do Details abre a aba Gerenciar
// e rola até ela — o mesmo mecanismo do `#jogo-N`.
public class CategoriasVoltamProLugarTests
{
    private static string Fonte() => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Padelizou", "Views", "Torneios", "Details.cshtml"));

    [Fact]
    public async Task Salvar_o_limite_volta_pra_linha_da_categoria()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0, status: "Inscrições Abertas");
        var categoria = await ctx.Categorias.FirstAsync(c => c.TorneioId == torneio.Id);

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .AtualizarLimiteCategoria(categoria.Id, 6);

        var redirect = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Details", redirect.ActionName);
        Assert.Equal($"categoria-{categoria.Id}", redirect.Fragment);
    }

    [Fact]
    public async Task Remover_volta_pro_card_de_categorias()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0, status: "Inscrições Abertas");
        // Precisa de duas: o torneio não pode ficar sem nenhuma.
        ctx.Categorias.Add(new Categoria { Nome = "4ª Categoria Feminina", Codigo = "4CatF", TorneioId = torneio.Id });
        await ctx.SaveChangesAsync();
        var vazia = await ctx.Categorias.OrderByDescending(c => c.Id).FirstAsync(c => c.TorneioId == torneio.Id);

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id).RemoverCategoria(vazia.Id);

        var redirect = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("categoriasDoTorneio", redirect.Fragment);
    }

    [Fact]
    public async Task Adicionar_volta_pro_card_de_categorias()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0, status: "Inscrições Abertas");
        ctx.CategoriasPadrao.Add(new padelizou.Models.CategoriaPadrao
        {
            Id = 77, Nome = "Categoria Mista B", Codigo = "MISTA-B", Tipo = "Mista",
        });
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id)
            .AdicionarCategoria(torneio.Id, 77, limiteDuplas: null);

        var redirect = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("categoriasDoTorneio", redirect.Fragment);
    }

    [Fact]
    public void A_tela_tem_as_ancoras_e_o_script_que_rola_ate_elas()
    {
        var fonte = Fonte();

        Assert.Contains("id=\"categoriasDoTorneio\"", fonte);
        Assert.Contains("id=\"categoria-@cat.Id\"", fonte);
        // O script reconhece as duas hashes e abre a aba Gerenciar (#admin) antes de rolar.
        Assert.Contains("#categoria-", fonte);
        Assert.Contains("pdzIrPraCategoriaDaHash", fonte);
    }
}
