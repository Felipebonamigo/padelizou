
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;

public class JogadoresController : Controller
{
    private readonly DbPadelContext _context;
    private readonly IEstatisticasService _estatisticas;

    public JogadoresController(DbPadelContext context, IEstatisticasService estatisticas)
    {
        _context = context;
        _estatisticas = estatisticas;
    }

    // GET: JOGADORS
    public async Task<IActionResult> Index()
    {
        return View(await _context.Jogadores.ToListAsync());
    }

    // GET: JOGADORS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var jogador = await _context.Jogadores
            .FirstOrDefaultAsync(m => m.Id == id);
        if (jogador == null)
        {
            return NotFound();
        }

        return View(jogador);
    }

    // GET: JOGADORS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: JOGADORS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Nome,Cpf,Codigo,DuplaJogador1s,DuplaJogador2s")] Jogador jogador)
    {
        if (ModelState.IsValid)
        {
            _context.Add(jogador);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(jogador);
    }

    // GET: Jogadores/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var jogador = await _context.Jogadores.FindAsync(id);
        if (jogador == null)
        {
            return NotFound();
        }

        // Carrega todos os times do banco para preencher o select na tela
        // Passamos: a lista de times, qual campo é o valor (Id), qual campo é o texto (Nome), 
        // e qual é o time atual do jogador para já vir selecionado.
        ViewBag.Times = new SelectList(_context.Times, "Id", "Nome", jogador.TimeId);

        return View(jogador);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    // Certifique-se de que "TimeId" está dentro do atributo [Bind]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Nome,PontuacaoGlobal,TimeId")] Jogador jogador)
    {
        if (id != jogador.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(jogador);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!JogadorExists(jogador.Id)) return NotFound();
                else throw;
            }
            return RedirectToAction(nameof(Index)); // Ou redirecione para o Perfil
        }

        // Se der erro de validação e a tela recarregar, a lista de times precisa ser enviada de novo!
        ViewBag.Times = new SelectList(_context.Times, "Id", "Nome", jogador.TimeId);
        return View(jogador);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Nome,Cpf,Codigo,DuplaJogador1s,DuplaJogador2s")] Jogador jogador)
    {
        if (id != jogador.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(jogador);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!JogadorExists(jogador.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(jogador);
    }

    // GET: JOGADORS/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var jogador = await _context.Jogadores
            .FirstOrDefaultAsync(m => m.Id == id);
        if (jogador == null)
        {
            return NotFound();
        }

        return View(jogador);
    }

    // POST: JOGADORS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var jogador = await _context.Jogadores.FindAsync(id);
        if (jogador != null)
        {
            _context.Jogadores.Remove(jogador);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool JogadorExists(int? id)
    {
        return _context.Jogadores.Any(e => e.Id == id);
    }
    [HttpGet]
    public async Task<IActionResult> Perfil(int id)
    {
        // Busca o jogador
        var jogador = await _context.Jogadores.FindAsync(id);
        if (jogador == null) return NotFound();

        // Busca todas as duplas em que este jogador participou
        var historicoDuplas = await _context.Duplas
            .Include(d => d.Categoria).ThenInclude(c => c.Torneio)
            .Where(d => d.Jogador1Id == id || d.Jogador2Id == id)
            .OrderByDescending(d => d.Categoria.Torneio.DataInicio)
            .ToListAsync();

        // Cálculos de Estatísticas (via serviço central, inclui "caiu na chave")
        var resumo = await _estatisticas.ObterResumoJogadorAsync(id);
        ViewBag.TotalTorneios = resumo.TotalTorneios;
        ViewBag.Titulos = resumo.Titulos;
        ViewBag.Finais = resumo.Finais;
        ViewBag.Semis = resumo.Semis;
        ViewBag.Quartas = resumo.Quartas;
        ViewBag.CaiuNaChave = resumo.CaiuNaChave;

        // Se tem alguém logado, monta o contexto de confronto (H2H)
        int? meuId = User.Identity?.IsAuthenticated == true
            ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
            : null;
        ViewBag.MeuId = meuId;

        if (meuId.HasValue && meuId.Value == id)
        {
            // É o próprio perfil: mostra os rivais (mais enfrentado / mais vencido / mais perdido)
            var confrontos = await _estatisticas.ObterConfrontosAsync(id);
            ViewBag.MaisEnfrentou = confrontos.OrderByDescending(c => c.Jogos).FirstOrDefault();
            ViewBag.MaisVenceu = confrontos.Where(c => c.Vitorias > 0).OrderByDescending(c => c.Vitorias).FirstOrDefault();
            ViewBag.MaisPerdeu = confrontos.Where(c => c.Derrotas > 0).OrderByDescending(c => c.Derrotas).FirstOrDefault();
        }
        else if (meuId.HasValue)
        {
            // É o perfil de outra pessoa: mostra o confronto entre eu e ela
            ViewBag.MeuConfronto = await _estatisticas.ObterHeadToHeadAsync(meuId.Value, id);
        }

        return View((jogador, historicoDuplas));
    }

    // Busca de jogadores por nome (para ver histórico/H2H de qualquer um).
    [HttpGet]
    public async Task<IActionResult> Buscar(string? q)
    {
        var resultados = string.IsNullOrWhiteSpace(q)
            ? new List<Jogador>()
            : await _context.Jogadores
                .Where(j => j.Nome.Contains(q))
                .OrderBy(j => j.Nome)
                .Take(50)
                .ToListAsync();

        ViewBag.Query = q;
        return View(resultados);
    }

    // Histórico completo de confrontos entre o jogador logado e um adversário.
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Confronto(int oponenteId)
    {
        var meuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (meuId == oponenteId) return RedirectToAction(nameof(Perfil), new { id = meuId });

        if (!await _context.Jogadores.AnyAsync(j => j.Id == oponenteId)) return NotFound();

        var h2h = await _estatisticas.ObterHeadToHeadAsync(meuId, oponenteId);
        return View(h2h);
    }
    [HttpGet]
    public async Task<IActionResult> Ranking(int? clubeId, int? torneioId)
    {
        // 1. RANKING POR CLUBE
        if (clubeId.HasValue)
        {
            var duplasDoClube = await _context.Duplas
                .Include(d => d.Categoria).ThenInclude(c => c.Torneio)
                .Include(d => d.Jogador1)
                .Include(d => d.Jogador2)
                .Where(d => d.Categoria.Torneio.ClubeId == clubeId)
                .ToListAsync();

            // Se quiser ver por clube, a View deve ser "RankingPorClube"
            return View("RankingPorClube", duplasDoClube);
        }

        // 2. RANKING POR TORNEIO ESPECÍFICO
        if (torneioId.HasValue)
        {
            var rankingPorTorneio = await _context.Duplas
                .Include(d => d.Jogador1)
                .Include(d => d.Jogador2)
                .Where(d => d.Categoria.TorneioId == torneioId)
                .OrderByDescending(d => d.UltimaFase == "Campeao" ? 4 :
                                       d.UltimaFase == "Final" ? 3 :
                                       d.UltimaFase == "Semifinal" ? 2 : 1)
                .ToListAsync();

            ViewBag.TorneioId = torneioId;
            // Importante: Retornar a View específica de torneio, não a global
            return View("RankingPorTorneio", rankingPorTorneio);
        }

        // 3. RANKING GLOBAL (Fallback se nada for selecionado)
        ViewBag.TorneiosList = await _context.Torneios
            .OrderByDescending(t => t.DataInicio)
            .ToListAsync();

        var rankingGlobal = await _context.Jogadores
            .OrderByDescending(j => j.PontuacaoGlobal)
            .ToListAsync();

        return View("Ranking", rankingGlobal);
    }

    // Ranking por categoria (agrupado por Categoria.Nome), pontuado pelos resultados em torneios.
    [HttpGet]
    public async Task<IActionResult> RankingCategorias(string? categoria)
    {
        var rankings = await _estatisticas.ObterRankingPorCategoriaAsync();
        ViewBag.CategoriaSelecionada = categoria;
        return View(rankings);
    }
}
