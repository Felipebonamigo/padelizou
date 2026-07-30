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

        // Monta a grade de uma vez: uma regra por (quadra × dia escolhido), com a mesma
        // janela, duração e preço. O clube novo tem 3 quadras × 7 dias — regra por regra
        // são 21 formulários iguais, e é onde ele desiste no meio e fica invisível.
        // A decisão por slot (criar / religar pausada idêntica / pular sobreposta) mora em
        // HorarioDoClube.DecidirSlot, pura e testada.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GerarGrade(int clubeId, int? quadraClubeId, int[]? dias,
            TimeSpan horaInicio, TimeSpan horaFim, int duracaoMinutos, decimal? preco)
        {
            var meuId = ObterJogadorIdLogado();
            if (!await EhDonoOuAdminDoClubeAsync(clubeId, meuId)) return Forbid();

            if (dias == null || dias.Length == 0)
            {
                TempData["Erro"] = "Escolha pelo menos um dia da semana.";
                return RedirectToAction("Index", new { clubeId });
            }

            if (HorarioDoClube.ProblemaCom(horaInicio, horaFim, duracaoMinutos) is { } problema)
            {
                TempData["Erro"] = problema;
                return RedirectToAction("Index", new { clubeId });
            }

            var quadras = await _context.QuadrasClube
                .Where(q => q.ClubeId == clubeId && q.Ativa
                         && (quadraClubeId == null || q.Id == quadraClubeId))
                .ToListAsync();
            if (quadras.Count == 0)
            {
                TempData["Erro"] = "Cadastre pelo menos uma quadra antes de gerar a grade.";
                return RedirectToAction("Index", new { clubeId });
            }

            var existentes = await _context.HorariosMarcacaoDisponivel
                .Where(h => h.ClubeId == clubeId)
                .ToListAsync();

            int criados = 0, religados = 0, pulados = 0;
            foreach (var quadra in quadras)
            {
                foreach (var dia in dias.Distinct().Where(d => d is >= 0 and <= 6))
                {
                    var daQuadraEDia = existentes.Where(h => h.QuadraClubeId == quadra.Id && h.DiaSemana == dia);
                    var (decisao, alvo) = HorarioDoClube.DecidirSlot(daQuadraEDia, horaInicio, horaFim, duracaoMinutos);

                    switch (decisao)
                    {
                        case HorarioDoClube.DecisaoDaGrade.Criar:
                            _context.HorariosMarcacaoDisponivel.Add(new HorarioMarcacaoDisponivel
                            {
                                ClubeId = clubeId,
                                QuadraClubeId = quadra.Id,
                                DiaSemana = dia,
                                HoraInicio = horaInicio,
                                HoraFim = horaFim,
                                DuracaoMinutos = duracaoMinutos,
                                Preco = HorarioDoClube.NormalizarPreco(preco),
                            });
                            criados++;
                            break;

                        case HorarioDoClube.DecisaoDaGrade.Religar:
                            alvo!.Ativo = true;
                            alvo.Preco = HorarioDoClube.NormalizarPreco(preco);
                            religados++;
                            break;

                        default:
                            pulados++;
                            break;
                    }
                }
            }
            await _context.SaveChangesAsync();

            var partes = new List<string>();
            if (criados > 0) partes.Add($"{criados} horário(s) criado(s)");
            if (religados > 0) partes.Add($"{religados} religado(s)");
            if (partes.Count == 0) partes.Add("nada novo pra criar");
            var texto = $"Grade de {HorarioDoClube.Rotulo(duracaoMinutos)}: {string.Join(", ", partes)}.";
            if (pulados > 0) texto += $" {pulados} ficaram como estavam — já havia horário publicado naquela faixa.";

            TempData["Sucesso"] = texto;
            return RedirectToAction("Index", new { clubeId });
        }

        // Apaga todos os horários do clube (ou só de uma quadra) de uma vez. O uso real é
        // "montei a grade errada, quero recomeçar" — por isso é DELETE de verdade, não
        // pausa: o que o dono quer é a lista limpa pra gerar de novo.
        //
        // ⚠️ Consequência dita na confirmação da tela: o preço de reserva antiga sai da
        // REGRA (não fica gravado na marcação), então apagar regras com histórico faz o
        // financeiro perder aqueles valores. Quem só quer sumir da tela do jogador usa
        // Desativar, que existe linha a linha.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExcluirTodos(int clubeId, int? quadraClubeId)
        {
            var meuId = ObterJogadorIdLogado();
            if (!await EhDonoOuAdminDoClubeAsync(clubeId, meuId)) return Forbid();

            var horarios = await _context.HorariosMarcacaoDisponivel
                .Where(h => h.ClubeId == clubeId && (quadraClubeId == null || h.QuadraClubeId == quadraClubeId))
                .ToListAsync();

            if (horarios.Count == 0)
            {
                TempData["Erro"] = "Não há horário nenhum pra excluir.";
                return RedirectToAction("Index", new { clubeId });
            }

            _context.HorariosMarcacaoDisponivel.RemoveRange(horarios);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"{horarios.Count} horário(s) excluído(s). A grade está limpa — gere uma nova quando quiser.";
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
