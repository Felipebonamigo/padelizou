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
    public partial class AulasController : Controller
    {
        private readonly DbPadelContext _context;
        private readonly IEmailService _emailService;
        private readonly IGoogleCalendarService _googleCalendarService;
        private readonly IPushNotificationService _pushService;
        private readonly PlanoProfessorSettings _plano;
        private readonly ILogger<AulasController> _logger;

        private const int DuracaoPadraoMinutos = 60;
        private const int DiasDeJanelaBusca = 14;

        public AulasController(
            DbPadelContext context,
            IEmailService emailService,
            IGoogleCalendarService googleCalendarService,
            IPushNotificationService pushService,
            Microsoft.Extensions.Options.IOptions<PlanoProfessorSettings> plano,
            ILogger<AulasController> logger)
        {
            _context = context;
            _emailService = emailService;
            _googleCalendarService = googleCalendarService;
            _pushService = pushService;
            _plano = plano.Value;
            _logger = logger;
        }


        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            // Antes de abrir o painel, cobra o que falta pro aluno conseguir marcar. Um aviso no
            // topo já existia e não bastou — professor nenhum tinha cidade. Não dá pra entrar em
            // laço: MinhasCidades e MeusLocais não têm esta checagem, e as duas salvam e voltam.
            var pendencia = await CadastroDeProfessor.PendenciaAsync(_context, professorId.Value);

            if (pendencia != PendenciaDoProfessor.Nenhuma)
            {
                TempData["AvisoCadastroProfessor"] = CadastroDeProfessor.MensagemPara(pendencia);
                return RedirectToAction(CadastroDeProfessor.AcaoPara(pendencia));
            }

            var todasAulas = await _context.Aulas
                .Include(a => a.Aluno)
                .Include(a => a.LocalAula)
                .Where(a => a.ProfessorId == professorId)
                .ToListAsync();

            var hoje = DateTime.Today;
            var inicioSemana = hoje.AddDays(-(int)hoje.DayOfWeek);
            var fimSemana = inicioSemana.AddDays(7);

            // Os acordos particulares deste professor, indexados pela mesma chave que agrupa
            // os alunos logo abaixo — é o que casa "o Pedro paga 90" com a linha do Pedro.
            var precosCombinados = await _context.PrecosDeAluno
                .Where(p => p.ProfessorId == professorId)
                .ToListAsync();
            var precoPorChave = precosCombinados.ToLookup(p => PrecoDaAula.Chave(p));

            var alunos = todasAulas
                .GroupBy(a => PrecoDaAula.Chave(a))
                .Select(g =>
                {
                    var combinado = precoPorChave[g.Key].FirstOrDefault();
                    return new AlunoResumo
                    {
                        Nome = g.First().Aluno?.ComoChamar ?? g.First().NomeAlunoAvulso ?? "Aluno avulso",
                        Celular = g.First().Aluno?.Celular ?? g.First().TelefoneAlunoAvulso,
                        TotalAulas = g.Count(a => a.Status != "Cancelada" && a.Status != "Recusada"),
                        UltimaAula = g.Max(a => a.DataHora),
                        ProximaAula = g.Where(a => a.DataHora >= hoje && (a.Status == "Pendente" || a.Status == "Confirmada"))
                                        .OrderBy(a => a.DataHora)
                                        .Select(a => (DateTime?)a.DataHora)
                                        .FirstOrDefault(),
                        AlunoId = g.First().AlunoId,
                        NomeAvulso = g.First().NomeAlunoAvulso,
                        PrecoCombinado = combinado?.Preco,
                        PrecoCombinadoId = combinado?.Id,
                    };
                })
                .OrderByDescending(a => a.UltimaAula)
                .ToList();

            var locais = await _context.LocaisAula
                .Where(l => l.ProfessorId == professorId)
                .OrderByDescending(l => l.Ativo)
                .ThenBy(l => l.Nome)
                .ToListAsync();

            var cidades = await _context.ProfessorCidades
                .Where(pc => pc.ProfessorId == professorId)
                .Select(pc => pc.Cidade)
                .OrderBy(c => c.Nome)
                .ToListAsync();

            var inicioMes = new DateTime(hoje.Year, hoje.Month, 1);
            var fimMes = inicioMes.AddMonths(1).AddSeconds(-1);
            var financeiro = await CalcularRelatorioAsync(professorId.Value, inicioMes, fimMes);

            var painel = new PainelProfessorViewModel
            {
                TotalAlunosAtivos = alunos.Count,
                AulasEstaSemana = todasAulas.Count(a => a.DataHora >= inicioSemana && a.DataHora < fimSemana &&
                                                         (a.Status == "Pendente" || a.Status == "Confirmada")),
                AulasPendentes = todasAulas.Count(a => a.Status == "Pendente"),
                FinanceiroMesAtual = financeiro,
                Alunos = alunos,
                Locais = locais,
                ProximasAulas = todasAulas
                    .Where(a => a.DataHora >= hoje && (a.Status == "Pendente" || a.Status == "Confirmada"))
                    .OrderBy(a => a.DataHora)
                    .Take(10)
                    .ToList(),
                MinhasCidades = cidades
            };

            ViewBag.ProfessorId = professorId;
            var professor = await _context.Jogadores.FindAsync(professorId);
            ViewBag.Professor = professor;

            // O relógio dos 15 dias de teste do plano começa na primeira visita ao painel —
            // é aqui que o professor passa a ver a contagem (ver Services/PlanoDoProfessor).
            if (professor != null)
            {
                if (professor.TesteProfessorInicio == null)
                {
                    professor.TesteProfessorInicio = DateTime.Now;
                    await _context.SaveChangesAsync();
                }
                ViewBag.SituacaoPlano = PlanoDoProfessor.SituacaoDe(professor, DateTime.Now, _plano);
                ViewBag.FimDoTestePlano = PlanoDoProfessor.FimDoTeste(professor, _plano);
            }

            return View(painel);
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

        // ------------------------------------------------------------------
        // Anotações da aula: o caderno compartilhado entre professor e aluno.
        // Cada linha guarda quem escreveu — a regra de quem participa mora em
        // Services/AnotacoesDeAula.
        // ------------------------------------------------------------------

    }
}
