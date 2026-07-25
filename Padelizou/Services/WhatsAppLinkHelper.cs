using System.Net;

namespace Padelizou.Services;

public static class WhatsAppLinkHelper
{
    // Remove formatação comum de celular brasileiro (DDD + número, sem o "55").
    public static string LimparNumero(string? celular)
    {
        return (celular ?? string.Empty)
            .Replace("-", "")
            .Replace(" ", "")
            .Replace("(", "")
            .Replace(")", "");
    }

    // Gera um link wa.me pronto para o usuário clicar e enviar a mensagem pelo próprio WhatsApp.
    public static string GerarLink(string? celular, string mensagem)
    {
        return $"https://wa.me/55{LimparNumero(celular)}?text={WebUtility.UrlEncode(mensagem)}";
    }
}
