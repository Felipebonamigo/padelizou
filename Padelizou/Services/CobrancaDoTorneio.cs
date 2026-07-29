namespace Padelizou.Services;

// Como a inscrição de um torneio é cobrada: qual forma a cobrança trava no meio de pagamento
// e qual taxa o Padelizou fica.
//
// Existe porque as duas respostas TÊM QUE CONCORDAR. Travar a cobrança em Pix e ficar com a
// taxa de cartão (ou o contrário) seria cobrar do organizador uma coisa e entregar outra — e
// as duas decisões nasciam em lugares diferentes do código.
//
// Regra nova (Felipe, 29/07/2026): quando o organizador aceita TODAS as formas, vale a taxa
// da forma que o jogador escolheu de verdade. Quem paga por Pix não carrega o preço do cartão
// parcelado — Pix custa centavos e cai na hora; o crédito custa até 3,29% + R$ 0,49 e demora
// 32 dias. Antes, "todas as formas" cobrava 15% mesmo de quem pagava por Pix.
//
// Por que o jogador precisa DECLARAR a forma antes: o rateio é fixado quando a cobrança nasce.
// Deixando o meio de pagamento aberto ("escolha lá"), na hora de definir a taxa ainda não se
// sabe o que ele vai usar — então a escolha é trazida pra dentro do nosso checkout.
public static class CobrancaDoTorneio
{
    // O que o formulário manda. Nomes em português porque vêm da tela; o que vai pro meio de
    // pagamento é o `BillingType`.
    public const string EscolhaPix = "Pix";
    public const string EscolhaCartao = "Cartao";
    public const string EscolhaBoleto = "Boleto";

    public sealed record Cobranca(string BillingType, decimal Percentual);

    // Só faz sentido perguntar quando o organizador abriu mais de uma forma.
    public static bool JogadorEscolheAForma(string formaDoTorneio) => formaDoTorneio == "OnlineTodas";

    public static Cobranca Montar(string formaDoTorneio, string? escolhaDoJogador, TaxasExibicao taxas)
    {
        // Organizador travou em Pix: não há o que escolher, e a taxa é a de Pix.
        if (formaDoTorneio == "OnlinePix")
            return new Cobranca("PIX", taxas.ComissaoPercentualSomentePix);

        if (JogadorEscolheAForma(formaDoTorneio))
        {
            return (escolhaDoJogador ?? "") switch
            {
                EscolhaPix => new Cobranca("PIX", taxas.ComissaoPercentualSomentePix),
                EscolhaCartao => new Cobranca("CREDIT_CARD", taxas.ComissaoPercentualTodasFormas),

                // Boleto paga a taxa do Pix (Felipe, 29/07/2026): pro meio de pagamento os dois
                // custam o mesmo valor fixo em centavos — o que encarece é o cartão.
                EscolhaBoleto => new Cobranca("BOLETO", taxas.ComissaoPercentualSomentePix),

                // Escolha ausente ou desconhecida (formulário antigo em cache, requisição
                // montada à mão): cai no comportamento de sempre — todas as formas liberadas,
                // taxa cheia. Errar pra cá nunca cobra do organizador MENOS do que ele
                // combinou; o contrário seria prejuízo silencioso.
                _ => new Cobranca("UNDEFINED", taxas.ComissaoPercentualTodasFormas),
            };
        }

        // "Externo": o dinheiro não passa pelo sistema, então não deveria chegar aqui. Se
        // chegar, a taxa é a do externo e a forma fica aberta.
        return new Cobranca("UNDEFINED", taxas.PercentualDoTorneio(formaDoTorneio));
    }

    // O que a tela escreve ao lado de cada opção, pra escolha ser informada: o jogador vê o
    // prazo, e o organizador (que leu a régua na criação) entende por que a taxa muda.
    public static string ExplicacaoDaEscolha(string escolha, TaxasExibicao taxas) => escolha switch
    {
        EscolhaPix => $"Cai na hora pro organizador. Taxa do Padelizou: {Pct(taxas.ComissaoPercentualSomentePix)}.",
        EscolhaCartao => $"Dá pra parcelar. O organizador recebe em {taxas.CreditoAVista.DiasParaReceber} dias. " +
                         $"Taxa do Padelizou: {Pct(taxas.ComissaoPercentualTodasFormas)}.",
        EscolhaBoleto => $"Vence em alguns dias e leva 1 dia útil pra compensar. " +
                         $"Taxa do Padelizou: {Pct(taxas.ComissaoPercentualSomentePix)} — a mesma do Pix.",
        _ => "",
    };

    private static string Pct(decimal valor) =>
        valor.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) + "%";
}
