using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// CANCELAR QUEM FICOU SEM PARCEIRO, NA HORA DE SORTEAR — pedido do Felipe (09/09/2026): hoje
// quem fica sem parceiro só é filtrado do sorteio em silêncio (ForaDoSorteio); o alerta acima
// do botão já avisa quem fica de fora, mas não dá decisão nenhuma pro organizador. "Entrar
// igual" não é possível de verdade (dupla sem o segundo nome não é time, não joga mata-mata),
// então a decisão vira: não fazer nada (sortear sem essa pessoa, o padrão de sempre) ou
// cancelar a inscrição dela e estornar o que ela pagou — esta ação.
public class CancelarSemParceiroTests
{
    private static (Torneio torneio, Categoria categoria, Jogador organizador, Jogador solo, Dupla dupla)
        MontarSolo(DbPadelContext ctx, string status = "Chaves em Sorteio", bool pago = false)
    {
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0, status: status);

        var solo = new Jogador { Nome = "Paulo Prass", Cpf = "11144477735" };
        ctx.Jogadores.Add(solo);
        ctx.SaveChanges();

        var dupla = new Dupla
        {
            CategoriaId = categoria.Id, Jogador1Id = solo.Id, Jogador2Id = null,
            Pago = pago, PagoEm = pago ? DateTime.Now : null,
        };
        ctx.Duplas.Add(dupla);
        ctx.SaveChanges();

        return (torneio, categoria, organizador, solo, dupla);
    }

    private static Pagamento CobrancaConfirmada(int duplaId, int organizadorId, int jogadorId,
        string? asaasPaymentId = "pay_paulo") => new()
    {
        Tipo = "TorneioDupla", ReferenciaId = duplaId, Status = "Confirmado",
        Valor = 60m, ValorRepasse = 54m, Comissao = 6m,
        AsaasPaymentId = asaasPaymentId, JogadorId = jogadorId, RecebedorId = organizadorId,
    };

    [Fact]
    public async Task Organizador_cancela_inscricao_sem_parceiro_nao_paga()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador, _, dupla) = MontarSolo(ctx);

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id).CancelarSemParceiro(dupla.Id);

        Assert.Null(await ctx.Duplas.FindAsync(dupla.Id));
        Assert.IsType<RedirectToActionResult>(resultado);
    }

    [Fact]
    public async Task So_organizador_cancela()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, _, _, _, dupla) = MontarSolo(ctx);
        var intruso = new Jogador { Nome = "Intruso", Cpf = "99900000088" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, intruso.Id).CancelarSemParceiro(dupla.Id);

        Assert.IsType<ForbidResult>(resultado);
        Assert.NotNull(await ctx.Duplas.FindAsync(dupla.Id));
    }

    [Fact]
    public async Task Recusa_se_a_dupla_ja_tem_os_dois_parceiros()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);

        var c = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await c.CancelarSemParceiro(dupla.Id);

        Assert.NotNull(await ctx.Duplas.FindAsync(dupla.Id));
        Assert.NotNull(c.TempData["Erro"]);
    }

    [Fact]
    public async Task Recusa_fora_da_janela_do_sorteio()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, _, organizador, _, dupla) = MontarSolo(ctx, status: "Inscrições Abertas");

        var c = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await c.CancelarSemParceiro(dupla.Id);

        Assert.NotNull(await ctx.Duplas.FindAsync(dupla.Id));
        Assert.NotNull(c.TempData["Erro"]);
    }

    [Fact]
    public async Task Dupla_inexistente_da_NotFound()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, _, organizador, _, _) = MontarSolo(ctx);

        var resultado = await TestInfra.NovoTorneiosController(ctx, organizador.Id).CancelarSemParceiro(9999);

        Assert.IsType<NotFoundResult>(resultado);
    }

    [Fact]
    public async Task Dupla_paga_com_cobranca_no_gateway_estorna_e_remove()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, _, organizador, solo, dupla) = MontarSolo(ctx, pago: true);
        var pagamento = CobrancaConfirmada(dupla.Id, organizador.Id, solo.Id);
        ctx.Pagamentos.Add(pagamento);
        await ctx.SaveChangesAsync();

        var pagamentos = Substitute.For<IPagamentoInscricaoService>();
        pagamentos.EstornarTotalAsync(Arg.Any<Pagamento>()).Returns(true);

        var c = TestInfra.NovoTorneiosController(ctx, organizador.Id, pagamentos: pagamentos);
        await c.CancelarSemParceiro(dupla.Id);

        Assert.Null(await ctx.Duplas.FindAsync(dupla.Id));
        await pagamentos.Received(1).EstornarTotalAsync(
            Arg.Is<Pagamento>(p => p != null && p.Id == pagamento.Id));
        Assert.Contains("estornado", c.TempData["Sucesso"]!.ToString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Gateway_recusando_o_estorno_nao_remove_a_dupla()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, _, organizador, solo, dupla) = MontarSolo(ctx, pago: true);
        var pagamento = CobrancaConfirmada(dupla.Id, organizador.Id, solo.Id);
        ctx.Pagamentos.Add(pagamento);
        await ctx.SaveChangesAsync();

        var pagamentos = Substitute.For<IPagamentoInscricaoService>();
        pagamentos.EstornarTotalAsync(Arg.Any<Pagamento>()).Returns(false);

        var c = TestInfra.NovoTorneiosController(ctx, organizador.Id, pagamentos: pagamentos);
        await c.CancelarSemParceiro(dupla.Id);

        Assert.NotNull(await ctx.Duplas.FindAsync(dupla.Id));
        Assert.NotNull(c.TempData["Erro"]);
    }

    [Fact]
    public async Task Pago_sem_cobranca_no_gateway_remove_e_avisa_pra_acertar_por_fora()
    {
        // Marcado como pago na mão (dinheiro, Pix por fora) — não existe cobrança real pra
        // devolver pelo gateway. Mesmo caso de PagamentosController.Estornar sem AsaasPaymentId.
        using var ctx = TestInfra.NovoContexto();
        var (_, _, organizador, _, dupla) = MontarSolo(ctx, pago: true);

        var pagamentos = Substitute.For<IPagamentoInscricaoService>();

        var c = TestInfra.NovoTorneiosController(ctx, organizador.Id, pagamentos: pagamentos);
        await c.CancelarSemParceiro(dupla.Id);

        Assert.Null(await ctx.Duplas.FindAsync(dupla.Id));
        await pagamentos.DidNotReceiveWithAnyArgs().EstornarTotalAsync(default!);
        Assert.Contains("por fora", c.TempData["Sucesso"]!.ToString()!, StringComparison.OrdinalIgnoreCase);
    }
}
