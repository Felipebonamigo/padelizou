using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Middleware;

// CONTA UMA VISITA — e só uma visita de verdade, não asset nem polling de fundo.
//
// ⚠️ `Sec-Fetch-Mode: navigate` é a MESMA distinção que `wwwroot/sw.js` já faz do lado do
// cliente (`request.mode === "navigate"`): é o cabeçalho que o navegador manda quando a
// pessoa clicou um link ou digitou o endereço, e NÃO manda pra `<img>`, `<script>`, `fetch`
// de polling (placar ao vivo, palpitrômetro) ou chamada de API. Sem esse filtro, cada
// atualização de 20s do placar ao vivo contaria como um acesso novo, e "acessos hoje"
// mediria robô, não gente.
//
// Roda ANTES da autenticação de propósito: não precisa de `context.User` (o registro não
// guarda identidade — ver Models/AcessoAoSite), então não há motivo pra esperar o pipeline
// de auth rodar primeiro.
//
// ⚠️ DEV E ADMIN FICAM DE FORA — mesma régua de host que RobotsMiddleware já usa. "Acessos
// hoje" é sobre o produto sendo usado de verdade; teste no dev e o próprio raiz navegando no
// painel administrativo não são tráfego do site, e contá-los infla o número sem dizer nada
// sobre quanta gente está jogando padel.
public class RegistroDeAcessoMiddleware
{
    private readonly RequestDelegate _next;

    public RegistroDeAcessoMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    // Extraído do InvokeAsync pra dar pra testar sem montar pipeline — mesma régua de
    // AdminHostMiddleware.EhHostAdmin/EhHostDev, que também são predicados estáticos soltos.
    public static bool DeveRegistrar(HttpContext context) =>
        HttpMethods.IsGet(context.Request.Method)
        && context.Request.Headers["Sec-Fetch-Mode"] == "navigate"
        && !AdminHostMiddleware.EhHostDev(context)
        && !AdminHostMiddleware.EhHostAdmin(context);

    public async Task InvokeAsync(HttpContext context, DbPadelContext db)
    {
        if (DeveRegistrar(context))
        {
            await MetricasDeAcesso.RegistrarAsync(db);
        }

        await _next(context);
    }
}
