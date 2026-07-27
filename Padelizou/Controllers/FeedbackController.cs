using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Controllers
{
    // Canal de opinião sobre o site. Escrever exige login; ler o que os outros escreveram só
    // acontece depois que um admin aprova, um a um.
    public class FeedbackController : Controller
    {
        private readonly DbPadelContext _context;

        public FeedbackController(DbPadelContext context)
        {
            _context = context;
        }

        private int? JogadorLogadoId() =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Index()
        {
            var jogadorId = JogadorLogadoId();
            ViewBag.JaEnviados = jogadorId == null
                ? 0
                : await _context.FeedbacksSite.CountAsync(f => f.JogadorId == jogadorId);

            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enviar(int? nota, string? texto)
        {
            var jogadorId = JogadorLogadoId();
            if (jogadorId == null) return Challenge();

            var problema = RegrasDeFeedback.ProblemaCom(nota, texto);
            if (problema != null)
            {
                ViewBag.Erro = problema;
                ViewBag.Texto = texto;
                ViewBag.Nota = nota;
                return View("Index");
            }

            _context.FeedbacksSite.Add(new FeedbackSite
            {
                JogadorId = jogadorId.Value,
                Nota = nota,
                Texto = texto!.Trim()
            });
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Recebido! Obrigado — é assim que o Padelizou melhora.";
            return RedirectToAction("Index");
        }
    }
}
