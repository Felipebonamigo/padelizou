using padelizou.Models;

namespace Padelizou.Services;

public interface IGoogleCalendarService
{
    string GetAuthorizationUrl(int professorId);
    Task ExchangeCodeAsync(int professorId, string code);
    Task<bool> EstaConectadoAsync(int professorId);

    // Retorna o EventId criado, ou null se o professor não tiver conectado a Google Agenda.
    Task<string?> CriarEventoAsync(Aula aula);

    // Leva pro Google a aula que MUDOU de horário ou de local. Devolve o id do evento — o
    // mesmo de antes, ou um novo.
    //
    // Aceita aula sem `GoogleEventId` de propósito: aula marcada antes de o professor conectar
    // a Google Agenda nunca teve evento, e é justamente ela que ele descobre errada e corrige.
    // Sem isto, editar deixaria o horário velho no Google — que é onde ele olha antes de
    // marcar outra coisa.
    Task<string?> AtualizarEventoAsync(Aula aula);

    // Tira o evento da agenda do professor quando a aula é APAGADA aqui. Sem isto, a aula
    // some do Padelizou e continua ocupando o horário no Google — e é lá que o professor
    // olha antes de marcar outra coisa.
    Task RemoverEventoAsync(int professorId, string googleEventId);
}
