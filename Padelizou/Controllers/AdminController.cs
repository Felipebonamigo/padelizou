using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;

namespace padelizou.Controllers
{
    // Painel do administrador: hoje só gerencia donos de clube e a lista de administradores do
    // sistema — fundação pra futuras telas administrativas reaproveitarem o mesmo gate.
    [Authorize]
    public class AdminController : Controller
    {
        private readonly DbPadelContext _context;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly IConfiguration _configuration;
        private readonly RegistroResultadosSettings _registro;
        private readonly ILogger<AdminController>? _logger;

        public AdminController(DbPadelContext context, IPushNotificationService pushNotificationService,
            IConfiguration configuration,
            Microsoft.Extensions.Options.IOptions<RegistroResultadosSettings> registro,
            ILogger<AdminController>? logger = null)
        {
            _context = context;
            _pushNotificationService = pushNotificationService;
            _configuration = configuration;
            _registro = registro.Value;
            _logger = logger;
        }

        // Qualquer administrador (raiz ou nomeado) — usado pelas ações que administradores
        // nomeados também podem fazer, como atribuir dono de clube.
        private async Task<Jogador?> ObterJogadorAdminAsync()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId)) return null;

            var jogador = await _context.Jogadores.FindAsync(userId);
            return jogador != null && (jogador.IsAdminGeral || jogador.IsAdminRaiz) ? jogador : null;
        }

        // Só o administrador raiz — usado só pra gerenciar quem é IsAdminGeral.
        private async Task<Jogador?> ObterJogadorAdminRaizAsync()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId)) return null;

            var jogador = await _context.Jogadores.FindAsync(userId);
            return jogador != null && jogador.IsAdminRaiz ? jogador : null;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var admin = await ObterJogadorAdminAsync();
            if (admin == null) return RedirectToAction("Perfil", "Auth");

            ViewBag.EhRaiz = admin.IsAdminRaiz;
            return View();
        }

        // ── Pedidos de "nós registramos os resultados para você" ──────────────────────────
        // O organizador pede a equipe; aqui é onde a gente olha se dá e responde. O valor
        // sai SÓ nesta resposta: antes de saber quem vai e de onde vem, qualquer preço na
        // tela do organizador seria chute virando promessa.

        [HttpGet]
        public async Task<IActionResult> RegistroResultados()
        {
            var admin = await ObterJogadorAdminAsync();
            if (admin == null) return RedirectToAction("Perfil", "Auth");

            var pedidos = await _context.SolicitacoesRegistroResultados
                .Include(s => s.Torneio)
                .OrderBy(s => s.Status == SolicitacaoRegistroResultados.Solicitada ? 0 : 1)
                .ThenBy(s => s.Torneio.DataInicio)
                .ToListAsync();

            // Nome de quem pediu, numa consulta só.
            var ids = pedidos.Select(p => p.SolicitadoPorId).Distinct().ToList();
            ViewBag.Solicitantes = await _context.Jogadores
                .Where(j => ids.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id, j => j.Nome);

            ViewBag.Config = _registro;
            return View(pedidos);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResponderRegistroResultados(
            int id, bool temDisponibilidade, int? pessoas, decimal? valor, string? resposta)
        {
            var admin = await ObterJogadorAdminAsync();
            if (admin == null) return RedirectToAction("Perfil", "Auth");

            var pedido = await _context.SolicitacoesRegistroResultados
                .Include(s => s.Torneio)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (pedido == null) return NotFound();

            var problema = RegistroDeResultados.ProblemaParaResponder(pedido.Status);
            if (problema != null)
            {
                TempData["Erro"] = problema;
                return RedirectToAction("RegistroResultados");
            }

            if (temDisponibilidade && (valor == null || valor <= 0))
            {
                TempData["Erro"] = "Informe o valor combinado para confirmar o pedido.";
                return RedirectToAction("RegistroResultados");
            }

            pedido.Status = temDisponibilidade
                ? SolicitacaoRegistroResultados.Confirmada
                : SolicitacaoRegistroResultados.SemDisponibilidade;
            pedido.PessoasConfirmadas = temDisponibilidade ? (pessoas ?? pedido.PessoasSugeridas) : null;
            pedido.ValorCombinado = temDisponibilidade ? valor : null;
            pedido.Resposta = string.IsNullOrWhiteSpace(resposta) ? null : resposta.Trim();
            pedido.RespondidaEm = DateTime.Now;
            pedido.RespondidaPorId = admin.Id;

            await _context.SaveChangesAsync();

            // O organizador está esperando esta resposta pra decidir se arruma alguém por
            // conta própria. Deixá-la só na tela seria fazê-lo descobrir tarde demais.
            try
            {
                await _pushNotificationService.EnviarParaJogadorAsync(pedido.SolicitadoPorId,
                    temDisponibilidade ? "Temos equipe para o seu torneio!" : "Sem equipe disponível",
                    temDisponibilidade
                        ? $"{pedido.Torneio.Nome}: {pedido.PessoasConfirmadas} pessoa(s), R$ {pedido.ValorCombinado:N2}."
                        : $"{pedido.Torneio.Nome}: não conseguimos equipe para essa data.",
                    Url.Action("Details", "Torneios", new { id = pedido.TorneioId }));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Falha ao avisar o organizador da resposta do pedido {PedidoId}.", pedido.Id);
            }

            TempData["Sucesso"] = temDisponibilidade ? "Pedido confirmado." : "Pedido marcado como sem disponibilidade.";
            return RedirectToAction("RegistroResultados");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConcluirRegistroResultados(int id)
        {
            var admin = await ObterJogadorAdminAsync();
            if (admin == null) return RedirectToAction("Perfil", "Auth");

            var pedido = await _context.SolicitacoesRegistroResultados.FindAsync(id);
            if (pedido == null) return NotFound();

            if (pedido.Status != SolicitacaoRegistroResultados.Confirmada)
            {
                TempData["Erro"] = "Só dá pra concluir um pedido confirmado.";
                return RedirectToAction("RegistroResultados");
            }

            pedido.Status = SolicitacaoRegistroResultados.Concluida;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Pedido concluído.";
            return RedirectToAction("RegistroResultados");
        }

        [HttpGet]
        public async Task<IActionResult> Clubes()
        {
            if (await ObterJogadorAdminAsync() == null) return RedirectToAction("Perfil", "Auth");

            var clubes = await _context.Clubes
                .Include(c => c.Dono)
                .OrderBy(c => c.Nome)
                .ToListAsync();

            return View(clubes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtribuirDono(int clubeId, int jogadorId)
        {
            if (await ObterJogadorAdminAsync() == null) return Forbid();

            var clube = await _context.Clubes.FindAsync(clubeId);
            if (clube == null) return NotFound();

            clube.DonoId = jogadorId == 0 ? null : jogadorId;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = jogadorId == 0 ? "Dono removido." : "Dono do clube atualizado.";
            return RedirectToAction("Clubes");
        }

        [HttpGet]
        public async Task<IActionResult> Administradores()
        {
            if (await ObterJogadorAdminRaizAsync() == null) return RedirectToAction("Perfil", "Auth");

            var administradores = await _context.Jogadores
                .Where(j => j.IsAdminGeral)
                .OrderBy(j => j.Nome)
                .ToListAsync();

            return View(administradores);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarAdministrador(int jogadorId)
        {
            if (await ObterJogadorAdminRaizAsync() == null) return Forbid();

            var jogador = await _context.Jogadores.FindAsync(jogadorId);
            if (jogador == null) return NotFound();

            jogador.IsAdminGeral = true;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"{jogador.Nome} agora é administrador do sistema.";
            return RedirectToAction("Administradores");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverAdministrador(int jogadorId)
        {
            if (await ObterJogadorAdminRaizAsync() == null) return Forbid();

            var jogador = await _context.Jogadores.FindAsync(jogadorId);
            if (jogador != null)
            {
                jogador.IsAdminGeral = false;
                await _context.SaveChangesAsync();
            }

            TempData["Sucesso"] = "Administrador removido.";
            return RedirectToAction("Administradores");
        }

        // O que os jogadores acharam do site. Chega tudo invisível; publicar é decisão de
        // admin, uma a uma, depois de ler.
        [HttpGet]
        public async Task<IActionResult> Feedbacks()
        {
            if (await ObterJogadorAdminAsync() == null) return RedirectToAction("Perfil", "Auth");

            var feedbacks = await _context.FeedbacksSite
                .Include(f => f.Jogador)
                .OrderByDescending(f => f.CriadoEm)
                .ToListAsync();

            ViewBag.Nps = RegrasDeFeedback.Nps(feedbacks.Select(f => f.Nota));
            ViewBag.NaoLidos = feedbacks.Count(f => !f.Lido);
            ViewBag.Publicados = feedbacks.Count(f => f.Exibir);

            return View(feedbacks);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlternarExibicaoFeedback(int id)
        {
            if (await ObterJogadorAdminAsync() == null) return RedirectToAction("Perfil", "Auth");

            var feedback = await _context.FeedbacksSite.FindAsync(id);
            if (feedback == null) return NotFound();

            feedback.Exibir = !feedback.Exibir;
            feedback.ExibidoEm = feedback.Exibir ? DateTime.Now : null;
            feedback.Lido = true;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = feedback.Exibir
                ? "Publicado — agora aparece na página inicial."
                : "Tirado do ar.";
            return RedirectToAction("Feedbacks");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarcarFeedbackLido(int id)
        {
            if (await ObterJogadorAdminAsync() == null) return RedirectToAction("Perfil", "Auth");

            var feedback = await _context.FeedbacksSite.FindAsync(id);
            if (feedback == null) return NotFound();

            feedback.Lido = !feedback.Lido;
            await _context.SaveChangesAsync();

            return RedirectToAction("Feedbacks");
        }

        // ── Comentários denunciados ────────────────────────────────────────────────────
        // A fila fica aqui, não num e-mail: pra decidir é preciso ver o texto ao lado de
        // quem escreveu, em que perfil e quem denunciou — contexto que só a tela dá.
        // As duas saídas são deliberadamente as únicas: apagar o comentário ou mantê-lo
        // (limpando o carimbo). Não existe "banir autor" aqui — punição de conta é outra
        // decisão, tomada com mais calma que um clique numa fila.

        [HttpGet]
        public async Task<IActionResult> Denuncias()
        {
            if (await ObterJogadorAdminAsync() == null) return RedirectToAction("Perfil", "Auth");

            var comentarios = await _context.ComentariosPerfil
                .Include(c => c.Autor)
                .Include(c => c.Perfil)
                .Where(c => c.DenunciadoEm != null)
                .OrderBy(c => c.DenunciadoEm)
                .ToListAsync();

            // Nome de quem denunciou, numa consulta só (DenunciadoPorId não tem FK/nav
            // de propósito — ver ComentarioPerfil). Quem excluiu a conta sai como null
            // e a tela mostra "conta excluída".
            var ids = comentarios.Where(c => c.DenunciadoPorId != null)
                .Select(c => c.DenunciadoPorId!.Value).Distinct().ToList();
            ViewBag.Denunciantes = await _context.Jogadores
                .Where(j => ids.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id, j => j.Nome);

            return View(comentarios);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApagarComentarioDenunciado(int id)
        {
            if (await ObterJogadorAdminAsync() == null) return Forbid();

            var comentario = await _context.ComentariosPerfil.FindAsync(id);
            if (comentario != null)
            {
                _context.ComentariosPerfil.Remove(comentario);
                await _context.SaveChangesAsync();
            }

            TempData["Sucesso"] = "Comentário apagado.";
            return RedirectToAction("Denuncias");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManterComentarioDenunciado(int id)
        {
            if (await ObterJogadorAdminAsync() == null) return Forbid();

            var comentario = await _context.ComentariosPerfil.FindAsync(id);
            if (comentario != null)
            {
                comentario.DenunciadoEm = null;
                comentario.DenunciadoPorId = null;
                await _context.SaveChangesAsync();
            }

            TempData["Sucesso"] = "Comentário mantido — saiu da fila.";
            return RedirectToAction("Denuncias");
        }

        // Métricas de uso: os números que dizem se o sistema está crescendo e quanto a
        // plataforma já faturou no ano (controle do teto do MEI). CriadoEm nulo = registro
        // anterior a 25/07/2026 (antes da coluna existir) — entra nos totais, não nas séries.
        [HttpGet]
        public async Task<IActionResult> Metricas()
        {
            if (await ObterJogadorAdminAsync() == null) return RedirectToAction("Perfil", "Auth");

            var agora = DateTime.Now;
            var ha7 = agora.AddDays(-7);
            var ha30 = agora.AddDays(-30);
            var inicioAno = new DateTime(agora.Year, 1, 1);

            // Início da série semanal: segunda-feira de 8 semanas atrás.
            var inicioSemanaAtual = agora.Date.AddDays(-(((int)agora.DayOfWeek + 6) % 7));
            var inicioSerie = inicioSemanaAtual.AddDays(-7 * 7);

            var vm = new MetricasAdminVM
            {
                TotalJogadores = await _context.Jogadores.CountAsync(),
                JogadoresNovos7 = await _context.Jogadores.CountAsync(j => j.CriadoEm >= ha7),
                JogadoresNovos30 = await _context.Jogadores.CountAsync(j => j.CriadoEm >= ha30),

                InscricoesNovas7 = await _context.Duplas.CountAsync(d => d.CriadoEm >= ha7)
                    + await _context.InscricoesAmericanas.CountAsync(i => i.CriadoEm >= ha7),
                InscricoesNovas30 = await _context.Duplas.CountAsync(d => d.CriadoEm >= ha30)
                    + await _context.InscricoesAmericanas.CountAsync(i => i.CriadoEm >= ha30),

                PagamentosConfirmados30 = await _context.Pagamentos
                    .CountAsync(p => p.Status == "Confirmado" && p.ConfirmadoEm >= ha30),
                ValorConfirmado30 = await _context.Pagamentos
                    .Where(p => p.Status == "Confirmado" && p.ConfirmadoEm >= ha30)
                    .SumAsync(p => (decimal?)p.Valor) ?? 0,

                JogadoresComApp = await _context.PushSubscriptionsJogador
                    .Select(s => s.JogadorId).Distinct().CountAsync(),
                TorneiosTotal = await _context.Torneios.CountAsync(),
                TorneiosAtivos = await _context.Torneios.CountAsync(t => t.Status != "Finalizado"),

                ComissaoAno = await _context.Pagamentos
                    .Where(p => p.Status == "Confirmado" && p.ConfirmadoEm >= inicioAno)
                    .SumAsync(p => (decimal?)p.Comissao) ?? 0,
                TetoMei = _configuration.GetValue<decimal?>("Mei:TetoAnual") ?? 81000m,
            };

            // Série semanal — poucas linhas por semana, agrupar em memória é suficiente.
            var cadastros = await _context.Jogadores
                .Where(j => j.CriadoEm >= inicioSerie)
                .Select(j => j.CriadoEm!.Value).ToListAsync();
            var inscricoes = (await _context.Duplas
                    .Where(d => d.CriadoEm >= inicioSerie)
                    .Select(d => d.CriadoEm!.Value).ToListAsync())
                .Concat(await _context.InscricoesAmericanas
                    .Where(i => i.CriadoEm >= inicioSerie)
                    .Select(i => i.CriadoEm!.Value).ToListAsync())
                .ToList();
            var pagamentos = await _context.Pagamentos
                .Where(p => p.Status == "Confirmado" && p.ConfirmadoEm >= inicioSerie)
                .Select(p => new { Data = p.ConfirmadoEm!.Value, p.Valor }).ToListAsync();

            for (var inicio = inicioSerie; inicio <= inicioSemanaAtual; inicio = inicio.AddDays(7))
            {
                var fim = inicio.AddDays(7);
                vm.Semanas.Add(new SemanaMetricaVM
                {
                    Inicio = inicio,
                    Cadastros = cadastros.Count(d => d >= inicio && d < fim),
                    Inscricoes = inscricoes.Count(d => d >= inicio && d < fim),
                    Pagamentos = pagamentos.Count(p => p.Data >= inicio && p.Data < fim),
                    Valor = pagamentos.Where(p => p.Data >= inicio && p.Data < fim).Sum(p => p.Valor),
                });
            }

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarNotificacaoTeste()
        {
            if (await ObterJogadorAdminAsync() == null) return Forbid();

            var quantidade = await _pushNotificationService.EnviarParaTodosInscritosAsync(
                "Padelizou",
                "Notificação de teste — se você recebeu isso, o app instalado no seu celular está funcionando certinho.");

            TempData["Sucesso"] = quantidade > 0
                ? $"Notificação de teste enviada pra {quantidade} jogador(es) com o app instalado."
                : "Ninguém tem o app instalado com notificações ativas ainda.";
            return RedirectToAction("Index");
        }

        // Refaz as imagens antigas no padrão novo (o ImagemEnviada cuida das que chegam daqui
        // pra frente). Idempotente: rodar de novo pula tudo que já está no tamanho certo.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OtimizarImagens([FromServices] OtimizacaoDeImagens otimizacao)
        {
            if (await ObterJogadorAdminAsync() == null) return Forbid();

            var r = await otimizacao.RodarAsync();

            if (r.Otimizadas == 0)
            {
                TempData["Sucesso"] = r.ComProblema > 0
                    ? $"Nenhuma imagem precisou mudar. {r.ComProblema} não puderam ser lidas."
                    : "Nenhuma imagem precisou mudar — está tudo já no tamanho certo.";
            }
            else
            {
                var texto = $"{r.Otimizadas} imagem(ns) otimizada(s): "
                          + $"{OtimizacaoDeImagens.Resultado.EmMegas(r.BytesAntes)} viraram "
                          + $"{OtimizacaoDeImagens.Resultado.EmMegas(r.BytesDepois)} "
                          + $"({OtimizacaoDeImagens.Resultado.EmMegas(r.BytesEconomizados)} a menos).";

                if (r.ComProblema > 0) texto += $" {r.ComProblema} não puderam ser lidas.";

                TempData["Sucesso"] = texto;
            }

            return RedirectToAction("Index");
        }
    }
}
