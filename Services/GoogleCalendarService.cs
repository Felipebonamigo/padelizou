using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Options;
using padelizou.Models;

namespace Padelizou.Services;

public class GoogleCalendarService : IGoogleCalendarService
{
    private const string ApplicationName = "Padelizou";
    private static readonly string[] Scopes = { CalendarService.Scope.CalendarEvents };

    private readonly GoogleCalendarSettings _settings;
    private readonly GoogleAuthorizationCodeFlow _flow;
    private readonly ILogger<GoogleCalendarService> _logger;

    public GoogleCalendarService(IOptions<GoogleCalendarSettings> settings, IWebHostEnvironment env, ILogger<GoogleCalendarService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        var tokensPath = Path.Combine(env.ContentRootPath, "App_Data", "GoogleTokens");

        _flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = _settings.ClientId, ClientSecret = _settings.ClientSecret },
            Scopes = Scopes,
            DataStore = new FileDataStore(tokensPath, true)
        });
    }

    public string GetAuthorizationUrl(int professorId)
    {
        var request = _flow.CreateAuthorizationCodeRequest(_settings.RedirectUri);
        request.State = professorId.ToString();
        return request.Build().ToString();
    }

    public async Task ExchangeCodeAsync(int professorId, string code)
    {
        await _flow.ExchangeCodeForTokenAsync(professorId.ToString(), code, _settings.RedirectUri, CancellationToken.None);
    }

    public async Task<bool> EstaConectadoAsync(int professorId)
    {
        var token = await _flow.LoadTokenAsync(professorId.ToString(), CancellationToken.None);
        return token != null && !string.IsNullOrEmpty(token.RefreshToken);
    }

    public async Task<string?> CriarEventoAsync(Aula aula)
    {
        var token = await _flow.LoadTokenAsync(aula.ProfessorId.ToString(), CancellationToken.None);
        if (token == null || string.IsNullOrEmpty(token.RefreshToken))
        {
            return null;
        }

        var credential = new UserCredential(_flow, aula.ProfessorId.ToString(), token);
        var calendarService = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName
        });

        var duracaoMinutos = 60;
        var evento = new Event
        {
            Summary = $"Aula de Padel - {aula.Aluno.Nome}",
            Location = aula.LocalAula.Endereco,
            Start = new EventDateTime { DateTime = aula.DataHora, TimeZone = "America/Sao_Paulo" },
            End = new EventDateTime { DateTime = aula.DataHora.AddMinutes(duracaoMinutos), TimeZone = "America/Sao_Paulo" },
            Attendees = string.IsNullOrEmpty(aula.Aluno.Email)
                ? null
                : new List<EventAttendee> { new EventAttendee { Email = aula.Aluno.Email, DisplayName = aula.Aluno.Nome } }
        };

        try
        {
            var request = calendarService.Events.Insert(evento, "primary");
            request.SendUpdates = EventsResource.InsertRequest.SendUpdatesEnum.All;
            var criado = await request.ExecuteAsync();
            return criado.Id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao criar evento na Google Agenda para o professor {ProfessorId}", aula.ProfessorId);
            return null;
        }
    }
}
