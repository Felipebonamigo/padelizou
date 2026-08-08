namespace Padelizou.Services;

// Pra quando a cobrança vence, conforme a forma de pagamento.
//
// Era um `DateTime.Today.AddDays(1)` cravado, igual pros três meios — e pro BOLETO isso é
// um prazo que não existe. Boleto leva 1 dia útil só pra COMPENSAR depois de pago: emitido
// numa sexta com vencimento no sábado, ele já nasce morto. Vencido, o Asaas manda
// PAYMENT_OVERDUE, o pagamento vira "Cancelado" e a inscrição nunca acontece — o caminho
// NORMAL do boleto, não o excepcional.
//
// ⚠️ E a tela já prometia outra coisa: a escolha do jogador diz "vence em alguns dias"
// (CobrancaDoTorneio.ExplicacaoDaEscolha). Era o texto que estava certo e o código errado.
//
// Achado em 08/08/2026 preparando o primeiro torneio que aceita boleto (Nata Padel). Até
// aqui nenhum boleto real tinha sido emitido pelo sistema, então não há cobrança antiga a
// consertar.
public static class VencimentoDaCobranca
{
    // Três dias, que é o mesmo prazo que a taxa do torneio "por fora" e a mensalidade do
    // professor já usavam neste arquivo — o boleto era o único que destoava.
    public const int DiasParaBoleto = 3;

    // Pix cai na hora e cartão é autorizado na hora: pra eles o vencimento é só o limite do
    // QR/da tentativa, e um dia é de propósito — o valor fica reservado por pouco tempo e a
    // vaga não passa a noite pendurada em quem desistiu no meio do checkout.
    public const int DiasParaOResto = 1;

    public static DateTime Para(string? billingType, DateTime hoje) =>
        hoje.Date.AddDays(EhBoleto(billingType) ? DiasParaBoleto : DiasParaOResto);

    // "UNDEFINED" (o jogador escolhe no meio de pagamento) entra aqui de propósito: ali o
    // boleto É uma das opções que ele pode abrir, e um vencimento de um dia tiraria a opção
    // sem avisar ninguém.
    private static bool EhBoleto(string? billingType) =>
        billingType is "BOLETO" or "UNDEFINED";
}
