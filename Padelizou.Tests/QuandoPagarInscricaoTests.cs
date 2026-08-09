using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// "PAGAR AGORA OU DEPOIS?" (Felipe, 09/08/2026).
//
// A regra que estes testes guardam é uma só: **a cobrança só nasce quando alguém diz que vai
// pagar**. Fatura criada por via das dúvidas fica pendurada na conta do organizador, no painel
// financeiro e no e-mail de cobrança do meio de pagamento.
public class QuandoPagarInscricaoTests
{
    private static Torneio Torneio(bool exigePagarNaHora = false, decimal preco = 125m) => new()
    {
        Nome = "NATA PADEL TOUR", Codigo = "NATA1",
        PrecoInscricao = preco,
        PagamentoObrigatorioNaInscricao = exigePagarNaHora,
        PrevisaoEncerramentoInscricoes = new DateTime(2026, 9, 21),
    };

    [Fact]
    public void Pergunta_no_torneio_que_aceita_pagar_ate_o_fechamento()
    {
        Assert.True(QuandoPagarInscricao.PodePerguntar(Torneio(), podeCobrar: true));
    }

    [Fact]
    public void Nao_pergunta_quando_o_pagamento_e_exigido_na_inscricao()
    {
        // Ali não há escolha: o pagamento É a inscrição.
        Assert.False(QuandoPagarInscricao.PodePerguntar(Torneio(exigePagarNaHora: true), podeCobrar: true));
    }

    [Fact]
    public void Nao_pergunta_quando_o_torneio_nao_cobra_pelo_site()
    {
        // Recebimento por fora, ou conta do organizador desconectada: não existe cobrança pra
        // adiantar, e a pergunta prometeria um checkout que não abre.
        Assert.False(QuandoPagarInscricao.PodePerguntar(Torneio(), podeCobrar: false));
    }

    [Fact]
    public void Nao_pergunta_em_torneio_de_graca()
    {
        Assert.False(QuandoPagarInscricao.PodePerguntar(Torneio(preco: 0m), podeCobrar: true));
    }

    [Fact]
    public void So_o_agora_explicito_gera_cobranca()
    {
        Assert.True(QuandoPagarInscricao.VaiPagarAgora(Torneio(), podeCobrar: true, QuandoPagarInscricao.Agora));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("depois")]
    [InlineData("AGORA")]      // o valor certo é minúsculo; qualquer outra coisa é "depois"
    [InlineData("talvez")]
    public void Qualquer_outra_resposta_vale_como_depois(string? escolha)
    {
        // ⚠️ O lado seguro do erro: quem cai aqui fica inscrito e devendo, com o botão "pagar
        // agora" à mão. O outro lado criaria fatura pra quem não pediu.
        Assert.False(QuandoPagarInscricao.VaiPagarAgora(Torneio(), podeCobrar: true, escolha));
    }

    [Fact]
    public void Pedir_para_pagar_agora_nao_fura_um_torneio_que_nao_cobra()
    {
        // O POST vem do navegador e pode ser montado à mão: a checagem de PODER cobrar tem que
        // valer aqui dentro, não só na tela que desenha o formulário.
        Assert.False(QuandoPagarInscricao.VaiPagarAgora(Torneio(), podeCobrar: false, QuandoPagarInscricao.Agora));
    }
}
