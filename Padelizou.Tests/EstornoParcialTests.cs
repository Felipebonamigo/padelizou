using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Padelizou.Tests;

// Devolver PARTE do dinheiro de uma cobrança paga.
//
// Nasceu de um caso real (09/08/2026): uma inscrição do NATA PADEL TOUR foi cobrada R$ 250
// quando o preço de quem entra sem parceiro virou R$ 125 poucas horas depois. O estorno que
// existia era só TOTAL — e total, naquele pagamento, significaria APAGAR a inscrição.
//
// O que estes testes protegem, em uma frase: devolver um troco não pode custar a vaga de
// ninguém, nem tirar dinheiro do organizador duas vezes.
public class EstornoParcialTests
{
    private static Pagamento CobrancaDe250() => new()
    {
        Tipo = "TorneioDupla",
        Status = "Confirmado",
        Valor = 250m,
        ValorRepasse = 212.50m,
        Comissao = 37.50m,
        AsaasPaymentId = "pay_teste",
        JogadorId = 1,
        RecebedorId = 2,
        ReferenciaId = 511,
    };

    // ── A conta ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Devolver_metade_tira_metade_de_cada_um()
    {
        // O caso real: R$ 250 no cartão (15%), devolvendo os R$ 125 cobrados a mais. Quem
        // recebeu o split devolve a parte dele junto — não dá pra tirar só da comissão.
        var sobra = EstornoParcial.Calcular(250m, 212.50m, 37.50m, 125m);

        Assert.Equal(125m, sobra.Valor);
        Assert.Equal(106.25m, sobra.Repasse);
        Assert.Equal(18.75m, sobra.Comissao);
    }

    [Theory]
    [InlineData(250, 212.50, 37.50, 125)]
    [InlineData(100, 90, 10, 33.33)]
    [InlineData(33.33, 28.33, 5.00, 11.11)]
    [InlineData(79.90, 71.91, 7.99, 0.01)]
    public void Repasse_mais_comissao_sempre_fecham_com_o_valor(
        decimal valor, decimal repasse, decimal comissao, decimal devolver)
    {
        // O centavo que não fecha é exatamente o que faz o extrato do organizador divergir
        // da fatura. Por isso a comissão é arredondada e o repasse sai por diferença.
        var sobra = EstornoParcial.Calcular(valor, repasse, comissao, devolver);

        Assert.Equal(sobra.Valor, sobra.Repasse + sobra.Comissao);
    }

    [Fact]
    public void Devolver_tudo_zera_a_cobranca()
    {
        var sobra = EstornoParcial.Calcular(250m, 212.50m, 37.50m, 250m);

        Assert.Equal(0m, sobra.Valor);
        Assert.Equal(0m, sobra.Repasse);
        Assert.Equal(0m, sobra.Comissao);
    }

    [Fact]
    public void Devolver_mais_do_que_tem_nao_gera_valor_negativo()
    {
        // A validação da tela já barra isto; a conta não pode depender dela pra não inverter
        // o sinal do repasse.
        var sobra = EstornoParcial.Calcular(250m, 212.50m, 37.50m, 900m);

        Assert.Equal(0m, sobra.Valor);
        Assert.True(sobra.Repasse >= 0);
        Assert.True(sobra.Comissao >= 0);
    }

    [Fact]
    public void Duas_devolucoes_seguidas_acumulam_sem_se_atropelar()
    {
        var p = CobrancaDe250();

        EstornoParcial.Aplicar(p, 100m);
        EstornoParcial.Aplicar(p, 50m);

        Assert.Equal(150m, p.ValorEstornado);
        Assert.Equal(100m, p.Valor);
        Assert.Equal(p.Valor, p.ValorRepasse + p.Comissao);
    }

    // ── O que não pode ser devolvido ──────────────────────────────────────────────────────

    [Fact]
    public void Cobranca_nao_paga_nao_tem_o_que_devolver()
    {
        var p = CobrancaDe250();
        p.Status = "Pendente";

        Assert.NotNull(EstornoParcial.ProblemaParaEstornar(p, 10m));
        Assert.Equal(0m, EstornoParcial.DisponivelParaEstorno(p));
    }

    [Fact]
    public void Nao_da_pra_devolver_mais_do_que_a_cobranca_tem()
    {
        Assert.NotNull(EstornoParcial.ProblemaParaEstornar(CobrancaDe250(), 300m));
    }

    [Fact]
    public void Valor_zerado_ou_negativo_e_recusado()
    {
        Assert.NotNull(EstornoParcial.ProblemaParaEstornar(CobrancaDe250(), 0m));
        Assert.NotNull(EstornoParcial.ProblemaParaEstornar(CobrancaDe250(), -5m));
    }

    [Fact]
    public void Devolver_o_que_resta_e_estorno_TOTAL()
    {
        // Distinção que decide se a inscrição sobrevive: quem devolve tudo está cancelando.
        var p = CobrancaDe250();

        Assert.True(EstornoParcial.EhTotal(p, 250m));
        Assert.False(EstornoParcial.EhTotal(p, 249.99m));

        // Depois de devolver parte, "tudo" passa a ser o que sobrou.
        EstornoParcial.Aplicar(p, 125m);
        Assert.True(EstornoParcial.EhTotal(p, 125m));
    }

    // ── Pela tela ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Quanto_da_devolucao_sai_da_fatia_do_organizador()
    {
        // O número que o gateway PRECISA receber. Sem ele o Asaas tira os R$ 125 da comissão
        // de R$ 37,50 e recusa: "Valor da cobrança insuficiente para o estorno solicitado".
        Assert.Equal(106.25m, EstornoParcial.ParteDoRepasse(CobrancaDe250(), 125m));
    }

    [Fact]
    public async Task Devolver_parte_NAO_desfaz_a_inscricao()
    {
        var (c, ctx, asaas, inscricoes) = Montar(logadoComoId: 2);

        await c.Estornar(1, 125m);

        // A cobrança NÃO pode virar "Estornado": é esse status que o resto do sistema lê como
        // "essa inscrição não vale mais". Quem apaga de fato é o webhook de estorno total —
        // por isso o teste do webhook, mais abaixo, é o que fecha a garantia.
        await inscricoes.DidNotReceiveWithAnyArgs().DesfazerAsync(default!);

        var p = ctx.Pagamentos.Single();
        Assert.Equal("Confirmado", p.Status);
        Assert.Equal(125m, p.Valor);
        Assert.Equal(106.25m, p.ValorRepasse);
        Assert.Equal(18.75m, p.Comissao);
        Assert.Equal(125m, p.ValorEstornado);

        // E o gateway recebeu os DOIS números: quanto devolver e quanto disso é do organizador.
        await asaas.Received().EstornarAsync("pay_teste", true,
            Arg.Is<DevolucaoParcial>(d => d.Valor == 125m && d.ValorDoRepasse == 106.25m));
    }

    [Fact]
    public async Task Devolver_o_valor_cheio_cai_no_estorno_total()
    {
        // Quem apaga a inscrição é o webhook PAYMENT_REFUNDED (ver EstornoDesfazInscricao);
        // aqui o que importa é a cobrança virar "Estornado" e o gateway receber o pedido SEM
        // valor — com valor ele devolveria só um pedaço e a inscrição ficaria pendurada.
        var (c, ctx, asaas, _) = Montar(logadoComoId: 2);

        await c.Estornar(1, 250m);

        Assert.Equal("Estornado", ctx.Pagamentos.Single().Status);
        await asaas.Received().EstornarAsync("pay_teste", true, null);
    }

    [Fact]
    public async Task Sem_valor_continua_devolvendo_tudo()
    {
        // Compatibilidade com o botão de sempre: campo vazio = estorno total.
        var (c, ctx, asaas, _) = Montar(logadoComoId: 2);

        await c.Estornar(1, null);

        Assert.Equal("Estornado", ctx.Pagamentos.Single().Status);
        await asaas.Received().EstornarAsync("pay_teste", true, null);
    }

    [Fact]
    public async Task Gateway_que_recusa_nao_mexe_em_nada()
    {
        // Gravar antes da confirmação do gateway faria o extrato dizer que o dinheiro voltou
        // sem que ele tivesse voltado.
        var asaas = Substitute.For<IAsaasService>();
        asaas.EstornarAsync(default!, default, default).ReturnsForAnyArgs(false);

        var (c, ctx, _, _) = Montar(logadoComoId: 2, asaas: asaas);

        await c.Estornar(1, 125m);

        var p = ctx.Pagamentos.Single();
        Assert.Equal(250m, p.Valor);
        Assert.Equal(0m, p.ValorEstornado);
    }

    [Fact]
    public async Task Estranho_nao_devolve_dinheiro_de_ninguem()
    {
        var (c, ctx, _, _) = Montar(logadoComoId: 99);

        var resultado = await c.Estornar(1, 125m);

        Assert.IsType<ForbidResult>(resultado);
        Assert.Equal(250m, ctx.Pagamentos.Single().Valor);
    }

    [Fact]
    public async Task Admin_raiz_devolve_o_que_o_Padelizou_cobrou_a_mais()
    {
        // Quem cobrou errado foi a plataforma, e a conta do gateway é dela: recusar aqui
        // empurraria o conserto pro painel do gateway, onde o nosso banco não fica sabendo.
        var (c, ctx, _, _) = Montar(logadoComoId: 3, adminRaizId: 3);

        await c.Estornar(1, 125m);

        Assert.Equal(125m, ctx.Pagamentos.Single().Valor);
    }

    // ── Pelo webhook (estorno feito no painel do gateway) ─────────────────────────────────

    [Fact]
    public async Task Webhook_de_devolucao_parcial_registra_sem_apagar_a_inscricao()
    {
        var (c, ctx, _, inscricoes) = Montar(logadoComoId: 2);

        await EnviarWebhookAsync(c, "PAYMENT_PARTIALLY_REFUNDED", refunds: new[] { 125m });

        var p = ctx.Pagamentos.Single();
        Assert.Equal("Confirmado", p.Status);
        Assert.Equal(125m, p.Valor);
        Assert.Equal(125m, p.ValorEstornado);
        await inscricoes.DidNotReceiveWithAnyArgs().DesfazerAsync(default!);
    }

    [Fact]
    public async Task Webhook_repetido_nao_desconta_o_mesmo_dinheiro_duas_vezes()
    {
        // O Asaas reenvia o evento até receber 200. Descontar "o que chegou agora" a cada
        // entrega levaria a cobrança a zero sozinha.
        var (c, ctx, _, _) = Montar(logadoComoId: 2);

        await EnviarWebhookAsync(c, "PAYMENT_PARTIALLY_REFUNDED", refunds: new[] { 125m });
        await EnviarWebhookAsync(c, "PAYMENT_PARTIALLY_REFUNDED", refunds: new[] { 125m });

        var p = ctx.Pagamentos.Single();
        Assert.Equal(125m, p.Valor);
        Assert.Equal(125m, p.ValorEstornado);
    }

    [Fact]
    public async Task Webhook_com_uma_devolucao_nova_registra_so_a_diferenca()
    {
        // Segunda devolução na mesma cobrança: o payload traz a lista INTEIRA de estornos.
        var (c, ctx, _, _) = Montar(logadoComoId: 2);

        await EnviarWebhookAsync(c, "PAYMENT_PARTIALLY_REFUNDED", refunds: new[] { 100m });
        await EnviarWebhookAsync(c, "PAYMENT_PARTIALLY_REFUNDED", refunds: new[] { 100m, 50m });

        var p = ctx.Pagamentos.Single();
        Assert.Equal(100m, p.Valor);
        Assert.Equal(150m, p.ValorEstornado);
    }

    [Fact]
    public async Task Estorno_cancelado_no_gateway_nao_conta_como_dinheiro_devolvido()
    {
        var (c, ctx, _, _) = Montar(logadoComoId: 2);

        await EnviarWebhookAsync(c, "PAYMENT_PARTIALLY_REFUNDED",
            refunds: new[] { 125m }, statusDoPrimeiro: "CANCELLED");

        var p = ctx.Pagamentos.Single();
        Assert.Equal(250m, p.Valor);
        Assert.Equal(0m, p.ValorEstornado);
    }

    [Fact]
    public async Task Devolucao_pela_tela_nao_e_descontada_de_novo_quando_o_webhook_chega()
    {
        // O caminho completo do caso real: devolvemos pela tela e o gateway avisa depois.
        var (c, ctx, _, _) = Montar(logadoComoId: 2);

        await c.Estornar(1, 125m);
        await EnviarWebhookAsync(c, "PAYMENT_PARTIALLY_REFUNDED", refunds: new[] { 125m });

        var p = ctx.Pagamentos.Single();
        Assert.Equal(125m, p.Valor);
        Assert.Equal(125m, p.ValorEstornado);
    }

    // ── O que sai no fio pro gateway ──────────────────────────────────────────────────────
    //
    // Esta seção existe por causa de uma recusa em PRODUÇÃO (09/08/2026): o corpo ia só com
    // `value`, o Asaas tentou tirar os R$ 125 da comissão de R$ 37,50 e respondeu "Valor da
    // cobrança insuficiente para o estorno solicitado". Testar o serviço com dublê não pegaria:
    // o defeito estava no JSON, não na decisão de chamar.

    [Fact]
    public async Task Devolucao_parcial_manda_splitRefunds_com_o_id_do_split()
    {
        var http = new HttpFalso();
        http.Responder = req => req.Method == HttpMethod.Get
            ? Resposta("""{"data":[{"id":"split-abc","walletId":"w-1","value":212.50}]}""")
            : Resposta("""{"status":"REFUNDED"}""");

        var ok = await NovoAsaas(http).EstornarAsync("pay_1", true, new DevolucaoParcial(125m, 106.25m));

        Assert.True(ok);

        // Consultou os splits antes — é de lá que sai o id, que a gente não guarda.
        Assert.Contains(http.Chamadas, c => c.Metodo == "GET" && c.Url.EndsWith("/payments/pay_1/splits"));

        var post = http.Chamadas.Single(c => c.Metodo == "POST");
        using var corpo = JsonDocument.Parse(post.Corpo!);
        Assert.Equal(125m, corpo.RootElement.GetProperty("value").GetDecimal());

        var split = corpo.RootElement.GetProperty("splitRefunds").EnumerateArray().Single();
        Assert.Equal("split-abc", split.GetProperty("id").GetString());
        Assert.Equal(106.25m, split.GetProperty("value").GetDecimal());
    }

    [Fact]
    public async Task Sem_repasse_a_devolver_nao_consulta_split_nenhum()
    {
        // Cobrança sem split (o recebedor é o próprio Padelizou): o valor todo é da casa.
        var http = new HttpFalso();
        http.Responder = _ => Resposta("""{"status":"REFUNDED"}""");

        var ok = await NovoAsaas(http).EstornarAsync("pay_1", true, new DevolucaoParcial(50m, 0m));

        Assert.True(ok);
        Assert.DoesNotContain(http.Chamadas, c => c.Metodo == "GET");

        using var corpo = JsonDocument.Parse(http.Chamadas.Single().Corpo!);
        Assert.False(corpo.RootElement.TryGetProperty("splitRefunds", out _));
    }

    [Fact]
    public async Task Estorno_total_vai_sem_corpo_nenhum()
    {
        // No total o gateway reverte o split sozinho — mandar splitRefunds aqui seria pedir
        // duas vezes a mesma devolução.
        var http = new HttpFalso();
        http.Responder = _ => Resposta("""{"status":"REFUNDED"}""");

        await NovoAsaas(http).EstornarAsync("pay_1", true);

        Assert.Null(http.Chamadas.Single().Corpo);
    }

    [Fact]
    public async Task Split_que_nao_da_pra_identificar_ABORTA_a_devolucao()
    {
        // Duas carteiras e nenhuma regra pra dividir a devolução entre elas. Chutar tiraria
        // dinheiro de quem não devia — melhor não devolver do que devolver do lado errado.
        var http = new HttpFalso();
        http.Responder = req => req.Method == HttpMethod.Get
            ? Resposta("""{"data":[{"id":"split-a"},{"id":"split-b"}]}""")
            : Resposta("""{"status":"REFUNDED"}""");

        var ok = await NovoAsaas(http).EstornarAsync("pay_1", true, new DevolucaoParcial(125m, 106.25m));

        Assert.False(ok);
        Assert.DoesNotContain(http.Chamadas, c => c.Metodo == "POST");
    }

    [Fact]
    public async Task Se_a_consulta_de_splits_falha_o_estorno_nao_sai()
    {
        var http = new HttpFalso();
        http.Responder = req => req.Method == HttpMethod.Get
            ? Resposta("""{"errors":[{"description":"nao encontrado"}]}""", HttpStatusCode.NotFound)
            : Resposta("""{"status":"REFUNDED"}""");

        var ok = await NovoAsaas(http).EstornarAsync("pay_1", true, new DevolucaoParcial(125m, 106.25m));

        Assert.False(ok);
        Assert.DoesNotContain(http.Chamadas, c => c.Metodo == "POST");
    }

    private sealed record Chamada(string Metodo, string Url, string? Corpo);

    private sealed class HttpFalso : HttpMessageHandler
    {
        public List<Chamada> Chamadas { get; } = new();
        public Func<HttpRequestMessage, HttpResponseMessage>? Responder { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var corpo = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            Chamadas.Add(new Chamada(request.Method.Method, request.RequestUri!.AbsoluteUri, corpo));

            return Responder?.Invoke(request) ?? Resposta("{}");
        }
    }

    private static HttpResponseMessage Resposta(string corpo, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") };

    private static AsaasService NovoAsaas(HttpFalso http) =>
        new(new HttpClient(http),
            Options.Create(new AsaasSettings { ApiKey = "chave", BaseUrl = "https://gateway.test/v3" }),
            NullLogger<AsaasService>.Instance);

    // ── Montagem ──────────────────────────────────────────────────────────────────────────

    private static (PagamentosController c, DbPadelContext ctx, IAsaasService asaas,
        IPagamentoInscricaoService inscricoes) Montar(
        int logadoComoId, IAsaasService? asaas = null, int? adminRaizId = null)
    {
        var ctx = TestInfra.NovoContexto();

        ctx.Jogadores.Add(new Jogador { Id = 1, Nome = "Quem pagou", Cpf = "99900000001" });
        ctx.Jogadores.Add(new Jogador { Id = 2, Nome = "Quem organiza", Cpf = "99900000002" });
        ctx.Jogadores.Add(new Jogador
        {
            Id = 3,
            Nome = "Dono do app",
            Cpf = "99900000003",
            IsAdminRaiz = adminRaizId == 3,
        });

        var pagamento = CobrancaDe250();
        pagamento.Id = 1;
        ctx.Pagamentos.Add(pagamento);
        ctx.SaveChanges();

        if (asaas == null)
        {
            asaas = Substitute.For<IAsaasService>();
            asaas.EstornarAsync(default!, default, default).ReturnsForAnyArgs(true);
        }

        var inscricoes = Substitute.For<IPagamentoInscricaoService>();

        var controller = new PagamentosController(
            ctx,
            Options.Create(new AsaasSettings { WebhookToken = "token-de-teste" }),
            inscricoes, asaas, NullLogger<PagamentosController>.Instance)
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

        return (controller, ctx, asaas, inscricoes);
    }

    private static async Task EnviarWebhookAsync(PagamentosController c, string evento,
        decimal[] refunds, string? statusDoPrimeiro = null)
    {
        var itens = refunds.Select((valor, i) =>
        {
            var status = i == 0 && statusDoPrimeiro != null ? statusDoPrimeiro : "DONE";
            return $"{{\"value\":{valor.ToString(System.Globalization.CultureInfo.InvariantCulture)},"
                + $"\"status\":\"{status}\"}}";
        });

        var corpo = $"{{\"event\":\"{evento}\",\"payment\":{{\"id\":\"pay_teste\","
            + $"\"refunds\":[{string.Join(",", itens)}]}}}}";

        c.HttpContext.Request.Headers["asaas-access-token"] = "token-de-teste";
        c.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(corpo));

        await c.Webhook();
    }
}
