using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// Achado da análise de sistema de 21/08/2026: PagamentoInscricaoService.EfetivarMarcacaoAsync
// (e a versão de vários horários) grava Status="AguardandoEstorno" quando o webhook confirma o
// pagamento mas a quadra JÁ tinha sido tomada por outra marcação enquanto a cobrança estava
// pendente — dinheiro entrou, o serviço não pôde ser entregue. Até então nenhum código lia esse
// status: PagamentosController.Estornar só aceitava "Confirmado" ou "Pendente" e recusava com
// "Esta cobrança não pode ser estornada" — beco sem saída, dinheiro preso sem ninguém conseguir
// devolvê-lo pela tela.
public class AguardandoEstornoTests
{
    private static Pagamento CobrancaPresa() => new()
    {
        Id = 1,
        Tipo = "Jogo",
        Status = "AguardandoEstorno",
        Valor = 80m,
        ValorRepasse = 72m,
        Comissao = 8m,
        AsaasPaymentId = "pay_preso",
        JogadorId = 1,
        RecebedorId = 2,
    };

    private static (PagamentosController c, DbPadelContext ctx, IAsaasService asaas) Montar(int logadoComoId)
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Id = 1, Nome = "Quem pagou", Cpf = "99900000001" });
        ctx.Jogadores.Add(new Jogador { Id = 2, Nome = "Dono da quadra", Cpf = "99900000002" });
        ctx.Pagamentos.Add(CobrancaPresa());
        ctx.SaveChanges();

        var asaas = Substitute.For<IAsaasService>();
        asaas.EstornarAsync(default!, default, default).ReturnsForAnyArgs(true);

        var controller = new PagamentosController(
            ctx,
            Options.Create(new AsaasSettings { WebhookToken = "token-de-teste" }),
            Substitute.For<IPagamentoInscricaoService>(), asaas, NullLogger<PagamentosController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, logadoComoId.ToString()) }, "Teste")),
                },
            },
        };
        controller.TempData = new TempDataDictionary(
            controller.HttpContext, Substitute.For<ITempDataProvider>());

        return (controller, ctx, asaas);
    }

    [Fact]
    public async Task Dono_da_quadra_consegue_devolver_o_dinheiro_preso()
    {
        var (c, ctx, asaas) = Montar(logadoComoId: 2);

        var resultado = await c.Estornar(1, null);

        Assert.IsNotType<ForbidResult>(resultado);
        Assert.Equal("Estornado", ctx.Pagamentos.Single().Status);
        await asaas.Received().EstornarAsync("pay_preso", true, null);
    }

    [Fact]
    public async Task Nao_aceita_devolucao_parcial_mesmo_se_um_valor_for_enviado()
    {
        // O serviço não foi entregue NADA — não existe "parte" que fique de pé. Mesmo que
        // alguém envie um valor pela URL (POST montado à mão), o caminho é sempre total.
        var (c, ctx, asaas) = Montar(logadoComoId: 2);

        await c.Estornar(1, 30m);

        var p = ctx.Pagamentos.Single();
        Assert.Equal("Estornado", p.Status);
        Assert.Equal(80m, p.Valor);   // não foi reduzido por um estorno parcial
        await asaas.Received().EstornarAsync("pay_preso", true, null);
    }

    [Fact]
    public async Task Estranho_nao_devolve_dinheiro_preso_de_outra_quadra()
    {
        var (c, ctx, _) = Montar(logadoComoId: 99);

        var resultado = await c.Estornar(1, null);

        Assert.IsType<ForbidResult>(resultado);
        Assert.Equal("AguardandoEstorno", ctx.Pagamentos.Single().Status);
    }
}
