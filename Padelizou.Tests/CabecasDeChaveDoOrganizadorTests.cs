using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 11/11/2026 — O ORGANIZADOR ESCOLHE OS CABEÇAS DE CHAVE ANTES DO SORTEIO.
//
// 🗣️ Felipe: *"permita antes do sorteio, escolher os cabeças de chave caso seja uma escolha do
// organizador"*.
//
// Até aqui os cabeças saíam SÓ do ranking (a soma dos pontos da dupla), e o resto era sorteio.
// Agora o organizador pode cravar 1º, 2º, 3º…: os escolhidos abrem a lista do sorteio na ordem
// dele, e o resto segue como sempre (ranking, desempate sorteado). Ninguém escolhido = igual a
// antes.
public class CabecasDeChaveDoOrganizadorTests
{
    private static Dupla D(int id, int? cabeca = null) => new() { Id = id, Jogador1Id = id * 10, CabecaDeChave = cabeca };

    [Fact]
    public void Os_escolhidos_abrem_a_lista_na_ordem_do_organizador_mesmo_com_menos_ranking()
    {
        var duplas = new[] { D(1), D(2, cabeca: 2), D(3), D(4, cabeca: 1) };
        var pontos = new Dictionary<int, int> { [1] = 500, [2] = 0, [3] = 300, [4] = 10 };

        var ordem = CabecasDeChave.Ordenar(duplas, d => pontos[d.Id]).Select(d => d.Id).ToArray();

        Assert.Equal(new[] { 4, 2, 1, 3 }, ordem);
    }

    [Fact]
    public void Sem_escolha_segue_o_ranking()
    {
        var duplas = new[] { D(1), D(2), D(3) };
        var pontos = new Dictionary<int, int> { [1] = 10, [2] = 30, [3] = 20 };

        var ordem = CabecasDeChave.Ordenar(duplas, d => pontos[d.Id]).Select(d => d.Id).ToArray();

        Assert.Equal(new[] { 2, 3, 1 }, ordem);
    }

    // ── O botão ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Escolher_grava_e_tira_a_mesma_posicao_de_outra_dupla_da_categoria()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).OrderBy(d => d.Id).ToListAsync();
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.SalvarCabecaDeChave(duplas[0].Id, 1);
        await controller.SalvarCabecaDeChave(duplas[1].Id, 1);   // trocou de ideia

        Assert.Null(duplas[0].CabecaDeChave);
        Assert.Equal(1, duplas[1].CabecaDeChave);
    }

    [Fact]
    public async Task Zero_ou_vazio_tira_a_escolha()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.SalvarCabecaDeChave(dupla.Id, 2);

        await controller.SalvarCabecaDeChave(dupla.Id, null);

        Assert.Null(dupla.CabecaDeChave);
    }

    [Fact]
    public async Task Posicao_maior_que_o_numero_de_duplas_e_recusada()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 3);
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.SalvarCabecaDeChave(dupla.Id, 4);

        Assert.Null(dupla.CabecaDeChave);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Depois_do_sorteio_nao_mexe_mais()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);

        await controller.SalvarCabecaDeChave(dupla.Id, 1);

        Assert.Null(dupla.CabecaDeChave);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Quem_nao_organiza_nao_escolhe()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);

        var resultado = await TestInfra.NovoTorneiosController(ctx, dupla.Jogador1Id).SalvarCabecaDeChave(dupla.Id, 1);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(resultado);
        Assert.Null(dupla.CabecaDeChave);
    }

    // ── No sorteio de verdade ────────────────────────────────────────────────────────────

    [Fact]
    public async Task No_sorteio_os_escolhidos_abrem_os_grupos_na_ordem()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 9);   // 3 grupos de 3
        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).OrderBy(d => d.Id).ToListAsync();
        duplas[8].CabecaDeChave = 1;
        duplas[5].CabecaDeChave = 2;
        duplas[7].CabecaDeChave = 3;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        Assert.Equal("A", duplas[8].Grupo);
        Assert.Equal("B", duplas[5].Grupo);
        Assert.Equal("C", duplas[7].Grupo);
    }
}
