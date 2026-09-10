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

    // A cobrança do torneio que "garante a vaga e cobra depois": criada na inscrição, fica
    // PENDENTE com link válido até o prazo, e só ganha `ReferenciaId` quando o dinheiro entra.
    private static Pagamento CobrancaAbertaDoPagarDepois(int torneioId, int duplaId,
        int organizadorId, int jogadorId, string? asaasPaymentId = "pay_aberta") => new()
    {
        Tipo = "TorneioPagarDepois", TorneioId = torneioId, Status = "Pendente",
        Valor = 60m, ValorRepasse = 54m, Comissao = 6m,
        AsaasPaymentId = asaasPaymentId, InvoiceUrl = "https://asaas.exemplo/i/abc",
        JogadorId = jogadorId, RecebedorId = organizadorId,
        DadosInscricao = System.Text.Json.JsonSerializer.Serialize(
            new DadosPagamentoDeInscricao(torneioId, duplaId, null)),
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

    // ── OS DOIS ACHADOS DA REVISÃO ADVERSARIAL (09/09/2026) ───────────────────────────────

    [Fact]
    public async Task Recusa_cancelar_um_TIME()
    {
        // 🕳️ `Dupla.Completa` é `Jogador2Id != null`, e TODO time tem esse campo nulo — então o
        // time passava pela guarda como se fosse inscrição sozinha. A tela nunca desenha o botão
        // pra ele, mas um POST montado à mão apagaria a linha do time e ainda mandaria "Você saiu
        // do torneio" pro Jogador1Id dela, que é o próprio organizador.
        using var ctx = TestInfra.NovoContexto();
        var (_, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        var time = new Dupla
        {
            CategoriaId = categoria.Id, Jogador1Id = organizador.Id, Jogador2Id = null,
            NomeTime = "Nata Padel",
        };
        ctx.Duplas.Add(time);
        await ctx.SaveChangesAsync();

        var c = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await c.CancelarSemParceiro(time.Id);

        Assert.NotNull(await ctx.Duplas.FindAsync(time.Id));
        Assert.NotNull(c.TempData["Erro"]);
    }

    [Fact]
    public async Task Avisa_quem_chamou_no_mural_antes_de_a_cascata_apagar_o_chamado()
    {
        // 🕳️ Apagar a dupla leva junto os ChamadosDoMural dela (FK Cascade) — e é justamente a
        // inscrição SOZINHA que acumula chamado. Sem o aviso, quem se candidatou fica esperando
        // resposta de uma vaga que não existe mais. O caminho gêmeo (FecharDuplaComAsync) já lia
        // os ids antes de apagar, pelo mesmo motivo; esta ação nasceu sem.
        using var ctx = TestInfra.NovoContexto();
        var (_, _, organizador, _, dupla) = MontarSolo(ctx);
        var candidato = new Jogador { Nome = "Candidato", Cpf = "55500000011" };
        ctx.Jogadores.Add(candidato);
        await ctx.SaveChangesAsync();
        ctx.ChamadosDoMural.Add(new ChamadoDoMural { DuplaId = dupla.Id, CandidatoId = candidato.Id });
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        await TestInfra.NovoTorneiosController(ctx, organizador.Id, push: push)
            .CancelarSemParceiro(dupla.Id);

        Assert.Null(await ctx.Duplas.FindAsync(dupla.Id));
        await push.ReceivedWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!, default);
    }

    // ── A FATURA ABERTA NÃO PODE SOBREVIVER À INSCRIÇÃO ───────────────────────────────────
    //
    // 🕳️ No torneio que garante a vaga e cobra depois, quem não pagou tem uma cobrança
    // PENDENTE viva no gateway, com link que funciona até o prazo. O bloco de estorno acima só
    // roda quando `dupla.Pago` — então cancelar uma inscrição NÃO paga deixava a fatura de pé.
    // O jogador pagava depois de já ter sido removido: o dinheiro entrava, a dupla não existia
    // mais, e EfetivarPagamentoDeInscricaoAsync caía no LogError que pede devolução à mão.
    //
    // ⚠️ A fatura pendente NÃO tem `ReferenciaId` (ele só é gravado quando o pagamento
    // confirma), então CobrancaDaDupla.AtivaDe nunca a encontraria — o vínculo mora no JSON.

    [Fact]
    public async Task Cancelar_inscricao_nao_paga_mata_a_fatura_em_aberto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador, solo, dupla) = MontarSolo(ctx);
        var fatura = CobrancaAbertaDoPagarDepois(torneio.Id, dupla.Id, organizador.Id, solo.Id);
        ctx.Pagamentos.Add(fatura);
        await ctx.SaveChangesAsync();

        var pagamentos = Substitute.For<IPagamentoInscricaoService>();
        pagamentos.EstornarTotalAsync(Arg.Any<Pagamento>()).Returns(true);

        var c = TestInfra.NovoTorneiosController(ctx, organizador.Id, pagamentos: pagamentos);
        await c.CancelarSemParceiro(dupla.Id);

        Assert.Null(await ctx.Duplas.FindAsync(dupla.Id));
        await pagamentos.Received(1).EstornarTotalAsync(
            Arg.Is<Pagamento>(p => p != null && p.Id == fatura.Id));
    }

    [Fact]
    public async Task Gateway_recusando_matar_a_fatura_nao_remove_a_dupla()
    {
        // Remover a inscrição com a fatura de pé é o pior dos dois lados: o link continua
        // valendo e ninguém mais consegue estornar por tela nenhuma.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador, solo, dupla) = MontarSolo(ctx);
        ctx.Pagamentos.Add(CobrancaAbertaDoPagarDepois(torneio.Id, dupla.Id, organizador.Id, solo.Id));
        await ctx.SaveChangesAsync();

        var pagamentos = Substitute.For<IPagamentoInscricaoService>();
        pagamentos.EstornarTotalAsync(Arg.Any<Pagamento>()).Returns(false);

        var c = TestInfra.NovoTorneiosController(ctx, organizador.Id, pagamentos: pagamentos);
        await c.CancelarSemParceiro(dupla.Id);

        Assert.NotNull(await ctx.Duplas.FindAsync(dupla.Id));
        Assert.NotNull(c.TempData["Erro"]);
    }

    [Fact]
    public async Task A_fatura_de_OUTRA_inscricao_do_mesmo_torneio_fica_intacta()
    {
        // O filtro grosso em SQL é por torneio: quem separa uma inscrição da outra é o JSON.
        // Errar aqui cancelaria a cobrança de um terceiro que não tem nada a ver com isso.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador, _, dupla) = MontarSolo(ctx);
        var outro = new Jogador { Nome = "Outro", Cpf = "22233344456" };
        ctx.Jogadores.Add(outro);
        await ctx.SaveChangesAsync();
        var duplaDoOutro = new Dupla { CategoriaId = categoria.Id, Jogador1Id = outro.Id, Jogador2Id = null };
        ctx.Duplas.Add(duplaDoOutro);
        await ctx.SaveChangesAsync();
        ctx.Pagamentos.Add(CobrancaAbertaDoPagarDepois(torneio.Id, duplaDoOutro.Id, organizador.Id, outro.Id));
        await ctx.SaveChangesAsync();

        var pagamentos = Substitute.For<IPagamentoInscricaoService>();

        var c = TestInfra.NovoTorneiosController(ctx, organizador.Id, pagamentos: pagamentos);
        await c.CancelarSemParceiro(dupla.Id);

        Assert.Null(await ctx.Duplas.FindAsync(dupla.Id));
        Assert.NotNull(await ctx.Duplas.FindAsync(duplaDoOutro.Id));
        await pagamentos.DidNotReceiveWithAnyArgs().EstornarTotalAsync(default!);
    }

    [Fact]
    public async Task Fatura_ja_confirmada_nao_entra_no_caminho_da_inscricao_nao_paga()
    {
        // "Aberta" é Pendente. Uma cobrança já confirmada tem outro dono de decisão (o bloco
        // do `dupla.Pago`) — cair aqui pediria estorno de dinheiro sem a dupla estar paga.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador, solo, dupla) = MontarSolo(ctx);
        var fatura = CobrancaAbertaDoPagarDepois(torneio.Id, dupla.Id, organizador.Id, solo.Id);
        fatura.Status = "Confirmado";
        ctx.Pagamentos.Add(fatura);
        await ctx.SaveChangesAsync();

        var pagamentos = Substitute.For<IPagamentoInscricaoService>();

        var c = TestInfra.NovoTorneiosController(ctx, organizador.Id, pagamentos: pagamentos);
        await c.CancelarSemParceiro(dupla.Id);

        Assert.Null(await ctx.Duplas.FindAsync(dupla.Id));
        await pagamentos.DidNotReceiveWithAnyArgs().EstornarTotalAsync(default!);
    }
}
