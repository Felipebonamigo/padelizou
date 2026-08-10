namespace Padelizou.Services;

// Canal de contato do beta. Enquanto o Padelizou é testado por conhecidos, quem encontra
// um problema fala direto com o Felipe no WhatsApp — é mais rápido que e-mail e a pessoa
// já está com o celular na mão quando o erro acontece.
public class SuporteSettings
{
    // DDD + número, sem o 55. Fica em configuração pra trocar sem republicar o site.
    //
    // ⚠️ Este NÃO é o número que o robô usa pra enviar (esse é o chip da Evolution, em
    // `Evolution__Instancia`). Aqui é o número que a PESSOA vê e para o qual ela escreve —
    // tráfego de entrada, que não corre risco de restrição por spam.
    // 07/08/2026: era o 51 99239-5650 ("Bonamigo Systems"), trocado pelo pessoal do Felipe.
    public string WhatsApp { get; set; } = "51994854884";

    // Como o número aparece escrito na tela. Separado do de cima de propósito: o link precisa
    // do número limpo, a pessoa precisa ler formatado — e ninguém deve ter que manter os dois
    // em lugares diferentes do código.
    public string WhatsAppFormatado => WhatsAppLinkHelper.Formatar(WhatsApp);

    public string Email { get; set; } = "Padelizou@gmail.com";

    // ---- Quem responde pelos dados (LGPD) ----
    // A política de privacidade (Views/Home/Privacy.cshtml) precisa dizer QUEM é o controlador
    // dos dados e como falar com ele — é o artigo 9º da LGPD, e é o que transforma a página
    // de texto decorativo em compromisso de alguém.
    //
    // Fica em configuração pelo mesmo motivo da chave Pix: se a empresa mudar de nome ou de
    // CNPJ, quem corrige é o Felipe pelo systemd, sem esperar deploy — e enquanto estiver
    // errado, está errado numa página pública.
    //
    // Vazio = a política mostra só o nome de quem opera, sem número de documento. Ela não
    // quebra e não inventa: o que não está preenchido simplesmente não aparece.
    public string RazaoSocial { get; set; } = "";
    public string Cnpj { get; set; } = "";
}
