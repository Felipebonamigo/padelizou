using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;

namespace padelizou.Controllers
{
    // Caderno da aula: professor e aluno anotam sobre a MESMA aula no mesmo fio.
    // Quem participa e o que vale como texto moram em Services/AnotacoesDeAula.
    // O [Authorize] da classe fica no arquivo principal (AulasController.cs).
    public partial class AulasController
    {
        [Authorize]
        public async Task<IActionResult> Anotacoes(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var meuId))
                return RedirectToAction("Perfil", "Auth");

            var aula = await _context.Aulas
                .Include(a => a.Professor)
                .Include(a => a.Aluno)
                .Include(a => a.LocalAula)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (aula == null) return NotFound();
            if (!AnotacoesDeAula.PodeParticipar(aula, meuId)) return Forbid();

            ViewBag.Anotacoes = await _context.AnotacoesAula
                .Include(n => n.Autor)
                .Where(n => n.AulaId == id)
                .OrderBy(n => n.CriadoEm)
                .ToListAsync();
            ViewBag.MeuId = meuId;

            return View(aula);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AdicionarAnotacao(int aulaId, string? texto)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var meuId))
                return RedirectToAction("Perfil", "Auth");

            var aula = await _context.Aulas.FindAsync(aulaId);
            if (aula == null) return NotFound();
            if (!AnotacoesDeAula.PodeParticipar(aula, meuId)) return Forbid();

            if (!AnotacoesDeAula.TextoValido(texto))
            {
                TempData["Erro"] = $"Escreva algo (até {AnotacoesDeAula.TamanhoMaximo} caracteres).";
                return RedirectToAction(nameof(Anotacoes), new { id = aulaId });
            }

            _context.AnotacoesAula.Add(new AnotacaoAula
            {
                AulaId = aulaId,
                AutorId = meuId,
                Texto = texto!.Trim(),
            });
            await _context.SaveChangesAsync();

            // Anotação avisa o OUTRO lado da aula — professor escreve, aluno fica sabendo,
            // e vice-versa. Aula avulsa (aluno sem conta) não tem quem avisar.
            var destinatario = AnotacoesDeAula.QuemAvisar(aula, meuId);
            if (destinatario != null)
            {
                try
                {
                    var autor = await _context.Jogadores.FindAsync(meuId);
                    // ⚠️ SEM E-MAIL desde 09/08/2026: a anotação fica guardada na aula e é lida
                    // quando a pessoa abrir o caderno — não perde valor por chegar amanhã.
                    await _pushService.EnviarParaJogadorAsync(destinatario.Value,
                        "Nova anotação na aula",
                        $"{autor?.Nome ?? "Alguém"} anotou algo sobre a aula de {aula.DataHora:dd/MM}.",
                        Url.Action(nameof(Anotacoes), "Aulas", new { id = aulaId }),
                        AlcanceDoAviso.AppSemEmail);
                }
                catch (Exception ex)
                {
                    // A anotação já está salva; push é acessório.
                    _logger.LogWarning(ex, "Falha ao avisar anotação da aula {AulaId}.", aulaId);
                }
            }

            return RedirectToAction(nameof(Anotacoes), new { id = aulaId });
        }
    }
}
