using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Padelizou.Middleware;
using Padelizou.Services;

namespace padelizou.Controllers
{
    public class AcessoAntecipadoController : Controller
    {
        private readonly AcessoAntecipadoSettings _settings;

        public AcessoAntecipadoController(IOptions<AcessoAntecipadoSettings> options)
        {
            _settings = options.Value;
        }

        [HttpGet]
        public IActionResult Entrar(string? returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // O portão tem UMA senha compartilhada — sem trava por IP seria o alvo mais
        // barato de força-bruta do site inteiro (ver TravaDeEntrada).
        [HttpPost]
        [EnableRateLimiting(TravaDeEntrada.PoliticaPorIp)]
        public IActionResult Entrar(string usuario, string senha, string? returnUrl)
        {
            if (usuario == _settings.Usuario && senha == _settings.Senha)
            {
                Response.Cookies.Append(AcessoAntecipadoMiddleware.NomeCookie, AcessoAntecipadoMiddleware.CalcularHash(_settings), new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddDays(90)
                });

                // O login automático como o jogador demo (Felipe Bonamigo) acontece no
                // AcessoAntecipadoMiddleware, toda vez que o gate está liberado — não só aqui —
                // pra garantir que os menus de jogador logado nunca sumam depois.
                var destino = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
                return LocalRedirect(destino);
            }

            ViewBag.Erro = "Usuário ou senha incorretos.";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }
    }
}
