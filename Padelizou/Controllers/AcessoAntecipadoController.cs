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
            // O usuário do portão NÃO é segredo (o segredo é a senha) — e ele chega por
            // WhatsApp, digitado num celular que decide sozinho se capitaliza a primeira
            // letra. Comparar com caixa exigiria acertar "Corneteiros" e não "corneteiros",
            // o que viraria chamado de suporte na primeira noite. A senha continua exata.
            //
            // O Trim vale pros dois: copiar de uma mensagem quase sempre traz espaço colado,
            // e recusar por causa disso é recusar quem digitou certo.
            if (string.Equals(usuario?.Trim(), _settings.Usuario?.Trim(), StringComparison.OrdinalIgnoreCase)
                && senha?.Trim() == _settings.Senha?.Trim())
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
