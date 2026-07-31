using System.Net;

namespace Padelizou.Services;

// A regra do aviso por e-mail: quem recebe e como o texto fica. Pura e testável — o envio em
// si mora no EmailService, e quem dispara é o PushNotificationService (todo aviso que vira
// notificação vira também e-mail, igual ao WhatsApp).
//
// POR QUE EXISTE: o push só alcança quem instalou o app e aceitou notificação, e o WhatsApp
// depende do chip que ainda não está ligado. Quem não fez nenhum dos dois não ficava sabendo
// de NADA — nem que foi inscrito, nem que saiu da lista de espera, nem que o parceiro
// desistiu. E-mail é o que praticamente todo mundo tem, e o cadastro já pede.
public static class AvisoPorEmail
{
    // Não exige app instalado, pelo mesmo motivo do WhatsApp: é justamente quem NÃO instalou
    // que só é alcançável por aqui.
    public static bool PodeReceber(bool aceitaEmail, string? email)
    {
        if (!aceitaEmail) return false;

        var texto = (email ?? "").Trim();

        // Peneira mínima: tem arroba, tem algo dos dois lados e tem ponto no domínio. Validar
        // e-mail "de verdade" é uma toca de coelho, e o custo do falso positivo aqui é só uma
        // tentativa de envio que falha e vai pro log.
        var arroba = texto.IndexOf('@');
        if (arroba <= 0 || arroba == texto.Length - 1) return false;

        var dominio = texto[(arroba + 1)..];
        return dominio.Contains('.') && !dominio.StartsWith('.') && !dominio.EndsWith('.');
    }

    // O corpo do e-mail. Texto curto de propósito: é aviso, não boletim — quem quiser o
    // detalhe clica e vê na tela, que é onde a informação está sempre atualizada.
    //
    // Tudo que vem de fora passa por HtmlEncode: o corpo carrega nome de gente ("Fulano
    // inscreveu você") e nome é campo livre.
    public static string MontarHtml(string titulo, string corpo, string? url, string urlDoSite)
    {
        var link = LinkAbsoluto(url, urlDoSite);

        var html = $@"<div style=""font-family:Arial,Helvetica,sans-serif;max-width:520px;color:#1c1c1c"">
  <h2 style=""margin:0 0 12px;font-size:19px"">{WebUtility.HtmlEncode(titulo.Trim())}</h2>
  <p style=""margin:0 0 20px;font-size:15px;line-height:1.5"">{WebUtility.HtmlEncode(corpo.Trim())}</p>";

        if (link != null)
        {
            html += $@"
  <p style=""margin:0 0 24px""><a href=""{WebUtility.HtmlEncode(link)}""
     style=""background:#c6f432;color:#14261a;padding:11px 22px;border-radius:22px;
            text-decoration:none;font-weight:bold;display:inline-block"">Abrir no Padelizou</a></p>";
        }

        html += @"
  <p style=""margin:0;font-size:12px;color:#6c757d"">
    Você recebe este aviso porque ativou notificações por e-mail no Padelizou.
    Dá pra desligar em Preferências, no seu perfil.
  </p>
</div>";

        return html;
    }

    // O aviso carrega caminho relativo ("/Agenda") porque o navegador resolve sozinho. No
    // e-mail não há site em volta: sem o endereço na frente, o link não vai a lugar nenhum.
    private static string? LinkAbsoluto(string? url, string urlDoSite)
    {
        if (string.IsNullOrWhiteSpace(urlDoSite)) return null;

        var caminho = (url ?? "").Trim();
        if (caminho.StartsWith("http://") || caminho.StartsWith("https://")) return caminho;

        var raiz = urlDoSite.TrimEnd('/');
        if (caminho.Length == 0 || caminho == "/") return raiz;

        return caminho.StartsWith('/') ? raiz + caminho : $"{raiz}/{caminho}";
    }
}
