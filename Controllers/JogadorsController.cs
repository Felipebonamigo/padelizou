
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

public class JogadoresController : Controller
{
    private readonly DbPadelContext _context;

    public JogadoresController(DbPadelContext context)
    {
        _context = context;
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

    // GET: JOGADORS/Edit/5
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
        return View(jogador);
    }

    // POST: JOGADORS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
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
            .Include(d => d.Torneio)
            .Where(d => d.Jogador1Id == id || d.Jogador2Id == id)
            .OrderByDescending(d => d.Torneio.DataInicio)
            .ToListAsync();

        // Cálculos de Estatísticas
        ViewBag.TotalTorneios = historicoDuplas.Count;
        ViewBag.Titulos = historicoDuplas.Count(d => d.UltimaFase == "Campeao");
        ViewBag.Finais = historicoDuplas.Count(d => d.UltimaFase == "Final");
        ViewBag.Semis = historicoDuplas.Count(d => d.UltimaFase == "Semifinal");
        ViewBag.Quartas = historicoDuplas.Count(d => d.UltimaFase == "Quartas de Final");

        return View((jogador, historicoDuplas));
    }
    [HttpGet]
    public async Task<IActionResult> Ranking()
    {
        // Usando _context.Jogadores (Plural)
        var ranking = await _context.Jogadores
            .OrderByDescending(j => j.PontuacaoGlobal)
            .ToListAsync();

        return View(ranking);
    }
    [HttpGet]
    public async Task<IActionResult> Ranking(int? clubeId, int? torneioId)
    {
        // 1. RANKING POR CLUBE
        if (clubeId.HasValue)
        {
            var duplasDoClube = await _context.Duplas
                .Include(d => d.Torneio)
                .Include(d => d.Jogador1)
                .Include(d => d.Jogador2)
                .Where(d => d.Torneio.ClubeId == clubeId)
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
                .Where(d => d.TorneioId == torneioId)
                .OrderByDescending(d => d.UltimaFase == "Campeao" ? 4 :
                                       d.UltimaFase == "Final" ? 3 :
                                       d.UltimaFase == "Semifinal" ? 2 : 1)
                .ToListAsync();

            ViewBag.TorneioId = torneioId;
            // Importante: Retornar a View específica de torneio, não a global
            return View("RankingPorTorneio", rankingPorTorneio);
        }

        // 3. RANKING GLOBAL (Fallback se nada for selecionado)
        var rankingGlobal = await _context.Jogadores
            .OrderByDescending(j => j.PontuacaoGlobal)
            .ToListAsync();

        return View("Ranking", rankingGlobal);
    }
}
