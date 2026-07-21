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
    public class AvisosController : Controller
    {
        private readonly DbPadelContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<AvisosController> _logger;

        public AvisosController(DbPadelContext context, IEmailService emailService, ILogger<AvisosController> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var avisos = await _context.AvisosJogo
                .Include(a => a.Clube)
                .Include(a => a.CategoriaPadrao)
                .Include(a => a.Criador)
                .Where(a => a.Status == "Ativo" && a.DataHora >= DateTime.Now)
                .OrderBy(a => a.DataHora)
                .ToListAsync();

            return View(avisos);
        }

        [HttpGet]
        public async Task<IActionResult> Criar()
        {
            ViewBag.CatalogoCategorias = await _context.CategoriasPadrao.OrderBy(c => c.Id).ToListAsync();
            ViewBag.CatalogoClubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Criar(int clubeId, int categoriaPadraoId, DateTime dataHora, string? observacoes)
        {
            var criadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var aviso = new AvisoJogo
            {
                CriadorId = criadorId,
                ClubeId = clubeId,
                CategoriaPadraoId = categoriaPadraoId,
                DataHora = dataHora,
                Observacoes = observacoes,
                Status = "Ativo"
            };
            _context.AvisosJogo.Add(aviso);
            await _context.SaveChangesAsync();

            var avisoCompleto = await _context.AvisosJogo
                .Include(a => a.Clube)
                .Include(a => a.CategoriaPadrao)
                .Include(a => a.Criador)
                .FirstAsync(a => a.Id == aviso.Id);

            var elegiveis = await ObterJogadoresElegiveisAsync(avisoCompleto);

            foreach (var jogador in elegiveis.Where(j => j.NotificarEmail && !string.IsNullOrWhiteSpace(j.Email)))
            {
                try
                {
                    await _emailService.EnviarAsync(jogador.Email!, jogador.Nome,
                        "Novo jogo disponível - Padelizou",
                        $@"<p>Olá {jogador.Nome},</p>
                           <p><strong>{avisoCompleto.Criador.Nome}</strong> está procurando jogadores para
                           <strong>{avisoCompleto.CategoriaPadrao.Nome}</strong> em
                           <strong>{avisoCompleto.Clube.Nome}</strong> no dia
                           <strong>{avisoCompleto.DataHora:dd/MM/yyyy 'às' HH:mm}</strong>.</p>
                           {(string.IsNullOrWhiteSpace(avisoCompleto.Observacoes) ? "" : $"<p>{avisoCompleto.Observacoes}</p>")}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao enviar e-mail de aviso de jogo {AvisoId} para jogador {JogadorId}", avisoCompleto.Id, jogador.Id);
                }
            }

            return RedirectToAction("AvisoPublicado", new { id = aviso.Id });
        }

        [HttpGet]
        public async Task<IActionResult> AvisoPublicado(int id)
        {
            var aviso = await _context.AvisosJogo
                .Include(a => a.Clube)
                .Include(a => a.CategoriaPadrao)
                .Include(a => a.Criador)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (aviso == null) return NotFound();

            var elegiveis = await ObterJogadoresElegiveisAsync(aviso);

            ViewBag.TotalEmail = elegiveis.Count(j => j.NotificarEmail);

            var mensagem = $"Oi! Vi que você topa jogar {aviso.CategoriaPadrao.Nome} e queria te chamar pra um jogo em " +
                           $"{aviso.Clube.Nome} no dia {aviso.DataHora:dd/MM 'às' HH:mm}. Bora?";

            ViewBag.JogadoresWhatsApp = elegiveis
                .Where(j => j.NotificarWhatsApp && !string.IsNullOrWhiteSpace(j.Celular))
                .Select(j => (Nome: j.Nome, Link: WhatsAppLinkHelper.GerarLink(j.Celular, mensagem)))
                .ToList();

            return View(aviso);
        }

        [HttpPost]
        public async Task<IActionResult> Cancelar(int id)
        {
            var criadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var aviso = await _context.AvisosJogo.FirstOrDefaultAsync(a => a.Id == id && a.CriadorId == criadorId);

            if (aviso != null)
            {
                aviso.Status = "Cancelado";
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }

        // Jogadores que devem ser notificados: pelo menos um canal ativo, e sem restrição
        // (ou restrição compatível) em categoria/clube/dia-período.
        private async Task<List<Jogador>> ObterJogadoresElegiveisAsync(AvisoJogo aviso)
        {
            var periodo = ObterPeriodo(aviso.DataHora);
            var diaSemana = (int)aviso.DataHora.DayOfWeek;

            return await _context.Jogadores
                .Where(j => j.Id != aviso.CriadorId && (j.NotificarEmail || j.NotificarWhatsApp))
                .Where(j => !_context.JogadorCategorias.Any(c => c.JogadorId == j.Id)
                         || _context.JogadorCategorias.Any(c => c.JogadorId == j.Id && c.CategoriaPadraoId == aviso.CategoriaPadraoId))
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
