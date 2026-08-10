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
        var calendarService = await AbrirAgendaAsync(aula.ProfessorId);
        if (calendarService == null) return null;

        try
        {
            var request = calendarService.Events.Insert(EventoDa(aula), "primary");
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

    public async Task<string?> AtualizarEventoAsync(Aula aula)
    {
        // Aula que nunca teve evento (marcada antes de conectar a agenda, ou de um dia em que
        // a chamada falhou) entra aqui pelo caminho de criação. É o mesmo botão pro professor:
        // ele corrigiu a aula e espera que o Google fique certo, sem precisar saber se o evento
        // já existia.
        if (string.IsNullOrWhiteSpace(aula.GoogleEventId))
        {
            return await CriarEventoAsync(aula);
        }

        var calendarService = await AbrirAgendaAsync(aula.ProfessorId);
        if (calendarService == null) return aula.GoogleEventId;

        try
        {
            var request = calendarService.Events.Patch(EventoDa(aula), "primary", aula.GoogleEventId);
            // O aluno é convidado do evento: mudança de horário só serve se chegar até ele.
            request.SendUpdates = EventsResource.PatchRequest.SendUpdatesEnum.All;
            var atualizado = await request.ExecuteAsync();
            return atualizado.Id;
        }
        catch (Google.GoogleApiException ex) when (
            ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound ||
            ex.HttpStatusCode == System.Net.HttpStatusCode.Gone)
        {
            // O professor apagou o evento na mão lá no Google. Insistir no id morto deixaria a
            // aula fora da agenda pra sempre; criar de novo é o que ele espera ao corrigir.
            _logger.LogInformation(
                "Evento {EventoId} não existe mais na agenda do professor {ProfessorId}; criando outro.",
                aula.GoogleEventId, aula.ProfessorId);
            return await CriarEventoAsync(aula);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao atualizar o evento {EventoId} na Google Agenda do professor {ProfessorId}",
                aula.GoogleEventId, aula.ProfessorId);
            // Devolve o id que já existia: perder a referência aqui faria a próxima edição
            // criar um evento DUPLICADO em vez de corrigir este.
            return aula.GoogleEventId;
        }
    }

    // O evento como o Google o enxerga. Um lugar só, porque criar e atualizar precisam
    // montar exatamente a mesma coisa — se divergirem, editar a aula "conserta" o horário e
    // deixa o local velho.
    private static Event EventoDa(Aula aula)
    {
        return new Event
        {
            Summary = $"Aula de Padel - {aula.Aluno?.Nome ?? aula.NomeAlunoAvulso ?? "Aluno"}",
            // Cai no nome quando não há endereço: o Google mostra o campo vazio como "sem
            // local", e o nome do clube já leva o aluno até lá.
            Location = aula.LocalAula.NomeComEndereco,
            // `DateTime` está marcado como obsoleto pela biblioteca do Google, que recomenda
            // `DateTimeDateTimeOffset`. Ficamos no antigo DE PROPÓSITO: `aula.DataHora` é hora
            // local sem fuso embutido (Kind=Unspecified), e converter pra DateTimeOffset usa o
            // fuso da MÁQUINA — que em produção é UTC. As 14h do professor virariam 11h na agenda
            // do aluno. Do jeito atual, mandamos a hora e o `TimeZone` separados e quem resolve é
            // o Google. Só trocar junto com um teste que prove o horário na agenda de verdade.
#pragma warning disable CS0618
            Start = new EventDateTime { DateTime = aula.DataHora, TimeZone = "America/Sao_Paulo" },
            End = new EventDateTime { DateTime = DuracaoDaAula.Fim(aula), TimeZone = "America/Sao_Paulo" },
#pragma warning restore CS0618
            Attendees = string.IsNullOrEmpty(aula.Aluno?.Email)
                ? null
                : new List<EventAttendee> { new EventAttendee { Email = aula.Aluno.Email, DisplayName = aula.Aluno.Nome } }
        };
    }

    // Abre a agenda do professor, ou devolve null quando ele não conectou a conta. Separado
    // porque os três caminhos (criar, atualizar, remover) precisam do mesmo token.
    private async Task<CalendarService?> AbrirAgendaAsync(int professorId)
    {
        var token = await _flow.LoadTokenAsync(professorId.ToString(), CancellationToken.None);
        if (token == null || string.IsNullOrEmpty(token.RefreshToken)) return null;

        var credential = new UserCredential(_flow, professorId.ToString(), token);
        return new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName
        });
    }

    public async Task RemoverEventoAsync(int professorId, string googleEventId)
    {
        if (string.IsNullOrWhiteSpace(googleEventId)) return;

        var token = await _flow.LoadTokenAsync(professorId.ToString(), CancellationToken.None);
        if (token == null || string.IsNullOrEmpty(token.RefreshToken)) return;

        var credential = new UserCredential(_flow, professorId.ToString(), token);
        var calendarService = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName
        });

        try
        {
            var request = calendarService.Events.Delete("primary", googleEventId);
            // O aluno virou convidado do evento quando ele foi criado; sem avisar, ele fica
            // com a aula na agenda dele pra sempre.
            request.SendUpdates = EventsResource.DeleteRequest.SendUpdatesEnum.All;
            await request.ExecuteAsync();
        }
        catch (Exception ex)
        {
            // Falhar aqui não pode derrubar a exclusão: a aula já foi apagada no Padelizou, e
            // um evento sobrando no Google é bem menos grave do que a tela dar erro.
            _logger.LogWarning(ex, "Falha ao remover evento {EventoId} da Google Agenda do professor {ProfessorId}", googleEventId, professorId);
        }
    }
}
