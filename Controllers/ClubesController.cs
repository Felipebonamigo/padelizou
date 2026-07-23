using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using System.Security.Claims;

namespace padelizou.Controllers
{
    // Além do "criar clube inline" original, também é onde o dono/administrador de um clube
    // gerencia seus próprios administradores (atribuir dono é no AdminController, que é
    // exclusivo de quem já é administrador do sistema).
    [Authorize]
    public class ClubesController : Controller
    {
        private readonly DbPadelContext _context;

        public ClubesController(DbPadelContext context)
        {
            _context = context;
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Criar(string nome, string? endereco)
        {
            if (string.IsNullOrWhiteSpace(nome))
            {
                return BadRequest();
            }

            var clube = new Clube { Nome = nome, Endereco = endereco ?? "", Contato = "" };
            _context.Clubes.Add(clube);
            await _context.SaveChangesAsync();

            return Json(new { id = clube.Id, nome = clube.Nome });
        }

        private async Task<bool> EhDonoOuAdminDoClubeAsync(int clubeId, int jogadorId)
        {
            var clube = await _context.Clubes.FindAsync(clubeId);
            if (clube == null) return false;
            if (clube.DonoId == jogadorId) return true;

            return await _context.ClubeAdministradores
                .AnyAsync(a => a.ClubeId == clubeId && a.JogadorId == jogadorId);
        }

        private int? ObterJogadorIdLogado()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdValue, out var userId) ? userId : null;
        }

        [HttpGet]
        public async Task<IActionResult> Gerenciar(int id)
        {
            var meuId = ObterJogadorIdLogado() ?? 0;
            var clube = await _context.Clubes.Include(c => c.Dono).FirstOrDefaultAsync(c => c.Id == id);
            if (clube == null) return NotFound();
            if (!await EhDonoOuAdminDoClubeAsync(id, meuId)) return Forbid();

            ViewBag.SouDono = clube.DonoId == meuId;
            ViewBag.Administradores = await _context.ClubeAdministradores
                .Include(a => a.Jogador)
                .Where(a => a.ClubeId == id)
                .OrderBy(a => a.Jogador.Nome)
                .ToListAsync();

            return View(clube);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarAdministrador(int clubeId, int jogadorId)
        {
            var meuId = ObterJogadorIdLogado() ?? 0;
            if (!await EhDonoOuAdminDoClubeAsync(clubeId, meuId)) return Forbid();

            var jaEAdmin = await _context.ClubeAdministradores
                .AnyAsync(a => a.ClubeId == clubeId && a.JogadorId == jogadorId);
            if (!jaEAdmin)
            {
                _context.ClubeAdministradores.Add(new ClubeAdministrador { ClubeId = clubeId, JogadorId = jogadorId });
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Gerenciar", new { id = clubeId });
        }

        // Só o dono remove administradores — administradores não removem uns aos outros.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverAdministrador(int clubeId, int jogadorId)
        {
            var meuId = ObterJogadorIdLogado() ?? 0;
            var clube = await _context.Clubes.FindAsync(clubeId);
            if (clube == null || clube.DonoId != meuId) return Forbid();

            var vinculo = await _context.ClubeAdministradores
                .FirstOrDefaultAsync(a => a.ClubeId == clubeId && a.JogadorId == jogadorId);
            if (vinculo != null)
            {
                _context.ClubeAdministradores.Remove(vinculo);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Gerenciar", new { id = clubeId });
        }
    }
}
