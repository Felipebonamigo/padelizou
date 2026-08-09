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
// Era o texto que estava certo e o código errado. Hoje esse aviso vive em
// _EscolhaFormaPagamento.cshtml e lê `DiasParaBoleto` daqui — amarrado à constante justamente
// pra que a tela não volte a prometer um prazo que o código não cumpre.
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

    // ⚠️ `prazoCombinado` É O CONSERTO DE 09/08/2026, e ele fecha um buraco que passou batido:
    // o torneio que aceita "pagar até o fechamento das inscrições" esticava só o NOSSO relógio
    // (Pagamento.ExpiraEm) — o vencimento que ia pro gateway continuava sendo hoje+1.
    //
    // O resultado era a tela prometendo "Pague até 21/09" enquanto a fatura vencia no dia
    // seguinte; vencida, o Asaas manda PAYMENT_OVERDUE e o pagamento vira "Cancelado" aqui.
    // Pior: o lembrete de "sua cobrança está pra vencer" só procura status "Pendente", então
    // ele NUNCA sairia — a cobrança já estaria cancelada muito antes das 6h finais.
    //
    // ⚠️ O prazo combinado só ESTICA, nunca encurta. Se o fechamento for antes do mínimo do
    // meio de pagamento, vale o mínimo: boleto que vence antes de conseguir compensar já nasce
    // morto (a lição lá de cima), e emitir um assim seria trocar um problema por outro. Custa
    // aceitar pagamento um ou dois dias depois do fechamento — barato perto de cobrar de
    // alguém com um papel que o banco não aceita.
    public static DateTime Para(string? billingType, DateTime hoje, DateTime? prazoCombinado = null)
    {
        var minimo = hoje.Date.AddDays(EhBoleto(billingType) ? DiasParaBoleto : DiasParaOResto);

        if (prazoCombinado is not { } prazo) return minimo;

        return prazo.Date > minimo ? prazo.Date : minimo;
    }

    // "UNDEFINED" (o jogador escolhe no meio de pagamento) entra aqui de propósito: ali o
    // boleto É uma das opções que ele pode abrir, e um vencimento de um dia tiraria a opção
    // sem avisar ninguém.
    private static bool EhBoleto(string? billingType) =>
        billingType is "BOLETO" or "UNDEFINED";
}
