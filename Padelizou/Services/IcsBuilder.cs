using System.Text;

namespace Padelizou.Services;

public record IcsEvent(
    string Uid,
    DateTime Inicio,
    DateTime? Fim,
    bool DiaInteiro,
    string Resumo,
    string? Local,
    string? Descricao);

public static class IcsBuilder
{
    // Brasil não tem horário de verão desde 2019 e é sempre UTC-3 — a conversão pra UTC é uma
    // soma fixa de 3 horas, sem precisar de VTIMEZONE nem tabela de fusos horários.
    private const int OffsetBrasiliaParaUtcHoras = 3;

    public static string BuildCalendar(string nomeCalendario, IEnumerable<IcsEvent> eventos)
    {
        var sb = new StringBuilder();
        void Linha(string conteudo) => sb.Append(Fold(conteudo)).Append("\r\n");

        Linha("BEGIN:VCALENDAR");
        Linha("VERSION:2.0");
        Linha("PRODID:-//Padelizou//Agenda Feed//PT-BR");
        Linha("CALSCALE:GREGORIAN");
        Linha("METHOD:PUBLISH");
        Linha($"X-WR-CALNAME:{EscapeText(nomeCalendario)}");

        foreach (var evento in eventos)
        {
            Linha("BEGIN:VEVENT");
            Linha($"UID:{evento.Uid}");
            Linha($"DTSTAMP:{FormatUtcNow()}");

            if (evento.DiaInteiro)
            {
                Linha($"DTSTART;VALUE=DATE:{evento.Inicio:yyyyMMdd}");
                Linha($"DTEND;VALUE=DATE:{evento.Inicio.AddDays(1):yyyyMMdd}");
            }
            else
            {
                Linha($"DTSTART:{FormatUtcFromBrasilia(evento.Inicio)}");
                Linha($"DTEND:{FormatUtcFromBrasilia(evento.Fim ?? evento.Inicio.AddHours(1))}");
            }

            Linha($"SUMMARY:{EscapeText(evento.Resumo)}");
            if (!string.IsNullOrWhiteSpace(evento.Local))
                Linha($"LOCATION:{EscapeText(evento.Local)}");
            if (!string.IsNullOrWhiteSpace(evento.Descricao))
                Linha($"DESCRIPTION:{EscapeText(evento.Descricao)}");

            Linha("END:VEVENT");
        }

        Linha("END:VCALENDAR");
        return sb.ToString();
    }

    private static string FormatUtcFromBrasilia(DateTime horarioBrasilia)
        => horarioBrasilia.AddHours(OffsetBrasiliaParaUtcHoras).ToString("yyyyMMddTHHmmss") + "Z";

    private static string FormatUtcNow()
        => DateTime.UtcNow.ToString("yyyyMMddTHHmmss") + "Z";

    // RFC 5545 §3.3.11 — escapa backslash, ponto e vírgula, vírgula e quebras de linha em TEXT.
    private static string EscapeText(string valor) => valor
        .Replace("\\", "\\\\")
        .Replace(";", "\\;")
        .Replace(",", "\\,")
        .Replace("\r\n", "\\n")
        .Replace("\n", "\\n");

    // RFC 5545 §3.1 — quebra linhas com mais de 75 octetos; continuação começa com um espaço.
    // Opera em bytes UTF-8 (não em char/Length do .NET) e nunca corta no meio de um caractere
    // multibyte — essencial aqui porque nomes/endereços em português (ç, ã, é...) são a regra,
    // não a exceção.
    private static string Fold(string linha)
    {
        var bytes = Encoding.UTF8.GetBytes(linha);
        if (bytes.Length <= 75) return linha;

        var sb = new StringBuilder();
        var pos = 0;
        var primeira = true;
        while (pos < bytes.Length)
        {
            var limite = primeira ? 75 : 74; // linha de continuação "gasta" 1 octeto com o espaço inicial
            var tamanho = Math.Min(limite, bytes.Length - pos);

            while (tamanho > 0 && pos + tamanho < bytes.Length && (bytes[pos + tamanho] & 0xC0) == 0x80)
                tamanho--; // não cortar um caractere UTF-8 multibyte ao meio

            if (!primeira) sb.Append("\r\n ");
            sb.Append(Encoding.UTF8.GetString(bytes, pos, tamanho));
            pos += tamanho;
            primeira = false;
        }
        return sb.ToString();
    }
}
