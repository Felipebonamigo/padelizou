using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Middleware;

// Trava geral do site (login/senha único, sem relação com as contas de Jogador) usada só na
// fase de teste fechado com conhecidos, antes de o Padelizou ficar público de verdade.
// Fica desligada em Development (appsettings.Development.json) e ligada no appsettings.json
// base (que é gitignored) — então só entra em ação quando publicado.
public class AcessoAntecipadoMiddleware
{
    public const string NomeCookie = "PadelizouAcessoLiberado";

    // MODO DEMO TEMPORÁRIO (fase de teste fechado, site ainda não é público): quem já passou pela
    // senha do Acesso Antecipado sempre entra logado como esse jogador — checado em toda request
    // (não só no momento do POST do form), pra garantir que os menus de jogador logado nunca
    // sumam (cookie do jogador expirando, trocando de aba/dispositivo, etc.). Remover este bloco
    // (e o CPF abaixo) quando o site abrir pro público geral.
    private const string CpfLoginAutomatico = "02061197043"; // Felipe Bonamigo

    // Só o caminho exato do webhook entra aqui — liberar "/Pagamentos" inteiro abriria as telas
    // de extrato e configuração de recebimento pra qualquer visitante. O webhook em si não fica
    // desprotegido: ele exige o token secreto do Asaas no header.
    private static readonly string[] PrefixosLiberados =
    {
        "/AcessoAntecipado", "/lib", "/css", "/js", "/image", "/uploads", "/favicon", "/Agenda/Feed",
        "/manifest.json", "/sw.js", "/Pagamentos/Webhook"
    };

    private readonly RequestDelegate _next;

    public AcessoAntecipadoMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IOptions<AcessoAntecipadoSettings> options, DbPadelContext db)
    {
        // No subdomínio admin (admin.padelizou.com.br) esse gate nem entra em ação — nem a
        // senha compartilhada, nem o auto-login de demonstração como Felipe. Ele existe pra
        // liberar o site público pra quem tem a senha; deixar rodar aqui faria qualquer pessoa
        // com a senha pública (que vai ser compartilhada numa apresentação) virar admin sem
        // nunca ter feito login de verdade.
        if (AdminHostMiddleware.EhHostAdmin(context))
        {
            await _next(context);
            return;
        }

        var settings = options.Value;

        if (!settings.Habilitado || EhCaminhoLiberado(context.Request.Path))
        {
            await _next(context);
            return;
        }

        if (!EstaLiberado(context, settings))
        {
            var returnUrl = context.Request.Path + context.Request.QueryString;
            context.Response.Redirect($"/AcessoAntecipado/Entrar?returnUrl={Uri.EscapeDataString(returnUrl)}");
            return;
        }

        // A checagem de cookie/rota já rodou, mas o middleware de autenticação normal do
        // ASP.NET Core ainda não rodou nesse ponto do pipeline (estamos antes do UseRouting) —
        // por isso autenticamos manualmente aqui pra saber se o jogador já está logado.
        var resultado = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (resultado.Succeeded)
        {
            context.User = resultado.Principal!;
        }
        else
        {
            var jogadorDemo = await db.Jogadores.FirstOrDefaultAsync(j => j.Cpf == CpfLoginAutomatico);
            if (jogadorDemo != null)
            {
                // Mesmo conjunto de claims do login normal (AuthController) — sem isso os menus
                // condicionais (Painel do Professor, Painel Admin) não apareciam nesse modo, já
                // que dependiam de IsProfessor/IsAdmin que faltavam aqui.
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, jogadorDemo.Id.ToString()),
                    new Claim(ClaimTypes.Name, jogadorDemo.Nome),
                    new Claim(ClaimTypes.Email, jogadorDemo.Email ?? ""),
                    new Claim("FotoPerfil", jogadorDemo.FotoPerfil ?? ""),
                    new Claim("IsProfessor", jogadorDemo.IsProfessor ? "true" : "false"),
                    new Claim("IsAdmin", (jogadorDemo.IsAdminGeral || jogadorDemo.IsAdminRaiz) ? "true" : "false"),
                    new Claim("IsAdminRaiz", jogadorDemo.IsAdminRaiz ? "true" : "false")
                };
                var identidade = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identidade);
                await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(90)
                });
                context.User = principal;
            }
        }

        await _next(context);
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
