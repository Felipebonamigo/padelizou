using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Padelizou.Services;

namespace Padelizou.Middleware;

// Trava geral do site (login/senha único, sem relação com as contas de Jogador) usada só na
// fase de teste fechado com conhecidos, antes de o Padelizou ficar público de verdade.
// Fica desligada em Development (appsettings.Development.json) e ligada no appsettings.json
// base (que é gitignored) — então só entra em ação quando publicado.
public class AcessoAntecipadoMiddleware
{
    public const string NomeCookie = "PadelizouAcessoLiberado";

    private static readonly string[] PrefixosLiberados =
    {
        "/AcessoAntecipado", "/lib", "/css", "/js", "/image", "/uploads", "/favicon", "/Agenda/Feed"
    };

    private readonly RequestDelegate _next;

    public AcessoAntecipadoMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IOptions<AcessoAntecipadoSettings> options)
    {
        var settings = options.Value;

        if (!settings.Habilitado || EhCaminhoLiberado(context.Request.Path) || EstaLiberado(context, settings))
        {
            await _next(context);
            return;
        }

        var returnUrl = context.Request.Path + context.Request.QueryString;
        context.Response.Redirect($"/AcessoAntecipado/Entrar?returnUrl={Uri.EscapeDataString(returnUrl)}");
    }

    private static bool EhCaminhoLiberado(PathString path)
    {
        return PrefixosLiberados.Any(prefixo => path.StartsWithSegments(prefixo, StringComparison.OrdinalIgnoreCase));
    }

    private static bool EstaLiberado(HttpContext context, AcessoAntecipadoSettings settings)
    {
        return context.Request.Cookies.TryGetValue(NomeCookie, out var valor) && valor == CalcularHash(settings);
    }

    public static string CalcularHash(AcessoAntecipadoSettings settings)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{settings.Usuario}:{settings.Senha}:padelizou-acesso-antecipado"));
        return Convert.ToHexString(bytes);
    }
}
