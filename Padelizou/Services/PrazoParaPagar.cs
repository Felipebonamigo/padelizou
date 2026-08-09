using Padelizou.Models;

namespace Padelizou.Services;

// QUANDO o jogador precisa pagar a inscrição — e, por consequência, quanto tempo a cobrança
// dele fica de pé.
//
// São dois arranjos, e o organizador escolhe (Felipe, 08/08/2026):
//
//   1. "Só confirmo a inscrição depois de pago" — a vaga só existe com o dinheiro. Protege do
//      calote, mas cobra caro em desistência no meio do caminho.
//
//   2. "Aceito pagamento até o fechamento das inscrições" — a vaga nasce na hora, marcada como
//      pendente, e o acerto vale até a data em que as inscrições fecham. É como o torneio de
//      clube funciona de verdade: a pessoa garante o lugar no grupo do WhatsApp e paga durante
//      a semana.
//
// ⚠️ ESTE ARQUIVO NASCEU DE UM ESTRAGO REAL. No NATA PADEL TOUR, com o arranjo 1 ligado,
// OITO pessoas se inscreveram e sumiram: a cobrança tinha 60 minutos de vida
// (AsaasSettings.MinutosParaPagar), o varredor a marcava "Expirado", e como no arranjo 1 a
// inscrição só nasce no pagamento, não sobrava NADA — nem uma linha na lista de inscritos.
// Quem abriu o checkout às 20h15 e voltou às 22h tinha perdido a vaga sem nunca ser avisado.
//
// O comentário do próprio PagamentoExpiradoBackgroundService dizia "a inscrição não é afetada:
// ela só nasce quando o pagamento confirma, então não há vaga presa a liberar aqui" — a frase
// estava certa sobre o mecanismo e errada sobre a consequência.
public static class PrazoParaPagar
{
    // Os dois arranjos, escritos como o organizador os lê na tela.
    public const string SoDepoisDePago = "Só confirmo a inscrição depois de pago";
    public const string AteOFechamento = "Aceito pagamento até o fechamento das inscrições";

    public static bool ExigePagarNaHora(Torneio torneio) => torneio.PagamentoObrigatorioNaInscricao;

    // Até quando ESTA cobrança vale. Nulo = a regra de sempre (os minutos do gateway).
    //
    // ⚠️ No arranjo 2 a cobrança dura até o fim do dia do fechamento das inscrições, e não 60
    // minutos: o link de pagamento é o que a pessoa volta pra usar depois, e matá-lo em uma
    // hora transforma "pago depois" em "não pagou".
    //
    // `PrazoPagamento` (a data que o organizador digita à mão) manda quando existe; senão vale
    // o encerramento previsto das inscrições. Sem nenhum dos dois não há data pra prometer, e
    // aí a cobrança segue a regra de sempre.
    public static DateTime? Ate(Torneio torneio)
    {
        if (ExigePagarNaHora(torneio)) return null;

        var data = torneio.PrazoPagamento ?? torneio.PrevisaoEncerramentoInscricoes;
        if (data == null) return null;

        // Fim do dia: quem tem até dia 21 pra pagar tem o dia 21 inteiro.
        return data.Value.Date.AddDays(1).AddTicks(-1);
    }

    // O que a tela diz pra quem está devendo. Nulo quando não há data combinada — aí o selo
    // fica só em "Pagamento pendente", sem prometer prazo que ninguém definiu.
    public static string? Frase(Torneio torneio)
    {
        var ate = Ate(torneio);
        return ate == null ? null : $"Pague até {ate.Value:dd/MM}";
    }

    // ⚠️ O PRAZO É RECADO PESSOAL (Felipe, 09/08/2026: "só mostre esse 'pague até 21/09' para o
    // usuário que deve, não para todos").
    //
    // O selo "Pagamento pendente" é informação DO TORNEIO — quem abre a lista tem o direito de
    // saber quantas vagas já estão acertadas, e foi pedido justamente pra isso. Já "Pague até
    // 21/09" é uma ORDEM dirigida a UMA pessoa: pendurada no nome dos outros ela não informa
    // nada a quem lê e ainda cobra em público quem está devendo, na frente do torneio inteiro.
    //
    // Quem vê é só quem pode agir — as mesmas pessoas pra quem o botão "pagar agora" aparece.
    public static string? FraseParaODono(Torneio torneio, bool souDaInscricao, bool pago)
        => !souDaInscricao || pago ? null : Frase(torneio);
}
