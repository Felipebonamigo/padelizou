using Padelizou.Models;

namespace Padelizou.Services;

// "Esta pessoa consegue receber o dinheiro pelo site?"
//
// A pergunta é feita em três lugares — torneio, aula e aluguel de quadra — e até aqui os
// três davam a MESMA resposta muda: PodeCobrar devolvia false, a cobrança simplesmente não
// nascia e ninguém era avisado. O organizador escolhia "pelo site", publicava, recebia as
// inscrições e só descobria ao ir procurar o dinheiro.
//
// Está em classe própria, sem depender do gateway, porque a resposta útil não é "não pode":
// é O QUE FALTA fazer, escrito pra quem organiza padel e vai ler isso numa tela.
public static class ContaDeRecebimento
{
    // Ligou a chave E disse pra onde vai o dinheiro. Os dois: só a chave ligada mandaria a
    // cobrança inteira pro Padelizou, e só a conta informada não vale como consentimento.
    public static bool Conectada(Jogador? dono) =>
        dono is { ReceberPagamentoOnline: true } && !string.IsNullOrWhiteSpace(dono.AsaasWalletId);

    // Null = está tudo certo. Quando falta algo, o texto fala com QUEM VAI RECEBER.
    public static string? OQueFalta(Jogador? dono, bool recebimentoDisponivel = true)
    {
        // Gateway desligado é problema nosso, não dele — e a saída é outra: seguir por fora.
        if (!recebimentoDisponivel)
            return "O pagamento pelo app está fora do ar no momento.";

        if (dono == null)
            return "Não encontramos o cadastro de quem vai receber.";

        if (!Conectada(dono))
            return "Você ainda não disse pra onde quer que o dinheiro das inscrições vá.";

        return null;
    }

    // O convite que acompanha a recusa. Fica aqui pra não divergir entre as telas: as três
    // mandam a pessoa pro MESMO lugar, e é esse lugar que precisa estar certo.
    public const string ComoResolver =
        "Abra Perfil › Meus Pagamentos › Configurar recebimento e conecte sua conta — leva um minuto.";
}
