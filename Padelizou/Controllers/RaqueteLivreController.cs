using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace padelizou.Controllers
{
    // "Raquete Livre" — sessão de rodízio publicada pelo clube (dono ou administrador):
    // hora pra começar, valor fixo por pessoa, sem dupla fixa e sem número exato de gente.
    // Tem inscrição e lista de espera no app e avisa quem marcou NotificarRaqueteLivre.
    [Authorize]
    public class RaqueteLivreController : Controller
    {
        private readonly DbPadelContext _context;
        private readonly IPushNotificationService _pushService;
        private readonly ILogger<RaqueteLivreController> _logger;

        // Sem IEmailService de propósito, mesmo motivo do DuplasController: quem manda e-mail
        // aqui é o funil de avisos. Voltar a injetar isto é o caminho pro envio em dobro.
        public RaqueteLivreController(DbPadelContext context,
            IPushNotificationService pushService, ILogger<RaqueteLivreController> logger)
        {
            _context = context;
            _pushService = pushService;
            _logger = logger;
        }

        private int ObterJogadorIdLogado() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private async Task<bool> EhDonoOuAdminDoClubeAsync(int clubeId, int jogadorId)
        {
            var clube = await _context.Clubes.FindAsync(clubeId);
            if (clube == null) return false;
            if (clube.DonoId == jogadorId) return true;

            return await _context.ClubeAdministradores
                .AnyAsync(a => a.ClubeId == clubeId && a.JogadorId == jogadorId);
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var consulta = SessaoRaqueteLivre.EmCartaz(_context.AvisosRaqueteLivre, DateTime.Now);

            var avisos = await consulta
                .Include(a => a.Clube)
                .Include(a => a.Criador)
                .OrderBy(a => a.DataHoraInicio)
                .ToListAsync();

            return View(avisos);
        }

        [HttpGet]
        public async Task<IActionResult> Detalhes(int id)
        {
            var meuId = ObterJogadorIdLogado();
            var aviso = await _context.AvisosRaqueteLivre
                .Include(a => a.Clube)
                .Include(a => a.Criador)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (aviso == null) return NotFound();

            ViewBag.Inscricoes = await _context.InscricoesRaqueteLivre
                .Include(i => i.Jogador)
                .Where(i => i.AvisoRaqueteLivreId == id)
                .OrderBy(i => i.InscritoEm)
                .ToListAsync();
            ViewBag.MinhaInscricao = await _context.InscricoesRaqueteLivre
                .FirstOrDefaultAsync(i => i.AvisoRaqueteLivreId == id && i.JogadorId == meuId);
            ViewBag.PodeGerenciar = await EhDonoOuAdminDoClubeAsync(aviso.ClubeId, meuId) || aviso.CriadorId == meuId;

            return View(aviso);
        }

        [HttpGet]
        public async Task<IActionResult> Criar(int clubeId)
        {
            var meuId = ObterJogadorIdLogado();
            if (!await EhDonoOuAdminDoClubeAsync(clubeId, meuId)) return Forbid();

            var clube = await _context.Clubes.FindAsync(clubeId);
            if (clube == null) return NotFound();

            ViewBag.Clube = clube;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Criar(int clubeId, DateTime dataHoraInicio, DateTime? dataHoraFim,
            decimal? preco, string? observacoes, int? limiteVagas)
        {
            var meuId = ObterJogadorIdLogado();
            if (!await EhDonoOuAdminDoClubeAsync(clubeId, meuId)) return Forbid();

            // Fim é opcional, mas se vier tem que fazer sentido.
            if (dataHoraFim.HasValue && dataHoraFim.Value <= dataHoraInicio)
            {
                var clubeDaTela = await _context.Clubes.FindAsync(clubeId);
                if (clubeDaTela == null) return NotFound();

                ViewBag.Clube = clubeDaTela;
                ViewBag.Erro = "O fim tem que ser depois do início. Se não tem hora pra acabar, deixe o campo vazio.";
                return View();
            }

            var aviso = new AvisoRaqueteLivre
            {
                ClubeId = clubeId,
                CriadorId = meuId,
                DataHoraInicio = dataHoraInicio,
                DataHoraFim = dataHoraFim,
                Preco = preco,
                Observacoes = observacoes,
                LimiteVagas = limiteVagas,
                Status = "Ativo"
            };
            _context.AvisosRaqueteLivre.Add(aviso);
            await _context.SaveChangesAsync();

            var avisoCompleto = await _context.AvisosRaqueteLivre
                .Include(a => a.Clube)
                .Include(a => a.Criador)
                .FirstAsync(a => a.Id == aviso.Id);

            var elegiveis = await ObterJogadoresElegiveisAsync(avisoCompleto);
            var titulo = $"Raquete Livre em {avisoCompleto.Clube.Nome}";
            var corpo = $"{avisoCompleto.DataHoraInicio:dd/MM}, {SessaoRaqueteLivre.DescreverHorario(avisoCompleto)}.";
            var url = Url.Action("Detalhes", "RaqueteLivre", new { id = aviso.Id });

            // ⚠️ SEM E-MAIL desde 09/08/2026, e aqui saíam DOIS por pessoa: este laço inline
            // mais o do funil de avisos, que já cobre o canal — mesma sobra do jogo-aula e do
            // DuplasController. Divulgação é rajada proporcional ao grupo, e ninguém está
            // esperando por ela. Push e caixa de entrada continuam levando o recado.
            foreach (var jogador in elegiveis)
            {
                try
                {
                    await _pushService.EnviarParaJogadorAsync(jogador.Id, titulo, corpo, url,
                        AlcanceDoAviso.AppSemEmail);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao enviar push de raquete livre {AvisoId} para jogador {JogadorId}", avisoCompleto.Id, jogador.Id);
                }
            }

            TempData["Sucesso"] = "Raquete Livre publicado!";
            return RedirectToAction("Detalhes", new { id = aviso.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Inscrever(int id)
        {
            var meuId = ObterJogadorIdLogado();
            var aviso = await _context.AvisosRaqueteLivre.FindAsync(id);
            if (aviso == null || aviso.Status != "Ativo") return NotFound();

            var jaInscrito = await _context.InscricoesRaqueteLivre
                .AnyAsync(i => i.AvisoRaqueteLivreId == id && i.JogadorId == meuId);
            if (!jaInscrito)
            {
                bool cheio = false;
                if (aviso.LimiteVagas.HasValue)
                {
                    int confirmados = await _context.InscricoesRaqueteLivre
                        .CountAsync(i => i.AvisoRaqueteLivreId == id && !i.EmListaDeEspera);
                    cheio = confirmados >= aviso.LimiteVagas.Value;
                }

                _context.InscricoesRaqueteLivre.Add(new InscricaoRaqueteLivre
                {
                    AvisoRaqueteLivreId = id,
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
            var inscricao = await _context.InscricoesRaqueteLivre
                .FirstOrDefaultAsync(i => i.AvisoRaqueteLivreId == id && i.JogadorId == meuId);
            if (inscricao == null) return RedirectToAction("Detalhes", new { id });

            bool eraConfirmada = !inscricao.EmListaDeEspera;
            _context.InscricoesRaqueteLivre.Remove(inscricao);
            await _context.SaveChangesAsync();

            // Abriu vaga: promove quem está há mais tempo na lista de espera.
            if (eraConfirmada)
            {
                var proximaDaFila = await _context.InscricoesRaqueteLivre
                    .Where(i => i.AvisoRaqueteLivreId == id && i.EmListaDeEspera)
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
            var aviso = await _context.AvisosRaqueteLivre.FindAsync(id);
            if (aviso == null) return NotFound();
            if (aviso.CriadorId != meuId && !await EhDonoOuAdminDoClubeAsync(aviso.ClubeId, meuId)) return Forbid();

            aviso.Status = "Cancelado";
            await _context.SaveChangesAsync();

            return RedirectToAction("Index");
        }

        // Elegibilidade: exclui o criador, exige NotificarRaqueteLivre, sem restrição de
        // categoria (aberto pra todo mundo) — só Clube e Dia/Horário, mesmo padrão de Avisos.
        private async Task<List<Jogador>> ObterJogadoresElegiveisAsync(AvisoRaqueteLivre aviso)
        {
            var periodo = ObterPeriodo(aviso.DataHoraInicio);
            var diaSemana = (int)aviso.DataHoraInicio.DayOfWeek;

            return await _context.Jogadores
                .Where(j => j.Id != aviso.CriadorId && j.NotificarRaqueteLivre)
                .Where(j => !_context.JogadorClubes.Any(c => c.JogadorId == j.Id)
                         || _context.JogadorClubes.Any(c => c.JogadorId == j.Id && c.ClubeId == aviso.ClubeId))
                .Where(j => !_context.JogadorDiasHorarios.Any(d => d.JogadorId == j.Id)
                         || _context.JogadorDiasHorarios.Any(d => d.JogadorId == j.Id && d.DiaSemana == diaSemana && d.Periodo == periodo))
                .ToListAsync();
        }

        private static string ObterPeriodo(DateTime dataHora)
        {
            if (dataHora.Hour < 12) return "Manhã";
            if (dataHora.Hour < 18) return "Tarde";
            return "Noite";
        }
    }
}
