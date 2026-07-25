using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Controllers;

// Vitrine dos times. O time já existia como entidade (o jogador escolhe a "bandeira" no
// perfil e ela aparece no ranking), mas não havia tela pra ver quem está em cada um.
public class TimesController : Controller
{
    private readonly DbPadelContext _context;
    private readonly IEstatisticasService _estatisticas;

    public TimesController(DbPadelContext context, IEstatisticasService estatisticas)
    {
        _context = context;
        _estatisticas = estatisticas;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q)
    {
        var times = await _context.Times
            .Include(t => t.Clube)
            .Select(t => new TimeResumoVM
            {
                Id = t.Id,
                Nome = t.Nome,
                Logo = t.Logo,
                Clube = t.Clube != null ? t.Clube.Nome : null,
                Membros = _context.Jogadores.Count(j => j.TimeId == t.Id),
            })
            .ToListAsync();

        // Pontos do time = soma dos pontos reais dos membros (mesma regra do ranking).
        var pontosPorTime = (await _estatisticas.ObterRankingTimesAsync())
            .ToDictionary(r => r.TimeId, r => r.Pontos);

        foreach (var t in times)
        {
            t.Pontos = pontosPorTime.GetValueOrDefault(t.Id);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            times = times.Where(t => t.Nome.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        ViewBag.Query = q;

        return View(times
            .OrderByDescending(t => t.Membros)
            .ThenBy(t => t.Nome)
            .ToList());
    }

    [HttpGet]
    public async Task<IActionResult> Detalhes(int id)
    {
        var time = await _context.Times
            .Include(t => t.Clube)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (time == null) return NotFound();

        var membros = await _context.Jogadores
            .Where(j => j.TimeId == id)
            .OrderBy(j => j.Nome)
            .ToListAsync();

        var pontos = await _estatisticas.ObterPontosPorJogadorAsync(membros.Select(j => j.Id));

        var vm = new TimeDetalheVM
        {
            Time = time,
            Membros = membros
                .Select(j => new MembroTimeVM
                {
                    Jogador = j,
                    Pontos = pontos.GetValueOrDefault(j.Id),
                    EhDono = time.DonoId == j.Id,
                })
                .OrderByDescending(m => m.EhDono)   // o dono aparece primeiro
                .ThenByDescending(m => m.Pontos)
                .ThenBy(m => m.Jogador.Nome)
                .ToList(),
        };

        return View(vm);
    }
}
