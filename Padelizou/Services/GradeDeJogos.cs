namespace Padelizou.Services;

// Distribui os jogos de um torneio ao longo do relógio.
//
// O agendamento antigo somava a duração de um jogo por vez, a partir da data de início, sem
// nada mais: num torneio de 16 duplas em 4 grupos (24 jogos de 50 min) a grade marcava jogo
// às 3h40 da manhã. Dois erros somados —
//
//   1. ignorava as QUADRAS: com 3 quadras rodando em paralelo, 3 jogos começam no mesmo
//      horário, e o relógio só anda quando as quadras enchem;
//   2. ignorava o EXPEDIENTE: torneio não vira a noite. Ao passar do último horário do dia,
//      o que sobra vai pro dia seguinte.
//
// O padrão de um torneio de fim de semana (27/07/2026, descrito pelo Felipe):
//
//   sexta   — começa 18h (todo mundo trabalha de dia), últimos jogos 23h / 23h50
//   sábado  — começa 8h, vai até 23h / 23h50
//   domingo — começa 8h e vai até acabar, normalmente à tarde
//
// Daí duas coisas que a grade precisa saber e que uma hora só não expressa:
//
//   • o PRIMEIRO dia abre num horário e os DEMAIS em outro (18h × 8h);
//   • o corte do dia é a hora em que o último jogo COMEÇA, não em que termina — um jogo
//     das 23h50 varando a madrugada é normal, e ninguém quer calcular 23h50 + 50 min
//     pra preencher um campo.
public static class GradeDeJogos
{
    // Um horário por jogo, na ordem em que os jogos foram passados.
    //
    // inicio               — quando o torneio começa (data + hora da sexta, por exemplo).
    // ultimoInicioDoDia    — a hora limite pra COMEÇAR um jogo. Se for menor ou igual à
    //                        abertura dos dias seguintes, o dia é tratado como aberto (sem
    //                        virada), pra nunca entrar em laço infinito por configuração torta.
    // quadras              — quantos jogos rodam ao mesmo tempo.
    // aberturaDiasSeguintes— a que horas o dia seguinte recomeça. Omitida, repete a hora de
    //                        início — serve pro mata-mata, que entra emendado no meio do dia.
    public static IEnumerable<DateTime> Horarios(
        DateTime inicio, TimeSpan ultimoInicioDoDia, int quadras, int duracaoMinutos, int quantidade,
        TimeSpan? aberturaDiasSeguintes = null)
    {
        if (quantidade <= 0) yield break;

        quadras = Math.Max(quadras, 1);
        duracaoMinutos = duracaoMinutos > 0 ? duracaoMinutos : 50;

        var abertura = aberturaDiasSeguintes ?? inicio.TimeOfDay;
        bool viraODia = ultimoInicioDoDia > abertura;

        var aberturaDoDia = inicio;   // quando o dia corrente abriu
        var horario = inicio;
        int naQuadra = 0;

        for (int i = 0; i < quantidade; i++)
        {
            // Encheu as quadras: todo mundo joga junto, então o relógio anda uma partida.
            if (naQuadra == quadras)
            {
                horario = horario.AddMinutes(duracaoMinutos);
                naQuadra = 0;

                // Comparação em data cheia, não em hora do dia: o jogo das 23h50 empurra o
                // próximo pra 0h40, que já é OUTRA data — comparar só TimeOfDay diria
                // "0h40 é cedo, cabe" e marcaria jogo na madrugada.
                if (viraODia && horario > aberturaDoDia.Date.Add(ultimoInicioDoDia))
                {
                    aberturaDoDia = aberturaDoDia.Date.AddDays(1).Add(abertura);
                    horario = aberturaDoDia;
                }
            }

            yield return horario;
            naQuadra++;
        }
    }

    // Quantas rodadas cabem num dia que abre às `abertura`.
    // null = dia sem hora pra acabar.
    public static int? RodadasPorDia(TimeSpan abertura, TimeSpan ultimoInicioDoDia, int duracaoMinutos)
    {
        if (ultimoInicioDoDia <= abertura) return null;

        duracaoMinutos = duracaoMinutos > 0 ? duracaoMinutos : 50;
        return (int)((ultimoInicioDoDia - abertura).TotalMinutes / duracaoMinutos) + 1;
    }

    // A que horas começa de fato o ÚLTIMO jogo do dia. Não é o limite digitado: o limite é
    // um teto, e o jogo só começa nos horários que a cadência alcança — com jogos de 1h a
    // partir das 18h, o teto de 23h50 vira 23h, porque 23h50 não é múltiplo da cadência.
    // null quando o dia é aberto.
    public static TimeSpan? UltimoInicioDoDia(TimeSpan abertura, TimeSpan ultimoInicioDoDia, int duracaoMinutos)
    {
        var rodadas = RodadasPorDia(abertura, ultimoInicioDoDia, duracaoMinutos);
        if (rodadas == null) return null;

        duracaoMinutos = duracaoMinutos > 0 ? duracaoMinutos : 50;
        return abertura + TimeSpan.FromMinutes((rodadas.Value - 1) * duracaoMinutos);
    }

    // Quando a próxima partida pode começar, dado o último jogo já marcado — usado pra
    // encaixar o mata-mata logo depois da fase de grupos, em vez de recomeçar do zero em
    // cima dela.
    public static DateTime DepoisDe(DateTime ultimoJogo, TimeSpan ultimoInicioDoDia,
        TimeSpan aberturaDiasSeguintes, int duracaoMinutos)
    {
        duracaoMinutos = duracaoMinutos > 0 ? duracaoMinutos : 50;
        var proximo = ultimoJogo.AddMinutes(duracaoMinutos);

        if (ultimoInicioDoDia > aberturaDiasSeguintes && proximo > ultimoJogo.Date.Add(ultimoInicioDoDia))
        {
            proximo = ultimoJogo.Date.AddDays(1).Add(aberturaDiasSeguintes);
        }

        return proximo;
    }
}
