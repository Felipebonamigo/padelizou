namespace Padelizou.Services;

// Cancelar um torneio: ele não vai acontecer.
//
// Não é o mesmo que FINALIZAR (o torneio aconteceu e teve campeão) nem que ESCONDER (o torneio
// existe, só não aparece na lista). Faltava a terceira: choveu, o clube perdeu a quadra, não
// deu gente. Sem isso o organizador só tinha saídas erradas — deixar o torneio pendurado
// "Inscrições Abertas" pra sempre, ou marcar como finalizado um torneio que ninguém jogou, o
// que ainda faria o sistema procurar campeão onde não houve jogo.
//
// ⚠️ O sistema NÃO estorna nada (decisão do Felipe, 07/08/2026). Ele mostra quem pagou e
// quanto, e a devolução é feita por fora. Mexer em dinheiro de verdade sozinho, sem volta, é
// grande demais pra sair junto de um botão de cancelar.
public static class CancelamentoDoTorneio
{
    public const string Status = "Cancelado";

    public static bool EstaCancelado(string? status) => status == Status;

    // Devolve o motivo da recusa, ou null quando dá pra cancelar.
    public static string? MotivoParaNaoCancelar(string? statusAtual)
    {
        if (EstaCancelado(statusAtual))
            return "Este torneio já está cancelado.";

        // Torneio que ACONTECEU não se cancela: tem campeão, tem ponto de ranking distribuído
        // e tem gente com a conquista no perfil. Apagar isso não é cancelar, é reescrever o
        // passado — e o caminho pra desfazer resultado é outro (reabrir a partida).
        if (statusAtual == "Finalizado")
            return "Torneio já finalizado não pode ser cancelado — ele aconteceu. "
                 + "Pra corrigir um resultado, reabra a partida pela Mesa de Controle.";

        return null;
    }

    // O aviso que vai pra cada inscrito. O motivo é opcional e entra na frase quando existe:
    // "cancelado" sem explicação é o tipo de recado que gera dez mensagens no WhatsApp do
    // organizador.
    public static string RecadoParaInscritos(string nomeDoTorneio, string? motivo) =>
        string.IsNullOrWhiteSpace(motivo)
            ? $"{nomeDoTorneio} foi cancelado pelo organizador."
            : $"{nomeDoTorneio} foi cancelado pelo organizador: {motivo.Trim()}";
}
