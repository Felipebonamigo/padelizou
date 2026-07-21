using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using System.Security.Claims;

namespace padelizou.Controllers
{
    public class AuthController : Controller
    {
        private readonly DbPadelContext _context;
        private readonly IWebHostEnvironment _env;

        public AuthController(DbPadelContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string senha)
        {
            var jogador = await _context.Jogadores
                .FirstOrDefaultAsync(j => j.Email == email && j.SenhaHash == senha);

            if (jogador == null)
            {
                ViewBag.Erro = "E-mail ou senha incorretos.";
                return View();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, jogador.Id.ToString()),
                new Claim(ClaimTypes.Name, jogador.Nome),
                new Claim(ClaimTypes.Email, jogador.Email)
            };

            var identidade = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identidade);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return RedirectToAction("Perfil");
        }

        // 3. TELA DE PERFIL (Só entra se estiver logado!)
        [Authorize]
        public async Task<IActionResult> Perfil()
        {
            // Pega o ID do cara que está logado no cookie
            var jogadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            // Busca os dados dele e os grupos que ele participa
            var jogador = await _context.Jogadores
                .FirstOrDefaultAsync(j => j.Id == jogadorId);

            if (jogador == null) return NotFound();

            ViewBag.MeusGrupos = await _context.JogadoresGrupo
                .Include(jg => jg.GrupoPrivado)
                .Where(jg => jg.JogadorId == jogadorId)
                .OrderByDescending(jg => jg.PontuacaoInterna)
                .ToListAsync();

            return View(jogador);
        }

        // 4. LOGOUT
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
        // 5. TELA DE CADASTRO (Abre o formulário)
        [HttpGet]
        public async Task<IActionResult> Cadastro()
        {
            ViewBag.CatalogoCategorias = await _context.CategoriasPadrao.OrderBy(c => c.Id).ToListAsync();
            ViewBag.CatalogoClubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();
            return View();
        }

        // 6. RECEBE OS DADOS DE CADASTRO UNIFICADO, A FOTO E AS PREFERÊNCIAS
        [HttpPost]
        public async Task<IActionResult> Cadastro(
            string nome, string cpf, string email, string senha, bool isProfessor, IFormFile foto,
            string? ladoQuadra, bool notificarEmail, bool notificarWhatsApp,
            int[]? categoriasSelecionadas, int[]? clubesSelecionados, string[]? diasHorariosSelecionados)
        {
            string caminhoDaFotoParaBanco = "";

            // 1. Lógica de Upload da Foto (Salva na pasta, não no banco!)
            if (foto != null && foto.Length > 0)
            {
                string pastaUploads = Path.Combine(_env.WebRootPath, "uploads", "fotos-perfil");

                if (!Directory.Exists(pastaUploads))
                {
                    Directory.CreateDirectory(pastaUploads);
                }

                string nomeArquivoUnico = Guid.NewGuid().ToString() + "_" + foto.FileName;
                string caminhoFisicoCompleto = Path.Combine(pastaUploads, nomeArquivoUnico);

                using (var stream = new FileStream(caminhoFisicoCompleto, FileMode.Create))
                {
                    await foto.CopyToAsync(stream);
                }

                caminhoDaFotoParaBanco = "/uploads/fotos-perfil/" + nomeArquivoUnico;
            }

            // 2. Verifica se o CPF já existe (se ele já jogou um torneio antes)
            var jogador = await _context.Jogadores.FirstOrDefaultAsync(j => j.Cpf == cpf);

            if (jogador != null)
            {
                // Se já existe, atualizamos os dados de acesso e a flag de Professor
                jogador.Email = email;
                jogador.SenhaHash = senha;
                jogador.IsProfessor = isProfessor; // <- Salva se ele marcou a caixinha

                if (caminhoDaFotoParaBanco != null)
                {
                    jogador.FotoPerfil = caminhoDaFotoParaBanco;
                }
            }
            else
            {
                // É um cadastro 100% novo!
                jogador = new Jogador
                {
                    Nome = nome,
                    Cpf = cpf,
                    Email = email,
                    SenhaHash = senha,
                    IsProfessor = isProfessor, // <- Salva se ele marcou a caixinha
                    FotoPerfil = caminhoDaFotoParaBanco,
                    PontuacaoGlobal = 0 // Motor do ranking zerado!
                };
                _context.Jogadores.Add(jogador);
            }

            jogador.LadoQuadra = ladoQuadra;
            jogador.NotificarEmail = notificarEmail;
            jogador.NotificarWhatsApp = notificarWhatsApp;

            await _context.SaveChangesAsync();
            await AtualizarPreferenciasAsync(jogador.Id, categoriasSelecionadas, clubesSelecionados, diasHorariosSelecionados);

            // 3. Loga o usuário automaticamente e manda pro Perfil
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, jogador.Id.ToString()),
                new Claim(ClaimTypes.Name, jogador.Nome),
                new Claim(ClaimTypes.Email, jogador.Email)
            };
            var identidade = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidade));

            return RedirectToAction("Perfil");
        }

        // 7. TELA DE PREFERÊNCIAS (editar depois do cadastro, sem precisar recadastrar tudo)
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Preferencias()
        {
            var jogadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var jogador = await _context.Jogadores
                .Include(j => j.JogadorCategorias)
                .Include(j => j.JogadorClubes)
                .Include(j => j.JogadorDiasHorarios)
                .FirstOrDefaultAsync(j => j.Id == jogadorId);

            if (jogador == null) return NotFound();

            ViewBag.CatalogoCategorias = await _context.CategoriasPadrao.OrderBy(c => c.Id).ToListAsync();
            ViewBag.CatalogoClubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();

            return View(jogador);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Preferencias(
            string? ladoQuadra, bool notificarEmail, bool notificarWhatsApp,
            int[]? categoriasSelecionadas, int[]? clubesSelecionados, string[]? diasHorariosSelecionados)
        {
            var jogadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var jogador = await _context.Jogadores.FindAsync(jogadorId);
            if (jogador == null) return NotFound();

            jogador.LadoQuadra = ladoQuadra;
            jogador.NotificarEmail = notificarEmail;
            jogador.NotificarWhatsApp = notificarWhatsApp;
            await _context.SaveChangesAsync();

            await AtualizarPreferenciasAsync(jogadorId, categoriasSelecionadas, clubesSelecionados, diasHorariosSelecionados);

            TempData["Sucesso"] = "Preferências atualizadas!";
            return RedirectToAction("Preferencias");
        }

        // Substitui (limpa e recria) as preferências de categoria/clube/dia-horário do jogador.
        private async Task AtualizarPreferenciasAsync(
            int jogadorId, int[]? categoriasSelecionadas, int[]? clubesSelecionados, string[]? diasHorariosSelecionados)
        {
            _context.JogadorCategorias.RemoveRange(_context.JogadorCategorias.Where(c => c.JogadorId == jogadorId));
            _context.JogadorClubes.RemoveRange(_context.JogadorClubes.Where(c => c.JogadorId == jogadorId));
            _context.JogadorDiasHorarios.RemoveRange(_context.JogadorDiasHorarios.Where(c => c.JogadorId == jogadorId));

            foreach (var categoriaId in categoriasSelecionadas ?? Array.Empty<int>())
            {
                _context.JogadorCategorias.Add(new JogadorCategoria { JogadorId = jogadorId, CategoriaPadraoId = categoriaId });
            }

            foreach (var clubeId in clubesSelecionados ?? Array.Empty<int>())
            {
                _context.JogadorClubes.Add(new JogadorClube { JogadorId = jogadorId, ClubeId = clubeId });
            }

            foreach (var diaHorario in diasHorariosSelecionados ?? Array.Empty<string>())
            {
                var partes = diaHorario.Split('|');
                if (partes.Length == 2 && int.TryParse(partes[0], out var dia))
                {
                    _context.JogadorDiasHorarios.Add(new JogadorDiaHorario { JogadorId = jogadorId, DiaSemana = dia, Periodo = partes[1] });
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
