using Microsoft.AspNetCore.Mvc;
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

        [HttpPost]
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

                return LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
            }

            ViewBag.Erro = "Usuário ou senha incorretos.";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }
    }
}
