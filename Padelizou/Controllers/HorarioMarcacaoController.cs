using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace padelizou.Controllers
{
    // Gestão de quadras e regras de horário de um clube (dono/administrador) — a tela do
    // jogador que marca é MarcarJogoController.
    [Authorize]
    public class HorarioMarcacaoController : Controller
    {
        private readonly DbPadelContext _context;
        private readonly IHorarioMarcacaoService _horarioMarcacaoService;

        public HorarioMarcacaoController(DbPadelContext context, IHorarioMarcacaoService horarioMarcacaoService)
        {
            _context = context;
            _horarioMarcacaoService = horarioMarcacaoService;
        }

        private int ObterJogadorIdLogado() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private async Task<bool> EhDonoOuAdminDoClubeAsync(int clubeId, int jogadorId)
        {
            var clube = await _context.Clubes.FindAsync(clubeId);
            if (clube == null) return false;
            if (clube.DonoId == jogadorId) return true;

            return await _context.ClubeAdministradores
                .AnyAsync(a => a.ClubeId == clubeId && a.JogadorId == jogadorId);
        }

        [HttpGet]
        public async Task<IActionResult> Index(int clubeId)
        {
            var meuId = ObterJogadorIdLogado();
            var clube = await _context.Clubes.FindAsync(clubeId);
            if (clube == null || !clube.MarcacaoHorariosAtiva) return NotFound();
            if (!await EhDonoOuAdminDoClubeAsync(clubeId, meuId)) return Forbid();

            ViewBag.Clube = clube;
            ViewBag.Quadras = await _context.QuadrasClube
                .Where(q => q.ClubeId == clubeId)
                .OrderBy(q => q.Nome)
                .ToListAsync();
            ViewBag.Horarios = await _context.HorariosMarcacaoDisponivel
                .Include(h => h.QuadraClube)
                .Where(h => h.ClubeId == clubeId)
                .OrderBy(h => h.QuadraClube.Nome).ThenBy(h => h.DiaSemana).ThenBy(h => h.HoraInicio)
                .ToListAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CriarQuadra(int clubeId, string nome)
        {
            var meuId = ObterJogadorIdLogado();
            if (!await EhDonoOuAdminDoClubeAsync(clubeId, meuId)) return Forbid();
            if (string.IsNullOrWhiteSpace(nome)) return RedirectToAction("Index", new { clubeId });

            _context.QuadrasClube.Add(new QuadraClube { ClubeId = clubeId, Nome = nome.Trim() });
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", new { clubeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CriarHorario(int clubeId, int quadraClubeId, int diaSemana,
            TimeSpan horaInicio, TimeSpan horaFim, int duracaoMinutos, decimal? preco)
        {
            var meuId = ObterJogadorIdLogado();
            if (!await EhDonoOuAdminDoClubeAsync(clubeId, meuId)) return Forbid();

            var quadra = await _context.QuadrasClube.FirstOrDefaultAsync(q => q.Id == quadraClubeId && q.ClubeId == clubeId);
            if (quadra == null) return NotFound();

            _context.HorariosMarcacaoDisponivel.Add(new HorarioMarcacaoDisponivel
            {
                ClubeId = clubeId,
                QuadraClubeId = quadraClubeId,
                DiaSemana = diaSemana,
                HoraInicio = horaInicio,
                HoraFim = horaFim,
                DuracaoMinutos = duracaoMinutos,
                // Zero e nulo significam a mesma coisa aqui (quadra sem cobrança pelo app),
                // então normaliza pra não ter dois jeitos de dizer "de graça".
                Preco = preco > 0 ? preco : null
            });
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", new { clubeId });
        }

        // Ajusta o preço de uma regra já criada, pro dono não ter que apagar e recriar o
        // horário só pra mudar o valor.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtualizarPreco(int id, int clubeId, decimal? preco)
        {
            var meuId = ObterJogadorIdLogado();
            if (!await EhDonoOuAdminDoClubeAsync(clubeId, meuId)) return Forbid();

            var horario = await _context.HorariosMarcacaoDisponivel.FirstOrDefaultAsync(h => h.Id == id && h.ClubeId == clubeId);
            if (horario != null)
            {
                horario.Preco = preco > 0 ? preco : null;
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Preço atualizado.";
            }

            return RedirectToAction("Index", new { clubeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlternarHorario(int id, int clubeId)
        {
            var meuId = ObterJogadorIdLogado();
            if (!await EhDonoOuAdminDoClubeAsync(clubeId, meuId)) return Forbid();

            var horario = await _context.HorariosMarcacaoDisponivel.FirstOrDefaultAsync(h => h.Id == id && h.ClubeId == clubeId);
            if (horario != null)
            {
                horario.Ativo = !horario.Ativo;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Index", new { clubeId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AvisarVagaHoje(int clubeId)
        {
            var meuId = ObterJogadorIdLogado();
            if (!await EhDonoOuAdminDoClubeAsync(clubeId, meuId)) return Forbid();

            var total = await _horarioMarcacaoService.NotificarHorarioVagoAsync(clubeId);
            TempData["Sucesso"] = total > 0
                ? $"{total} jogador(es) da região avisado(s)."
                : "Nenhum jogador elegível pra avisar (confira se o clube tem cidade definida e se alguém marcou preferência de receber esse aviso).";

            return RedirectToAction("Index", new { clubeId });
        }
    }
}
