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

// A FATURA SAIU DA CASA DO GATEWAY (10/08/2026).
//
// Quem clicava em "pagar" era mandado pra página hospedada do meio de pagamento — e aquela
// página estampa o cadastro comercial inteiro de quem EMITE a cobrança: nome, CNPJ, e-mail,
// telefone e o endereço completo, com número e apartamento.
//
// Como toda cobrança do Padelizou nasce na conta principal (o split é que manda a fatia do
// organizador pra carteira dele), o cadastro estampado era sempre o mesmo. Não era um torneio
// mostrando o endereço de alguém: era toda inscrição de todo torneio, mais aula, quadra,
// mensalidade de professor e taxa de plataforma, mostrando o mesmo endereço residencial.
//
// O Pix continua vindo de lá — o que mudou foi de quem é a TELA.
public class FaturaDentroDoPadelizouTests
{
    private const string UrlDoGateway = "https://www.asaas.com/i/abc123";

    // ── O link que sai de dentro do sistema ───────────────────────────────────────────────

    [Fact]
    public void O_link_de_pagamento_aponta_pra_dentro_do_Padelizou()
    {
        Assert.Equal("/Pagamentos/Fatura/42", LinkDoPagamento.Para(42));
        Assert.Equal("/Pagamentos/Fatura/7", LinkDoPagamento.Para(new Pagamento { Id = 7 }));
    }

    [Fact]
    public async Task Quem_se_inscreve_nao_recebe_mais_a_URL_do_gateway()
    {
        // ⚠️ O teste que importa neste arquivo. A regressão aqui é silenciosa: devolver
        // `cobranca.InvoiceUrl` de novo continua "funcionando" — a pessoa paga, a inscrição
        // confirma, nada quebra. Só que o endereço de casa volta a aparecer pra ela.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1);
        torneio.PrecoInscricao = 125m;
        torneio.FormaPagamento = FormaDePagamentoDoTorneio.TodasAsFormas;
        ctx.SaveChanges();

        var dupla = ctx.Duplas.First(d => d.CategoriaId == categoria.Id);
        var pagador = ctx.Jogadores.Find(dupla.Jogador1Id)!;

        var asaas = Substitute.For<IAsaasService>();
        asaas.Configurado.Returns(true);
        asaas.CalcularRateio(default, default!, default, default)
            .ReturnsForAnyArgs(ci => new RateioComissao((decimal)ci[0]!, (decimal)ci[0]!, 0m));
        asaas.ObterOuCriarClienteAsync(default!, default!, default, default)
            .ReturnsForAnyArgs(Task.FromResult<string?>("cli_1"));
        asaas.CriarCobrancaAsync(default!, default!, default!, default!, default, default, default!)
            .ReturnsForAnyArgs(_ => Task.FromResult<CobrancaAsaas?>(
                new CobrancaAsaas("pay_1", UrlDoGateway)));

        var servico = new PagamentoInscricaoService(
            ctx, asaas, Options.Create(new AsaasSettings()),
            NullLogger<PagamentoInscricaoService>.Instance,
            Substitute.For<IPushNotificationService>(),
            Options.Create(new TaxasExibicao()),
            Options.Create(new PlanoProfessorSettings()));

        var organizador = new Jogador
        {
            Nome = "Organizador", Cpf = "11144477735",
            ReceberPagamentoOnline = true, AsaasWalletId = "carteira",
        };

        var destino = await servico.IniciarCobrancaDeInscricaoAsync(
            torneio, organizador, pagador, inscricaoDeDupla: true, impedimentos: 0,
            new DadosPagamentoDeInscricao(torneio.Id, dupla.Id, null), CobrancaDoTorneio.EscolhaPix);

        var gravado = ctx.Pagamentos.Single();
        Assert.Equal(LinkDoPagamento.Para(gravado), destino);
        Assert.DoesNotContain("asaas", destino!, StringComparison.OrdinalIgnoreCase);

        // Mas a fatura de lá continua GRAVADA: ela é o plano B da tela (cobrança de cartão) e
        // é o que várias consultas usam pra saber que a cobrança existe no gateway.
        Assert.Equal(UrlDoGateway, gravado.InvoiceUrl);
    }

    // ── A tela ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cobranca_com_Pix_desenha_o_QR_aqui_e_nao_manda_ninguem_pra_fora()
    {
        var (c, ctx, eu) = Montar(ComPix("00020126...5802BR"));
        var pagamento = Pendente(ctx, pagadorId: eu.Id);

        var resultado = await c.Fatura(pagamento.Id);

        var view = Assert.IsType<ViewResult>(resultado);
        Assert.Equal("00020126...5802BR", view.ViewData["CopiaECola"]);

        // O QR é desenhado aqui, do mesmo código: um PNG de verdade (o "iVBORw0KGgo" é a
        // assinatura de PNG em base64), e não a imagem que o gateway mandou junto.
        var qr = Assert.IsType<string>(view.ViewData["QrBase64"]);
        Assert.StartsWith("iVBORw0KGgo", qr);
    }

    [Fact]
    public async Task Sem_Pix_a_pessoa_vai_pro_gateway_porque_o_cartao_se_digita_la()
    {
        // Cobrança travada em cartão não tem QR de Pix. Aqui o desvio é o certo: número de
        // cartão não passa pelo Padelizou, e a tela de digitar cartão é a deles.
        var (c, ctx, eu) = Montar(SemPix());
        var pagamento = Pendente(ctx, pagadorId: eu.Id);

        var resultado = await c.Fatura(pagamento.Id);

        var redirect = Assert.IsType<RedirectResult>(resultado);
        Assert.Equal(UrlDoGateway, redirect.Url);
    }

    [Fact]
    public async Task Cobranca_ja_paga_nao_vai_perguntar_Pix_pro_gateway()
    {
        // Pedir o código de uma cobrança confirmada gasta uma chamada pra devolver um QR que
        // ninguém deve usar — e ainda ofereceria "pague de novo" pra quem já pagou.
        var asaas = ComPix("nao-deveria-ser-pedido");
        var (c, ctx, eu) = Montar(asaas);

        var pagamento = Pendente(ctx, pagadorId: eu.Id);
        pagamento.Status = "Confirmado";
        ctx.SaveChanges();

        var resultado = await c.Fatura(pagamento.Id);

        Assert.IsType<ViewResult>(resultado);
        await asaas.DidNotReceiveWithAnyArgs().ObterPixAsync(default!);
    }

    [Fact]
    public async Task Fatura_de_terceiro_nao_abre()
    {
        // O id vai na URL, e adivinhar o do vizinho é trivial. Sem esta trava, a fatura de
        // qualquer pessoa abriria pra qualquer um que estivesse logado.
        var (c, ctx, _) = Montar(ComPix("qr"));
        var outra = new Jogador { Nome = "Outra pessoa", Cpf = "52998224725" };
        ctx.Jogadores.Add(outra);
        ctx.SaveChanges();

        var pagamento = Pendente(ctx, pagadorId: outra.Id);

        var resultado = await c.Fatura(pagamento.Id);

        Assert.IsType<ForbidResult>(resultado);
    }

    [Fact]
    public async Task Pix_direto_continua_na_tela_dele()
    {
        // O Pix direto não passa pelo gateway: a chave é nossa e a confirmação é manual.
        // Mandá-lo pra cá faria a tela pedir um QR que não existe lá.
        var asaas = ComPix("qr-do-gateway");
        var (c, ctx, eu) = Montar(asaas);

        var pagamento = Pendente(ctx, pagadorId: eu.Id);
        pagamento.MetodoPagamento = PixDireto.Metodo;
        pagamento.AsaasPaymentId = null;
        ctx.SaveChanges();

        var resultado = await c.Fatura(pagamento.Id);

        var redirect = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(nameof(PagamentosController.Pix), redirect.ActionName);
        await asaas.DidNotReceiveWithAnyArgs().ObterPixAsync(default!);
    }

    // ── Andaimes ──────────────────────────────────────────────────────────────────────────

    private static IAsaasService ComPix(string copiaECola)
    {
        var asaas = Substitute.For<IAsaasService>();
        asaas.Configurado.Returns(true);
        asaas.ObterPixAsync(default!).ReturnsForAnyArgs(
            Task.FromResult<PixDaCobranca?>(new PixDaCobranca(copiaECola)));
        return asaas;
    }

    private static IAsaasService SemPix()
    {
        var asaas = Substitute.For<IAsaasService>();
        asaas.Configurado.Returns(true);
        asaas.ObterPixAsync(default!).ReturnsForAnyArgs(Task.FromResult<PixDaCobranca?>(null));
        return asaas;
    }

    private static Pagamento Pendente(DbPadelContext ctx, int pagadorId)
    {
        var pagamento = new Pagamento
        {
            Tipo = "TorneioDupla",
            JogadorId = pagadorId,
            Valor = 125m,
            AsaasPaymentId = "pay_1",
            InvoiceUrl = UrlDoGateway,
            Status = "Pendente",
        };
        ctx.Pagamentos.Add(pagamento);
        ctx.SaveChanges();
        return pagamento;
    }

    private static (PagamentosController c, DbPadelContext ctx, Jogador eu) Montar(IAsaasService asaas)
    {
        var ctx = TestInfra.NovoContexto();
        var eu = new Jogador { Nome = "Quem paga", Cpf = "11144477735" };
        ctx.Jogadores.Add(eu);
        ctx.SaveChanges();

        var controller = new PagamentosController(
            ctx, Options.Create(new AsaasSettings()),
            Substitute.For<IPagamentoInscricaoService>(), asaas,
            NullLogger<PagamentosController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, eu.Id.ToString()) }, "Teste")),
                },
            },
        };
        controller.TempData = new TempDataDictionary(
            controller.HttpContext, Substitute.For<ITempDataProvider>());

        return (controller, ctx, eu);
    }
}
