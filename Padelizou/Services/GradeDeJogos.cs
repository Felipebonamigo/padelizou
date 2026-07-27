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
    public static IEnumerable<DateTime> Horarios(
        DateTime inicio, TimeSpan horaFimDoDia, int quadras, int duracaoMinutos, int quantidade)
    {
        if (quantidade <= 0) yield break;

        quadras = Math.Max(quadras, 1);
        duracaoMinutos = duracaoMinutos > 0 ? duracaoMinutos : 50;

        var horaAbertura = inicio.TimeOfDay;
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

    // Quando a última partida de uma grade termina — usado pra encaixar o mata-mata logo
    // depois da fase de grupos, em vez de recomeçar do zero em cima dela.
    public static DateTime DepoisDe(DateTime ultimoJogo, TimeSpan horaFimDoDia, TimeSpan horaAbertura, int duracaoMinutos)
    {
        var proximo = ultimoJogo.AddMinutes(duracaoMinutos > 0 ? duracaoMinutos : 50);

        if (horaFimDoDia > horaAbertura && proximo.TimeOfDay >= horaFimDoDia)
        {
            proximo = proximo.Date.AddDays(1).Add(horaAbertura);
        }

        return proximo;
    }
}
