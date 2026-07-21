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
    [Authorize] // Só quem está logado pode acessar as rotas de aula
    public class AulasController : Controller
    {
        private readonly DbPadelContext _context;
        private readonly IEmailService _emailService;
        private readonly IGoogleCalendarService _googleCalendarService;
        private readonly ILogger<AulasController> _logger;

        private const int DuracaoPadraoMinutos = 60;
        private const int DiasDeJanelaBusca = 14;

        public AulasController(
            DbPadelContext context,
            IEmailService emailService,
            IGoogleCalendarService googleCalendarService,
            ILogger<AulasController> logger)
        {
            _context = context;
            _emailService = emailService;
            _googleCalendarService = googleCalendarService;
            _logger = logger;
        }

        // ===================== BUSCA E SOLICITAÇÃO (ALUNO) =====================

        // 1. TELA DE BUSCA (professor -> local -> horário)
        [HttpGet]
        public async Task<IActionResult> Solicitar()
        {
            var professores = await _context.Jogadores
                .Where(j => j.IsProfessor)
                .ToListAsync();

            return View(professores);
        }

        [HttpGet]
        public async Task<IActionResult> ObterLocais(int professorId)
        {
            var locais = await _context.LocaisAula
                .Where(l => l.ProfessorId == professorId && l.Ativo)
                .Select(l => new { l.Id, l.Nome, l.Endereco, l.PrecoPadrao })
                .ToListAsync();

            return Json(locais);
        }

        [HttpGet]
        public async Task<IActionResult> ObterHorarios(int professorId, int localId)
        {
            var regras = await _context.HorariosDisponiveis
                .Where(h => h.ProfessorId == professorId && h.LocalAulaId == localId && h.Ativo)
                .ToListAsync();

            if (regras.Count == 0)
            {
                return Json(Array.Empty<object>());
            }

            var aulasOcupadas = (await _context.Aulas
                .Where(a => a.ProfessorId == professorId &&
                            (a.Status == "Pendente" || a.Status == "Confirmada") &&
                            a.DataHora >= DateTime.Today)
                .Select(a => a.DataHora)
                .ToListAsync())
                .ToHashSet();

            var slots = new List<DateTime>();
            var hoje = DateTime.Today;

            for (var dia = 0; dia < DiasDeJanelaBusca; dia++)
            {
                var data = hoje.AddDays(dia);
                var regrasDoDia = regras.Where(r => (int)data.DayOfWeek == r.DiaSemana);

                foreach (var regra in regrasDoDia)
                {
                    var horario = data.Add(regra.HoraInicio);
                    var fimJanela = data.Add(regra.HoraFim);

                    while (horario.AddMinutes(regra.DuracaoMinutos) <= fimJanela)
                    {
                        if (horario > DateTime.Now && !aulasOcupadas.Contains(horario))
                        {
                            slots.Add(horario);
                        }
                        horario = horario.AddMinutes(regra.DuracaoMinutos);
                    }
                }
            }

            slots.Sort();
            return Json(slots.Select(s => new { valor = s.ToString("yyyy-MM-ddTHH:mm:ss") }));
        }

        // 2. SALVA A SOLICITAÇÃO (fica Pendente até o professor confirmar)
        [HttpPost]
        public async Task<IActionResult> Solicitar(int professorId, int localId, DateTime dataHora)
        {
            var alunoIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(alunoIdValue, out var alunoId))
            {
                return RedirectToAction("Perfil", "Auth");
            }

            var local = await _context.LocaisAula.FirstOrDefaultAsync(l => l.Id == localId && l.ProfessorId == professorId);
            if (local == null)
            {
                TempData["Erro"] = "Local inválido para este professor.";
                return RedirectToAction("Solicitar");
            }

            bool horarioOcupado = await _context.Aulas
                .AnyAsync(a => a.ProfessorId == professorId &&
                               a.DataHora == dataHora &&
                               (a.Status == "Pendente" || a.Status == "Confirmada"));

            if (horarioOcupado)
            {
                TempData["Erro"] = "Este horário acabou de ser reservado. Escolha outro.";
                return RedirectToAction("Solicitar");
            }

            var novaAula = new Aula
            {
                ProfessorId = professorId,
                AlunoId = alunoId,
                LocalAulaId = localId,
                DataHora = dataHora,
                Preco = local.PrecoPadrao,
                Status = "Pendente"
            };

            _context.Aulas.Add(novaAula);
            await _context.SaveChangesAsync();

            var professor = await _context.Jogadores.FindAsync(professorId);
            var aluno = await _context.Jogadores.FindAsync(alunoId);

            var linkAceitar = Url.Action("ConfirmarPorEmail", "Aulas",
                new { aulaId = novaAula.Id, token = novaAula.TokenConfirmacao, aceitar = true }, Request.Scheme);
            var linkRecusar = Url.Action("ConfirmarPorEmail", "Aulas",
                new { aulaId = novaAula.Id, token = novaAula.TokenConfirmacao, aceitar = false }, Request.Scheme);

            try
            {
                await _emailService.EnviarAsync(professor!.Email!, professor.Nome,
                    "Nova solicitação de aula - Padelizou",
                    $@"<p>Olá {professor.Nome},</p>
                       <p><strong>{aluno!.Nome}</strong> solicitou uma aula em <strong>{local.Nome}</strong>
                       no dia <strong>{dataHora:dd/MM/yyyy 'às' HH:mm}</strong>.</p>
                       <p>
                         <a href=""{linkAceitar}"" style=""padding:10px 20px;background:#28a745;color:#fff;text-decoration:none;border-radius:6px;"">Aceitar</a>
                         &nbsp;
                         <a href=""{linkRecusar}"" style=""padding:10px 20px;background:#dc3545;color:#fff;text-decoration:none;border-radius:6px;"">Recusar</a>
                       </p>");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao enviar e-mail de solicitação para a aula {AulaId}", novaAula.Id);
            }

            return RedirectToAction("SolicitacaoEnviada", new { id = novaAula.Id });
        }

        [HttpGet]
        public async Task<IActionResult> SolicitacaoEnviada(int id)
        {
            var aula = await _context.Aulas
                .Include(a => a.Professor)
                .Include(a => a.LocalAula)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (aula == null) return NotFound();

            return View(aula);
        }

        // ===================== CONFIRMAÇÃO (PROFESSOR) =====================

        // Chamado a partir da Minha Agenda (professor logado)
        [HttpPost]
        public async Task<IActionResult> ConfirmarSolicitacao(int aulaId, bool aceitar)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var professorId))
            {
                return RedirectToAction("Perfil", "Auth");
            }

            var aula = await _context.Aulas
                .Include(a => a.Aluno)
                .Include(a => a.Professor)
                .Include(a => a.LocalAula)
                .FirstOrDefaultAsync(a => a.Id == aulaId && a.ProfessorId == professorId);

            if (aula == null || aula.Status != "Pendente")
            {
                return RedirectToAction("MinhaAgenda");
            }

            var linkWhatsApp = await ProcessarDecisaoAsync(aula, aceitar);
            TempData["Sucesso"] = aceitar ? "Aula confirmada!" : "Solicitação recusada.";
            TempData["WhatsAppLink"] = linkWhatsApp;

            return RedirectToAction("MinhaAgenda");
        }

        // Chamado a partir do link enviado por e-mail (sem exigir login)
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> ConfirmarPorEmail(int aulaId, Guid token, bool aceitar)
        {
            var aula = await _context.Aulas
                .Include(a => a.Aluno)
                .Include(a => a.Professor)
                .Include(a => a.LocalAula)
                .FirstOrDefaultAsync(a => a.Id == aulaId && a.TokenConfirmacao == token);

            if (aula == null)
            {
                return NotFound();
            }

            if (aula.Status != "Pendente")
            {
                ViewBag.JaProcessada = true;
                return View(aula);
            }

            var linkWhatsApp = await ProcessarDecisaoAsync(aula, aceitar);
            ViewBag.Aceitou = aceitar;
            ViewBag.WhatsAppLink = linkWhatsApp;

            return View(aula);
        }

        // Aplica o aceite/recusa: atualiza status, cria evento no Google Calendar (se aceito e conectado)
        // e dispara o e-mail ao aluno. Retorna o link wa.me pronto para o professor avisar o aluno.
        private async Task<string> ProcessarDecisaoAsync(Aula aula, bool aceitar)
        {
            aula.Status = aceitar ? "Confirmada" : "Recusada";
            await _context.SaveChangesAsync();

            if (aceitar)
            {
                try
                {
                    var eventId = await _googleCalendarService.CriarEventoAsync(aula);
                    if (eventId != null)
                    {
                        aula.GoogleEventId = eventId;
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao criar evento na Google Agenda para a aula {AulaId}", aula.Id);
                }

                try
                {
                    await _emailService.EnviarAsync(aula.Aluno.Email!, aula.Aluno.Nome,
                        "Sua aula foi confirmada! - Padelizou",
                        $@"<p>Olá {aula.Aluno.Nome},</p>
                           <p>O professor <strong>{aula.Professor.Nome}</strong> confirmou sua aula em
                           <strong>{aula.LocalAula.Nome}</strong> ({aula.LocalAula.Endereco})
                           no dia <strong>{aula.DataHora:dd/MM/yyyy 'às' HH:mm}</strong>.</p>");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao enviar e-mail de confirmação para a aula {AulaId}", aula.Id);
                }
            }
            else
            {
                try
                {
                    await _emailService.EnviarAsync(aula.Aluno.Email!, aula.Aluno.Nome,
                        "Sua solicitação de aula foi recusada - Padelizou",
                        $@"<p>Olá {aula.Aluno.Nome},</p>
                           <p>O professor <strong>{aula.Professor.Nome}</strong> não pôde confirmar a aula
                           no dia <strong>{aula.DataHora:dd/MM/yyyy 'às' HH:mm}</strong>. Tente outro horário.</p>");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao enviar e-mail de recusa para a aula {AulaId}", aula.Id);
                }
            }

            var mensagem = aceitar
                ? $"Olá {aula.Aluno.Nome}! Sua aula comigo dia {aula.DataHora:dd/MM 'às' HH:mm} em {aula.LocalAula.Nome} está confirmada!"
                : $"Olá {aula.Aluno.Nome}, infelizmente não vou poder dar a aula dia {aula.DataHora:dd/MM 'às' HH:mm}. Vamos combinar outro horário?";

            return WhatsAppLinkHelper.GerarLink(aula.Aluno.Celular, mensagem);
        }

        // ===================== GESTÃO DO PROFESSOR =====================

        [HttpGet]
        public async Task<IActionResult> MeusLocais()
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var locais = await _context.LocaisAula
                .Where(l => l.ProfessorId == professorId)
                .OrderByDescending(l => l.Ativo)
                .ThenBy(l => l.Nome)
                .ToListAsync();

            return View(locais);
        }

        [HttpPost]
        public async Task<IActionResult> CriarLocal(string nome, string endereco, decimal precoPadrao, decimal? custoPorAula)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            _context.LocaisAula.Add(new LocalAula
            {
                ProfessorId = professorId.Value,
                Nome = nome,
                Endereco = endereco,
                PrecoPadrao = precoPadrao,
                CustoPorAula = custoPorAula,
                Ativo = true
            });
            await _context.SaveChangesAsync();

            return RedirectToAction("MeusLocais");
        }

        [HttpPost]
        public async Task<IActionResult> AtualizarCustoLocal(int id, decimal? custoPorAula)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var local = await _context.LocaisAula.FirstOrDefaultAsync(l => l.Id == id && l.ProfessorId == professorId);
            if (local != null)
            {
                local.CustoPorAula = custoPorAula;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("MeusLocais");
        }

        [HttpPost]
        public async Task<IActionResult> AlternarLocal(int id)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var local = await _context.LocaisAula.FirstOrDefaultAsync(l => l.Id == id && l.ProfessorId == professorId);
            if (local != null)
            {
                local.Ativo = !local.Ativo;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("MeusLocais");
        }

        [HttpGet]
        public async Task<IActionResult> MeusHorarios()
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            ViewBag.Locais = await _context.LocaisAula
                .Where(l => l.ProfessorId == professorId && l.Ativo)
                .ToListAsync();

            var horarios = await _context.HorariosDisponiveis
                .Include(h => h.LocalAula)
                .Where(h => h.ProfessorId == professorId)
                .OrderBy(h => h.DiaSemana)
                .ThenBy(h => h.HoraInicio)
                .ToListAsync();

            return View(horarios);
        }

        [HttpPost]
        public async Task<IActionResult> CriarHorario(int localAulaId, int diaSemana, TimeSpan horaInicio, TimeSpan horaFim, int duracaoMinutos)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var local = await _context.LocaisAula.FirstOrDefaultAsync(l => l.Id == localAulaId && l.ProfessorId == professorId);
            if (local == null)
            {
                return RedirectToAction("MeusHorarios");
            }

            _context.HorariosDisponiveis.Add(new HorarioDisponivel
            {
                ProfessorId = professorId.Value,
                LocalAulaId = localAulaId,
                DiaSemana = diaSemana,
                HoraInicio = horaInicio,
                HoraFim = horaFim,
                DuracaoMinutos = duracaoMinutos <= 0 ? DuracaoPadraoMinutos : duracaoMinutos,
                Ativo = true
            });
            await _context.SaveChangesAsync();

            return RedirectToAction("MeusHorarios");
        }

        [HttpPost]
        public async Task<IActionResult> AlternarHorario(int id)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var horario = await _context.HorariosDisponiveis.FirstOrDefaultAsync(h => h.Id == id && h.ProfessorId == professorId);
            if (horario != null)
            {
                horario.Ativo = !horario.Ativo;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("MeusHorarios");
        }

        // 3. TELA DE GERENCIAMENTO DO PROFESSOR (Minha Agenda)
        [HttpGet]
        public async Task<IActionResult> MinhaAgenda()
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var agenda = await _context.Aulas
                .Include(a => a.Aluno)
                .Include(a => a.LocalAula)
                .Where(a => a.ProfessorId == professorId)
                .OrderBy(a => a.Status == "Pendente" ? 0 : 1)
                .ThenBy(a => a.DataHora)
                .ToListAsync();

            ViewBag.GoogleConectado = await _googleCalendarService.EstaConectadoAsync(professorId.Value);

            return View(agenda);
        }

        // 4. ATUALIZAR STATUS DA AULA (Finalizar ou Cancelar) — só a partir de uma aula já Confirmada
        [HttpPost]
        public async Task<IActionResult> AtualizarStatus(int aulaId, string novoStatus)
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Perfil", "Auth");
            }

            var aula = await _context.Aulas.FindAsync(aulaId);

            var transicaoValida = novoStatus == "Realizada" || novoStatus == "Cancelada";
            if (aula != null && aula.ProfessorId == userId && aula.Status == "Confirmada" && transicaoValida)
            {
                aula.Status = novoStatus;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("MinhaAgenda");
        }

        // 5. TELA DE HISTÓRICO DO ALUNO (Minhas Aulas)
        [HttpGet]
        public async Task<IActionResult> MinhasAulas()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId))
            {
                return RedirectToAction("Perfil", "Auth");
            }

            var minhasAulas = await _context.Aulas
                .Include(a => a.Professor)
                .Include(a => a.LocalAula)
                .Where(a => a.AlunoId == userId)
                .OrderByDescending(a => a.DataHora)
                .ToListAsync();

            return View(minhasAulas);
        }

        // 6. RELATÓRIO DO PROFESSOR (aulas dadas, alunos, receita e gasto por período)
        [HttpGet]
        public async Task<IActionResult> Relatorio(DateTime? dataInicio, DateTime? dataFim)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var inicio = (dataInicio ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)).Date;
            var fim = (dataFim ?? DateTime.Today).Date.AddDays(1).AddSeconds(-1);

            var aulas = await _context.Aulas
                .Include(a => a.LocalAula)
                .Where(a => a.ProfessorId == professorId &&
                            a.Status == "Realizada" &&
                            a.DataHora >= inicio && a.DataHora <= fim)
                .ToListAsync();

            var relatorio = new RelatorioAulasViewModel
            {
                DataInicio = inicio,
                DataFim = fim,
                TotalAulas = aulas.Count,
                TotalAlunosDiferentes = aulas.Select(a => a.AlunoId).Distinct().Count(),
                TotalRecebido = aulas.Sum(a => a.Preco),
                PorLocal = aulas
                    .GroupBy(a => a.LocalAula)
                    .Select(g => new RelatorioPorLocal
                    {
                        NomeLocal = g.Key.Nome,
                        QuantidadeAulas = g.Count(),
                        Recebido = g.Sum(a => a.Preco),
                        Gasto = g.Key.CustoPorAula.HasValue ? g.Key.CustoPorAula.Value * g.Count() : null
                    })
                    .OrderByDescending(l => l.Recebido)
                    .ToList()
            };
            relatorio.TotalGasto = relatorio.PorLocal.Any(l => l.Gasto.HasValue)
                ? relatorio.PorLocal.Sum(l => l.Gasto ?? 0)
                : null;

            return View(relatorio);
        }

        private async Task<int?> ObterProfessorLogadoAsync()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId))
            {
                return null;
            }

            var jogador = await _context.Jogadores.FindAsync(userId);
            return jogador != null && jogador.IsProfessor ? userId : null;
        }
    }
}
