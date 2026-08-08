using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A SEGUNDA CATEGORIA DO MESMO JOGADOR É MAIS BARATA (08/08/2026, pedido do Felipe).
//
// É como o padel cobra: a segunda categoria não traz um atleta novo, traz mais jogos pro
// mesmo. ⚠️ O desconto é POR PESSOA, não por inscrição — numa dupla em que um repete e o
// outro é novo, um lado paga cheio e o outro paga o desconto.
public class PrecoDaSegundaInscricaoTests
{
    private static Torneio Torneio(decimal preco = 100m, decimal? segunda = 60m, bool multiplas = true) =>
        new()
        {
            Nome = "T", Codigo = "T1",
            PrecoInscricao = preco,
            PrecoSegundaInscricao = segunda,
            PermiteMultiplasCategorias = multiplas,
        };

    [Fact]
    public void Primeira_inscricao_paga_o_preco_cheio()
    {
        Assert.Equal(100m, PrecoDaInscricao.PorPessoa(Torneio(), jaEstaNoTorneio: false));
    }

    [Fact]
    public void Segunda_inscricao_paga_o_desconto()
    {
        Assert.Equal(60m, PrecoDaInscricao.PorPessoa(Torneio(), jaEstaNoTorneio: true));
    }

    [Fact]
    public void Sem_segundo_preco_definido_a_segunda_custa_igual()
    {
        Assert.Equal(100m, PrecoDaInscricao.PorPessoa(Torneio(segunda: null), jaEstaNoTorneio: true));
    }

    [Fact]
    public void Sem_multiplas_categorias_o_desconto_nao_vale()
    {
        // Nem que o campo tenha ficado preenchido de uma configuração anterior: com a chave
        // desligada ninguém entra duas vezes, e um preço escondido esperando pra valer
        // sozinho é armadilha.
        Assert.Equal(100m, PrecoDaInscricao.PorPessoa(Torneio(multiplas: false), jaEstaNoTorneio: true));
    }

    [Fact]
    public void Numa_dupla_so_quem_repete_ganha_o_desconto()
    {
        // ⚠️ O coração da regra. 100 (quem é novo) + 60 (quem repete) = 160 — e NÃO
        // "preço da dupla", que era como o cálculo antigo pensava.
        Assert.Equal(160m, PrecoDaInscricao.Total(Torneio(), new[] { false, true }));
    }

    [Fact]
    public void Dupla_em_que_os_dois_repetem_paga_dois_descontos()
    {
        Assert.Equal(120m, PrecoDaInscricao.Total(Torneio(), new[] { true, true }));
    }

    [Fact]
    public void Dupla_de_dois_novatos_paga_como_sempre_pagou()
    {
        // A regressão que importa: torneio sem desconto configurado não pode mudar de preço.
        Assert.Equal(200m, PrecoDaInscricao.Total(Torneio(segunda: null), new[] { false, false }));
    }

    [Fact]
    public void Impedimento_entra_por_INSCRICAO_e_nao_ganha_desconto()
    {
        // O impedimento é uma janela da grade que se perde, não um atleta: ele não se
        // multiplica por pessoa nem entra na conta do desconto.
        var t = Torneio();
        t.TaxaPorImpedimento = 15m;

        Assert.Equal(160m + 30m, PrecoDaInscricao.Total(t, new[] { false, true }, impedimentos: 2));
    }

    [Fact]
    public void Somatorio_le_o_valor_GRAVADO_na_inscricao()
    {
        // ⚠️ É isto que impede relatório e devolução de mentirem: com dois preços possíveis,
        // recalcular por PrecoInscricao não tem como saber quem entrou pela segunda vez.
        var t = Torneio();
        var dupla = new Dupla { Jogador1Id = 1, Jogador2Id = 2, ValorInscricao = 160m };

        Assert.Equal(160m, PrecoDaInscricao.DaDupla(t, dupla));
    }

    [Fact]
    public void Inscricao_ANTIGA_sem_valor_gravado_cai_no_calculo_de_sempre()
    {
        // Linha anterior à coluna: naquele tempo havia um preço só, e a conta batia.
        var t = Torneio(segunda: null);

        Assert.Equal(200m, PrecoDaInscricao.DaDupla(t, new Dupla { Jogador1Id = 1, Jogador2Id = 2 }));
        Assert.Equal(100m, PrecoDaInscricao.DaDupla(t, new Dupla { Jogador1Id = 1 }));
        Assert.Equal(100m, PrecoDaInscricao.DaInscricaoAmericana(t, new InscricaoAmericana()));
    }

    [Fact]
    public void Preco_negativo_e_recusado()
    {
        Assert.NotNull(PrecoDaInscricao.ProblemaCom(-1m));
        Assert.Null(PrecoDaInscricao.ProblemaCom(0m));
        Assert.Null(PrecoDaInscricao.ProblemaCom(null));
    }
}
