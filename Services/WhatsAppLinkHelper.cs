using System.Net;

namespace Padelizou.Services;

public static class WhatsAppLinkHelper
{
    // Gera um link wa.me pronto para o usuário clicar e enviar a mensagem pelo próprio WhatsApp.
    // Assume número de celular brasileiro (DDD + número, sem o "55").
    public static string GerarLink(string? celular, string mensagem)
    {
        var numeroLimpo = (celular ?? string.Empty)
            .Replace("-", "")
            .Replace(" ", "")
            .Replace("(", "")
            .Replace(")", "");

        return $"https://wa.me/55{numeroLimpo}?text={WebUtility.UrlEncode(mensagem)}";
    }
}
