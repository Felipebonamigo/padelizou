using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using System.Security.Claims;

namespace padelizou.Controllers
{
    // Painel do administrador: hoje só gerencia donos de clube e a lista de administradores do
    // sistema — fundação pra futuras telas administrativas reaproveitarem o mesmo gate.
    [Authorize]
    public class AdminController : Controller
    {
        private readonly DbPadelContext _context;

        public AdminController(DbPadelContext context)
        {
            _context = context;
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
    }
}
