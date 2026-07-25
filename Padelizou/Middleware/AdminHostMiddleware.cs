namespace Padelizou.Middleware;

// Separa o painel admin (/Admin) pro subdomínio admin.padelizou.com.br — o site público não
// serve /Admin, e o subdomínio admin só serve /Admin, /Auth (login/logout/perfil) e assets
// estáticos. dev.padelizou.com.br (instância/banco próprios) fica de fora dessa restrição —
// serve tudo, sem exceção. Não mexe em quem PODE entrar (isso continua sendo
// IsAdminGeral/IsAdminRaiz, checado no AdminController normalmente) — só em ONDE cada coisa é
// servida.
// Desligado em Development (não existe esse host localmente) — testar via curl com
// -H "Host: admin.padelizou.com.br" contra o servidor local, ou direto em produção.
public class AdminHostMiddleware
{
    private const string AdminHost = "admin.padelizou.com.br";

    // Instância própria (padelizou-dev.service) com banco próprio, isolada da produção — não
    // precisa da restrição de /Admin: é ambiente de teste, não fica exposto pra usuário real.
    private const string DevHost = "dev.padelizou.com.br";

    private static readonly string[] PrefixosLiberadosNoAdmin =
    {
        "/Admin", "/Auth", "/lib", "/css", "/js", "/image", "/favicon", "/manifest.json"
    };

    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _env;

    public AdminHostMiddleware(RequestDelegate next, IWebHostEnvironment env)
    {
        _next = next;
        _env = env;
    }

    public static bool EhHostAdmin(HttpContext context)
        => string.Equals(context.Request.Host.Host, AdminHost, StringComparison.OrdinalIgnoreCase);

    public static bool EhHostDev(HttpContext context)
        => string.Equals(context.Request.Host.Host, DevHost, StringComparison.OrdinalIgnoreCase);

    public async Task InvokeAsync(HttpContext context)
    {
        if (_env.IsDevelopment())
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path;

        if (EhHostAdmin(context))
        {
            if (path == "/")
            {
                context.Response.Redirect("/Admin/Index");
                return;
            }

            var liberado = PrefixosLiberadosNoAdmin.Any(p => path.StartsWithSegments(p, StringComparison.OrdinalIgnoreCase));
            if (!liberado)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
        }
        else if (EhHostDev(context))
        {
            // sem restrição nenhuma — libera tudo, inclusive /Admin, pra dar pra testar
            // qualquer funcionalidade nessa instância isolada.
        }
        else if (path.StartsWithSegments("/Admin", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        await _next(context);
    }
}
