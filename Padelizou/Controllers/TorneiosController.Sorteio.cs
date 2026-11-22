using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Controllers
{
    // O SORTEIO DE BRINDES (21/11/2026) — só o HTTP; a regra mora em Services/SorteioDeBrindes.
    //
    // 🔒 A régua de sempre pra gerir o torneio (EhOrganizadorAsync), sem papel novo: sortear é do
    // organizador, como o resto da configuração. A página do torneio e o telão MOSTRAM o resultado
    // a todos — é por isso que ele é gravado.
    public partial class TorneiosController
    {
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Sorteio(int id, int? animar = null)
        {
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            var participantes = await SorteioDeBrindes.Participantes(_context, id).ToListAsync();
            ViewBag.Torneio = torneio;
            ViewBag.NomesNaRoleta = await _context.Jogadores.Where(j => participantes.Contains(j.Id))
                .Select(j => j.Nome).ToListAsync();
            ViewBag.Animar = animar;
            return View(await SorteiosDoTorneioAsync(id));
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Sortear(int id, int quantidade, string? premio, bool quemJaGanhouConcorre, int? substituir)
        {
            var quem = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(id, quem)) return Forbid();

            var r = await new SorteioDeBrindes(_context, _pushService)
                .SortearAsync(id, quantidade, premio, quemJaGanhouConcorre, substituir, quem, DateTime.Now);
            if (r.Sorteio == null)
            {
                TempData["Erro"] = r.Erro;
                return RedirectToAction(nameof(Sorteio), new { id });
            }
            return RedirectToAction(nameof(Sorteio), new { id, animar = r.Sorteio.Id });
        }

        // Os sorteios do torneio, o mais novo primeiro, com os nomes.
        private async Task<List<SorteioDoTorneio>> SorteiosDoTorneioAsync(int torneioId) =>
            await _context.SorteiosDoTorneio.AsNoTracking()
                .Where(s => s.TorneioId == torneioId)
                .Include(s => s.Ganhadores).ThenInclude(g => g.Jogador)
                .OrderByDescending(s => s.FeitoEm).ThenByDescending(s => s.Id)
                .ToListAsync();
    }
}
