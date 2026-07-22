using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Padelizou.Services;

public class WhatsAppApiService : IWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly ZApiSettings _settings;
    private readonly ILogger<WhatsAppApiService> _logger;

    public WhatsAppApiService(HttpClient httpClient, IOptions<ZApiSettings> settings, ILogger<WhatsAppApiService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> EnviarAsync(string? celular, string mensagem)
    {
        if (string.IsNullOrEmpty(_settings.InstanceId) || string.IsNullOrEmpty(_settings.Token))
        {
            _logger.LogWarning("Z-API não configurada (InstanceId/Token em branco) — lembrete não enviado.");
            return false;
        }

        var numero = WhatsAppLinkHelper.LimparNumero(celular);
        if (string.IsNullOrEmpty(numero))
        {
            _logger.LogWarning("Jogador sem celular válido — lembrete não enviado.");
            return false;
        }

        try
        {
            var url = $"{_settings.BaseUrl.TrimEnd('/')}/instances/{_settings.InstanceId}/token/{_settings.Token}/send-text";

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(new { phone = $"55{numero}", message = mensagem })
            };
            request.Headers.Add("Client-Token", _settings.ClientToken);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var corpo = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Z-API retornou {Status} ao enviar pra {Numero}: {Corpo}", response.StatusCode, numero, corpo);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar WhatsApp via Z-API pra {Numero}.", numero);
            return false;
        }
    }
}
