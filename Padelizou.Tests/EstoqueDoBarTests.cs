using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Estoque do bar. A premissa: NÃO é exato, e prometer exatidão é o jeito mais rápido de o
// dono parar de confiar e voltar pro caderno. O que segura o saldo colado na prateleira é a
// contagem semanal — por isso perda e contagem são de primeira classe, e o balcão NUNCA é
// impedido de vender por causa do número.
public class EstoqueDoBarTests
{
    // ---------- As regras puras ----------

    [Fact]
    public void Compra_por_caixa_vira_unidade_de_venda()
    {
        // O erro mais comum de todo controle de estoque de bar: digitar 24 e ficar com 24
        // caixas no sistema.
        Assert.Equal(72, EstoqueDoBar.UnidadesDaCompra(3, 24, porEmbalagem: true));
        Assert.Equal(3, EstoqueDoBar.UnidadesDaCompra(3, 24, porEmbalagem: false));
        // Produto que compra e vende na mesma unidade.
        Assert.Equal(10, EstoqueDoBar.UnidadesDaCompra(10, 1, porEmbalagem: true));
        // Embalagem zerada por engano não pode zerar a compra.
        Assert.Equal(5, EstoqueDoBar.UnidadesDaCompra(5, 0, porEmbalagem: true));
    }

    [Fact]
    public void O_saldo_e_a_soma_dos_movimentos()
    {
        var movimentos = new[]
        {
            new MovimentoEstoque { Tipo = MovimentoEstoque.Entrada, Quantidade = 48 },
            new MovimentoEstoque { Tipo = MovimentoEstoque.Venda, Quantidade = -14 },
            new MovimentoEstoque { Tipo = MovimentoEstoque.Perda, Quantidade = -2 },
            new MovimentoEstoque { Tipo = MovimentoEstoque.Estorno, Quantidade = 1 },
        };

        Assert.Equal(33, EstoqueDoBar.Saldo(movimentos));
        Assert.Equal(0, EstoqueDoBar.Saldo(Array.Empty<MovimentoEstoque>()));
    }

    [Fact]
    public void A_contagem_acerta_a_diferenca_nos_dois_sentidos()
    {
        Assert.Equal(-3, EstoqueDoBar.AjusteDaContagem(saldoAtual: 20, contado: 17));  // sumiu
        Assert.Equal(4, EstoqueDoBar.AjusteDaContagem(saldoAtual: 20, contado: 24));   // sobrou
        Assert.Equal(0, EstoqueDoBar.AjusteDaContagem(saldoAtual: 20, contado: 20));   // bateu
    }

    [Fact]
    public void O_aviso_de_repor_respeita_o_minimo_zerado()
    {
        Assert.True(EstoqueDoBar.PrecisaRepor(saldo: 3, estoqueMinimo: 6));
        Assert.True(EstoqueDoBar.PrecisaRepor(saldo: 6, estoqueMinimo: 6));   // no limite já avisa
        Assert.False(EstoqueDoBar.PrecisaRepor(saldo: 7, estoqueMinimo: 6));
        // Mínimo zero desliga: nem todo produto merece um aviso.
        Assert.False(EstoqueDoBar.PrecisaRepor(saldo: 0, estoqueMinimo: 0));
    }

    [Fact]
    public void A_margem_usa_o_ultimo_custo_e_nao_divide_por_zero()
    {
        var m = EstoqueDoBar.Margem(12.50m, 7.90m);
        Assert.NotNull(m);
        Assert.Equal(4.60m, m!.Value.Lucro);
        Assert.Equal(58.2m, m.Value.Percentual);

        // Sem compra registrada não há margem que se calcule.
        Assert.Null(EstoqueDoBar.Margem(12.50m, null));

        // Custo zero (brinde) tem lucro, mas percentual não: dividir por zero não é
        // "infinito" numa tela, é conta que não faz sentido mostrar.
        var brinde = EstoqueDoBar.Margem(5m, 0m);
        Assert.NotNull(brinde);
        Assert.Equal(5m, brinde!.Value.Lucro);
        Assert.Null(brinde.Value.Percentual);

        // Vender abaixo do custo aparece como prejuízo, não some.
        var prejuizo = EstoqueDoBar.Margem(5m, 8m);
        Assert.Equal(-3m, prejuizo!.Value.Lucro);
    }

    // ---------- De ponta a ponta ----------

    private static BarController Controller(DbPadelContext ctx, int usuarioId)
    {
        var c = new BarController(ctx,
            new ModuloDoBar(ctx, Options.Create(new BarSettings { Habilitado = false })),
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

    // Admin do Padelizou (id 2) que administra o clube 1, com uma cerveja controlada.
    private static DbPadelContext Cenario()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Id = 2, Nome = "Felipe", Cpf = "2", IsAdminRaiz = true });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Arena Teste", DonoId = 2 });
        ctx.ProdutosBar.Add(new ProdutoBar
        {
            Id = 1,
            ClubeId = 1,
            Nome = "Heineken lata",
            Preco = 12.50m,
            ControlaEstoque = true,
            UnidadesPorEmbalagem = 24,
            EstoqueMinimo = 6,
        });
        ctx.SaveChanges();
        return ctx;
    }

    private static int Saldo(DbPadelContext ctx, int produtoId) =>
        EstoqueDoBar.Saldo(ctx.MovimentosEstoque.Where(m => m.ProdutoBarId == produtoId).ToList());

    [Fact]
    public async Task Vender_baixa_o_estoque_sozinho()
    {
        // A baixa pega carona no lançamento: o operador não faz NADA a mais, que é a única
        // forma de estoque sobreviver a um sábado cheio.
        var ctx = Cenario();
        var c = Controller(ctx, 2);

        await c.RegistrarEntrada(1, quantidade: 2, porEmbalagem: true, custoUnitario: 7.90m);
        Assert.Equal(48, Saldo(ctx, 1));

        await c.AbrirComanda(1, "Rafael", null);
        var comandaId = ctx.Comandas.Single().Id;
        await c.LancarItem(comandaId, produtoBarId: 1, descricaoLivre: null, precoLivre: null, quantidade: 3);

        Assert.Equal(45, Saldo(ctx, 1));
    }

    [Fact]
    public async Task Cancelar_item_devolve_a_unidade_pra_geladeira()
    {
        // Sem isto, cancelar item furaria o estoque em silêncio e a diferença só apareceria na
        // contagem da semana seguinte, quando ninguém mais lembra do que aconteceu.
        var ctx = Cenario();
        var c = Controller(ctx, 2);

        await c.RegistrarEntrada(1, quantidade: 10, porEmbalagem: false, custoUnitario: null);
        await c.AbrirComanda(1, "Rafael", null);
        var comandaId = ctx.Comandas.Single().Id;
        await c.LancarItem(comandaId, 1, null, null, quantidade: 2);
        Assert.Equal(8, Saldo(ctx, 1));

        var itemId = ctx.ItensComanda.Single().Id;
        await c.CancelarItem(itemId, "cliente desistiu");

        Assert.Equal(10, Saldo(ctx, 1));
        // O histórico conta os dois lados: saiu 2, voltou 2.
        Assert.Contains(ctx.MovimentosEstoque, m => m.Tipo == MovimentoEstoque.Estorno && m.Quantidade == 2);
    }

    [Fact]
    public async Task Produto_sem_controle_nao_gera_movimento_nenhum()
    {
        // Porção e drink dependem da mão de quem serve: um saldo que mente é pior que saldo
        // nenhum.
        var ctx = Cenario();
        ctx.ProdutosBar.Add(new ProdutoBar { Id = 2, ClubeId = 1, Nome = "Porção", Preco = 40m });
        ctx.SaveChanges();

        var c = Controller(ctx, 2);
        await c.AbrirComanda(1, "Rafael", null);
        var comandaId = ctx.Comandas.Single().Id;
        await c.LancarItem(comandaId, produtoBarId: 2, descricaoLivre: null, precoLivre: null);

        Assert.Empty(ctx.MovimentosEstoque);
    }

    [Fact]
    public async Task Saldo_zerado_nao_impede_a_venda()
    {
        // A prateleira manda, não o sistema. Recusar uma cerveja que já está na mão do cliente
        // porque o número não bate seria o jeito mais rápido de o bar desligar o estoque pra
        // sempre. Negativo é sinal de ENTRADA não registrada.
        var ctx = Cenario();
        var c = Controller(ctx, 2);

        await c.AbrirComanda(1, "Rafael", null);
        var comandaId = ctx.Comandas.Single().Id;
        await c.LancarItem(comandaId, 1, null, null, quantidade: 5);

        Assert.Single(ctx.ItensComanda);        // a venda aconteceu
        Assert.Equal(-5, Saldo(ctx, 1));        // e o sistema avisa que falta registrar entrada
    }

    [Fact]
    public async Task Contagem_que_bate_nao_suja_o_historico()
    {
        var ctx = Cenario();
        var c = Controller(ctx, 2);
        await c.RegistrarEntrada(1, 20, false, null);

        await c.RegistrarContagem(1, contado: 20);

        // Uma linha de "ajuste de 0" só encheria o histórico de registro que não conta nada.
        Assert.Single(ctx.MovimentosEstoque);
        Assert.Equal(20, Saldo(ctx, 1));
    }

    [Fact]
    public async Task Contagem_que_nao_bate_acerta_e_registra_o_que_o_sistema_achava()
    {
        var ctx = Cenario();
        var c = Controller(ctx, 2);
        await c.RegistrarEntrada(1, 20, false, null);

        await c.RegistrarContagem(1, contado: 17);

        Assert.Equal(17, Saldo(ctx, 1));
        var ajuste = ctx.MovimentosEstoque.Single(m => m.Tipo == MovimentoEstoque.Contagem);
        Assert.Equal(-3, ajuste.Quantidade);
        // O motivo guarda os dois números — é o que permite entender a diferença depois.
        Assert.Contains("20", ajuste.Motivo);
        Assert.Contains("17", ajuste.Motivo);
    }

    [Fact]
    public async Task Perda_sem_motivo_e_recusada()
    {
        // Perda sem explicação é um buraco: é justamente o registro que o dono vai ler quando
        // quiser saber por que sumiram seis cervejas na sexta.
        var ctx = Cenario();
        var c = Controller(ctx, 2);
        await c.RegistrarEntrada(1, 20, false, null);

        await c.RegistrarPerda(1, 2, motivo: null);
        Assert.Equal(20, Saldo(ctx, 1));

        await c.RegistrarPerda(1, 2, motivo: "caixa caiu");
        Assert.Equal(18, Saldo(ctx, 1));
    }

    [Fact]
    public async Task Entrada_com_custo_pode_virar_conta_a_pagar()
    {
        // A ponte com a fase 2: a mesma compra que entra no estoque vira o boleto que vence.
        // Digitar duas vezes é o que faz o dono desistir de uma das duas telas.
        var ctx = Cenario();

        await Controller(ctx, 2).RegistrarEntrada(1, quantidade: 2, porEmbalagem: true,
            custoUnitario: 7.90m, fornecedor: "Ambev", lancarEmContas: true);

        var conta = Assert.Single(ctx.LancamentosFinanceiros);
        Assert.Equal(LancamentoFinanceiro.Pagar, conta.Tipo);
        Assert.Equal(379.20m, conta.Valor);       // 48 unidades × R$ 7,90
        Assert.Equal("Ambev", conta.Contraparte);
    }

    [Fact]
    public async Task Sem_marcar_a_opcao_a_compra_nao_vira_conta()
    {
        var ctx = Cenario();
        await Controller(ctx, 2).RegistrarEntrada(1, 10, false, 7.90m, "Ambev", lancarEmContas: false);
        Assert.Empty(ctx.LancamentosFinanceiros);
    }

    [Fact]
    public async Task Editar_o_preco_pelo_cardapio_nao_desliga_o_controle_de_estoque()
    {
        // Com parâmetro não-nulável e valor padrão `false`, salvar o produto pela tela de
        // cardápio desligaria o estoque em silêncio — e o dono só descobriria na contagem
        // seguinte, sem nenhuma pista.
        var ctx = Cenario();

        await Controller(ctx, 2).SalvarProduto(1, id: 1, nome: "Heineken lata", preco: 13.50m,
            categoria: "Bebidas");

        var produto = ctx.ProdutosBar.Single();
        Assert.Equal(13.50m, produto.Preco);
        Assert.True(produto.ControlaEstoque);
        Assert.Equal(24, produto.UnidadesPorEmbalagem);
        Assert.Equal(6, produto.EstoqueMinimo);
    }
}
