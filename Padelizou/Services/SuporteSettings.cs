namespace Padelizou.Services;

// Canal de contato do beta. Enquanto o Padelizou é testado por conhecidos, quem encontra
// um problema fala direto com o Felipe no WhatsApp — é mais rápido que e-mail e a pessoa
// já está com o celular na mão quando o erro acontece.
public class SuporteSettings
{
    // DDD + número, sem o 55. Fica em configuração pra trocar sem republicar o site.
    public string WhatsApp { get; set; } = "51992395650";

    // Como o número aparece escrito na tela. Separado do de cima de propósito: o link precisa
    // do número limpo, a pessoa precisa ler formatado — e ninguém deve ter que manter os dois
    // em lugares diferentes do código.
    public string WhatsAppFormatado => WhatsAppLinkHelper.Formatar(WhatsApp);

    public string Email { get; set; } = "Padelizou@gmail.com";
}
