using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// DUPLA SEM PARCEIRO PAGA POR UMA PESSOA (Felipe, 08/08/2026).
//
// Antes o teto virava piso: inscrição de dupla cobrava 2 mesmo com o segundo nome em branco,
// então quem entrava "procurando parceiro" pagava por DOIS — sozinho, e sem saber quem seria
// o outro.
//
// ⚠️ E as duas contas do sistema já discordavam entre si: TaxaDoTorneioExterno.PessoasInscritas
// conta dupla sem parceiro como 1 ("cobrar por alguém que ainda não foi definido seria cobrar
// por fantasma"), enquanto a cobrança do site pedia 2 pela mesma inscrição.
public class DuplaSemParceiroPagaUmTests
{
    private static Torneio Torneio(decimal preco = 120m, decimal? segunda = null) => new()
    {
        Nome = "NATA PADEL TOUR", Codigo = "NATA1",
        PrecoInscricao = preco,
        PrecoSegundaInscricao = segunda,
        PermiteMultiplasCategorias = segunda != null,
    };

    [Fact]
    public void Sozinho_paga_uma_pessoa()
    {
        // Uma pessoa na conta = uma inscrição.
        Assert.Equal(120m, PrecoDaInscricao.Total(Torneio(), new[] { false }));
    }

    [Fact]
    public void Dupla_fechada_paga_duas()
    {
        Assert.Equal(240m, PrecoDaInscricao.Total(Torneio(), new[] { false, false }));
    }

    [Fact]
    public void O_valor_gravado_de_uma_dupla_sem_parceiro_e_de_uma_pessoa()
    {
        // A leitura de quem já existe (relatório, devolução, base da taxa) tem que enxergar o
        // mesmo número: a linha antiga, sem ValorInscricao, cai no cálculo por preço — e ele
        // já contava 1 pra dupla sem parceiro.
        var torneio = Torneio();
        var sozinha = new Dupla { Jogador1Id = 1, Jogador2Id = null };

        Assert.Equal(120m, PrecoDaInscricao.DaDupla(torneio, sozinha));
    }

    [Fact]
    public void Quando_o_parceiro_entra_o_valor_passa_a_contar_dois()
    {
        // É a conta que o TrocarParceiro/Convite refaz: o que estava gravado (uma pessoa) mais
        // a parte do parceiro que acabou de entrar.
        var torneio = Torneio();
        decimal gravadoQuandoEstavaSozinha = PrecoDaInscricao.Total(torneio, new[] { false });

        decimal depoisDoParceiro = gravadoQuandoEstavaSozinha
            + PrecoDaInscricao.PorPessoa(torneio, jaEstaNoTorneio: false);

        Assert.Equal(240m, depoisDoParceiro);
    }

    [Fact]
    public void O_parceiro_que_repete_no_torneio_entra_pelo_preco_de_segunda()
    {
        var torneio = Torneio(preco: 120m, segunda: 80m);
        decimal sozinha = PrecoDaInscricao.Total(torneio, new[] { false });

        // 120 (o primeiro, novo no torneio) + 80 (o parceiro, que já joga outra categoria)
        Assert.Equal(200m, sozinha + PrecoDaInscricao.PorPessoa(torneio, jaEstaNoTorneio: true));
    }

    [Fact]
    public void A_base_da_taxa_do_externo_sempre_contou_um_e_agora_a_cobranca_concorda()
    {
        // Este teste existe pra amarrar as DUAS contas juntas: era a discordância entre elas
        // que fazia o mesmo torneio cobrar 2 e taxar 1 pela mesma inscrição.
        var sozinha = new Dupla { Jogador1Id = 1, Jogador2Id = null };

        Assert.Equal(1, TaxaDoTorneioExterno.PessoasInscritas(new[] { sozinha }, Array.Empty<InscricaoAmericana>()));
        Assert.Equal(120m, PrecoDaInscricao.Total(Torneio(), new[] { false }));
    }
}
