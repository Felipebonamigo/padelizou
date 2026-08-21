using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// AS DUAS SAÍDAS da pergunta sobre quem não pagou, pelo controller de verdade.
//
// A régua está em DecisaoSobreQuemNaoPagouTests. Aqui se exercita o que ACONTECE quando o
// organizador aperta cada botão — e é uma ação destrutiva: ela apaga inscrição de gente real.
public class DecidirSobreQuemNaoPagouTests
{
    // Monta um torneio com 3 duplas: uma paga, duas devendo. O organizador é quem o
    // TestInfra cria.
    private static (Torneio torneio, Categoria categoria, int organizadorId) Cenario(DbPadelContext ctx)
    {
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 3, status: "Inscrições Abertas");
        torneio.PrecoInscricao = 125m;
        torneio.ExcluirSeNaoPagar = true;
        torneio.PrevisaoEncerramentoInscricoes = new DateTime(2026, 10, 5);

        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).OrderBy(d => d.Id).ToList();
        duplas[0].Pago = true;
        foreach (var d in duplas) d.ValorInscricao = 250m;
        ctx.SaveChanges();

        return (torneio, categoria, organizador.Id);
    }

    [Fact]
    public async Task A_tela_lista_SO_quem_esta_devendo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizadorId) = Cenario(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, organizadorId);

        var vm = Assert.IsType<NaoPagosVM>(
            Assert.IsType<ViewResult>(await controller.NaoPagos(torneio.Id)).Model);

        Assert.Equal(2, vm.Total);
        Assert.Equal(500m, vm.ValorTotal);
    }

    [Fact]
    public async Task Quem_nao_organiza_o_torneio_nao_ve_a_tela()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = Cenario(ctx);
        var estranho = ctx.Duplas.First(d => d.CategoriaId == categoria.Id).Jogador1Id;
        var controller = TestInfra.NovoTorneiosController(ctx, estranho);

        Assert.IsType<ForbidResult>(await controller.NaoPagos(torneio.Id));
    }

    [Fact]
    public async Task Remover_tira_SO_os_devedores_avisa_e_deixa_quem_pagou()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizadorId) = Cenario(ctx);
        var push = Substitute.For<IPushNotificationService>();
        var controller = TestInfra.NovoTorneiosController(ctx, organizadorId, push: push);

        var devedores = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id && !d.Pago)
            .Select(d => d.Id).ToArray();

        await controller.RemoverNaoPagos(torneio.Id, devedores, Array.Empty<int>());

        var sobraram = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();
        Assert.Single(sobraram);
        Assert.True(sobraram[0].Pago);

        // ⚠️ Quem foi tirado PRECISA ser avisado: sem isso a pessoa aparece no clube no dia do
        // jogo e descobre ali que não estava mais no torneio. São 2 duplas × 2 pessoas.
        await push.Received(4).EnviarParaJogadorAsync(
            Arg.Any<int>(), "Você saiu do torneio", Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Quem_PAGOU_nao_sai_nem_se_o_id_dele_vier_no_formulario()
    {
        // ⚠️ O formulário vem do navegador e pode ser montado à mão. A condição de "não pago" é
        // conferida DE NOVO contra o banco: confiar só na lista da tela deixaria a remoção a um
        // POST forjado de distância de tirar gente que pagou.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizadorId) = Cenario(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, organizadorId);

        var todos = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).Select(d => d.Id).ToArray();

        await controller.RemoverNaoPagos(torneio.Id, todos, Array.Empty<int>());

        Assert.Single(ctx.Duplas.Where(d => d.CategoriaId == categoria.Id));
        Assert.True(ctx.Duplas.Single(d => d.CategoriaId == categoria.Id).Pago);
    }

    [Fact]
    public async Task Remover_promove_quem_estava_na_lista_de_espera()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizadorId) = Cenario(ctx);

        var naEspera = new Dupla
        {
            CategoriaId = categoria.Id,
            Jogador1Id = ctx.Duplas.First(d => d.CategoriaId == categoria.Id).Jogador1Id,
            EmListaDeEspera = true,
            UltimaFase = "",
        };
        ctx.Duplas.Add(naEspera);
        ctx.SaveChanges();

        var devedores = ctx.Duplas
            .Where(d => d.CategoriaId == categoria.Id && !d.Pago && !d.EmListaDeEspera)
            .Select(d => d.Id).ToArray();

        var controller = TestInfra.NovoTorneiosController(ctx, organizadorId);
        await controller.RemoverNaoPagos(torneio.Id, devedores, Array.Empty<int>());

        Assert.False(ctx.Duplas.Find(naEspera.Id)!.EmListaDeEspera);
    }

    // Achado da análise de sistema de 21/08/2026: a varredura em lote de não-pagos removia
    // InscricaoAmericana sem chamar a lista de espera nenhuma vez — a vaga abria e ninguém
    // era promovido.
    [Fact]
    public async Task Remover_promove_a_lista_de_espera_de_uma_categoria_de_americano_tambem()
    {
        using var ctx = TestInfra.NovoContexto();
        var torneio = new Torneio { Nome = "Americano de Teste", Codigo = "AME1", Status = "Inscrições Abertas", Formato = "Americano" };
        ctx.Torneios.Add(torneio);
        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000098" };
        ctx.Jogadores.Add(organizador);
        var categoria = new Categoria { Nome = "Mista", Codigo = "MIX1", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });

        var devendo = new Jogador { Nome = "Devendo", Cpf = "99900000097" };
        var naEspera = new Jogador { Nome = "Na espera", Cpf = "99900000096" };
        ctx.Jogadores.AddRange(devendo, naEspera);
        ctx.SaveChanges();

        var inscricaoDevendo = new InscricaoAmericana { CategoriaId = categoria.Id, JogadorId = devendo.Id, Pago = false };
        var inscricaoEspera = new InscricaoAmericana { CategoriaId = categoria.Id, JogadorId = naEspera.Id, EmListaDeEspera = true };
        ctx.InscricoesAmericanas.AddRange(inscricaoDevendo, inscricaoEspera);
        ctx.SaveChanges();

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await controller.RemoverNaoPagos(torneio.Id, Array.Empty<int>(), new[] { inscricaoDevendo.Id });

        Assert.False(ctx.InscricoesAmericanas.Find(inscricaoEspera.Id)!.EmListaDeEspera);
    }

    [Fact]
    public async Task Depois_de_encerradas_as_inscricoes_a_remocao_e_RECUSADA()
    {
        // Ali o chaveamento já pode ter usado essas duplas — tirá-las deixaria jogo órfão.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizadorId) = Cenario(ctx);
        torneio.Status = "Chaves em Sorteio";
        ctx.SaveChanges();

        var devedores = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id && !d.Pago)
            .Select(d => d.Id).ToArray();

        var controller = TestInfra.NovoTorneiosController(ctx, organizadorId);
        await controller.RemoverNaoPagos(torneio.Id, devedores, Array.Empty<int>());

        Assert.Equal(3, ctx.Duplas.Count(d => d.CategoriaId == categoria.Id));
    }

    [Fact]
    public async Task Vou_acertar_pessoalmente_nao_tira_ninguem_e_desliga_a_caixinha()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizadorId) = Cenario(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, organizadorId);

        await controller.ManterNaoPagos(torneio.Id);

        Assert.Equal(3, ctx.Duplas.Count(d => d.CategoriaId == categoria.Id));
        Assert.False(ctx.Torneios.Find(torneio.Id)!.ExcluirSeNaoPagar);
    }

    [Fact]
    public async Task Estranho_nao_remove_ninguem()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = Cenario(ctx);
        var estranho = ctx.Duplas.First(d => d.CategoriaId == categoria.Id).Jogador1Id;
        var controller = TestInfra.NovoTorneiosController(ctx, estranho);

        var devedores = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id && !d.Pago)
            .Select(d => d.Id).ToArray();

        Assert.IsType<ForbidResult>(await controller.RemoverNaoPagos(torneio.Id, devedores, Array.Empty<int>()));
        Assert.Equal(3, ctx.Duplas.Count(d => d.CategoriaId == categoria.Id));
    }
}
