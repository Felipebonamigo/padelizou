using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;

namespace Padelizou.Controllers
{
    public class TorneiosController : Controller
    {
        private readonly DbPadelContext _context;
        private readonly IEstatisticasService _estatisticas;
        private readonly IPalpiteService _palpites;

        // Injeta o banco de dados
        public TorneiosController(DbPadelContext context, IEstatisticasService estatisticas, IPalpiteService palpites)
        {
            _context = context;
            _estatisticas = estatisticas;
            _palpites = palpites;
        }

        // 1. ABRE A TELA DE CRIAÇÃO (Carrega o Catálogo)
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Create()
        {
            // Busca todas as categorias do banco para montar os Checkboxes na tela
            var catalogo = await _context.CategoriasPadrao.OrderBy(c => c.Id).ToListAsync();
            ViewBag.CatalogoCategorias = catalogo;

            return View();
        }

        // 2. RECEBE OS DADOS E SALVA O TORNEIO
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create(Torneio torneio, int[] categoriasSelecionadas)
        {
            // Validação de Segurança: Se for formato único, iguala todas as fases à Fase de Grupos
            if (torneio.FormatoUnico)
            {
                torneio.SetsFaseMataMata = torneio.SetsFaseGrupos;
                torneio.GamesFaseMataMata = torneio.GamesFaseGrupos;
                torneio.SetsFaseFinal = torneio.SetsFaseGrupos;
                torneio.GamesFaseFinal = torneio.GamesFaseGrupos;
            }

            // O Torneio nasce com Inscrições Abertas
            torneio.Status = "Inscrições Abertas";

            // Salva o Torneio primeiro para gerar o ID dele
            _context.Torneios.Add(torneio);
            await _context.SaveChangesAsync();

            // Pega as categorias que o organizador marcou e salva na tabela Categoria do Torneio
            if (categoriasSelecionadas != null && categoriasSelecionadas.Length > 0)
            {
                foreach (var catId in categoriasSelecionadas)
                {
                    var catPadrao = await _context.CategoriasPadrao.FindAsync(catId);
                    if (catPadrao != null)
                    {
                        var novaCategoria = new Categoria
                        {
                            TorneioId = torneio.Id,
                            Nome = catPadrao.Nome,
                            Codigo = catPadrao.Codigo
                        };
                        _context.Categorias.Add(novaCategoria);
                    }
                }
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Details", new { id = torneio.Id });
        }
        // Exemplo de como deve ficar o seu método Details
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var torneio = await _context.Torneios
                .Include(t => t.Categorias)
                    .ThenInclude(c => c.Duplas)
                        .ThenInclude(d => d.Jogador1)
                .Include(t => t.Categorias)
                    .ThenInclude(c => c.Duplas)
                        .ThenInclude(d => d.Jogador2)
                // NOVOS INCLUDES: Puxando os Grupos que o algoritmo sorteou!
                .Include(t => t.Categorias)
                    .ThenInclude(c => c.GruposTorneio)
                        .ThenInclude(g => g.Duplas)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (torneio == null) return NotFound();


            // MOTOR MATEMÁTICO DE CLASSIFICAÇÃO
            // 1. Puxa todas as partidas já finalizadas deste torneio
            var partidasFinalizadas = await _context.Partidas
                .Where(p => p.TorneioId == id && p.Status == "Finalizada")
                .ToListAsync();

            // 2. Roda a contabilidade grupo por grupo
            foreach (var categoria in torneio.Categorias)
            {
                foreach (var grupo in categoria.GruposTorneio)
                {
                    foreach (var dupla in grupo.Duplas)
                    {
                        // Pega só os jogos onde esta dupla participou
                        var meusJogos = partidasFinalizadas
                            .Where(p => p.Dupla1Id == dupla.Id || p.Dupla2Id == dupla.Id)
                            .ToList();

                        dupla.Jogos = meusJogos.Count;
                        dupla.Vitorias = meusJogos.Count(p => p.VencedorId == dupla.Id);
                        dupla.Derrotas = dupla.Jogos - dupla.Vitorias;

                        // Saldo de Games = (Games que eu fiz) - (Games que eu levei)
                        int gamesFeitos =
                            meusJogos.Where(p => p.Dupla1Id == dupla.Id).Sum(p => p.GamesDupla1 ?? 0) +
                            meusJogos.Where(p => p.Dupla2Id == dupla.Id).Sum(p => p.GamesDupla2 ?? 0);

                        int gamesLevados =
                            meusJogos.Where(p => p.Dupla1Id == dupla.Id).Sum(p => p.GamesDupla2 ?? 0) +
                            meusJogos.Where(p => p.Dupla2Id == dupla.Id).Sum(p => p.GamesDupla1 ?? 0);

                        dupla.SaldoGames = gamesFeitos - gamesLevados;
                    }

                    // 3. O SEGREDO DO SUCESSO: Ordena as duplas e devolve para a lista!
                    // Desempate 1: Maior número de vitórias. Desempate 2: Melhor Saldo de Games.
                    grupo.Duplas = grupo.Duplas
                        .OrderByDescending(d => d.Vitorias)
                        .ThenByDescending(d => d.SaldoGames)
                        .ToList();
                }
            }

            // SELOS HISTÓRICOS: melhor colocação + títulos de cada jogador nas mesmas categorias
            // (por Categoria.Nome), considerando torneios anteriores a este.
            var nomesCategorias = torneio.Categorias.Select(c => c.Nome).Distinct().ToList();
            ViewBag.HistoricoJogadores = await _estatisticas.ObterMelhoresColocacoesAsync(nomesCategorias, excluirTorneioId: id);

            return View(torneio);
        }
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> EncerrarInscricoes(int id)
        {
            var torneio = await _context.Torneios.FindAsync(id);

            // Verifica se o torneio existe
            if (torneio == null) return NotFound();

            // No futuro, podemos adicionar uma trava de segurança aqui:
            // if (torneio.OrganizadorId.ToString() != User.FindFirstValue(ClaimTypes.NameIdentifier)) return Unauthorized();

            torneio.Status = "Chaves em Sorteio";
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = torneio.Id });
        }
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> GerarChaves(int id)
        {
            var torneio = await _context.Torneios
                .Include(t => t.Categorias)
                    .ThenInclude(c => c.Duplas)
                        .ThenInclude(d => d.Jogador1)
                .Include(t => t.Categorias)
                    .ThenInclude(c => c.Duplas)
                        .ThenInclude(d => d.Jogador2)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (torneio == null || torneio.Status != "Chaves em Sorteio") return NotFound();

            foreach (var categoria in torneio.Categorias)
            {
                var duplas = categoria.Duplas.ToList();

                // CORREÇÃO DA REGRA DE OURO: 
                // O mínimo para ter jogo não é 3, é 2 duplas (Para uma chave final direta)!
                if (duplas.Count < 2) continue;

                // ORDENAÇÃO PELO RANKING (Define os Cabeças de Chave)
                var duplasOrdenadas = duplas
                    .OrderByDescending(d => d.Jogador1.PontuacaoGlobal + d.Jogador2.PontuacaoGlobal)
                    .ToList();

                // O SISTEMA DE DIVISÃO POR 3 (Garante chaves de 3, ou subdivide em 2 se necessário)
                int numGrupos = (int)Math.Ceiling(duplasOrdenadas.Count / 3.0);
                var gruposCriados = new List<GrupoTorneio>();

                for (int i = 0; i < numGrupos; i++)
                {
                    char letra = (char)('A' + i);
                    var novoGrupo = new GrupoTorneio { CategoriaId = categoria.Id, Nome = $"Grupo {letra}" };
                    _context.Add(novoGrupo);
                    gruposCriados.Add(novoGrupo);
                }
                await _context.SaveChangesAsync();

                // DISTRIBUIÇÃO EM ZIGUE-ZAGUE (Garante os cruzamentos 1x4 e 2x3 automaticamente)
                int grupoIndex = 0;
                int direcao = 1;

                foreach (var dupla in duplasOrdenadas)
                {
                    dupla.GrupoTorneioId = gruposCriados[grupoIndex].Id;

                    grupoIndex += direcao;

                    if (grupoIndex >= numGrupos)
                    {
                        grupoIndex = numGrupos - 1;
                        direcao = -1;
                    }
                    else if (grupoIndex < 0)
                    {
                        grupoIndex = 0;
                        direcao = 1;
                    }
                }
            }

            torneio.Status = "Fase de Grupos";
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", new { id = torneio.Id });
        }
        // 1. TELA DA MESA DE CONTROLE (Onde o ajudante fica no celular)
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> MesaControle(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            // SEGURANÇA: Verifica se o usuário é o Dono do Torneio ou um Ajudante
            var temPermissao = await _context.TorneioOrganizadores
                .AnyAsync(to => to.TorneioId == id && to.JogadorId == userId);

            // (Para o MVP, vamos assumir que se chegou aqui tem permissão, mas a trava de segurança está montada acima)

            var partidasEmAndamento = await _context.Partidas
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
                .Where(p => p.TorneioId == id && p.Status == "Em Andamento")
                .ToListAsync();

            ViewBag.TorneioId = id;
            return View(partidasEmAndamento);
        }

        // 1. ATUALIZAR PLACAR AO VIVO (Corrigido para Dupla1 e Dupla2)
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AtualizarPlacarAoVivo(int partidaId, int equipe, string tipo, int valor)
        {
            var partida = await _context.Partidas.FindAsync(partidaId);
            if (partida == null) return NotFound();

            if (equipe == 1)
            {
                if (tipo == "Game") partida.GamesDupla1 += valor;
                if (tipo == "Set") partida.SetsDupla1 += valor;
            }
            else if (equipe == 2)
            {
                if (tipo == "Game") partida.GamesDupla2 += valor;
                if (tipo == "Set") partida.SetsDupla2 += valor;
            }

            partida.GamesDupla1 = Math.Max(0, partida.GamesDupla1 ?? 0);
            partida.GamesDupla2 = Math.Max(0, partida.GamesDupla2 ?? 0);
            partida.SetsDupla1 = Math.Max(0, partida.SetsDupla1 ?? 0);
            partida.SetsDupla2 = Math.Max(0, partida.SetsDupla2 ?? 0);

            partida.SendoTransmitida = true;
            await _context.SaveChangesAsync();

            return Json(new { success = true, games1 = partida.GamesDupla1, games2 = partida.GamesDupla2, sets1 = partida.SetsDupla1, sets2 = partida.SetsDupla2 });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> FinalizarPartida(int partidaId)
        {
            // Usando _context.Partidas (Plural)
            var partida = await _context.Partidas
                .Include(p => p.Dupla1)
                .Include(p => p.Dupla2)
                .FirstOrDefaultAsync(p => p.Id == partidaId);

            if (partida != null)
            {
                partida.Status = "Finalizada";
                partida.SendoTransmitida = false;

                int vencedorId = (partida.SetsDupla1 > partida.SetsDupla2 ||
                                 (partida.SetsDupla1 == partida.SetsDupla2 && partida.GamesDupla1 > partida.GamesDupla2))
                                 ? partida.Dupla1Id : partida.Dupla2Id;

                partida.VencedorId = vencedorId;
                int perdedorId = (vencedorId == partida.Dupla1Id) ? partida.Dupla2Id : partida.Dupla1Id;

                // ESTATÍSTICA: Carimba o perdedor com a fase em que foi eliminado
                if (partida.Fase != "Fase de Grupos")
                {
                    var perdedor = await _context.Duplas.FindAsync(perdedorId);
                    if (perdedor != null) perdedor.UltimaFase = partida.Fase;
                }

                await _context.SaveChangesAsync();

                // GATILHOS DO ROBÔ (Usando _context.Partidas)
                if (partida.Fase == "Fase de Grupos")
                {
                    var jogosPendentes = await _context.Partidas
                        .CountAsync(p => p.CategoriaId == partida.CategoriaId && p.Fase == "Fase de Grupos" && p.Status != "Finalizada");
                    if (jogosPendentes == 0) await ProcessarMataMataAutomatico(partida.CategoriaId, partida.TorneioId);
                }
                else if (partida.Fase == "Quartas de Final" || partida.Fase == "Semifinal")
                {
                    var jogosPendentesFase = await _context.Partidas
                        .CountAsync(p => p.CategoriaId == partida.CategoriaId && p.Fase == partida.Fase && p.Status != "Finalizada");
                    if (jogosPendentesFase == 0) await ProcessarAvancoMataMataAutomatico(partida.CategoriaId, partida.TorneioId, partida.Fase);
                }
                else if (partida.Fase == "Final")
                {
                    // Campeão!
                    var campeao = await _context.Duplas.FindAsync(vencedorId);
                    if (campeao != null) campeao.UltimaFase = "Campeao";

                    var torneio = await _context.Torneios.FindAsync(partida.TorneioId);
                    if (torneio != null) torneio.Status = "Finalizado";
                    await _context.SaveChangesAsync();
                }
            }
            return RedirectToAction("MesaControle", new { id = partida?.TorneioId });
        }

        // ROBÔ DE PROGRESSÃO (Corrigido para Plurais)
        private async Task ProcessarAvancoMataMataAutomatico(int categoriaId, int? torneioId, string faseConcluida)
        {
            // Usando _context.Categorias e _context.Partidas
            var categoria = await _context.Categorias
                .Include(c => c.GruposTorneio).ThenInclude(g => g.Duplas)
                .FirstOrDefaultAsync(c => c.Id == categoriaId);

            var vencedores = await _context.Partidas
                .Where(p => p.CategoriaId == categoriaId && p.Fase == faseConcluida && p.Status == "Finalizada")
                .Select(p => p.VencedorId.Value)
                .ToListAsync();

            if (faseConcluida == "Quartas de Final" && vencedores.Count == 4)
            {
                _context.Partidas.Add(new Partida { TorneioId = torneioId, CategoriaId = categoriaId, Fase = "Semifinal", Status = "Agendada", Dupla1Id = vencedores[0], Dupla2Id = vencedores[3] });
                _context.Partidas.Add(new Partida { TorneioId = torneioId, CategoriaId = categoriaId, Fase = "Semifinal", Status = "Agendada", Dupla1Id = vencedores[1], Dupla2Id = vencedores[2] });
            }
            else if (faseConcluida == "Semifinal" && vencedores.Count == 2)
            {
                _context.Partidas.Add(new Partida { TorneioId = torneioId, CategoriaId = categoriaId, Fase = "Final", Status = "Agendada", Dupla1Id = vencedores[0], Dupla2Id = vencedores[1] });
            }
            await _context.SaveChangesAsync();
        }

        // ROBÔ INVISÍVEL DE CRUZAMENTO DE CHAVES
        private async Task ProcessarMataMataAutomatico(int categoriaId, int? torneioId)
        {
            var categoria = await _context.Categorias
                .Include(c => c.GruposTorneio)
                    .ThenInclude(g => g.Duplas)
                .FirstOrDefaultAsync(c => c.Id == categoriaId);

            if (categoria == null) return;

            var partidasFinalizadas = await _context.Partidas
                .Where(p => p.CategoriaId == categoriaId && p.Fase == "Fase de Grupos" && p.Status == "Finalizada")
                .ToListAsync();

            var primeirosColocados = new List<Dupla>();
            var segundosColocados = new List<Dupla>();

            var grupos = categoria.GruposTorneio.OrderBy(g => g.Nome).ToList();

            // 1. Calcula o ranking final real de cada grupo
            foreach (var grupo in grupos)
            {
                foreach (var dupla in grupo.Duplas)
                {
                    var meusJogos = partidasFinalizadas.Where(p => p.Dupla1Id == dupla.Id || p.Dupla2Id == dupla.Id).ToList();
                    dupla.Vitorias = meusJogos.Count(p => p.VencedorId == dupla.Id);

                    int gf = meusJogos.Where(p => p.Dupla1Id == dupla.Id).Sum(p => p.GamesDupla1 ?? 0) +
                             meusJogos.Where(p => p.Dupla2Id == dupla.Id).Sum(p => p.GamesDupla2 ?? 0);
                    int gc = meusJogos.Where(p => p.Dupla1Id == dupla.Id).Sum(p => p.GamesDupla2 ?? 0) +
                             meusJogos.Where(p => p.Dupla2Id == dupla.Id).Sum(p => p.GamesDupla1 ?? 0);
                    dupla.SaldoGames = gf - gc;
                }

                var ranking = grupo.Duplas.OrderByDescending(d => d.Vitorias).ThenByDescending(d => d.SaldoGames).ToList();

                if (ranking.Count >= 1) primeirosColocados.Add(ranking[0]);
                if (ranking.Count >= 2) segundosColocados.Add(ranking[1]);
            }

            // 2. Cruzamento Olímpico (Oposto)
            int totalGrupos = grupos.Count;
            if (totalGrupos < 2) return; // Segurança: Se houver só 1 grupo, não tem cruzamento. A final já está definida.

            string nomeFase = totalGrupos > 2 ? "Quartas de Final" : "Semifinal";

            for (int i = 0; i < primeirosColocados.Count; i++)
            {
                var dupla1 = primeirosColocados[i];
                var duplaOposta = segundosColocados[totalGrupos - 1 - i];

                var novaPartida = new Partida
                {
                    TorneioId = torneioId,
                    CategoriaId = categoriaId,
                    Dupla1Id = dupla1.Id,
                    Dupla2Id = duplaOposta.Id,
                    Status = "Agendada", // Nasce agendada para ir para a Mesa de Controle!
                    Fase = nomeFase
                };
                _context.Partidas.Add(novaPartida);
            }

            await _context.SaveChangesAsync();
        }

      

        // 3. API PARA O PÚBLICO LER O PLACAR AO VIVO (Atualiza a tela de quem tá assistindo)
        [HttpGet]
        public async Task<IActionResult> ObterPlacaresAoVivo(int torneioId)
        {
            // Títulos históricos por jogador nas categorias deste torneio (exceto este torneio).
            var nomes = await _context.Categorias
                .Where(c => c.TorneioId == torneioId)
                .Select(c => c.Nome).Distinct().ToListAsync();
            var hist = await _estatisticas.ObterMelhoresColocacoesAsync(nomes, excluirTorneioId: torneioId);
            int Titulos(int jogadorId) => hist.TryGetValue(jogadorId, out var h) ? h.Titulos : 0;

            var partidas = await _context.Partidas
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
                .Where(p => p.TorneioId == torneioId && p.SendoTransmitida == true)
                .ToListAsync();

            var resultado = partidas.Select(p => new {
                id = p.Id,
                jogador1IdD1 = p.Dupla1.Jogador1Id,
                jogador1NomeD1 = p.Dupla1.Jogador1.Nome,
                jogador2IdD1 = p.Dupla1.Jogador2Id,
                jogador2NomeD1 = p.Dupla1.Jogador2.Nome,
                jogador1IdD2 = p.Dupla2.Jogador1Id,
                jogador1NomeD2 = p.Dupla2.Jogador1.Nome,
                jogador2IdD2 = p.Dupla2.Jogador2Id,
                jogador2NomeD2 = p.Dupla2.Jogador2.Nome,
                setsD1 = p.SetsDupla1,
                gamesD1 = p.GamesDupla1,
                setsD2 = p.SetsDupla2,
                gamesD2 = p.GamesDupla2,
                titulosD1 = Titulos(p.Dupla1.Jogador1Id) + Titulos(p.Dupla1.Jogador2Id),
                titulosD2 = Titulos(p.Dupla2.Jogador1Id) + Titulos(p.Dupla2.Jogador2Id)
            }).ToList();

            return Json(resultado);
        }
        public async Task<IActionResult> Jogos(int id, int? timeFiltroId)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            // 1. Busca todas as partidas deste torneio e TRAZ JUNTO as Duplas, Jogadores e Times
            var query = _context.Partidas
                .Include(p => p.Categoria)
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1).ThenInclude(j => j.Time)
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2).ThenInclude(j => j.Time)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1).ThenInclude(j => j.Time)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2).ThenInclude(j => j.Time)
                .Where(p => p.TorneioId == id);

            // 2. O Filtro Mágico: Se o usuário selecionou um time (ex: Nata Padel), mostra só os jogos deles
            if (timeFiltroId.HasValue)
            {
                query = query.Where(p =>
                    (p.Dupla1.Jogador1.TimeId == timeFiltroId || p.Dupla1.Jogador2.TimeId == timeFiltroId) ||
                    (p.Dupla2.Jogador1.TimeId == timeFiltroId || p.Dupla2.Jogador2.TimeId == timeFiltroId)
                );
            }

            var partidas = await query.ToListAsync();

            // 3. Separando os jogos para as 3 abas na View
            ViewBag.AoVivo = partidas.Where(p => p.Status == "AoVivo").OrderBy(p => p.HorarioInicioReal).ToList();
            ViewBag.Finalizadas = partidas.Where(p => p.Status == "Finalizada").OrderByDescending(p => p.HorarioFimReal).ToList();
            ViewBag.Agendadas = partidas.Where(p => p.Status == "Agendada").OrderBy(p => p.HorarioPrevisto).ToList();

            // 4. Mandando os times para o Dropdown de filtro na tela
            ViewBag.Times = new SelectList(_context.Times, "Id", "Nome", timeFiltroId);
            ViewBag.Torneio = torneio;
            ViewBag.TimeAtual = timeFiltroId; // Para manter o select preenchido após filtrar

            // PALPITRÔMETRO: resumo de votos de cada partida exibida, num único lote.
            int? meuId = User.Identity?.IsAuthenticated == true
                ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
                : null;
            ViewBag.MeuId = meuId;
            ViewBag.Palpites = await _palpites.ObterResumosAsync(partidas.Select(p => p.Id), meuId);

            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GerarFaseGrupos(int torneioId, int categoriaId, DateTime dataHoraInicio)
        {
            var torneio = await _context.Torneios.FindAsync(torneioId);
            if (torneio == null) return NotFound();

            var duplasInscritas = await _context.Duplas
                .Where(d => d.CategoriaId == categoriaId && d.Categoria.TorneioId == torneioId)
                .ToListAsync();

            if (duplasInscritas.Count < torneio.TamanhoGrupo)
            {
                TempData["Erro"] = "Número de duplas insuficiente para formar um grupo.";
                return RedirectToAction("Detalhes", new { id = torneioId });
            }

            // 1. Embaralha as duplas para o sorteio
            var rng = new Random();
            var duplasSorteadas = duplasInscritas.OrderBy(d => rng.Next()).ToList();

            // 2. Separa em Grupos (A, B, C...)
            int numGrupos = (int)Math.Ceiling((double)duplasSorteadas.Count / torneio.TamanhoGrupo);
            string alfabeto = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            var novasPartidas = new List<Partida>();
            int tempoPartida = torneio.TempoPrevistoPartidaMinutos > 0 ? torneio.TempoPrevistoPartidaMinutos : 50;

            // Controle básico de horário (simulando 1 quadra para simplificar a leitura agora)
            DateTime horarioAtual = dataHoraInicio;

            for (int g = 0; g < numGrupos; g++)
            {
                string nomeGrupo = alfabeto[g].ToString();

                // Pega as duplas deste grupo (ex: as 3 primeiras, depois as próximas 3...)
                var duplasDoGrupo = duplasSorteadas.Skip(g * torneio.TamanhoGrupo).Take(torneio.TamanhoGrupo).ToList();

                // Salva a letra do grupo na Dupla para a Tabela de Classificação depois
                foreach (var dupla in duplasDoGrupo)
                {
                    dupla.Grupo = nomeGrupo;
                    _context.Update(dupla);
                }

                // 3. Gera os jogos (Todos contra Todos dentro do grupo)
                // Se for grupo de 3, gera: 1x2, 1x3, 2x3
                for (int i = 0; i < duplasDoGrupo.Count; i++)
                {
                    for (int j = i + 1; j < duplasDoGrupo.Count; j++)
                    {
                        var partida = new Partida
                        {
                            TorneioId = torneioId,
                            CategoriaId = categoriaId,
                            Dupla1Id = duplasDoGrupo[i].Id,
                            Dupla2Id = duplasDoGrupo[j].Id,
                            Fase = $"Grupo {nomeGrupo}", // Salva como "Grupo A", "Grupo B"
                            Status = "Agendada",
                            HorarioPrevisto = horarioAtual,
                            Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper()
                        };

                        novasPartidas.Add(partida);
                        horarioAtual = horarioAtual.AddMinutes(tempoPartida); // Avança o relógio
                    }
                }
            }

            _context.Partidas.AddRange(novasPartidas);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Fase de Grupos gerada! {numGrupos} grupos criados e {novasPartidas.Count} partidas agendadas.";
            return RedirectToAction("Jogos", new { id = torneioId });
        }
        // GET: Torneios/Classificacao/5?categoriaId=1
        public async Task<IActionResult> Classificacao(int id, int categoriaId)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            // 1. Busca as duplas desta categoria que já têm um Grupo definido
            var duplas = await _context.Duplas
                .Include(d => d.Jogador1)
                .Include(d => d.Jogador2)
                .Where(d => d.Categoria.TorneioId == id && d.CategoriaId == categoriaId && d.Grupo != null)
                .ToListAsync();

            // 2. Busca todas as partidas finalizadas desta fase de grupos
            var partidas = await _context.Partidas
                .Where(p => p.TorneioId == id && p.CategoriaId == categoriaId && p.Status == "Finalizada" && p.Fase.StartsWith("Grupo"))
                .ToListAsync();

            var listaClassificacao = new List<ClassificacaoGrupoViewModel>();

            // 3. O Cálculo Matemático para cada dupla
            foreach (var dupla in duplas)
            {
                var stats = new ClassificacaoGrupoViewModel { Dupla = dupla, Grupo = dupla.Grupo! };

                // Pega só os jogos onde essa dupla jogou
                var jogosDaDupla = partidas.Where(p => p.Dupla1Id == dupla.Id || p.Dupla2Id == dupla.Id).ToList();
                stats.JogosJogados = jogosDaDupla.Count;

                foreach (var jogo in jogosDaDupla)
                {
                    // Descobre se a dupla atual é a Dupla1 ou Dupla2 no registro da partida
                    bool ehDupla1 = jogo.Dupla1Id == dupla.Id;

                    int meusGames = ehDupla1 ? (jogo.GamesDupla1 ?? 0) : (jogo.GamesDupla2 ?? 0);
                    int gamesAdversario = ehDupla1 ? (jogo.GamesDupla2 ?? 0) : (jogo.GamesDupla1 ?? 0);

                    stats.GamesPro += meusGames;
                    stats.GamesContra += gamesAdversario;

                    if (meusGames > gamesAdversario) stats.Vitorias++;
                    else if (gamesAdversario > meusGames) stats.Derrotas++;
                }

                listaClassificacao.Add(stats);
            }

            // 4. O Agrupamento e a Regra de Desempate (Muito importante!)
            // Agrupamos por Letra do Grupo e ordenamos primeiro por Vitória e depois por Saldo de Games
            var classificacaoFinal = listaClassificacao
                .GroupBy(c => c.Grupo)
                .OrderBy(g => g.Key)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderByDescending(c => c.Vitorias).ThenByDescending(c => c.SaldoGames).ToList()
                );

            ViewBag.Torneio = torneio;
            ViewBag.RegraClassificados = torneio.ClassificadosPorGrupo; // Para pintar de verde quem passa de fase

            return View(classificacaoFinal);
        }
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GerarMataMata(int torneioId, int categoriaId, DateTime dataHoraInicio)
        {
            var torneio = await _context.Torneios.FindAsync(torneioId);
            if (torneio == null) return NotFound();

            // 1. Busca todas as duplas e jogos finalizados dos Grupos para calcular quem passou
            var duplas = await _context.Duplas
                .Where(d => d.Categoria.TorneioId == torneioId && d.CategoriaId == categoriaId && d.Grupo != null)
                .ToListAsync();

            var partidasGrupos = await _context.Partidas
                .Where(p => p.TorneioId == torneioId && p.CategoriaId == categoriaId && p.Status == "Finalizada" && p.Fase.StartsWith("Grupo"))
                .ToListAsync();

            // 2. Calcula a classificação em memória (O mesmo motor da tela visual)
            var classificacao = duplas.Select(dupla =>
            {
                var jogos = partidasGrupos.Where(p => p.Dupla1Id == dupla.Id || p.Dupla2Id == dupla.Id).ToList();
                int vitorias = 0, saldo = 0;

                foreach (var jogo in jogos)
                {
                    bool ehDupla1 = jogo.Dupla1Id == dupla.Id;
                    int pro = ehDupla1 ? (jogo.GamesDupla1 ?? 0) : (jogo.GamesDupla2 ?? 0);
                    int contra = ehDupla1 ? (jogo.GamesDupla2 ?? 0) : (jogo.GamesDupla1 ?? 0);

                    saldo += (pro - contra);
                    if (pro > contra) vitorias++;
                }
                return new { Dupla = dupla, Vitorias = vitorias, Saldo = saldo, Grupo = dupla.Grupo };
            })
            .GroupBy(c => c.Grupo)
            .OrderBy(g => g.Key) // Ordena os grupos: A, B, C, D...
            .ToList();

            var primeirosColocados = new List<Dupla>();
            var segundosColocados = new List<Dupla>();

            // 3. Extrai os 1º e 2º colocados de cada grupo
            foreach (var grupo in classificacao)
            {
                var rankingDoGrupo = grupo.OrderByDescending(c => c.Vitorias).ThenByDescending(c => c.Saldo).ToList();

                if (rankingDoGrupo.Count > 0) primeirosColocados.Add(rankingDoGrupo[0].Dupla);
                if (rankingDoGrupo.Count > 1) segundosColocados.Add(rankingDoGrupo[1].Dupla);
            }

            if (primeirosColocados.Count != segundosColocados.Count || primeirosColocados.Count == 0)
            {
                TempData["Erro"] = "Não foi possível gerar o Mata-Mata. Verifique se todos os jogos da fase de grupos foram finalizados e se há duplas suficientes.";
                return RedirectToAction("Jogos", new { id = torneioId });
            }

            // 4. Define o nome da Fase baseado na quantidade de jogos
            int totalJogosMataMata = primeirosColocados.Count;
            string nomeFase = totalJogosMataMata switch
            {
                1 => "Final",
                2 => "Semifinal",
                4 => "Quartas de Final",
                8 => "Oitavas de Final",
                _ => $"Mata-Mata ({totalJogosMataMata} jogos)"
            };

            var novasPartidas = new List<Partida>();
            int tempoPartida = torneio.TempoPrevistoPartidaMinutos > 0 ? torneio.TempoPrevistoPartidaMinutos : 50;
            DateTime horarioAtual = dataHoraInicio;
            int numeroGrupos = primeirosColocados.Count;

            // 5. O CRUZAMENTO MÁGICO EM "X" (1º do Grupo I contra o 2º do Último Grupo - I)
            for (int i = 0; i < numeroGrupos; i++)
            {
                var dupla1 = primeirosColocados[i];
                // Pega o 2º colocado de trás pra frente (Ex: Se i=0 (Grupo A), pega N-1-0 (Último Grupo))
                var dupla2 = segundosColocados[numeroGrupos - 1 - i];

                novasPartidas.Add(new Partida
                {
                    TorneioId = torneioId,
                    CategoriaId = categoriaId,
                    Dupla1Id = dupla1.Id,
                    Dupla2Id = dupla2.Id,
                    Fase = nomeFase,
                    Status = "Agendada",
                    HorarioPrevisto = horarioAtual,
                    Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper()
                });

                horarioAtual = horarioAtual.AddMinutes(tempoPartida);
            }

            _context.Partidas.AddRange(novasPartidas);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Fase {nomeFase} gerada com sucesso! {novasPartidas.Count} partidas agendadas.";
            return RedirectToAction("Jogos", new { id = torneioId });
        }
        
    }
}