using Padelizou.Services;

namespace Padelizou.Tests;

// "Chaves em Sorteio" mentia na tela (08/08/2026).
//
// O valor gravado descreve o PRÓXIMO PASSO (sortear), não o estado atual: nada está sendo
// sorteado, e o torneio pode ficar semanas assim. No NATA PADEL TOUR ele aparecia como
// "CHAVES EM SORTEIO" logo acima da faixa dizendo "as inscrições estão fechadas" — duas
// frases sobre o mesmo estado, e a de cima era a errada.
public class StatusDoTorneioNaTelaTests
{
    [Fact]
    public void Chaves_em_sorteio_se_le_inscricoes_fechadas()
    {
        Assert.Equal("Inscrições Fechadas", StatusDoTorneioNaTela.Nome(PortaDaInscricao.Fechada));
    }

    [Theory]
    [InlineData("Inscrições Abertas")]
    [InlineData("Fase de Grupos")]
    [InlineData("Mata-Mata")]
    [InlineData("Finalizado")]
    [InlineData("Cancelado")]
    public void Os_outros_status_saem_como_estao(string status)
    {
        Assert.Equal(status, StatusDoTorneioNaTela.Nome(status));
    }

    [Fact]
    public void O_valor_GRAVADO_nao_muda()
    {
        // ⚠️ A tradução é só de exibição. Se alguém um dia trocar a constante achando que o
        // banco acompanha, a vitrine, a trava de inscrição e o chaveamento — que comparam a
        // string crua em ~25 lugares — param de reconhecer o torneio, calados.
        Assert.Equal("Chaves em Sorteio", PortaDaInscricao.Fechada);
    }

    [Fact]
    public void Status_nulo_nao_estoura()
    {
        Assert.Equal("", StatusDoTorneioNaTela.Nome(null));
    }
}
