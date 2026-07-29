using Padelizou.Services;

namespace Padelizou.Tests;

// Como a inscrição é cobrada: que forma a cobrança trava no meio de pagamento e que taxa o
// Padelizou fica. As duas respostas saem do MESMO lugar de propósito — travar em Pix e ficar
// com a taxa de cartão seria cobrar do organizador uma coisa e entregar outra.
public class CobrancaDoTorneioTests
{
    private static readonly TaxasExibicao Taxas = new();   // 5% externo / 10% Pix / 15% todas

    [Fact]
    public void Organizador_travou_em_Pix_nao_pergunta_nada_e_cobra_a_taxa_de_Pix()
    {
        Assert.False(CobrancaDoTorneio.JogadorEscolheAForma("OnlinePix"));

        var c = CobrancaDoTorneio.Montar("OnlinePix", escolhaDoJogador: null, Taxas);

        Assert.Equal("PIX", c.BillingType);
        Assert.Equal(10m, c.Percentual);
    }

    [Fact]
    public void Todas_as_formas_com_o_jogador_pagando_Pix_cobra_a_taxa_de_Pix()
    {
        // A regra nova (29/07/2026): aceitar cartão não pode encarecer as inscrições que
        // vieram por Pix. Antes, "todas as formas" cobrava 15% de todo mundo.
        Assert.True(CobrancaDoTorneio.JogadorEscolheAForma("OnlineTodas"));

        var c = CobrancaDoTorneio.Montar("OnlineTodas", CobrancaDoTorneio.EscolhaPix, Taxas);

        Assert.Equal("PIX", c.BillingType);
        Assert.Equal(10m, c.Percentual);
    }

    [Theory]
    [InlineData(CobrancaDoTorneio.EscolhaCartao, "CREDIT_CARD")]
    [InlineData(CobrancaDoTorneio.EscolhaBoleto, "BOLETO")]
    public void Quem_escolhe_cartao_ou_boleto_paga_a_taxa_cheia(string escolha, string billingEsperado)
    {
        var c = CobrancaDoTorneio.Montar("OnlineTodas", escolha, Taxas);

        Assert.Equal(billingEsperado, c.BillingType);
        Assert.Equal(15m, c.Percentual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bitcoin")]
    public void Escolha_ausente_ou_desconhecida_cai_no_comportamento_de_sempre(string? escolha)
    {
        // Formulário antigo em cache do navegador, ou requisição montada à mão. Errar pra
        // ESTE lado nunca cobra do organizador menos do que ele combinou — o contrário seria
        // prejuízo silencioso, que é o pior tipo.
        var c = CobrancaDoTorneio.Montar("OnlineTodas", escolha, Taxas);

        Assert.Equal("UNDEFINED", c.BillingType);
        Assert.Equal(15m, c.Percentual);
    }

    [Fact]
    public void A_forma_travada_e_a_taxa_cobrada_nunca_se_contradizem()
    {
        // O teste que justifica o serviço existir: PIX travado tem que vir com taxa de Pix,
        // e taxa de Pix só pode vir com PIX travado — em qualquer combinação de entrada.
        var entradas = new[] { CobrancaDoTorneio.EscolhaPix, CobrancaDoTorneio.EscolhaCartao,
                               CobrancaDoTorneio.EscolhaBoleto, "", "lixo", null };

        foreach (var forma in new[] { "OnlinePix", "OnlineTodas" })
            foreach (var escolha in entradas)
            {
                var c = CobrancaDoTorneio.Montar(forma, escolha, Taxas);
                Assert.Equal(c.BillingType == "PIX", c.Percentual == Taxas.ComissaoPercentualSomentePix);
            }
    }

    [Fact]
    public void Externo_nao_cobra_pelo_site_e_fica_com_a_taxa_do_externo()
    {
        Assert.False(CobrancaDoTorneio.JogadorEscolheAForma("Externo"));

        var c = CobrancaDoTorneio.Montar("Externo", null, Taxas);

        Assert.Equal(5m, c.Percentual);
    }

    [Fact]
    public void A_explicacao_de_cada_forma_diz_prazo_e_taxa()
    {
        var pix = CobrancaDoTorneio.ExplicacaoDaEscolha(CobrancaDoTorneio.EscolhaPix, Taxas);
        var cartao = CobrancaDoTorneio.ExplicacaoDaEscolha(CobrancaDoTorneio.EscolhaCartao, Taxas);

        Assert.Contains("na hora", pix);
        Assert.Contains("10%", pix);
        Assert.Contains("32 dias", cartao);   // o prazo do crédito é o que mais pesa na escolha
        Assert.Contains("15%", cartao);
    }
}
