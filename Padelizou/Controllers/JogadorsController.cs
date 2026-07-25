
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
    public async Task<IActionResult> Edit(int id, [Bind("Id,Nome,TimeId")] Jogador jogador)
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
        // Busca o jogador (com clubes e dias/horários preferidos, pro bloco "joga em")
        var jogador = await _context.Jogadores
            .Include(j => j.JogadorClubes).ThenInclude(c => c.Clube)
            .Include(j => j.JogadorDiasHorarios)
            .FirstOrDefaultAsync(j => j.Id == id);
        if (jogador == null) return NotFound();

        int? meuId = User.Identity?.IsAuthenticated == true
            ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
            : null;
        ViewBag.MeuId = meuId;

        bool souEuMesmo = meuId.HasValue && meuId.Value == id;

        // Perfil privado: visitantes (que não são o dono) só veem foto e nome — pula todo o resto.
        if (jogador.PerfilPrivado && !souEuMesmo)
        {
            ViewBag.PerfilBloqueado = true;
            return View((jogador, new List<Dupla>()));
        }

        // Busca todas as duplas em que este jogador participou
        var historicoDuplas = await _context.Duplas
            .Include(d => d.Categoria).ThenInclude(c => c.Torneio)
            .Where(d => d.Jogador1Id == id || d.Jogador2Id == id)
            .OrderByDescending(d => d.Categoria.Torneio.DataInicio)
            .ToListAsync();

        // Cálculos de Estatísticas (via serviço central, inclui "caiu na chave")
        var resumo = await _estatisticas.ObterResumoJogadorAsync(id);
        ViewBag.Pontos = resumo.Pontos;
        ViewBag.TotalTorneios = resumo.TotalTorneios;
        ViewBag.Titulos = resumo.Titulos;
        ViewBag.Finais = resumo.Finais;
        ViewBag.Semis = resumo.Semis;
        ViewBag.Quartas = resumo.Quartas;
        ViewBag.CaiuNaChave = resumo.CaiuNaChave;
        ViewBag.Vitorias = resumo.Vitorias;

        // Categoria prevista (nível comprovado): categoria mais forte em que o jogador
        // chegou à final/foi campeão. Base da regra anti-sandbagging. Null se ainda não comprovou.
        ViewBag.CategoriaPrevista = await _estatisticas.ObterNivelComprovadoJogadorAsync(id);

        // Conquistas/badges: público, aparece pra qualquer visitante do perfil
        ViewBag.Conquistas = await _estatisticas.ObterConquistasAsync(id);

        // Evolução de pontos mês a mês (gráfico do perfil).
        ViewBag.Evolucao = await _estatisticas.ObterEvolucaoJogadorAsync(id);

        // Elogios recebidos, agregados por tipo (só os tipos que têm pelo menos 1).
        var elogiosRecebidos = await _context.Elogios
            .Where(e => e.ParaJogadorId == id)
            .GroupBy(e => e.Tipo)
            .Select(g => new { Tipo = g.Key, Quantidade = g.Count(), DeJogadorIds = g.Select(e => e.DeJogadorId) })
            .ToListAsync();
        ViewBag.Elogios = elogiosRecebidos
            .Select(g => CatalogoElogios.Obter(g.Tipo) is { } t
                ? new ElogioResumoVM
                {
                    Codigo = t.Codigo,
                    Titulo = t.Titulo,
                    Icone = t.Icone,
                    Quantidade = g.Quantidade,
                    EuDei = meuId.HasValue && g.DeJogadorIds.Contains(meuId.Value),
                }
                : null)
            .Where(v => v != null)
            .OrderByDescending(v => v!.Quantidade)
            .ToList();
        ViewBag.CatalogoElogios = CatalogoElogios.Todos;

        // Comentários no perfil: públicos, quem pode apagar é o autor, o dono do perfil ou um admin.
        ViewBag.Comentarios = await _context.ComentariosPerfil
            .Include(c => c.Autor)
            .Where(c => c.PerfilId == id)
            .OrderByDescending(c => c.CriadoEm)
            .ToListAsync();
        bool souAdmin = User.FindFirstValue("IsAdmin") == "true";
        ViewBag.PodeModerarComentarios = souEuMesmo || souAdmin;

        if (souEuMesmo)
        {
            // É o próprio perfil: mostra parceiros de sempre e os confrontos (jogou contra / rivais)
            var confrontos = await _estatisticas.ObterConfrontosAsync(id);
            var parceiros = await _estatisticas.ObterParceirosAsync(id);
            ViewBag.Confrontos = confrontos;
            ViewBag.Parceiros = parceiros;
            // Destaques reaproveitam as listas já carregadas (sem recarregar partidas).
            ViewBag.Destaques = EstatisticasService.MontarDestaques(parceiros, confrontos);
        }
        else if (meuId.HasValue)
        {
            // É o perfil de outra pessoa: mostra o confronto entre eu e ela
            ViewBag.MeuConfronto = await _estatisticas.ObterHeadToHeadAsync(meuId.Value, id);
            ViewBag.EstouSeguindo = await _context.SeguidoresJogador
                .AnyAsync(s => s.SeguidorId == meuId.Value && s.SeguidoId == id);
        }

        return View((jogador, historicoDuplas));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Seguir(int id)
    {
        var meuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (meuId != id)
        {
            var jaSigo = await _context.SeguidoresJogador.AnyAsync(s => s.SeguidorId == meuId && s.SeguidoId == id);
            if (!jaSigo)
            {
                _context.SeguidoresJogador.Add(new SeguidorJogador { SeguidorId = meuId, SeguidoId = id });
                await _context.SaveChangesAsync();
            }
        }

        return RedirectToAction("Perfil", new { id });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeixarDeSeguir(int id)
    {
        var meuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var vinculo = await _context.SeguidoresJogador
            .FirstOrDefaultAsync(s => s.SeguidorId == meuId && s.SeguidoId == id);
        if (vinculo != null)
        {
            _context.SeguidoresJogador.Remove(vinculo);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Perfil", new { id });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DarElogio(int id, string tipo)
    {
        var meuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (meuId != id && CatalogoElogios.Obter(tipo) != null)
        {
            var jaDei = await _context.Elogios
                .AnyAsync(e => e.DeJogadorId == meuId && e.ParaJogadorId == id && e.Tipo == tipo);
            if (!jaDei)
            {
                _context.Elogios.Add(new Elogio { DeJogadorId = meuId, ParaJogadorId = id, Tipo = tipo });
                await _context.SaveChangesAsync();
            }
        }

        return RedirectToAction("Perfil", new { id });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoverElogio(int id, string tipo)
    {
        var meuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var elogio = await _context.Elogios
            .FirstOrDefaultAsync(e => e.DeJogadorId == meuId && e.ParaJogadorId == id && e.Tipo == tipo);
        if (elogio != null)
        {
            _context.Elogios.Remove(elogio);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Perfil", new { id });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ComentarPerfil(int id, string texto)
    {
        var meuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        texto = (texto ?? "").Trim();

        if (meuId == id)
        {
            TempData["Erro"] = "Você não pode comentar no seu próprio perfil.";
        }
        else if (string.IsNullOrWhiteSpace(texto))
        {
            TempData["Erro"] = "Escreva alguma coisa antes de comentar.";
        }
        else if (texto.Length > 500)
        {
            TempData["Erro"] = "Comentário muito longo (máximo 500 caracteres).";
        }
        else if (FiltroPalavroes.EhOfensivo(texto))
        {
            TempData["Erro"] = "Esse comentário parece ter linguagem ofensiva — não é permitido aqui. Revise e tente de novo.";
        }
        else
        {
            _context.ComentariosPerfil.Add(new ComentarioPerfil { AutorId = meuId, PerfilId = id, Texto = texto });
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Perfil", new { id });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoverComentario(int comentarioId)
    {
        var meuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        bool souAdmin = User.FindFirstValue("IsAdmin") == "true";
        var comentario = await _context.ComentariosPerfil.FindAsync(comentarioId);
        int perfilId = comentario?.PerfilId ?? 0;

        // Só o autor, o dono do perfil ou um admin pode apagar — nunca um terceiro qualquer.
        if (comentario != null && (comentario.AutorId == meuId || comentario.PerfilId == meuId || souAdmin))
        {
            _context.ComentariosPerfil.Remove(comentario);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction("Perfil", new { id = perfilId });
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

        // Pontos reais de ranking dos jogadores listados, em UMA consulta.
        ViewBag.PontosPorJogador = await _estatisticas.ObterPontosPorJogadorAsync(resultados.Select(j => j.Id));
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
    public async Task<IActionResult> Ranking(int? clubeId, int? torneioId, string[]? cidade, string? estado, string? periodo)
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

        // 2. RANKING CONSOLIDADO (padrão): tudo calculado a partir de resultados de
        //    torneio. Substitui o antigo "ranking global" baseado em PontuacaoGlobal
        //    (campo manual que nada de torneio atualizava).
        ViewBag.TorneiosList = await _context.Torneios
            .OrderByDescending(t => t.DataInicio)
            .ToListAsync();

        var hub = await _estatisticas.ObterRankingHubAsync(cidade, estado, periodo);

        // Opções dos selects de cidade/estado (cidades já filtradas pelo estado escolhido).
        var (estados, cidades) = await _estatisticas.ObterLocaisDisponiveisAsync(estado);
        hub.EstadosDisponiveis = estados;
        hub.CidadesDisponiveis = cidades;

        // 3. RANKING DE UM TORNEIO: exibido embutido NESTA mesma página (não abre outra tela).
        if (torneioId.HasValue)
        {
            var torneio = await _context.Torneios.FindAsync(torneioId.Value);
            if (torneio != null)
            {
                hub.TorneioSelecionadoId = torneio.Id;
                hub.TorneioSelecionadoNome = torneio.Nome;
                hub.RankingTorneio = await _estatisticas.ObterRankingDoTorneioAsync(torneio.Id);
            }
        }

        return View("Ranking", hub);
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
