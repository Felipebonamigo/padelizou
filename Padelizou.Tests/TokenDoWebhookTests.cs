using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Services;
using System.Text;

namespace Padelizou.Tests;

// A PORTA DO WEBHOOK DE PAGAMENTO.
//
// `POST /Pagamentos/Webhook` é o único ponto do controller sem login, sem cookie e sem
// antiforgery — quem chama é o meio de pagamento, de fora. O que separa esse endpoint do
// mundo inteiro é UM token no cabeçalho `asaas-access-token`. Quem descobre o token manda
// "PAYMENT_CONFIRMED" e marca inscrição como paga sem ter pago nada.
//
// ⚠️ O DEFEITO QUE ESTES TESTES GUARDAM não é um "if" faltando: a porta já recusava token
// errado. Era COMO ela recusava. O `==` de string do .NET para no primeiro caractere
// diferente, então o tempo de resposta cresce conforme o prefixo vai acertando — dá pra
// descobrir o token um caractere por vez, com chamadas repetidas, sem nunca ter acertado
// ele inteiro. Por isso a comparação é `CryptographicOperations.FixedTimeEquals`.
//
// ⚠️ E É POR ISSO QUE TEM UM TESTE QUE LÊ O CÓDIGO-FONTE. Vazamento por tempo não muda
// resposta nenhuma: os testes de comportamento aqui embaixo passam IGUAL com o `==` de
// volta. Sem `A_comparacao_do_token_e_em_tempo_fixo`, alguém "simplifica" a linha numa
// limpeza, todo o resto continua verde, e o buraco volta calado.
public class TokenDoWebhookTests
{
    private const string TokenCerto = "token-secreto-do-webhook";

    private static PagamentosController ControllerCom(string tokenConfigurado)
    {
        var ctx = TestInfra.NovoContexto();

        return new PagamentosController(
            ctx,
            Options.Create(new AsaasSettings { WebhookToken = tokenConfigurado }),
            Substitute.For<IPagamentoInscricaoService>(),
            Substitute.For<IAsaasService>(),
            NullLogger<PagamentosController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    // Corpo de um evento real, mas para uma cobrança que não é nossa: assim o teste mede a
    // PORTA, não o que acontece depois dela. Token bom devolve Ok (cobrança desconhecida),
    // token ruim devolve Unauthorized — dois resultados que não se confundem.
    private static async Task<IActionResult> ChamarAsync(PagamentosController c, string? tokenEnviado)
    {
        if (tokenEnviado != null) c.HttpContext.Request.Headers["asaas-access-token"] = tokenEnviado;

        c.HttpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(
            "{\"event\":\"PAYMENT_CONFIRMED\",\"payment\":{\"id\":\"pay_que_nao_e_nosso\"}}"));

        return await c.Webhook();
    }

    [Fact]
    public async Task Token_certo_entra()
    {
        var resultado = await ChamarAsync(ControllerCom(TokenCerto), TokenCerto);

        Assert.IsType<OkResult>(resultado);
    }

    [Fact]
    public async Task Token_errado_leva_401()
    {
        var resultado = await ChamarAsync(ControllerCom(TokenCerto), "token-de-quem-tentou");

        Assert.IsType<UnauthorizedResult>(resultado);
    }

    [Fact]
    public async Task Sem_o_cabecalho_leva_401()
    {
        var resultado = await ChamarAsync(ControllerCom(TokenCerto), null);

        Assert.IsType<UnauthorizedResult>(resultado);
    }

    // O caso que o atacante de tempo constrói: acertou o começo, ainda não o resto.
    [Fact]
    public async Task Prefixo_certo_nao_entra()
    {
        var resultado = await ChamarAsync(ControllerCom(TokenCerto), TokenCerto.Substring(0, 10));

        Assert.IsType<UnauthorizedResult>(resultado);
    }

    // ⚠️ Sem token configurado a porta fecha, em vez de abrir pra todo mundo. É o modo de
    // falhar que importa: um ambiente novo que esqueceu a variável recusa o webhook (dá pra
    // perceber) em vez de aceitar qualquer chamada anônima (não dá).
    [Fact]
    public async Task Sem_token_configurado_a_porta_fica_fechada()
    {
        var resultado = await ChamarAsync(ControllerCom(""), "qualquer-coisa");

        Assert.IsType<UnauthorizedResult>(resultado);
    }

    // `FixedTimeEquals` estoura se os arrays têm tamanhos diferentes? Não — devolve false. Mas
    // um espaço sobrando na variável de ambiente derrubaria o webhook inteiro em produção com
    // "token inválido", e o motivo seria invisível. Daí o Trim dos dois lados.
    [Fact]
    public async Task Espaco_em_volta_nao_derruba_o_webhook()
    {
        var resultado = await ChamarAsync(ControllerCom("  " + TokenCerto + "  "), TokenCerto);

        Assert.IsType<OkResult>(resultado);
    }

    // ── O teste que guarda o conserto ────────────────────────────────────────────────────
    //
    // Lê o código-fonte porque a diferença entre certo e errado aqui NÃO aparece em nenhuma
    // resposta HTTP — só no relógio. Mesmo motivo do teste de CSS do dropdown.
    [Fact]
    public void A_comparacao_do_token_e_em_tempo_fixo()
    {
        var corpo = CorpoDoTokenValido();

        Assert.Contains("FixedTimeEquals", corpo);

        // ⚠️ Escrito como o `==` APARECERIA se alguém revertesse, e não como um `==` solto:
        // o comentário do método fala em "o == do .NET", e um teste que procurasse só "=="
        // acusaria erro com o código certo.
        Assert.DoesNotContain("== _settings.WebhookToken", corpo);
    }

    // O corpo de `TokenValido()`, SEM as linhas de comentário — senão o teste acima estaria
    // lendo a explicação em vez do código.
    private static string CorpoDoTokenValido()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "Controllers", "PagamentosController.cs")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");

        var linhas = File.ReadAllLines(Path.Combine(
            dir!.FullName, "Padelizou", "Controllers", "PagamentosController.cs"));

        var inicio = Array.FindIndex(linhas, l => l.Contains("private bool TokenValido()"));
        Assert.True(inicio >= 0, "Não achei o método TokenValido() — ele foi renomeado?");

        // Para no fecha-chaves do próprio método (o único recuado em 4 espaços daqui pra
        // frente): sem esse limite o teste leria o resto do arquivo, e um `FixedTimeEquals`
        // de qualquer outro método faria a asserção passar sem esta porta estar protegida.
        var fim = Array.FindIndex(linhas, inicio + 1, l => l.TrimEnd() == "    }");
        Assert.True(fim > inicio, "Não achei o fim do método TokenValido().");

        return string.Join("\n", linhas[inicio..(fim + 1)]
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));
    }
}
