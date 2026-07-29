using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;

namespace Padelizou.Controllers;

// Página pública do professor e avaliações. Separado de AulasController de propósito:
// lá é a área logada de quem dá aula; aqui é a vitrine que qualquer um vê.
public class ProfessoresController : Controller
{
    private readonly DbPadelContext _context;
    private readonly IPushNotificationService _pushService;
    private readonly ILogger<ProfessoresController> _logger;

    public ProfessoresController(DbPadelContext context, IPushNotificationService pushService,
        ILogger<ProfessoresController> logger)
    {
        _context = context;
        _pushService = pushService;
        _logger = logger;
    }

    // Vitrine: quem são os professores da plataforma.
    [HttpGet]
    public async Task<IActionResult> Index(int? cidadeId)
    {
        var query = _context.Jogadores.Where(j => j.IsProfessor);

        if (cidadeId != null)
        {
            query = query.Where(j => _context.ProfessorCidades.Any(pc => pc.ProfessorId == j.Id && pc.CidadeId == cidadeId));
        }

        var professores = await query.OrderBy(j => j.Nome).ToListAsync();
        var ids = professores.Select(p => p.Id).ToList();

        // Média e preço em duas consultas, não uma por professor.
        var medias = (await _context.AvaliacoesProfessor
                .Where(a => ids.Contains(a.ProfessorId))
                .Select(a => new { a.ProfessorId, a.Nota })
                .ToListAsync())
            .GroupBy(a => a.ProfessorId)
            .ToDictionary(g => g.Key, g => (Media: g.Average(x => x.Nota), Total: g.Count()));

        var precos = (await _context.LocaisAula
                .Where(l => ids.Contains(l.ProfessorId) && l.Ativo)
                .Select(l => new { l.ProfessorId, l.PrecoPadrao })
                .ToListAsync())
            .GroupBy(l => l.ProfessorId)
            .ToDictionary(g => g.Key, g => g.Min(x => x.PrecoPadrao));

        ViewBag.Medias = medias;
        ViewBag.Precos = precos;
        ViewBag.Cidades = await _context.Cidades.OrderBy(c => c.Nome).ToListAsync();
        ViewBag.CidadeId = cidadeId;

        return View(professores);
    }

    [HttpGet]
    public async Task<IActionResult> Perfil(int id)
    {
        var professor = await _context.Jogadores.FirstOrDefaultAsync(j => j.Id == id && j.IsProfessor);
        if (professor == null) return NotFound();

        var locais = await _context.LocaisAula
            .Where(l => l.ProfessorId == id && l.Ativo)
            .OrderBy(l => l.PrecoPadrao)
            .ToListAsync();

        var aulas = await _context.Aulas
            .Where(a => a.ProfessorId == id)
            .Select(a => new { a.Status, a.AlunoId })
            .ToListAsync();

        var avaliacoes = await _context.AvaliacoesProfessor
            .Include(a => a.Aluno)
            .Where(a => a.ProfessorId == id)
            .OrderByDescending(a => a.AtualizadoEm ?? a.CriadoEm)
            .ToListAsync();

        var vm = new ProfessorPublicoVM
        {
            Professor = professor,
            Locais = locais,
            MenorPreco = locais.Any() ? locais.Min(l => l.PrecoPadrao) : null,
            AulasRealizadas = aulas.Count(a => a.Status == PoliticaAula.Realizada),
            AlunosAtendidos = aulas.Where(a => a.AlunoId != null).Select(a => a.AlunoId).Distinct().Count(),
            Cidades = await _context.ProfessorCidades
                .Where(pc => pc.ProfessorId == id)
                .Select(pc => pc.Cidade.Nome)
                .OrderBy(n => n)
                .ToListAsync(),
            TotalAvaliacoes = avaliacoes.Count,
            MediaNota = avaliacoes.Any() ? Math.Round(avaliacoes.Average(a => a.Nota), 1) : null,
            // O interruptor do professor decide se TEXTO aparece; a nota e a média ficam
            // sempre — são o dado que protege o próximo aluno.
            Depoimentos = professor.DepoimentosDeAulaHabilitados
                ? avaliacoes.Where(a => !string.IsNullOrWhiteSpace(a.Depoimento)).Take(10).ToList()
                : new List<AvaliacaoProfessor>(),
            DepoimentosHabilitados = professor.DepoimentosDeAulaHabilitados,
            PoliticaCancelamento = PoliticaAula.DescreverPolitica(professor),
        };

        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var meuId))
        {
            vm.EhOProprioProfessor = meuId == id;

            // Só avalia quem realmente teve aula com ele — é o que separa isso de
            // comentário aberto e mantém a nota confiável.
            vm.PodeAvaliar = !vm.EhOProprioProfessor && await _context.Aulas
                .AnyAsync(a => a.ProfessorId == id && a.AlunoId == meuId && a.Status == PoliticaAula.Realizada);

            vm.MinhaAvaliacao = avaliacoes.FirstOrDefault(a => a.AlunoId == meuId);
        }

        return View(vm);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Avaliar(int professorId, int nota, string? depoimento)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var alunoId))
            return RedirectToAction("Perfil", "Auth");

        if (!AvaliacaoDoProfessor.NotaValida(nota))
        {
            TempData["Erro"] = "A nota precisa ser de 0 a 10.";
            return RedirectToAction("Perfil", new { id = professorId });
        }

        bool teveAula = await _context.Aulas.AnyAsync(a =>
            a.ProfessorId == professorId && a.AlunoId == alunoId && a.Status == PoliticaAula.Realizada);

        if (!teveAula)
        {
            TempData["Erro"] = "Só quem já teve aula com o professor pode avaliar.";
            return RedirectToAction("Perfil", new { id = professorId });
        }

        // Um aluno, uma avaliação por professor — reavaliar edita a mesma linha.
        var existente = await _context.AvaliacoesProfessor
            .FirstOrDefaultAsync(a => a.ProfessorId == professorId && a.AlunoId == alunoId);

        bool ehNova = existente == null;

        if (existente == null)
        {
            existente = new AvaliacaoProfessor { ProfessorId = professorId, AlunoId = alunoId };
            _context.AvaliacoesProfessor.Add(existente);
        }
        else
        {
            existente.AtualizadoEm = DateTime.Now;
        }

        // O interruptor do professor vale também pra quem manda o POST direto, sem
        // passar pela tela — desligado, texto não entra por porta nenhuma.
        var professorAvaliado = await _context.Jogadores.FindAsync(professorId);

        existente.Nota = nota;
        existente.Depoimento = AvaliacaoDoProfessor.DepoimentoFinal(
            professorAvaliado?.DepoimentosDeAulaHabilitados ?? true, depoimento);

        await _context.SaveChangesAsync();

        if (ehNova)
        {
            try
            {
                var aluno = await _context.Jogadores.FindAsync(alunoId);
                await _pushService.EnviarParaJogadorAsync(professorId,
                    "Você recebeu uma avaliação",
                    $"{aluno?.Nome ?? "Um aluno"} te deu nota {nota}/10.",
                    Url.Action("Perfil", "Professores", new { id = professorId }));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao notificar avaliação do professor {ProfessorId}", professorId);
            }
        }

        TempData["Sucesso"] = ehNova ? "Avaliação enviada. Obrigado!" : "Sua avaliação foi atualizada.";
        return RedirectToAction("Perfil", new { id = professorId });
    }

    // O interruptor dos comentários: só o próprio professor liga/desliga a exibição de
    // depoimentos na página dele. A nota não passa por aqui — nota não se desliga.
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> AlternarDepoimentos()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var meuId))
            return RedirectToAction("Perfil", "Auth");

        var professor = await _context.Jogadores.FindAsync(meuId);
        if (professor == null || !professor.IsProfessor) return Forbid();

        professor.DepoimentosDeAulaHabilitados = !professor.DepoimentosDeAulaHabilitados;
        await _context.SaveChangesAsync();

        TempData["Sucesso"] = professor.DepoimentosDeAulaHabilitados
            ? "Comentários de alunos voltaram a aparecer na sua página."
            : "Comentários de alunos não aparecem mais na sua página (as notas continuam).";
        return RedirectToAction("Perfil", new { id = meuId });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> SalvarApresentacao(string? apresentacao, string? experiencia)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var meuId))
            return RedirectToAction("Perfil", "Auth");

        var professor = await _context.Jogadores.FindAsync(meuId);
        if (professor == null || !professor.IsProfessor) return Forbid();

        professor.ApresentacaoProfessor = string.IsNullOrWhiteSpace(apresentacao) ? null : apresentacao.Trim();
        professor.ExperienciaProfessor = string.IsNullOrWhiteSpace(experiencia) ? null : experiencia.Trim();

        await _context.SaveChangesAsync();
        TempData["Sucesso"] = "Sua apresentação foi atualizada.";
        return RedirectToAction("Perfil", new { id = meuId });
    }
}
