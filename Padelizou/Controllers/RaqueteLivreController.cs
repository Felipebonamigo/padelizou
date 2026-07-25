using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace padelizou.Controllers
{
    // "Raquete Livre" — aviso de clube (dono ou administrador) com inscrição e lista de
    // espera dentro do app, notificando quem tem NotificarRaqueteLivre marcado.
    [Authorize]
    public class RaqueteLivreController : Controller
    {
        private readonly DbPadelContext _context;
        private readonly IEmailService _emailService;
        private readonly IPushNotificationService _pushService;
        private readonly ILogger<RaqueteLivreController> _logger;

        public RaqueteLivreController(DbPadelContext context, IEmailService emailService,
            IPushNotificationService pushService, ILogger<RaqueteLivreController> logger)
        {
            _context = context;
            _emailService = emailService;
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
            var avisos = await _context.AvisosRaqueteLivre
                .Include(a => a.Clube)
                .Include(a => a.Criador)
                .Where(a => a.Status == "Ativo" && a.DataHoraFim >= DateTime.Now)
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
        public async Task<IActionResult> Criar(int clubeId, DateTime dataHoraInicio, DateTime dataHoraFim,
            decimal? preco, string? observacoes, int? limiteVagas)
        {
            var meuId = ObterJogadorIdLogado();
            if (!await EhDonoOuAdminDoClubeAsync(clubeId, meuId)) return Forbid();

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
            var corpo = $"{avisoCompleto.DataHoraInicio:dd/MM} das {avisoCompleto.DataHoraInicio:HH:mm} às {avisoCompleto.DataHoraFim:HH:mm}.";
            var url = Url.Action("Detalhes", "RaqueteLivre", new { id = aviso.Id });

            foreach (var jogador in elegiveis.Where(j => j.NotificarEmail && !string.IsNullOrWhiteSpace(j.Email)))
            {
                try
                {
                    await _emailService.EnviarAsync(jogador.Email!, jogador.Nome,
                        "Raquete Livre - Padelizou",
                        $@"<p>Olá {jogador.Nome},</p>
                           <p><strong>{avisoCompleto.Clube.Nome}</strong> tem um raquete livre marcado pra
                           <strong>{avisoCompleto.DataHoraInicio:dd/MM/yyyy}</strong>, das
                           <strong>{avisoCompleto.DataHoraInicio:HH:mm}</strong> às
                           <strong>{avisoCompleto.DataHoraFim:HH:mm}</strong>.</p>
                           {(avisoCompleto.Preco.HasValue ? $"<p>Valor: R$ {avisoCompleto.Preco:0.00}</p>" : "")}
                           {(string.IsNullOrWhiteSpace(avisoCompleto.Observacoes) ? "" : $"<p>{avisoCompleto.Observacoes}</p>")}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao enviar e-mail de raquete livre {AvisoId} para jogador {JogadorId}", avisoCompleto.Id, jogador.Id);
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
