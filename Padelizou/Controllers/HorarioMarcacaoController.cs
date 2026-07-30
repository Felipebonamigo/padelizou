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

            // Janela que não comporta a duração publicava um horário que a tela do jogador
            // simplesmente não oferece — sem erro, parecendo clube sem vaga. Mesma armadilha
            // que já existia na agenda do professor (ver Services/HorarioDoClube).
            if (HorarioDoClube.ProblemaCom(horaInicio, horaFim, duracaoMinutos) is { } problema)
            {
                TempData["Erro"] = problema;
                return RedirectToAction("Index", new { clubeId });
            }

            _context.HorariosMarcacaoDisponivel.Add(new HorarioMarcacaoDisponivel
            {
                ClubeId = clubeId,
                QuadraClubeId = quadraClubeId,
                DiaSemana = diaSemana,
                HoraInicio = horaInicio,
                HoraFim = horaFim,
                DuracaoMinutos = duracaoMinutos,
                Preco = HorarioDoClube.NormalizarPreco(preco)
            });
            await _context.SaveChangesAsync();

            return RedirectToAction("Index", new { clubeId });
        }

        // Ajusta preço E duração de uma regra já criada, pro dono não ter que apagar e recriar
        // o horário só pra mudar o valor ou trocar de 1h pra 1h30.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AtualizarHorario(int id, int clubeId, decimal? preco, int? duracaoMinutos)
        {
            var meuId = ObterJogadorIdLogado();
            if (!await EhDonoOuAdminDoClubeAsync(clubeId, meuId)) return Forbid();

            var horario = await _context.HorariosMarcacaoDisponivel.FirstOrDefaultAsync(h => h.Id == id && h.ClubeId == clubeId);
            if (horario == null) return RedirectToAction("Index", new { clubeId });

            var novaDuracao = duracaoMinutos ?? horario.DuracaoMinutos;
            if (HorarioDoClube.ProblemaCom(horario.HoraInicio, horario.HoraFim, novaDuracao) is { } problema)
            {
                TempData["Erro"] = problema;
                return RedirectToAction("Index", new { clubeId });
            }

            horario.Preco = HorarioDoClube.NormalizarPreco(preco);
            horario.DuracaoMinutos = novaDuracao;
            await _context.SaveChangesAsync();
            TempData["Sucesso"] = "Horário atualizado.";

            return RedirectToAction("Index", new { clubeId });
        }

        // Aplica preço e/ou duração em TODOS os horários do clube de uma vez. Sem isto, um
        // clube com 3 quadras × 7 dias × 2 faixas precisa de 42 edições pra reajustar o preço.
        //
        // Campo em branco = NÃO MEXE naquilo. É a única semântica segura aqui: se vazio
        // significasse "de graça", clicar pra mudar só a duração zeraria o preço do clube
        // inteiro. Deixar de graça continua sendo possível linha a linha.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AplicarEmTodos(int clubeId, decimal? precoTodos, int? duracaoTodos, int? quadraClubeId)
        {
            var meuId = ObterJogadorIdLogado();
            if (!await EhDonoOuAdminDoClubeAsync(clubeId, meuId)) return Forbid();

            if (precoTodos == null && duracaoTodos == null)
            {
                TempData["Erro"] = "Informe o preço, a duração, ou os dois — o que ficar em branco não é alterado.";
                return RedirectToAction("Index", new { clubeId });
            }

            var horarios = await _context.HorariosMarcacaoDisponivel
                .Where(h => h.ClubeId == clubeId && (quadraClubeId == null || h.QuadraClubeId == quadraClubeId))
                .ToListAsync();

            // Horário cuja janela não comporta a duração nova fica de fora, e a tela diz
            // quantos foram: aplicar mesmo assim criaria horários invisíveis pro jogador.
            var pulados = 0;
            var mexidos = 0;
            foreach (var h in horarios)
            {
                if (duracaoTodos is int nova)
                {
                    if (HorarioDoClube.ProblemaCom(h.HoraInicio, h.HoraFim, nova) != null) { pulados++; continue; }
                    h.DuracaoMinutos = nova;
                }
                if (precoTodos != null) h.Preco = HorarioDoClube.NormalizarPreco(precoTodos);
                mexidos++;
            }
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = pulados == 0
                ? $"{mexidos} horário(s) atualizado(s)."
                : $"{mexidos} horário(s) atualizado(s). {pulados} ficaram como estavam: a janela deles é curta demais pra essa duração.";

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
