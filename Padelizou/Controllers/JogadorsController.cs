
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

        // Prateleira de troféus: um por MATERIAL da categoria (ver Services/TrofeuDeMaterial).
        // Sai do histórico que já foi carregado acima — o total de títulos sozinho tratava um
        // título na Open e um na 7ª como a mesma coisa.
        ViewBag.TrofeusPorMaterial = TrofeuDeMaterial.Contar(
            historicoDuplas.Select(d => ((string?)d.Categoria.Nome, (string?)d.UltimaFase)));

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
    public async Task<IActionResult> DenunciarComentario(int comentarioId)
    {
        var meuId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var comentario = await _context.ComentariosPerfil.FindAsync(comentarioId);
        if (comentario == null) return NotFound();

        // O autor não denuncia o próprio comentário — ele pode apagá-lo direto.
        // Denúncia repetida não sobrescreve a primeira: a fila do admin ordena por
        // DenunciadoEm, e re-carimbar empurraria o comentário pro fim da fila.
        if (comentario.AutorId != meuId && comentario.DenunciadoEm == null)
        {
            comentario.DenunciadoEm = DateTime.Now;
            comentario.DenunciadoPorId = meuId;
            await _context.SaveChangesAsync();
        }

        TempData["Sucesso"] = "Obrigado pelo aviso — um administrador vai revisar esse comentário.";
        return RedirectToAction("Perfil", new { id = comentario.PerfilId });
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

    // Busca de jogadores: nome + categoria + cidade/estado + clube, tudo combinável.
    // É a tela de "achar parceiro" — por isso os filtros são os mesmos critérios que
    // alguém usa pra escolher com quem jogar.
    [HttpGet]
    public async Task<IActionResult> Buscar(string? q, int? categoriaId, string? estado, string? cidade, int? clubeId, int pagina = 1)
    {
        var vm = new BuscaJogadoresVM
        {
            Termo = q,
            CategoriaId = categoriaId,
            Estado = string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpper(),
            Cidade = string.IsNullOrWhiteSpace(cidade) ? null : cidade.Trim(),
            ClubeId = clubeId,
        };

        // Opções dos selects. As cidades já saem filtradas pelo estado escolhido.
        var (estados, cidades) = await _estatisticas.ObterLocaisDisponiveisAsync(vm.Estado);
        vm.Estados = estados;
        vm.Cidades = cidades;
        vm.Categorias = await _context.CategoriasPadrao.OrderBy(c => c.Id).ToListAsync();
        vm.Clubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();

        // Sem filtro nenhum a busca lista TODO MUNDO (pedido do Felipe, 29/07/2026): quem
        // abre a tela querendo "ver quem tem por aqui" não deveria precisar adivinhar um
        // nome primeiro. A paginação logo abaixo é o que torna isso barato.
        var query = _context.Jogadores.AsQueryable();

        // Nome, apelido ou CPF — mesma regra do resto do sistema (Services/BuscaJogador).
        query = BuscaJogador.Filtrar(query, vm.Termo);

        if (vm.Estado != null)
            query = query.Where(j => j.Estado != null && j.Estado.ToUpper() == vm.Estado);

        if (vm.Cidade != null)
            query = query.Where(j => j.Cidade != null && j.Cidade.ToUpper() == vm.Cidade.ToUpper());

        // Categoria e clube vivem em tabelas de ligação, e "sem nenhuma linha" significa
        // "aceita qualquer uma" (ver JogadorCategoria/JogadorClube) — quem não declarou
        // preferência entra no resultado em vez de sumir da busca.
        if (vm.CategoriaId != null)
        {
            query = query.Where(j =>
                !_context.JogadorCategorias.Any(c => c.JogadorId == j.Id)
                || _context.JogadorCategorias.Any(c => c.JogadorId == j.Id && c.CategoriaPadraoId == vm.CategoriaId));
        }

        if (vm.ClubeId != null)
        {
            query = query.Where(j =>
                !_context.JogadorClubes.Any(c => c.JogadorId == j.Id)
                || _context.JogadorClubes.Any(c => c.JogadorId == j.Id && c.ClubeId == vm.ClubeId));
        }

        vm.TotalEncontrado = await query.CountAsync();

        // Página fora do intervalo não dá erro: vai pra mais próxima que existe. Link velho
        // de "página 9" continua funcionando depois que a base encolher.
        vm.TotalPaginas = Math.Max(1, (int)Math.Ceiling(vm.TotalEncontrado / (double)BuscaJogadoresVM.TamanhoDaPagina));
        vm.Pagina = Math.Clamp(pagina, 1, vm.TotalPaginas);

        // A página corta pela ordem ALFABÉTICA (que o banco sabe ordenar); o selo "combina"
        // e os pontos reordenam só DENTRO da página — pontos vêm de um cálculo em memória e
        // ordenar o total por eles obrigaria a carregar todo mundo, desfazendo a paginação.
        var jogadores = await query
            .Include(j => j.Time)
            .OrderBy(j => j.Nome)
            .ThenBy(j => j.Id)
            .Skip((vm.Pagina - 1) * BuscaJogadoresVM.TamanhoDaPagina)
            .Take(BuscaJogadoresVM.TamanhoDaPagina)
            .ToListAsync();

        var ids = jogadores.Select(j => j.Id).ToList();
        var pontos = await _estatisticas.ObterPontosPorJogadorAsync(ids);

        // Categorias e clubes de todos os achados em duas consultas, não uma por jogador.
        var catsPorJogador = (await _context.JogadorCategorias
                .Where(c => ids.Contains(c.JogadorId))
                .Select(c => new { c.JogadorId, Nome = c.CategoriaPadrao.Nome })
                .ToListAsync())
            .GroupBy(x => x.JogadorId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Nome).ToList());

        var clubesPorJogador = (await _context.JogadorClubes
                .Where(c => ids.Contains(c.JogadorId))
                .Select(c => new { c.JogadorId, Nome = c.Clube.Nome })
                .ToListAsync())
            .GroupBy(x => x.JogadorId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Nome).ToList());

        // Quem DECLAROU a categoria/clube filtrado sobe pro topo com selo. Sem isso o
        // filtro pareceria quebrado: como quase ninguém preenche preferência, todo mundo
        // entra pela regra do "sem linha = aceita qualquer um".
        var declararamCategoria = vm.CategoriaId == null
            ? new HashSet<int>()
            : (await _context.JogadorCategorias
                .Where(c => ids.Contains(c.JogadorId) && c.CategoriaPadraoId == vm.CategoriaId)
                .Select(c => c.JogadorId).ToListAsync()).ToHashSet();

        var declararamClube = vm.ClubeId == null
            ? new HashSet<int>()
            : (await _context.JogadorClubes
                .Where(c => ids.Contains(c.JogadorId) && c.ClubeId == vm.ClubeId)
                .Select(c => c.JogadorId).ToListAsync()).ToHashSet();

        bool filtraPreferencia = vm.CategoriaId != null || vm.ClubeId != null;

        vm.Resultados = jogadores.Select(j => new JogadorEncontradoVM
        {
            Jogador = j,
            Pontos = pontos.GetValueOrDefault(j.Id),
            Time = j.Time?.Nome,
            Categorias = catsPorJogador.GetValueOrDefault(j.Id) ?? new List<string>(),
            Clubes = clubesPorJogador.GetValueOrDefault(j.Id) ?? new List<string>(),
            Declarou = filtraPreferencia
                && (vm.CategoriaId == null || declararamCategoria.Contains(j.Id))
                && (vm.ClubeId == null || declararamClube.Contains(j.Id)),
        })
        .OrderByDescending(r => r.Declarou)
        .ThenByDescending(r => r.Pontos)
        .ThenBy(r => r.Jogador.Nome)
        .ToList();

        vm.FiltraPreferencia = filtraPreferencia;
        vm.QtdDeclarou = vm.Resultados.Count(r => r.Declarou);

        return View(vm);
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

}
