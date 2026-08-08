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
        // Sem IEmailService de propósito: e-mail daqui sai pela FilaDeAvisos, junto do push
        // (EnviarParaJogadorAsync enfileira os dois canais). SMTP dentro da requisição foi o
        // "Publicando o torneio…" de minutos de 07/08/2026 — não voltar.
        private readonly IPushNotificationService _pushService;
        private readonly IPagamentoInscricaoService _pagamentos;
        private readonly TaxasExibicao _taxas;
        private readonly RegistroResultadosSettings _registro;
        private readonly ILogger<TorneiosController> _logger;
        private readonly IPadelimetroService _padelimetro;
        private readonly EncerramentoDaPartida _encerramento;

        // Injeta o banco de dados
        public TorneiosController(DbPadelContext context, IEstatisticasService estatisticas, IPalpiteService palpites,
            IWebHostEnvironment env, IPushNotificationService pushService,
            IPagamentoInscricaoService pagamentos, Microsoft.Extensions.Options.IOptions<TaxasExibicao> taxas,
            Microsoft.Extensions.Options.IOptions<RegistroResultadosSettings> registro,
            ILogger<TorneiosController> logger, IPadelimetroService padelimetro,
            EncerramentoDaPartida encerramento)
        {
            _context = context;
            _estatisticas = estatisticas;
            _palpites = palpites;
            _env = env;
            _pushService = pushService;
            _pagamentos = pagamentos;
            _taxas = taxas.Value;
            _registro = registro.Value;
            _logger = logger;
            _padelimetro = padelimetro;
            _encerramento = encerramento;
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

            // Só ENFILEIRA: a FilaDeAvisos entrega e-mail e push por fora da requisição. O
            // e-mail inline que morava aqui saía em dobro (a fila já cobre o canal) e segurava
            // a tela de quem estava se inscrevendo.
            foreach (var grupo in seguidores.GroupBy(s => s.SeguidorId))
            {
                var seguidor = grupo.First().Seguidor;
                var nomesQueSigo = grupo.Select(s => jogadores.TryGetValue(s.SeguidoId, out var nome) ? nome : "").Where(n => n != "");
                var titulo = "Alguém que você segue se inscreveu num torneio";
                var corpo = $"{string.Join(" e ", nomesQueSigo)} se inscreveu em {torneio.Nome}.";

                await _pushService.EnviarParaJogadorAsync(seguidor.Id, titulo, corpo, url);
            }
        }

        // Salva a capa do torneio (redimensionada e recodificada) e devolve o caminho relativo
        // pra gravar no banco, ou null se a imagem não pôde ser processada.
        private Task<ResultadoDaImagem> SalvarCapaAsync(IFormFile arquivo) =>
            ImagemEnviada.SalvarAsync(arquivo, _env.WebRootPath, "capas-torneio", FormatoDeImagem.CapaTorneio, _logger);

        // Quem manda neste torneio: o organizador (criador ou adicionado) — e o admin do
        // Padelizou, em QUALQUER torneio.
        //
        // O admin precisa disso pra socorrer organizador travado: no dia do torneio, com as
        // quadras ocupadas, o problema é sempre urgente, e "me adiciona como organizador"
        // depende justamente da pessoa que não está conseguindo mexer no sistema. Antes o
        // único caminho era ir no banco na mão.
        //
        // A checagem é sobre o jogadorId RECEBIDO, não sobre o claim de quem chamou: esta
        // função também responde "fulano já manda aqui?" no AdicionarOrganizador, e ler o
        // claim faria a resposta ser sobre outra pessoa.
        private async Task<bool> EhOrganizadorAsync(int torneioId, int jogadorId)
        {
            if (jogadorId <= 0) return false;

            if (await _context.TorneioOrganizadores
                    .AnyAsync(o => o.TorneioId == torneioId && o.JogadorId == jogadorId))
                return true;

            return await _context.Jogadores
                .AnyAsync(j => j.Id == jogadorId && (j.IsAdminRaiz || j.IsAdminGeral));
        }

        // Organizar junto não é ver o caixa. Quem CRIOU o torneio (e o admin da plataforma, que
        // precisa disso pra dar suporte) vê dinheiro; quem foi adicionado pra ajudar, não —
        // ver o dinheiro é o único poder que não vem junto (ver AcessoAoDinheiroDoTorneio).
        private async Task<bool> PodeVerDinheiroAsync(int torneioId, int jogadorId)
        {
            if (jogadorId <= 0) return false;

            var nivel = await _context.TorneioOrganizadores
                .Where(o => o.TorneioId == torneioId && o.JogadorId == jogadorId)
                .Select(o => o.NivelAcesso)
                .FirstOrDefaultAsync();

            var ehAdmin = await _context.Jogadores
                .AnyAsync(j => j.Id == jogadorId && (j.IsAdminRaiz || j.IsAdminGeral));

            return AcessoAoDinheiroDoTorneio.PodeVer(nivel, ehAdmin);
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
            var torneios = await _context.Torneios.ToListAsync();

            // Torneio OCULTO e torneio AINDA NÃO APROVADO somem da listagem pública — os dois
            // continuam visíveis aqui pra quem organiza (pra não "perder" o próprio torneio de
            // vista) e pro admin. A regra de vitrine mora em Services/PermissaoDeOrganizador.
            var jogadorId = ObterJogadorIdLogado();
            var meusTorneioIds = jogadorId.HasValue
                ? (await _context.TorneioOrganizadores.Where(o => o.JogadorId == jogadorId.Value).Select(o => o.TorneioId).ToListAsync()).ToHashSet()
                : new HashSet<int>();

            // Admin do Padelizou vê os ocultos também: se ele manda em qualquer torneio, não
            // faz sentido ter que adivinhar o link de um que não aparece na lista.
            bool souAdmin = User.FindFirstValue("IsAdmin") == "true";
            torneios = torneios
                .Where(t => PermissaoDeOrganizador.ApareceNaVitrine(t) || souAdmin || meusTorneioIds.Contains(t.Id))
                .ToList();

            // Cancelado some da lista pela MESMA porta do oculto: quem organiza continua vendo
            // (é dele o torneio, e é lá que ele vê quem tem que ser reembolsado), o resto não.
            torneios = torneios
                .Where(t => !CancelamentoDoTorneio.EstaCancelado(t.Status)
                            || souAdmin || meusTorneioIds.Contains(t.Id))
                .ToList();

            // A ORDEM É POR DATA, e ela troca de sentido conforme a seção: o que ainda vai
            // acontecer se lê do mais próximo pro mais distante ("é esse fim de semana?"), e o
            // que já passou se lê do mais recente pro mais antigo. Torneio SEM data marcada cai
            // pro fim dos dois — no decrescente que existia antes, o nulo encabeçava a lista.
            static List<Torneio> DoMaisProximo(IEnumerable<Torneio> lista) =>
                lista.OrderBy(t => t.DataInicio == null).ThenBy(t => t.DataInicio).ToList();
            static List<Torneio> DoMaisRecente(IEnumerable<Torneio> lista) =>
                lista.OrderBy(t => t.DataInicio == null).ThenByDescending(t => t.DataInicio).ToList();

            // ── "Em breve": torneio que ainda NÃO abriu inscrição ──────────────────────
            // Ele não é "aberto" (ninguém consegue entrar) nem "em andamento" (não começou
            // nada), e sem seção própria caía no meio dos que já estão rolando — anunciando
            // como acontecendo um torneio que o organizador ainda está montando.
            //
            // ⚠️ Duas consultas de CONJUNTO, não uma por torneio: a lista já está inteira em
            // memória, e perguntar "tem inscrito?" card a card seria uma ida ao banco por
            // card numa página que qualquer visitante abre.
            var idsNaTela = torneios.Select(t => t.Id).ToList();

            var comInscrito = (await _context.Duplas
                    .Where(d => idsNaTela.Contains(d.Categoria.TorneioId) && d.NomeTime == null)
                    .Select(d => d.Categoria.TorneioId)
                    .Distinct()
                    .ToListAsync())
                .Concat(await _context.InscricoesAmericanas
                    .Where(i => idsNaTela.Contains(i.Categoria.TorneioId))
                    .Select(i => i.Categoria.TorneioId)
                    .Distinct()
                    .ToListAsync())
                .ToHashSet();

            var comPartida = (await _context.Partidas
                    .Where(p => p.TorneioId != null && idsNaTela.Contains(p.TorneioId.Value))
                    .Select(p => p.TorneioId!.Value)
                    .Distinct()
                    .ToListAsync())
                .ToHashSet();

            bool AindaVaiAbrir(Torneio t) =>
                PortaDaInscricao.NuncaAbriu(t, comInscrito.Contains(t.Id), comPartida.Contains(t.Id));

            ViewBag.Abertos = DoMaisProximo(torneios.Where(t => t.Status == "Inscrições Abertas"));
            ViewBag.EmBreve = DoMaisProximo(torneios.Where(AindaVaiAbrir));
            ViewBag.EmAndamento = DoMaisProximo(torneios
                .Where(t => t.Status != "Inscrições Abertas" && t.Status != "Finalizado"
                            && !CancelamentoDoTorneio.EstaCancelado(t.Status)
                            && !AindaVaiAbrir(t)));
            ViewBag.Finalizados = DoMaisRecente(torneios.Where(t => t.Status == "Finalizado"));
            // Bloco próprio: cancelado no meio de "em andamento" faria o organizador achar que
            // o torneio ainda está de pé.
            ViewBag.Cancelados = DoMaisRecente(torneios.Where(t => CancelamentoDoTorneio.EstaCancelado(t.Status)));

            // O convite pra pedir a liberação do torneio Oficial mora AQUI, e não só na tela
            // de criação: esta é a página que a pessoa abre pra ver torneio dos outros, e é
            // olhando ela que dá vontade de fazer o próprio. Na criação o convite chega tarde
            // — ela já entrou querendo criar.
            var euMesmo = jogadorId.HasValue ? await _context.Jogadores.FindAsync(jogadorId.Value) : null;
            ViewBag.EstadoDoPedido = PermissaoDeOrganizador.EstadoDe(euMesmo);
            ViewBag.EstouLogado = euMesmo != null;

            return View();
        }

        // Exemplo de como deve ficar o seu método Details
        [HttpGet]
        public async Task<IActionResult> Details(int id, int? timeFiltroId, int[]? categoriaFiltroIds, bool soMeusJogos = false)
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
                // O escudo da dupla-TIME (categoria de times) vem do vínculo dela mesma.
                .Include(t => t.Categorias)
                    .ThenInclude(c => c.Duplas)
                        .ThenInclude(d => d.Time)
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
            // A previsão monta grupos + mata-mata, então só vale no formato padrão — os dois
            // Americanos têm a própria conta de rodadas.
            if (torneio.Status == "Chaves em Sorteio" && torneio.Formato == "Padrao")
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
                var inscricoesAmericanas = await _context.InscricoesAmericanas
                    .Include(i => i.Jogador)
                    .Where(i => i.Categoria.TorneioId == id)
                    .ToListAsync();
                ViewBag.InscricoesAmericanas = inscricoesAmericanas;

                // As divisões em grupos que o organizador pode escolher no sorteio. Calculadas
                // aqui e não na view: a view não decide regra, e o Razor não deixa declarar
                // variável no meio de um bloco de código sem virar erro de compilação.
                //
                // Só faz sentido com UMA categoria — que é o caso normal do Americano. Com
                // várias, cada uma teria a sua divisão e a pergunta não caberia numa opção só;
                // aí o sorteio escolhe a que mais mistura em cada.
                var categoriasComGente = inscricoesAmericanas
                    .Where(i => !i.EmListaDeEspera)
                    .GroupBy(i => i.CategoriaId)
                    .ToList();

                if (categoriasComGente.Count == 1)
                {
                    int quantos = categoriasComGente[0].Count();
                    ViewBag.InscritosDoAmericano = quantos;
                    ViewBag.DivisoesDoAmericano = DivisaoDoAmericano.Possiveis(quantos);
                    ViewBag.PorQueNaoFechaOAmericano = DivisaoDoAmericano.Aceita(quantos)
                        ? null
                        : DivisaoDoAmericano.PorQueNaoFecha(quantos);
                }
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
                    torneio.ModoComissao, CobrancaDoTorneio.PercentualExibicao(torneio, _taxas))
                : null;
            ViewBag.PrecoTotal = exibicao?.Total;
            ViewBag.TaxaServico = exibicao?.Taxa;

            ViewBag.PodeGerenciar = jogadorLogadoId.HasValue && await EhOrganizadorAsync(id, jogadorLogadoId.Value);

            // Organiza junto, mas o caixa é de quem criou (ver AcessoAoDinheiroDoTorneio).
            ViewBag.PodeVerDinheiro = jogadorLogadoId.HasValue
                && await PodeVerDinheiroAsync(id, jogadorLogadoId.Value);

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

                // Trocar a forma de recebimento depois de criado: dá enquanto ninguém se
                // inscreveu. A MESMA pergunta que o POST do Editar faz — se as duas
                // divergirem, a tela oferece o que o servidor recusa.
                bool temInscrito = await TemAlguemInscritoAsync(id);
                ViewBag.PodeTrocarFormaPagamento = FormaDePagamentoDoTorneio.PodeTrocar(temInscrito);

                // Abrir/fechar inscrição é um interruptor, e não mais um caminho só de ida.
                // "Já sorteou" é PARTIDA existindo — é ela que a jogadora vê e que diz contra
                // quem joga; o nome da fase não serve de prova.
                bool jaSorteou = await _context.Partidas.AnyAsync(p => p.TorneioId == id);
                ViewBag.PodeReabrirInscricoes =
                    PortaDaInscricao.PorQueNaoPodeAbrir(torneio, jaSorteou) == null;
                ViewBag.InscricoesNuncaAbriram =
                    PortaDaInscricao.NuncaAbriu(torneio, temInscrito, jaSorteou);

                // Sem conta conectada as opções "pelo site" ficam à vista mas travadas, como
                // na criação: escondê-las faria o organizador concluir que não existem.
                ViewBag.RecebimentoConectado = _pagamentos.PodeReceberOnline(recebedorTorneio);
                // Pra poder ACRESCENTAR categoria depois de publicado: o organizador que
                // esqueceu a Mista, ou que abriu mais uma quadra, resolvia isso criando outro
                // torneio.
                ViewBag.CatalogoCategorias = await _context.CategoriasPadrao.Ativas().OrderBy(c => c.Id).ToListAsync();

                // Quem o Ranking RS barrou. Vem SEMPRE que o organizador olha a tela, mesmo com
                // a validação já desligada depois: as linhas antigas continuam valendo (é o que
                // segura quem foi "Mantido"), e sumir com elas esconderia decisão tomada.
                // Pendente primeiro — é o que pede ação dele.
                ViewBag.BloqueiosDoRanking = await _context.BloqueiosDoRanking
                    .Include(b => b.Categoria)
                    .Include(b => b.DecididoPor)
                    .Where(b => b.TorneioId == id)
                    .OrderBy(b => b.Situacao == SituacaoDoBloqueio.Pendente ? 0 : 1)
                    .ThenByDescending(b => b.CriadoEm)
                    .ToListAsync();

                // Torneio cancelado: QUEM PAGOU e quanto. O sistema não estorna sozinho
                // (decisão do Felipe) — então ele precisa entregar a lista pronta, senão
                // devolver o dinheiro vira garimpo na mão de quem acabou de cancelar um
                // torneio, que já é o pior dia do organizador.
                if (CancelamentoDoTorneio.EstaCancelado(torneio.Status))
                {
                    ViewBag.PagosParaDevolver = await _context.Duplas
                        .Where(d => d.Categoria.TorneioId == id && d.Pago && d.NomeTime == null)
                        .Select(d => new DevolucaoPendenteVM
                        {
                            Quem = d.Jogador2Id == null
                                ? d.Jogador1.Nome
                                : d.Jogador1.Nome + " e " + d.Jogador2!.Nome,
                            Categoria = d.Categoria.Nome,
                            Contato = d.Jogador1.Celular,
                            // Dupla paga pelos DOIS: o preço do torneio é por pessoa.
                            Valor = torneio.PrecoInscricao * (d.Jogador2Id == null ? 1 : 2),
                            PagoEm = d.PagoEm,
                        })
                        .ToListAsync();

                    var americanosPagos = await _context.InscricoesAmericanas
                        .Where(i => i.Categoria.TorneioId == id && i.Pago)
                        .Select(i => new DevolucaoPendenteVM
                        {
                            Quem = i.Jogador.Nome,
                            Categoria = i.Categoria.Nome,
                            Contato = i.Jogador.Celular,
                            Valor = torneio.PrecoInscricao,
                            PagoEm = i.PagoEm,
                        })
                        .ToListAsync();

                    ((List<DevolucaoPendenteVM>)ViewBag.PagosParaDevolver).AddRange(americanosPagos);
                }
            }

            // Aba "Jogos" embutida (Ao Vivo/Agendadas/Finalizadas) — só depois que as inscrições fecham.
            if (torneio.Status != "Inscrições Abertas")
            {
                await CarregarViewBagJogosAsync(id, timeFiltroId, categoriaFiltroIds, soMeusJogos);
                // Pontos por time neste torneio (só faz sentido depois que começa a valer resultado).
                ViewBag.PontosTimes = await _estatisticas.ObterPontosTimesNoTorneioAsync(id);

                // Chaveamento do mata-mata: as partidas de fase eliminatória por categoria, pra
                // a view desenhar o bracket com os confrontos REAIS (antes era um desenho fixo).
                // "Primeira Rodada" é a abertura da CHAVE DIRETA (quadro de 32 com bye) — sem
                // ela na lista, a rodada existe no banco e não aparece no desenho da chave.
                var fasesMataMata = new[] { ChaveamentoMataMata.PrimeiraRodada,
                                            "Oitavas de Final", "Quartas de Final", "Semifinal", "Final" };
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

                // Quem passou DIRETO (bye) em cada categoria: sem isto essas duplas somem do
                // desenho da chave — jogam a fase seguinte sem aparecer em vaga nenhuma. A
                // ORDEM da lista é a do pareamento (melhor → pior), e o desenho conta com ela.
                var byesPorCategoria = new Dictionary<int, List<Dupla>>();
                foreach (var categoriaId in ((Dictionary<int, List<Partida>>)ViewBag.MataMataPorCategoria).Keys)
                {
                    var byeIds = await AvancoDaChave.ByesDaCategoriaAsync(_context, categoriaId);
                    if (byeIds.Count == 0) continue;

                    var duplasDeBye = await _context.Duplas
                        .Include(d => d.Jogador1).Include(d => d.Jogador2)
                        .Where(d => byeIds.Contains(d.Id))
                        .ToListAsync();
                    byesPorCategoria[categoriaId] = byeIds
                        .Select(id => duplasDeBye.First(d => d.Id == id))
                        .ToList();
                }
                ViewBag.ByesPorCategoria = byesPorCategoria;

                // Os jogos de GRUPO, pra tabela de cada grupo mostrar os resultados ao lado
                // da classificação. Sem isso a tabela dizia "V 0 · D 0 · SG 0" sem nenhuma
                // pista de quais jogos faltavam — e o placar já estava no sistema, só não
                // aparecia onde a pessoa estava olhando.
                ViewBag.JogosDeGrupo = await _context.Partidas
                    .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
                    .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
                    .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
                    .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
                    .Where(p => p.TorneioId == id
                             && (p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo ")))
                    .OrderBy(p => p.HorarioPrevisto).ThenBy(p => p.Id)
                    .ToListAsync();
            }

            return View(torneio);
        }
        public async Task<IActionResult> Jogos(int id, int? timeFiltroId, int[]? categoriaFiltroIds, bool soMeusJogos = false)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            await CarregarViewBagJogosAsync(id, timeFiltroId, categoriaFiltroIds, soMeusJogos);
            ViewBag.Torneio = torneio;

            return View();
        }

        // Semifinal e Final não existiam pra quem olhava a tela: o motor só cria a rodada
        // quando a anterior fecha, então o jogador via a primeira rodada e mais nada — sem
        // saber a que horas voltar nem contra quem pode jogar. A regra mora em
        // Services/ProximasFasesDaChave; aqui só se junta o que ela precisa.
        private async Task<List<ProximasFasesDaChave.JogoQueVem>> ProjetarProximasFasesAsync(
            int torneioId, List<Partida> partidas)
        {
            var torneio = await _context.Torneios.FindAsync(torneioId);
            if (torneio == null) return new();

            var deMataMata = partidas.Where(p => ChaveamentoMataMata.EhFaseDeMataMata(p.Fase)).ToList();
            var cadeias = new List<ProximasFasesDaChave.CadeiaDeFases>();

            // Categoria AINDA NA FASE DE GRUPOS: não há jogo de mata-mata nenhum de onde
            // partir, então a projeção começa nas COLOCAÇÕES ("1º do Grupo A × 2º do Grupo
            // C"). Sem isto, só a chave direta mostrava o caminho até a final — ela já nasce
            // com a primeira rodada criada, e as categorias de grupo apareciam sem mata-mata.
            var comMataMata = deMataMata.Select(p => p.CategoriaId).ToHashSet();
            var aindaEmGrupos = await _context.Categorias
                .Include(c => c.GruposTorneio)
                .Where(c => c.TorneioId == torneioId && !c.ChaveDireta && !comMataMata.Contains(c.Id))
                .ToListAsync();

            // O mata-mata de uma categoria emenda no fim dos grupos DELA, não no fim dos
            // grupos do torneio — é o mesmo lugar de onde o robô o agenda (ver
            // AgendarNaGradeAsync). A categoria que fecha os grupos às 20h55 não tem por que
            // esperar a que só fecha às 21h39: são pessoas diferentes e as quadras estão
            // livres. Enquanto a conta era do torneio inteiro, a previsão empurrava TODAS as
            // chaves pro fim e fazia o torneio parecer uma hora mais longo do que é.
            var fimDosGruposPorCategoria = partidas
                .Where(p => FasesTorneio.EhFaseDeGrupos(p.Fase) && p.HorarioPrevisto != null)
                .GroupBy(p => p.CategoriaId)
                .ToDictionary(g => g.Key, g => g.Max(p => p.HorarioPrevisto!.Value));

            foreach (var categoria in aindaEmGrupos.Where(c => c.GruposTorneio.Count > 0))
            {
                DateTime? fimDosGrupos =
                    !torneio.SemHorarioPrevisto && fimDosGruposPorCategoria.TryGetValue(categoria.Id, out var fim)
                        ? fim : null;

                cadeias.Add(ProximasFasesDaChave.MontarDosGrupos(
                    categoria.GruposTorneio.Select(g => g.Nome).OrderBy(n => n).ToList(),
                    Math.Max(1, categoria.ClassificadosPorGrupo ?? 2),
                    fimDosGrupos,
                    categoria.Nome));
            }

            // Categoria por categoria: cada uma tem a própria chave, e misturá-las cruzaria
            // duplas que nunca vão se enfrentar.
            foreach (var porCategoria in deMataMata.GroupBy(p => p.CategoriaId))
            {
                var byeIds = await AvancoDaChave.ByesDaCategoriaAsync(_context, porCategoria.Key);
                var nomePorDupla = porCategoria
                    .SelectMany(p => new[] { p.Dupla1, p.Dupla2 })
                    .DistinctBy(d => d.Id)
                    .ToDictionary(d => d.Id, d => d.NomeDeExibicao);

                var byes = byeIds
                    .Select(id => nomePorDupla.TryGetValue(id, out var nome) ? nome : null)
                    .Where(n => n != null)
                    .ToList()!;

                cadeias.Add(ProximasFasesDaChave.Montar(
                    porCategoria.Select(p => new ProximasFasesDaChave.PartidaDaChave(
                        p.Id, p.Fase, p.Dupla1.NomeDeExibicao, p.Dupla2.NomeDeExibicao,
                        // Torneio "por ordem de liberação" não marca hora: sem horário a
                        // projeção ainda diz QUEM joga, que é metade do pedido.
                        torneio.SemHorarioPrevisto ? null : p.HorarioPrevisto)).ToList(),
                    byes!,
                    porCategoria.First().Categoria.Nome));
            }

            if (cadeias.Count == 0) return new();

            // As quadras são do TORNEIO, não da categoria: só dá pra dizer em qual quadra um
            // jogo projetado cai depois de pôr todas as categorias na mesma grade, junto com
            // o que já está marcado de verdade. Sem isso a tela prometia oito jogos no mesmo
            // minuto num torneio de cinco quadras — e nenhum deles com quadra, porque não
            // havia como saber qual.
            var ocupadas = partidas
                .Where(p => p.HorarioPrevisto != null && !torneio.SemHorarioPrevisto)
                .Select(p => new ProximasFasesDaChave.VagaOcupada(p.HorarioPrevisto!.Value, p.NomeQuadra))
                .ToList();

            var projetados = ProximasFasesDaChave.Agendar(
                cadeias,
                new ProximasFasesDaChave.ConfiguracaoDaGrade(
                    torneio.TempoPrevistoPartidaMinutos,
                    torneio.QuantidadeQuadras,
                    await QuadrasEmUsoAsync(torneioId),
                    torneio.HoraFimDoDia,
                    torneio.HoraInicioDiasSeguintes),
                ocupadas);

            return projetados.OrderBy(j => j.Horario ?? DateTime.MaxValue).ToList();
        }

        // O jogador está NESTA dupla? Usado pra saber se ele venceu ou perdeu um jogo já
        // encerrado — perder é o que corta a corrente da projeção.
        private static bool EstaNaDupla(Partida p, int duplaId, int jogadorId)
        {
            var dupla = p.Dupla1Id == duplaId ? p.Dupla1 : p.Dupla2Id == duplaId ? p.Dupla2 : null;
            return dupla != null && (dupla.Jogador1Id == jogadorId || dupla.Jogador2Id == jogadorId);
        }

        // O jogador está em quadra neste jogo? Vale pras duas duplas e pros dois lugares da
        // dupla. Categoria de TIMES fica de fora naturalmente: lá o Jogador1Id é o organizador
        // do time, não quem joga.
        private static bool EstouNesteJogo(Partida p, int jogadorId) =>
            p.Dupla1.Jogador1Id == jogadorId || p.Dupla1.Jogador2Id == jogadorId ||
            p.Dupla2.Jogador1Id == jogadorId || p.Dupla2.Jogador2Id == jogadorId;

        // Os jogos que AINDA NÃO EXISTEM e que podem ser dele. A regra (seguir a corrente das
        // procedências, parando em quem perdeu) mora em Services/MeusJogos; aqui só se traduz
        // o dado do banco pro que ela pede.
        private static List<ProximasFasesDaChave.JogoQueVem> RecortarProjecaoDoJogador(
            List<ProximasFasesDaChave.JogoQueVem> projetados, List<Partida> todas, int jogadorId)
        {
            // A ORDEM DENTRO DA FASE precisa ser a mesma que a projeção usou pra numerar
            // ("Vencedor Quartas de Final 1"), que é a mesma do avanço de verdade: por Id.
            var reais = todas
                .Where(p => ChaveamentoMataMata.EhFaseDeMataMata(p.Fase))
                .GroupBy(p => new { p.CategoriaId, p.Fase })
                .SelectMany(g => g.OrderBy(p => p.Id).Select((p, i) => new MeusJogos.JogoReal(
                    p.Categoria?.Nome ?? "", p.Fase, i + 1,
                    EstouNesteJogo(p, jogadorId),
                    p.Status == "Finalizada" && p.VencedorId != null && !EstaNaDupla(p, p.VencedorId.Value, jogadorId))))
                .ToList();

            // Quem folgou a primeira rodada aparece na projeção pelo NOME, não por
            // procedência — é o único lado sem jogo de origem que pode ser dele.
            var minhasDuplas = todas
                .SelectMany(p => new[] { p.Dupla1, p.Dupla2 })
                .Concat(todas.SelectMany(p => new[] { p.Dupla1, p.Dupla2 }))
                .DistinctBy(d => d.Id)
                .Where(d => d.Jogador1Id == jogadorId || d.Jogador2Id == jogadorId)
                .Select(d => d.NomeDeExibicao)
                .ToHashSet();

            // Categoria dele que ainda está na fase de grupos: a projeção fala em "1º do Grupo
            // A" e ninguém sabe quem vai ser o 1º. Mostrar a chave inteira da categoria é o
            // honesto — é o caminho que ele PODE percorrer.
            var minhasCategorias = todas
                .Where(p => EstouNesteJogo(p, jogadorId))
                .Select(p => p.CategoriaId)
                .ToHashSet();

            var categoriasEmGrupos = todas
                .Where(p => minhasCategorias.Contains(p.CategoriaId))
                .GroupBy(p => p.CategoriaId)
                .Where(g => !g.Any(p => ChaveamentoMataMata.EhFaseDeMataMata(p.Fase)))
                .Select(g => g.First().Categoria?.Nome ?? "")
                .Where(nome => nome != "")
                .ToHashSet();

            return MeusJogos.Filtrar(projetados, reais, minhasDuplas, categoriasEmGrupos);
        }

        // Compartilhado entre Jogos() (página dedicada, usada como destino do "Editar Jogo")
        // e Details() (aba "Jogos" embutida na página do torneio) — mesma lógica de filtro/abas
        // pros dois lugares não divergirem.
        private async Task CarregarViewBagJogosAsync(int torneioId, int? timeFiltroId, int[]? categoriaFiltroIds, bool soMeusJogos = false)
        {
            var query = _context.Partidas
                .Include(p => p.Categoria)
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1).ThenInclude(j => j.Time)
                // `Jogador2!`: quem se inscreveu sozinho ainda não tem parceiro. O EF lê a
                // expressão pra montar o JOIN, não executa o acesso.
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2!).ThenInclude(j => j.Time)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1).ThenInclude(j => j.Time)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2!).ThenInclude(j => j.Time)
                // O escudo da dupla-TIME (categoria de times) vem do vínculo dela mesma.
                .Include(p => p.Dupla1).ThenInclude(d => d.Time)
                .Include(p => p.Dupla2).ThenInclude(d => d.Time)
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

            // "MEUS JOGOS": a lista inteira de um torneio de 86 jogos não serve pra quem só
            // quer saber a que horas ele joga. Precisa vir ANTES da projeção — os jogos que
            // ainda não existem saem dos que existem, e a corrente parte dos jogos DELE.
            var meuJogadorId = ObterJogadorIdLogado();
            var todasAsPartidas = partidas;
            ViewBag.TenhoJogoAqui = meuJogadorId != null && partidas.Any(p => EstouNesteJogo(p, meuJogadorId.Value));
            ViewBag.SoMeusJogos = soMeusJogos && ViewBag.TenhoJogoAqui;

            if (ViewBag.SoMeusJogos)
                partidas = partidas.Where(p => EstouNesteJogo(p, meuJogadorId!.Value)).ToList();

            ViewBag.AoVivo = partidas.Where(p => p.Status == "AoVivo").OrderBy(p => p.HorarioInicioReal).ToList();
            // Placar lançado depois (sem HorarioFimReal) cai pro horário previsto em vez
            // de flutuar em ordem arbitrária no meio da lista.
            ViewBag.Finalizadas = partidas.Where(p => p.Status == "Finalizada")
                .OrderByDescending(p => p.HorarioFimReal ?? p.HorarioPrevisto).ThenByDescending(p => p.Id).ToList();
            ViewBag.Agendadas = partidas.Where(p => p.Status == "Agendada").OrderBy(p => p.HorarioPrevisto).ToList();

            // ⚠️ A projeção parte do torneio INTEIRO, não da lista filtrada: a chave de uma
            // categoria só se desenha com todos os jogos dela na mão, e filtrar antes deixaria
            // a projeção montando um quadro que não existe. O recorte vem depois.
            var projetados = await ProjetarProximasFasesAsync(torneioId, todasAsPartidas);

            ViewBag.JogosQueVem = ViewBag.SoMeusJogos
                ? RecortarProjecaoDoJogador(projetados, todasAsPartidas, meuJogadorId!.Value)
                : projetados;

            // Só times que de fato jogam ESTE torneio — a lista vinha com todos os times
            // do sistema, e a tela esconde o filtro quando ela é vazia.
            var timesDoTorneio = await _context.Times
                .Where(t => _context.Partidas.Any(p => p.TorneioId == torneioId &&
                    (p.Dupla1.Jogador1.TimeId == t.Id || p.Dupla1.Jogador2!.TimeId == t.Id ||
                     p.Dupla2.Jogador1.TimeId == t.Id || p.Dupla2.Jogador2!.TimeId == t.Id)))
                .OrderBy(t => t.Nome)
                .ToListAsync();
            ViewBag.Times = new SelectList(timesDoTorneio, "Id", "Nome", timeFiltroId);
            ViewBag.TimeAtual = timeFiltroId;
            var categoriasDoTorneio = await _context.Categorias.Where(c => c.TorneioId == torneioId).OrderBy(c => c.Nome).ToListAsync();
            ViewBag.CategoriasDoTorneio = categoriasDoTorneio;
            ViewBag.CategoriaFiltroAtual = categoriaFiltroIds ?? Array.Empty<int>();

            // As quadras que o torneio usa — é a lista do "mudar de quadra". Vem dos JOGOS e
            // só na falta deles do cadastro, pelo mesmo motivo de sempre: renomear o cadastro
            // depois do sorteio não reescreve os jogos (ver QuadrasEmUsoAsync).
            ViewBag.QuadrasDoTorneio = await QuadrasEmUsoAsync(torneioId);

            // No Americano a classificação É o placar do torneio — quem somou mais games.
            // Ela morava numa página separada, sem botão de voltar; agora é uma aba aqui,
            // ao lado de Ao Vivo, que é onde o pessoal já está olhando.
            var torneioDaTela = await _context.Torneios.FindAsync(torneioId);

            // Quem organiza vê os botões de mexer no jogo ("colocar no ar", editar placar).
            // Fica FORA do if do Americano de propósito: nasceu lá dentro, quando só o
            // desempate precisava dele, e o resultado era que num torneio de duplas — a
            // maioria — a flag nem existia, e o organizador não via botão nenhum.
            ViewBag.EhOrganizador = await EhOrganizadorAsync(torneioId, ObterJogadorIdLogado() ?? 0);

            // Torneio por ordem de liberação não tem horário pra recalcular — o servidor já
            // recusava, mas só DEPOIS do clique, e a recusa voltava como faixa vermelha em cima
            // da tela. Oferecer o botão e negá-lo em seguida é fazer o organizador descobrir a
            // regra pelo erro.
            ViewBag.SemHorarioPrevisto = torneioDaTela?.SemHorarioPrevisto == true;

            if (torneioDaTela?.Formato == FormatoDoTorneio.Americano)
            {
                // ⚠️ Sai da montagem compartilhada, e NÃO da lista `partidas` desta tela: ela
                // já veio filtrada (por time, por categoria, por "só meus jogos"), e a
                // classificação calculada em cima de um recorte é a classificação de outro
                // torneio. Além disso a conta é POR GRUPO — ver ClassificacaoDoAmericano.
                ViewBag.ClassificacaoAmericano = await MontarClassificacaoAmericanaAsync(torneioId);

                // O botão "montar o desempate" é só do organizador; o jogador vê o aviso.
                ViewBag.DesempateAmericano = torneioDaTela.DesempateAmericano;
            }
            else if (torneioDaTela?.Formato == "AmericanoDuplas")
            {
                // No Americano de DUPLAS a conta é por dupla — a dupla é fixa, então quem
                // soma games é ela (Services/TabelaDoAmericanoDeDuplas).
                var finalizadas = partidas.Where(p => p.Status == "Finalizada" && p.Fase.StartsWith("Americano"));

                ViewBag.ClassificacaoAmericanoDuplas = categoriasDoTorneio.ToDictionary(
                    c => c.Nome,
                    c => TabelaDoAmericanoDeDuplas.Montar(finalizadas.Where(p => p.CategoriaId == c.Id)));

                ViewBag.DesempateAmericano = torneioDaTela.DesempateAmericano;
            }

            // PALPITRÔMETRO: resumo de votos de cada partida exibida, num único lote.
            int? meuId = User.Identity?.IsAuthenticated == true
                ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
                : null;
            ViewBag.MeuId = meuId;
            ViewBag.Palpites = await _palpites.ObterResumosAsync(partidas.Select(p => p.Id), meuId);

            // O NÚMERO de cada jogo dentro da fase dele ("Quartas de Final 2"). Os jogos que
            // ainda vão acontecer se descrevem citando esse número ("Vencedor Quartas de
            // Final 2"), e sem ele na etiqueta do jogo REAL a referência apontaria pro nada.
            //
            // A ordem é a de Id — a mesma que o avanço de verdade usa pra parear vencedores.
            ViewBag.NumeroNaFase = partidas
                .Where(p => ChaveamentoMataMata.EhFaseDeMataMata(p.Fase))
                .GroupBy(p => new { p.CategoriaId, p.Fase })
                .SelectMany(g => g.OrderBy(p => p.Id).Select((p, i) => (p.Id, Numero: i + 1)))
                .ToDictionary(x => x.Id, x => x.Numero);
        }
    }
}
