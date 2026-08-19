using padelizou.Models;
using Padelizou.Models;

namespace Padelizou.Services;

// Regras de cancelamento e falta. Ficam aqui (e não no controller) porque valem em três
// lugares: quando o aluno desmarca, quando o professor desmarca e quando o professor
// fecha a presença depois da aula.
public static class PoliticaAula
{
    // Status que o sistema usa. Antes existiam só "Pendente" e "Confirmada" espalhados
    // como texto solto; centralizar evita a mesma armadilha das duas grafias de fase.
    public const string Pendente = "Pendente";
    public const string Confirmada = "Confirmada";
    public const string Realizada = "Realizada";
    public const string Cancelada = "Cancelada";
    public const string Recusada = "Recusada";
    public const string Faltou = "Faltou";

    // O aluno não vem NESTE dia, a aula é cobrada assim mesmo e ele tem direito a repô-la
    // outro dia. É o caso do mensalista: quem paga o mês não perde a aula por faltar, e o
    // professor não perde o dinheiro por ele ter faltado — ver Services/Reposicao.
    //
    // Guardado COM ESPAÇO porque o status é impresso cru na agenda e no card do aluno:
    // "ARecuperar" apareceria assim mesmo, na tela, pro professor e pro aluno.
    public const string ARecuperar = "A recuperar";

    // "Ativa" é a aula que ainda ocupa o horário do professor. Ficar de fora é justamente o
    // que faz "vai recuperar" LIBERAR a vaga do dia — o professor encaixa outro aluno ali,
    // que é o pedido inteiro. Também é o que tira a aula da lista de próximas do aluno: ela
    // não vai acontecer naquele dia, e prometer que vai seria mentira na tela dele.
    public static bool ContaComoAtiva(string? status) =>
        status == Pendente || status == Confirmada;

    // A aula AINDA VAI ACONTECER: continua de pé e não terminou. É o corte entre "aula
    // marcada" e "histórico", e mora aqui porque vale em dois lugares que não podem
    // discordar — a consulta que separa as duas listas e o card que desenha cada aula.
    //
    // ⚠️ Compara o FIM, não o início: uma aula de 1h30 que começou há 30 minutos ainda é a
    // próxima da pessoa, e mandá-la pro histórico no minuto seguinte ao início faria a aula
    // sumir da tela com o aluno ainda na quadra.
    public static bool AindaVaiAcontecer(Aula aula, DateTime agora) =>
        ContaComoAtiva(aula.Status) && aula.DataHora.AddMinutes(aula.DuracaoMinutos) > agora;

    // Cancelamento dentro do prazo que o professor definiu? Prazo 0 = sempre no prazo.
    public static bool DentroDoPrazo(Jogador professor, DateTime dataHoraAula, DateTime agora)
    {
        if (professor.HorasMinimasCancelamento <= 0) return true;
        return (dataHoraAula - agora).TotalHours >= professor.HorasMinimasCancelamento;
    }

    // Quantas horas antes da aula estamos. Negativo = a aula já passou.
    public static int HorasAntes(DateTime dataHoraAula, DateTime agora) =>
        (int)Math.Floor((dataHoraAula - agora).TotalHours);

    // Cancelar gera cobrança? Só quando o professor cobra falta E o aviso veio fora do
    // prazo. Professor que cancela nunca cobra — a regra existe pra proteger a agenda
    // dele, não pra penalizar o aluno por decisão dele.
    public static bool DeveCobrar(Jogador professor, Aula aula, string canceladaPor, DateTime agora)
    {
        if (canceladaPor != "Aluno") return false;
        if (!professor.CobraFaltaSemAviso) return false;
        return !DentroDoPrazo(professor, aula.DataHora, agora);
    }

    // Texto que o aluno lê antes de confirmar. Usa o texto livre do professor quando ele
    // escreveu um; senão monta a frase a partir dos números.
    public static string DescreverPolitica(Jogador professor)
    {
        if (!string.IsNullOrWhiteSpace(professor.PoliticaCancelamentoTexto))
            return professor.PoliticaCancelamentoTexto!.Trim();

        if (professor.HorasMinimasCancelamento <= 0)
            return "Você pode desmarcar a qualquer momento, sem cobrança.";

        var prazo = professor.HorasMinimasCancelamento == 24
            ? "com 24h de antecedência"
            : $"com pelo menos {professor.HorasMinimasCancelamento}h de antecedência";

        return professor.CobraFaltaSemAviso
            ? $"Desmarque {prazo}. Faltas sem aviso ou avisos fora do prazo podem ser cobrados."
            : $"Desmarque {prazo} sempre que puder, pra liberar o horário pra outro aluno.";
    }
}
