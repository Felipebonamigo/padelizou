using Padelizou.Models;

namespace Padelizou.Services;

// O TORNEIO "POR ORDEM DE LIBERAÇÃO": a Mesa chama, a quadra é decidida na hora — mas o
// horário existe.
//
// 🗣️ Felipe, 09/09/2026, olhando os grupos do torneio do Er: "mesmo que seja por ordem os
// jogos, tem q ter o horario dos jogos; o que realmente muda é a quadra — o horário do jogo,
// teoricamente, é pré-definido, para as pessoas se organizarem".
//
// ⚠️ ISSO REVISA UMA DECISÃO ESCRITA, e vale registrar o que exatamente mudou. O modo nasceu
// dizendo que "horário inventado que ninguém cumpre é pior que horário nenhum" — e a
// preocupação continua de pé. O que mudou foi o ALVO dela:
//
//   • o que atrasa e não se cumpre é a QUADRA — qual delas vaga primeiro depende de um jogo de
//     4 games com desempate, e ninguém prevê isso. Ela continua em aberto.
//   • a HORA sai da mesma conta de sempre (quantos jogos, quantas quadras, quanto dura cada um)
//     e responde a pergunta que o jogador realmente faz: "chego às 8h ou às 15h?". Escondê-la
//     não tornava o dia mais previsível — só deixava 63 duplas sem saber quando aparecer.
//
// Então o modo continua sendo "a Mesa chama". O que ele deixou de fazer foi esconder a hora.
public static class OrdemDeLiberacao
{
    public static bool Vale(Torneio torneio) => torneio.SemHorarioPrevisto;

    // Tira a QUADRA dos jogos que a grade acabou de montar, mantendo o horário.
    //
    // ⚠️ POR QUE DEPOIS DA GRADE, E NÃO EM VEZ DELA: é o encaixe que garante que ninguém seja
    // chamado pra dois jogos no mesmo horário, e ele só sabe disso porque distribui as partidas
    // ENTRE as quadras. Calcular hora sem passar por lá daria um relógio que põe a mesma pessoa
    // em dois lugares — e aí sim seria horário que ninguém cumpre. A quadra é apagada no fim,
    // depois de ter feito o trabalho dela.
    public static void ApagarAsQuadras(Torneio torneio, IEnumerable<Partida> jogos)
    {
        if (!Vale(torneio)) return;

        foreach (var jogo in jogos) jogo.NomeQuadra = null;
    }

    // GRAVA EM QUE CLUBE O MOTOR PÔS CADA JOGO — antes de a quadra ser apagada, e em todo
    // torneio, por ordem ou não (10/09/2026).
    //
    // 🗣️ Felipe: *"tem q o sistema mesmo definir o que irão para o 'radar' no sabado de manhã,
    // nao precisa ter a quadra definida, mas o clube sempre tem q estar definido"*. O sistema já
    // definia — a janela do Radar e o "cede a quadra de casa" moram no Encaixar — e o
    // `ApagarAsQuadras` logo acima jogava a definição fora junto com a quadra. Este passo é a
    // única coisa entre o Encaixar e o apagar, e por isso tem que ser chamado ANTES dele.
    //
    // Quadra fora do cadastro (texto solto) ou torneio sem quadra cadastrada caem no clube do
    // torneio: é o único prédio que existe nesses casos, e nulo aqui seria a tela muda de novo.
    public static void CarimbarOClube(Torneio torneio, IEnumerable<Partida> jogos, SedesDoTorneio sedes)
    {
        foreach (var jogo in jogos)
        {
            if (jogo.HorarioPrevisto == null && string.IsNullOrWhiteSpace(jogo.NomeQuadra)) continue;
            jogo.ClubeId = sedes.ClubeDaQuadra(jogo.NomeQuadra) ?? torneio.ClubeId;
        }
    }
}
