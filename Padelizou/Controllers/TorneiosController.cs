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
    public partial class TorneiosController : Controller
    {
        private readonly DbPadelContext _context;
        private readonly IEstatisticasService _estatisticas;
        private readonly IPalpiteService _palpites;
        private readonly IWebHostEnvironment _env;
        private readonly IEmailService _emailService;
        private readonly IPushNotificationService _pushService;
        private readonly IPagamentoInscricaoService _pagamentos;
        private readonly TaxasExibicao _taxas;
        private readonly RegistroResultadosSettings _registro;
        private readonly ILogger<TorneiosController> _logger;

        // Injeta o banco de dados
        public TorneiosController(DbPadelContext context, IEstatisticasService estatisticas, IPalpiteService palpites,
            IWebHostEnvironment env, IEmailService emailService, IPushNotificationService pushService,
            IPagamentoInscricaoService pagamentos, Microsoft.Extensions.Options.IOptions<TaxasExibicao> taxas,
            Microsoft.Extensions.Options.IOptions<RegistroResultadosSettings> registro,
            ILogger<TorneiosController> logger)
        {
            _context = context;
            _estatisticas = estatisticas;
            _palpites = palpites;
            _env = env;
            _emailService = emailService;
            _pushService = pushService;
            _pagamentos = pagamentos;
            _taxas = taxas.Value;
            _registro = registro.Value;
            _logger = logger;
        }

        // Notifica quem tem NotificarSeguidosTorneio marcado e segue algum dos jogadores que
        // acabou de se inscrever num torneio (usado por DuplasController seria o ideal, mas o
        // gancho fica aqui perto de InscreverIndividual pra reaproveitar os mesmos campos —
        // ver também o gancho equivalente em DuplasController.Create).
        private async Task NotificarSeguidoresDeInscricaoAsync(int torneioId, IEnumerable<int> jogadoresInscritos)
        {
            var torneio = await _context.Torneios.FindAsync(torneioId);
            if (torneio == null) return;

            var jogadores = await _context.Jogadores
                .Where(j => jogadoresInscritos.Contains(j.Id))
                .ToDictionaryAsync(j => j.Id, j => j.Nome);

            var seguidores = await _context.SeguidoresJogador
                .Include(s => s.Seguidor)
                .Where(s => jogadoresInscritos.Contains(s.SeguidoId) && s.Seguidor.NotificarSeguidosTorneio)
                .ToListAsync();

            var url = Url.Action("Details", "Torneios", new { id = torneioId });

            foreach (var grupo in seguidores.GroupBy(s => s.SeguidorId))
            {
                var seguidor = grupo.First().Seguidor;
                var nomesQueSigo = grupo.Select(s => jogadores.TryGetValue(s.SeguidoId, out var nome) ? nome : "").Where(n => n != "");
                var titulo = "Alguém que você segue se inscreveu num torneio";
                var corpo = $"{string.Join(" e ", nomesQueSigo)} se inscreveu em {torneio.Nome}.";

                if (seguidor.NotificarEmail && !string.IsNullOrWhiteSpace(seguidor.Email))
                {
                    try
                    {
                        await _emailService.EnviarAsync(seguidor.Email!, seguidor.Nome, titulo,
                            $"<p>{corpo}</p>");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Falha ao enviar e-mail de seguidor pro torneio {TorneioId}, jogador {JogadorId}", torneioId, seguidor.Id);
                    }
                }

                try
                {
                    await _pushService.EnviarParaJogadorAsync(seguidor.Id, titulo, corpo, url);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao enviar push de seguidor pro torneio {TorneioId}, jogador {JogadorId}", torneioId, seguidor.Id);
                }
            }
        }

        // Salva a capa do torneio (redimensionada e recodificada) e devolve o caminho relativo
        // pra gravar no banco, ou null se a imagem não pôde ser processada.
        private Task<ResultadoDaImagem> SalvarCapaAsync(IFormFile arquivo) =>
            ImagemEnviada.SalvarAsync(arquivo, _env.WebRootPath, "capas-torneio", FormatoDeImagem.CapaTorneio, _logger);

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

            // Torneios Ocultos somem da listagem pública — só continuam visíveis aqui
            // pra quem é organizador deles (pra não "perder" o próprio torneio de vista).
            var jogadorId = ObterJogadorIdLogado();
            var meusTorneioIds = jogadorId.HasValue
                ? (await _context.TorneioOrganizadores.Where(o => o.JogadorId == jogadorId.Value).Select(o => o.TorneioId).ToListAsync()).ToHashSet()
                : new HashSet<int>();
            torneios = torneios.Where(t => !t.Oculto || meusTorneioIds.Contains(t.Id)).ToList();

            ViewBag.Abertos = torneios.Where(t => t.Status == "Inscrições Abertas").ToList();
            ViewBag.EmAndamento = torneios.Where(t => t.Status != "Inscrições Abertas" && t.Status != "Finalizado").ToList();
            ViewBag.Finalizados = torneios.Where(t => t.Status == "Finalizado").ToList();

            return View();
        }

        // Exemplo de como deve ficar o seu método Details
        [HttpGet]
        public async Task<IActionResult> Details(int id, int? timeFiltroId, int[]? categoriaFiltroIds)
        {
            var torneio = await _context.Torneios
                .Include(t => t.Categorias)
                    .ThenInclude(c => c.Duplas)
                        .ThenInclude(d => d.Jogador1)
                            .ThenInclude(j => j.Time)
                .Include(t => t.Categorias)
                    .ThenInclude(c => c.Duplas)
                        // `Jogador2!`: dupla sem parceiro é caso normal; o EF só lê a expressão.
                        .ThenInclude(d => d.Jogador2!)
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

            // "Cabe?" — enquanto as chaves não foram sorteadas ainda dá pra mudar quadras,
            // duração ou horário. Depois, remarcar significa avisar todo mundo de novo.
            if (torneio.Status == "Chaves em Sorteio" && torneio.Formato != "Americano")
            {
                ViewBag.PrevisaoGrade = MontarPrevisaoDaGrade(torneio);
            }

            // Torneio "por fora" com inscrições fechadas: o botão de sortear dá lugar ao
            // caminho do pagamento enquanto a taxa não for quitada/negociada.
            ViewBag.TaxaExternoPendente = torneio.Status == "Chaves em Sorteio"
                && await TaxaExternoImpedeChavesAsync(torneio);

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

            // Pro botão "sou eu" da inscrição e pro aviso de estar inscrevendo outra pessoa.
            // O único CPF que vai pra tela é o de quem está logado — o dele mesmo.
            if (jogadorLogadoId.HasValue)
            {
                var eu = await _context.Jogadores
                    .Where(j => j.Id == jogadorLogadoId.Value)
                    .Select(j => new { j.Id, j.Cpf, j.Nome })
                    .FirstOrDefaultAsync();
                ViewBag.MeuJogadorId = eu?.Id;
                ViewBag.MeuCpf = eu?.Cpf;
                ViewBag.MeuNome = eu?.Nome;
            }

            // Valor final anunciado: quem se inscreve precisa ver na tela o mesmo que será
            // cobrado no checkout, e não descobrir a taxa só depois de clicar.
            var recebedorTorneio = await _pagamentos.ObterRecebedorTorneioAsync(id);
            // Preço por pessoa: quem vê a tela quer saber quanto sai do bolso dele.
            var exibicao = torneio.CobraPeloSite
                ? _pagamentos.CalcularExibicao(torneio.PrecoInscricao, "Torneio", recebedorTorneio,
                    torneio.ModoComissao, _taxas.PercentualDoTorneio(torneio.FormaPagamento))
                : null;
            ViewBag.PrecoTotal = exibicao?.Total;
            ViewBag.TaxaServico = exibicao?.Taxa;

            ViewBag.PodeGerenciar = jogadorLogadoId.HasValue && await EhOrganizadorAsync(id, jogadorLogadoId.Value);

            // Pedido de equipe pra registrar os resultados: o mais recente manda na tela.
            ViewBag.RegistroHabilitado = _registro.Habilitado;
            ViewBag.PedidoRegistro = await _context.SolicitacoesRegistroResultados
                .Where(s => s.TorneioId == id)
                .OrderByDescending(s => s.SolicitadaEm)
                .FirstOrDefaultAsync();
            if (ViewBag.PodeGerenciar == true)
            {
                ViewBag.Organizadores = await _context.TorneioOrganizadores
                    .Include(o => o.Jogador)
                    .Where(o => o.TorneioId == id)
                    .ToListAsync();
                ViewBag.CatalogoClubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();
                ViewBag.Quadras = await _context.Quadras.Where(q => q.TorneioId == id).OrderBy(q => q.Id).ToListAsync();
            }

            // Aba "Jogos" embutida (Ao Vivo/Agendadas/Finalizadas) — só depois que as inscrições fecham.
            if (torneio.Status != "Inscrições Abertas")
            {
                await CarregarViewBagJogosAsync(id, timeFiltroId, categoriaFiltroIds);
                // Pontos por time neste torneio (só faz sentido depois que começa a valer resultado).
                ViewBag.PontosTimes = await _estatisticas.ObterPontosTimesNoTorneioAsync(id);

                // Chaveamento do mata-mata: as partidas de fase eliminatória por categoria, pra
                // a view desenhar o bracket com os confrontos REAIS (antes era um desenho fixo).
                var fasesMataMata = new[] { "Oitavas de Final", "Quartas de Final", "Semifinal", "Final" };
                var partidasMataMata = await _context.Partidas
                    .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
                    .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
                    .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
                    .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
                    .Where(p => p.TorneioId == id && fasesMataMata.Contains(p.Fase))
                    .ToListAsync();
                ViewBag.MataMataPorCategoria = partidasMataMata
                    .GroupBy(p => p.CategoriaId)
                    .ToDictionary(g => g.Key, g => g.ToList());
            }

            return View(torneio);
        }
        public async Task<IActionResult> Jogos(int id, int? timeFiltroId, int[]? categoriaFiltroIds)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            await CarregarViewBagJogosAsync(id, timeFiltroId, categoriaFiltroIds);
            ViewBag.Torneio = torneio;

            return View();
        }

        // Compartilhado entre Jogos() (página dedicada, usada como destino do "Editar Jogo")
        // e Details() (aba "Jogos" embutida na página do torneio) — mesma lógica de filtro/abas
        // pros dois lugares não divergirem.
        private async Task CarregarViewBagJogosAsync(int torneioId, int? timeFiltroId, int[]? categoriaFiltroIds)
        {
            var query = _context.Partidas
                .Include(p => p.Categoria)
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1).ThenInclude(j => j.Time)
                // `Jogador2!`: quem se inscreveu sozinho ainda não tem parceiro. O EF lê a
                // expressão pra montar o JOIN, não executa o acesso.
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2!).ThenInclude(j => j.Time)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1).ThenInclude(j => j.Time)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2!).ThenInclude(j => j.Time)
                .Where(p => p.TorneioId == torneioId);

            if (timeFiltroId.HasValue)
            {
                // Vira SQL, não roda em memória: dupla sem parceiro compara contra NULO e
                // simplesmente não casa, que é o resultado certo pro filtro por time.
                query = query.Where(p =>
                    (p.Dupla1.Jogador1.TimeId == timeFiltroId || p.Dupla1.Jogador2!.TimeId == timeFiltroId) ||
                    (p.Dupla2.Jogador1.TimeId == timeFiltroId || p.Dupla2.Jogador2!.TimeId == timeFiltroId)
                );
            }

            if (categoriaFiltroIds != null && categoriaFiltroIds.Length > 0)
            {
                query = query.Where(p => categoriaFiltroIds.Contains(p.CategoriaId));
            }

            var partidas = await query.ToListAsync();

            ViewBag.AoVivo = partidas.Where(p => p.Status == "AoVivo").OrderBy(p => p.HorarioInicioReal).ToList();
            ViewBag.Finalizadas = partidas.Where(p => p.Status == "Finalizada").OrderByDescending(p => p.HorarioFimReal).ToList();
            ViewBag.Agendadas = partidas.Where(p => p.Status == "Agendada").OrderBy(p => p.HorarioPrevisto).ToList();

            ViewBag.Times = new SelectList(_context.Times, "Id", "Nome", timeFiltroId);
            ViewBag.TimeAtual = timeFiltroId;
            var categoriasDoTorneio = await _context.Categorias.Where(c => c.TorneioId == torneioId).OrderBy(c => c.Nome).ToListAsync();
            ViewBag.CategoriasDoTorneio = categoriasDoTorneio;
            ViewBag.CategoriaFiltroAtual = categoriaFiltroIds ?? Array.Empty<int>();

            // No Americano a classificação É o placar do torneio — quem somou mais games.
            // Ela morava numa página separada, sem botão de voltar; agora é uma aba aqui,
            // ao lado de Ao Vivo, que é onde o pessoal já está olhando.
            var torneioDaTela = await _context.Torneios.FindAsync(torneioId);

            // Quem organiza vê os botões de mexer no jogo ("colocar no ar", editar placar).
            // Fica FORA do if do Americano de propósito: nasceu lá dentro, quando só o
            // desempate precisava dele, e o resultado era que num torneio de duplas — a
            // maioria — a flag nem existia, e o organizador não via botão nenhum.
            ViewBag.EhOrganizador = await EhOrganizadorAsync(torneioId, ObterJogadorIdLogado() ?? 0);

            if (torneioDaTela?.Formato == "Americano")
            {
                var finalizadas = partidas.Where(p => p.Status == "Finalizada" && p.Fase.StartsWith("Americano"));

                ViewBag.ClassificacaoAmericano = categoriasDoTorneio.ToDictionary(
                    c => c.Nome,
                    c => TabelaDoAmericano.Montar(finalizadas.Where(p => p.CategoriaId == c.Id)));

                // O botão "montar o desempate" é só do organizador; o jogador vê o aviso.
                ViewBag.DesempateAmericano = torneioDaTela.DesempateAmericano;
            }

            // PALPITRÔMETRO: resumo de votos de cada partida exibida, num único lote.
            int? meuId = User.Identity?.IsAuthenticated == true
                ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
                : null;
            ViewBag.MeuId = meuId;
            ViewBag.Palpites = await _palpites.ObterResumosAsync(partidas.Select(p => p.Id), meuId);
        }
    }
}
