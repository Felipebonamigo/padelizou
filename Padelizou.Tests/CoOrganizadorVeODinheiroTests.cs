using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// A regra chegando nas telas: QUEM ORGANIZA JUNTO VÊ IGUAL A QUEM CRIOU (17/11/2026).
//
// 🗣️ Felipe: *"Os co organizadores tem q ver igual os organizadores/criadores"*. Até aqui este
// arquivo se chamava AjudanteNaoVeDinheiroTests e cobrava o contrário ("abre o Financeiro, marca
// quem pagou, mas nenhum valor sai de lá"). Os testes foram reescritos DE PROPÓSITO, pela regra
// nova; as guardas que protegem quem NÃO organiza e o nível desconhecido continuam.
public class CoOrganizadorVeODinheiroTests
{
    // Quem cria o torneio ganha a linha "Criador"; quem entra pela caixinha vira "Organizador".
    private static (Torneio torneio, Jogador criador, Jogador ajudante) Cenario(
        DbPadelContext ctx, string formaPagamento = "Externo")
    {
        var (torneio, _, criador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");
        torneio.FormaPagamento = formaPagamento;
        torneio.PrecoInscricao = 60m;

        var doCriador = ctx.TorneioOrganizadores.Single(o => o.TorneioId == torneio.Id);
        doCriador.NivelAcesso = "Criador";

        var ajudante = new Jogador { Nome = "Ajudante da Mesa", Cpf = "11144477735" };
        ctx.Jogadores.Add(ajudante);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador
        {
            TorneioId = torneio.Id,
            JogadorId = ajudante.Id,
            NivelAcesso = "Organizador",
        });
        ctx.SaveChanges();

        return (torneio, criador, ajudante);
    }

    private static async Task<FinanceiroTorneioVM> AbrirFinanceiroAsync(
        DbPadelContext ctx, int torneioId, int quem)
    {
        var resultado = await TestInfra.NovoTorneiosController(ctx, quem).Financeiro(torneioId);
        return (FinanceiroTorneioVM)Assert.IsType<ViewResult>(resultado).Model!;
    }

    [Fact]
    public async Task Quem_criou_ve_os_valores()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, criador, _) = Cenario(ctx);

        var vm = await AbrirFinanceiroAsync(ctx, torneio.Id, criador.Id);

        Assert.True(vm.PodeVerDinheiro);
    }

    [Fact]
    public async Task Co_organizador_ve_os_valores_no_financeiro()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, ajudante) = Cenario(ctx);

        var vm = await AbrirFinanceiroAsync(ctx, torneio.Id, ajudante.Id);

        Assert.True(vm.PodeVerDinheiro);
    }

    [Fact]
    public async Task Quem_nao_organiza_nada_continua_barrado_na_porta()
    {
        // A regra nova não pode ter afrouxado a antiga: estranho segue sem entrar.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = Cenario(ctx);
        var estranho = new Jogador { Nome = "Estranho", Cpf = "52998224725" };
        ctx.Jogadores.Add(estranho);
        ctx.SaveChanges();

        var resultado = await TestInfra.NovoTorneiosController(ctx, estranho.Id).Financeiro(torneio.Id);

        Assert.IsType<ForbidResult>(resultado);
    }

    [Fact]
    public async Task Ajudante_MARCA_quem_pagou_isso_ele_pode()
    {
        // Pedido explícito do Felipe: ajudar inclui colocar como pago.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, ajudante) = Cenario(ctx);
        var dupla = ctx.Duplas.Include(d => d.Categoria).First(d => d.Categoria.TorneioId == torneio.Id);

        await TestInfra.NovoTorneiosController(ctx, ajudante.Id)
            .AlternarPagamentoDupla(dupla.Id, "Financeiro");

        Assert.True((await ctx.Duplas.FindAsync(dupla.Id))!.Pago);
    }

    [Fact]
    public async Task A_area_da_taxa_abre_pro_co_organizador()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, ajudante) = Cenario(ctx);

        Assert.IsNotType<ForbidResult>(
            await TestInfra.NovoTorneiosController(ctx, ajudante.Id).TaxaPlataforma(torneio.Id));
    }

    [Fact]
    public async Task A_area_da_taxa_continua_fechada_pra_quem_nao_organiza()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = Cenario(ctx);
        var estranho = new Jogador { Nome = "Estranho", Cpf = "52998224725" };
        ctx.Jogadores.Add(estranho);
        ctx.SaveChanges();

        Assert.IsType<ForbidResult>(
            await TestInfra.NovoTorneiosController(ctx, estranho.Id).TaxaPlataforma(torneio.Id));
        Assert.IsType<ForbidResult>(
            await TestInfra.NovoTorneiosController(ctx, estranho.Id).PagarTaxaPlataforma(torneio.Id));
    }

    [Fact]
    public async Task A_area_da_taxa_continua_aberta_pra_quem_criou()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, criador, _) = Cenario(ctx);

        Assert.IsNotType<ForbidResult>(
            await TestInfra.NovoTorneiosController(ctx, criador.Id).TaxaPlataforma(torneio.Id));
    }

    [Fact]
    public async Task O_relatorio_do_co_organizador_vem_com_o_bloco_de_dinheiro()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, criador, ajudante) = Cenario(ctx);

        var doAjudante = (RelatorioTorneioVM)Assert.IsType<ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, ajudante.Id).Relatorio(torneio.Id)).Model!;
        var doCriador = (RelatorioTorneioVM)Assert.IsType<ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, criador.Id).Relatorio(torneio.Id)).Model!;

        Assert.True(doAjudante.PodeVerDinheiro);
        Assert.True(doCriador.PodeVerDinheiro);
        Assert.Equal(doCriador.TotalDuplas, doAjudante.TotalDuplas);
    }

    [Fact]
    public async Task Nivel_desconhecido_continua_sem_ver()
    {
        // A lista de níveis continua FECHADA: um nível que o código não escreve (ajuste na mão,
        // nível novo amanhã) não vê — o erro seguro é alguém reclamar, não vazar calado.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, criador, _) = Cenario(ctx);
        ctx.TorneioOrganizadores.Single(o => o.JogadorId == criador.Id).NivelAcesso = "Mesario";
        ctx.SaveChanges();

        var vm = await AbrirFinanceiroAsync(ctx, torneio.Id, criador.Id);

        Assert.False(vm.PodeVerDinheiro);
    }
}
