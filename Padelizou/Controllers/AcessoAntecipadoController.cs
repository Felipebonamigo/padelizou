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
        private readonly BetaSettings _beta;

        public AcessoAntecipadoController(IOptions<AcessoAntecipadoSettings> options, IOptions<BetaSettings> beta)
        {
            _settings = options.Value;
            _beta = beta.Value;
        }

        [HttpGet]
        public IActionResult Entrar(string? returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            // O aviso de "isto é o ambiente de teste" mora no portão: dentro do site as duas
            // instalações são idênticas, e aqui é o último momento em que dá pra perceber
            // que se errou de endereço.
            ViewBag.AmbienteDeTeste = _beta.AmbienteDeTeste;
            return View();
        }

        // O portão tem senha compartilhada — sem trava por IP seria o alvo mais barato de
        // força-bruta do site inteiro (ver TravaDeEntrada).
        [HttpPost]
        [EnableRateLimiting(TravaDeEntrada.PoliticaPorIp)]
        public IActionResult Entrar(string usuario, string senha, string? returnUrl)
        {
            // Vale a principal ou qualquer uma das extras — a regra mora nas Settings porque é
            // lá que as credenciais vivem (ver AcessoAntecipadoSettings.Confere).
            if (_settings.Confere(usuario, senha))
            {
                // O cookie é o MESMO pra qualquer credencial: ele diz "passou pelo portão", não
                // quem passou. Sai da credencial principal de propósito — assim entrar por uma
                // extra não cria um cookie que sobreviveria à troca da senha principal.
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
