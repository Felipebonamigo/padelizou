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

    // Os eventos do calendário do professor no período — o sentido INVERSO de tudo acima, e
    // o primeiro método que LÊ do Google (27/08/2026, importação de aulas). O escopo OAuth de
    // sempre (`calendar.events`) já cobre leitura: nenhum professor precisa reconectar.
    //
    // Devolve null quando o professor não conectou OU quando a chamada falhou — o chamador
    // TEM que distinguir null de lista vazia e dizer isso na tela: o token morto por refresh
    // recusado é a falha mais muda deste sistema, e uma lista vazia no lugar dela viraria
    // "sua agenda não tem nada" pra quem tem a semana lotada.
    //
    // Dia-inteiro e cancelado já vêm filtrados (ver EventoDaAgenda.De); recorrência vem
    // EXPANDIDA — cada aula fixa semanal aparece como um evento por semana, que é o que a
    // importação precisa pra criar uma Aula por sessão.
    Task<List<EventoDaAgenda>?> ListarEventosAsync(int professorId, DateTime de, DateTime ate);
}
