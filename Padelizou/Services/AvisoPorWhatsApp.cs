namespace Padelizou.Services;

// A regra do aviso por WhatsApp: quem recebe e como o texto fica. Pura e testável — o envio
// em si mora no WhatsAppApiService, e quem dispara é o PushNotificationService (todo aviso
// que vira notificação vira também mensagem).
public static class AvisoPorWhatsApp
{
    // Dois requisitos, e nenhum deles é ter o app instalado: é justamente quem NÃO instalou
    // que só é alcançável por aqui. Amarrar o WhatsApp ao push faria o canal novo falar só
    // com quem já era alcançável.
    public static bool PodeReceber(bool aceitaWhatsApp, string? celular)
    {
        if (!aceitaWhatsApp) return false;

        // Um número precisa ter DDD + número pra existir: 10 dígitos (fixo) ou 11 (celular).
        // Sem esse piso, "51" ou um campo com um traço solto viraria uma tentativa de envio
        // que só serve pra queimar cota e encher o log.
        var digitos = new string(WhatsAppLinkHelper.LimparNumero(celular).Where(char.IsDigit).ToArray());
        return digitos.Length is 10 or 11;
    }

    // O texto que chega no celular. A notificação tem título e corpo separados porque o
    // sistema operacional desenha os dois; no WhatsApp é tudo uma mensagem só, então o título
    // vai em negrito (o `*` é a marcação do próprio WhatsApp) e o link fecha a mensagem.
    //
    // O link existe SEMPRE: aviso sem caminho de volta obriga a pessoa a procurar o site, e é
    // esse atrito que faz o aviso morrer na tela de bloqueio.
    public static string Montar(string titulo, string corpo, string? url, string urlDoSite)
    {
        var texto = $"*{titulo.Trim()}*";

        if (!string.IsNullOrWhiteSpace(corpo) && corpo.Trim() != titulo.Trim())
            texto += $"\n{corpo.Trim()}";

        var link = LinkAbsoluto(url, urlDoSite);
        if (link != null) texto += $"\n\n{link}";

        var saida = Saida(urlDoSite);
        if (saida != null) texto += $"\n\n{saida}";

        return texto;
    }

    // O caminho de saída, em toda mensagem. Não é gentileza: **denúncia é o gatilho mais forte
    // que a Meta tem**, e quem não acha como sair denuncia. Custa três linhas e tira o motivo.
    //
    // ⚠️ O link vem PRIMEIRO porque é o único que funciona sozinho — desmarcar nas preferências
    // é imediato e não depende de ninguém. O "responda SAIR" vem atrás e é **manual**: não há
    // webhook de entrada, então quem lê a resposta é o Felipe, no celular do chip. Prometer os
    // dois e cumprir só um seria pior que não prometer.
    private static string? Saida(string urlDoSite)
    {
        if (string.IsNullOrWhiteSpace(urlDoSite)) return null;

        return $"_Pra não receber mais: {urlDoSite.TrimEnd('/')}/Auth/Preferencias — ou responda SAIR._";
    }

    // A notificação carrega caminho relativo ("/Agenda") porque o navegador resolve sozinho.
    // No WhatsApp não há site em volta: sem o endereço na frente, o texto chega com uma barra
    // solta que não vira link nenhum.
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
