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

    // "51992395650" -> "(51) 99239-5650". Serve pra MOSTRAR o número; o link usa o limpo.
    // Se vier num tamanho que não reconheço, devolvo como está: melhor um número sem máscara
    // do que um número embaralhado por uma regra que não previa aquele caso.
    public static string Formatar(string? celular)
    {
        var n = LimparNumero(celular);
        if (n.Length == 11) return $"({n[..2]}) {n[2..7]}-{n[7..]}";   // celular com o 9
        if (n.Length == 10) return $"({n[..2]}) {n[2..6]}-{n[6..]}";   // fixo
        return n;
    }
}
