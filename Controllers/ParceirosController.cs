using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace padelizou.Controllers
{
    [Authorize]
    public class ParceirosController : Controller
    {
        private readonly DbPadelContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<ParceirosController> _logger;

        public ParceirosController(DbPadelContext context, IEmailService emailService, ILogger<ParceirosController> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var avisos = await _context.AvisosParceiro
                .Include(a => a.Criador)
                .Where(a => a.Status == "Ativo" && a.DataHora >= DateTime.Now)
                .OrderBy(a => a.DataHora)
                .ToListAsync();

            return View(avisos);
        }

        [HttpGet]
        public IActionResult Criar()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Criar(string local, DateTime dataHora, string nomeTorneio, string? observacoes)
        {
            var criadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var aviso = new AvisoParceiro
            {
                CriadorId = criadorId,
                Local = local,
                DataHora = dataHora,
                NomeTorneio = nomeTorneio,
                Observacoes = observacoes,
                Status = "Ativo"
            };
            _context.AvisosParceiro.Add(aviso);
            await _context.SaveChangesAsync();

            return RedirectToAction("Detalhes", new { id = aviso.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Detalhes(int id)
        {
            var aviso = await _context.AvisosParceiro
                .Include(a => a.Criador)
                .Include(a => a.Candidaturas).ThenInclude(c => c.Candidato)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (aviso == null) return NotFound();

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            ViewBag.EhCriador = aviso.CriadorId == userId;
            ViewBag.JaCandidatou = aviso.Candidaturas.Any(c => c.CandidatoId == userId);

            return View(aviso);
        }

        [HttpPost]
        public async Task<IActionResult> Candidatar(int id)
        {
            var candidatoId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var aviso = await _context.AvisosParceiro
                .Include(a => a.Criador)
                .FirstOrDefaultAsync(a => a.Id == id && a.Status == "Ativo");

            if (aviso == null || aviso.CriadorId == candidatoId)
            {
                return RedirectToAction("Index");
            }

            var jaCandidatou = await _context.CandidaturasParceiro
                .AnyAsync(c => c.AvisoParceiroId == id && c.CandidatoId == candidatoId);

            if (!jaCandidatou)
            {
                _context.CandidaturasParceiro.Add(new CandidaturaParceiro { AvisoParceiroId = id, CandidatoId = candidatoId });
                await _context.SaveChangesAsync();

                var candidato = await _context.Jogadores.FindAsync(candidatoId);
                try
                {
                    await _emailService.EnviarAsync(aviso.Criador.Email!, aviso.Criador.Nome,
                        "Alguém quer ser seu parceiro de torneio! - Padelizou",
                        $@"<p>Olá {aviso.Criador.Nome},</p>
                           <p><strong>{candidato!.Nome}</strong> se candidatou para ser seu parceiro no torneio
                           <strong>{aviso.NomeTorneio}</strong> em <strong>{aviso.Local}</strong>
                           no dia <strong>{aviso.DataHora:dd/MM/yyyy 'às' HH:mm}</strong>.</p>
                           <p>Acesse o Padelizou para ver os candidatos.</p>");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao enviar e-mail de candidatura para o aviso {AvisoId}", id);
                }
            }

            return RedirectToAction("Detalhes", new { id });
        }

        [HttpPost]
        public async Task<IActionResult> Aceitar(int candidaturaId)
        {
            var criadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var candidatura = await _context.CandidaturasParceiro
                .Include(c => c.AvisoParceiro)
                .Include(c => c.Candidato)
                .FirstOrDefaultAsync(c => c.Id == candidaturaId);

            if (candidatura == null || candidatura.AvisoParceiro.CriadorId != criadorId)
            {
                return RedirectToAction("Index");
            }

            var outrasCandidaturas = await _context.CandidaturasParceiro
                .Where(c => c.AvisoParceiroId == candidatura.AvisoParceiroId && c.Id != candidaturaId && c.Status == "Pendente")
                .ToListAsync();

            candidatura.Status = "Aceito";
            candidatura.AvisoParceiro.Status = "Preenchida";
            foreach (var outra in outrasCandidaturas)
            {
                outra.Status = "Recusado";
            }
            await _context.SaveChangesAsync();

            try
            {
                await _emailService.EnviarAsync(candidatura.Candidato.Email!, candidatura.Candidato.Nome,
                    "Você foi aceito como parceiro de torneio! - Padelizou",
                    $@"<p>Olá {candidatura.Candidato.Nome},</p>
                       <p><strong>{candidatura.AvisoParceiro.Criador.Nome}</strong> aceitou você como parceiro no
                       torneio <strong>{candidatura.AvisoParceiro.NomeTorneio}</strong> em
                       <strong>{candidatura.AvisoParceiro.Local}</strong> no dia
                       <strong>{candidatura.AvisoParceiro.DataHora:dd/MM/yyyy 'às' HH:mm}</strong>.</p>");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao enviar e-mail de aceite da candidatura {CandidaturaId}", candidaturaId);
            }

            var mensagem = $"Oi {candidatura.Candidato.Nome}! Bora ser dupla no {candidatura.AvisoParceiro.NomeTorneio} " +
                          $"em {candidatura.AvisoParceiro.Local} dia {candidatura.AvisoParceiro.DataHora:dd/MM 'às' HH:mm}?";
            TempData["WhatsAppLink"] = WhatsAppLinkHelper.GerarLink(candidatura.Candidato.Celular, mensagem);
            TempData["Sucesso"] = $"{candidatura.Candidato.Nome} foi aceito(a) como seu parceiro!";

            return RedirectToAction("Detalhes", new { id = candidatura.AvisoParceiroId });
        }

        [HttpPost]
        public async Task<IActionResult> Recusar(int candidaturaId)
        {
            var criadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var candidatura = await _context.CandidaturasParceiro
                .Include(c => c.AvisoParceiro)
                .FirstOrDefaultAsync(c => c.Id == candidaturaId);

            if (candidatura != null && candidatura.AvisoParceiro.CriadorId == criadorId && candidatura.Status == "Pendente")
            {
                candidatura.Status = "Recusado";
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Detalhes", new { id = candidatura?.AvisoParceiroId });
        }

        [HttpPost]
        public async Task<IActionResult> Cancelar(int id)
        {
            var criadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var aviso = await _context.AvisosParceiro.FirstOrDefaultAsync(a => a.Id == id && a.CriadorId == criadorId);

            if (aviso != null)
            {
                aviso.Status = "Cancelado";
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }
    }
}
