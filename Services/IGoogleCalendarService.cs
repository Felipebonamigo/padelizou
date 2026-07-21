using padelizou.Models;

namespace Padelizou.Services;

public interface IGoogleCalendarService
{
    string GetAuthorizationUrl(int professorId);
    Task ExchangeCodeAsync(int professorId, string code);
    Task<bool> EstaConectadoAsync(int professorId);

    // Retorna o EventId criado, ou null se o professor não tiver conectado a Google Agenda.
    Task<string?> CriarEventoAsync(Aula aula);
}
