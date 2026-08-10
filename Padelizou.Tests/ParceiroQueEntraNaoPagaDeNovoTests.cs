using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// "A DUPLA QUE JÁ PAGOU 250, QUANDO ENTRAR O NOVO PARCEIRO, NÃO COBRE DELE" (Felipe, 10/08/2026).
//
// ⚠️ O caso real: até 08/08/2026 a inscrição SEM PARCEIRO era cobrada por DUAS pessoas. Naquele
// dia a régua mudou (passou a cobrar uma), e das 20 inscrições do NATA PADEL TOUR sobrou UMA da
// época antiga — R$ 250 pagos, ainda procurando parceiro.
//
// Quando o parceiro entrasse, a soma cega faria a inscrição valer R$ 375: pediria de novo um
// dinheiro que já entrou e ainda inflaria o faturamento do organizador com R$ 125 que nunca
// chegaram (o relatório do dia do jogo e a caderneta leem esse número).
public class ParceiroQueEntraNaoPagaDeNovoTests
{
    private static Torneio Torneio(decimal preco = 125m, decimal? segundaInscricao = null,
        decimal taxaImpedimento = 0m) => new()
    {
        Nome = "NATA PADEL TOUR", Codigo = "NATA1",
        PrecoInscricao = preco,
        PermiteMultiplasCategorias = segundaInscricao != null,
        PrecoSegundaInscricao = segundaInscricao,
        TaxaPorImpedimento = taxaImpedimento,
    };

    [Fact]
    public void Quem_ja_pagou_pelos_DOIS_nao_paga_de_novo()
    {
        // O caso do Felipe: R$ 250 na mão, dupla completa custa R$ 250. Não sobe nada.
        var valor = PrecoDaInscricao.AoEntrarOParceiro(
            Torneio(), valorAtual: 250m, segundoJaEstaNoTorneio: false);

        Assert.Equal(250m, valor);
    }

    [Fact]
    public void Quem_pagou_por_UMA_pessoa_soma_a_parte_do_parceiro()
    {
        // O caso do Lucas, e a razão de a regra não poder ser "já pagou, então não soma": ele
        // pagou R$ 125 (uma pessoa), e o parceiro dele realmente ainda não pagou.
        var valor = PrecoDaInscricao.AoEntrarOParceiro(
            Torneio(), valorAtual: 125m, segundoJaEstaNoTorneio: false);

        Assert.Equal(250m, valor);
    }

    [Fact]
    public void O_parceiro_que_repete_no_torneio_entra_pelo_preco_de_segunda()
    {
        var valor = PrecoDaInscricao.AoEntrarOParceiro(
            Torneio(segundaInscricao: 100m), valorAtual: 125m, segundoJaEstaNoTorneio: true);

        Assert.Equal(225m, valor);
    }

    [Fact]
    public void O_teto_vale_tambem_quando_o_parceiro_repete()
    {
        // Já tinha 250; com o desconto do parceiro a dupla completa sairia por 225. O valor
        // fica em 250 — o que ENTROU de verdade —, e não cai pra 225.
        var valor = PrecoDaInscricao.AoEntrarOParceiro(
            Torneio(segundaInscricao: 100m), valorAtual: 250m, segundoJaEstaNoTorneio: true);

        Assert.Equal(250m, valor);
    }

    [Fact]
    public void O_teto_NUNCA_abaixa_o_que_ja_esta_gravado()
    {
        // ⚠️ Inscrição de um torneio que barateou depois: R$ 300 pagos quando a dupla custava
        // isso, e hoje custaria 250. Corrigir pra menos seria reescrever história de dinheiro —
        // e a devolução passaria a oferecer menos do que a pessoa deu.
        var valor = PrecoDaInscricao.AoEntrarOParceiro(
            Torneio(), valorAtual: 300m, segundoJaEstaNoTorneio: false);

        Assert.Equal(300m, valor);
    }

    [Fact]
    public void O_impedimento_entra_no_teto()
    {
        // A taxa de impedimento é da INSCRIÇÃO, não de um atleta: uma dupla completa com
        // impedimento custa 125 + 125 + 30. Sem contá-la no teto, quem tem impedimento seria
        // travado cedo demais e o parceiro entraria de graça.
        var valor = PrecoDaInscricao.AoEntrarOParceiro(
            Torneio(taxaImpedimento: 30m), valorAtual: 155m, segundoJaEstaNoTorneio: false,
            impedimentos: 1);

        Assert.Equal(280m, valor);
    }

    [Fact]
    public void Torneio_de_graca_continua_de_graca()
    {
        Assert.Equal(0m, PrecoDaInscricao.AoEntrarOParceiro(
            Torneio(preco: 0m), valorAtual: 0m, segundoJaEstaNoTorneio: false));
    }
}
