namespace Padelizou.Services;

// Distribui os jogos de um torneio ao longo do relógio.
//
// O agendamento antigo somava a duração de um jogo por vez, a partir da data de início, sem
// nada mais: num torneio de 16 duplas em 4 grupos (24 jogos de 50 min) a grade marcava jogo
// às 3h40 da manhã. Dois erros somados —
//
//   1. ignorava as QUADRAS: com 3 quadras rodando em paralelo, 3 jogos começam no mesmo
//      horário, e o relógio só anda quando as quadras enchem;
//   2. ignorava o EXPEDIENTE: torneio não vira a noite. Ao bater o horário de encerramento
//      do dia, o que sobra vai pro dia seguinte, no horário de abertura.
public static class GradeDeJogos
{
    // Um horário por jogo, na ordem em que os jogos foram passados.
    //
    // inicio        — quando o torneio começa (data + hora).
    // horaFimDoDia  — a que horas para de encaixar jogo. Se for menor ou igual à hora de
    //                 início, o dia é tratado como aberto (sem virada), pra nunca entrar
    //                 em laço infinito por configuração torta.
    // quadras       — quantos jogos rodam ao mesmo tempo.
    // abertura      — a que horas o dia SEGUINTE recomeça. Só precisa ser informada quando a
    //                 grade não começa na abertura do torneio: o mata-mata entra emendado na
    //                 fase de grupos (14h40, digamos) e, se virar o dia, tem que recomeçar às
    //                 8h como todo mundo, não às 14h40.
    public static IEnumerable<DateTime> Horarios(
        DateTime inicio, TimeSpan horaFimDoDia, int quadras, int duracaoMinutos, int quantidade,
        TimeSpan? abertura = null)
    {
        if (quantidade <= 0) yield break;

        quadras = Math.Max(quadras, 1);
        duracaoMinutos = duracaoMinutos > 0 ? duracaoMinutos : 50;

        var horaAbertura = abertura ?? inicio.TimeOfDay;
        // Expediente inválido (fim antes do início, ou igual) = dia sem limite.
        bool viraODia = horaFimDoDia > horaAbertura;

        var horario = inicio;
        int naQuadra = 0;

        for (int i = 0; i < quantidade; i++)
        {
            // Encheu as quadras: todo mundo joga junto, então o relógio anda uma partida.
            if (naQuadra == quadras)
            {
                horario = horario.AddMinutes(duracaoMinutos);
                naQuadra = 0;

                // O jogo tem que caber inteiro dentro do expediente.
                if (viraODia && horario.TimeOfDay.Add(TimeSpan.FromMinutes(duracaoMinutos)) > horaFimDoDia)
                {
                    horario = horario.Date.AddDays(1).Add(horaAbertura);
                }
            }

            yield return horario;
            naQuadra++;
        }
    }

    // Quantas rodadas cabem num dia de expediente.
    // null = dia sem hora pra acabar; 0 = nem um jogo cabe na janela escolhida.
    public static int? RodadasPorDia(TimeSpan horaAbertura, TimeSpan horaFimDoDia, int duracaoMinutos)
    {
        if (horaFimDoDia <= horaAbertura) return null;

        duracaoMinutos = duracaoMinutos > 0 ? duracaoMinutos : 50;
        return (int)((horaFimDoDia - horaAbertura).TotalMinutes / duracaoMinutos);
    }

    // A que horas começa o ÚLTIMO jogo do dia — o jogo tem que caber inteiro dentro do
    // expediente, então às 23h com jogo de 50 min o último começa 21h30, não 22h20.
    // null quando o dia é aberto ou quando nem um jogo cabe.
    public static TimeSpan? UltimoInicioDoDia(TimeSpan horaAbertura, TimeSpan horaFimDoDia, int duracaoMinutos)
    {
        var rodadas = RodadasPorDia(horaAbertura, horaFimDoDia, duracaoMinutos);
        if (rodadas is null or 0) return null;

        duracaoMinutos = duracaoMinutos > 0 ? duracaoMinutos : 50;
        return horaAbertura + TimeSpan.FromMinutes((rodadas.Value - 1) * duracaoMinutos);
    }

    // Quando a próxima partida pode começar, dado o último jogo já marcado — usado pra
    // encaixar o mata-mata logo depois da fase de grupos, em vez de recomeçar do zero em
    // cima dela.
    public static DateTime DepoisDe(DateTime ultimoJogo, TimeSpan horaFimDoDia, TimeSpan horaAbertura, int duracaoMinutos)
    {
        duracaoMinutos = duracaoMinutos > 0 ? duracaoMinutos : 50;
        var proximo = ultimoJogo.AddMinutes(duracaoMinutos);

        // Mesma regra da grade: o jogo tem que caber INTEIRO no expediente.
        if (horaFimDoDia > horaAbertura
            && proximo.TimeOfDay.Add(TimeSpan.FromMinutes(duracaoMinutos)) > horaFimDoDia)
        {
            proximo = proximo.Date.AddDays(1).Add(horaAbertura);
        }

        return proximo;
    }
}
