using padelizou.Models;

namespace Padelizou.Services;

// Quanto tempo dura uma aula — e, por causa disso, quando ela TERMINA.
//
// POR QUE ISTO EXISTE: até 10/08/2026 a duração era uma constante 60 escondida dentro do
// GoogleCalendarService, e a agenda tratava toda aula como um ponto no tempo. Aula de 1h30 e
// de 2h são o normal em padel (a quadra é alugada por bloco), e sem duração de verdade:
//   - o evento na Google Agenda do professor terminava no meio da aula;
//   - a trava de conflito comparava só o INÍCIO, então 17:00–19:00 e 18:00–19:00 conviviam.
//
// A lista de opções é fechada de propósito: o campo vem do formulário, e minuto livre viraria
// aula de 7 minutos ou de 40 horas por dedo trocado (ou por formulário adulterado).
public static class DuracaoDaAula
{
    public const int Padrao = 60;

    public static readonly int[] Opcoes = { 30, 45, 60, 90, 120 };

    public static int Valida(int? minutos) =>
        minutos is int m && Opcoes.Contains(m) ? m : Padrao;

    // "1h", "1h30", "45 min" — como o professor fala, não "90 minutos".
    public static string Rotulo(int minutos)
    {
        var horas = minutos / 60;
        var resto = minutos % 60;

        if (horas == 0) return $"{resto} min";
        if (resto == 0) return $"{horas}h";
        return $"{horas}h{resto:00}";
    }

    public static DateTime Fim(DateTime inicio, int duracaoMinutos) =>
        inicio.AddMinutes(Valida(duracaoMinutos));

    public static DateTime Fim(Aula aula) => Fim(aula.DataHora, aula.DuracaoMinutos);

    // "17:00 às 19:00" — o que a agenda mostra quando a aula não é de uma hora.
    public static string Faixa(DateTime inicio, int duracaoMinutos) =>
        $"{inicio:HH\\:mm} às {Fim(inicio, duracaoMinutos):HH\\:mm}";

    // Duas aulas se atropelam? Fim exclusivo dos dois lados: 17–18 e 18–19 são coladas, não
    // conflitantes — é assim que a quadra funciona.
    public static bool Conflita(DateTime inicioA, int duracaoA, DateTime inicioB, int duracaoB) =>
        inicioA < Fim(inicioB, duracaoB) && Fim(inicioA, duracaoA) > inicioB;
}
