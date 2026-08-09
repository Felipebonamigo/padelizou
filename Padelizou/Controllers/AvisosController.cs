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
        // Pela FilaDeAvisos, não por SMTP direto: publicar um aviso mandava um e-mail POR
        // ELEGÍVEL dentro da requisição — a mesma lentidão do "Publicando o torneio…" de
        // 07/08/2026. A fila cobre e-mail (respeitando NotificarEmail) e ainda alcança o
        // push de quem instalou o app, que aqui ficava sem aviso nenhum.
        private readonly IPushNotificationService _pushService;
        private readonly ILogger<AvisosController> _logger;

        public AvisosController(DbPadelContext context, IPushNotificationService pushService, ILogger<AvisosController> logger)
        {
            _context = context;
            _pushService = pushService;
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
            ViewBag.CatalogoCategorias = await _context.CategoriasPadrao.Ativas().OrderBy(c => c.Id).ToListAsync();
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

            // Só ENFILEIRA — a fila decide o canal de cada um (e-mail pra quem tem
            // NotificarEmail, push pra quem instalou o app) e entrega por fora da requisição.
            var corpo = $"{avisoCompleto.Criador.ComoChamar} procura jogadores para "
                      + $"{avisoCompleto.CategoriaPadrao.Nome} em {avisoCompleto.Clube.Nome}, "
                      + $"dia {avisoCompleto.DataHora:dd/MM 'às' HH:mm}."
                      + (string.IsNullOrWhiteSpace(avisoCompleto.Observacoes) ? "" : $" {avisoCompleto.Observacoes}");

            var urlAvisos = Url.Action("Index", "Avisos");
            foreach (var jogador in elegiveis)
            {
                // ⚠️ SEM E-MAIL desde 09/08/2026: é divulgação, rajada proporcional ao número
                // de elegíveis, e ninguém está esperando por ela. Push e caixa de entrada ficam.
                await _pushService.EnviarParaJogadorAsync(jogador.Id, "Novo jogo disponível", corpo, urlAvisos,
                    AlcanceDoAviso.AppSemEmail);
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
                .Where(j => j.Id != aviso.CriadorId && (j.NotificarEmail || j.NotificarWhatsApp) && j.NotificarAvisoJogo)
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
