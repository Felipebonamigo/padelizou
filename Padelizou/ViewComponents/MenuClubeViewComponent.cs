using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using System.Security.Claims;

namespace Padelizou.ViewComponents;

// O item "Meu clube" do menu.
//
// Por que uma consulta e não uma claim, como IsProfessor: a claim é carimbada no login, e
// quem acabou de ser nomeado dono em /Admin/Clubes só veria o menu depois de sair e entrar
// de novo — o tipo de coisa que vira chamado ("nomeei e não apareceu"). São duas consultas
// por chave primária/índice, e só pra quem está logado.
public class MenuClubeViewComponent : ViewComponent
{
    private readonly DbPadelContext _context;

    public MenuClubeViewComponent(DbPadelContext context) => _context = context;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var claim = (User as ClaimsPrincipal)?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(claim, out var jogadorId)) return View(new List<Clube>());

        var idsDono = await _context.Clubes
            .Where(c => c.DonoId == jogadorId)
            .Select(c => c.Id)
            .ToListAsync();

        var idsAdmin = await _context.ClubeAdministradores
            .Where(a => a.JogadorId == jogadorId)
            .Select(a => a.ClubeId)
            .ToListAsync();

        var ids = idsDono.Concat(idsAdmin).Distinct().ToList();
        if (ids.Count == 0) return View(new List<Clube>());

        var clubes = await _context.Clubes
            .Where(c => ids.Contains(c.Id))
            .OrderBy(c => c.Nome)
            .ToListAsync();

        return View(clubes);
    }
}
