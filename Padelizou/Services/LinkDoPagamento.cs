using Padelizou.Models;

namespace Padelizou.Services;

// Pra onde vai quem clicou em "pagar".
//
// Até 10/08/2026 a resposta era a `InvoiceUrl`: a fatura hospedada do gateway. O problema não
// era de experiência — aquela página estampa o cadastro comercial INTEIRO de quem emite a
// cobrança (nome, CNPJ, e-mail, telefone e o endereço completo, com número e apartamento).
//
// E como toda cobrança do Padelizou nasce na conta principal, com split pro organizador, o
// cadastro estampado é sempre o MESMO: o de quem opera o sistema. Não era um torneio expondo
// o endereço de alguém — era toda inscrição de todo torneio, mais as aulas, as quadras, a
// mensalidade do professor e a taxa da plataforma, expondo o mesmo endereço residencial.
//
// Agora o destino é sempre uma tela nossa. Ela busca o Pix no gateway e desenha a fatura aqui
// dentro; quando não existe Pix pra aquela cobrança (a travada em cartão), aí sim manda pra
// fatura de lá — que é onde o cartão pode ser digitado com segurança, no ambiente deles.
//
// ⚠️ Continua sendo a `InvoiceUrl` gravada que diz "esta cobrança existe no gateway" — várias
// consultas filtram por `InvoiceUrl != null`. O que mudou foi o DESTINO, não o registro.
public static class LinkDoPagamento
{
    // Caminho relativo de propósito: `Redirect` aceita, e os avisos (e-mail, WhatsApp, push)
    // já sabem transformá-lo em URL absoluta com o domínio do site — ver AvisoPorEmail.
    public static string Para(int pagamentoId) => $"/Pagamentos/Fatura/{pagamentoId}";

    public static string Para(Pagamento pagamento) => Para(pagamento.Id);
}
