using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;

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

        var administradores = await _context.TimeAdministradores
            .Include(a => a.Jogador)
            .Where(a => a.TimeId == id)
            .ToListAsync();

        var idsAdmin = administradores.Select(a => a.JogadorId).ToHashSet();

        // Nome de quem concedeu, numa consulta só em vez de uma por linha.
        var concedentes = await _context.Jogadores
            .Where(j => administradores.Select(a => a.ConcedidoPorId).Contains(j.Id))
            .ToDictionaryAsync(j => j.Id, j => j.Nome);

        var meuId = ObterJogadorIdLogado();

        var vm = new TimeDetalheVM
        {
            Time = time,
            Membros = membros
                .Select(j => new MembroTimeVM
                {
                    Jogador = j,
                    Pontos = pontos.GetValueOrDefault(j.Id),
                    EhAdministrador = idsAdmin.Contains(j.Id),
                })
                .OrderByDescending(m => m.EhAdministrador)   // quem administra aparece primeiro
                .ThenByDescending(m => m.Pontos)
                .ThenBy(m => m.Jogador.Nome)
                .ToList(),
            Administradores = administradores
                .Select(a => new AdministradorTimeVM
                {
                    Jogador = a.Jogador,
                    ConcedidoEm = a.ConcedidoEm,
                    ConcedidoPor = a.ConcedidoPorId != null
                        ? concedentes.GetValueOrDefault(a.ConcedidoPorId.Value)
                        : null,
                })
                .OrderBy(a => a.ConcedidoEm)
                .ToList(),
            SouAdminDoSistema = await SouAdminDoSistemaAsync(meuId),
        };

        vm.PossoGerenciar = AdministracaoTime.PodeGerenciar(
            vm.SouAdminDoSistema, meuId != null && idsAdmin.Contains(meuId.Value));

        // Candidatos a administrador: quem já veste a camisa vem primeiro (é o caso comum),
        // mas a lista é geral porque um time recém-importado não tem membro nenhum — sem
        // isso, designar o primeiro administrador seria impossível pela tela.
        ViewBag.JogadoresParaAdmin = vm.PossoGerenciar
            ? await _context.Jogadores
                .Where(j => !idsAdmin.Contains(j.Id))
                .OrderByDescending(j => j.TimeId == id)
                .ThenBy(j => j.Nome)
                .ToListAsync()
            : new List<Jogador>();

        return View(vm);
    }

    // ── Administradores do time ───────────────────────────────────────────────────────
    // Regra: um time tem VÁRIOS administradores. O primeiro de cada time só entra pela mão
    // de um admin do Padelizou (os 44 times importados do ranking nasceram sem nenhum);
    // daí em diante, um administrador do time inclui o próximo.

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IncluirAdministrador(int timeId, int jogadorId)
    {
        var meuId = ObterJogadorIdLogado();
        if (meuId == null) return Forbid();

        var time = await _context.Times.FindAsync(timeId);
        if (time == null) return NotFound();

        var novo = await _context.Jogadores.FindAsync(jogadorId);
        if (novo == null)
        {
            TempData["Erro"] = "Jogador não encontrado.";
            return RedirectToAction("Detalhes", new { id = timeId });
        }

        var problema = AdministracaoTime.ProblemaParaConceder(
            quemPedeEhAdminDoSistema: await SouAdminDoSistemaAsync(meuId),
            quemPedeEhAdminDoTime: await AdministracaoTime.EhAdministradorAsync(_context, timeId, meuId.Value),
            alvoJaEAdmin: await AdministracaoTime.EhAdministradorAsync(_context, timeId, jogadorId));

        if (problema != null)
        {
            TempData["Erro"] = problema;
            return RedirectToAction("Detalhes", new { id = timeId });
        }

        _context.TimeAdministradores.Add(new TimeAdministrador
        {
            TimeId = timeId,
            JogadorId = jogadorId,
            ConcedidoPorId = meuId,
            ConcedidoEm = DateTime.Now,
        });

        // Quem administra o time veste a camisa dele. Sem isto o administrador ficaria de
        // fora da própria lista de membros, e a tela diria "não tem ninguém" com ele lá.
        if (novo.TimeId != timeId) novo.TimeId = timeId;

        await _context.SaveChangesAsync();

        TempData["Sucesso"] = $"{novo.ComoChamar} agora administra o {time.Nome}.";
        return RedirectToAction("Detalhes", new { id = timeId });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoverAdministrador(int timeId, int jogadorId)
    {
        var meuId = ObterJogadorIdLogado();
        if (meuId == null) return Forbid();

        var quantos = await _context.TimeAdministradores.CountAsync(a => a.TimeId == timeId);

        var problema = AdministracaoTime.ProblemaParaRemover(
            quemPedeEhAdminDoSistema: await SouAdminDoSistemaAsync(meuId),
            quemPedeEhAdminDoTime: await AdministracaoTime.EhAdministradorAsync(_context, timeId, meuId.Value),
            alvoEAdmin: await AdministracaoTime.EhAdministradorAsync(_context, timeId, jogadorId),
            quantosAdministradores: quantos);

        if (problema != null)
        {
            TempData["Erro"] = problema;
            return RedirectToAction("Detalhes", new { id = timeId });
        }

        var linha = await _context.TimeAdministradores
            .FirstAsync(a => a.TimeId == timeId && a.JogadorId == jogadorId);
        _context.TimeAdministradores.Remove(linha);
        await _context.SaveChangesAsync();

        // Sai da administração, mas continua no time: perder o cargo não é ser expulso.
        TempData["Sucesso"] = "Administrador removido.";
        return RedirectToAction("Detalhes", new { id = timeId });
    }

    private int? ObterJogadorIdLogado()
    {
        var valor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(valor, out var id) ? id : null;
    }

    private async Task<bool> SouAdminDoSistemaAsync(int? jogadorId)
    {
        if (jogadorId == null) return false;
        return await _context.Jogadores
            .AnyAsync(j => j.Id == jogadorId && (j.IsAdminGeral || j.IsAdminRaiz));
    }
}
