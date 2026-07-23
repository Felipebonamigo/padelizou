using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace padelizou.Controllers
{
    public class AuthController : Controller
    {
        private readonly DbPadelContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IPasswordHasher<Jogador> _passwordHasher;
        private readonly IEstatisticasService _estatisticas;
        private readonly IEmailService _email;

        public AuthController(DbPadelContext context, IWebHostEnvironment env, IPasswordHasher<Jogador> passwordHasher, IEstatisticasService estatisticas, IEmailService email)
        {
            _context = context;
            _env = env;
            _passwordHasher = passwordHasher;
            _estatisticas = estatisticas;
            _email = email;
        }

        // Reportar Problema: qualquer jogador logado pode mandar um relato pra equipe do Padelizou.
        private const string EmailSuporte = "Padelizou@gmail.com";

        [Authorize]
        [HttpGet]
        public IActionResult ReportarProblema()
        {
            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportarProblema(string mensagem)
        {
            if (string.IsNullOrWhiteSpace(mensagem))
            {
                ViewBag.Erro = "Descreva o problema antes de enviar.";
                return View();
            }

            var jogadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var jogador = await _context.Jogadores.FindAsync(jogadorId);
            if (jogador == null) return NotFound();

            var corpo = $@"
                <p><strong>Jogador:</strong> {jogador.Nome} (Id {jogador.Id})</p>
                <p><strong>E-mail:</strong> {jogador.Email}</p>
                <p><strong>Problema relatado:</strong></p>
                <p>{System.Net.WebUtility.HtmlEncode(mensagem).Replace("\n", "<br/>")}</p>";

            await _email.EnviarAsync(EmailSuporte, "Padelizou", $"Problema reportado por {jogador.Nome}", corpo);

            TempData["Sucesso"] = "Problema reportado! Nossa equipe vai dar uma olhada.";
            return RedirectToAction("Perfil");
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
                .FirstOrDefaultAsync(j => j.Email == email);

            if (jogador == null || string.IsNullOrEmpty(jogador.SenhaHash) ||
                _passwordHasher.VerifyHashedPassword(jogador, jogador.SenhaHash, senha) == PasswordVerificationResult.Failed)
            {
                ViewBag.Erro = "E-mail ou senha incorretos.";
                return View();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, jogador.Id.ToString()),
                new Claim(ClaimTypes.Name, jogador.Nome),
                new Claim(ClaimTypes.Email, jogador.Email),
                new Claim("FotoPerfil", jogador.FotoPerfil ?? ""),
                new Claim("IsProfessor", jogador.IsProfessor ? "true" : "false"),
                new Claim("IsAdmin", (jogador.IsAdminGeral || jogador.IsAdminRaiz) ? "true" : "false"),
                new Claim("IsAdminRaiz", jogador.IsAdminRaiz ? "true" : "false")
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

            // Resumo de torneios (vitórias/derrotas/vezes campeão) pro dashboard não depender de
            // ir até Jogadores/Perfil só pra ver esses números.
            ViewBag.Resumo = await _estatisticas.ObterResumoJogadorAsync(jogadorId);
            ViewBag.Destaques = await _estatisticas.ObterDestaquesAsync(jogadorId);
            ViewBag.HistoricoTorneios = await _context.Duplas
                .Include(d => d.Categoria).ThenInclude(c => c.Torneio)
                .Where(d => d.Jogador1Id == jogadorId || d.Jogador2Id == jogadorId)
                .OrderByDescending(d => d.Categoria.Torneio.DataInicio)
                .Take(5)
                .ToListAsync();

            // Clubes que ele é dono ou administrador — pra linkar direto na tela de gerenciar.
            var clubesAdministrados = await _context.ClubeAdministradores
                .Include(a => a.Clube)
                .Where(a => a.JogadorId == jogadorId)
                .Select(a => a.Clube)
                .ToListAsync();
            var clubesDono = await _context.Clubes.Where(c => c.DonoId == jogadorId).ToListAsync();
            ViewBag.MeusClubes = clubesDono.Concat(clubesAdministrados).DistinctBy(c => c.Id).ToList();

            return View(jogador);
        }

        // 3.1 TELA DE EDITAR PERFIL (dados pessoais — diferente de "Preferências", que é sobre jogo)
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> EditarPerfil()
        {
            var jogadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var jogador = await _context.Jogadores.FindAsync(jogadorId);
            if (jogador == null) return NotFound();

            await PopularDadosTimeAsync(jogadorId);
            return View(jogador);
        }

        // Dados para a seção "Meu time" do editar perfil: times disponíveis (não-dono escolhe),
        // clubes (sede opcional) e o time que este jogador é dono (se houver).
        private async Task PopularDadosTimeAsync(int jogadorId)
        {
            ViewBag.Times = await _context.Times.OrderBy(t => t.Nome).ToListAsync();
            ViewBag.Clubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();
            ViewBag.MeuTimeDono = await _context.Times.FirstOrDefaultAsync(t => t.DonoId == jogadorId);
        }

        // Salva o logo do time em wwwroot/uploads/logos-time e devolve o caminho relativo.
        private async Task<string> SalvarLogoTimeAsync(IFormFile arquivo)
        {
            string pasta = Path.Combine(_env.WebRootPath, "uploads", "logos-time");
            if (!Directory.Exists(pasta)) Directory.CreateDirectory(pasta);

            string nomeArquivo = Guid.NewGuid().ToString() + "_" + arquivo.FileName;
            string caminho = Path.Combine(pasta, nomeArquivo);
            using (var stream = new FileStream(caminho, FileMode.Create))
            {
                await arquivo.CopyToAsync(stream);
            }
            return "/uploads/logos-time/" + nomeArquivo;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> EditarPerfil(
            string nome, string email, string? celular, string? cidade, string? estado, bool isProfessor, IFormFile? foto,
            bool ehDonoTime = false, int? timeId = null, string? nomeTime = null, IFormFile? logoTime = null, int? clubeSedeId = null)
        {
            var jogadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var jogador = await _context.Jogadores.FindAsync(jogadorId);
            if (jogador == null) return NotFound();

            if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Erro = "Preencha nome e e-mail.";
                await PopularDadosTimeAsync(jogadorId);
                return View(jogador);
            }

            // Se marcou "sou dono", precisa do nome do time (ao menos na primeira vez).
            var meuTime = await _context.Times.FirstOrDefaultAsync(t => t.DonoId == jogadorId);
            if (ehDonoTime && meuTime == null && string.IsNullOrWhiteSpace(nomeTime))
            {
                ViewBag.Erro = "Informe o nome do time (você marcou que é dono de um time).";
                await PopularDadosTimeAsync(jogadorId);
                return View(jogador);
            }

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

                jogador.FotoPerfil = "/uploads/fotos-perfil/" + nomeArquivoUnico;
            }

            jogador.Nome = nome;
            jogador.Email = email;
            jogador.Celular = string.IsNullOrWhiteSpace(celular) ? null : celular.Trim();
            jogador.Cidade = string.IsNullOrWhiteSpace(cidade) ? null : cidade.Trim();
            jogador.Estado = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim();
            jogador.IsProfessor = isProfessor;

            // --- Meu time ---
            if (ehDonoTime)
            {
                if (meuTime == null)
                {
                    meuTime = new Time { Nome = nomeTime!.Trim(), DonoId = jogadorId };
                    _context.Times.Add(meuTime);
                }
                else if (!string.IsNullOrWhiteSpace(nomeTime))
                {
                    meuTime.Nome = nomeTime.Trim();
                }
                meuTime.ClubeId = clubeSedeId; // clube sede é opcional
                if (logoTime != null && logoTime.Length > 0)
                {
                    meuTime.Logo = await SalvarLogoTimeAsync(logoTime);
                }
                await _context.SaveChangesAsync(); // garante o Id do time recém-criado
                jogador.TimeId = meuTime.Id;       // o dono também faz parte do próprio time
            }
            else
            {
                // Não é dono: entra num time existente (ou em nenhum).
                jogador.TimeId = timeId;
            }

            await _context.SaveChangesAsync();

            // Renova o cookie com nome/e-mail atualizados (o chip do usuário na navbar lê da claim,
            // não do banco — sem isso ficaria com o nome antigo até o próximo login).
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, jogador.Id.ToString()),
                new Claim(ClaimTypes.Name, jogador.Nome),
                new Claim(ClaimTypes.Email, jogador.Email),
                new Claim("FotoPerfil", jogador.FotoPerfil ?? ""),
                new Claim("IsProfessor", jogador.IsProfessor ? "true" : "false"),
                new Claim("IsAdmin", (jogador.IsAdminGeral || jogador.IsAdminRaiz) ? "true" : "false"),
                new Claim("IsAdminRaiz", jogador.IsAdminRaiz ? "true" : "false")
            };
            var identidade = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidade));

            TempData["Sucesso"] = "Perfil atualizado!";
            return RedirectToAction("Perfil");
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
            string nome, string cpf, string login, string email, string senha, string? celular, bool isProfessor, IFormFile foto,
            string? ladoQuadra, string? lateralidade, string? instagram, bool notificarEmail, bool notificarWhatsApp,
            int[]? categoriasSelecionadas, int[]? clubesSelecionados, string[]? diasHorariosSelecionados)
        {
            if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(cpf) ||
                string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
            {
                ViewBag.Erro = "Preencha nome, CPF, login, e-mail e senha pra finalizar o cadastro.";
                ViewBag.CatalogoCategorias = await _context.CategoriasPadrao.OrderBy(c => c.Id).ToListAsync();
                ViewBag.CatalogoClubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();
                return View();
            }

            login = login.Trim();

            // Login precisa ser único (usado depois pra buscar organizadores de torneio)
            var loginEmUso = await _context.Jogadores.AnyAsync(j => j.Login == login && j.Cpf != cpf);
            if (loginEmUso)
            {
                ViewBag.Erro = "Esse login já está em uso. Escolha outro.";
                ViewBag.CatalogoCategorias = await _context.CategoriasPadrao.OrderBy(c => c.Id).ToListAsync();
                ViewBag.CatalogoClubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();
                return View();
            }

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
                jogador.SenhaHash = _passwordHasher.HashPassword(jogador, senha);
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
                    IsProfessor = isProfessor, // <- Salva se ele marcou a caixinha
                    FotoPerfil = caminhoDaFotoParaBanco,
                    PontuacaoGlobal = 0 // Motor do ranking zerado!
                };
                jogador.SenhaHash = _passwordHasher.HashPassword(jogador, senha);
                _context.Jogadores.Add(jogador);
            }

            jogador.Login = login;
            jogador.LadoQuadra = ladoQuadra;
            jogador.Lateralidade = lateralidade;
            jogador.Instagram = string.IsNullOrWhiteSpace(instagram) ? null : instagram.Trim().TrimStart('@');
            jogador.Celular = string.IsNullOrWhiteSpace(celular) ? null : celular.Trim();
            jogador.NotificarEmail = notificarEmail;
            jogador.NotificarWhatsApp = notificarWhatsApp;

            await _context.SaveChangesAsync();
            await AtualizarPreferenciasAsync(jogador.Id, categoriasSelecionadas, clubesSelecionados, diasHorariosSelecionados);

            // 3. Loga o usuário automaticamente e manda pro Perfil
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, jogador.Id.ToString()),
                new Claim(ClaimTypes.Name, jogador.Nome),
                new Claim(ClaimTypes.Email, jogador.Email),
                new Claim("FotoPerfil", jogador.FotoPerfil ?? ""),
                new Claim("IsProfessor", jogador.IsProfessor ? "true" : "false"),
                new Claim("IsAdmin", (jogador.IsAdminGeral || jogador.IsAdminRaiz) ? "true" : "false"),
                new Claim("IsAdminRaiz", jogador.IsAdminRaiz ? "true" : "false")
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
            string? ladoQuadra, string? lateralidade, string? instagram, bool perfilPrivado, bool notificarEmail, bool notificarWhatsApp, bool aceitaConvitesJogo,
            bool notificarTorneiosAbertos, bool notificarSeguidosTorneio, bool notificarAvisoJogo, bool notificarJogoAula, bool notificarRaqueteLivre,
            int[]? categoriasSelecionadas, int[]? clubesSelecionados, string[]? diasHorariosSelecionados)
        {
            var jogadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var jogador = await _context.Jogadores.FindAsync(jogadorId);
            if (jogador == null) return NotFound();

            jogador.LadoQuadra = ladoQuadra;
            jogador.Lateralidade = lateralidade;
            jogador.Instagram = string.IsNullOrWhiteSpace(instagram) ? null : instagram.Trim().TrimStart('@');
            jogador.PerfilPrivado = perfilPrivado;
            jogador.NotificarEmail = notificarEmail;
            jogador.NotificarWhatsApp = notificarWhatsApp;
            jogador.AceitaConvitesJogo = aceitaConvitesJogo;
            jogador.NotificarTorneiosAbertos = notificarTorneiosAbertos;
            jogador.NotificarSeguidosTorneio = notificarSeguidosTorneio;
            jogador.NotificarAvisoJogo = notificarAvisoJogo;
            jogador.NotificarJogoAula = notificarJogoAula;
            jogador.NotificarRaqueteLivre = notificarRaqueteLivre;
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
