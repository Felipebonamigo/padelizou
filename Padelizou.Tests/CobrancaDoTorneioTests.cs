using Padelizou.Models;
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

    [Fact]
    public void Quem_escolhe_cartao_paga_a_taxa_cheia()
    {
        var c = CobrancaDoTorneio.Montar("OnlineTodas", CobrancaDoTorneio.EscolhaCartao, Taxas);

        Assert.Equal("CREDIT_CARD", c.BillingType);
        Assert.Equal(15m, c.Percentual);
    }

    [Fact]
    public void Boleto_paga_a_taxa_do_Pix()
    {
        // Decisão do Felipe (29/07/2026): pro meio de pagamento, boleto e Pix custam o mesmo
        // valor fixo em centavos — quem encarece é o cartão. Então o boleto não pode carregar
        // a taxa do cartão.
        var c = CobrancaDoTorneio.Montar("OnlineTodas", CobrancaDoTorneio.EscolhaBoleto, Taxas);

        Assert.Equal("BOLETO", c.BillingType);
        Assert.Equal(10m, c.Percentual);
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
        // O teste que justifica o serviço existir: forma barata (Pix/boleto) tem que vir com
        // a taxa menor, e a taxa menor só pode vir com forma barata — em qualquer combinação.
        var entradas = new[] { CobrancaDoTorneio.EscolhaPix, CobrancaDoTorneio.EscolhaCartao,
                               CobrancaDoTorneio.EscolhaBoleto, "", "lixo", null };

        foreach (var forma in new[] { "OnlinePix", "OnlineTodas" })
            foreach (var escolha in entradas)
            {
                var c = CobrancaDoTorneio.Montar(forma, escolha, Taxas);
                Assert.Equal(c.BillingType is "PIX" or "BOLETO",
                             c.Percentual == Taxas.ComissaoPercentualSomentePix);
            }
    }

    [Fact]
    public void Externo_nao_cobra_pelo_site_e_fica_com_a_taxa_do_externo()
    {
        Assert.False(CobrancaDoTorneio.JogadorEscolheAForma("Externo"));

        var c = CobrancaDoTorneio.Montar("Externo", null, Taxas);

        Assert.Equal(5m, c.Percentual);
    }

    // O AMERICANO NÃO PAGA PERCENTUAL, em nenhuma forma de recebimento. A monetização dele
    // é R$ 5 por pessoa se comprar o Ranking Americano, acertada por fora.
    [Theory]
    [InlineData("Americano", false)]
    [InlineData("Americano", true)]
    [InlineData("AmericanoDuplas", false)]
    [InlineData("AmericanoDuplas", true)]
    public void Americano_nunca_paga_percentual_em_qualquer_forma(string formato, bool compraRanking)
    {
        foreach (var forma in new[] { "Externo", "OnlinePix", "OnlineTodas" })
        {
            var torneio = new Torneio
            {
                Nome = "x", Codigo = "x", Formato = formato, FormaPagamento = forma,
                PontuaNoRankingAmericano = compraRanking,
            };
            Assert.True(CobrancaDoTorneio.IsentoDeTaxa(torneio));
            Assert.Equal(0m, CobrancaDoTorneio.Montar(torneio, null, Taxas).Percentual);
            Assert.Equal(0m, CobrancaDoTorneio.PercentualExibicao(torneio, Taxas));
        }
    }

    [Fact]
    public void Comprar_o_ranking_NAO_traz_o_percentual_de_volta()
    {
        // O defeito que cobrou errado num torneio real (o da Carol, 07/08/2026): a primeira
        // versão isentava só o Americano SEM ranking, então quem comprava o ranking pagava
        // as DUAS coisas — os R$ 5 por pessoa E o percentual sobre cada inscrição.
        var comRanking = new Torneio
        {
            Nome = "x", Codigo = "x", Formato = "Americano", FormaPagamento = "Externo",
            PontuaNoRankingAmericano = true,
        };
        var semRanking = new Torneio
        {
            Nome = "x", Codigo = "x", Formato = "Americano", FormaPagamento = "Externo",
            PontuaNoRankingAmericano = false,
        };

        // Comprar ponto não pode virar motivo pra passar a dever comissão: os dois dão zero.
        Assert.Equal(CobrancaDoTorneio.Montar(semRanking, null, Taxas).Percentual,
                     CobrancaDoTorneio.Montar(comRanking, null, Taxas).Percentual);
        Assert.Equal(0m, CobrancaDoTorneio.Montar(comRanking, null, Taxas).Percentual);
    }

    [Fact]
    public void Oficial_nunca_e_isento_mesmo_sem_a_flag_de_ranking()
    {
        // PontuaNoRankingAmericano não existe pro Oficial — a flag pode vir falsa à toa,
        // e isso não pode virar isenção de taxa por acidente.
        var torneio = new Torneio
        {
            Nome = "x", Codigo = "x", Formato = "Padrao", FormaPagamento = "Externo",
            PontuaNoRankingAmericano = false,
        };
        Assert.False(CobrancaDoTorneio.IsentoDeTaxa(torneio));
        Assert.Equal(5m, CobrancaDoTorneio.Montar(torneio, null, Taxas).Percentual);
    }

    [Fact]
    public void A_explicacao_de_cada_forma_diz_prazo_e_taxa()
    {
        var pix = CobrancaDoTorneio.ExplicacaoDaEscolha(CobrancaDoTorneio.EscolhaPix, Taxas);
        var cartao = CobrancaDoTorneio.ExplicacaoDaEscolha(CobrancaDoTorneio.EscolhaCartao, Taxas);
        var boleto = CobrancaDoTorneio.ExplicacaoDaEscolha(CobrancaDoTorneio.EscolhaBoleto, Taxas);

        Assert.Contains("na hora", pix);
        Assert.Contains("10%", pix);
        Assert.Contains("32 dias", cartao);   // o prazo do crédito é o que mais pesa na escolha
        Assert.Contains("15%", cartao);
        Assert.Contains("10%", boleto);       // boleto compartilha a taxa do Pix
    }
}
