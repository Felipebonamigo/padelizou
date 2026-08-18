using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;
using padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// DINHEIRO É SÓ DO RAIZ (18/08/2026, decisão do Felipe: "adm não vê nada de financeiro,
// somente eu"). O administrador NOMEADO continua editando o sistema inteiro; o ASSISTENTE
// continua enxergando a operação inteira. Nenhum dos dois vê um real.
//
// ⚠️ O QUE ESTE ARQUIVO PROTEGE não é uma tela, é uma CONFUSÃO: `PodeOlharTudo` respondia por
// duas perguntas diferentes — "enxerga a operação?" e "enxerga o caixa?" — e era por isso que
// um administrador nomeado lia a receita do MEI, a carteira dos parceiros e o caixa de
// qualquer torneio. Quem um dia trocar `PodeVerDinheiro` de volta por `PodeOlharTudo` (ou por
// `PodeEditarTudo`, que também inclui o nomeado) reabre tudo sem que nenhuma tela pareça
// diferente. É isso que os testes abaixo fazem barulho.
public class AdminNaoVeDinheiroTests
{
    private static Jogador Com(bool raiz = false, bool geral = false,
                               bool assistente = false, bool parceiroRanking = false) =>
        new()
        {
            Nome = "Fulano", Cpf = "11144477735",
            IsAdminRaiz = raiz, IsAdminGeral = geral,
            IsAssistente = assistente, IsParceiroRanking = parceiroRanking,
        };

    // ── A RÉGUA ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void So_o_raiz_ve_dinheiro()
    {
        Assert.True(PoderesNoSistema.PodeVerDinheiro(Com(raiz: true)));
    }

    [Fact]
    public void O_administrador_nomeado_NAO_ve_dinheiro()
    {
        // ⚠️ E continua editando tudo: as duas coisas são independentes de propósito.
        var nomeado = Com(geral: true);

        Assert.False(PoderesNoSistema.PodeVerDinheiro(nomeado));
        Assert.True(PoderesNoSistema.PodeEditarTudo(nomeado));
    }

    [Fact]
    public void O_assistente_NAO_ve_dinheiro()
    {
        var foka = Com(assistente: true);

        Assert.False(PoderesNoSistema.PodeVerDinheiro(foka));
        Assert.True(PoderesNoSistema.PodeOlharTudo(foka));
    }

    [Fact]
    public void Jogador_comum_e_ninguem_logado_nao_veem_dinheiro()
    {
        Assert.False(PoderesNoSistema.PodeVerDinheiro(Com()));
        Assert.False(PoderesNoSistema.PodeVerDinheiro((Jogador?)null));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void A_mesma_pergunta_pelo_cracha(string valorDoClaim, bool esperado)
    {
        // A sobrecarga do crachá serve às views e aos controllers que não buscam o Jogador
        // (a fatura do Pix, a taxa do externo). Ela tem que responder o mesmo que a do banco.
        var usuario = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("IsAdminRaiz", valorDoClaim) }, "Teste"));

        Assert.Equal(esperado, PoderesNoSistema.PodeVerDinheiro(usuario));
    }

    [Fact]
    public void Sem_cracha_de_raiz_nao_ve_dinheiro()
    {
        // O crachá do administrador nomeado tem `IsAdmin=true` e nenhum `IsAdminRaiz` — é o
        // caso em que confundir as duas credenciais custaria a fatura de outra pessoa.
        var usuario = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("IsAdmin", "true") }, "Teste"));

        Assert.False(PoderesNoSistema.PodeVerDinheiro(usuario));
    }

    // ── AS TELAS DO PAINEL ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Comissoes_recusa_o_administrador_nomeado()
    {
        using var ctx = TestInfra.NovoContexto();
        var nomeado = Adicionar(ctx, Com(geral: true));

        Assert.IsType<RedirectToActionResult>(await Painel(ctx, nomeado.Id).Comissoes(null));
    }

    [Fact]
    public async Task Comissoes_abre_pro_raiz()
    {
        using var ctx = TestInfra.NovoContexto();
        var felipe = Adicionar(ctx, Com(raiz: true));

        Assert.IsType<ViewResult>(await Painel(ctx, felipe.Id).Comissoes(null));
    }

    [Fact]
    public async Task Registrar_repasse_recusa_o_administrador_nomeado()
    {
        // A porta da tela e a da gravação são checagens diferentes: fechar só a primeira
        // deixaria o POST de pé pra quem soubesse o endereço.
        using var ctx = TestInfra.NovoContexto();
        var nomeado = Adicionar(ctx, Com(geral: true));
        var parceiro = Adicionar(ctx, new Jogador
        {
            Nome = "Ana Vendedora", Cpf = "99900000011", IsParceiroComercial = true,
        });

        var resposta = await Painel(ctx, nomeado.Id).RegistrarRepasse(parceiro.Id, 30m, null, null);

        Assert.IsType<ForbidResult>(resposta);
        Assert.Empty(ctx.RepassesAoParceiro);
    }

    [Fact]
    public async Task Registro_de_resultados_e_do_raiz()
    {
        // A tela mostra o nosso CUSTO e a MARGEM de cada pedido, e responder é fechar preço
        // em nome do Padelizou — não dava pra esconder números e manter o formulário.
        using var ctx = TestInfra.NovoContexto();
        var nomeado = Adicionar(ctx, Com(geral: true));
        var felipe = Adicionar(ctx, Com(raiz: true));

        Assert.IsType<RedirectToActionResult>(await Painel(ctx, nomeado.Id).RegistroResultados());
        Assert.IsType<ViewResult>(await Painel(ctx, felipe.Id).RegistroResultados());
    }

    [Fact]
    public async Task Metricas_abre_pros_dois_e_so_o_raiz_ve_os_valores()
    {
        // ⚠️ A tela NÃO fecha: cadastros e inscrições são operação. O que sai é o medidor do
        // MEI, os "pagos em 30 dias" e a coluna Valor da série — e quem decide é esta flag.
        using var ctx = TestInfra.NovoContexto();
        var nomeado = Adicionar(ctx, Com(geral: true));
        var felipe = Adicionar(ctx, Com(raiz: true));

        var doNomeado = Assert.IsType<ViewResult>(await Painel(ctx, nomeado.Id).Metricas());
        var doRaiz = Assert.IsType<ViewResult>(await Painel(ctx, felipe.Id).Metricas());

        Assert.False((bool)doNomeado.ViewData["PodeVerDinheiro"]!);
        Assert.True((bool)doRaiz.ViewData["PodeVerDinheiro"]!);
    }

    [Fact]
    public async Task Professores_abre_pros_dois_e_so_o_raiz_ve_quanto_cada_um_pagou()
    {
        using var ctx = TestInfra.NovoContexto();
        var nomeado = Adicionar(ctx, Com(geral: true));
        var felipe = Adicionar(ctx, Com(raiz: true));
        var plano = Options.Create(new PlanoProfessorSettings());

        var doNomeado = Assert.IsType<ViewResult>(await Painel(ctx, nomeado.Id).Professores(plano));
        var doRaiz = Assert.IsType<ViewResult>(await Painel(ctx, felipe.Id).Professores(plano));

        Assert.False((bool)doNomeado.ViewData["PodeVerDinheiro"]!);
        Assert.True((bool)doRaiz.ViewData["PodeVerDinheiro"]!);
    }

    [Fact]
    public async Task No_relatorio_do_ranking_o_parceiro_continua_vendo_os_valores()
    {
        // O corte é sobre dinheiro NOSSO e de terceiros — não sobre o extrato de quem tem a
        // receber. Esvaziar a tela do parceiro seria quebrar a parceria pra resolver outra
        // coisa. O administrador nomeado lê o mesmo relatório sem a coluna de valor.
        using var ctx = TestInfra.NovoContexto();
        var nomeado = Adicionar(ctx, Com(geral: true));
        var parceiro = Adicionar(ctx, Com(parceiroRanking: true));
        var felipe = Adicionar(ctx, Com(raiz: true));

        var doNomeado = Assert.IsType<ViewResult>(await Painel(ctx, nomeado.Id).RankingRsRelatorio());
        var doParceiro = Assert.IsType<ViewResult>(await Painel(ctx, parceiro.Id).RankingRsRelatorio());
        var doRaiz = Assert.IsType<ViewResult>(await Painel(ctx, felipe.Id).RankingRsRelatorio());

        Assert.False((bool)doNomeado.ViewData["PodeVerValores"]!);
        Assert.True((bool)doParceiro.ViewData["PodeVerValores"]!);
        Assert.True((bool)doRaiz.ViewData["PodeVerValores"]!);
    }

    // ── O PAINEL: O CARTÃO E A ABA ────────────────────────────────────────────────────

    [Fact]
    public void A_aba_de_comissoes_some_pra_quem_nao_e_raiz()
    {
        var parceiros = PainelAdminAgrupado.Grupos.Single(g => g.Titulo == "Parceiros");

        var doNomeado = PainelAdminAgrupado.AbasVisiveis(parceiros, ehRaiz: false);
        var doRaiz = PainelAdminAgrupado.AbasVisiveis(parceiros, ehRaiz: true);

        Assert.DoesNotContain(doNomeado, a => a.Action == "Comissoes");
        Assert.Contains(doNomeado, a => a.Action == "Leads");
        Assert.Contains(doRaiz, a => a.Action == "Comissoes");
    }

    [Fact]
    public void O_cartao_de_parceiros_continua_de_pe_e_abre_nas_indicacoes()
    {
        // ⚠️ O cartão abre na primeira aba VISÍVEL. Apontando pra `Abas[0]` (Comissões), o
        // administrador nomeado levaria um Forbid clicando no cartão que o painel ofereceu.
        var visiveis = PainelAdminAgrupado.GruposVisiveis(ehRaiz: false);
        var parceiros = Assert.Single(visiveis, g => g.Titulo == "Parceiros");

        Assert.Equal("Leads", PainelAdminAgrupado.AbasVisiveis(parceiros, ehRaiz: false)[0].Action);
    }

    [Fact]
    public void Todo_grupo_visivel_tem_pelo_menos_uma_aba()
    {
        // Cartão que promete abas e abre numa tela sem nenhuma é pior do que cartão nenhum —
        // e é o que aconteceria com um grupo cujas abas fossem todas de dinheiro.
        foreach (var ehRaiz in new[] { true, false })
            foreach (var grupo in PainelAdminAgrupado.GruposVisiveis(ehRaiz))
                Assert.NotEmpty(PainelAdminAgrupado.AbasVisiveis(grupo, ehRaiz));
    }

    [Fact]
    public void O_cartao_nao_promete_dinheiro_pra_quem_nao_vai_encontrar()
    {
        // A descrição é parte do contrato do cartão: "quanto cada parceiro tem a receber"
        // dito a quem abre uma tela sem valor nenhum é o painel mentindo.
        foreach (var grupo in PainelAdminAgrupado.GruposVisiveis(ehRaiz: false))
            Assert.DoesNotContain("receber", grupo.DescricaoPara(ehRaiz: false));

        var parceiros = PainelAdminAgrupado.Grupos.Single(g => g.Titulo == "Parceiros");
        Assert.Contains("receber", parceiros.DescricaoPara(ehRaiz: true));
    }

    // ── FORA DO PAINEL: O CAIXA DO TORNEIO E A TAXA ───────────────────────────────────

    [Fact]
    public async Task O_administrador_nomeado_abre_a_gestao_do_torneio_SEM_os_valores()
    {
        // Ele continua entrando pra socorrer organizador — o que some são os números. É a
        // mesma separação que já valia pra quem só ajuda na mesa (AjudanteNaoVeDinheiroTests).
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");
        var nomeado = Adicionar(ctx, Com(geral: true));

        var resultado = await TestInfra.NovoTorneiosController(ctx, nomeado.Id).Financeiro(torneio.Id);
        var vm = (FinanceiroTorneioVM)Assert.IsType<ViewResult>(resultado).Model!;

        Assert.False(vm.PodeVerDinheiro);
    }

    [Fact]
    public async Task O_raiz_continua_vendo_o_caixa_de_qualquer_torneio()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");
        var felipe = Adicionar(ctx, Com(raiz: true));

        var resultado = await TestInfra.NovoTorneiosController(ctx, felipe.Id).Financeiro(torneio.Id);
        var vm = (FinanceiroTorneioVM)Assert.IsType<ViewResult>(resultado).Model!;

        Assert.True(vm.PodeVerDinheiro);
    }

    [Fact]
    public async Task A_taxa_do_torneio_externo_recusa_o_administrador_nomeado()
    {
        // Esta tela é dinheiro do começo ao fim: o valor devido e o botão de pagar.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");
        torneio.FormaPagamento = "Externo";
        torneio.PrecoInscricao = 60m;
        ctx.SaveChanges();
        var nomeado = Adicionar(ctx, Com(geral: true));

        var controller = TestInfra.NovoTorneiosController(ctx, nomeado.Id);

        Assert.IsType<ForbidResult>(await controller.TaxaPlataforma(torneio.Id));
        Assert.IsType<ForbidResult>(await controller.RegistrarNegociacaoTaxa(torneio.Id, "combinado"));
    }

    private static Jogador Adicionar(DbPadelContext ctx, Jogador jogador)
    {
        // CPF único por jogador: `Com()` devolve sempre o mesmo, e o índice único do banco
        // reclamaria num cenário com dois perfis.
        jogador.Cpf = $"999{ctx.Jogadores.Count():D8}";
        ctx.Jogadores.Add(jogador);
        ctx.SaveChanges();
        return jogador;
    }

    private static AdminController Painel(DbPadelContext ctx, int usuarioLogadoId)
    {
        var controller = new AdminController(
            ctx,
            Substitute.For<IPushNotificationService>(),
            new ConfigurationBuilder().Build(),
            Options.Create(new RegistroResultadosSettings()));

        var http = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.ToString()) }, "Teste")),
        };
        // O relatório do Ranking confere o VERBO pra deixar o parceiro entrar; sem isto ele
        // seria recusado por um método vazio, e não pela regra que o teste quer exercitar.
        http.Request.Method = HttpMethods.Get;

        controller.ControllerContext = new ControllerContext { HttpContext = http };
        controller.TempData = new TempDataDictionary(
            controller.HttpContext, Substitute.For<ITempDataProvider>());
        return controller;
    }
}
