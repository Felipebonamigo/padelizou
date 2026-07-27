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
        private readonly IEmailService _emailService;
        private readonly IPushNotificationService _pushService;
        private readonly IPagamentoInscricaoService _pagamentos;
        private readonly TaxasExibicao _taxas;
        private readonly ILogger<TorneiosController> _logger;

        // Injeta o banco de dados
        public TorneiosController(DbPadelContext context, IEstatisticasService estatisticas, IPalpiteService palpites,
            IWebHostEnvironment env, IEmailService emailService, IPushNotificationService pushService,
            IPagamentoInscricaoService pagamentos, Microsoft.Extensions.Options.IOptions<TaxasExibicao> taxas,
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

        // Gera a chave de acesso de um torneio Restrito. Sem 0/O/1/I pra não confundir
        // na hora de digitar/ler em voz alta.
        private static string GerarChaveAcesso()
        {
            const string caracteres = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var buffer = new char[6];
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = caracteres[Random.Shared.Next(caracteres.Length)];
            }
            return new string(buffer);
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

        // 1. ABRE A TELA DE CRIAÇÃO (Carrega o Catálogo)
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Create()
        {
            // Busca todas as categorias do banco para montar os Checkboxes na tela
            var catalogo = await _context.CategoriasPadrao.OrderBy(c => c.Id).ToListAsync();
            ViewBag.CatalogoCategorias = catalogo;
            ViewBag.CatalogoClubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();

            // Sem um Torneio no View(), asp-for não teria de onde tirar valor e os campos
            // obrigatórios de horário e duração nasceriam VAZIOS — o organizador teria que
            // adivinhar o que preencher. Assim ele começa com 8h-22h e 50 min e só ajusta.
            return View(new Torneio());
        }

        // 2. RECEBE OS DADOS E SALVA O TORNEIO
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create(Torneio torneio, int[] categoriasSelecionadas, int[]? organizadoresSelecionados, string[]? nomesQuadras, IFormFile? capa, Dictionary<int, int?>? limiteCategoria, string? novoClubeNome = null)
        {
            // O clube pode ser escrito na hora: numa base nova não existe nenhum, e um select
            // obrigatório e vazio impediria de criar o primeiro torneio.
            var clubeNovo = await CatalogoLocais.AcharOuCriarClubeAsync(_context, novoClubeNome);
            if (clubeNovo != null) torneio.ClubeId = clubeNovo.Id;

            // Sem clube o insert estouraria na chave estrangeira, com erro 500 e o formulário
            // inteiro perdido. Melhor recusar aqui, explicando o que fazer.
            if (torneio.ClubeId <= 0)
            {
                ViewBag.Erro = "Escolha o clube responsável, ou escreva o nome dele no campo abaixo do seletor.";
                ViewBag.CatalogoCategorias = await _context.CategoriasPadrao.OrderBy(c => c.Id).ToListAsync();
                ViewBag.CatalogoClubes = await _context.Clubes.OrderBy(c => c.Nome).ToListAsync();
                return View(torneio);
            }

            // O local do torneio é o clube: pedir os dois era pedir o mesmo dado duas vezes.
            if (string.IsNullOrWhiteSpace(torneio.LocalTorneio))
            {
                torneio.LocalTorneio = clubeNovo?.Nome
                    ?? (await _context.Clubes.FindAsync(torneio.ClubeId))?.Nome;
            }

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
            torneio.Codigo = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            torneio.ChaveAcesso = torneio.Restrito ? GerarChaveAcesso() : null;

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
                        int? limite = limiteCategoria != null && limiteCategoria.TryGetValue(catId, out var l) && l is > 0 ? l : null;
                        var novaCategoria = new Categoria
                        {
                            TorneioId = torneio.Id,
                            Nome = catPadrao.Nome,
                            Codigo = catPadrao.Codigo,
                            LimiteDuplas = limite
                        };
                        _context.Categorias.Add(novaCategoria);
                    }
                }
                await _context.SaveChangesAsync();
            }

            // Avisa quem tem NotificarTorneiosAbertos marcado que um torneio novo abriu.
            var elegiveis = await _context.Jogadores.Where(j => j.NotificarTorneiosAbertos).ToListAsync();
            var urlTorneio = Url.Action("Details", "Torneios", new { id = torneio.Id });

            foreach (var jogador in elegiveis.Where(j => j.NotificarEmail && !string.IsNullOrWhiteSpace(j.Email)))
            {
                try
                {
                    await _emailService.EnviarAsync(jogador.Email!, jogador.Nome,
                        "Novo torneio aberto - Padelizou",
                        $"<p>Olá {jogador.Nome},</p><p>Um novo torneio acabou de abrir: <strong>{torneio.Nome}</strong>.</p>");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao enviar e-mail de torneio aberto {TorneioId} para jogador {JogadorId}", torneio.Id, jogador.Id);
                }
            }

            foreach (var jogador in elegiveis)
            {
                try
                {
                    await _pushService.EnviarParaJogadorAsync(jogador.Id, "Novo torneio aberto", torneio.Nome, urlTorneio);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha ao enviar push de torneio aberto {TorneioId} para jogador {JogadorId}", torneio.Id, jogador.Id);
                }
            }

            return RedirectToAction("Details", new { id = torneio.Id });
        }

        // Autocomplete pra achar quem vai organizar o torneio. Usa a mesma busca do resto
        // do sistema (nome, apelido ou CPF completo, sem diferenciar maiúsculas) — antes
        // aqui era CPF parcial + login exato em maiúsculas, uma terceira regra própria.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> BuscarJogadorParaOrganizador(string termo)
        {
            if (string.IsNullOrWhiteSpace(termo) || termo.Trim().Length < 3) return Json(Array.Empty<object>());

            var achados = await BuscaJogador.BuscarAsync(_context, termo, limite: 8);

            return Json(achados.Select(j => new
            {
                j.Id,
                j.Nome,
                j.FotoPerfil,
                j.Login,
                apelido = j.Apelido ?? "",
                // A tela mostra uma linha só: o apelido quando existir, senão o nome.
                exibicao = j.ComoChamar,
            }));
        }

        // Preenche sozinho os dados do jogador na tela de inscrição quando o CPF digitado já
        // existe. Exige CPF completo de propósito: com busca parcial daria pra varrer a base
        // e colher nome/celular/cidade de terceiros.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> BuscarJogadorPorCpf(string cpf)
        {
            cpf = Documentos.SomenteDigitos(cpf);
            if (!Documentos.CpfTemFormatoValido(cpf)) return Json(new { encontrado = false });

            var jogador = await _context.Jogadores
                .Where(j => j.Cpf == cpf)
                .Select(j => new { j.Nome, j.Apelido, j.Celular, j.Cidade, j.Estado })
                .FirstOrDefaultAsync();

            if (jogador == null) return Json(new { encontrado = false });

            return Json(new
            {
                encontrado = true,
                nome = jogador.Nome,
                // A tela mostra "achamos: Fulano" — o apelido confirma que é quem se pensa.
                apelido = jogador.Apelido ?? "",
                celular = jogador.Celular ?? "",
                cidade = jogador.Cidade ?? "",
                estado = jogador.Estado ?? ""
            });
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
        public async Task<IActionResult> InscreverIndividual(int torneioId, int categoriaId, string nome, string cpf, string? chaveAcesso = null)
        {
            // Mesma limpeza de DuplasController.Create: CPF com máscara estoura a coluna de
            // 11 chars e derruba a página em vez de avisar o jogador.
            cpf = Documentos.SomenteDigitos(cpf);
            if (!Documentos.CpfTemFormatoValido(cpf))
            {
                TempData["Erro"] = "CPF inválido — informe os 11 números, sem pontos ou traço.";
                return RedirectToAction("Details", new { id = torneioId });
            }

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

            if (torneio.Restrito && !string.Equals(chaveAcesso?.Trim(), torneio.ChaveAcesso, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Erro"] = "Chave de acesso inválida. Confira com o organizador do torneio.";
                return RedirectToAction("Details", new { id = torneioId });
            }

            var jogador = await _context.Jogadores.FirstOrDefaultAsync(j => j.Cpf == cpf);
            if (jogador == null)
            {
                jogador = new Jogador { Nome = nome, Cpf = cpf };
                _context.Jogadores.Add(jogador);
                await _context.SaveChangesAsync();
            }

            // Uma categoria por jogador, quando o organizador desligou as múltiplas.
            var bloqueioCategorias = await InscricaoTorneio.MotivoBloqueioMultiplasCategoriasAsync(
                _context, torneio, new[] { jogador.Id });
            if (bloqueioCategorias != null)
            {
                TempData["Erro"] = bloqueioCategorias;
                return RedirectToAction("Details", new { id = torneioId });
            }

            bool jaInscrito = await _context.InscricoesAmericanas
                .AnyAsync(i => i.CategoriaId == categoriaId && i.JogadorId == jogador.Id);
            if (!jaInscrito)
            {
                // Torneio pago com recebimento ativado? A inscrição ainda NÃO é criada: o
                // jogador vai pro checkout e ela nasce quando o webhook confirmar o pagamento
                // (PagamentoInscricaoService.EfetivarAsync).
                var recebedor = await _pagamentos.ObterRecebedorTorneioAsync(torneioId);
                // Pagar na hora só é obrigatório se o organizador quis assim. Senão a inscrição
            // nasce agora mesmo, marcada como não paga, e o acerto vem depois.
            if (_pagamentos.PodeCobrar(torneio, recebedor) && torneio.PagamentoObrigatorioNaInscricao)
                {
                    var dadosInscricao = new DadosInscricaoTorneio(
                        torneioId, categoriaId, jogador.Id, null, false, false, false);

                    var checkout = await _pagamentos.IniciarCobrancaTorneioAsync(
                        torneio, recebedor!, jogador, "TorneioAmericano", dadosInscricao);

                    if (checkout != null) return Redirect(checkout);

                    TempData["Erro"] = "Não foi possível gerar a cobrança agora. Tente novamente em instantes.";
                    return RedirectToAction("Details", new { id = torneioId });
                }

                // Vagas: mesma regra da inscrição em dupla (ver DuplasController) — se a
                // categoria ou o torneio já estão cheios, entra na lista de espera.
                bool emListaDeEspera = false;
                if (categoria.LimiteDuplas.HasValue)
                {
                    int naCategoria = await _context.InscricoesAmericanas.CountAsync(i => i.CategoriaId == categoriaId && !i.EmListaDeEspera);
                    emListaDeEspera = naCategoria >= categoria.LimiteDuplas.Value;
                }
                if (!emListaDeEspera && torneio.LimiteDuplasTotal.HasValue)
                {
                    int noTorneio = await _context.InscricoesAmericanas.CountAsync(i => i.Categoria.TorneioId == torneioId && !i.EmListaDeEspera);
                    emListaDeEspera = noTorneio >= torneio.LimiteDuplasTotal.Value;
                }

                _context.InscricoesAmericanas.Add(new InscricaoAmericana { CategoriaId = categoriaId, JogadorId = jogador.Id, EmListaDeEspera = emListaDeEspera });
                await _context.SaveChangesAsync();

                await NotificarSeguidoresDeInscricaoAsync(torneioId, new[] { jogador.Id });

                TempData["Sucesso"] = emListaDeEspera
                    ? "Vagas esgotadas — inscrição entrou na lista de espera. Se alguém desistir, é chamado na ordem de inscrição."
                    : "Inscrição individual confirmada!";
            }
            else
            {
                TempData["Sucesso"] = "Inscrição individual confirmada!";
            }
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
            string? restricaoCategoria,
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
            torneio.RestricaoCategoria = string.IsNullOrEmpty(restricaoCategoria) ? "Livre" : restricaoCategoria;
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

        // A última palavra sobre quem pagou é sempre do organizador: muita inscrição é
        // acertada em dinheiro na quadra ou por Pix direto, e o site não tem como saber.
        // Vale nos dois sentidos — marcar e desmarcar.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlternarPagamentoDupla(int duplaId)
        {
            var dupla = await _context.Duplas.Include(d => d.Categoria).FirstOrDefaultAsync(d => d.Id == duplaId);
            if (dupla == null) return NotFound();

            int torneioId = dupla.Categoria.TorneioId;
            var jogadorId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(torneioId, jogadorId)) return Forbid();

            dupla.Pago = !dupla.Pago;
            dupla.PagoEm = dupla.Pago ? DateTime.Now : null;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = dupla.Pago ? "Inscrição marcada como paga." : "Inscrição marcada como não paga.";
            return RedirectToAction("Details", new { id = torneioId });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AlternarPagamentoAmericano(int inscricaoId)
        {
            var inscricao = await _context.InscricoesAmericanas
                .Include(i => i.Categoria).FirstOrDefaultAsync(i => i.Id == inscricaoId);
            if (inscricao == null) return NotFound();

            int torneioId = inscricao.Categoria.TorneioId;
            var jogadorId = ObterJogadorIdLogado() ?? 0;
            if (!await EhOrganizadorAsync(torneioId, jogadorId)) return Forbid();

            inscricao.Pago = !inscricao.Pago;
            inscricao.PagoEm = inscricao.Pago ? DateTime.Now : null;
            await _context.SaveChangesAsync();

            TempData["Sucesso"] = inscricao.Pago ? "Inscrição marcada como paga." : "Inscrição marcada como não paga.";
            return RedirectToAction("Details", new { id = torneioId });
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

            bool eraConfirmada = !dupla.EmListaDeEspera;
            _context.Duplas.Remove(dupla);
            await _context.SaveChangesAsync();

            // Abriu vaga: promove quem está há mais tempo na lista de espera desta categoria
            // (a dupla de menor Id, já que a ordem de inscrição segue a ordem de criação).
            if (eraConfirmada)
            {
                var proximaDaFila = await _context.Duplas
                    .Where(d => d.CategoriaId == dupla.CategoriaId && d.EmListaDeEspera)
                    .OrderBy(d => d.Id)
                    .FirstOrDefaultAsync();
                if (proximaDaFila != null)
                {
                    proximaDaFila.EmListaDeEspera = false;
                    await _context.SaveChangesAsync();
                }
            }

            TempData["Sucesso"] = "Inscrito removido do torneio.";
            return RedirectToAction("Details", new { id = torneioId });
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

            // "Cabe?" — enquanto as chaves não foram sorteadas ainda dá pra mudar quadras,
            // duração ou horário. Depois, remarcar significa avisar todo mundo de novo.
            if (torneio.Status == "Chaves em Sorteio" && torneio.Formato != "Americano")
            {
                ViewBag.PrevisaoGrade = MontarPrevisaoDaGrade(torneio);
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

            // Pontos reais de todo mundo inscrito, numa consulta só. Antes isto usava
            // Jogador.PontuacaoGlobal, campo morto que é sempre 0 — na prática os cabeças de
            // chave saíam na ordem de inscrição, e não por ranking.
            var idsInscritos = torneio.Categorias
                .SelectMany(c => c.Duplas)
                .SelectMany(d => new[] { d.Jogador1Id, d.Jogador2Id })
                .Where(id => id != null).Select(id => id!.Value)
                .ToList();
            var pontosPorJogador = await _estatisticas.ObterPontosPorJogadorAsync(idsInscritos);

            // A grade é UMA só pro torneio inteiro. Antes cada categoria recomeçava do
            // horário de início, então três categorias marcavam jogos no mesmo horário nas
            // mesmas quadras. Os horários são atribuídos depois, com todos os jogos na mão.
            var jogosPraAgendar = new List<Partida>();

            foreach (var categoria in torneio.Categorias)
            {
                // Só entra no sorteio quem está pronto pra jogar: dupla fechada (com os dois
                // nomes) e confirmada. Quem está na lista de espera ou ainda sem parceiro
                // continua inscrito, mas fora das chaves.
                var duplas = categoria.Duplas.Where(d => d.Completa && !d.EmListaDeEspera).ToList();

                // CORREÇÃO DA REGRA DE OURO:
                // O mínimo para ter jogo não é 3, é 2 duplas (Para uma chave final direta)!
                if (duplas.Count < 2) continue;

                // ORDENAÇÃO PELO RANKING (Define os Cabeças de Chave)
                var duplasOrdenadas = duplas
                    .OrderByDescending(d => pontosPorJogador.GetValueOrDefault(d.Jogador1Id)
                                          + pontosPorJogador.GetValueOrDefault(d.Jogador2Id!.Value))
                    .ToList();

                // O normal dos torneios é fechar em grupos de 3 duplas. Quando o total não é
                // múltiplo de 3, os melhores rankeados resolvem em grupo(s) de 2 (chave direta),
                // e o restante (sempre múltiplo de 3 depois disso) fecha em grupos de 3 normalmente:
                //   - sobra 2 (ex: 14 duplas): 1º x 2º vira um grupo de 2 só, o resto (12) fecha em 4 grupos de 3.
                //   - sobra 1 (ex: 13 duplas): os 4 melhores viram 2 grupos de 2 (1º x 4º e 2º x 3º),
                //     o resto (9) fecha em 3 grupos de 3.
                int n = duplasOrdenadas.Count;
                var gruposDeDuplas = new List<List<Dupla>>();

                if (n < 3)
                {
                    gruposDeDuplas.Add(duplasOrdenadas); // 2 duplas: só dá pra ter a chave direta 1x2
                }
                else
                {
                    int resto = n % 3;
                    List<Dupla> restantes;

                    if (resto == 1)
                    {
                        gruposDeDuplas.Add(new List<Dupla> { duplasOrdenadas[0], duplasOrdenadas[3] });
                        gruposDeDuplas.Add(new List<Dupla> { duplasOrdenadas[1], duplasOrdenadas[2] });
                        restantes = duplasOrdenadas.Skip(4).ToList();
                    }
                    else if (resto == 2)
                    {
                        gruposDeDuplas.Add(new List<Dupla> { duplasOrdenadas[0], duplasOrdenadas[1] });
                        restantes = duplasOrdenadas.Skip(2).ToList();
                    }
                    else
                    {
                        restantes = duplasOrdenadas;
                    }

                    int numGruposDeTres = restantes.Count / 3;
                    if (numGruposDeTres > 0)
                    {
                        var bucket = new List<Dupla>[numGruposDeTres];
                        for (int i = 0; i < numGruposDeTres; i++) bucket[i] = new List<Dupla>();

                        // DISTRIBUIÇÃO EM ZIGUE-ZAGUE (balanceia 1 cabeça de chave forte/médio/fraco por grupo)
                        int grupoIndex = 0;
                        int direcao = 1;
                        foreach (var dupla in restantes)
                        {
                            bucket[grupoIndex].Add(dupla);

                            grupoIndex += direcao;
                            if (grupoIndex >= numGruposDeTres)
                            {
                                grupoIndex = numGruposDeTres - 1;
                                direcao = -1;
                            }
                            else if (grupoIndex < 0)
                            {
                                grupoIndex = 0;
                                direcao = 1;
                            }
                        }
                        gruposDeDuplas.AddRange(bucket);
                    }
                }

                var gruposCriados = new List<GrupoTorneio>();
                for (int i = 0; i < gruposDeDuplas.Count; i++)
                {
                    char letra = (char)('A' + i);
                    var novoGrupo = new GrupoTorneio { CategoriaId = categoria.Id, Nome = $"Grupo {letra}" };
                    _context.Add(novoGrupo);
                    gruposCriados.Add(novoGrupo);
                }
                await _context.SaveChangesAsync();

                // Vincula as duplas aos grupos E gera os jogos. Antes, este passo só setava o
                // GrupoTorneioId: as duplas ficavam com Grupo(str) nulo e NENHUMA partida era
                // criada, então o torneio travava em "Fase de Grupos" sem jogos pra registrar e
                // sem como avançar pro mata-mata (que agrupa por dupla.Grupo).
                for (int i = 0; i < gruposDeDuplas.Count; i++)
                {
                    char letra = (char)('A' + i);
                    var duplasDoGrupo = gruposDeDuplas[i];

                    foreach (var dupla in duplasDoGrupo)
                    {
                        dupla.GrupoTorneioId = gruposCriados[i].Id;
                        dupla.Grupo = letra.ToString();
                    }

                    // Todos contra todos dentro do grupo.
                    for (int a = 0; a < duplasDoGrupo.Count; a++)
                    {
                        for (int b = a + 1; b < duplasDoGrupo.Count; b++)
                        {
                            jogosPraAgendar.Add(new Partida
                            {
                                TorneioId = torneio.Id,
                                CategoriaId = categoria.Id,
                                Dupla1Id = duplasDoGrupo[a].Id,
                                Dupla2Id = duplasDoGrupo[b].Id,
                                Fase = $"Grupo {letra}",
                                Status = "Agendada",
                                Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper()
                            });
                        }
                    }
                }
            }

            // Agora sim os horários: N quadras em paralelo, parando no fim do expediente e
            // retomando no dia seguinte (ver GradeDeJogos).
            var horarios = GradeDeJogos.Horarios(
                torneio.AberturaDaGrade,
                torneio.HoraFimDoDia,
                torneio.QuantidadeQuadras,
                torneio.TempoPrevistoPartidaMinutos,
                jogosPraAgendar.Count,
                aberturaDiasSeguintes: torneio.HoraInicioDiasSeguintes).ToList();

            for (int i = 0; i < jogosPraAgendar.Count; i++)
            {
                jogosPraAgendar[i].HorarioPrevisto = horarios[i];
            }
            _context.Partidas.AddRange(jogosPraAgendar);

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
                .Where(p => p.TorneioId == id && p.Status == "AoVivo")
                .ToListAsync();

            ViewBag.TorneioId = id;
            return View(partidasEmAndamento);
        }

        // 1. ATUALIZAR PLACAR AO VIVO (Corrigido para Dupla1 e Dupla2)
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> AtualizarPlacarAoVivo(int partidaId, string equipe, string tipo, int valor)
        {
            var partida = await _context.Partidas.FindAsync(partidaId);
            if (partida == null) return NotFound();
            if (partida.TorneioId == null || !await EhOrganizadorAsync(partida.TorneioId.Value, ObterJogadorIdLogado() ?? 0)) return Forbid();

            if (equipe == "A")
            {
                if (tipo == "Game") partida.GamesDupla1 += valor;
                if (tipo == "Set") partida.SetsDupla1 += valor;
            }
            else if (equipe == "B")
            {
                if (tipo == "Game") partida.GamesDupla2 += valor;
                if (tipo == "Set") partida.SetsDupla2 += valor;
            }

            // Contagem de games não passa de 9 (regra do torneio).
            partida.GamesDupla1 = Math.Clamp(partida.GamesDupla1 ?? 0, 0, 9);
            partida.GamesDupla2 = Math.Clamp(partida.GamesDupla2 ?? 0, 0, 9);
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

                // ESTATÍSTICA: Carimba o perdedor com a fase em que foi eliminado.
                // Jogo de grupo NÃO carimba (senão o perdedor ficaria com UltimaFase="Grupo A").
                if (!FasesTorneio.EhFaseDeGrupos(partida.Fase))
                {
                    var perdedor = await _context.Duplas.FindAsync(perdedorId);
                    if (perdedor != null) perdedor.UltimaFase = partida.Fase;
                }

                await _context.SaveChangesAsync();

                // GATILHOS DO ROBÔ (Usando _context.Partidas). A fase de grupos existe em duas
                // grafias ("Fase de Grupos" nos seeds antigos, "Grupo A/B/..." no GerarChaves) —
                // o gatilho precisa aceitar as duas, senão o mata-mata nunca é gerado.
                if (FasesTorneio.EhFaseDeGrupos(partida.Fase))
                {
                    var jogosPendentes = await _context.Partidas
                        .CountAsync(p => p.CategoriaId == partida.CategoriaId
                                      && (p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo "))
                                      && p.Status != "Finalizada");
                    if (jogosPendentes == 0) await ProcessarMataMataAutomatico(partida.CategoriaId, partida.TorneioId);
                }
                else if (ChaveamentoMataMata.ProximaFase(partida.Fase) != null) // Oitavas, Quartas ou Semifinal
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

                await NotificarResultadoAsync(partida, vencedorId, perdedorId);
            }
            return RedirectToAction("MesaControle", new { id = partida?.TorneioId });
        }

        // Fim de jogo: avisa quem jogou e quem acompanha esses jogadores. É o momento em que
        // o app tem algo a dizer — antes disso a pessoa precisava abrir a tela pra descobrir.
        private async Task NotificarResultadoAsync(Partida partida, int vencedorId, int perdedorId)
        {
            try
            {
                var duplas = await _context.Duplas
                    .Include(d => d.Jogador1).Include(d => d.Jogador2)
                    .Where(d => d.Id == vencedorId || d.Id == perdedorId)
                    .ToListAsync();

                var vencedora = duplas.FirstOrDefault(d => d.Id == vencedorId);
                var perdedora = duplas.FirstOrDefault(d => d.Id == perdedorId);
                if (vencedora == null || perdedora == null) return;

                var torneio = partida.TorneioId == null ? null : await _context.Torneios.FindAsync(partida.TorneioId.Value);
                var url = Url.Action("Details", "Torneios", new { id = partida.TorneioId });

                // Push é lido de relance: apelido identifica mais rápido que nome completo.
                string Nomes(Dupla d) => $"{d.Jogador1?.ComoChamar} e {d.Jogador2?.ComoChamar}";
                var placar = $"{partida.GamesDupla1}x{partida.GamesDupla2}";
                var ondeFoi = torneio != null ? $" · {torneio.Nome}" : "";
                bool ehFinal = partida.Fase == "Final";

                // 1. Quem jogou recebe o próprio resultado.
                // Dupla incompleta não chega a jogar, mas o filtro protege o push de nulo.
                var idsVencedores = new[] { vencedora.Jogador1Id, vencedora.Jogador2Id }
                    .Where(id => id != null).Select(id => id!.Value).ToArray();
                var idsPerdedores = new[] { perdedora.Jogador1Id, perdedora.Jogador2Id }
                    .Where(id => id != null).Select(id => id!.Value).ToArray();

                foreach (var id in idsVencedores)
                {
                    await _pushService.EnviarParaJogadorAsync(id,
                        ehFinal ? "🏆 Campeões!" : "Vitória!",
                        ehFinal
                            ? $"Vocês venceram a final{ondeFoi}!"
                            : $"Vocês venceram {Nomes(perdedora)} ({placar}){ondeFoi}.",
                        url);
                }

                foreach (var id in idsPerdedores)
                {
                    await _pushService.EnviarParaJogadorAsync(id,
                        "Resultado do seu jogo",
                        $"{Nomes(vencedora)} venceu ({placar}){ondeFoi}.",
                        url);
                }

                // 2. Quem segue os jogadores fica sabendo — mas SÓ do mata-mata. Num dia de
                //    torneio a fase de grupos tem dezenas de jogos; avisar seguidor a cada um
                //    viraria spam e a pessoa desligaria a notificação de vez.
                if (FasesTorneio.EhFaseDeGrupos(partida.Fase)) return;

                var idsEmQuadra = idsVencedores.Concat(idsPerdedores).ToHashSet();
                var seguidores = await _context.SeguidoresJogador
                    .Include(s => s.Seguidor)
                    .Where(s => idsEmQuadra.Contains(s.SeguidoId)
                             && !idsEmQuadra.Contains(s.SeguidorId)
                             && s.Seguidor.NotificarSeguidosTorneio)
                    .Select(s => s.SeguidorId)
                    .Distinct()
                    .ToListAsync();

                foreach (var seguidorId in seguidores)
                {
                    await _pushService.EnviarParaJogadorAsync(seguidorId,
                        ehFinal ? "Saiu o campeão!" : "Resultado de quem você segue",
                        $"{Nomes(vencedora)} venceu {Nomes(perdedora)} ({placar}){ondeFoi}.",
                        url);
                }
            }
            catch (Exception ex)
            {
                // Push é acessório — o resultado já está gravado, não pode falhar por isso.
                _logger.LogWarning(ex, "Falha ao notificar resultado da partida {PartidaId}.", partida.Id);
            }
        }

        // ===================== FINANCEIRO DO TORNEIO =====================

        // Arrecadado, pendente e estornado por categoria, numa tela só. Antes o
        // organizador tinha que cruzar Pagamentos/Meus com a lista de inscritos.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Financeiro(int id)
        {
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var torneio = await _context.Torneios
                .Include(t => t.Categorias)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (torneio == null) return NotFound();

            var pagamentos = await _context.Pagamentos
                .Include(p => p.Jogador)
                .Where(p => p.TorneioId == id)
                .ToListAsync();

            var duplas = await _context.Duplas
                .Where(d => d.Categoria.TorneioId == id)
                .Select(d => new { d.CategoriaId, d.EmListaDeEspera })
                .ToListAsync();

            var americanos = await _context.InscricoesAmericanas
                .Where(i => i.Categoria.TorneioId == id)
                .Select(i => new { i.CategoriaId, i.EmListaDeEspera })
                .ToListAsync();

            var confirmados = pagamentos.Where(p => p.Status == "Confirmado").ToList();
            var pendentes = pagamentos.Where(p => p.Status == "Pendente").ToList();
            var estornados = pagamentos.Where(p => p.Status == "Estornado").ToList();

            var vm = new FinanceiroTorneioVM
            {
                Torneio = torneio,
                Arrecadado = confirmados.Sum(p => p.Valor),
                Pendente = pendentes.Sum(p => p.Valor),
                Estornado = estornados.Sum(p => p.Valor),
                TaxaPlataforma = confirmados.Sum(p => p.Comissao),
                Inscritos = duplas.Count + americanos.Count,
                Pagantes = confirmados.Select(p => p.JogadorId).Distinct().Count(),
            };

            // Quebra por categoria. O pagamento guarda a categoria dentro do JSON de
            // DadosInscricao, então o vínculo confiável é pela inscrição já criada
            // (ReferenciaId) — pagamento pendente ainda não tem inscrição.
            var categoriaPorDupla = await _context.Duplas
                .Where(d => d.Categoria.TorneioId == id)
                .ToDictionaryAsync(d => d.Id, d => d.CategoriaId);

            var categoriaPorAmericano = await _context.InscricoesAmericanas
                .Where(i => i.Categoria.TorneioId == id)
                .ToDictionaryAsync(i => i.Id, i => i.CategoriaId);

            int? CategoriaDo(Pagamento p)
            {
                if (p.ReferenciaId == null) return null;
                if (p.Tipo == "TorneioDupla" && categoriaPorDupla.TryGetValue(p.ReferenciaId.Value, out var c1)) return c1;
                if (p.Tipo == "TorneioAmericano" && categoriaPorAmericano.TryGetValue(p.ReferenciaId.Value, out var c2)) return c2;
                return null;
            }

            vm.PorCategoria = torneio.Categorias.Select(c => new FinanceiroCategoriaVM
            {
                Categoria = c.Nome,
                Inscritos = duplas.Count(d => d.CategoriaId == c.Id && !d.EmListaDeEspera)
                          + americanos.Count(a => a.CategoriaId == c.Id && !a.EmListaDeEspera),
                ListaDeEspera = duplas.Count(d => d.CategoriaId == c.Id && d.EmListaDeEspera)
                              + americanos.Count(a => a.CategoriaId == c.Id && a.EmListaDeEspera),
                Arrecadado = confirmados.Where(p => CategoriaDo(p) == c.Id).Sum(p => p.Valor),
                Estornado = estornados.Where(p => CategoriaDo(p) == c.Id).Sum(p => p.Valor),
                // Pendente não tem inscrição ainda, então não dá pra atribuir categoria —
                // aparece só no total e na lista de "aguardando pagamento" abaixo.
                Pendente = 0,
            })
            .OrderBy(c => c.Categoria)
            .ToList();

            vm.Pendentes = pendentes
                .OrderBy(p => p.ExpiraEm ?? p.CriadoEm)
                .Select(p => new PagamentoPendenteVM
                {
                    Jogador = p.Jogador.Nome,
                    Celular = p.Jogador.Celular,
                    Categoria = "—",
                    Valor = p.Valor,
                    CriadoEm = p.CriadoEm,
                    ExpiraEm = p.ExpiraEm,
                    LinkCobranca = p.InvoiceUrl,
                })
                .ToList();

            return View(vm);
        }

        // ===================== RELATÓRIO PÓS-TORNEIO =====================

        // Fechamento pra prestar contas ao patrocinador: pódio por categoria, público e
        // financeiro. A tela é feita pra imprimir/salvar em PDF pelo próprio navegador
        // (Ctrl+P), sem depender de biblioteca de PDF no servidor.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Relatorio(int id)
        {
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var torneio = await _context.Torneios
                .Include(t => t.Categorias)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (torneio == null) return NotFound();

            var duplas = await _context.Duplas
                .Include(d => d.Jogador1).Include(d => d.Jogador2).Include(d => d.Categoria)
                .Where(d => d.Categoria.TorneioId == id)
                .ToListAsync();

            var partidas = await _context.Partidas
                .Where(p => p.TorneioId == id)
                .Select(p => new { p.Status })
                .ToListAsync();

            var pagamentos = await _context.Pagamentos
                .Where(p => p.TorneioId == id && p.Status == "Confirmado")
                .ToListAsync();

            string Nomes(Dupla d) => d.Jogador2 != null
                ? $"{d.Jogador1.Nome} / {d.Jogador2.Nome}"
                : d.Jogador1.Nome;

            var jogadores = new HashSet<int>();
            foreach (var d in duplas)
            {
                jogadores.Add(d.Jogador1Id);
                if (d.Jogador2Id != null) jogadores.Add(d.Jogador2Id.Value);
            }

            var vm = new RelatorioTorneioVM
            {
                Torneio = torneio,
                TotalDuplas = duplas.Count,
                TotalJogadores = jogadores.Count,
                TotalCategorias = torneio.Categorias.Count,
                TotalPartidas = partidas.Count,
                PartidasFinalizadas = partidas.Count(p => p.Status == "Finalizada"),
                Arrecadado = pagamentos.Sum(p => p.Valor),
                TaxaPlataforma = pagamentos.Sum(p => p.Comissao),
                JogadoresAlcancados = jogadores.Count,
                Podios = torneio.Categorias.Select(c =>
                {
                    var daCategoria = duplas.Where(d => d.CategoriaId == c.Id).ToList();
                    return new PodioCategoriaVM
                    {
                        Categoria = c.Nome,
                        Duplas = daCategoria.Count,
                        Campea = daCategoria.FirstOrDefault(d => d.UltimaFase == "Campeao") is { } camp ? Nomes(camp) : null,
                        Vice = daCategoria.FirstOrDefault(d => d.UltimaFase == "Final") is { } vice ? Nomes(vice) : null,
                        Semifinalistas = daCategoria.Where(d => d.UltimaFase == "Semifinal").Select(Nomes).ToList(),
                    };
                })
                .OrderBy(p => p.Categoria)
                .ToList(),
            };

            return View(vm);
        }

        // ===================== CHECK-IN NO DIA =====================

        // Lista de presença do torneio: quem já chegou, quem falta. Evita descobrir o
        // W.O. só na hora de chamar o jogo.
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> CheckIn(int id)
        {
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var torneio = await _context.Torneios
                .Include(t => t.Categorias).ThenInclude(c => c.Duplas).ThenInclude(d => d.Jogador1)
                .Include(t => t.Categorias).ThenInclude(c => c.Duplas).ThenInclude(d => d.Jogador2)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (torneio == null) return NotFound();
            return View(torneio);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> MarcarCheckIn(int duplaId, bool presente)
        {
            var dupla = await _context.Duplas
                .Include(d => d.Categoria)
                .FirstOrDefaultAsync(d => d.Id == duplaId);

            if (dupla == null) return NotFound();

            int torneioId = dupla.Categoria.TorneioId;
            if (!await EhOrganizadorAsync(torneioId, ObterJogadorIdLogado() ?? 0)) return Forbid();

            dupla.CheckInEm = presente ? DateTime.Now : null;
            await _context.SaveChangesAsync();

            return RedirectToAction("CheckIn", new { id = torneioId });
        }

        // ===================== COMUNICADO EM MASSA =====================

        // Um clique avisa todo mundo do torneio. É o que hoje o organizador faz na mão,
        // em cinco grupos de WhatsApp diferentes.
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Comunicar(int id, string mensagem, int? categoriaId)
        {
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            if (string.IsNullOrWhiteSpace(mensagem))
            {
                TempData["Erro"] = "Escreva a mensagem antes de enviar.";
                return RedirectToAction("Details", new { id });
            }

            // Todo mundo inscrito: duplas (os dois nomes) + americano.
            var duplas = _context.Duplas.Where(d => d.Categoria.TorneioId == id);
            var americanos = _context.InscricoesAmericanas.Where(i => i.Categoria.TorneioId == id);

            if (categoriaId != null)
            {
                duplas = duplas.Where(d => d.CategoriaId == categoriaId);
                americanos = americanos.Where(i => i.CategoriaId == categoriaId);
            }

            var ids = new HashSet<int>();
            foreach (var d in await duplas.Select(d => new { d.Jogador1Id, d.Jogador2Id }).ToListAsync())
            {
                ids.Add(d.Jogador1Id);
                if (d.Jogador2Id != null) ids.Add(d.Jogador2Id.Value);
            }
            foreach (var jid in await americanos.Select(i => i.JogadorId).ToListAsync()) ids.Add(jid);

            var url = Url.Action("Details", "Torneios", new { id });
            int enviados = 0;

            foreach (var jogadorId in ids)
            {
                try
                {
                    await _pushService.EnviarParaJogadorAsync(jogadorId, torneio.Nome, mensagem.Trim(), url);
                    enviados++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Falha no comunicado do torneio {TorneioId} pro jogador {JogadorId}", id, jogadorId);
                }
            }

            TempData["Sucesso"] = $"Comunicado enviado para {enviados} de {ids.Count} inscrito(s). " +
                                  "Quem não tem o app instalado não recebe push.";
            return RedirectToAction("Details", new { id });
        }

        // Projeta a grade inteira ANTES do sorteio: quantos jogos saem das duplas já
        // inscritas e a que horas o último termina. Cada categoria tem os próprios grupos e
        // o próprio mata-mata, mas todas dividem as mesmas quadras — por isso os jogos se
        // somam antes de virar horário.
        private static PrevisaoGradeVM MontarPrevisaoDaGrade(Torneio torneio)
        {
            int duplas = 0, grupos = 0, jogosDeGrupo = 0, jogosDeMataMata = 0;

            foreach (var categoria in torneio.Categorias)
            {
                // Dupla sem parceiro ainda não é uma dupla: não entra em grupo nenhum.
                int daCategoria = categoria.Duplas.Count(d => d.Jogador2Id != null);
                var (g, jogos) = PrevisaoDoTorneio.FaseDeGrupos(daCategoria);

                duplas += daCategoria;
                grupos += g;
                jogosDeGrupo += jogos;
                jogosDeMataMata += PrevisaoDoTorneio.MataMata(g);
            }

            int total = jogosDeGrupo + jogosDeMataMata;
            var inicio = torneio.AberturaDaGrade;

            var ultimo = PrevisaoDoTorneio.UltimoJogo(
                inicio, torneio.HoraFimDoDia, torneio.HoraInicioDiasSeguintes,
                torneio.QuantidadeQuadras, torneio.TempoPrevistoPartidaMinutos, total);

            var duracao = torneio.TempoPrevistoPartidaMinutos > 0 ? torneio.TempoPrevistoPartidaMinutos : 50;
            var fim = ultimo?.AddMinutes(duracao) ?? inicio;

            return new PrevisaoGradeVM
            {
                Duplas = duplas,
                Grupos = grupos,
                JogosDeGrupo = jogosDeGrupo,
                JogosDeMataMata = jogosDeMataMata,
                TotalDeJogos = total,
                Inicio = inicio,
                FimPrevisto = fim,
                Dias = ultimo == null ? 1 : PrevisaoDoTorneio.DiasOcupados(inicio, ultimo.Value),
                // Comparação por DIA: o limite é "até domingo", não "até domingo às 00h".
                EstouraOPrazo = torneio.DataFim != null && ultimo != null
                                && ultimo.Value.Date > torneio.DataFim.Value.Date
            };
        }

        // TODO jogo do torneio nasce com horário previsto — inclusive os do mata-mata, que
        // só existem depois que a fase de grupos acaba. Sem isso o jogador via "a definir" na
        // fase que mais importa, e a Mesa de Controle não tinha ordem nenhuma pra seguir.
        //
        // A grade do mata-mata emenda no último jogo já marcado do torneio, respeitando as
        // mesmas quadras e o mesmo expediente da fase de grupos.
        private async Task AgendarNaGradeAsync(List<Partida> jogos, int? torneioId)
        {
            if (jogos.Count == 0 || torneioId == null) return;

            var torneio = await _context.Torneios.FindAsync(torneioId.Value);
            if (torneio == null) return;

            var ultimoMarcado = await _context.Partidas
                .Where(p => p.TorneioId == torneioId && p.HorarioPrevisto != null)
                .MaxAsync(p => p.HorarioPrevisto);

            // Vira o dia na abertura dos DIAS SEGUINTES: o mata-mata quase sempre cai no
            // domingo, que começa cedo — não às 18h da sexta em que o torneio abriu.
            var inicio = ultimoMarcado == null
                ? torneio.AberturaDaGrade
                : GradeDeJogos.DepoisDe(ultimoMarcado.Value, torneio.HoraFimDoDia,
                                        torneio.HoraInicioDiasSeguintes, torneio.TempoPrevistoPartidaMinutos);

            var horarios = GradeDeJogos.Horarios(
                inicio, torneio.HoraFimDoDia, torneio.QuantidadeQuadras,
                torneio.TempoPrevistoPartidaMinutos, jogos.Count,
                aberturaDiasSeguintes: torneio.HoraInicioDiasSeguintes).ToList();

            for (int i = 0; i < jogos.Count; i++)
            {
                jogos[i].HorarioPrevisto = horarios[i];
            }
        }

        // ROBÔ DE PROGRESSÃO: Oitavas → Quartas → Semifinal → Final (motor único).
        private async Task ProcessarAvancoMataMataAutomatico(int categoriaId, int? torneioId, string faseConcluida)
        {
            var proximaFase = ChaveamentoMataMata.ProximaFase(faseConcluida);
            if (proximaFase == null) return;

            var vencedores = await _context.Partidas
                .Where(p => p.CategoriaId == categoriaId && p.Fase == faseConcluida && p.Status == "Finalizada")
                .OrderBy(p => p.Id)
                .Select(p => p.VencedorId!.Value)
                .ToListAsync();

            // Só avança com a fase completa, e nunca gera a próxima em duplicidade.
            if (vencedores.Count != ChaveamentoMataMata.JogosDaFase(faseConcluida)) return;
            if (await _context.Partidas.AnyAsync(p => p.CategoriaId == categoriaId && p.Fase == proximaFase)) return;

            var novos = ChaveamentoMataMata.ParearVencedores(vencedores)
                // Codigo é obrigatório no banco (NOT NULL) — sem ele o INSERT do robô falha.
                .Select(confronto => new Partida
                {
                    TorneioId = torneioId,
                    CategoriaId = categoriaId,
                    Fase = proximaFase,
                    Status = "Agendada",
                    Dupla1Id = confronto.Dupla1Id,
                    Dupla2Id = confronto.Dupla2Id,
                    Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper()
                })
                .ToList();

            await AgendarNaGradeAsync(novos, torneioId);

            _context.Partidas.AddRange(novos);
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
                .Where(p => p.CategoriaId == categoriaId
                         && (p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo "))
                         && p.Status == "Finalizada")
                .ToListAsync();

            // Evita gerar a chave duas vezes (ex: dois finalizamentos quase simultâneos).
            bool mataMataJaGerado = await _context.Partidas.AnyAsync(p =>
                p.CategoriaId == categoriaId && !(p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo ")));
            if (mataMataJaGerado) return;

            var grupos = categoria.GruposTorneio.OrderBy(g => g.Nome).ToList();

            // 1. Calcula o ranking final real de cada grupo e monta a lista de classificados.
            var classificados = new List<ChaveamentoMataMata.Classificado>();
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
                for (int pos = 0; pos < ranking.Count && pos < 2; pos++)
                {
                    classificados.Add(new ChaveamentoMataMata.Classificado(
                        ranking[pos].Id, grupo.Nome, ranking[pos].Vitorias, ranking[pos].SaldoGames, pos + 1));
                }
            }

            // 2. Motor único de chaveamento: funciona pra QUALQUER nº de grupos
            //    (todos os 1ºs + melhores 2ºs completando o quadro; 1 grupo = final direta).
            var (nomeFase, confrontos) = ChaveamentoMataMata.MontarPrimeiraFase(classificados);
            if (confrontos.Count == 0) return;

            var jogosDoMataMata = confrontos
                .Select(confronto => new Partida
                {
                    TorneioId = torneioId,
                    CategoriaId = categoriaId,
                    Dupla1Id = confronto.Dupla1Id,
                    Dupla2Id = confronto.Dupla2Id,
                    Status = "Agendada", // Nasce agendada para ir para a Mesa de Controle!
                    Fase = nomeFase,
                    Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper() // NOT NULL no banco
                })
                .ToList();

            // Nasce agendada E com hora: o mata-mata emenda no fim da fase de grupos.
            await AgendarNaGradeAsync(jogosDoMataMata, torneioId);

            _context.Partidas.AddRange(jogosDoMataMata);
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
            // Aceita nulo (dupla ainda sem parceiro) devolvendo 0 — a transmissão não pode
            // quebrar por causa de uma inscrição incompleta.
            int Titulos(int? jogadorId) => jogadorId != null && hist.TryGetValue(jogadorId.Value, out var porTier)
                ? porTier.Values.Sum(v => v.Titulos) : 0;

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
                jogador2NomeD1 = p.Dupla1.Jogador2 != null ? p.Dupla1.Jogador2.Nome : "A definir",
                jogador1IdD2 = p.Dupla2.Jogador1Id,
                jogador1NomeD2 = p.Dupla2.Jogador1.Nome,
                jogador2IdD2 = p.Dupla2.Jogador2Id,
                jogador2NomeD2 = p.Dupla2.Jogador2 != null ? p.Dupla2.Jogador2.Nome : "A definir",
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
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2).ThenInclude(j => j.Time)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1).ThenInclude(j => j.Time)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2).ThenInclude(j => j.Time)
                .Where(p => p.TorneioId == torneioId);

            if (timeFiltroId.HasValue)
            {
                query = query.Where(p =>
                    (p.Dupla1.Jogador1.TimeId == timeFiltroId || p.Dupla1.Jogador2.TimeId == timeFiltroId) ||
                    (p.Dupla2.Jogador1.TimeId == timeFiltroId || p.Dupla2.Jogador2.TimeId == timeFiltroId)
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
            if (torneioDaTela?.Formato == "Americano")
            {
                var finalizadas = partidas.Where(p => p.Status == "Finalizada" && p.Fase.StartsWith("Americano"));

                ViewBag.ClassificacaoAmericano = categoriasDoTorneio.ToDictionary(
                    c => c.Nome,
                    c => TabelaDoAmericano.Montar(finalizadas.Where(p => p.CategoriaId == c.Id)));
            }

            // PALPITRÔMETRO: resumo de votos de cada partida exibida, num único lote.
            int? meuId = User.Identity?.IsAuthenticated == true
                ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
                : null;
            ViewBag.MeuId = meuId;
            ViewBag.Palpites = await _palpites.ObterResumosAsync(partidas.Select(p => p.Id), meuId);
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
            int totalPartidasGeradas = 0;
            int totalDeFora = 0;
            var jogosDoAmericano = new List<Partida>();

            foreach (var categoria in torneio.Categorias)
            {
                var inscritos = await _context.InscricoesAmericanas
                    .Where(i => i.CategoriaId == categoria.Id)
                    .Select(i => i.JogadorId)
                    .ToListAsync();

                int usaveis = RodadasAmericano.Aproveitaveis(inscritos.Count);
                if (usaveis < 4) continue; // categoria sem jogadores suficientes pra fechar um grupo de 4

                var jogadoresEmbaralhados = inscritos.OrderBy(_ => rng.Next()).ToList();
                var jogadoresUsados = jogadoresEmbaralhados.Take(usaveis).ToList();
                totalDeFora += jogadoresEmbaralhados.Count - usaveis;

                // O sorteio vive em Services/RodadasAmericano: método do círculo, que GARANTE
                // cada jogador fazendo dupla com cada um dos outros exatamente uma vez. O
                // código que estava aqui era guloso e olhava só 4 jogadores por vez — medido
                // num ensaio de 8, deixava 4 parcerias repetidas e 4 sem acontecer.
                var rodadasSorteadas = RodadasAmericano.Montar(jogadoresUsados);

                for (int rodada = 1; rodada <= rodadasSorteadas.Count; rodada++)
                {
                    foreach (var confronto in rodadasSorteadas[rodada - 1])
                    {
                        var dupla1 = new Dupla { CategoriaId = categoria.Id, Jogador1Id = confronto.A1, Jogador2Id = confronto.A2 };
                        var dupla2 = new Dupla { CategoriaId = categoria.Id, Jogador1Id = confronto.B1, Jogador2Id = confronto.B2 };
                        _context.Duplas.Add(dupla1);
                        _context.Duplas.Add(dupla2);
                        await _context.SaveChangesAsync(); // precisa dos Ids gerados antes de criar a Partida

                        jogosDoAmericano.Add(new Partida
                        {
                            TorneioId = torneioId,
                            CategoriaId = categoria.Id,
                            Dupla1Id = dupla1.Id,
                            Dupla2Id = dupla2.Id,
                            Fase = $"Americano Rodada {rodada}",
                            Status = "Agendada",
                            Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper()
                        });
                        totalPartidasGeradas++;
                    }
                }
            }

            // O Americano também somava um jogo por vez a partir do início: com 3 quadras,
            // marcava em fila indiana e varava a noite. Mesma grade da fase de grupos.
            var horariosAmericano = GradeDeJogos.Horarios(
                dataHoraInicio, torneio.HoraFimDoDia, torneio.QuantidadeQuadras,
                tempoPartida, jogosDoAmericano.Count,
                aberturaDiasSeguintes: torneio.HoraInicioDiasSeguintes).ToList();

            for (int i = 0; i < jogosDoAmericano.Count; i++)
            {
                jogosDoAmericano[i].HorarioPrevisto = horariosAmericano[i];
            }
            _context.Partidas.AddRange(jogosDoAmericano);

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

            // A conta vive em Services/TabelaDoAmericano — a mesma que alimenta a aba
            // "Classificação" na tela de Jogos. Duas contas separadas divergiriam mais cedo
            // ou mais tarde, e aí o torneio teria dois campeões diferentes na mesma tela.
            var classificacao = TabelaDoAmericano.Montar(partidas)
                .Select(l => new ClassificacaoAmericanoItemVM { Jogador = l.Jogador, TotalGames = l.TotalGames })
                .ToList();

            ViewBag.Torneio = torneio;
            ViewBag.CategoriaId = categoriaId;
            return View(classificacao);
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
                .Where(p => p.TorneioId == id && p.CategoriaId == categoriaId && p.Status == "Finalizada"
                         && (p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo ")))
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