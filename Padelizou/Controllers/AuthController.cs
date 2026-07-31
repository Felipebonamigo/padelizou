using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
        private readonly TravaDeEntrada _trava;

        public AuthController(DbPadelContext context, IWebHostEnvironment env, IPasswordHasher<Jogador> passwordHasher, IEstatisticasService estatisticas, IEmailService email, ILogger<AuthController> logger, IOptions<SuporteSettings> suporte, TravaDeEntrada trava)
        {
            _context = context;
            _env = env;
            _passwordHasher = passwordHasher;
            _estatisticas = estatisticas;
            _email = email;
            _logger = logger;
            _suporte = suporte.Value;
            _trava = trava;
        }

        // Salva a foto e devolve o caminho, ou null se não deu.
        //
        // Nunca lança: a foto é opcional, e derrubar o cadastro por causa dela custa caro —
        // a pessoa preenche um formulário longo, escolhe a foto e recebe "Ops! Algo deu
        // errado", perdendo tudo. Foi exatamente o que aconteceu quando a pasta de uploads
        // do dev estava sem permissão de escrita.
        private Task<ResultadoDaImagem> SalvarFotoPerfilAsync(IFormFile? foto) =>
            ImagemEnviada.SalvarAsync(foto, _env.WebRootPath, "fotos-perfil", FormatoDeImagem.FotoPerfil, _logger);

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
        // returnUrl: quem foi mandado pra cá por uma tela que exige login (a inscrição no
        // torneio, por exemplo) volta pra ela depois de entrar. Sem isso a pessoa caía no
        // perfil e tinha que achar o torneio de novo — no meio de uma inscrição, é o
        // suficiente pra desistir.
        public IActionResult Login(string? returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // ── Esqueci minha senha ───────────────────────────────────────────────────────────
        [AllowAnonymous]
        [HttpGet]
        public IActionResult EsqueciSenha() => View();

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(TravaDeEntrada.PoliticaPorIp)]
        public async Task<IActionResult> EsqueciSenha(string email)
        {
            var jogador = await BuscaJogador.PorIdentificadorAsync(_context, email);

            if (jogador != null && !string.IsNullOrWhiteSpace(jogador.Email))
            {
                RecuperacaoSenha.Emitir(jogador, DateTime.Now);
                await _context.SaveChangesAsync();

                var link = Url.Action("RedefinirSenha", "Auth",
                    new { token = jogador.TokenRecuperacao }, Request.Scheme);

                try
                {
                    await _email.EnviarAsync(jogador.Email!, jogador.Nome, "Recuperar senha - Padelizou",
                        $@"<p>Olá {jogador.ComoChamar},</p>
                           <p>Clique no link abaixo pra criar uma senha nova. Ele vale por 1 hora:</p>
                           <p><a href=""{link}"">Criar nova senha</a></p>
                           <p>Se não foi você que pediu, ignore este e-mail — sua senha continua a mesma.</p>");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Falha ao enviar e-mail de recuperação de senha.");
                }
            }

            // Mesma resposta com ou sem conta: dizer "esse e-mail não existe" entregaria de
            // graça quem está cadastrado no site.
            ViewBag.Enviado = true;
            return View();
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> RedefinirSenha(string? token)
        {
            var jogador = await AcharPorTokenAsync(token);
            if (jogador == null)
            {
                ViewBag.Erro = "Esse link expirou ou já foi usado. Peça um novo.";
                return View("EsqueciSenha");
            }

            ViewBag.Token = token;
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [EnableRateLimiting(TravaDeEntrada.PoliticaPorIp)]
        public async Task<IActionResult> RedefinirSenha(string? token, string? senha)
        {
            var jogador = await AcharPorTokenAsync(token);
            if (jogador == null)
            {
                ViewBag.Erro = "Esse link expirou ou já foi usado. Peça um novo.";
                return View("EsqueciSenha");
            }

            var problema = RecuperacaoSenha.ProblemaComASenha(senha);
            if (problema != null)
            {
                ViewBag.Erro = problema;
                ViewBag.Token = token;
                return View();
            }

            jogador.SenhaHash = _passwordHasher.HashPassword(jogador, senha!);
            RecuperacaoSenha.Consumir(jogador);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Senha alterada! Entre com a senha nova.";
            return RedirectToAction("Login");
        }

        private async Task<Jogador?> AcharPorTokenAsync(string? token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;

            var jogador = await _context.Jogadores.FirstOrDefaultAsync(j => j.TokenRecuperacao == token);
            return jogador != null && RecuperacaoSenha.TokenValido(jogador, token, DateTime.Now) ? jogador : null;
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string senha, string? returnUrl = null)
        {
            // O destino tem que sobreviver às recusas: sem isto, errar a senha uma vez apagava
            // a volta e a pessoa terminava no perfil em vez do torneio que ela queria.
            ViewBag.ReturnUrl = returnUrl;

            // Trava de força-bruta POR CONTA, não por IP: no dia de torneio o clube
            // inteiro sai pelo mesmo Wi-Fi, e uma janela por IP trancaria gente legítima
            // na hora errada (ver TravaDeEntrada). Checada ANTES de conferir a senha —
            // conta trancada recusa até a senha certa, senão a trava não trava nada.
            if (_trava.Bloqueada(email, DateTime.Now))
            {
                ViewBag.Erro = "Muitas tentativas seguidas. Espere alguns minutos e tente de novo.";
                return View();
            }

            // Aceita e-mail OU login, sem diferenciar maiúsculas (ver BuscaJogador).
            // Antes só o e-mail servia, e exato — quem se cadastrou com login nunca
            // conseguia entrar por ele.
            var jogador = await BuscaJogador.PorIdentificadorAsync(_context, email);

            if (jogador == null || string.IsNullOrEmpty(jogador.SenhaHash) ||
                _passwordHasher.VerifyHashedPassword(jogador, jogador.SenhaHash, senha) == PasswordVerificationResult.Failed)
            {
                _trava.RegistrarFalha(email, DateTime.Now);
                ViewBag.Erro = "Login ou senha incorretos.";
                return View();
            }

            _trava.Zerar(email);
            var claims = IdentidadeJogador.ClaimsDe(jogador);

            var identidade = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identidade);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            // IsLocalUrl é o que impede o login de virar trampolim pra outro site: o returnUrl
            // vem da barra de endereços, e "entre aqui e você cai lá" é golpe clássico.
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);

            return RedirectToAction("Perfil");
        }

        // 3. TELA DE PERFIL (Só entra se estiver logado!)
        [Authorize]
        public async Task<IActionResult> Perfil()
        {
            // Pega o ID do cara que está logado no cookie. O `!` vale porque a ação é [Authorize]
            // e quem emite o cookie é o ClaimsDe() aqui do lado, que sempre grava o identificador.
            var jogadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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

        // Dados para a seção "Meu time" do editar perfil: times disponíveis (quem não
        // administra nenhum escolhe da lista), clubes (sede opcional) e o time que este
        // jogador administra (se houver).
        private async Task PopularDadosTimeAsync(int jogadorId)
        {
            ViewBag.Times = await _context.Times.OrderBy(t => t.Nome).ToListAsync();
            ViewBag.Clubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();
            ViewBag.MeuTimeDono = await MeuTimeAsync(jogadorId);
        }

        // O time que este jogador administra. Um time pode ter vários administradores, mas
        // esta tela cuida de "o meu" — quem administra mais de um usa a página do time.
        private async Task<Time?> MeuTimeAsync(int jogadorId) =>
            await _context.TimeAdministradores
                .Where(a => a.JogadorId == jogadorId)
                .OrderBy(a => a.ConcedidoEm)
                .Select(a => a.Time)
                .FirstOrDefaultAsync();

        // Salva o logo do time em wwwroot/uploads/logos-time e devolve o caminho relativo,
        // ou null se a imagem não pôde ser processada.
        private Task<ResultadoDaImagem> SalvarLogoTimeAsync(IFormFile arquivo) =>
            ImagemEnviada.SalvarAsync(arquivo, _env.WebRootPath, "logos-time", FormatoDeImagem.LogoTime, _logger);

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

            // Coluna varchar(100): o Postgres recusa o que passa, e o perfil voltaria em 500.
            if (LimitesDeTexto.Problema(nome, LimitesDeTexto.NomeDeJogador, "O nome") is { } nomeLongo)
            {
                ViewBag.Erro = nomeLongo;
                await PopularDadosTimeAsync(jogadorId);
                return View(jogador);
            }

            email = email.Trim();

            // Sem esta checagem dava pra pôr o e-mail de outra pessoa na própria conta, e
            // trancá-la fora da dela: a entrada casa e-mail OU login com FirstOrDefault, então
            // com duas linhas respondendo pelo mesmo e-mail o banco escolhe sozinho qual —
            // a vítima não entra (a senha confere contra a outra conta) e o link de recuperar
            // senha vai pro e-mail de quem ocupou.
            if (await IdentidadeJogador.EmUsoAsync(_context, email, jogadorId))
            {
                ViewBag.Erro = "Esse e-mail já está em uso por outra conta.";
                await PopularDadosTimeAsync(jogadorId);
                return View(jogador);
            }

            // Se marcou "sou dono", precisa do nome do time (ao menos na primeira vez).
            var meuTime = await MeuTimeAsync(jogadorId);
            if (ehDonoTime && meuTime == null && string.IsNullOrWhiteSpace(nomeTime))
            {
                ViewBag.Erro = "Informe o nome do time (você marcou que é dono de um time).";
                await PopularDadosTimeAsync(jogadorId);
                return View(jogador);
            }

            var fotoSalva = await SalvarFotoPerfilAsync(foto);
            if (fotoSalva.Salvou) jogador.FotoPerfil = fotoSalva.Caminho;
            else if (fotoSalva.DeuErro) TempData["ErroImagem"] = fotoSalva.Erro;

            jogador.Nome = nome;
            // Apelido em branco volta a ser nulo — "sem apelido" e "apelido vazio" viram
            // a mesma coisa, senão ComoChamar teria que testar string vazia em todo lugar.
            jogador.Apelido = string.IsNullOrWhiteSpace(apelido) ? null : apelido.Trim();
            jogador.Email = email;
            jogador.Celular = Documentos.SomenteDigitosOuNulo(celular);
            // A cidade do perfil é texto livre e alimenta o filtro do ranking. Sem arrumar na
            // entrada, "Gravataí ", "Gravataí" e "gravataí " viram três opções na lista. Isto
            // resolve espaço sobrando e duplicado; acento continua por conta de quem digita.
            jogador.Cidade = string.IsNullOrWhiteSpace(cidade) ? null : NomeDeCidade.Arrumar(cidade);
            jogador.Estado = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim();

            // Guardado ANTES de sobrescrever: quem acabou de marcar "sou professor" vai direto
            // definir a cidade, em vez de voltar pro perfil achando que terminou. Marcar a
            // caixinha não coloca ninguém na tela de marcar aula — falta cidade e local.
            var acabouDeVirarProfessor = isProfessor && !jogador.IsProfessor;
            jogador.IsProfessor = isProfessor;

            // --- Meu time ---
            if (ehDonoTime)
            {
                bool acabouDeCriar = meuTime == null;
                if (meuTime == null)
                {
                    meuTime = new Time { Nome = nomeTime!.Trim() };
                    _context.Times.Add(meuTime);
                }
                else if (!string.IsNullOrWhiteSpace(nomeTime))
                {
                    meuTime.Nome = nomeTime.Trim();
                }
                meuTime.ClubeId = clubeSedeId; // clube sede é opcional
                if (logoTime != null && logoTime.Length > 0)
                {
                    // Se o processamento falhar, o logo que já estava lá continua — trocar por
                    // nulo apagaria o escudo do time por causa de um envio ruim.
                    var logoSalvo = await SalvarLogoTimeAsync(logoTime);
                    if (logoSalvo.Salvou) meuTime.Logo = logoSalvo.Caminho;
                    else if (logoSalvo.DeuErro) TempData["ErroImagem"] = logoSalvo.Erro;
                }
                await _context.SaveChangesAsync(); // garante o Id do time recém-criado

                // Quem cria o time é o primeiro administrador dele. Só vale pra time NOVO:
                // os 44 importados do ranking já existem, e entrar num deles pelo nome não
                // dá cargo nenhum — quem manda lá é designado por um admin do Padelizou.
                if (acabouDeCriar)
                {
                    _context.TimeAdministradores.Add(new TimeAdministrador
                    {
                        TimeId = meuTime.Id,
                        JogadorId = jogadorId,
                        ConcedidoPorId = jogadorId,   // criou, logo se concedeu
                        ConcedidoEm = DateTime.Now,
                    });
                }

                jogador.TimeId = meuTime.Id;       // quem administra também veste a camisa
            }
            else
            {
                // Não é dono: entra num time existente (ou em nenhum).
                jogador.TimeId = timeId;
            }

            await _context.SaveChangesAsync();

            // Renova o cookie com nome/e-mail atualizados (o chip do usuário na navbar lê da claim,
            // não do banco — sem isso ficaria com o nome antigo até o próximo login).
            var claims = IdentidadeJogador.ClaimsDe(jogador);
            var identidade = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identidade));

            TempData["Sucesso"] = "Perfil atualizado!";

            // Recém-chegado ao ofício vai direto pro que falta. O painel do professor cobraria
            // isso de qualquer forma, mas pedir agora — enquanto a pessoa ainda está mexendo no
            // cadastro — é bem menos irritante do que interceptá-la depois.
            if (acabouDeVirarProfessor)
            {
                TempData["AvisoCadastroProfessor"] =
                    CadastroDeProfessor.MensagemPara(PendenciaDoProfessor.Cidade);
                return RedirectToAction("MinhasCidades", "Aulas");
            }

            return RedirectToAction("Perfil");
        }

        // ── Excluir a própria conta (LGPD) ────────────────────────────────────────────────

        // Quantos torneios ainda não finalizados dependem SÓ desta pessoa como organizador.
        private async Task<int> TorneiosOrfaosSeEuSairAsync(int jogadorId)
        {
            var meus = await _context.TorneioOrganizadores
                .Where(o => o.JogadorId == jogadorId)
                .Select(o => o.TorneioId)
                .ToListAsync();

            if (meus.Count == 0) return 0;

            var abertos = await _context.Torneios
                .Where(t => meus.Contains(t.Id) && t.Status != "Finalizado")
                .Select(t => t.Id)
                .ToListAsync();

            if (abertos.Count == 0) return 0;

            var comOutroOrganizador = await _context.TorneioOrganizadores
                .Where(o => abertos.Contains(o.TorneioId) && o.JogadorId != jogadorId)
                .Select(o => o.TorneioId)
                .Distinct()
                .ToListAsync();

            return abertos.Count(id => !comOutroOrganizador.Contains(id));
        }

        private async Task<string?> ProblemaParaExcluirAsync(Jogador jogador)
        {
            var souAdmin = jogador.IsAdminGeral || jogador.IsAdminRaiz;
            var outrosAdmins = souAdmin
                ? await _context.Jogadores.CountAsync(j =>
                    j.Id != jogador.Id && j.ExcluidoEm == null && (j.IsAdminGeral || j.IsAdminRaiz))
                : 1;

            return ExclusaoDeConta.ProblemaParaExcluir(
                ehUnicoAdminDoSistema: souAdmin && outrosAdmins == 0,
                torneiosEmQueEhUnicoOrganizador: await TorneiosOrfaosSeEuSairAsync(jogador.Id));
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ExcluirConta()
        {
            var jogadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var jogador = await _context.Jogadores.FindAsync(jogadorId);
            if (jogador == null) return NotFound();

            ViewBag.Problema = await ProblemaParaExcluirAsync(jogador);
            return View(jogador);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirConta(string senha, bool confirmo)
        {
            var jogadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var jogador = await _context.Jogadores.FindAsync(jogadorId);
            if (jogador == null) return NotFound();

            async Task<IActionResult> Recusar(string erro)
            {
                ViewBag.Erro = erro;
                ViewBag.Problema = await ProblemaParaExcluirAsync(jogador);
                return View(jogador);
            }

            if (!confirmo)
                return await Recusar("Marque a confirmação para excluir a conta.");

            // Senha antes de qualquer coisa: sem isto, um celular destravado esquecido na
            // mesa apagaria a conta de alguém em dois toques.
            if (string.IsNullOrEmpty(jogador.SenhaHash) ||
                _passwordHasher.VerifyHashedPassword(jogador, jogador.SenhaHash, senha ?? "")
                    == PasswordVerificationResult.Failed)
            {
                return await Recusar("Senha incorreta.");
            }

            var problema = await ProblemaParaExcluirAsync(jogador);
            if (problema != null) return await Recusar(problema);

            // 1. Some o que é conteúdo pessoal e não é histórico de ninguém.
            //    Texto livre é o que mais carrega dado pessoal por acidente (um comentário
            //    pode citar telefone, endereço, o nome de um filho), e é dela.
            _context.ComentariosPerfil.RemoveRange(
                _context.ComentariosPerfil.Where(c => c.AutorId == jogadorId));
            _context.FeedbacksSite.RemoveRange(
                _context.FeedbacksSite.Where(f => f.JogadorId == jogadorId));

            // Preferências, avisos abertos, quem ela seguia e os aparelhos que recebiam push:
            // nada disso é histórico de terceiro, e mantê-los seria continuar perfilando
            // alguém que pediu pra sair.
            _context.JogadorCategorias.RemoveRange(_context.JogadorCategorias.Where(x => x.JogadorId == jogadorId));
            _context.JogadorClubes.RemoveRange(_context.JogadorClubes.Where(x => x.JogadorId == jogadorId));
            _context.JogadorCidades.RemoveRange(_context.JogadorCidades.Where(x => x.JogadorId == jogadorId));
            _context.JogadorDiasHorarios.RemoveRange(_context.JogadorDiasHorarios.Where(x => x.JogadorId == jogadorId));
            _context.PushSubscriptionsJogador.RemoveRange(_context.PushSubscriptionsJogador.Where(x => x.JogadorId == jogadorId));
            _context.SeguidoresJogador.RemoveRange(_context.SeguidoresJogador.Where(x => x.SeguidorId == jogadorId));
            _context.AvisosJogo.RemoveRange(_context.AvisosJogo.Where(x => x.CriadorId == jogadorId));
            _context.AvisosParceiro.RemoveRange(_context.AvisosParceiro.Where(x => x.CriadorId == jogadorId));

            // Sai da administração de times: o time continua, sem ela.
            _context.TimeAdministradores.RemoveRange(_context.TimeAdministradores.Where(x => x.JogadorId == jogadorId));

            // 2. Apaga o arquivo da foto do disco. Só tirar o caminho do banco deixaria o
            //    rosto da pessoa servido numa URL pública pra sempre.
            ApagarArquivoDeUpload(jogador.FotoPerfil);

            // 3. Raspa a identidade, mantendo a linha viva pro histórico dos outros.
            ExclusaoDeConta.Anonimizar(jogador, DateTime.Now);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Conta {JogadorId} excluída a pedido do titular (LGPD).", jogadorId);

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData["Sucesso"] = "Sua conta foi excluída. Obrigado por ter jogado com a gente.";
            return RedirectToAction("Index", "Home");
        }

        private void ApagarArquivoDeUpload(string? caminhoRelativo)
        {
            if (string.IsNullOrWhiteSpace(caminhoRelativo)) return;

            try
            {
                var caminho = Path.Combine(_env.WebRootPath, caminhoRelativo.TrimStart('/', '\\')
                    .Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(caminho)) System.IO.File.Delete(caminho);
            }
            catch (Exception ex)
            {
                // Não pode impedir a exclusão: o dado no banco é o que identifica a pessoa,
                // e um arquivo órfão é problema de faxina, não de privacidade em aberto.
                _logger.LogWarning(ex, "Falha ao apagar arquivo de upload {Caminho}.", caminhoRelativo);
            }
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
            await PopularCatalogosAsync();
            return View();
        }

        private async Task PopularCatalogosAsync()
        {
            ViewBag.CatalogoCategorias = await _context.CategoriasPadrao.OrderBy(c => c.Id).ToListAsync();
            ViewBag.CatalogoClubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();
            ViewBag.CatalogoCidades = await _context.Cidades.OrderBy(c => c.Nome).ToListAsync();
            ViewBag.CatalogoTimes = await _context.Times.OrderBy(t => t.Nome).ToListAsync();
        }

        // 6. RECEBE OS DADOS DE CADASTRO UNIFICADO, A FOTO E AS PREFERÊNCIAS
        [HttpPost]
        [EnableRateLimiting(TravaDeEntrada.PoliticaCadastro)]
        public async Task<IActionResult> Cadastro(
            string nome, string cpf, string login, string email, string senha, string? celular, bool isProfessor, IFormFile foto,
            string? ladoQuadra, string? lateralidade, string? instagram, bool notificarEmail, bool notificarWhatsApp,
            int[]? categoriasSelecionadas, int[]? clubesSelecionados, string[]? diasHorariosSelecionados,
            string? apelido = null, int[]? cidadesSelecionadas = null, string? novoClubeNome = null,
            string? novaCidadeNome = null, string? novaCidadeEstado = null,
            int? timeId = null, string? nomeTime = null)
        {
            if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(cpf) ||
                string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
            {
                ViewBag.Erro = "Preencha nome, CPF, login, e-mail e senha pra finalizar o cadastro.";
                await PopularCatalogosAsync();
                return View();
            }

            login = login.Trim();
            email = email.Trim();

            // Cadastro recusado NÃO apaga o que a pessoa digitou: o formulário volta
            // preenchido, com o erro por cima. Sem isso a recusa parecia sumiço — a pessoa
            // via a tela limpa, achava que deu certo e ia embora sem conta (aconteceu no
            // teste de 29/07). A senha é a exceção: senha nunca volta pro HTML.
            ViewBag.FormNome = nome;
            ViewBag.FormApelido = apelido;
            ViewBag.FormCpf = cpf;
            ViewBag.FormLogin = login;
            ViewBag.FormEmail = email;
            ViewBag.FormCelular = celular;
            ViewBag.FormIsProfessor = isProfessor;

            // Nome e login antes de qualquer consulta: as duas colunas são varchar e o Postgres
            // RECUSA o que passa do tamanho (não corta), então sem isto o cadastro morria com
            // erro 500 e a pessoa perdia tudo — no primeiro contato dela com o site.
            var erroDeTamanho = LimitesDeTexto.Problema(nome, LimitesDeTexto.NomeDeJogador, "O nome")
                                ?? IdentidadeJogador.ValidarLogin(login);
            if (erroDeTamanho != null)
            {
                ViewBag.Erro = erroDeTamanho;
                await PopularCatalogosAsync();
                return View();
            }

            // Normaliza antes de qualquer consulta: o CPF é a chave usada pra reconhecer quem
            // já jogou torneio, e "111.444.777-35" nunca casaria com o "11144477735" gravado
            // na inscrição — criaria um segundo cadastro pra mesma pessoa.
            cpf = Documentos.SomenteDigitos(cpf);
            celular = Documentos.SomenteDigitosOuNulo(celular);

            if (!Documentos.CpfTemFormatoValido(cpf))
            {
                ViewBag.Erro = "CPF inválido — informe os 11 números.";
                await PopularCatalogosAsync();
                return View();
            }

            // 2. Verifica se o CPF já existe (se ele já jogou um torneio antes).
            // Vem ANTES das checagens de unicidade porque é o que define quem é "eu mesmo":
            // quem reivindica a própria conta de pré-cadastro não pode ser barrado pelo
            // e-mail que o organizador já gravou nela.
            var jogador = await _context.Jogadores.FirstOrDefaultAsync(j => j.Cpf == cpf);

            // O CPF já existe. Duas situações MUITO diferentes:
            //
            // (a) jogador criado por um organizador ao inscrever a dupla, que nunca teve
            //     senha — está reivindicando a própria conta, e isso é legítimo;
            // (b) conta com senha, de alguém que já usa o site.
            //
            // No caso (b), deixar o cadastro sobrescrever a senha era uma tomada de conta:
            // CPF não é segredo no Brasil, e quem soubesse o seu entrava como você, trocava
            // o e-mail e pronto. Agora esse caminho manda pra recuperação de senha.
            if (jogador != null && !string.IsNullOrEmpty(jogador.SenhaHash))
            {
                ViewBag.Erro = "Já existe uma conta com esse CPF. Entre com sua senha, ou use "
                             + "\"Esqueci minha senha\" se não lembrar dela.";
                await PopularCatalogosAsync();
                return View();
            }

            // Login e e-mail são checados contra os DOIS campos (ver IdentidadeJogador): a
            // entrada aceita e-mail OU login, então um login igual ao e-mail de outra pessoa
            // deixaria a consulta ambígua — e a vítima sem entrar e sem recuperar a senha.
            if (await IdentidadeJogador.EmUsoAsync(_context, login, jogador?.Id))
            {
                ViewBag.Erro = "Já foi criado um login com esse nome — escolha outro.";
                await PopularCatalogosAsync();
                return View();
            }

            if (await IdentidadeJogador.EmUsoAsync(_context, email, jogador?.Id))
            {
                ViewBag.Erro = "Já tem alguém cadastrado com esse e-mail. Entre com ele, ou use "
                             + "\"Esqueci minha senha\" se não lembrar da senha.";
                await PopularCatalogosAsync();
                return View();
            }

            // A foto é opcional e não pode impedir o cadastro: se falhar, entra vazia.
            // Fica depois das validações pra um cadastro recusado não deixar arquivo órfão.
            var fotoDoCadastro = await SalvarFotoPerfilAsync(foto);
            string caminhoDaFotoParaBanco = fotoDoCadastro.Caminho ?? "";
            if (fotoDoCadastro.DeuErro) TempData["ErroImagem"] = fotoDoCadastro.Erro + " Sua conta foi criada — dá pra colocar a foto depois em Editar Perfil.";

            if (jogador != null)
            {
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
            await AtualizarPreferenciasAsync(jogador.Id, categoriasSelecionadas, clubesSelecionados,
                diasHorariosSelecionados, cidadesSelecionadas, novoClubeNome, novaCidadeNome, novaCidadeEstado);
            await DefinirTimeAsync(jogador, timeId, nomeTime);

            // 3. Loga o usuário automaticamente e manda pro Perfil
            var claims = IdentidadeJogador.ClaimsDe(jogador);
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
            int[]? categoriasSelecionadas, int[]? clubesSelecionados, string[]? diasHorariosSelecionados, int[]? cidadesSelecionadas,
            string? novoClubeNome = null, string? novaCidadeNome = null, string? novaCidadeEstado = null)
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

            await AtualizarPreferenciasAsync(jogadorId, categoriasSelecionadas, clubesSelecionados,
                diasHorariosSelecionados, cidadesSelecionadas, novoClubeNome, novaCidadeNome, novaCidadeEstado);

            TempData["Sucesso"] = "Preferências atualizadas!";
            return RedirectToAction("Preferencias");
        }

        // Entra num time existente ou cria o seu.
        //
        // **Cada jogador só pode criar um time.** Sem essa trava, a mesma pessoa criaria
        // "Nata Padel", "Nata Padel 2"... e a listagem de times viraria lixo. Entrar em time
        // dos outros não tem limite — só criar.
        private async Task DefinirTimeAsync(Jogador jogador, int? timeId, string? nomeTime)
        {
            nomeTime = nomeTime?.Trim();

            if (!string.IsNullOrWhiteSpace(nomeTime))
            {
                var jaTenhoUm = await _context.TimeAdministradores.AnyAsync(a => a.JogadorId == jogador.Id);
                if (jaTenhoUm) return;

                var alvo = nomeTime.ToLower();
                var existente = await _context.Times.FirstOrDefaultAsync(t => t.Nome.ToLower() == alvo);
                if (existente != null)
                {
                    // Alguém já criou um time com esse nome: entra nele em vez de duplicar.
                    // Sem cargo nenhum — é isto que impede que digitar "SINDAQUA" no cadastro
                    // dê a alguém o comando de um dos times importados do ranking.
                    jogador.TimeId = existente.Id;
                }
                else
                {
                    if (nomeTime.Length > CatalogoLocais.TamanhoMaximoNome)
                        nomeTime = nomeTime[..CatalogoLocais.TamanhoMaximoNome];

                    var time = new Time { Nome = nomeTime };
                    _context.Times.Add(time);
                    await _context.SaveChangesAsync();

                    // Criou um time novo: é o primeiro administrador dele.
                    _context.TimeAdministradores.Add(new TimeAdministrador
                    {
                        TimeId = time.Id,
                        JogadorId = jogador.Id,
                        ConcedidoPorId = jogador.Id,
                        ConcedidoEm = DateTime.Now,
                    });
                    jogador.TimeId = time.Id;
                }
            }
            else if (timeId.HasValue && await _context.Times.AnyAsync(t => t.Id == timeId.Value))
            {
                jogador.TimeId = timeId.Value;
            }

            await _context.SaveChangesAsync();
        }

        // Substitui (limpa e recria) as preferências de categoria/clube/dia-horário/cidade do jogador.
        // Os "novo*" permitem cadastrar clube e cidade que ainda não existem, direto do formulário.
        private async Task AtualizarPreferenciasAsync(
            int jogadorId, int[]? categoriasSelecionadas, int[]? clubesSelecionados, string[]? diasHorariosSelecionados,
            int[]? cidadesSelecionadas = null, string? novoClubeNome = null,
            string? novaCidadeNome = null, string? novaCidadeEstado = null)
        {
            var clubeNovo = await CatalogoLocais.AcharOuCriarClubeAsync(_context, novoClubeNome);
            if (clubeNovo != null)
            {
                clubesSelecionados = (clubesSelecionados ?? Array.Empty<int>()).Append(clubeNovo.Id).Distinct().ToArray();
            }

            var cidadeNova = await CatalogoLocais.AcharOuCriarCidadeAsync(_context, novaCidadeNome, novaCidadeEstado);
            if (cidadeNova != null)
            {
                cidadesSelecionadas = (cidadesSelecionadas ?? Array.Empty<int>()).Append(cidadeNova.Id).Distinct().ToArray();
            }

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
