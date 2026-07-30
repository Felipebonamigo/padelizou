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

    public const string SitePublicoHost = "padelizou.com.br";

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

    // Este host serve SÓ o painel? É o que o InvokeAsync abaixo faz valer, e o menu precisa
    // saber: ali dentro os itens do site público (Torneio, Aula, Ranking) dão 404, então o
    // layout troca a barra inteira por "Voltar ao site" em vez de oferecer beco sem saída.
    public static bool ServeSoOPainel(HttpContext context, bool ehDesenvolvimento)
        => !ehDesenvolvimento && EhHostAdmin(context);

    // Pra onde o item "Painel Admin" do menu manda. Só vale o pulo de host quando estamos no
    // site público de produção — que é onde /Admin dá 404. No dev, no localhost e dentro do
    // próprio painel o caminho relativo resolve, e evita que um clique no ambiente de teste
    // jogue quem está testando dentro da produção.
    public static string LinkDoPainel(HttpContext context, bool ehDesenvolvimento)
        => ehDesenvolvimento || EhHostAdmin(context) || EhHostDev(context)
            ? "/Admin"
            : $"https://{AdminHost}/Admin";

    // A volta: de dentro do painel, o caminho relativo cairia no 404 do próprio host.
    public static string LinkDoSitePublico => $"https://{SitePublicoHost}";

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
