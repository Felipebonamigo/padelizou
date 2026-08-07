using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A condição do torneio "por fora" (5%): chaves só saem com a taxa paga ou negociada.
// É a única cobrança do sistema que não tem webhook de inscrição pra se defender — a trava
// no sorteio é o mecanismo inteiro, então cada borda dela precisa estar escrita aqui.
public class TaxaDoTorneioExternoTests
{
    private static Torneio Externo(decimal preco = 100m) => new()
    {
        Nome = "Copa Teste",
        Codigo = "TST",
        FormaPagamento = "Externo",
        PrecoInscricao = preco,
    };

    [Fact]
    public void So_se_aplica_ao_externo_pago()
    {
        Assert.True(TaxaDoTorneioExterno.SeAplica(Externo()));

        // Gratuito não deve nada; formas online cobram a taxa dentro de cada inscrição.
        Assert.False(TaxaDoTorneioExterno.SeAplica(Externo(preco: 0m)));
        Assert.False(TaxaDoTorneioExterno.SeAplica(new Torneio
        {
            Nome = "x", Codigo = "x", FormaPagamento = "OnlinePix", PrecoInscricao = 100m
        }));
    }

    [Fact]
    public void Americano_sem_ranking_por_fora_nao_trava_e_nao_deve_taxa()
    {
        // O Americano livre (Felipe, 07/08/2026): sem comprar o Ranking Americano, o "por
        // fora" não deve os 5% — e por isso as chaves nunca ficam presas nele.
        var torneio = Externo();
        torneio.Formato = "Americano";
        torneio.PontuaNoRankingAmericano = false;

        Assert.False(TaxaDoTorneioExterno.SeAplica(torneio));
        Assert.True(TaxaDoTorneioExterno.ChavesLiberadas(torneio));
    }

    [Fact]
    public void Americano_que_compra_ranking_continua_devendo_a_taxa_do_externo()
    {
        var torneio = Externo();
        torneio.Formato = "Americano";
        torneio.PontuaNoRankingAmericano = true;

        Assert.True(TaxaDoTorneioExterno.SeAplica(torneio));
        Assert.False(TaxaDoTorneioExterno.ChavesLiberadas(torneio));
    }

    [Fact]
    public void Chaves_travadas_ate_pagar_ou_negociar()
    {
        var torneio = Externo();
        Assert.False(TaxaDoTorneioExterno.ChavesLiberadas(torneio));

        torneio.TaxaExternoPagaEm = DateTime.Now;
        Assert.True(TaxaDoTorneioExterno.ChavesLiberadas(torneio));

        var negociado = Externo();
        negociado.TaxaExternoNegociadaEm = DateTime.Now;
        Assert.True(TaxaDoTorneioExterno.ChavesLiberadas(negociado));
    }

    [Fact]
    public void Torneio_online_nunca_e_travado_por_esta_regra()
    {
        var online = new Torneio
        {
            Nome = "x", Codigo = "x", FormaPagamento = "OnlineTodas", PrecoInscricao = 100m
        };
        Assert.True(TaxaDoTorneioExterno.ChavesLiberadas(online));
    }

    [Fact]
    public void Conta_gente_de_verdade_dupla_2_sem_parceiro_1_espera_0()
    {
        var duplas = new[]
        {
            new Dupla { Jogador1Id = 1, Jogador2Id = 2 },                          // completa: 2
            new Dupla { Jogador1Id = 3, Jogador2Id = null },                       // sem parceiro: 1
            new Dupla { Jogador1Id = 4, Jogador2Id = 5, EmListaDeEspera = true },  // espera: 0
        };
        var americanas = new[]
        {
            new InscricaoAmericana { JogadorId = 6 },                              // 1
            new InscricaoAmericana { JogadorId = 7, EmListaDeEspera = true },      // 0
        };

        Assert.Equal(4, TaxaDoTorneioExterno.PessoasInscritas(duplas, americanas));
    }

    [Fact]
    public void Time_nao_entra_na_base_da_taxa()
    {
        // Time é cadastrado pelo organizador, não paga inscrição pelo sistema — cobrá-lo
        // como "1 pessoa" seria taxar fantasma. Mesma cortesia dos impedimentos.
        var duplas = new[]
        {
            new Dupla { Jogador1Id = 1, Jogador2Id = 2 },                    // completa: 2
            new Dupla { Jogador1Id = 9, NomeTime = "Nata Padel" },           // time: 0
            new Dupla { Jogador1Id = 9, NomeTime = "Quanto Tá" },            // time: 0
        };

        Assert.Equal(2, TaxaDoTorneioExterno.PessoasInscritas(duplas, Array.Empty<InscricaoAmericana>()));
    }

    [Fact]
    public void O_valor_e_pessoas_vezes_preco_vezes_percentual()
    {
        // 24 pessoas × R$ 100 × 5% = R$ 120.
        Assert.Equal(120m, TaxaDoTorneioExterno.Valor(24, 100m, 5m));

        // Centavos arredondados: 7 × R$ 85 × 5% = R$ 29,75.
        Assert.Equal(29.75m, TaxaDoTorneioExterno.Valor(7, 85m, 5m));

        Assert.Equal(0m, TaxaDoTorneioExterno.Valor(0, 100m, 5m));
    }
}
