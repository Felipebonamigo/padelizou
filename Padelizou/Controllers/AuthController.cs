using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
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
        private readonly ILogger<AuthController> _logger;
        private readonly SuporteSettings _suporte;

        public AuthController(DbPadelContext context, IWebHostEnvironment env, IPasswordHasher<Jogador> passwordHasher, IEstatisticasService estatisticas, IEmailService email, ILogger<AuthController> logger, IOptions<SuporteSettings> suporte)
        {
            _context = context;
            _env = env;
            _passwordHasher = passwordHasher;
            _estatisticas = estatisticas;
            _email = email;
            _logger = logger;
            _suporte = suporte.Value;
        }

        // Salva a foto e devolve o caminho, ou null se não deu.
        //
        // Nunca lança: a foto é opcional, e derrubar o cadastro por causa dela custa caro —
        // a pessoa preenche um formulário longo, escolhe a foto e recebe "Ops! Algo deu
        // errado", perdendo tudo. Foi exatamente o que aconteceu quando a pasta de uploads
        // do dev estava sem permissão de escrita.
        private async Task<string?> SalvarFotoPerfilAsync(IFormFile? foto)
        {
            if (foto == null || foto.Length == 0) return null;

            // Só a extensão do arquivo enviado é aproveitada — o nome vem do usuário e não
            // tem por que virar caminho no servidor.
            var extensao = Path.GetExtension(foto.FileName)?.ToLowerInvariant();
            if (extensao is not (".jpg" or ".jpeg" or ".png" or ".webp")) return null;

            try
            {
                var pasta = Path.Combine(_env.WebRootPath, "uploads", "fotos-perfil");
                Directory.CreateDirectory(pasta);

                var nomeArquivo = Guid.NewGuid() + extensao;
                using (var stream = new FileStream(Path.Combine(pasta, nomeArquivo), FileMode.Create))
                {
                    await foto.CopyToAsync(stream);
                }

                return "/uploads/fotos-perfil/" + nomeArquivo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao salvar foto de perfil — seguindo sem ela.");
                return null;
            }
        }

        // Sugestão, bug ou crítica: aberto pra qualquer visitante, inclusive deslogado —
        // no beta, boa parte dos problemas acontece justamente antes de conseguir entrar.
        [AllowAnonymous]
        [HttpGet]
        public IActionResult ReportarProblema()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportarProblema(string mensagem, string? tipo, string? contato)
        {
            if (string.IsNullOrWhiteSpace(mensagem))
            {
                ViewBag.Erro = "Escreva sua mensagem antes de enviar.";
                return View();
            }

            var rotulo = tipo switch
            {
                "Sugestão" or "Bug" or "Crítica" => tipo,
                _ => "Mensagem",
            };

            // Quem está logado a gente já sabe quem é; quem não está pode deixar um contato.
            var jogador = User.Identity?.IsAuthenticated == true
                ? await _context.Jogadores.FindAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!))
                : null;

            var quem = jogador != null
                ? $"{jogador.ComoChamar} ({jogador.Email})"
                : string.IsNullOrWhiteSpace(contato) ? "visitante não identificado" : contato.Trim();

            // E-mail é a cópia de segurança: se a pessoa desistir no WhatsApp, o relato não
            // se perde. Falha de envio não pode travar o encaminhamento.
            try
            {
                var corpo = $@"
                    <p><strong>{rotulo}</strong> de {System.Net.WebUtility.HtmlEncode(quem)}</p>
                    <p>{System.Net.WebUtility.HtmlEncode(mensagem).Replace("\n", "<br/>")}</p>";
                await _email.EnviarAsync(_suporte.Email, "Padelizou", $"[{rotulo}] Padelizou — {quem}", corpo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Não consegui mandar o e-mail de feedback — seguindo pro WhatsApp.");
            }

            // Encaminha pro WhatsApp com tudo já digitado: é só apertar enviar.
            var texto = $"*{rotulo} — Padelizou*\n\nDe: {quem}\n\n{mensagem}";
            return Redirect(WhatsAppLinkHelper.GerarLink(_suporte.WhatsApp, texto));
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string senha)
        {
            // Aceita e-mail OU login, sem diferenciar maiúsculas (ver BuscaJogador).
            // Antes só o e-mail servia, e exato — quem se cadastrou com login nunca
            // conseguia entrar por ele.
            var jogador = await BuscaJogador.PorIdentificadorAsync(_context, email);

            if (jogador == null || string.IsNullOrEmpty(jogador.SenhaHash) ||
                _passwordHasher.VerifyHashedPassword(jogador, jogador.SenhaHash, senha) == PasswordVerificationResult.Failed)
            {
                ViewBag.Erro = "Login ou senha incorretos.";
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
            ViewBag.Evolucao = await _estatisticas.ObterEvolucaoJogadorAsync(jogadorId);
            ViewBag.Onboarding = await _estatisticas.ObterOnboardingAsync(jogadorId);
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
            string? apelido = null,
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

            var fotoSalva = await SalvarFotoPerfilAsync(foto);
            if (fotoSalva != null) jogador.FotoPerfil = fotoSalva;

            jogador.Nome = nome;
            // Apelido em branco volta a ser nulo — "sem apelido" e "apelido vazio" viram
            // a mesma coisa, senão ComoChamar teria que testar string vazia em todo lugar.
            jogador.Apelido = string.IsNullOrWhiteSpace(apelido) ? null : apelido.Trim();
            jogador.Email = email;
            jogador.Celular = Documentos.SomenteDigitosOuNulo(celular);
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
            ViewBag.CatalogoCidades = await _context.Cidades.OrderBy(c => c.Nome).ToListAsync();
            return View();
        }

        // 6. RECEBE OS DADOS DE CADASTRO UNIFICADO, A FOTO E AS PREFERÊNCIAS
        [HttpPost]
        public async Task<IActionResult> Cadastro(
            string nome, string cpf, string login, string email, string senha, string? celular, bool isProfessor, IFormFile foto,
            string? ladoQuadra, string? lateralidade, string? instagram, bool notificarEmail, bool notificarWhatsApp,
            int[]? categoriasSelecionadas, int[]? clubesSelecionados, string[]? diasHorariosSelecionados,
            string? apelido = null)
        {
            if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(cpf) ||
                string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
            {
                ViewBag.Erro = "Preencha nome, CPF, login, e-mail e senha pra finalizar o cadastro.";
                ViewBag.CatalogoCategorias = await _context.CategoriasPadrao.OrderBy(c => c.Id).ToListAsync();
                ViewBag.CatalogoClubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();
                ViewBag.CatalogoCidades = await _context.Cidades.OrderBy(c => c.Nome).ToListAsync();
                return View();
            }

            login = login.Trim();

            // Normaliza antes de qualquer consulta: o CPF é a chave usada pra reconhecer quem
            // já jogou torneio, e "111.444.777-35" nunca casaria com o "11144477735" gravado
            // na inscrição — criaria um segundo cadastro pra mesma pessoa.
            cpf = Documentos.SomenteDigitos(cpf);
            celular = Documentos.SomenteDigitosOuNulo(celular);

            if (!Documentos.CpfTemFormatoValido(cpf))
            {
                ViewBag.Erro = "CPF inválido — informe os 11 números.";
                ViewBag.CatalogoCategorias = await _context.CategoriasPadrao.OrderBy(c => c.Id).ToListAsync();
                ViewBag.CatalogoClubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();
                ViewBag.CatalogoCidades = await _context.Cidades.OrderBy(c => c.Nome).ToListAsync();
                return View();
            }

            // Login precisa ser único IGNORANDO maiúsculas — como a entrada aceita
            // "Bona" e "bona" como a mesma coisa, deixar as duas se cadastrarem tornaria
            // o login ambíguo (duas contas atenderiam pelo mesmo identificador).
            var loginNormalizado = login.ToLower();
            var loginEmUso = await _context.Jogadores
                .AnyAsync(j => j.Login != null && j.Login.ToLower() == loginNormalizado && j.Cpf != cpf);
            if (loginEmUso)
            {
                ViewBag.Erro = "Esse login já está em uso. Escolha outro.";
                ViewBag.CatalogoCategorias = await _context.CategoriasPadrao.OrderBy(c => c.Id).ToListAsync();
                ViewBag.CatalogoClubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();
                ViewBag.CatalogoCidades = await _context.Cidades.OrderBy(c => c.Nome).ToListAsync();
                return View();
            }

            // A foto é opcional e não pode impedir o cadastro: se falhar, entra vazia.
            string caminhoDaFotoParaBanco = await SalvarFotoPerfilAsync(foto) ?? "";

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
                    Apelido = string.IsNullOrWhiteSpace(apelido) ? null : apelido.Trim(),
                    Cpf = cpf,
                    Email = email,
                    IsProfessor = isProfessor, // <- Salva se ele marcou a caixinha
                    FotoPerfil = caminhoDaFotoParaBanco,
                };
                jogador.SenhaHash = _passwordHasher.HashPassword(jogador, senha);
                _context.Jogadores.Add(jogador);
            }

            jogador.Login = login;
            jogador.LadoQuadra = ladoQuadra;
            jogador.Lateralidade = lateralidade;
            jogador.Instagram = string.IsNullOrWhiteSpace(instagram) ? null : instagram.Trim().TrimStart('@');
            jogador.Celular = Documentos.SomenteDigitosOuNulo(celular);
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
                .Include(j => j.JogadorCidades)
                .FirstOrDefaultAsync(j => j.Id == jogadorId);

            if (jogador == null) return NotFound();

            ViewBag.CatalogoCategorias = await _context.CategoriasPadrao.OrderBy(c => c.Id).ToListAsync();
            ViewBag.CatalogoClubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();
            ViewBag.CatalogoCidades = await _context.Cidades.OrderBy(c => c.Nome).ToListAsync();

            return View(jogador);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Preferencias(
            string? ladoQuadra, string? lateralidade, string? instagram, bool perfilPrivado, bool notificarEmail, bool notificarWhatsApp, bool aceitaConvitesJogo,
            bool notificarTorneiosAbertos, bool notificarSeguidosTorneio, bool notificarAvisoJogo, bool notificarJogoAula, bool notificarRaqueteLivre,
            bool notificarHorarioVagoRegiao,
            int[]? categoriasSelecionadas, int[]? clubesSelecionados, string[]? diasHorariosSelecionados, int[]? cidadesSelecionadas)
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
            jogador.NotificarHorarioVagoRegiao = notificarHorarioVagoRegiao;
            await _context.SaveChangesAsync();

            await AtualizarPreferenciasAsync(jogadorId, categoriasSelecionadas, clubesSelecionados, diasHorariosSelecionados, cidadesSelecionadas);

            TempData["Sucesso"] = "Preferências atualizadas!";
            return RedirectToAction("Preferencias");
        }

        // Substitui (limpa e recria) as preferências de categoria/clube/dia-horário/cidade do jogador.
        private async Task AtualizarPreferenciasAsync(
            int jogadorId, int[]? categoriasSelecionadas, int[]? clubesSelecionados, string[]? diasHorariosSelecionados, int[]? cidadesSelecionadas = null)
        {
            _context.JogadorCategorias.RemoveRange(_context.JogadorCategorias.Where(c => c.JogadorId == jogadorId));
            _context.JogadorClubes.RemoveRange(_context.JogadorClubes.Where(c => c.JogadorId == jogadorId));
            _context.JogadorDiasHorarios.RemoveRange(_context.JogadorDiasHorarios.Where(c => c.JogadorId == jogadorId));
            _context.JogadorCidades.RemoveRange(_context.JogadorCidades.Where(c => c.JogadorId == jogadorId));

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

            foreach (var cidadeId in cidadesSelecionadas ?? Array.Empty<int>())
            {
                _context.JogadorCidades.Add(new JogadorCidade { JogadorId = jogadorId, CidadeId = cidadeId });
            }

            await _context.SaveChangesAsync();
        }
    }
}
