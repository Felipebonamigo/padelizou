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
        var professores = await MontarVitrineAsync(cidadeId);
        return View(professores);
    }

    // A PORTA DE QUEM NUNCA JOGOU: "quero começar no padel". Mesma vitrine de professores,
    // outra conversa — aqui a pessoa não sabe o que é uma bandeja nem o que perguntar; a
    // página diz os três passos e entrega o professor da cidade como o primeiro deles.
    // É a única tela do site voltada pra quem ainda NÃO é jogador de padel.
    [HttpGet]
    public async Task<IActionResult> Comecar(int? cidadeId)
    {
        var professores = await MontarVitrineAsync(cidadeId);
        return View(professores);
    }

    // Monta a vitrine (professores + média + menor preço + cidades) pra Index e Comecar.
    private async Task<List<Jogador>> MontarVitrineAsync(int? cidadeId)
    {
        var query = _context.Jogadores.Where(j => j.IsProfessor);

        // O filtro só oferece cidade que TEM professor: a vitrine listava o catálogo inteiro,
        // então dava pra escolher uma cidade e receber de volta uma tela vazia — e o catálogo
        // é alimentado por quem digita a própria cidade nas preferências, não por quem dá aula.
        var cidadesComProfessor = await _context.ProfessorCidades
            .Where(pc => pc.Professor.IsProfessor)
            .Select(pc => pc.Cidade)
            .Distinct()
            .ToListAsync();

        if (cidadeId != null)
        {
            // ⚠️ Todos os ids da MESMA cidade, não só o escolhido: com "Gravataí" e "Gravatai"
            // como duas linhas do catálogo, filtrar por uma escondia os professores da outra.
            var idsDaCidade = CidadesSemRepetir.IdsDaMesma(cidadeId.Value, cidadesComProfessor);
            query = query.Where(j => _context.ProfessorCidades.Any(pc => pc.ProfessorId == j.Id && idsDaCidade.Contains(pc.CidadeId)));
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
        ViewBag.Cidades = CidadesSemRepetir.Agrupar(cidadesComProfessor);
        ViewBag.CidadeId = cidadeId;

        return professores;
    }

    [HttpGet]
    public async Task<IActionResult> Perfil(int id)
    {
        var professor = await _context.Jogadores.FirstOrDefaultAsync(j => j.Id == id && j.IsProfessor);
        if (professor == null) return NotFound();

        var locais = await _context.LocaisAula
            .Include(l => l.Pacotes)
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

        var esportesSalvos = await _context.ProfessorEsportes
            .Where(pe => pe.ProfessorId == id)
            .Select(pe => pe.Esporte)
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
            // Ordem fixa (Padel, Tênis, Beach Tênis), não a ordem que caiu no banco.
            EsportesQueEnsina = EsporteDaAula.Todos
                .Where(esportesSalvos.Contains)
                .ToList(),
            TotalAvaliacoes = avaliacoes.Count,
            MediaNota = avaliacoes.Any() ? Math.Round(avaliacoes.Average(a => a.Nota), 1) : null,
            // O interruptor do professor decide se TEXTO aparece; a nota e a média ficam
            // sempre — são o dado que protege o próximo aluno.
            Depoimentos = professor.DepoimentosDeAulaHabilitados
                ? avaliacoes.Where(a => !string.IsNullOrWhiteSpace(a.Depoimento)).Take(10).ToList()
                : new List<AvaliacaoProfessor>(),
            DepoimentosHabilitados = professor.DepoimentosDeAulaHabilitados,
            NotasPorEstrela = Enumerable.Range(1, 5).ToDictionary(n => n, n => avaliacoes.Count(a => a.Nota == n)),
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
            TempData["Erro"] = "A nota precisa ser de 1 a 5 estrelas.";
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
                // ⚠️ SEM E-MAIL desde 09/08/2026: é bilhete social, do mesmo naipe do elogio no
                // perfil — bom de ver, mas não pede resposta nem tem hora pra ser lido.
                await _pushService.EnviarParaJogadorAsync(professorId,
                    "Você recebeu uma avaliação",
                    $"{aluno?.Nome ?? "Um aluno"} te deu {nota} estrela(s).",
                    Url.Action("Perfil", "Professores", new { id = professorId }),
                    AlcanceDoAviso.AppSemEmail);
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

    // Esportes que o professor dá aula (Padel, Tênis, Beach Tênis — muitos ensinam mais de
    // um). Pedido do Felipe: "acho que é uma realidade dos professores".
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> SalvarEsportes(List<string>? esportes)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var meuId))
            return RedirectToAction("Perfil", "Auth");

        var professor = await _context.Jogadores.FindAsync(meuId);
        if (professor == null || !professor.IsProfessor) return Forbid();

        var validos = EsporteDaAula.Todos
            .Where(e => esportes != null && esportes.Contains(e))
            .ToList();

        var atuais = await _context.ProfessorEsportes.Where(pe => pe.ProfessorId == meuId).ToListAsync();
        _context.ProfessorEsportes.RemoveRange(atuais);
        _context.ProfessorEsportes.AddRange(validos.Select(e => new ProfessorEsporte { ProfessorId = meuId, Esporte = e }));

        await _context.SaveChangesAsync();
        TempData["Sucesso"] = "Seus esportes foram atualizados.";
        return RedirectToAction("Perfil", new { id = meuId });
    }
}
