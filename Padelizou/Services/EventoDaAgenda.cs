namespace Padelizou.Services;

// UM EVENTO DO GOOGLE AGENDA, do jeito que a importação precisa dele (27/08/2026).
//
// Existe porque o `Event` cru da biblioteca do Google é grande demais e traiçoeiro demais pra
// circular pelo sistema: a hora vem em três formas (DateTimeRaw, DateTimeDateTimeOffset e a
// obsoleta DateTime), o dia-inteiro vem em OUTRO campo (`Start.Date`), e evento cancelado
// ainda aparece na listagem. Traduzir num lugar só é o que impede cada chamador de lembrar
// dessas pegadinhas por conta própria.
//
// `Inicio`/`Fim` são hora LOCAL sem fuso embutido (Kind=Unspecified) — o mesmo contrato de
// `Aula.DataHora`, e o espelho do cuidado que o ENVIO já toma no sentido contrário (ver o
// comentário sobre CS0618 em GoogleCalendarService.EventoDa): converter por DateTimeOffset
// usaria o fuso da MÁQUINA, que em produção é UTC, e as 18h do professor virariam 21h.
// Quem garante que o texto cru já vem no fuso de Brasília é o `TimeZone` pedido na listagem.
public record EventoDaAgenda(string Id, string Titulo, DateTime Inicio, DateTime Fim, string? LocalTexto)
{
    // O evento cru vira candidato a aula — ou null quando não tem como ser uma.
    //
    //   • DIA INTEIRO (`Start.Date` preenchido, sem hora) não é aula: aula tem hora.
    //   • CANCELADO ainda vem na listagem do Google, como casca; importar criaria a aula que
    //     o professor acabou de desmarcar.
    //   • SEM ID ou SEM HORA não tem como casar com `Aula.GoogleEventId` nem com `DataHora`.
    //
    // A recusa mora AQUI, na tradução, de propósito: nenhum chamador precisa lembrar delas.
    public static EventoDaAgenda? De(Google.Apis.Calendar.v3.Data.Event evento)
    {
        if (string.IsNullOrWhiteSpace(evento.Id)) return null;
        if (evento.Status == "cancelled") return null;
        if (string.IsNullOrWhiteSpace(evento.Start?.DateTimeRaw)
            || string.IsNullOrWhiteSpace(evento.End?.DateTimeRaw)) return null;   // dia inteiro cai aqui

        if (!DateTimeOffset.TryParse(evento.Start.DateTimeRaw, out var inicio)) return null;
        if (!DateTimeOffset.TryParse(evento.End.DateTimeRaw, out var fim)) return null;

        // `.DateTime` pega a hora COMO ESCRITA no texto, no fuso em que o Google a emitiu —
        // sem converter pro relógio da máquina. É o que faz 18:00-03:00 virar 18:00.
        var inicioLocal = inicio.DateTime;
        var fimLocal = fim.DateTime;

        // Evento de duração zero ou torta (fim antes do início) não vira aula — guarda de
        // canto, sem caminho conhecido que a alcance: o Google não cria evento assim pela UI.
        if (fimLocal <= inicioLocal) return null;

        return new EventoDaAgenda(
            evento.Id,
            string.IsNullOrWhiteSpace(evento.Summary) ? "(Sem título)" : evento.Summary.Trim(),
            inicioLocal,
            fimLocal,
            string.IsNullOrWhiteSpace(evento.Location) ? null : evento.Location.Trim());
    }
}
