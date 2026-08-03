using padelizou.Models;

namespace Padelizou.Services;

// Apagar uma aula de vez, que é diferente de cancelar. Cancelar é um fato do mundo — o aluno
// desmarcou, e isso fica registrado, conta pra política de 24h e aparece no financeiro.
// Apagar é dizer que a aula nunca existiu: o professor digitou o dia errado, criou a série
// duas vezes, lançou no aluno errado.
//
// Sem isto o erro ficava na agenda pra sempre, porque "Cancelada" continua na tela — e uma
// aula que nunca deveria ter existido não é uma aula cancelada.
public static class ExclusaoDeAula
{
    // O aluno com conta precisa saber que a aula sumiu da agenda dele — mas só quando havia
    // o que sumir. Aula já cancelada ou recusada ele nem contava mais; aula que já passou,
    // avisar só confunde ("sua aula de terça foi apagada" três semanas depois).
    public static bool PrecisaAvisarAluno(Aula aula, DateTime agora) =>
        aula.AlunoId != null
        && PoliticaAula.ContaComoAtiva(aula.Status)
        && aula.DataHora > agora;

    // O que o professor lê antes de confirmar. A frase muda com o que está em jogo: apagar
    // um lançamento errado de ontem não é o mesmo que desmarcar alguém que está esperando.
    public static string Confirmacao(Aula aula, DateTime agora) =>
        PrecisaAvisarAluno(aula, agora)
            ? "Apagar esta aula? Ela some da sua agenda e da do aluno, e ele será avisado. "
            + "Se a intenção é registrar que a aula foi desmarcada, use Cancelar — o "
            + "cancelamento fica no histórico e conta pra sua política."
            : "Apagar esta aula? Ela some da agenda e do histórico, sem deixar registro.";
}
