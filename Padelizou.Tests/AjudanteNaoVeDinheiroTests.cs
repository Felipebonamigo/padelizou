using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// A regra chegando nas telas: quem foi adicionado pra ajudar abre o Financeiro e marca quem
// já acertou, mas nenhum valor sai de lá — e a área da taxa, que é dinheiro do começo ao fim,
// recusa no SERVIDOR, não só escondendo o botão.
public class AjudanteNaoVeDinheiroTests
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
    public async Task Quem_so_ajuda_abre_a_tela_mas_sem_os_valores()
    {
        // Abrir é necessário: é onde ele marca quem acertou. O que não pode é ver quanto.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, ajudante) = Cenario(ctx);

        var vm = await AbrirFinanceiroAsync(ctx, torneio.Id, ajudante.Id);

        Assert.False(vm.PodeVerDinheiro);
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
    public async Task A_area_da_taxa_recusa_o_ajudante_no_servidor()
    {
        // Aqui não basta esconder o botão: a tela mostra o valor devido e gera cobrança em
        // nome de quem recebeu as inscrições.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, ajudante) = Cenario(ctx);

        Assert.IsType<ForbidResult>(
            await TestInfra.NovoTorneiosController(ctx, ajudante.Id).TaxaPlataforma(torneio.Id));
        Assert.IsType<ForbidResult>(
            await TestInfra.NovoTorneiosController(ctx, ajudante.Id).PagarTaxaPlataforma(torneio.Id));
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
    public async Task O_relatorio_do_ajudante_vem_sem_o_bloco_de_dinheiro()
    {
        // O pódio e os números do torneio ele levou junto — foi ele que fez acontecer.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, criador, ajudante) = Cenario(ctx);

        var doAjudante = (RelatorioTorneioVM)Assert.IsType<ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, ajudante.Id).Relatorio(torneio.Id)).Model!;
        var doCriador = (RelatorioTorneioVM)Assert.IsType<ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, criador.Id).Relatorio(torneio.Id)).Model!;

        Assert.False(doAjudante.PodeVerDinheiro);
        Assert.True(doCriador.PodeVerDinheiro);
        Assert.Equal(doCriador.TotalDuplas, doAjudante.TotalDuplas);
    }

    [Fact]
    public async Task Torneio_antigo_sem_ninguem_marcado_como_criador_nao_vaza_pra_ajudante()
    {
        // Se um dia sobrar um torneio sem "Criador", o erro seguro é ninguém ver — nunca
        // "não achei o dono, então libera pra todo mundo".
        using var ctx = TestInfra.NovoContexto();
        var (torneio, criador, _) = Cenario(ctx);
        ctx.TorneioOrganizadores.Single(o => o.JogadorId == criador.Id).NivelAcesso = "Ajudante";
        ctx.SaveChanges();

        var vm = await AbrirFinanceiroAsync(ctx, torneio.Id, criador.Id);

        Assert.False(vm.PodeVerDinheiro);
    }
}
