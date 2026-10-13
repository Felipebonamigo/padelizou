using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O ORGANIZADOR MARCOU COMO PAGO: A COBRANÇA DO ASAAS É CANCELADA.
//
// 🗣️ Felipe, 13/10/2026: *"quando o sistema marcar como pago (na mão do organizador) é pq ele
// recebeu no pix dele ou de alguma forma dele, ai marque como pago no sistema e cancele a
// cobrança do asaas"*.
//
// O caso que trouxe isto: o Lucas Fonseca (NATA PADEL TOUR) estava pago no sistema e seguia
// recebendo cobrança — a fatura de "pagar depois" continuava viva no Asaas, que lembra sozinho
// de toda cobrança aberta. E aberta, ela ainda podia ser paga de novo.
//
// ⚠️ "Cancelado" no nosso banco NÃO quer dizer cancelado lá: o webhook PAYMENT_OVERDUE grava
// "Cancelado" aqui e a cobrança segue VENCIDA (e cobrando) no Asaas. Por isso "Cancelado" com
// AsaasPaymentId entra na lista do que apagar.
public class MarcarPagoCancelaCobrancaDoAsaasTests
{
    private static (DbPadelContext ctx, PagamentoInscricaoService servico, IAsaasService asaas, Torneio torneio, Dupla dupla)
        Cenario()
    {
        var ctx = TestInfra.NovoContexto();
        var torneio = new Torneio { Nome = "NATA PADEL TOUR", Codigo = "NATA", Status = "Inscrições Abertas" };
        ctx.Torneios.Add(torneio);
        var categoria = new Categoria { Nome = "5ª Masc", Codigo = "C5M", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();
        var dupla = new Dupla { CategoriaId = categoria.Id, Jogador1Id = 1, Jogador2Id = 2, Pago = true };
        ctx.Duplas.Add(dupla);
        ctx.SaveChanges();

        var asaas = Substitute.For<IAsaasService>();
        asaas.Configurado.Returns(true);
        asaas.EstornarAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<DevolucaoParcial?>()).Returns(true);

        var servico = new PagamentoInscricaoService(
            ctx, asaas, Options.Create(new AsaasSettings()),
            NullLogger<PagamentoInscricaoService>.Instance,
            Substitute.For<IPushNotificationService>(),
            Options.Create(new TaxasExibicao()), Options.Create(new PlanoProfessorSettings()));

        return (ctx, servico, asaas, torneio, dupla);
    }

    private static Pagamento Cobranca(int torneioId, string asaasId, string status,
        int? duplaId = null, int? americanaId = null, string tipo = "TorneioPagarDepois") => new()
    {
        Tipo = tipo, TorneioId = torneioId, JogadorId = 1, Valor = 125m, Status = status,
        AsaasPaymentId = asaasId,
        DadosInscricao = JsonSerializer.Serialize(new DadosPagamentoDeInscricao(torneioId, duplaId, americanaId)),
    };

    [Fact]
    public async Task Apaga_no_Asaas_a_cobranca_aberta_da_inscricao_e_marca_cancelada()
    {
        var (ctx, servico, asaas, torneio, dupla) = Cenario();
        using var _ = ctx;
        ctx.Pagamentos.Add(Cobranca(torneio.Id, "pay_aberta", "Pendente", dupla.Id));
        await ctx.SaveChangesAsync();

        var canceladas = await servico.CancelarCobrancasAbertasDaInscricaoAsync(torneio.Id, dupla.Id, null);

        Assert.Equal(1, canceladas);
        // `jaFoiPaga: false` = DELETE da fatura, não pedido de devolução.
        await asaas.Received(1).EstornarAsync("pay_aberta", false, null);
        Assert.Equal("Cancelado", (await ctx.Pagamentos.SingleAsync()).Status);
    }

    // As que o NOSSO banco já dá por mortas, mas o Asaas não: "Expirado" (o varredor local) e
    // "Cancelado" (o PAYMENT_OVERDUE). É exatamente a do Lucas — vencida e cobrando.
    [Theory]
    [InlineData("Expirado")]
    [InlineData("Cancelado")]
    public async Task Tambem_apaga_a_vencida_que_so_morreu_do_nosso_lado(string status)
    {
        var (ctx, servico, asaas, torneio, dupla) = Cenario();
        using var _ = ctx;
        ctx.Pagamentos.Add(Cobranca(torneio.Id, "pay_vencida", status, dupla.Id));
        await ctx.SaveChangesAsync();

        await servico.CancelarCobrancasAbertasDaInscricaoAsync(torneio.Id, dupla.Id, null);

        await asaas.Received(1).EstornarAsync("pay_vencida", false, null);
    }

    // O que NÃO pode ser tocado: dinheiro que entrou, cobrança de outra inscrição, de outro
    // torneio, Pix direto (nunca passou pelo Asaas) e a cobrança que CRIA a inscrição
    // ("TorneioDupla" — o estorno dela apaga a dupla, ver JuntarInscricoes).
    [Fact]
    public async Task Nao_toca_no_que_nao_e_cobranca_aberta_desta_inscricao()
    {
        var (ctx, servico, asaas, torneio, dupla) = Cenario();
        using var _ = ctx;
        ctx.Pagamentos.AddRange(
            Cobranca(torneio.Id, "pay_paga", "Confirmado", dupla.Id),
            Cobranca(torneio.Id, "pay_estornada", "Estornado", dupla.Id),
            Cobranca(torneio.Id, "pay_outra_dupla", "Pendente", dupla.Id + 100),
            Cobranca(torneio.Id + 100, "pay_outro_torneio", "Pendente", dupla.Id),
            Cobranca(torneio.Id, "pay_dupla_nova", "Pendente", dupla.Id, tipo: "TorneioDupla"));
        var pixDireto = Cobranca(torneio.Id, "x", "Pendente", dupla.Id);
        pixDireto.AsaasPaymentId = null;
        ctx.Pagamentos.Add(pixDireto);
        await ctx.SaveChangesAsync();

        var canceladas = await servico.CancelarCobrancasAbertasDaInscricaoAsync(torneio.Id, dupla.Id, null);

        Assert.Equal(0, canceladas);
        await asaas.DidNotReceive().EstornarAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<DevolucaoParcial?>());
    }

    // A cobrança repontada por uma junção de inscrições guarda o alvo no ReferenciaId também.
    [Fact]
    public async Task Acha_a_cobranca_pelo_ReferenciaId()
    {
        var (ctx, servico, asaas, torneio, dupla) = Cenario();
        using var _ = ctx;
        var cobranca = Cobranca(torneio.Id, "pay_ref", "Pendente");
        cobranca.DadosInscricao = null;
        cobranca.ReferenciaId = dupla.Id;
        ctx.Pagamentos.Add(cobranca);
        await ctx.SaveChangesAsync();

        await servico.CancelarCobrancasAbertasDaInscricaoAsync(torneio.Id, dupla.Id, null);

        await asaas.Received(1).EstornarAsync("pay_ref", false, null);
    }

    [Fact]
    public async Task Vale_pra_inscricao_do_americano()
    {
        var (ctx, servico, asaas, torneio, _d) = Cenario();
        using var _ = ctx;
        ctx.Pagamentos.AddRange(
            Cobranca(torneio.Id, "pay_americano", "Pendente", americanaId: 7),
            Cobranca(torneio.Id, "pay_outro_americano", "Pendente", americanaId: 8));
        await ctx.SaveChangesAsync();

        await servico.CancelarCobrancasAbertasDaInscricaoAsync(torneio.Id, null, 7);

        await asaas.Received(1).EstornarAsync("pay_americano", false, null);
        await asaas.DidNotReceive().EstornarAsync("pay_outro_americano", Arg.Any<bool>(), Arg.Any<DevolucaoParcial?>());
    }

    // O gateway recusou (ex.: a pessoa pagou naquele instante): o status fica como estava —
    // gravar "Cancelado" mentiria sobre uma cobrança que pode ter virado dinheiro.
    [Fact]
    public async Task Se_o_Asaas_recusa_o_status_nao_muda()
    {
        var (ctx, servico, asaas, torneio, dupla) = Cenario();
        using var _ = ctx;
        asaas.EstornarAsync("pay_recusada", false, null).Returns(false);
        ctx.Pagamentos.Add(Cobranca(torneio.Id, "pay_recusada", "Pendente", dupla.Id));
        await ctx.SaveChangesAsync();

        var canceladas = await servico.CancelarCobrancasAbertasDaInscricaoAsync(torneio.Id, dupla.Id, null);

        Assert.Equal(0, canceladas);
        Assert.Equal("Pendente", (await ctx.Pagamentos.SingleAsync()).Status);
    }

    // ═══════════════════ O BOTÃO DO ORGANIZADOR ═══════════════════

    [Fact]
    public async Task Marcar_a_dupla_como_paga_cancela_a_cobranca_dela_no_Asaas()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneioId, duplaId, organizadorId) = TorneioComDupla(ctx);
        var pagamentos = Substitute.For<IPagamentoInscricaoService>();

        await TestInfra.NovoTorneiosController(ctx, organizadorId, pagamentos).AlternarPagamentoDupla(duplaId);

        Assert.True((await ctx.Duplas.FindAsync(duplaId))!.Pago);
        await pagamentos.Received(1).CancelarCobrancasAbertasDaInscricaoAsync(torneioId, duplaId, null);
    }

    // Desmarcar não cancela nada: aí a pessoa precisa justamente de um jeito de pagar.
    [Fact]
    public async Task Desmarcar_o_pago_nao_cancela_cobranca_nenhuma()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_t, duplaId, organizadorId) = TorneioComDupla(ctx);
        var dupla = (await ctx.Duplas.FindAsync(duplaId))!;
        dupla.Pago = true;
        await ctx.SaveChangesAsync();
        var pagamentos = Substitute.For<IPagamentoInscricaoService>();

        await TestInfra.NovoTorneiosController(ctx, organizadorId, pagamentos).AlternarPagamentoDupla(duplaId);

        Assert.False((await ctx.Duplas.FindAsync(duplaId))!.Pago);
        await pagamentos.DidNotReceive().CancelarCobrancasAbertasDaInscricaoAsync(
            Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<int?>());
    }

    [Fact]
    public async Task Marcar_a_inscricao_do_americano_como_paga_cancela_a_cobranca_dela()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneioId, _d, organizadorId) = TorneioComDupla(ctx);
        var categoriaId = ctx.Categorias.Single(c => c.TorneioId == torneioId).Id;
        var inscricao = new InscricaoAmericana { CategoriaId = categoriaId, JogadorId = 2 };
        ctx.InscricoesAmericanas.Add(inscricao);
        await ctx.SaveChangesAsync();
        var pagamentos = Substitute.For<IPagamentoInscricaoService>();

        await TestInfra.NovoTorneiosController(ctx, organizadorId, pagamentos).AlternarPagamentoAmericano(inscricao.Id);

        await pagamentos.Received(1).CancelarCobrancasAbertasDaInscricaoAsync(torneioId, null, inscricao.Id);
    }

    private static (int torneioId, int duplaId, int organizadorId) TorneioComDupla(DbPadelContext ctx)
    {
        var organizador = new Jogador { Nome = "Lucas Almeida", Cpf = "11144477735" };
        ctx.Jogadores.Add(organizador);
        ctx.SaveChanges();
        var torneio = new Torneio
        {
            Nome = "NATA PADEL TOUR", Codigo = "NATA", Status = "Inscrições Abertas",
        };
        ctx.Torneios.Add(torneio);
        ctx.SaveChanges();
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });
        var categoria = new Categoria { Nome = "5ª Masc", Codigo = "C5M", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();
        var dupla = new Dupla { CategoriaId = categoria.Id, Jogador1Id = 50, Jogador2Id = 51 };
        ctx.Duplas.Add(dupla);
        ctx.SaveChanges();
        return (torneio.Id, dupla.Id, organizador.Id);
    }
}
