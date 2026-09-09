using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 🗣️ Felipe, 09/09/2026, olhando o torneio do Er: "no caso do ER, já está pago, a gente já
// negociou" — e logo depois: "aonde eu coloco q foi cortesia?".
//
// 🕳️ NÃO TINHA ONDE. O formulário de "Registrar negociação e liberar" (o único lugar que marca
// cortesia) vivia atrás de `ehAdmin && !liberadas` — e isso estava CERTO enquanto só havia dois
// jeitos de estar liberado: pago ou negociado. O FIADO de 08/09/2026 abriu um terceiro
// (`TaxaExternoAdiadaEm`), e `ChavesLiberadas` passou a responder `true` pra ele também.
//
// Resultado: o organizador que aperta "Sortear agora, pagar depois" some com o próprio caminho
// da cortesia. O torneio fica DEVENDO pra sempre no /Admin/Financeiro, e a única saída é pagar
// — mesmo quando o Padelizou já abriu mão. É o oposto do que o fiado queria.
//
// ⚠️ A PERGUNTA CERTA NÃO É "a chave está liberada?", É "a taxa ainda está em aberto?". As duas
// coincidiam antes do fiado; agora não coincidem mais, e é essa a distinção.
public class CortesiaDepoisDoFiadoTests
{
    private static Torneio Externo() => new()
    {
        Nome = "T", Codigo = "T1",
        FormaPagamento = "Externo",
        PrecoInscricao = 150m,
    };

    [Fact]
    public void Taxa_em_aberto_aceita_registrar_a_cortesia()
    {
        Assert.True(TaxaDoTorneioExterno.PodeRegistrarNegociacao(Externo()));
    }

    // ⚠️ O CASO DO ER, e o que este arquivo existe pra consertar: fiado ≠ resolvido.
    [Fact]
    public void Fiado_continua_aceitando_a_cortesia()
    {
        var torneio = Externo();
        torneio.TaxaExternoAdiadaEm = new DateTime(2026, 9, 9, 8, 9, 0);

        Assert.True(TaxaDoTorneioExterno.ChavesLiberadas(torneio));      // a chave saiu, sim
        Assert.True(TaxaDoTorneioExterno.EstaDevendo(torneio));          // mas ele DEVE
        Assert.True(TaxaDoTorneioExterno.PodeRegistrarNegociacao(torneio));
    }

    [Fact]
    public void Ja_pago_nao_tem_cortesia_a_registrar()
    {
        var torneio = Externo();
        torneio.TaxaExternoPagaEm = DateTime.Now;

        Assert.False(TaxaDoTorneioExterno.PodeRegistrarNegociacao(torneio));
    }

    [Fact]
    public void Ja_negociado_nao_registra_de_novo()
    {
        var torneio = Externo();
        torneio.TaxaExternoNegociadaEm = DateTime.Now;

        Assert.False(TaxaDoTorneioExterno.PodeRegistrarNegociacao(torneio));
    }

    // Torneio que não paga taxa nenhuma não tem o que negociar — não há dívida pra perdoar.
    [Fact]
    public void Torneio_sem_taxa_nao_tem_cortesia()
    {
        var deGraca = Externo();
        deGraca.PrecoInscricao = 0m;

        Assert.False(TaxaDoTorneioExterno.PodeRegistrarNegociacao(deGraca));
    }

    // ⚠️ E A CORTESIA ENCERRA A DÍVIDA, inclusive a que veio do fiado — senão o torneio
    // continuaria na lista de cobrança depois de o Padelizou ter abrido mão.
    [Fact]
    public void Registrada_a_cortesia_o_torneio_deixa_de_dever()
    {
        var torneio = Externo();
        torneio.TaxaExternoAdiadaEm = new DateTime(2026, 9, 9, 8, 9, 0);
        torneio.TaxaExternoNegociadaEm = DateTime.Now;

        Assert.False(TaxaDoTorneioExterno.EstaDevendo(torneio));
        Assert.False(TaxaDoTorneioExterno.PodeRegistrarNegociacao(torneio));
    }
}
