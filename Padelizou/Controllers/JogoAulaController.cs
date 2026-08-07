using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace padelizou.Controllers
{
    // Jogo Aula — professor publica uma aula/turma por categoria, com inscrição e lista de
    // espera dentro do app, notificando quem tem NotificarJogoAula marcado.
    [Authorize]
    public class JogoAulaController : Controller
    {
        private readonly DbPadelContext _context;
        private readonly IEmailService _emailService;
        private readonly IPushNotificationService _pushService;
        private readonly IPagamentoInscricaoService _pagamentos;
        private readonly PlanoProfessorSettings _plano;
        private readonly ILogger<JogoAulaController> _logger;

        public JogoAulaController(DbPadelContext context, IEmailService emailService,
            IPushNotificationService pushService, IPagamentoInscricaoService pagamentos,
            Microsoft.Extensions.Options.IOptions<PlanoProfessorSettings> plano,
            ILogger<JogoAulaController> logger)
        {
            _context = context;
            _emailService = emailService;
            _pushService = pushService;
            _pagamentos = pagamentos;
            _plano = plano.Value;
            _logger = logger;
        }

        private int ObterJogadorIdLogado() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private async Task<int?> ObterProfessorLogadoAsync()
        {
            var userId = ObterJogadorIdLogado();
            var jogador = await _context.Jogadores.FindAsync(userId);
            return jogador != null && jogador.IsProfessor ? userId : null;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var jogos = await _context.JogosAula
                .Include(j => j.Professor)
                .Include(j => j.LocalAula)
                .Include(j => j.CategoriaPadrao)
                .Where(j => j.Status == "Ativo" && j.DataHora >= DateTime.Now)
                .OrderBy(j => j.DataHora)
                .ToListAsync();

            return View(jogos);
        }

        [HttpGet]
        public async Task<IActionResult> Detalhes(int id)
        {
            var meuId = ObterJogadorIdLogado();
            var jogo = await _context.JogosAula
                .Include(j => j.Professor)
                .Include(j => j.LocalAula)
                .Include(j => j.CategoriaPadrao)
                .FirstOrDefaultAsync(j => j.Id == id);
            if (jogo == null) return NotFound();

            ViewBag.Inscricoes = await _context.InscricoesJogoAula
                .Include(i => i.Jogador)
                .Where(i => i.JogoAulaId == id)
                .OrderBy(i => i.InscritoEm)
                .ToListAsync();
            ViewBag.MinhaInscricao = await _context.InscricoesJogoAula
                .FirstOrDefaultAsync(i => i.JogoAulaId == id && i.JogadorId == meuId);
            ViewBag.SouOProfessor = jogo.ProfessorId == meuId;

            // Valor final anunciado: o aluno precisa ver na tela o mesmo que será cobrado no
            // checkout, e não descobrir a taxa só depois de clicar em inscrever.
            //
            // Professor com condições de assinante: a taxa muda com a forma que o aluno
            // declarar (Pix/boleto mais barato que cartão) — a tela pergunta e mostra o
            // total de cada opção. Avulso: taxa única, forma aberta, nada a perguntar.
            bool condAssinante = PlanoDoProfessor.CondicoesDeAssinante(jogo.Professor, DateTime.Now, _plano);
            bool cobraOnline = _pagamentos.PodeCobrarAula(jogo, jogo.Professor);
            ViewBag.EscolheFormaAula = condAssinante && cobraOnline;

            var cobrancaPadrao = PlanoDoProfessor.CobrancaDaAula(
                jogo.Professor, CobrancaDoTorneio.EscolhaPix, DateTime.Now, _plano);
            var exibicao = _pagamentos.CalcularExibicao(jogo.Preco ?? 0m, "Aula", jogo.Professor,
                percentual: condAssinante ? cobrancaPadrao.Percentual : null);
            ViewBag.PrecoTotal = exibicao?.Total;
            ViewBag.TaxaServico = exibicao?.Taxa;

            if (condAssinante)
            {
                var cobrancaCartao = PlanoDoProfessor.CobrancaDaAula(
                    jogo.Professor, CobrancaDoTorneio.EscolhaCartao, DateTime.Now, _plano);
                ViewBag.PrecoTotalCartao = _pagamentos.CalcularExibicao(jogo.Preco ?? 0m, "Aula",
                    jogo.Professor, percentual: cobrancaCartao.Percentual)?.Total;
            }

            return View(jogo);
        }

        [HttpGet]
        public async Task<IActionResult> Publicar()
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            ViewBag.MeusLocais = await _context.LocaisAula.Where(l => l.ProfessorId == professorId && l.Ativo).ToListAsync();
            ViewBag.CatalogoCategorias = await _context.CategoriasPadrao.Ativas().OrderBy(c => c.Id).ToListAsync();

            // Diferente do torneio, aqui NÃO se bloqueia: combinar o pagamento na quadra é o
            // jeito normal de dar aula, não um erro. O que faltava era dizer qual dos dois vai
            // acontecer — o professor punha preço e supunha que o app cobraria.
            ViewBag.RecebimentoConectado = _pagamentos.PodeReceberOnline(
                await _context.Jogadores.FindAsync(professorId.Value));

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Publicar(int localAulaId, int categoriaPadraoId, string modalidade,
            DateTime dataHora, decimal? preco, string? observacoes, int? limiteVagas)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var local = await _context.LocaisAula.FirstOrDefaultAsync(l => l.Id == localAulaId && l.ProfessorId == professorId);
            if (local == null) return Forbid();

            var jogoAula = new JogoAula
            {
                ProfessorId = professorId.Value,
                LocalAulaId = localAulaId,
                CategoriaPadraoId = categoriaPadraoId,
                Modalidade = modalidade,
                DataHora = dataHora,
                Preco = preco,
                Observacoes = observacoes,
                LimiteVagas = limiteVagas,
                Status = "Ativo"
            };
            _context.JogosAula.Add(jogoAula);
            await _context.SaveChangesAsync();

            var jogoCompleto = await _context.JogosAula
                .Include(j => j.Professor)
                .Include(j => j.LocalAula)
                .Include(j => j.CategoriaPadrao)
                .FirstAsync(j => j.Id == jogoAula.Id);

            var elegiveis = await ObterJogadoresElegiveisAsync(jogoCompleto);
            var titulo = $"Jogo Aula: {jogoCompleto.CategoriaPadrao.Nome}";
            var corpo = $"Com Prof. {jogoCompleto.Professor.Nome} em {jogoCompleto.LocalAula.Nome}, {jogoCompleto.DataHora:dd/MM 'às' HH:mm}.";
            var url = Url.Action("Detalhes", "JogoAula", new { id = jogoAula.Id });

            foreach (var jogador in elegiveis.Where(j => j.NotificarEmail && !string.IsNullOrWhiteSpace(j.Email)))
            {
                try
                {
                    await _emailService.EnviarAsync(jogador.Email!, jogador.Nome,
                        "Novo Jogo Aula - Padelizou",
                        $@"<p>Olá {jogador.Nome},</p>
                           <p>O Prof. <strong>{jogoCompleto.Professor.Nome}</strong> publicou um jogo aula de
                           <strong>{jogoCompleto.CategoriaPadrao.Nome}</strong> ({jogoCompleto.Modalidade}) em
                           <strong>{jogoCompleto.LocalAula.Nome}</strong> no dia
                           <strong>{jogoCompleto.DataHora:dd/MM/yyyy 'às' HH:mm}</strong>.</p>
                           {(string.IsNullOrWhiteSpace(jogoCompleto.Observacoes) ? "" : $"<p>{jogoCompleto.Observacoes}</p>")}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao enviar e-mail de jogo aula {JogoAulaId} para jogador {JogadorId}", jogoCompleto.Id, jogador.Id);
                }
            }

            foreach (var jogador in elegiveis)
            {
                try
                {
                    await _pushService.EnviarParaJogadorAsync(jogador.Id, titulo, corpo, url);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao enviar push de jogo aula {JogoAulaId} para jogador {JogadorId}", jogoCompleto.Id, jogador.Id);
                }
            }

            // Aula com preço e sem conta conectada: o aluno se inscreve e NADA é cobrado
            // (PodeCobrarAula recusa). O professor precisa sair desta tela sabendo disso —
            // até aqui ele descobria na hora de cobrar o aluno que já tinha jogado.
            TempData["Sucesso"] = preco > 0 && !_pagamentos.PodeCobrarAula(jogoAula, jogoCompleto.Professor)
                ? "Jogo aula publicado! O pagamento você combina direto com o aluno — " +
                  "pra receber pelo app, conecte sua conta em Perfil › Meus Pagamentos."
                : "Jogo aula publicado!";
            return RedirectToAction("Detalhes", new { id = jogoAula.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Inscrever(int id, string? formaPagamentoEscolhida = null)
        {
            var meuId = ObterJogadorIdLogado();
            var jogo = await _context.JogosAula.FindAsync(id);
            if (jogo == null || jogo.Status != "Ativo") return NotFound();

            var jaInscrito = await _context.InscricoesJogoAula
                .AnyAsync(i => i.JogoAulaId == id && i.JogadorId == meuId);
            if (!jaInscrito)
            {
                // Aula paga com recebimento ativado? A inscrição ainda NÃO é criada: o aluno
                // vai pro checkout e ela nasce quando o webhook confirmar o pagamento
                // (PagamentoInscricaoService.EfetivarAsync).
                var professor = await _context.Jogadores.FindAsync(jogo.ProfessorId);
                var aluno = await _context.Jogadores.FindAsync(meuId);
                if (aluno != null && _pagamentos.PodeCobrarAula(jogo, professor))
                {
                    var checkout = await _pagamentos.IniciarCobrancaAulaAsync(
                        jogo, professor!, aluno, new DadosInscricaoAula(id, meuId), formaPagamentoEscolhida);

                    if (checkout != null) return Redirect(checkout);

                    TempData["Erro"] = "Não foi possível gerar a cobrança agora. Tente novamente em instantes.";
                    return RedirectToAction("Detalhes", new { id });
                }

                bool cheio = false;
                if (jogo.LimiteVagas.HasValue)
                {
                    int confirmados = await _context.InscricoesJogoAula
                        .CountAsync(i => i.JogoAulaId == id && !i.EmListaDeEspera);
                    cheio = confirmados >= jogo.LimiteVagas.Value;
                }

                _context.InscricoesJogoAula.Add(new InscricaoJogoAula
                {
                    JogoAulaId = id,
                    JogadorId = meuId,
                    EmListaDeEspera = cheio
                });
                await _context.SaveChangesAsync();

                TempData["Sucesso"] = cheio
                    ? "Vagas esgotadas — você entrou na lista de espera."
                    : "Inscrição confirmada!";
            }

            return RedirectToAction("Detalhes", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarInscricao(int id)
        {
            var meuId = ObterJogadorIdLogado();
            var inscricao = await _context.InscricoesJogoAula
                .FirstOrDefaultAsync(i => i.JogoAulaId == id && i.JogadorId == meuId);
            if (inscricao == null) return RedirectToAction("Detalhes", new { id });

            bool eraConfirmada = !inscricao.EmListaDeEspera;
            _context.InscricoesJogoAula.Remove(inscricao);
            await _context.SaveChangesAsync();

            if (eraConfirmada)
            {
                var proximaDaFila = await _context.InscricoesJogoAula
                    .Where(i => i.JogoAulaId == id && i.EmListaDeEspera)
                    .OrderBy(i => i.InscritoEm)
                    .FirstOrDefaultAsync();
                if (proximaDaFila != null)
                {
                    proximaDaFila.EmListaDeEspera = false;
                    await _context.SaveChangesAsync();
                }
            }

            TempData["Sucesso"] = "Inscrição cancelada.";
            return RedirectToAction("Detalhes", new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancelar(int id)
        {
            var meuId = ObterJogadorIdLogado();
            var jogo = await _context.JogosAula.FindAsync(id);
            if (jogo == null || jogo.ProfessorId != meuId) return Forbid();

            jogo.Status = "Cancelado";
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // Elegibilidade: exclui o professor, exige NotificarJogoAula, filtro só por categoria
        // (LocalAula não é a mesma coisa que Clube, não dá pra reaproveitar JogadorClube aqui).
        private async Task<List<Jogador>> ObterJogadoresElegiveisAsync(JogoAula jogo)
        {
            return await _context.Jogadores
                .Where(j => j.Id != jogo.ProfessorId && j.NotificarJogoAula)
                .Where(j => !_context.JogadorCategorias.Any(c => c.JogadorId == j.Id)
                         || _context.JogadorCategorias.Any(c => c.JogadorId == j.Id && c.CategoriaPadraoId == jogo.CategoriaPadraoId))
                .ToListAsync();
        }
    }
}
