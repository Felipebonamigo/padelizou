namespace Padelizou.Middleware;

// Diz aos buscadores o que pode entrar no índice — por HOST, porque o mesmo binário serve
// três: o site público (indexável), o dev e o admin (nunca).
//
// O caso que motivou isto: quando o portão de acesso antecipado caiu pro lançamento, o dev
// abriu junto — público, com dado de teste, e SEM robots.txt. O Google indexaria o ambiente
// de teste ao lado do site real, na semana do lançamento.
//
// Duas defesas, porque robots.txt sozinho não basta: ele impede RASTREAR, mas página já
// linkada em algum lugar ainda pode ser indexada "às cegas" (URL no índice, sem conteúdo).
// O cabeçalho X-Robots-Tag em toda resposta é o que de fato mantém (e tira) o host da busca.
public class RobotsMiddleware
{
    private readonly RequestDelegate _next;

    public RobotsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    private static bool HostNaoIndexavel(HttpContext context)
        => AdminHostMiddleware.EhHostDev(context) || AdminHostMiddleware.EhHostAdmin(context);

    public async Task InvokeAsync(HttpContext context)
    {
        var naoIndexavel = HostNaoIndexavel(context);

        if (naoIndexavel)
        {
            context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        }

        // Responde aqui e não passa adiante: fica na frente do portão de acesso antecipado
        // de propósito (ver a ordem no Program.cs) — com o portão religado, o robots.txt
        // continua legível pros buscadores em vez de virar redirect pra tela de senha.
        if (context.Request.Path == "/robots.txt")
        {
            context.Response.ContentType = "text/plain; charset=utf-8";

            // O ponteiro pro sitemap sai daqui porque é onde o buscador olha primeiro, antes
            // de rastrear qualquer coisa. No host não indexável ele não vai junto: seria
            // entregar a lista de endereços logo a quem acabamos de mandar não entrar.
            var sitemap = $"Sitemap: {context.Request.Scheme}://{context.Request.Host}/sitemap.xml\n";

            await context.Response.WriteAsync(naoIndexavel
                ? "User-agent: *\nDisallow: /\n"
                : "User-agent: *\nAllow: /\n\n" + sitemap);
            return;
        }

        await _next(context);
    }
}
