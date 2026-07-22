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
        private readonly IWebHostEnvironment _env;

        // Injeta o banco de dados
        public TorneiosController(DbPadelContext context, IEstatisticasService estatisticas, IPalpiteService palpites, IWebHostEnvironment env)
        {
            _context = context;
            _estatisticas = estatisticas;
            _palpites = palpites;
            _env = env;
        }

        // Salva um IFormFile de imagem numa subpasta de wwwroot/uploads e devolve o caminho relativo
        // pra gravar no banco (mesmo padrão usado em AuthController.Cadastro/EditarPerfil).
        private async Task<string> SalvarImagemAsync(IFormFile arquivo, string subpasta)
        {
            string pastaUploads = Path.Combine(_env.WebRootPath, "uploads", subpasta);
            if (!Directory.Exists(pastaUploads))
            {
                Directory.CreateDirectory(pastaUploads);
            }

            string nomeArquivoUnico = Guid.NewGuid().ToString() + "_" + arquivo.FileName;
            string caminhoFisicoCompleto = Path.Combine(pastaUploads, nomeArquivoUnico);

            using (var stream = new FileStream(caminhoFisicoCompleto, FileMode.Create))
            {
                await arquivo.CopyToAsync(stream);
            }

            return "/uploads/" + subpasta + "/" + nomeArquivoUnico;
        }

        // Confere se o jogador logado é organizador (criador ou adicionado) deste torneio específico
        private async Task<bool> EhOrganizadorAsync(int torneioId, int jogadorId)
        {
            return await _context.TorneioOrganizadores
                .AnyAsync(o => o.TorneioId == torneioId && o.JogadorId == jogadorId);
        }

        private int? ObterJogadorIdLogado()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim) : (int?)null;
        }

        // TELA INICIAL DA ABA "TORNEIO": lista tudo, separado por status
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var torneios = await _context.Torneios.OrderByDescending(t => t.DataInicio).ToListAsync();

            ViewBag.Abertos = torneios.Where(t => t.Status == "Inscrições Abertas").ToList();
            ViewBag.EmAndamento = torneios.Where(t => t.Status != "Inscrições Abertas" && t.Status != "Finalizado").ToList();
            ViewBag.Finalizados = torneios.Where(t => t.Status == "Finalizado").ToList();

            return View();
        }

        // 1. ABRE A TELA DE CRIAÇÃO (Carrega o Catálogo)
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Create()
        {
            // Busca todas as categorias do banco para montar os Checkboxes na tela
            var catalogo = await _context.CategoriasPadrao.OrderBy(c => c.Id).ToListAsync();
            ViewBag.CatalogoCategorias = catalogo;
            ViewBag.CatalogoClubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();

            return View();
        }

        // 2. RECEBE OS DADOS E SALVA O TORNEIO
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create(Torneio torneio, int[] categoriasSelecionadas, int[]? organizadoresSelecionados, string[]? nomesQuadras, IFormFile? capa)
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
            torneio.OrganizadorId = null;
            torneio.Codigo = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();

            if (capa != null && capa.Length > 0)
            {
                torneio.ImagemCapa = await SalvarImagemAsync(capa, "capas-torneio");
            }

            // Salva o Torneio primeiro para gerar o ID dele
            _context.Torneios.Add(torneio);
            await _context.SaveChangesAsync();

            // Cria as quadras do torneio a partir da quantidade informada, usando o nome que o
            // organizador deu a cada uma (ou "Quadra A/B..." como fallback se deixou em branco).
            string alfabetoQuadras = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            int quantidadeQuadras = Math.Max(1, torneio.QuantidadeQuadras);
            for (int q = 0; q < quantidadeQuadras && q < alfabetoQuadras.Length; q++)
            {
                string? nomeInformado = nomesQuadras != null && q < nomesQuadras.Length ? nomesQuadras[q]?.Trim() : null;
                string nomeQuadra = string.IsNullOrWhiteSpace(nomeInformado) ? $"Quadra {alfabetoQuadras[q]}" : nomeInformado;
                _context.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = nomeQuadra });
            }
            await _context.SaveChangesAsync();

            // Quem criou o torneio já entra como organizador dele
            var criadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            _context.TorneioOrganizadores.Add(new TorneioOrganizador
            {
                TorneioId = torneio.Id,
                JogadorId = criadorId,
                NivelAcesso = "Criador"
            });

            // Co-organizadores escolhidos por CPF/Login na tela de criação
            if (organizadoresSelecionados != null)
            {
                foreach (var jogadorId in organizadoresSelecionados.Distinct())
                {
                    if (jogadorId == criadorId) continue;
                    _context.TorneioOrganizadores.Add(new TorneioOrganizador
                    {
                        TorneioId = torneio.Id,
                        JogadorId = jogadorId,
                        NivelAcesso = "Organizador"
                    });
                }
            }
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

        // Autocomplete usado na criação/gerenciamento do torneio pra achar um Jogador por CPF ou Login
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> BuscarJogadorParaOrganizador(string termo)
        {
            if (string.IsNullOrWhiteSpace(termo) || termo.Trim().Length < 3) return Json(Array.Empty<object>());

            termo = termo.Trim();
            var resultados = await _context.Jogadores
                .Where(j => j.Cpf.StartsWith(termo) || (j.Login != null && j.Login.Contains(termo)))
                .OrderBy(j => j.Nome)
                .Take(8)
                .Select(j => new { j.Id, j.Nome, j.FotoPerfil, j.Login })
                .ToListAsync();

            return Json(resultados);
        }

        // Adiciona um co-organizador já cadastrado (achado por CPF/Login) na aba "Gerenciar Torneio"
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdicionarOrganizador(int torneioId, int jogadorId)
        {
            var chamadorId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(torneioId, chamadorId)) return Forbid();

            if (!await EhOrganizadorAsync(torneioId, jogadorId))
            {
                _context.TorneioOrganizadores.Add(new TorneioOrganizador
                {
                    TorneioId = torneioId,
                    JogadorId = jogadorId,
                    NivelAcesso = "Organizador"
                });
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Details", new { id = torneioId });
        }
        // Inscrição individual (Torneio Americano) — achar-ou-criar Jogador por CPF, mesmo
        // padrão de DuplasController.Create, só que sem parceiro fixo.
        [HttpPost]
        public async Task<IActionResult> InscreverIndividual(int torneioId, int categoriaId, string nome, string cpf)
        {
            var categoria = await _context.Categorias.FindAsync(categoriaId);
            if (categoria == null || categoria.TorneioId != torneioId)
            {
                TempData["Erro"] = "Categoria inválida para este torneio.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            var torneio = await _context.Torneios.FindAsync(torneioId);
            if (torneio == null || torneio.Status != "Inscrições Abertas")
            {
                TempData["Erro"] = "As inscrições deste torneio não estão mais abertas.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            var jogador = await _context.Jogadores.FirstOrDefaultAsync(j => j.Cpf == cpf);
            if (jogador == null)
            {
                jogador = new Jogador { Nome = nome, Cpf = cpf };
                _context.Jogadores.Add(jogador);
                await _context.SaveChangesAsync();
            }

            bool jaInscrito = await _context.InscricoesAmericanas
                .AnyAsync(i => i.CategoriaId == categoriaId && i.JogadorId == jogador.Id);
            if (!jaInscrito)
            {
                _context.InscricoesAmericanas.Add(new InscricaoAmericana { CategoriaId = categoriaId, JogadorId = jogador.Id });
                await _context.SaveChangesAsync();
            }

            TempData["Sucesso"] = "Inscrição individual confirmada!";
            return RedirectToAction("Details", new { id = torneioId });
        }

        // Aba "Gerenciar Torneio": edita os dados do torneio já criado (inclusive trocar a capa)
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(
            int id, string nome, string? localTorneio, DateTime? dataInicio, decimal precoInscricao, int clubeId,
            int quantidadeQuadras, string[]? nomesQuadras,
            bool permiteImpedimentos, bool permiteImpedimentoSextaNoite, bool permiteImpedimentoSabadoManha, bool permiteImpedimentoSabadoTarde,
            IFormFile? capa)
        {
            var jogadorId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(id, jogadorId)) return Forbid();

            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            torneio.Nome = nome;
            torneio.LocalTorneio = localTorneio;
            torneio.DataInicio = dataInicio;
            torneio.PrecoInscricao = precoInscricao;
            torneio.ClubeId = clubeId;
            torneio.QuantidadeQuadras = quantidadeQuadras;
            torneio.PermiteImpedimentos = permiteImpedimentos;
            torneio.PermiteImpedimentoSextaNoite = permiteImpedimentoSextaNoite;
            torneio.PermiteImpedimentoSabadoManha = permiteImpedimentoSabadoManha;
            torneio.PermiteImpedimentoSabadoTarde = permiteImpedimentoSabadoTarde;

            if (capa != null && capa.Length > 0)
            {
                torneio.ImagemCapa = await SalvarImagemAsync(capa, "capas-torneio");
            }

            // Reconcilia a lista de Quadras com a nova quantidade/nomes (por posição)
            var quadrasAtuais = await _context.Quadras.Where(q => q.TorneioId == id).OrderBy(q => q.Id).ToListAsync();
            string alfabetoQuadras = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            int quantidade = Math.Max(1, quantidadeQuadras);
            for (int i = 0; i < quantidade && i < alfabetoQuadras.Length; i++)
            {
                string? nomeInformado = nomesQuadras != null && i < nomesQuadras.Length ? nomesQuadras[i]?.Trim() : null;
                string nomeQuadra = string.IsNullOrWhiteSpace(nomeInformado) ? $"Quadra {alfabetoQuadras[i]}" : nomeInformado;
                if (i < quadrasAtuais.Count)
                {
                    quadrasAtuais[i].Nome = nomeQuadra;
                }
                else
                {
                    _context.Quadras.Add(new Quadra { TorneioId = id, Nome = nomeQuadra });
                }
            }
            if (quadrasAtuais.Count > quantidade)
            {
                _context.Quadras.RemoveRange(quadrasAtuais.Skip(quantidade));
            }

            await _context.SaveChangesAsync();
            TempData["Sucesso"] = "Dados do torneio atualizados!";
            return RedirectToAction("Details", new { id });
        }

        // Aba "Gerenciar Torneio": remove um inscrito (só enquanto as inscrições estiverem abertas —
        // depois disso já pode existir Partida referenciando a dupla)
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverDupla(int duplaId)
        {
            var dupla = await _context.Duplas.Include(d => d.Categoria).FirstOrDefaultAsync(d => d.Id == duplaId);
            if (dupla == null) return NotFound();

            int torneioId = dupla.Categoria.TorneioId;
            var jogadorId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(torneioId, jogadorId)) return Forbid();

            var torneio = await _context.Torneios.FindAsync(torneioId);
            if (torneio == null) return NotFound();

            if (torneio.Status != "Inscrições Abertas")
            {
                TempData["Erro"] = "Só é possível remover inscritos enquanto as inscrições estiverem abertas.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            _context.Duplas.Remove(dupla);
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Inscrito removido do torneio.";
            return RedirectToAction("Details", new { id = torneioId });
        }

        // Exemplo de como deve ficar o seu método Details
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var torneio = await _context.Torneios
                .Include(t => t.Categorias)
                    .ThenInclude(c => c.Duplas)
                        .ThenInclude(d => d.Jogador1)
                            .ThenInclude(j => j.Time)
                .Include(t => t.Categorias)
                    .ThenInclude(c => c.Duplas)
                        .ThenInclude(d => d.Jogador2)
                            .ThenInclude(j => j.Time)
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

            if (torneio.Formato == "Americano")
            {
                ViewBag.InscricoesAmericanas = await _context.InscricoesAmericanas
                    .Include(i => i.Jogador)
                    .Where(i => i.Categoria.TorneioId == id)
                    .ToListAsync();
            }

            // Só quem está em TorneioOrganizadores deste torneio pode ver/usar a aba "Gerenciar Torneio"
            var jogadorLogadoId = ObterJogadorIdLogado();

            // Aba Inscritos: abre direto na categoria em que o usuário logado já está inscrito
            // neste torneio (se estiver); senão cai pra primeira categoria do torneio.
            int? categoriaDoUsuario = null;
            if (jogadorLogadoId.HasValue)
            {
                if (torneio.Formato == "Americano")
                {
                    categoriaDoUsuario = await _context.InscricoesAmericanas
                        .Where(i => i.Categoria.TorneioId == id && i.JogadorId == jogadorLogadoId.Value)
                        .Select(i => (int?)i.CategoriaId)
                        .FirstOrDefaultAsync();
                }
                else
                {
                    categoriaDoUsuario = await _context.Duplas
                        .Where(d => d.Categoria.TorneioId == id && (d.Jogador1Id == jogadorLogadoId.Value || d.Jogador2Id == jogadorLogadoId.Value))
                        .Select(d => (int?)d.CategoriaId)
                        .FirstOrDefaultAsync();
                }
            }
            ViewBag.CategoriaSelecionadaId = categoriaDoUsuario ?? torneio.Categorias.Select(c => c.Id).FirstOrDefault();

            ViewBag.PodeGerenciar = jogadorLogadoId.HasValue && await EhOrganizadorAsync(id, jogadorLogadoId.Value);
            if (ViewBag.PodeGerenciar == true)
            {
                ViewBag.Organizadores = await _context.TorneioOrganizadores
                    .Include(o => o.Jogador)
                    .Where(o => o.TorneioId == id)
                    .ToListAsync();
                ViewBag.CatalogoClubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();
                ViewBag.Quadras = await _context.Quadras.Where(q => q.TorneioId == id).OrderBy(q => q.Id).ToListAsync();
            }

            return View(torneio);
        }
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> EncerrarInscricoes(int id)
        {
            var torneio = await _context.Torneios.FindAsync(id);

            // Verifica se o torneio existe
            if (torneio == null) return NotFound();

            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

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
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

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

                // Grupos do tamanho configurado no torneio (Torneio.TamanhoGrupo, normalmente 3 duplas)
                int numGrupos = (int)Math.Ceiling((double)duplasOrdenadas.Count / torneio.TamanhoGrupo);
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

            // SEGURANÇA: Só o Dono do Torneio ou um Ajudante (TorneioOrganizador) pode acessar a Mesa de Controle
            if (!await EhOrganizadorAsync(id, userId)) return Forbid();

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
            if (partida.TorneioId == null || !await EhOrganizadorAsync(partida.TorneioId.Value, ObterJogadorIdLogado() ?? 0)) return Forbid();

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

            if (partida != null && partida.TorneioId != null && !await EhOrganizadorAsync(partida.TorneioId.Value, ObterJogadorIdLogado() ?? 0))
            {
                return Forbid();
            }

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
            int Titulos(int jogadorId) => hist.TryGetValue(jogadorId, out var porTier) ? porTier.Values.Sum(v => v.Titulos) : 0;

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
        public async Task<IActionResult> Jogos(int id, int? timeFiltroId, int[]? categoriaFiltroIds)
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

            // 2.1 Filtro por 1 ou mais Categorias
            if (categoriaFiltroIds != null && categoriaFiltroIds.Length > 0)
            {
                query = query.Where(p => categoriaFiltroIds.Contains(p.CategoriaId));
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
            ViewBag.CategoriasDoTorneio = await _context.Categorias.Where(c => c.TorneioId == id).OrderBy(c => c.Nome).ToListAsync();
            ViewBag.CategoriaFiltroAtual = categoriaFiltroIds ?? Array.Empty<int>();

            // PALPITRÔMETRO: resumo de votos de cada partida exibida, num único lote.
            int? meuId = User.Identity?.IsAuthenticated == true
                ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
                : null;
            ViewBag.MeuId = meuId;
            ViewBag.Palpites = await _palpites.ObterResumosAsync(partidas.Select(p => p.Id), meuId);

            return View();
        }
        // Torneio Americano: sorteia as rodadas de todas as categorias do torneio, trocando os
        // parceiros a cada rodada. Heurística gulosa (não é uma escalação matematicamente perfeita
        // de round-robin) — pra cada rodada, embaralha os jogadores, agrupa de 4 em 4 e escolhe,
        // entre as 3 formas possíveis de dividir o quarteto em duplas, a que menos repete parceiros
        // já usados antes.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GerarRodadasAmericano(int torneioId, DateTime dataHoraInicio)
        {
            var torneio = await _context.Torneios.Include(t => t.Categorias).FirstOrDefaultAsync(t => t.Id == torneioId);
            if (torneio == null || torneio.Formato != "Americano") return NotFound();
            if (!await EhOrganizadorAsync(torneioId, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var rng = new Random();
            int tempoPartida = torneio.TempoPrevistoPartidaMinutos > 0 ? torneio.TempoPrevistoPartidaMinutos : 50;
            DateTime horarioAtual = dataHoraInicio;
            int totalPartidasGeradas = 0;
            int totalDeFora = 0;

            foreach (var categoria in torneio.Categorias)
            {
                var inscritos = await _context.InscricoesAmericanas
                    .Where(i => i.CategoriaId == categoria.Id)
                    .Select(i => i.JogadorId)
                    .ToListAsync();

                int usaveis = inscritos.Count - (inscritos.Count % 4);
                if (usaveis < 4) continue; // categoria sem jogadores suficientes pra fechar um grupo de 4

                var jogadoresEmbaralhados = inscritos.OrderBy(_ => rng.Next()).ToList();
                var jogadoresUsados = jogadoresEmbaralhados.Take(usaveis).ToList();
                totalDeFora += jogadoresEmbaralhados.Count - usaveis;

                var jaParceiros = new HashSet<(int, int)>();
                (int, int) NormalizarPar(int a, int b) => a < b ? (a, b) : (b, a);

                int numRodadas = usaveis - 1;

                for (int rodada = 1; rodada <= numRodadas; rodada++)
                {
                    var ordemRodada = jogadoresUsados.OrderBy(_ => rng.Next()).ToList();

                    for (int g = 0; g + 4 <= ordemRodada.Count; g += 4)
                    {
                        var quarteto = ordemRodada.GetRange(g, 4);
                        var opcoes = new[]
                        {
                            (D1: (quarteto[0], quarteto[1]), D2: (quarteto[2], quarteto[3])),
                            (D1: (quarteto[0], quarteto[2]), D2: (quarteto[1], quarteto[3])),
                            (D1: (quarteto[0], quarteto[3]), D2: (quarteto[1], quarteto[2]))
                        };

                        var melhorOpcao = opcoes
                            .OrderBy(o => (jaParceiros.Contains(NormalizarPar(o.D1.Item1, o.D1.Item2)) ? 1 : 0)
                                        + (jaParceiros.Contains(NormalizarPar(o.D2.Item1, o.D2.Item2)) ? 1 : 0))
                            .First();

                        jaParceiros.Add(NormalizarPar(melhorOpcao.D1.Item1, melhorOpcao.D1.Item2));
                        jaParceiros.Add(NormalizarPar(melhorOpcao.D2.Item1, melhorOpcao.D2.Item2));

                        var dupla1 = new Dupla { CategoriaId = categoria.Id, Jogador1Id = melhorOpcao.D1.Item1, Jogador2Id = melhorOpcao.D1.Item2 };
                        var dupla2 = new Dupla { CategoriaId = categoria.Id, Jogador1Id = melhorOpcao.D2.Item1, Jogador2Id = melhorOpcao.D2.Item2 };
                        _context.Duplas.Add(dupla1);
                        _context.Duplas.Add(dupla2);
                        await _context.SaveChangesAsync(); // precisa dos Ids gerados antes de criar a Partida

                        _context.Partidas.Add(new Partida
                        {
                            TorneioId = torneioId,
                            CategoriaId = categoria.Id,
                            Dupla1Id = dupla1.Id,
                            Dupla2Id = dupla2.Id,
                            Fase = $"Americano Rodada {rodada}",
                            Status = "Agendada",
                            HorarioPrevisto = horarioAtual,
                            Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper()
                        });
                        horarioAtual = horarioAtual.AddMinutes(tempoPartida);
                        totalPartidasGeradas++;
                    }
                }
            }

            torneio.Status = "Fase de Grupos"; // reaproveita o mesmo status de "torneio em andamento"
            await _context.SaveChangesAsync();

            string avisoDeFora = totalDeFora > 0 ? $" {totalDeFora} jogador(es) ficaram de fora por não fechar grupos de 4." : "";
            TempData["Sucesso"] = $"Rodadas geradas! {totalPartidasGeradas} partidas agendadas.{avisoDeFora}";
            return RedirectToAction("Jogos", new { id = torneioId });
        }

        // GET: Torneios/ClassificacaoAmericano/5?categoriaId=1 — soma de games por jogador
        // (não por dupla, já que o parceiro muda a cada rodada no formato Americano)
        public async Task<IActionResult> ClassificacaoAmericano(int id, int categoriaId)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            var partidas = await _context.Partidas
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
                .Where(p => p.TorneioId == id && p.CategoriaId == categoriaId && p.Fase.StartsWith("Americano") && p.Status == "Finalizada")
                .ToListAsync();

            var pontosPorJogador = new Dictionary<int, (Jogador Jogador, int TotalGames)>();
            void Somar(Jogador jogador, int games)
            {
                if (pontosPorJogador.TryGetValue(jogador.Id, out var atual))
                {
                    pontosPorJogador[jogador.Id] = (atual.Jogador, atual.TotalGames + games);
                }
                else
                {
                    pontosPorJogador[jogador.Id] = (jogador, games);
                }
            }

            foreach (var p in partidas)
            {
                Somar(p.Dupla1.Jogador1, p.GamesDupla1 ?? 0);
                Somar(p.Dupla1.Jogador2, p.GamesDupla1 ?? 0);
                Somar(p.Dupla2.Jogador1, p.GamesDupla2 ?? 0);
                Somar(p.Dupla2.Jogador2, p.GamesDupla2 ?? 0);
            }

            var classificacao = pontosPorJogador.Values
                .OrderByDescending(v => v.TotalGames)
                .Select(v => new ClassificacaoAmericanoItemVM { Jogador = v.Jogador, TotalGames = v.TotalGames })
                .ToList();

            ViewBag.Torneio = torneio;
            ViewBag.CategoriaId = categoriaId;
            return View(classificacao);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GerarFaseGrupos(int torneioId, int categoriaId, DateTime dataHoraInicio)
        {
            var torneio = await _context.Torneios.FindAsync(torneioId);
            if (torneio == null) return NotFound();
            if (!await EhOrganizadorAsync(torneioId, ObterJogadorIdLogado() ?? 0)) return Forbid();

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
        // GerarMataMata (manual) foi removida: o cruzamento agora é sempre automático, via
        // ProcessarMataMataAutomatico (disparado por FinalizarPartida assim que a última partida
        // da Fase de Grupos de uma categoria termina).
        
    }
}