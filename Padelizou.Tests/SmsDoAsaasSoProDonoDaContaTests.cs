using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Padelizou.Models;
using Padelizou.Services;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Padelizou.Tests;

// O ASAAS NÃO MANDA SMS PRO JOGADOR — SÓ PRO DONO DA CONTA.
//
// 🗣️ Felipe, 13/10/2026, depois do Lucas Fonseca (NATA PADEL TOUR) receber cobrança por SMS de
// uma inscrição que já estava paga: *"remova os sms, somente eu posso receber sms"*.
//
// O Asaas cria, sozinho, um jogo de notificações pra cada cliente — e o SMS pro cliente vem
// ligado. Quem desliga é o Padelizou, cliente a cliente: `smsEnabledForCustomer` vira false e o
// resto (e-mail, o SMS do DONO da conta — `smsEnabledForProvider`) fica como estava.
public class SmsDoAsaasSoProDonoDaContaTests
{
    private const string Notificacoes = """
        {"object":"list","hasMore":false,"data":[
          {"id":"not_1","event":"PAYMENT_OVERDUE","enabled":true,
           "emailEnabledForProvider":true,"smsEnabledForProvider":true,
           "emailEnabledForCustomer":true,"smsEnabledForCustomer":true,
           "phoneCallEnabledForCustomer":false,"whatsappEnabledForCustomer":true},
          {"id":"not_2","event":"PAYMENT_RECEIVED","enabled":true,
           "emailEnabledForProvider":true,"smsEnabledForProvider":false,
           "emailEnabledForCustomer":true,"smsEnabledForCustomer":false,
           "phoneCallEnabledForCustomer":false,"whatsappEnabledForCustomer":false}
        ]}
        """;

    [Fact]
    public async Task Desliga_so_o_SMS_do_cliente_e_mantem_o_resto()
    {
        var http = new HttpFalso
        {
            Responder = req => req.Method == HttpMethod.Get ? Resposta(Notificacoes) : Resposta("{}"),
        };

        var ok = await NovoAsaas(http).DesligarSmsDoClienteAsync("cus_1");

        Assert.True(ok);
        var get = Assert.Single(http.Chamadas, c => c.Metodo == "GET");
        Assert.StartsWith("https://gateway.test/v3/customers/cus_1/notifications", get.Url);

        var put = Assert.Single(http.Chamadas, c => c.Metodo == "PUT");
        Assert.Equal("https://gateway.test/v3/notifications/batch", put.Url);

        using var corpo = JsonDocument.Parse(put.Corpo!);
        Assert.Equal("cus_1", corpo.RootElement.GetProperty("customer").GetString());

        // Só a que tinha SMS ligado vai no lote — a outra já está certa.
        var item = Assert.Single(corpo.RootElement.GetProperty("notifications").EnumerateArray());
        Assert.Equal("not_1", item.GetProperty("id").GetString());
        Assert.False(item.GetProperty("smsEnabledForCustomer").GetBoolean());

        // O SMS do DONO DA CONTA continua — "somente eu posso receber sms".
        Assert.True(item.GetProperty("smsEnabledForProvider").GetBoolean());
        Assert.True(item.GetProperty("enabled").GetBoolean());
        Assert.True(item.GetProperty("emailEnabledForCustomer").GetBoolean());
        Assert.True(item.GetProperty("emailEnabledForProvider").GetBoolean());
        Assert.True(item.GetProperty("whatsappEnabledForCustomer").GetBoolean());
        Assert.False(item.GetProperty("phoneCallEnabledForCustomer").GetBoolean());
    }

    // Cliente que já está sem SMS não gasta um PUT à toa.
    [Fact]
    public async Task Sem_nenhum_SMS_ligado_nao_altera_nada()
    {
        var semSms = Notificacoes.Replace("\"smsEnabledForCustomer\":true", "\"smsEnabledForCustomer\":false");
        var http = new HttpFalso { Responder = _ => Resposta(semSms) };

        Assert.True(await NovoAsaas(http).DesligarSmsDoClienteAsync("cus_1"));
        Assert.DoesNotContain(http.Chamadas, c => c.Metodo == "PUT");
    }

    [Fact]
    public async Task Falha_do_gateway_devolve_false_sem_estourar()
    {
        var http = new HttpFalso { Responder = _ => Resposta("{}", HttpStatusCode.InternalServerError) };

        Assert.False(await NovoAsaas(http).DesligarSmsDoClienteAsync("cus_1"));
    }

    // ── Todo cliente que passa pelo Padelizou sai sem SMS, novo ou reaproveitado ──

    [Fact]
    public async Task Cliente_reaproveitado_pelo_CPF_tem_o_SMS_desligado()
    {
        var http = new HttpFalso
        {
            Responder = req =>
                req.RequestUri!.AbsolutePath.EndsWith("/customers") && req.Method == HttpMethod.Get
                    ? Resposta("""{"data":[{"id":"cus_velho"}]}""")
                    : req.RequestUri.AbsolutePath.EndsWith("/notifications")
                        ? Resposta(Notificacoes)
                        : Resposta("{}"),
        };

        var id = await NovoAsaas(http).ObterOuCriarClienteAsync("Lucas Fonseca", "11144477735", null, null);

        Assert.Equal("cus_velho", id);
        Assert.Contains(http.Chamadas, c => c.Metodo == "GET" && c.Url.Contains("/customers/cus_velho/notifications"));
        Assert.Contains(http.Chamadas, c => c.Metodo == "PUT" && c.Url.EndsWith("/notifications/batch"));
    }

    [Fact]
    public async Task Cliente_novo_tem_o_SMS_desligado_antes_da_primeira_cobranca()
    {
        var http = new HttpFalso
        {
            Responder = req =>
                req.RequestUri!.AbsolutePath.EndsWith("/customers") && req.Method == HttpMethod.Get
                    ? Resposta("""{"data":[]}""")
                    : req.RequestUri.AbsolutePath.EndsWith("/customers") && req.Method == HttpMethod.Post
                        ? Resposta("""{"id":"cus_novo"}""")
                        : req.RequestUri.AbsolutePath.EndsWith("/notifications")
                            ? Resposta(Notificacoes)
                            : Resposta("{}"),
        };

        var id = await NovoAsaas(http).ObterOuCriarClienteAsync("Lucas Fonseca", "11144477735", null, null);

        Assert.Equal("cus_novo", id);
        Assert.Contains(http.Chamadas, c => c.Metodo == "PUT" && c.Url.EndsWith("/notifications/batch"));
    }

    // Desligar o SMS é acessório: se falhar, a cobrança sai do mesmo jeito. Perder uma
    // inscrição porque o Asaas não respondeu a lista de notificações seria trocar um incômodo
    // por um prejuízo.
    [Fact]
    public async Task Falha_ao_desligar_o_SMS_nao_impede_o_cliente()
    {
        var http = new HttpFalso
        {
            Responder = req => req.RequestUri!.AbsolutePath.EndsWith("/notifications")
                ? Resposta("{}", HttpStatusCode.InternalServerError)
                : Resposta("""{"data":[{"id":"cus_velho"}]}"""),
        };

        Assert.Equal("cus_velho",
            await NovoAsaas(http).ObterOuCriarClienteAsync("Lucas Fonseca", "11144477735", null, null));
    }

    // ── Quem JÁ é cliente e nunca mais vai gerar cobrança também precisa sair do SMS ──
    //
    // O caso do próprio Lucas: a cobrança dele já existe e é ela que dispara o SMS. A varredura
    // de partida pega os clientes das cobranças recentes.

    [Fact]
    public async Task A_varredura_pega_os_clientes_das_cobrancas_recentes_sem_repetir()
    {
        using var ctx = TestInfra.NovoContexto();
        var agora = new DateTime(2026, 10, 13, 12, 0, 0);

        ctx.Pagamentos.AddRange(
            new Pagamento { Tipo = "TorneioPagarDepois", AsaasCustomerId = "cus_a", CriadoEm = agora.AddDays(-5) },
            new Pagamento { Tipo = "TorneioPagarDepois", AsaasCustomerId = "cus_a", CriadoEm = agora.AddDays(-2) },
            new Pagamento { Tipo = "Torneio", AsaasCustomerId = "cus_b", CriadoEm = agora.AddDays(-80) },
            // Antiga demais: o Asaas não lembra mais ninguém dela.
            new Pagamento { Tipo = "Torneio", AsaasCustomerId = "cus_velho", CriadoEm = agora.AddDays(-200) },
            // Pix direto: nunca passou pelo Asaas.
            new Pagamento { Tipo = "Torneio", AsaasCustomerId = null, CriadoEm = agora.AddDays(-1) });
        await ctx.SaveChangesAsync();

        var clientes = await SmsDoAsaasSoProDonoDaConta.ClientesParaDesligarAsync(ctx, agora);

        Assert.Equal(new[] { "cus_a", "cus_b" }, clientes.OrderBy(c => c));
    }

    // ── Montagem ──

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
}
