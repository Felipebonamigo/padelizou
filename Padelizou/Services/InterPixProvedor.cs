using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Padelizou.Services;

// A implementação de IProvedorDeExtratoPix que fala com a API do Inter — a ÚNICA peça deste
// recurso que depende do banco real.
//
// ⚠️ Endpoint, escopo e nome de campo aqui são os documentados PUBLICAMENTE pela API PJ do
// Inter até a escrita disto, mas ninguém rodou isto contra a conta de verdade ainda (a conta
// não existia quando este arquivo foi escrito). O domínio (cdpj.partners.bancointer.com.br)
// e o fluxo OAuth2 client_credentials com certificado mTLS são o que está bem estabelecido;
// o formato exato do JSON de extrato é o que precisa de uma passada no sandbox deles antes
// de confiar em produção. Enquanto isso não acontecer, o pior caso é o
// ConciliacaoAutomaticaDoPixBackgroundService logar erro e a conferência manual em
// /Admin/PixDireto continuar valendo — nada quebra por isto estar errado.
public class InterPixProvedor : IProvedorDeExtratoPix
{
    private const string BaseUrl = "https://cdpj.partners.bancointer.com.br";

    private readonly HttpClient _http;
    private readonly InterPixSettings _cfg;
    private readonly ILogger<InterPixProvedor> _logger;

    private string? _tokenEmCache;
    private DateTime _tokenValidoAte = DateTime.MinValue;

    public InterPixProvedor(HttpClient http, IOptions<InterPixSettings> cfg, ILogger<InterPixProvedor> logger)
    {
        _http = http;
        _cfg = cfg.Value;
        _logger = logger;
    }

    private async Task<string> ObterTokenAsync(CancellationToken ct)
    {
        if (_tokenEmCache != null && DateTime.UtcNow < _tokenValidoAte)
            return _tokenEmCache;

        var corpo = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _cfg.ClientId,
            ["client_secret"] = _cfg.ClientSecret,
            ["grant_type"] = "client_credentials",
            // Escopo de LEITURA do extrato — não pedimos escopo de pagamento/cobrança porque
            // este provedor nunca movimenta dinheiro, só lê o que já caiu.
            ["scope"] = "extrato.read",
        });

        using var resposta = await _http.PostAsync($"{BaseUrl}/oauth/v2/token", corpo, ct);
        resposta.EnsureSuccessStatusCode();

        var token = await resposta.Content.ReadFromJsonAsync<TokenResposta>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Inter não devolveu token.");

        _tokenEmCache = token.AccessToken;
        // 30s de margem: preferimos renovar um pouco antes do que levar um 401 no meio da varredura.
        _tokenValidoAte = DateTime.UtcNow.AddSeconds(Math.Max(0, token.ExpiresIn - 30));
        return _tokenEmCache;
    }

    public async Task<IReadOnlyList<PixRecebidoDoBanco>> ListarRecebidosAsync(
        DateTime desde, DateTime hasta, CancellationToken ct)
    {
        var token = await ObterTokenAsync(ct);

        var url = $"{BaseUrl}/banking/v2/extrato" +
            $"?dataInicio={desde:yyyy-MM-dd}&dataFim={hasta:yyyy-MM-dd}";
        using var requisicao = new HttpRequestMessage(HttpMethod.Get, url);
        requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resposta = await _http.SendAsync(requisicao, ct);
        resposta.EnsureSuccessStatusCode();

        var extrato = await resposta.Content.ReadFromJsonAsync<ExtratoResposta>(cancellationToken: ct);

        return (extrato?.Transacoes ?? [])
            // Só crédito de Pix — TED, boleto pago, taxa do banco etc. não interessam aqui.
            .Where(t => string.Equals(t.TipoTransacao, "PIX", StringComparison.OrdinalIgnoreCase)
                && string.Equals(t.TipoOperacao, "C", StringComparison.OrdinalIgnoreCase))
            .Select(ParaPixRecebido)
            .Where(p => p != null)
            .Select(p => p!)
            .ToList();
    }

    private static PixRecebidoDoBanco? ParaPixRecebido(TransacaoExtrato t)
    {
        if (!decimal.TryParse(t.Valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor))
            return null;
        if (!DateTime.TryParse(t.DataEntrada ?? t.DataTransacao, out var horario))
            return null;

        return new PixRecebidoDoBanco(
            EndToEndId: t.CodigoTransacao ?? $"{horario:O}-{valor}",
            Valor: valor,
            Horario: horario,
            CpfCnpjPagador: t.Detalhes?.CpfCnpj,
            NomePagador: t.Detalhes?.Nome,
            TxId: t.Detalhes?.TxId);
    }

    private record TokenResposta(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private record ExtratoResposta(
        [property: JsonPropertyName("transacoes")] List<TransacaoExtrato>? Transacoes);

    private record TransacaoExtrato(
        [property: JsonPropertyName("codigoTransacao")] string? CodigoTransacao,
        [property: JsonPropertyName("dataTransacao")] string? DataTransacao,
        [property: JsonPropertyName("dataEntrada")] string? DataEntrada,
        [property: JsonPropertyName("tipoTransacao")] string? TipoTransacao,
        [property: JsonPropertyName("tipoOperacao")] string? TipoOperacao,
        [property: JsonPropertyName("valor")] string? Valor,
        [property: JsonPropertyName("detalhes")] DetalhesTransacao? Detalhes);

    private record DetalhesTransacao(
        [property: JsonPropertyName("cpfCnpj")] string? CpfCnpj,
        [property: JsonPropertyName("nome")] string? Nome,
        [property: JsonPropertyName("txId")] string? TxId);
}
