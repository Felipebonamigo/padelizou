using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Padelizou.Services;

// Envia a mensagem pelo WhatsApp usando a Evolution API — um chip pré-pago de verdade
// conectado num container do nosso próprio VPS. Substituiu a Z-API (paga, mesmo risco).
//
// Regra de ouro deste arquivo: NUNCA lançar. Quem chama é o PushNotificationService, no meio
// de uma ação do jogador (marcar placar, abrir torneio). Provedor fora do ar não pode virar
// tela de erro na cara de quem clicou — devolve false e o log conta a história.
public class EvolutionApiService : IWhatsAppService
{
    private readonly HttpClient _httpClient;
    private readonly EvolutionSettings _settings;
    private readonly ILogger<EvolutionApiService> _logger;

    public EvolutionApiService(HttpClient httpClient, IOptions<EvolutionSettings> settings,
        ILogger<EvolutionApiService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public bool Configurado => _settings.Configurado;

    public async Task<bool> EnviarAsync(string? celular, string mensagem)
    {
        // Debug, não Warning: hoje TODA notificação passa por aqui, e no localhost/dev o canal
        // é desligado de propósito. Em Warning isso viraria ruído que esconde problema real.
        if (!_settings.Configurado)
        {
            _logger.LogDebug("Evolution API não configurada — mensagem não enviada.");
            return false;
        }

        var numero = NumeroInternacional(celular);
        if (numero == null)
        {
            _logger.LogWarning("Jogador sem celular válido — mensagem não enviada.");
            return false;
        }

        try
        {
            var url = $"{_settings.BaseUrl.TrimEnd('/')}/message/sendText/{_settings.Instancia}";

            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(new { number = numero, text = mensagem }),
            };
            request.Headers.Add("apikey", _settings.ApiKey);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var corpo = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Evolution API devolveu {Status} ao enviar pra {Numero}: {Corpo}",
                    response.StatusCode, numero, corpo);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao enviar WhatsApp pra {Numero}.", numero);
            return false;
        }
    }

    public async Task<EstadoDoCanal> ConsultarEstadoAsync()
    {
        if (!_settings.Configurado) return EstadoDoCanal.Desligado;

        try
        {
            var url = $"{_settings.BaseUrl.TrimEnd('/')}/instance/connectionState/{_settings.Instancia}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("apikey", _settings.ApiKey);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return EstadoDoCanal.SemResposta;

            var corpo = await response.Content.ReadAsStringAsync();

            // "open" é o único estado que envia. "connecting" parece esperança, mas foi
            // exatamente nele que a instância ficou presa por 17h em 03/08 — tratar como
            // conectado seria deixar o vigia dormir justo na hora em que ele é necessário.
            return corpo.Contains("\"state\":\"open\"", StringComparison.OrdinalIgnoreCase)
                ? EstadoDoCanal.Conectado
                : EstadoDoCanal.Desconectado;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não consegui consultar o estado do canal de WhatsApp.");
            return EstadoDoCanal.SemResposta;
        }
    }

    public async Task<bool> ReligarAsync()
    {
        if (!_settings.Configurado) return false;

        try
        {
            var url = $"{_settings.BaseUrl.TrimEnd('/')}/instance/restart/{_settings.Instancia}";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("apikey", _settings.ApiKey);

            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não consegui religar o canal de WhatsApp.");
            return false;
        }
    }

    // O WhatsApp identifica pelo número com código do país. O cadastro guarda só DDD+número
    // (é o que o formulário pede), então o 55 entra aqui — mas só quando ainda não veio, pra
    // um "5551..." colado de algum lugar não virar "555551...".
    public static string? NumeroInternacional(string? celular)
    {
        var digitos = new string((celular ?? "").Where(char.IsDigit).ToArray());

        if (digitos.Length is 10 or 11) return "55" + digitos;
        if (digitos.Length is 12 or 13 && digitos.StartsWith("55")) return digitos;

        return null;
    }
}
