using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Padelizou.Middleware;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace padelizou.Controllers
{
    public class AcessoAntecipadoController : Controller
    {
        // MODO DEMO TEMPORÁRIO (fase de teste fechado, site ainda não é público): quem passa pela
        // senha do Acesso Antecipado já entra direto logado como esse jogador, pulando a tela de
        // login normal — pra todo visitante já cair num perfil com histórico completo (torneios,
        // aulas, grupo). Remover este bloco (e o CPF abaixo) quando o site abrir pro público geral.
        private const string CpfLoginAutomatico = "02061197043"; // Felipe Bonamigo

        private readonly AcessoAntecipadoSettings _settings;
        private readonly DbPadelContext _context;

        public AcessoAntecipadoController(IOptions<AcessoAntecipadoSettings> options, DbPadelContext context)
        {
            _settings = options.Value;
            _context = context;
        }

        [HttpGet]
        public IActionResult Entrar(string? returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Entrar(string usuario, string senha, string? returnUrl)
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

                var jogadorDemo = await _context.Jogadores.FirstOrDefaultAsync(j => j.Cpf == CpfLoginAutomatico);
                if (jogadorDemo != null)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, jogadorDemo.Id.ToString()),
                        new Claim(ClaimTypes.Name, jogadorDemo.Nome),
                        new Claim(ClaimTypes.Email, jogadorDemo.Email ?? "")
                    };
                    var identidade = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var propriedades = new AuthenticationProperties
                    {
                        IsPersistent = true,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(90) // mesma duração do cookie do Acesso Antecipado
                    };
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidade), propriedades);
                }

                return LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
            }

            ViewBag.Erro = "Usuário ou senha incorretos.";
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }
    }
}
