using padelizou.Models;

namespace Padelizou.Services;

public interface IGoogleCalendarService
{
    string GetAuthorizationUrl(int professorId);
    Task ExchangeCodeAsync(int professorId, string code);
    Task<bool> EstaConectadoAsync(int professorId);

    // Retorna o EventId criado, ou null se o professor não tiver conectado a Google Agenda.
    Task<string?> CriarEventoAsync(Aula aula);

    // Tira o evento da agenda do professor quando a aula é APAGADA aqui. Sem isto, a aula
    // some do Padelizou e continua ocupando o horário no Google — e é lá que o professor
    // olha antes de marcar outra coisa.
    Task RemoverEventoAsync(int professorId, string googleEventId);
}
