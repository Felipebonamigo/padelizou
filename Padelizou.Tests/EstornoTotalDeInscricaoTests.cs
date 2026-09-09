using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// ESTORNO TOTAL, REIVOCÁVEL DE FORA — pedido do Felipe (09/09/2026): na hora de sortear as
// chaves, o organizador pode cancelar a inscrição de quem ficou sem parceiro e estornar o
// valor pago, ali mesmo, sem esperar o webhook do Asaas confirmar antes de tirar a dupla da
// tela (ver TorneiosController.CancelarSemParceiro).
//
// Até aqui esse "chamar o gateway e marcar o status" só existia dentro de
// PagamentosController.Estornar, amarrado à tela de Pagamentos → Meus. Este método extrai
// SÓ o caminho de estorno TOTAL (não o parcial, que mexe em ValorEstornado e mantém a
// inscrição de pé) pra dar aos dois lugares uma base comum, sem duplicar a chamada ao gateway.
public class EstornoTotalDeInscricaoTests
{
    private static (PagamentoInscricaoService servico, IAsaasService asaas) Novo(DbPadelContext ctx)
    {
        var asaas = Substitute.For<IAsaasService>();

        var servico = new PagamentoInscricaoService(
            ctx, asaas, Options.Create(new AsaasSettings()),
            NullLogger<PagamentoInscricaoService>.Instance,
            Substitute.For<IPushNotificationService>(),
            Options.Create(new TaxasExibicao()),
            Options.Create(new PlanoProfessorSettings()));

        return (servico, asaas);
    }

    private static Pagamento Cobranca(string status, string? asaasPaymentId = "pay_123") => new()
    {
        Id = 1,
        Tipo = "TorneioDupla",
        Status = status,
        Valor = 60m,
        ValorRepasse = 54m,
        Comissao = 6m,
        AsaasPaymentId = asaasPaymentId,
        JogadorId = 1,
        RecebedorId = 2,
    };

    [Fact]
    public async Task Cobranca_confirmada_pede_devolucao_ao_gateway_e_vira_Estornado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (servico, asaas) = Novo(ctx);
        asaas.EstornarAsync("pay_123", true, null).Returns(true);
        var pagamento = Cobranca("Confirmado");
        ctx.Pagamentos.Add(pagamento);
        await ctx.SaveChangesAsync();

        var ok = await servico.EstornarTotalAsync(pagamento);

        Assert.True(ok);
        Assert.Equal("Estornado", pagamento.Status);
        await asaas.Received(1).EstornarAsync("pay_123", true, null);
    }

    [Fact]
    public async Task Cobranca_pendente_so_cancela_a_fatura_e_vira_Cancelado()
    {
        // Nada foi pago ainda: "estornar" aqui é matar o link, não pedir dinheiro de volta.
        using var ctx = TestInfra.NovoContexto();
        var (servico, asaas) = Novo(ctx);
        asaas.EstornarAsync("pay_123", false, null).Returns(true);
        var pagamento = Cobranca("Pendente");
        ctx.Pagamentos.Add(pagamento);
        await ctx.SaveChangesAsync();

        var ok = await servico.EstornarTotalAsync(pagamento);

        Assert.True(ok);
        Assert.Equal("Cancelado", pagamento.Status);
        await asaas.Received(1).EstornarAsync("pay_123", false, null);
    }

    [Fact]
    public async Task Gateway_recusando_nao_muda_o_status()
    {
        using var ctx = TestInfra.NovoContexto();
        var (servico, asaas) = Novo(ctx);
        asaas.EstornarAsync("pay_123", true, null).Returns(false);
        var pagamento = Cobranca("Confirmado");
        ctx.Pagamentos.Add(pagamento);
        await ctx.SaveChangesAsync();

        var ok = await servico.EstornarTotalAsync(pagamento);

        Assert.False(ok);
        Assert.Equal("Confirmado", pagamento.Status);
    }

    [Fact]
    public async Task Sem_id_no_gateway_recusa_sem_chamar_o_Asaas()
    {
        // Cobrança antiga, sem vínculo com o meio de pagamento — mesmo caso do
        // "Cobrança sem identificação no gateway" em PagamentosController.Estornar.
        using var ctx = TestInfra.NovoContexto();
        var (servico, asaas) = Novo(ctx);
        var pagamento = Cobranca("Confirmado", asaasPaymentId: null);
        ctx.Pagamentos.Add(pagamento);
        await ctx.SaveChangesAsync();

        var ok = await servico.EstornarTotalAsync(pagamento);

        Assert.False(ok);
        await asaas.DidNotReceiveWithAnyArgs().EstornarAsync(default!, default, default);
    }

    [Theory]
    [InlineData("Estornado")]
    [InlineData("Cancelado")]
    public async Task Cobranca_que_ja_nao_pode_ser_estornada_e_recusada(string status)
    {
        using var ctx = TestInfra.NovoContexto();
        var (servico, asaas) = Novo(ctx);
        var pagamento = Cobranca(status);
        ctx.Pagamentos.Add(pagamento);
        await ctx.SaveChangesAsync();

        var ok = await servico.EstornarTotalAsync(pagamento);

        Assert.False(ok);
        await asaas.DidNotReceiveWithAnyArgs().EstornarAsync(default!, default, default);
    }
}
