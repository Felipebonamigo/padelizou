using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O bar do clube: comanda, caixa e cardápio. Módulo EM CONSTRUÇÃO — enquanto
// `Bar:Habilitado` estiver desligado, só admin do Padelizou entra. Os testes cobrem as duas
// coisas que não podem falhar: a trava de visibilidade e o dinheiro.
public class BarDoClubeTests
{
    // ---------- As regras puras ----------

    [Fact]
    public void Numero_da_comanda_nao_reusa_o_de_uma_cancelada()
    {
        // Comanda cancelada continua ocupando o número dela — é o que o papel do balcão diz.
        // Reusar faria duas comandas diferentes atenderem por "a 3" no mesmo turno.
        Assert.Equal(1, BarDoClube.ProximoNumero(Array.Empty<int>()));
        Assert.Equal(4, BarDoClube.ProximoNumero(new[] { 1, 2, 3 }));
        Assert.Equal(4, BarDoClube.ProximoNumero(new[] { 3, 1 }));      // o 2 foi cancelado
        Assert.Equal(11, BarDoClube.ProximoNumero(new[] { 10 }));
    }

    [Fact]
    public void Caixa_confere_so_o_que_passa_pela_gaveta()
    {
        // Abriu com R$ 100 de troco, vendeu R$ 250 em dinheiro → tem que ter R$ 350 lá.
        var (esperado, diferenca) = BarDoClube.ConferenciaDoCaixa(100m, 250m, 350m);
        Assert.Equal(350m, esperado);
        Assert.Equal(0m, diferenca);

        // Faltando R$ 20.
        (_, diferenca) = BarDoClube.ConferenciaDoCaixa(100m, 250m, 330m);
        Assert.Equal(-20m, diferenca);

        // Sobrando R$ 5.
        (_, diferenca) = BarDoClube.ConferenciaDoCaixa(100m, 250m, 355m);
        Assert.Equal(5m, diferenca);
    }

    [Fact]
    public void Centavos_no_caixa_batem_exatamente()
    {
        // A conta de dinheiro se testa COM centavos: foi justamente o número redondo que
        // escondeu por semanas o bug em que 79,90 virava 7990.
        var (esperado, diferenca) = BarDoClube.ConferenciaDoCaixa(50.50m, 189.70m, 240.20m);
        Assert.Equal(240.20m, esperado);
        Assert.Equal(0m, diferenca);
    }

    [Fact]
    public void Produto_precisa_de_nome_e_preco_que_facam_sentido()
    {
        Assert.NotNull(BarDoClube.ProblemaComProduto("", 10m));
        Assert.NotNull(BarDoClube.ProblemaComProduto("   ", 10m));
        Assert.NotNull(BarDoClube.ProblemaComProduto("Cerveja", -1m));
        Assert.NotNull(BarDoClube.ProblemaComProduto(new string('x', 81), 10m));

        Assert.Null(BarDoClube.ProblemaComProduto("Heineken lata", 12.50m));
        // Preço zero é VÁLIDO: cortesia e brinde existem, e o item precisa aparecer na
        // comanda pra o estoque bater quando ele chegar.
        Assert.Null(BarDoClube.ProblemaComProduto("Água de cortesia", 0m));
    }

    [Fact]
    public void Comanda_nao_fecha_sem_forma_de_pagamento_nem_com_desconto_maior_que_a_conta()
    {
        Assert.NotNull(BarDoClube.ProblemaParaFechar(100m, 0m, null));
        Assert.NotNull(BarDoClube.ProblemaParaFechar(100m, 0m, "Bitcoin"));
        Assert.NotNull(BarDoClube.ProblemaParaFechar(100m, -5m, BarDoClube.Dinheiro));
        // Desconto maior que a conta viraria "o bar deve dinheiro ao cliente".
        Assert.NotNull(BarDoClube.ProblemaParaFechar(100m, 150m, BarDoClube.Dinheiro));

        Assert.Null(BarDoClube.ProblemaParaFechar(100m, 10m, BarDoClube.Dinheiro));
        Assert.Null(BarDoClube.ProblemaParaFechar(100m, 100m, BarDoClube.Cortesia));
    }

    // ---------- O total da comanda ----------

    [Fact]
    public void Item_cancelado_sai_da_conta_mas_nao_do_historico()
    {
        var comanda = new Comanda();
        comanda.Itens.Add(new ItemComanda { Descricao = "Heineken", PrecoUnitario = 12.50m, Quantidade = 2 });
        comanda.Itens.Add(new ItemComanda { Descricao = "Água", PrecoUnitario = 5m, Quantidade = 1 });
        comanda.Itens.Add(new ItemComanda
        {
            Descricao = "Porção",
            PrecoUnitario = 40m,
            Quantidade = 1,
            CanceladoEm = DateTime.Now,
            MotivoCancelamento = "cliente desistiu",
        });

        Assert.Equal(30m, comanda.Subtotal);      // 25 + 5, sem a porção cancelada
        Assert.Equal(30m, comanda.Total);
        // A linha cancelada CONTINUA na comanda: item que some é a forma mais comum de furto
        // num bar, e é o histórico que permite auditar.
        Assert.Equal(3, comanda.Itens.Count);
    }

    [Fact]
    public void Desconto_abate_o_total_mas_o_subtotal_continua_contando_a_venda()
    {
        var comanda = new Comanda { Desconto = 10m };
        comanda.Itens.Add(new ItemComanda { PrecoUnitario = 12.50m, Quantidade = 4 });

        Assert.Equal(50m, comanda.Subtotal);   // o que foi vendido
        Assert.Equal(40m, comanda.Total);      // o que o cliente pagou
    }

    // ---------- A trava de visibilidade ----------

    private static ModuloDoBar Modulo(DbPadelContext ctx, bool habilitado) =>
        new(ctx, Options.Create(new BarSettings { Habilitado = habilitado }));

    private static BarController Controller(DbPadelContext ctx, int usuarioId, bool habilitado)
    {
        var modulo = Modulo(ctx, habilitado);
        var c = new BarController(ctx, modulo,
            new ModuloFiscal(ctx, modulo, Options.Create(new FiscalSettings { Habilitado = habilitado })),
            TestInfra.CepQueNaoResponde(),
            new NotasDoClube(ctx, NullLogger<NotasDoClube>.Instance),
            new MedidorDeFranquia(ctx, Options.Create(new PlanoClubeSettings())),
            new EmissorFiscalDesligado(),
            NullLogger<BarController>.Instance);

        c.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) }, "Teste")),
            },
        };
        c.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            c.HttpContext, NSubstitute.Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        return c;
    }

    // Dono do clube (id 1), admin do Padelizou que também é dono (id 2), estranho (id 3).
    //
    // ⚠️ O clube nasce COM ASSINATURA EM DIA desde 19/08/2026, e isso não é detalhe de
    // montagem: o bar virou plano pago (ver Services/PlanoDoClube), e um clube sem plano é
    // mandado pra tela de assinatura em vez de abrir o balcão. Estes testes são sobre o
    // BALCÃO — quem cuida da porta é o PlanoDoClubeTests.
    private static DbPadelContext Cenario()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Id = 1, Nome = "Dono do Clube", Cpf = "1" });
        ctx.Jogadores.Add(new Jogador { Id = 2, Nome = "Felipe", Cpf = "2", IsAdminRaiz = true });
        ctx.Jogadores.Add(new Jogador { Id = 3, Nome = "Estranho", Cpf = "3" });
        ctx.Clubes.Add(new Clube
        {
            Id = 1, Nome = "Arena Teste", DonoId = 1,
            PlanoDoClube = PlanoDoClube.Gestao,
            AssinaturaClubePagaAte = DateTime.Now.AddMonths(1)
        });
        ctx.ClubeAdministradores.Add(new ClubeAdministrador { ClubeId = 1, JogadorId = 2, AdicionadoEm = DateTime.Now });
        ctx.SaveChanges();
        return ctx;
    }

    [Fact]
    public async Task Em_construcao_nem_o_dono_do_clube_entra()
    {
        var ctx = Cenario();

        // O dono do clube manda no clube, mas o módulo ainda não existe pra ele.
        Assert.IsType<ForbidResult>(await Controller(ctx, 1, habilitado: false).Index(1));
        Assert.IsType<ForbidResult>(await Controller(ctx, 1, habilitado: false).Produtos(1));
    }

    [Fact]
    public async Task Em_construcao_o_admin_do_padelizou_entra()
    {
        var ctx = Cenario();

        Assert.IsType<ViewResult>(await Controller(ctx, 2, habilitado: false).Index(1));
    }

    [Fact]
    public async Task Ligado_o_dono_entra_e_o_estranho_continua_de_fora()
    {
        var ctx = Cenario();

        Assert.IsType<ViewResult>(await Controller(ctx, 1, habilitado: true).Index(1));
        // Ligar o módulo não afrouxa a dona da regra: só dono e administrador do clube.
        Assert.IsType<ForbidResult>(await Controller(ctx, 3, habilitado: true).Index(1));
    }

    // ---------- O fluxo do balcão ----------

    [Fact]
    public async Task Comanda_abre_com_nome_e_recebe_o_preco_congelado_do_produto()
    {
        var ctx = Cenario();
        ctx.ProdutosBar.Add(new ProdutoBar { Id = 1, ClubeId = 1, Nome = "Heineken", Preco = 12.50m });
        ctx.SaveChanges();

        var c = Controller(ctx, 2, habilitado: false);
        await c.AbrirComanda(1, "Rafael", null);

        var comanda = Assert.Single(ctx.Comandas);
        Assert.Equal("Rafael", comanda.NomeCliente);
        Assert.Equal(1, comanda.Numero);
        Assert.True(comanda.EstaAberta);

        await c.LancarItem(comanda.Id, produtoBarId: 1, descricaoLivre: null, precoLivre: null, quantidade: 2);

        var item = Assert.Single(ctx.ItensComanda);
        Assert.Equal("Heineken", item.Descricao);
        Assert.Equal(12.50m, item.PrecoUnitario);
        Assert.Equal(25m, item.Total);
    }

    [Fact]
    public async Task Mudar_o_preco_do_produto_nao_mexe_no_que_ja_foi_vendido()
    {
        // É o bug clássico de sistema de bar: a comanda somava lendo o preço ATUAL, e
        // aumentar a cerveja às 20h mudava o valor de tudo que foi vendido às 15h — e das
        // comandas fechadas do mês inteiro.
        var ctx = Cenario();
        ctx.ProdutosBar.Add(new ProdutoBar { Id = 1, ClubeId = 1, Nome = "Heineken", Preco = 12.50m });
        ctx.SaveChanges();

        var c = Controller(ctx, 2, habilitado: false);
        await c.AbrirComanda(1, "Rafael", null);
        var comandaId = ctx.Comandas.Single().Id;
        await c.LancarItem(comandaId, produtoBarId: 1, descricaoLivre: null, precoLivre: null);

        // O bar reajusta o preço no meio do dia.
        ctx.ProdutosBar.Single().Preco = 15m;
        ctx.SaveChanges();

        Assert.Equal(12.50m, ctx.ItensComanda.Single().PrecoUnitario);
    }

    [Fact]
    public async Task Comanda_da_reserva_nasce_com_o_nome_de_quem_esta_na_quadra()
    {
        var ctx = Cenario();
        ctx.QuadrasClube.Add(new QuadraClube { Id = 1, ClubeId = 1, Nome = "Quadra 1", Ativa = true });
        ctx.MarcacoesJogo.Add(new MarcacaoJogo
        {
            Id = 1,
            ClubeId = 1,
            QuadraClubeId = 1,
            JogadorId = 1,
            DataHora = DateTime.Now,
            DuracaoMinutos = 60,
            NomeClienteBalcao = "Índio",
            CelularClienteBalcao = "51999998888",
        });
        ctx.SaveChanges();

        // Sem digitar nome nenhum: é a vantagem de bar e quadra viverem no mesmo sistema.
        await Controller(ctx, 2, habilitado: false).AbrirComanda(1, nomeCliente: null, marcacaoJogoId: 1);

        var comanda = Assert.Single(ctx.Comandas);
        Assert.Equal("Índio", comanda.NomeCliente);
        Assert.Equal("51999998888", comanda.Celular);
        Assert.Equal(1, comanda.MarcacaoJogoId);
    }

    [Fact]
    public async Task Comanda_fechada_nao_aceita_mais_item()
    {
        var ctx = Cenario();
        ctx.ProdutosBar.Add(new ProdutoBar { Id = 1, ClubeId = 1, Nome = "Água", Preco = 5m });
        ctx.SaveChanges();

        var c = Controller(ctx, 2, habilitado: false);
        await c.AbrirComanda(1, "Rafael", null);
        var comandaId = ctx.Comandas.Single().Id;
        await c.LancarItem(comandaId, 1, null, null);
        await c.FecharComanda(comandaId, BarDoClube.Dinheiro);

        await c.LancarItem(comandaId, 1, null, null);

        Assert.Single(ctx.ItensComanda);   // continua com um item só
        Assert.Equal(Comanda.Fechada, ctx.Comandas.Single().Status);
    }

    [Fact]
    public async Task Caixa_nao_fecha_com_comanda_aberta()
    {
        // O erro clássico: o consumo em aberto não entra na conferência e o dia fecha
        // "faltando" dinheiro que ninguém deve.
        var ctx = Cenario();
        var c = Controller(ctx, 2, habilitado: false);

        await c.AbrirCaixa(1, 100m);
        await c.AbrirComanda(1, "Rafael", null);
        await c.FecharCaixa(1, 100m, null);

        Assert.Null(ctx.CaixasDoDia.Single().FechadoEm);
    }

    [Fact]
    public async Task Caixa_nao_fecha_com_o_valor_contado_em_branco()
    {
        // Achado testando o fluxo de ponta a ponta: com `decimal` não-nulável, campo em
        // branco chegava como ZERO e o caixa fechava acusando um rombo do tamanho do dia
        // inteiro — e fechar caixa não tem volta. O `required` do formulário protege o
        // clique normal, mas não protege página velha em cache nem POST feito à mão.
        var ctx = Cenario();
        var c = Controller(ctx, 2, habilitado: false);

        await c.AbrirCaixa(1, 100m);
        await c.FecharCaixa(1, valorContado: null, observacao: null);

        var caixa = ctx.CaixasDoDia.Single();
        Assert.Null(caixa.FechadoEm);
        Assert.Null(caixa.ValorContado);
    }

    [Fact]
    public async Task Caixa_fechado_guarda_a_contagem_com_centavos()
    {
        var ctx = Cenario();
        var c = Controller(ctx, 2, habilitado: false);

        await c.AbrirCaixa(1, 100.50m);
        await c.FecharCaixa(1, 128.50m, "conferido");

        var caixa = ctx.CaixasDoDia.Single();
        Assert.Equal(128.50m, caixa.ValorContado);
        Assert.NotNull(caixa.FechadoEm);
    }

    [Fact]
    public async Task Cancelar_item_deixa_rastro_de_quem_e_por_que()
    {
        var ctx = Cenario();
        ctx.ProdutosBar.Add(new ProdutoBar { Id = 1, ClubeId = 1, Nome = "Porção", Preco = 40m });
        ctx.SaveChanges();

        var c = Controller(ctx, 2, habilitado: false);
        await c.AbrirComanda(1, "Rafael", null);
        var comandaId = ctx.Comandas.Single().Id;
        await c.LancarItem(comandaId, 1, null, null);

        var itemId = ctx.ItensComanda.Single().Id;
        await c.CancelarItem(itemId, "cozinha não tinha");

        var item = ctx.ItensComanda.Single();
        Assert.NotNull(item.CanceladoEm);
        Assert.Equal(2, item.CanceladoPorId);
        Assert.Equal("cozinha não tinha", item.MotivoCancelamento);
    }
}
