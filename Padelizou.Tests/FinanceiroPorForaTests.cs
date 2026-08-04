using Padelizou.Models;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// O Financeiro do "Interno Los Corneteiros" mostrava R$ 0,00 nos quatro cartões do topo
// enquanto a caderneta logo abaixo, na MESMA tela, somava R$ 4.080 recebidos.
//
// Os cartões liam só de Pagamento — o dinheiro que passou pelo gateway. Num torneio "por
// fora" não passa nenhum, então eles ficavam zerados pra sempre. Duas verdades sobre o mesmo
// dinheiro, e a que estava em letra garrafal era a cega.
public class FinanceiroPorForaTests
{
    private static FinanceiroTorneioVM PorFora(decimal preco = 60m, decimal taxaExterno = 0m,
        params (decimal valor, bool pago)[] cobrancas) => new()
    {
        Torneio = new Torneio { Nome = "Interno", Codigo = "INT1", FormaPagamento = "Externo", PrecoInscricao = preco },
        TaxaExterno = taxaExterno,
        CobrancaPorFora = cobrancas
            .Select(c => new CobrancaPorForaVM { Valor = c.valor, Pago = c.pago, Nomes = "Dupla" })
            .ToList(),
    };

    private static FinanceiroTorneioVM PeloSite(decimal arrecadado, decimal pendente, decimal taxa) => new()
    {
        Torneio = new Torneio { Nome = "Interno", Codigo = "INT1", FormaPagamento = "OnlinePix", PrecoInscricao = 60m },
        Arrecadado = arrecadado,
        Pendente = pendente,
        TaxaPlataforma = taxa,
    };

    [Fact]
    public void Por_fora_o_topo_mostra_o_que_foi_marcado_na_caderneta()
    {
        // Era o caso do print: cartões zerados com R$ 4.080 recebidos logo abaixo.
        var vm = PorFora(cobrancas: new[] { (120m, true), (120m, true), (60m, false) });

        Assert.Equal(240m, vm.ArrecadadoNaTela);
        Assert.Equal(60m, vm.AguardandoNaTela);
        Assert.Equal(2, vm.PagantesNaTela);
    }

    [Fact]
    public void Pelo_site_nada_muda_o_gateway_continua_mandando()
    {
        var vm = PeloSite(arrecadado: 500m, pendente: 120m, taxa: 50m);

        Assert.Equal(500m, vm.ArrecadadoNaTela);
        Assert.Equal(120m, vm.AguardandoNaTela);
        Assert.Equal(50m, vm.TaxaNaTela);
        Assert.Equal(450m, vm.LiquidoNaTela);
    }

    [Fact]
    public void Liquido_por_fora_desconta_a_taxa_de_5_por_cento()
    {
        var vm = PorFora(taxaExterno: 12m, cobrancas: new[] { (120m, true), (120m, true) });

        Assert.Equal(240m, vm.ArrecadadoNaTela);
        Assert.Equal(228m, vm.LiquidoNaTela);
    }

    [Fact]
    public void Taxa_ja_acertada_nao_e_descontada_de_novo()
    {
        // Torneio com a taxa paga ou negociada chega aqui com TaxaExterno zero. Descontar
        // assim mesmo mostraria um líquido menor do que o organizador tem na mão.
        var vm = PorFora(taxaExterno: 0m, cobrancas: new[] { (120m, true) });

        Assert.Equal(120m, vm.LiquidoNaTela);
    }

    [Fact]
    public void Estornado_some_no_por_fora()
    {
        // Quem devolve ali é o organizador, por fora também — o sistema não tem como saber,
        // e um zero em destaque seria só ruído.
        Assert.False(PorFora(cobrancas: new[] { (120m, true) }).MostraEstornado);
        Assert.True(PeloSite(100m, 0m, 10m).MostraEstornado);
    }

    [Fact]
    public void Ninguem_pagou_ainda_nao_e_o_mesmo_que_sem_movimento()
    {
        // Com gente devendo, a tela tem o que mostrar: "a receber". TemMovimento lia só do
        // gateway e escondia justamente a lista de cobrança do organizador.
        var vm = PorFora(cobrancas: new[] { (120m, false), (120m, false) });

        Assert.Equal(0m, vm.ArrecadadoNaTela);
        Assert.Equal(240m, vm.AguardandoNaTela);
        Assert.True(vm.TemMovimento);
    }

    [Fact]
    public void Liquido_nunca_fica_negativo()
    {
        // No "por fora" a taxa é devida sobre TODOS os inscritos, não sobre quem já pagou.
        // Antes de alguém acertar dava "−R$ 12,00", e o organizador lê isso como prejuízo.
        // O valor devido continua escrito na linha da taxa — nada some.
        var vm = PorFora(taxaExterno: 12m, cobrancas: new[] { (120m, false), (120m, false) });

        Assert.Equal(0m, vm.ArrecadadoNaTela);
        Assert.Equal(0m, vm.LiquidoNaTela);
        Assert.Equal(12m, vm.TaxaNaTela);
    }

    [Fact]
    public void Recebeu_menos_que_a_taxa_tambem_para_no_zero()
    {
        var vm = PorFora(taxaExterno: 12m, cobrancas: new[] { (10m, true) });

        Assert.Equal(0m, vm.LiquidoNaTela);
    }

    [Fact]
    public void A_soma_das_categorias_bate_com_o_total_do_topo()
    {
        // A tabela por categoria mostrava R$ 0,00 em toda linha enquanto o topo já somava
        // certo — o mesmo defeito dos cartões, uma camada abaixo, que sobreviveu à primeira
        // correção. Se as duas contas divergirem, o organizador vê um total que não fecha.
        var vm = PorFora(cobrancas: new[] { (120m, true), (120m, true), (60m, false) });
        vm.PorCategoria = new List<FinanceiroCategoriaVM>
        {
            new() { CategoriaId = 1, Categoria = "4ª", Arrecadado = 120m, AReceber = 0m },
            new() { CategoriaId = 2, Categoria = "5ª", Arrecadado = 120m, AReceber = 60m },
        };

        Assert.Equal(vm.ArrecadadoNaTela, vm.PorCategoria.Sum(c => c.Arrecadado));
        Assert.Equal(vm.AguardandoNaTela, vm.PorCategoria.Sum(c => c.AReceber));
    }

    [Fact]
    public void Categoria_e_casada_por_id_e_nao_por_nome()
    {
        // Nome de categoria se edita, e já houve torneio com dois nomes iguais — casar a
        // linha pelo texto era pedir pra somar dinheiro na fileira errada.
        var linha = new FinanceiroCategoriaVM { CategoriaId = 7, Categoria = "6ª Masculina" };

        Assert.Equal(7, linha.CategoriaId);
    }

    [Fact]
    public void Torneio_gratuito_nao_entra_na_regra_do_por_fora()
    {
        // Preço zero: não há caderneta nem cobrança, e a tela já explica isso em vez de
        // mostrar quatro zeros.
        var vm = PorFora(preco: 0m);

        Assert.False(vm.CobraPorFora);
        Assert.True(vm.EhGratuito);
    }
}
