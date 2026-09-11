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
        private readonly AvisoDeInscricaoNoTorneio _avisoDeInscricao;
        private readonly AvisoDePlacarAoVivo _avisoDePlacar;

        // Injeta o banco de dados
        public TorneiosController(DbPadelContext context, IEstatisticasService estatisticas, IPalpiteService palpites,
            IWebHostEnvironment env, IPushNotificationService pushService,
            IPagamentoInscricaoService pagamentos, Microsoft.Extensions.Options.IOptions<TaxasExibicao> taxas,
            Microsoft.Extensions.Options.IOptions<RegistroResultadosSettings> registro,
            ILogger<TorneiosController> logger, IPadelimetroService padelimetro,
            EncerramentoDaPartida encerramento, AvisoDeInscricaoNoTorneio avisoDeInscricao,
            AvisoDePlacarAoVivo avisoDePlacar)
        {
            _avisoDeInscricao = avisoDeInscricao;
            _avisoDePlacar = avisoDePlacar;
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

            // Só ENFILEIRA: a FilaDeAvisos entrega por fora da requisição. O e-mail inline que
            // morava aqui saía em dobro (a fila já cobre o canal) e segurava a tela de quem
            // estava se inscrevendo.
            //
            // ⚠️ SEM E-MAIL desde 09/08/2026 — mesma decisão e mesmo motivo do gêmeo em
            // DuplasController: bilhete social não vale uma linha na caixa de entrada de
            // ninguém, e cada um desses gasta cota que a recuperação de senha precisa.
            foreach (var grupo in seguidores.GroupBy(s => s.SeguidorId))
            {
                var seguidor = grupo.First().Seguidor;
                var nomesQueSigo = grupo.Select(s => jogadores.TryGetValue(s.SeguidoId, out var nome) ? nome : "").Where(n => n != "");
                var titulo = "Alguém que você segue se inscreveu num torneio";
                var corpo = $"{string.Join(" e ", nomesQueSigo)} se inscreveu em {torneio.Nome}.";

                await _pushService.EnviarParaJogadorAsync(seguidor.Id, titulo, corpo, url,
                    AlcanceDoAviso.AppSemEmail);
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

        // O MARCADOR opera o dia de jogo: Mesa de Controle, placar, largada, W.O., check-in
        // e o remendo da grade (trocar quadra/horário, refazer). Organizador e admin passam
        // junto — quem pode mais pode o menos. O que o marcador NÃO alcança continua atrás
        // de EhOrganizadorAsync: inscrições, dinheiro, edição, sorteio e comunicado.
        //
        // A lista de onde o marcador entra é escrita gate a gate, de propósito (fail-closed):
        // é a checagem que o inclui, nunca uma linha em TorneioOrganizadores — ver o
        // comentário em Models/TorneioMarcador.
        private async Task<bool> PodeOperarODiaDeJogoAsync(int torneioId, int jogadorId)
        {
            if (jogadorId <= 0) return false;

            if (await _context.TorneioMarcadores
                    .AnyAsync(m => m.TorneioId == torneioId && m.JogadorId == jogadorId))
                return true;

            return await EhOrganizadorAsync(torneioId, jogadorId);
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

            // ⚠️ MUDOU EM 18/08/2026: era `PodeOlharTudo`, e por isso o administrador nomeado e
            // o assistente liam o caixa de QUALQUER torneio do sistema. Agora é `PodeVerDinheiro`
            // — só o raiz. Quem organiza continua entrando pelo NÍVEL DE ACESSO, que é a régua
            // de sempre: o caixa do torneio dele não passa por aqui.
            //
            // O que o admin nomeado perde junto: socorrer organizador em questão de dinheiro.
            // A porta da gestão continua aberta pra ele (PodeOlharAGestaoAsync) — o que some
            // são os valores dentro dela.
            var quem = await _context.Jogadores.FindAsync(jogadorId);

            return AcessoAoDinheiroDoTorneio.PodeVer(nivel, PoderesNoSistema.PodeVerDinheiro(quem));
        }

        // "Consegue ABRIR a gestão deste torneio?" — separada de EhOrganizadorAsync de
        // propósito, porque aquela é a autoridade de EDITAR e é consultada por todos os POSTs.
        //
        // ⚠️ Somar o assistente lá teria dado a ele o poder de mexer em qualquer torneio do
        // sistema. Aqui ele só ganha a porta: a tela abre em modo leitura (formulários
        // desligados) e o servidor continua recusando toda gravação.
        private async Task<bool> PodeOlharAGestaoAsync(int torneioId, int jogadorId)
        {
            if (await EhOrganizadorAsync(torneioId, jogadorId)) return true;

            var quem = await _context.Jogadores.FindAsync(jogadorId);
            return PoderesNoSistema.PodeOlharTudo(quem);
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

            // As cidades que têm torneio, pro bloco de links no rodapé da listagem. Não é
            // enfeite: é por esses links que o buscador (e o visitante) chega às páginas de
            // cidade. Página que ninguém aponta é página que ninguém acha.
            ViewBag.CidadesComTorneio = await TorneiosPorCidade.ListarAsync(_context);

            return View();
        }

        // "Torneios de padel em Porto Alegre" — a página que responde à busca que as pessoas
        // realmente fazem. Ver Services/TorneiosPorCidade pro desenho e pro porquê.
        //
        // A rota é escrita por extenso, e não /Torneios/Cidade/porto-alegre, porque o endereço
        // é a primeira coisa que o buscador e a pessoa leem sobre a página — e este responde a
        // pergunta antes mesmo de abrir.
        [HttpGet("/torneios-de-padel-em-{slug}")]
        public async Task<IActionResult> Cidade(string slug)
        {
            var pagina = await TorneiosPorCidade.AbrirAsync(_context, slug);

            // 404 quando a cidade não tem torneio, de propósito: responder 200 com uma tela
            // vazia ensina o buscador a indexar endereço que não leva a nada, e some com a
            // confiança nas páginas que TÊM conteúdo.
            if (pagina == null) return NotFound();

            ViewBag.Lugar = pagina.Value.Lugar;
            return View(pagina.Value.Torneios);
        }

        // Exemplo de como deve ficar o seu método Details
        [HttpGet]
        // `clubeFiltroId`, `quadraFiltro` e `faseFiltro`: a sequência de jogos por clube, quadra
        // e fase (Services/FiltroDeJogos). Chegam do formulário da aba Jogos; vazio = sem recorte.
        public async Task<IActionResult> Details(int id, int? timeFiltroId, int[]? categoriaFiltroIds, bool soMeusJogos = false,
            int? clubeFiltroId = null, string? quadraFiltro = null, string? faseFiltro = null)
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

            // ── A PORTA DO TORNEIO OCULTO ──────────────────────────────────────────────
            // Não é "some da lista": o torneio que ainda não foi divulgado não abre nem com o
            // link na mão. Os escapes (organizador, admin, inscrito) e o porquê do 404 estão
            // em Services/VisibilidadeDoTorneio.
            //
            // ⚠️ Fica AQUI, logo na entrada, e não junto do `PodeGerenciar` lá embaixo: a
            // página monta classificação, chaves, histórico e palpite antes daquele ponto, e
            // recusar depois seria pagar a página inteira pra jogá-la fora.
            if (!await VisibilidadeDoTorneio.PodeAbrirAsync(_context, torneio, ObterJogadorIdLogado()))
                return NotFound();

            // O clube e a cidade dele alimentam a descrição que o Google mostra e a ficha de
            // evento (Services/DadosEstruturados) — é o "em Arena Beira Rio, Porto Alegre" que
            // faz este torneio responder a "torneio de padel em <cidade>".
            //
            // ⚠️ Consulta à PARTE, e não `.Include(t => t.Clube)`: Clube é navegação
            // OBRIGATÓRIA, e Include de navegação obrigatória vira INNER JOIN — um torneio
            // cujo clube faltasse sumiria da consulta e a página inteira viraria 404. Foi
            // exatamente o que os 8 testes pegaram na primeira tentativa. Aqui a falta do
            // clube custa no máximo o nome do local na descrição.
            ViewBag.ClubeDoTorneio = await _context.Clubes
                .AsNoTracking()
                .Include(c => c.Cidade)
                .FirstOrDefaultAsync(c => c.Id == torneio.ClubeId);

            // O torneio em mais de um clube. Vai por ViewData, e não por ViewBag, porque quem
            // consome são as PARCIAIS COMPARTILHADAS de jogo — ver Services/LugarDoJogo.
            ViewData[LugarDoJogo.ChaveNaTela] = await Robo.SedesAsync(torneio.Id);

            // MOTOR MATEMÁTICO DE CLASSIFICAÇÃO
            // 1. Puxa todas as partidas já finalizadas deste torneio
            var partidasFinalizadas = await _context.Partidas
                .Where(p => p.TorneioId == id && p.Status == "Finalizada")
                .ToListAsync();

            // A votação do MVP abre 7 dias depois do ÚLTIMO JOGO (ver Services/MvpDoTorneio).
            //
            // ⚠️ Calculado AQUI e não na view: a view não tem as partidas em mãos — o Include
            // do torneio traz categorias, duplas e grupos, e `Categoria.Partidas` chegaria
            // VAZIA. O botão simplesmente não apareceria, sem erro nenhum, em todo torneio do
            // sistema. Aqui a lista já está carregada e a conta sai de graça.
            var ultimoJogoDoTorneio = MvpDoTorneio.UltimoJogo(partidasFinalizadas
                .Where(p => p.VencedorId != null)
                .Select(p => p.HorarioFimReal ?? p.HorarioInicioReal ?? p.HorarioPrevisto));

            ViewBag.TemVotacaoDeMvp = MvpDoTorneio.TemVotacao(
                torneio.UsaVotacaoDeMvp,
                torneio.Status,
                ultimoJogoDoTorneio,
                DateTime.Now,
                torneio.Formato);

            // A ENQUETE do clube segue o mesmo corte de FORMATO do MVP (nada no Americano),
            // mas ignora o INTERRUPTOR: torneio normal com a eleição desligada continua
            // avaliando, e é aí que este link é a única porta pra tela.
            ViewBag.TemEnqueteDoTorneio = EnqueteDoTorneio.Aberta(
                torneio.Status, ultimoJogoDoTorneio, DateTime.Now, torneio.Formato);

            // O RANKING DE PALPITEIROS: a aba e o botão. Duas perguntas, nesta ordem — e é a
            // ordem que segura o custo desta página, a mais visitada do site.
            //
            // 1) A BARATA: existe QUALQUER palpite neste torneio? Um `Any` que não monta nada.
            // 2) Só então a conta inteira, e é o RANKING PRONTO quem decide se a aba aparece.
            //
            // ⚠️ ATÉ 10/09/2026 A PERGUNTA ERA OUTRA: "existe palpite em jogo já TERMINADO?".
            // 🗣️ Felipe, com o 2ª Etapa ER PADEL TOUR no ar e 41 jogos já votados: *"acho que o
            // ranking do palpitometro ja tem que aparecer"*. Na véspera do torneio — justamente
            // quando todo mundo palpita — a aba não existia, porque nenhum jogo tinha terminado.
            //
            // ⚠️ E a troca MATA o falso-positivo que morava aqui: num torneio em que os únicos
            // palpites vieram dos quatro jogadores das PRÓPRIAS partidas (que ficam fora da
            // conta), o botão aparecia e a página respondia 404. Agora as duas telas obedecem
            // ao mesmo `TemRanking`.
            ViewBag.TemRankingDePalpiteiros = false;
            if (await RankingDePalpiteiros.PalpitesDoTorneio(_context, id).AnyAsync())
            {
                var palpiteirosDoTorneio = await RankingDePalpiteiros.DoTorneioAsync(_context, id, ObterJogadorIdLogado());
                ViewBag.RankingDePalpiteiros = palpiteirosDoTorneio;
                ViewBag.TemRankingDePalpiteiros = palpiteirosDoTorneio?.TemRanking == true;
            }

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

            // O fiado: o organizador sorteou com a taxa em aberto. O aviso CONTINUA na tela dele
            // depois de adiar, com a data — sumir faria a dívida desaparecer justamente pra quem
            // deve, e o único lugar que lembraria dela seria o financeiro do Padelizou.
            ViewBag.TaxaExternoDevendo = TaxaDoTorneioExterno.EstaDevendo(torneio);

            // SELOS HISTÓRICOS: títulos de cada jogador nas mesmas categorias (por Categoria.Nome),
            // considerando torneios anteriores a este. Só campeão — ver _SeloHistorico.cshtml.
            var nomesCategorias = torneio.Categorias.Select(c => c.Nome).Distinct().ToList();
            ViewBag.HistoricoJogadores = await _estatisticas.ObterTitulosPorCategoriaAsync(nomesCategorias, excluirTorneioId: id);

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

            // QUANTAS INSCRIÇÕES JÁ FORAM FEITAS — serve só pra avisar quem vai mexer no preço
            // na aba "Gerenciar Torneio". Cada inscrição guarda o que ELA custou, então mudar
            // o valor não reescreve nenhuma; o aviso existe pra isso não ser descoberto no
            // bolso (ver Services/AvisoDeMudancaDePreco).
            //
            // ⚠️ As DUAS tabelas: torneio de chave grava em `Dupla` e Americano em
            // `InscricaoAmericana`. Contar só uma daria "ninguém inscrito" num Americano
            // lotado, e o aviso sumiria justamente onde havia mais gente a prejudicar.
            ViewBag.InscricoesJaFeitas =
                await _context.Duplas.CountAsync(d => d.Categoria.TorneioId == id)
                + await _context.InscricoesAmericanas.CountAsync(i => i.Categoria.TorneioId == id);

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
            // ⚠️ O FALLBACK É "TODOS", NÃO "A PRIMEIRA CATEGORIA" (Felipe, 04/09/2026).
            //
            // A primeira da lista não responde pergunta nenhuma — ela é só a primeira. No ER
            // PADEL TOUR isso abria a aba na 3ª Masculina, com 2 duplas de 49; quem chega na
            // página conclui que o torneio está vazio.
            //
            // Quem ESTÁ inscrito continua abrindo na própria categoria: essa pessoa abriu a
            // página pra ver a categoria dela, e "Todos" seria um passo a mais pra ela.
            ViewBag.CategoriaSelecionadaId = categoriaDoUsuario ?? InscritosDeTodasAsCategorias.Todos;

            // "Eu estou NESTE torneio?" — a mesma consulta que escolheu a categoria acima, de
            // propósito: com duas, a tela poderia abrir na categoria de alguém que ela não
            // considera inscrito.
            //
            // ⚠️ Conta quem está na LISTA DE ESPERA e quem está com pagamento pendente. Em
            // outros lugares do sistema "quem espera não está inscrito" (não ocupa vaga, não
            // entra no sorteio), mas aqui a pergunta é outra — quem está na fila é justamente
            // quem mais precisa do grupo, porque é lá que a vaga que abre é anunciada.
            bool estouNoTorneio = categoriaDoUsuario != null;

            // Pro botão "sou eu" da inscrição e pro aviso de estar inscrevendo outra pessoa.
            // O único CPF que vai pra tela é o de quem está logado — o dele mesmo.
            if (jogadorLogadoId.HasValue)
            {
                var eu = await _context.Jogadores
                    .Where(j => j.Id == jogadorLogadoId.Value)
                    .Select(j => new { j.Id, j.Cpf, j.Nome, j.LadoQuadra })
                    .FirstOrDefaultAsync();
                ViewBag.MeuJogadorId = eu?.Id;
                ViewBag.MeuCpf = eu?.Cpf;
                ViewBag.MeuNome = eu?.Nome;
                // Pré-seleciona o "de que lado você joga?" da inscrição sem parceiro com o que
                // já está no cadastro — no caso comum a pessoa não precisa mexer em nada.
                ViewBag.MeuLadoQuadra = eu?.LadoQuadra;

                // QUANTOS ME CHAMARAM, por inscrição minha (Felipe, 17/08/2026).
                //
                // ⚠️ Sem isto, a tela de aceitar/recusar só era alcançável PELO AVISO — quem
                // apagasse a notificação perdia o caminho, e a própria inscrição não dizia
                // que havia gente esperando resposta. Aviso é lembrete, não deve ser a única
                // porta: o que existe no sistema precisa estar visível de dentro dele.
                ViewBag.ChamadosPorInscricao = await _context.ChamadosDoMural
                    .Where(c => c.Dupla.Jogador1Id == jogadorLogadoId.Value
                                && c.Dupla.Categoria.TorneioId == id
                                && c.Dupla.Jogador2Id == null)
                    .GroupBy(c => c.DuplaId)
                    .Select(g => new { DuplaId = g.Key, Quantos = g.Count() })
                    .ToDictionaryAsync(x => x.DuplaId, x => x.Quantos);

                // AS INSCRIÇÕES QUE EU CHAMEI, pra a tela oferecer "tirar pedido" (21/08/2026).
                //
                // ⚠️ SEM o filtro `Jogador2Id == null` que a consulta de cima usa — e isso é
                // deliberado: é justamente o que permite tirar o pedido de uma inscrição que
                // fechou por outro caminho. Com o filtro, aquela linha viraria zumbi, invisível
                // pros dois lados.
                ViewBag.ChamadosQueEuFiz = (await _context.ChamadosDoMural
                    .Where(c => c.CandidatoId == jogadorLogadoId.Value && c.Dupla.Categoria.TorneioId == id)
                    .Select(c => c.DuplaId)
                    .ToListAsync()).ToHashSet();

                // EM QUE CATEGORIAS DESTE TORNEIO EU JÁ ESTOU — a régua de "não dá pra se
                // candidatar duas vezes na mesma categoria" (Services/MuralDeParceiros).
                //
                // ⚠️ ZERO QUERY NOVA: a consulta lá em cima já traz Categorias → Duplas →
                // Jogador1/Jogador2 pra desenhar a grade. Perguntar isso ao banco rodaria mais
                // uma consulta em toda abertura da página mais pesada do site, e seria tradução
                // nova pro Postgres que o teste InMemory não prova.
                ViewBag.MinhasInscricoesNoTorneio = torneio.Categorias
                    .SelectMany(c => InscricaoRepetida.DasDuplasCarregadas(c.Duplas, new[] { jogadorLogadoId.Value }))
                    .ToList();

                // QUEM JÁ ENTROU EM QUADRA — a janela de fechar dupla (Services/JanelaDoParceiro)
                // deixou de ser "Inscrições Abertas" em 09/09/2026 e vai até a bola rolar pra
                // AQUELA dupla. O Razor não consulta banco, então o fato chega pronto: uma
                // consulta pro torneio inteiro, e não uma por card do mural.
                //
                // ⚠️ Achata em MEMÓRIA depois de trazer duas colunas. `SelectMany` sobre um array
                // novo (`new[] { p.Dupla1Id, p.Dupla2Id }`) é o tipo de projeção que o InMemory
                // dos testes aceita e o Postgres pode recusar — o defeito de 19/08/2026 de novo.
                var duplasQueJaJogaram = await _context.Partidas
                    .Where(p => p.TorneioId == id
                             && (p.Status == "Finalizada" || p.HorarioInicioReal != null))
                    .Select(p => new { p.Dupla1Id, p.Dupla2Id })
                    .ToListAsync();

                ViewBag.DuplasQueJaJogaram = duplasQueJaJogaram
                    .SelectMany(p => new[] { p.Dupla1Id, p.Dupla2Id })
                    .ToHashSet();
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

            // PRA QUEM MANDAR O COMPROVANTE. A tela do "por fora" pedia o comprovante e não
            // dava caminho nenhum — quem já tinha o WhatsApp do organizador resolvia por fora,
            // e quem não tinha ficava com a inscrição pendurada. Ver Services/PixDoOrganizador.
            if (PixDoOrganizador.Aparece(torneio))
            {
                // O card recolhe pra quem já acertou com o organizador (Emerson, 10/09/2026:
                // "se o cara já pagou, daria pra tirar info do pagamento, ocupa muito
                // espaço"). UMA consulta a mais, e só no "por fora" com alguém logado — o `if`
                // acima já é a condição estrutural de o bloco existir na tela.
                var minhasInscricoes = jogadorLogadoId.HasValue
                    ? await PixDoOrganizador.MinhasInscricoesAsync(_context, torneio, jogadorLogadoId.Value)
                    : default;

                ViewBag.JaPagueiNesteTorneio = minhasInscricoes.JaPagueiTudo;

                // Depois das chaves publicadas o card só existe pra quem ainda deve — inclusive
                // pra quem acabou de ser promovido da lista de espera no dia do jogo.
                ViewBag.DevoAlgumaNesteTorneio = minhasInscricoes.DevoAlguma;

                // ⚠️ O CONTATO SÓ É BUSCADO SE O CARD VAI MESMO SER DESENHADO, e o portão é o
                // MESMO que a view usa. Com o controller no `Aparece` e a view no
                // `ApareceParaMim`, o dia do jogo — quando mais gente abre esta página, que é a
                // mais pesada do site — pagava uma consulta por abertura pra alimentar um card
                // que ninguém vê. Visitante deslogado depois de publicado não faz consulta
                // nenhuma: `default` acima já diz que ele não deve nada.
                if (PixDoOrganizador.ApareceParaMim(torneio, minhasInscricoes.DevoAlguma))
                    ViewBag.QuemRecebeOPix = await PixDoOrganizador.QuemRecebeOComprovanteAsync(_context, id);
            }

            // Este torneio consegue cobrar pelo site AGORA? (forma online + conta de
            // recebimento de pé). É o que decide se a inscrição pergunta "pagar agora ou
            // depois" — ver Services/QuandoPagarInscricao.
            ViewBag.TorneioPodeCobrar = _pagamentos.PodeCobrar(torneio, recebedorTorneio);

            // ── "Pagar agora": a MINHA inscrição que ainda não foi paga ──────────────────
            // Só existe em torneio que cobra pelo site e que NÃO exige pagamento na inscrição
            // (nos que exigem, a inscrição já nasce paga). Antes disso, quem entrava nesse
            // arranjo não tinha como pagar pelo app em lugar nenhum.
            if (jogadorLogadoId.HasValue && _pagamentos.PodeCobrar(torneio, recebedorTorneio))
            {
                var minhaDupla = await _context.Duplas
                    .Where(d => d.Categoria.TorneioId == id && !d.Pago && d.NomeTime == null
                             && (d.Jogador1Id == jogadorLogadoId.Value || d.Jogador2Id == jogadorLogadoId.Value))
                    .Select(d => new
                    {
                        d.Id,
                        Impedimentos = (d.ImpedimentoSextaNoite ? 1 : 0)
                                     + (d.ImpedimentoSabadoManha ? 1 : 0)
                                     + (d.ImpedimentoSabadoTarde ? 1 : 0),
                    })
                    .FirstOrDefaultAsync();

                if (minhaDupla != null)
                {
                    ViewBag.MinhaInscricaoNaoPagaDuplaId = minhaDupla.Id;
                    ViewBag.MinhaInscricaoNaoPagaValor =
                        torneio.ValorCobrado(inscricaoDeDupla: true, impedimentos: minhaDupla.Impedimentos);
                }
                else
                {
                    var minhaAmericana = await _context.InscricoesAmericanas
                        .Where(i => i.Categoria.TorneioId == id && !i.Pago
                                 && i.JogadorId == jogadorLogadoId.Value)
                        .Select(i => (int?)i.Id)
                        .FirstOrDefaultAsync();

                    if (minhaAmericana is int americanaId)
                    {
                        ViewBag.MinhaInscricaoNaoPagaAmericanaId = americanaId;
                        ViewBag.MinhaInscricaoNaoPagaValor =
                            torneio.ValorCobrado(inscricaoDeDupla: false, impedimentos: 0);
                    }
                }
            }

            // O MURAL: o que quem jogou escreveu e está publicado. Só faz sentido em torneio
            // que acabou — antes disso não existe avaliação nenhuma, e uma consulta a mais em
            // toda página de torneio aberto seria custo sem resposta.
            if (torneio.Status == Services.MvpDoTorneio.StatusFinalizado)
            {
                ViewBag.ComentariosPublicos = await EnqueteDoTorneio.PublicadosAsync(_context, id);
            }

            // ⚠️ Duas perguntas diferentes: EDITAR (organizador de verdade) e ABRIR A TELA (que
            // o assistente do sistema também pode). A aba de gestão aparece pelos dois, mas em
            // modo leitura ela vem com os formulários desligados — ver PoderesNoSistema.
            bool ehOrganizadorDeVerdade = jogadorLogadoId.HasValue
                && await EhOrganizadorAsync(id, jogadorLogadoId.Value);

            // Quem pode ver e aprovar as chaves pendentes (ver Services/AprovacaoDeChaves) —
            // a MESMA régua de organizador/admin, sem o assistente somado: ele acompanha o
            // resto do sistema, mas esta aprovação é decisão de quem organiza de verdade.
            ViewBag.PodeAprovarChaves = ehOrganizadorDeVerdade;

            ViewBag.PodeGerenciar = ehOrganizadorDeVerdade
                || (jogadorLogadoId.HasValue && await PodeOlharAGestaoAsync(id, jogadorLogadoId.Value));

            ViewBag.GestaoSoLeitura = ViewBag.PodeGerenciar == true && !ehOrganizadorDeVerdade;

            // Quem enxerga o botão do grupo no WhatsApp: quem está inscrito e quem cuida do
            // torneio. Fica AQUI, e não na view, porque é uma decisão de quem-pode — a view só
            // pergunta ao Services/GrupoDoTorneioNoWhatsApp se há link pra esta pessoa.
            ViewBag.PodeVerOGrupoDoWhats = estouNoTorneio || ViewBag.PodeGerenciar == true;

            // Organiza junto, mas o caixa é de quem criou (ver AcessoAoDinheiroDoTorneio).
            ViewBag.PodeVerDinheiro = jogadorLogadoId.HasValue
                && await PodeVerDinheiroAsync(id, jogadorLogadoId.Value);

            // Pedido de equipe pra registrar os resultados: o mais recente manda na tela.
            ViewBag.RegistroHabilitado = _registro.Habilitado;
            ViewBag.RegistroPercentual = _registro.PercentualDasInscricoes;
            ViewBag.RegistroValorMinimo = _registro.ValorMinimo;
            ViewBag.PedidoRegistro = await _context.SolicitacoesRegistroResultados
                .Where(s => s.TorneioId == id)
                .OrderByDescending(s => s.SolicitadaEm)
                .FirstOrDefaultAsync();
            // O marcador não ganha o painel do organizador — ganha um atalho pro posto dele
            // (Mesa, jogos, check-in). A flag existe pra tela desenhar esse atalho; cada
            // ação continua conferindo no servidor.
            ViewBag.EhMarcador = jogadorLogadoId.HasValue && await _context.TorneioMarcadores
                .AnyAsync(m => m.TorneioId == id && m.JogadorId == jogadorLogadoId.Value);

            if (ViewBag.PodeGerenciar == true)
            {
                // A aba "Planejamento de quadras" (09/09/2026, pedido do Felipe pelo Er:
                // "quantos horários eles teriam que locar de quadra, extra"). Aqui só o RESUMO
                // — girar os números é na tela cheia (TorneiosController.Planejamento), porque
                // esta página é a mais pesada do site e um planejador existe pra ser girado.
                ViewBag.PlanejamentoResumo = await ResumoDoPlanejamentoAsync(torneio);

                ViewBag.Organizadores = await _context.TorneioOrganizadores
                    .Include(o => o.Jogador)
                    .Where(o => o.TorneioId == id)
                    .ToListAsync();

                ViewBag.Marcadores = await _context.TorneioMarcadores
                    .Include(m => m.Jogador)
                    .Where(m => m.TorneioId == id)
                    .ToListAsync();

                // Como o torneio foi avaliado (enquete pós-torneio). A média só existe com
                // resposta o bastante — a regra mora em EnqueteDoTorneio.MediaVisivel.
                ViewBag.ResumoDaEnquete = await EnqueteDoTorneio.ResumoAsync(_context, id);

                // O que escreveram, publicado ou não — inclusive o que é anônimo, que só
                // existe pra estes olhos. ⚠️ Quem PUBLICA não é quem abre a tela: o assistente
                // do sistema chega até aqui em modo leitura, e o serviço recusa o POST dele.
                ViewBag.ComentariosParaModerar = await EnqueteDoTorneio.ParaModerarAsync(_context, id);

                ViewBag.CatalogoClubes = await _context.Clubes.ParaEscolher().ToListAsync();
                // Ordenadas por Id, que é a mesma ordem em que o formulário desenha os campos
                // de nome e a mesma que o POST do Editar usa pra reconciliar. As três ordens
                // TÊM que ser a mesma, senão renomear a quadra 2 renomeia a 3.
                var quadrasDoTorneio = await _context.Quadras
                    .Where(q => q.TorneioId == id).OrderBy(q => q.Id).ToListAsync();
                ViewBag.Quadras = quadrasDoTorneio;

                // A quadra preferida de cada categoria, já traduzida pro par
                // "categoria:POSIÇÃO" que o formulário fala (ver PreferenciaDeQuadra.Ler).
                // Posição, e não Id da quadra, porque aqui o organizador pode marcar uma
                // quadra que ainda vai nascer — quem sobe de 3 pra 5 quadras escolhe a
                // preferência da quinta no mesmo salvamento que a cria.
                var posicaoDaQuadra = quadrasDoTorneio
                    .Select((q, posicao) => (q.Id, posicao))
                    .ToDictionary(q => q.Id, q => q.posicao);

                ViewBag.QuadrasPreferidas = (await _context.QuadrasDaCategoria
                        .Where(q => q.Quadra.TorneioId == id)
                        .Select(q => new { q.CategoriaId, q.QuadraId })
                        .ToListAsync())
                    .Where(p => posicaoDaQuadra.ContainsKey(p.QuadraId))
                    .Select(p => $"{p.CategoriaId}:{posicaoDaQuadra[p.QuadraId]}")
                    .ToList();

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

                // O Americano não tem a tela de aprovação do Padrão — GerarRodadas* vai direto
                // pra "Fase de Grupos", sem passar por "Chaves em Aprovação". Este botão é o
                // "apague as chaves antes" que a própria PortaDaInscricao.PorQueNaoPodeAbrir
                // promete pro organizador quando `jaSorteou` bloqueia o Reabrir. Mesma régua do
                // servidor (DesfazerRodadasAmericano): só aparece enquanto ninguém jogou nada.
                ViewBag.PodeDesfazerRodadasAmericano = jaSorteou
                    && torneio.Status == "Fase de Grupos"
                    && FormatoDoTorneio.EhAmericano(torneio.Formato)
                    && !await _context.Partidas.AnyAsync(p => p.TorneioId == id && p.Status != "Agendada");

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
                            Valor = PrecoDaInscricao.DaDupla(torneio, d),
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
                            Valor = PrecoDaInscricao.DaInscricaoAmericana(torneio, i),
                            PagoEm = i.PagoEm,
                        })
                        .ToListAsync();

                    ((List<DevolucaoPendenteVM>)ViewBag.PagosParaDevolver).AddRange(americanosPagos);
                }
            }

            // Aba "Jogos" embutida (Ao Vivo/Agendadas/Finalizadas) — só depois que as inscrições fecham.
            if (torneio.Status != "Inscrições Abertas")
            {
                await CarregarViewBagJogosAsync(id, timeFiltroId, categoriaFiltroIds, soMeusJogos,
                    new FiltroDeJogos(clubeFiltroId, quadraFiltro, faseFiltro));
                // Pontos por time neste torneio (só faz sentido depois que começa a valer resultado).
                ViewBag.PontosTimes = await _estatisticas.ObterPontosTimesNoTorneioAsync(id);

                // Chaveamento do mata-mata: as partidas de fase eliminatória por categoria, pra
                // a view desenhar o bracket com os confrontos REAIS (antes era um desenho fixo).
                // "Primeira Rodada" é a abertura da CHAVE DIRETA (quadro de 32 com bye) — sem
                // ela na lista, a rodada existe no banco e não aparece no desenho da chave.
                var fasesMataMata = new[] { ChaveamentoMataMata.PrimeiraRodada,
                                            "Oitavas de Final", "Quartas de Final", "Semifinal", "Final" };
                // O desenho da chave obedece ao MESMO portão da lista (ViewBag.ChaveAindaNaoPublicada,
                // decidido em CarregarViewBagJogosAsync): chave esperando aprovação não se desenha
                // pra quem não organiza.
                var partidasMataMata = ViewBag.ChaveAindaNaoPublicada == true
                    ? new List<Partida>()
                    : await _context.Partidas
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
                //
                // ⚠️ Numa VARIÁVEL, e não só no ViewBag: o painel logo abaixo come da mesma
                // lista, e ler de volta do `dynamic` seria um cast sem rede — mudou o tipo aqui,
                // o erro só aparece em tempo de execução, na página mais visitada do site.
                var todosOsJogosDeGrupo = await _context.Partidas
                    .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
                    .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
                    .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
                    .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
                    .Where(p => p.TorneioId == id
                             && (p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo ")))
                    .OrderBy(p => p.HorarioPrevisto).ThenBy(p => p.Id)
                    .ToListAsync();
                ViewBag.JogosDeGrupo = todosOsJogosDeGrupo;

                // "O QUE CADA UM PRECISA PARA PASSAR", por grupo — 🗣️ Felipe, 11/09/2026, num
                // print do Grupo B do 2ª Etapa ER Padel Tour: *"quando chegar nessa parte, que o
                // grupo de 3, ja tiveram 2 jogos e falta um, exiba botão ... explicando qual
                // placar cada um precisa fazer para passar ... fica a duvida de quantos games
                // precisa fazer para passar de fase"*.
                //
                // O painel já existia na tela de Classificação desde 13/08/2026 (mesmo pedido,
                // mesma conta): o que faltava era ele estar ONDE o print foi tirado. Motor único
                // em Services/OQuePrecisaParaClassificar, que simula o placar que falta e
                // pergunta à régua oficial quem classifica — nenhuma régua nova aqui.
                //
                // ⚠️ A CHAVE DO DICIONÁRIO É O Id DO GRUPO, e não o nome: "Grupo A" existe em
                // TODA categoria do torneio, e um dicionário por nome faria o card da 4ª
                // Masculina mostrar o que a 2ª precisa fazer.
                //
                // ⚠️ QUANTOS PASSAM sai da régua única (`ClassificacaoDeGrupos.VagasPorGrupo`) —
                // o MESMO número que o AvancoDaChave usa pra montar o mata-mata de verdade.
                // Simular com outro promete vaga que a chave não vai dar, que é o defeito exato
                // que o serviço foi escrito pra impedir.
                var oQuePrecisaPorGrupo = new Dictionary<int, OQuePrecisaParaClassificar.Quadro>();
                // O formato é do TORNEIO por fase (Services/FormatoDaPartida) — a categoria não
                // tem o próprio, então a pergunta é feita uma vez só.
                var formatoDosGrupos = FormatoDaPartida.De(torneio, FasesTorneio.FaseDeGrupos);

                foreach (var categoria in torneio.Categorias)
                {
                    int passamDaCategoria = ClassificacaoDeGrupos.VagasPorGrupo(categoria);

                    foreach (var grupo in categoria.GruposTorneio)
                    {
                        var idsDoGrupo = grupo.Duplas.Select(d => d.Id).ToHashSet();
                        var doGrupo = todosOsJogosDeGrupo
                            .Where(p => idsDoGrupo.Contains(p.Dupla1Id) && idsDoGrupo.Contains(p.Dupla2Id))
                            .ToList();

                        if (OQuePrecisaParaClassificar.Montar(
                                grupo.Duplas.ToList(), doGrupo, passamDaCategoria, formatoDosGrupos) is { } quadro)
                            oQuePrecisaPorGrupo[grupo.Id] = quadro;
                    }
                }

                ViewBag.OQuePrecisaPorGrupo = oQuePrecisaPorGrupo;
            }

            // SEGUIR O TORNEIO: o botão só existe pra quem JÁ ESTÁ INSCRITO (pedido do Felipe,
            // 10/08/2026). Quem se inscreveu é quem fica olhando a chave encher pra saber
            // contra quem vai jogar; pra quem só passou na página, o mesmo aviso vira ruído —
            // e cada seguidor a mais multiplica uma rajada que já é grande.
            //
            // ⚠️ Fica AQUI, no corpo do Details, e não dentro do CarregarViewBagJogosAsync:
            // aquele só roda quando as inscrições já fecharam, e o botão sumiria justamente
            // durante as inscrições — que é quando ele serve. (Foi onde nasceu, e o teste
            // O_botao_de_seguir_aparece_pra_quem_esta_inscrito_por_qualquer_porta pegou.)
            //
            // Vale as DUAS portas de inscrição: dupla (Dupla.Jogador1/Jogador2) e
            // individual/americano (InscricaoAmericana). Olhar só uma esconderia o botão de
            // metade das pessoas, e justamente sem erro nenhum na tela.
            if (ObterJogadorIdLogado() is int quemOlha)
            {
                var inscritoEmDupla = await _context.Duplas
                    .AnyAsync(d => d.Categoria!.TorneioId == id
                                && (d.Jogador1Id == quemOlha || d.Jogador2Id == quemOlha));

                var inscritoNoAmericano = await _context.InscricoesAmericanas
                    .AnyAsync(i => i.Categoria!.TorneioId == id && i.JogadorId == quemOlha);

                ViewBag.EstouInscritoNesteTorneio = inscritoEmDupla || inscritoNoAmericano;
                ViewBag.SigoEsteTorneio = await _context.SeguidoresTorneio
                    .AnyAsync(s => s.TorneioId == id && s.JogadorId == quemOlha);
            }

            return View(torneio);
        }
        public async Task<IActionResult> Jogos(int id, int? timeFiltroId, int[]? categoriaFiltroIds, bool soMeusJogos = false,
            int? clubeFiltroId = null, string? quadraFiltro = null, string? faseFiltro = null)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();
            // Mesma porta do Details: a grade de jogos de um torneio escondido conta o torneio
            // inteiro (nomes, horários, quadras) pra quem não deveria nem saber que ele existe.
            if (!await VisibilidadeDoTorneio.PodeAbrirAsync(_context, torneio, ObterJogadorIdLogado()))
                return NotFound();

            // As chaves foram sorteadas mas ainda não aprovadas (ver Services/AprovacaoDeChaves)
            // — só quem organiza/administra enxerga os jogos por aqui. Pra todo mundo mais é
            // como se elas não tivessem saído ainda.
            if (torneio.Status == AprovacaoDeChaves.Pendente
                && !await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0))
            {
                TempData["Erro"] = "As chaves deste torneio ainda não foram aprovadas.";
                return RedirectToAction("Details", new { id });
            }

            await CarregarViewBagJogosAsync(id, timeFiltroId, categoriaFiltroIds, soMeusJogos,
                new FiltroDeJogos(clubeFiltroId, quadraFiltro, faseFiltro));
            ViewBag.Torneio = torneio;

            return View();
        }

        // Semifinal e Final não existiam pra quem olhava a tela: o motor só cria a rodada
        // quando a anterior fecha, então o jogador via a primeira rodada e mais nada — sem
        // saber a que horas voltar nem contra quem pode jogar. A regra mora em
        // Services/ProximasFasesDaChave; aqui só se junta o que ela precisa.
        //
        // `reservas` são os horários que o organizador reservou pra jogos previstos
        // (Models/ReservaDeHorario). Nulo = lê do banco; a troca de horário passa as dela pra
        // conferir a troca ANTES de gravar.
        private async Task<List<ProximasFasesDaChave.JogoQueVem>> ProjetarProximasFasesAsync(
            int torneioId, List<Partida> partidas, IReadOnlyList<ReservaDeHorario>? reservas = null)
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
                // Com as duplas de cada grupo: o TAMANHO do grupo é o que diz quem descansa
                // na prévia (ChaveProjetada), como no robô.
                .Include(c => c.GruposTorneio).ThenInclude(g => g.Duplas)
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
                    fimDosGruposPorCategoria.TryGetValue(categoria.Id, out var fim)
                        ? fim : null;

                var gruposEmOrdem = categoria.GruposTorneio.OrderBy(g => g.Nome).ToList();
                cadeias.Add(ProximasFasesDaChave.MontarDosGrupos(
                    gruposEmOrdem.Select(g => g.Nome).ToList(),
                    ClassificacaoDeGrupos.VagasPorGrupo(categoria),
                    fimDosGrupos,
                    categoria.Nome,
                    categoria.Id,
                    gruposEmOrdem.Select(g => g.Duplas.Count).ToList()));
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
                        // ⚠️ O "por ordem" TAMBÉM tem hora desde 09/09/2026 (ver
                        // Services/OrdemDeLiberacao) — escondê-la aqui deixaria a projeção das
                        // próximas fases muda justamente pro torneio que mais precisa dela.
                        p.HorarioPrevisto)).ToList(),
                    byes!,
                    porCategoria.First().Categoria.Nome,
                    porCategoria.Key));
            }

            if (cadeias.Count == 0) return new();

            // As quadras são do TORNEIO, não da categoria: só dá pra dizer em qual quadra um
            // jogo projetado cai depois de pôr todas as categorias na mesma grade, junto com
            // o que já está marcado de verdade. Sem isso a tela prometia oito jogos no mesmo
            // minuto num torneio de cinco quadras — e nenhum deles com quadra, porque não
            // havia como saber qual.
            var ocupadas = partidas
                .Where(p => p.HorarioPrevisto != null)
                // A FASE vai junto: é ela que diz o posto do jogo real, e é o que faz a prévia
                // esperar a fase de grupos das OUTRAS categorias. Ver ProximasFasesDaChave.Agendar.
                .Select(p => new ProximasFasesDaChave.VagaOcupada(p.HorarioPrevisto!.Value, p.NomeQuadra, p.Fase))
                .ToList();

            // As reservas do organizador — só as de fases que AINDA NÃO NASCERAM: a fase que já
            // virou jogo real entra por `ocupadas`, e a reserva dela já foi consumida pelo robô.
            reservas ??= await ReservasDeHorario.DoTorneio(_context, torneioId).ToListAsync();
            var fasesReais = partidas.Select(p => (p.CategoriaId, p.Fase)).ToHashSet();
            var reservadas = reservas
                .Where(r => !fasesReais.Contains((r.CategoriaId, r.Fase)))
                .Select(r => new ProximasFasesDaChave.HorarioReservado(r.CategoriaId, r.Fase, r.Numero, r.Horario, r.NomeQuadra))
                .ToList();

            // As SEDES vão junto: janela do local alugado e trava de clube da categoria são as
            // mesmas da grade de verdade — sem elas a prévia prometia o Radar no domingo.
            var projetados = ProximasFasesDaChave.Agendar(
                cadeias,
                new ProximasFasesDaChave.ConfiguracaoDaGrade(
                    torneio.TempoPrevistoPartidaMinutos,
                    torneio.QuantidadeQuadras,
                    await QuadrasEmUsoAsync(torneioId),
                    torneio.HoraFimDoDia,
                    torneio.HoraInicioDiasSeguintes,
                    await SedesAsync(torneioId)),
                ocupadas,
                reservadas);

            // A POSIÇÃO DENTRO DO HORÁRIO QUE O ORGANIZADOR GRAVOU PRA CADA PRÉVIA (10/09/2026).
            // Ela mora na reserva, junto com a hora e a quadra dela — e é lida AQUI, e não dentro
            // do motor da projeção: aquele decide QUANDO o jogo cai; em que posição da linha o
            // organizador quer vê-lo é assunto da tela (Services/OrdemNoHorario).
            var ordemReservada = reservas
                .Where(r => r.OrdemNoHorario != null)
                .ToDictionary(r => (r.CategoriaId, r.Fase, r.Numero), r => r.OrdemNoHorario);

            return projetados
                .Select(j => j.CategoriaId is int categoria
                             && ordemReservada.TryGetValue((categoria, j.Fase, j.Numero), out var ordem)
                    ? j with { OrdemNoHorario = ordem }
                    : j)
                .OrderBy(j => j.Horario ?? DateTime.MaxValue)
                .ToList();
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
            // A" e ninguém sabe quem vai ser o 1º — mas se sabe DE QUAL GRUPO ele sai, e é
            // isso que recorta a chave. Quem está no Grupo A pode terminar em 1º ou em 2º e
            // cair nas duas vagas do Grupo A; a oitava do "1º do Grupo E" nunca vai ser dele.
            var minhasCategorias = todas
                .Where(p => EstouNesteJogo(p, jogadorId))
                .Select(p => p.CategoriaId)
                .ToHashSet();

            var minhasVagasNosGrupos = todas
                .Where(p => minhasCategorias.Contains(p.CategoriaId))
                .GroupBy(p => p.CategoriaId)
                .Where(g => !g.Any(p => ChaveamentoMataMata.EhFaseDeMataMata(p.Fase)))
                .Select(g => new MeusJogos.VagaNosGrupos(
                    g.First().Categoria?.Nome ?? "", MeuGrupoNaCategoria(g, jogadorId)))
                .Where(v => v.Categoria != "")
                .ToList();

            return MeusJogos.Filtrar(projetados, reais, minhasDuplas, minhasVagasNosGrupos);
        }

        // Em que grupo o jogador caiu nesta categoria. Nulo quando a dupla não tem grupo
        // sorteado — aí não dá pra recortar a chave, e MeusJogos volta a mostrá-la inteira.
        private static string? MeuGrupoNaCategoria(IEnumerable<Partida> daCategoria, int jogadorId) =>
            daCategoria
                .SelectMany(p => new[] { p.Dupla1, p.Dupla2 })
                .FirstOrDefault(d => d.Jogador1Id == jogadorId || d.Jogador2Id == jogadorId)
                ?.GrupoTorneio?.Nome;

        // Compartilhado entre Jogos() (página dedicada, usada como destino do "Editar Jogo")
        // e Details() (aba "Jogos" embutida na página do torneio) — mesma lógica de filtro/abas
        // pros dois lugares não divergirem.
        private async Task CarregarViewBagJogosAsync(int torneioId, int? timeFiltroId, int[]? categoriaFiltroIds, bool soMeusJogos = false,
            FiltroDeJogos? sequencia = null)
        {
            // As sedes ficam AQUI, e não só na ação, porque é este método que abastece as duas
            // telas que mostram jogo (`Details` e `Jogos`) — e a etiqueta de quadra sai das
            // mesmas parciais nas duas. Ver Services/LugarDoJogo.
            var sedes = await Robo.SedesAsync(torneioId);
            ViewData[LugarDoJogo.ChaveNaTela] = sedes;

            // ⚠️ DUAS LISTAS, E O NOME DIZ QUAL É QUAL (10/09/2026). `doTorneio` é a grade INTEIRA;
            // `query` é ela com os filtros de time e de categoria, que viram SQL logo abaixo. Até
            // hoje existia só a segunda, com o nome `todasAsPartidas` — que prometia o torneio
            // inteiro e entregava o recorte da tela. 🗣️ Felipe, com "3ª Feminina" marcada: *"eu
            // selecionei '3 feminina' e esta aparecendo jogos de outras categorias no filtro"*.
            var doTorneio = _context.Partidas
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
                // O grupo da dupla é o que recorta "meus jogos" na fase de grupos (ver
                // MeuGrupoNaCategoria). Fica explícito de propósito: hoje a projeção carrega
                // os GruposTorneio da categoria logo depois e o fixup do EF preencheria esta
                // navegação de graça — dependência invisível, que some no dia em que aquela
                // consulta virar AsNoTracking ou mudar de lugar, e volta a mostrar a chave
                // inteira sem quebrar teste nenhum.
                .Include(p => p.Dupla1).ThenInclude(d => d.GrupoTorneio)
                .Include(p => p.Dupla2).ThenInclude(d => d.GrupoTorneio)
                .Where(p => p.TorneioId == torneioId);

            var query = doTorneio;

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

            // A GRADE INTEIRA, pra quem PRECISA dela: a projeção das próximas fases (as quadras
            // ocupadas de todas as categorias decidem a hora de cada prévia), a numeração dentro
            // da fase e a lista de horários do "Definir horário". Uma segunda leitura só quando o
            // SQL recortou — sem filtro as duas listas são a mesma, e o EF devolve as mesmas
            // instâncias nos dois casos (identity map).
            bool recortadaNoSql = timeFiltroId.HasValue || (categoriaFiltroIds != null && categoriaFiltroIds.Length > 0);
            var jogosDoTorneio = recortadaNoSql ? await doTorneio.ToListAsync() : partidas;

            // ⚠️ AS CHAVES AINDA NÃO FORAM APROVADAS: PRA QUEM NÃO ORGANIZA, NÃO EXISTE JOGO
            // (09/09/2026). 🗣️ Felipe, num print de PRODUÇÃO em navegação anônima: *"outro erro
            // grave apareceu as partidas agendadas sem ter aprovado as chaves, cuide isso e nao
            // deixe q nada vaze sem ser publicado"* — e depois: *"nao exiba nada até publicar a
            // chave (desse menu de jogos)"*.
            //
            // 🕳️ O PORTÃO EXISTIA NA PORTA ERRADA. A ação `Jogos` conferia a aprovação desde 22/08;
            // a MESMA lista, embutida na aba "Jogos" do `Details`, só conferia `Status !=
            // "Inscrições Abertas"` — e "Chaves em Aprovação" passa nisso. Porta da frente
            // trancada, porta dos fundos aberta pra qualquer visitante, inclusive deslogado.
            //
            // ⚠️ POR ISSO O PORTÃO MORA AQUI, no método que abastece as DUAS telas, e não numa
            // ação: régua de visibilidade escrita numa ação protege aquela ação, não o dado. E
            // esvazia a lista em vez de sair cedo, pra TODA chave do ViewBag continuar existindo
            // com o valor vazio — a view não precisa saber que houve portão.
            var torneioDaGrade = await _context.Torneios.FindAsync(torneioId);
            bool chaveAindaNaoPublicada = torneioDaGrade != null
                && torneioDaGrade.Status == AprovacaoDeChaves.Pendente
                && !await EhOrganizadorAsync(torneioId, ObterJogadorIdLogado() ?? 0);
            if (chaveAindaNaoPublicada)
            {
                partidas = new List<Partida>();
                jogosDoTorneio = new List<Partida>();
            }
            ViewBag.ChaveAindaNaoPublicada = chaveAindaNaoPublicada;

            // "MEUS JOGOS": a lista inteira de um torneio de 86 jogos não serve pra quem só
            // quer saber a que horas ele joga. Precisa vir ANTES da projeção — os jogos que
            // ainda não existem saem dos que existem, e a corrente parte dos jogos DELE.
            var meuJogadorId = ObterJogadorIdLogado();
            var todasAsPartidas = partidas;
            ViewBag.TenhoJogoAqui = meuJogadorId != null && partidas.Any(p => EstouNesteJogo(p, meuJogadorId.Value));
            ViewBag.SoMeusJogos = soMeusJogos && ViewBag.TenhoJogoAqui;

            if (ViewBag.SoMeusJogos)
                partidas = partidas.Where(p => EstouNesteJogo(p, meuJogadorId!.Value)).ToList();

            // A SEQUÊNCIA POR CLUBE, QUADRA OU FASE (Services/FiltroDeJogos, 10/09/2026). Vem
            // DEPOIS do "meus jogos" e ANTES das três listas, pelo mesmo motivo dele: recorta o
            // que a tela MOSTRA, não o que a projeção das próximas fases e a classificação
            // precisam — essas continuam partindo de `todasAsPartidas`.
            var filtro = sequencia ?? FiltroDeJogos.Nenhum;
            if (filtro.Ativo)
                partidas = partidas.Where(p => filtro.Aceita(sedes, p)).ToList();

            ViewBag.AoVivo = partidas.Where(p => p.Status == "AoVivo").OrderBy(p => p.HorarioInicioReal).ToList();

            // PLACAR AO VIVO NA TELA DE BLOQUEIO: quais desses jogos EU já sigo — precisa vir
            // do servidor, não só de estado no JavaScript, porque o card AO VIVO se REDESENHA
            // sozinho a cada 20s (js/jogos-ao-vivo-atualiza.js): sem isto o botão voltaria a
            // dizer "Seguir" no tique seguinte pra quem já estava seguindo.
            var idsAoVivo = ((List<Partida>)ViewBag.AoVivo).Select(p => p.Id).ToList();
            ViewBag.PartidasQueSigo = meuJogadorId != null && idsAoVivo.Count > 0
                ? (await _context.Set<SeguidorDePartida>()
                    .Where(s => s.JogadorId == meuJogadorId && idsAoVivo.Contains(s.PartidaId))
                    .Select(s => s.PartidaId)
                    .ToListAsync()).ToHashSet()
                : new HashSet<int>();
            // Placar lançado depois (sem HorarioFimReal) cai pro horário previsto em vez
            // de flutuar em ordem arbitrária no meio da lista.
            ViewBag.Finalizadas = partidas.Where(p => p.Status == "Finalizada")
                .OrderByDescending(p => p.HorarioFimReal ?? p.HorarioPrevisto).ThenByDescending(p => p.Id).ToList();
            // ⚠️ A FILA SAI DE Services/OrdemNoHorario, E NÃO DE UM `OrderBy(HorarioPrevisto)` SOLTO
            // (10/09/2026). Aquele ordenava só pela hora, sobre uma lista que veio do Postgres SEM
            // `ORDER BY`: dois jogos no mesmo minuto saíam na ordem que o banco quisesse — e ela
            // muda sozinha depois de um UPDATE. 🗣️ *"se tem semifinal 1 e semifinal 2 no mesmo
            // horario, siga a ordem automatica de a 1 vir antes da 2, mas permita q o usuario edite"*.
            // É a MESMA régua que as setas ↑↓ usam pra achar o vizinho: duas contas de "quem vem
            // antes" fariam a seta mover o jogo pra um lugar diferente do que a tela mostrou.
            ViewBag.Agendadas = OrdemNoHorario
                .Ordenar(partidas.Where(p => p.Status == "Agendada"), Array.Empty<ProximasFasesDaChave.JogoQueVem>())
                .Select(l => l.Jogo!)
                .ToList();

            // ⚠️ A projeção parte do torneio INTEIRO, não da lista filtrada: a chave de uma
            // categoria só se desenha com todos os jogos dela na mão, e filtrar antes deixaria
            // a projeção montando um quadro que não existe. O recorte vem depois.
            // ⚠️ A PRÉVIA TAMBÉM FICA MUDA: `ProjetarProximasFasesAsync` monta a chave a partir dos
            // GRUPOS lidos direto do banco, e desenharia "1º do Grupo A × 2º do Grupo C" mesmo com
            // a lista de jogos vazia — o desenho do sorteio vazando por outra porta.
            var projetados = chaveAindaNaoPublicada
                ? new List<ProximasFasesDaChave.JogoQueVem>()
                : await ProjetarProximasFasesAsync(torneioId, jogosDoTorneio);

            List<ProximasFasesDaChave.JogoQueVem> previstos = ViewBag.SoMeusJogos
                ? RecortarProjecaoDoJogador(projetados, jogosDoTorneio, meuJogadorId!.Value)
                : projetados;

            // A prévia obedece ao MESMO recorte do jogo real: filtrar por "Semifinal" e ver a
            // Final prevista logo abaixo seria a lista discordando do próprio filtro.
            //
            // ⚠️ A CATEGORIA ENTRA AQUI, e não no `FiltroDeJogos`, porque pro jogo real ela vira
            // SQL lá em cima — e era essa metade faltando que punha a Oitava da 4ª Masculina na
            // tela de quem marcou só a 3ª Feminina. A projeção monta as cadeias a partir das
            // CATEGORIAS do banco, que nenhum `Where` de partida alcança.
            //
            // Prévia sem categoria (projeção antiga, sem Id) sai quando há filtro: não dá pra
            // dizer que ela é da categoria escolhida, e afirmar que é seria o mesmo defeito.
            var categoriasEscolhidas = categoriaFiltroIds ?? Array.Empty<int>();
            ViewBag.JogosQueVem = previstos
                .Where(j => categoriasEscolhidas.Length == 0
                            || (j.CategoriaId is int daPrevia && categoriasEscolhidas.Contains(daPrevia)))
                .Where(j => !filtro.Ativo || filtro.Aceita(sedes, j))
                .ToList();

            // ⚠️ A PROJEÇÃO INTEIRA, PRA QUEM CASA POR ÍNDICE (10/09/2026). A aba CHAVES do
            // `Details` desenha o quadro previsto lendo esta lista e parseando a vaga pela
            // POSIÇÃO dentro da fase (`daFase[i]`) — os dois saem do mesmo motor, na mesma
            // ordem. Uma lista recortada faz a Semifinal 2 aparecer na vaga da Semifinal 1: o
            // quadro mostrando confronto que não existe.
            //
            // Isto JÁ ACONTECIA com o "meus jogos", que recorta a mesma lista desde 09/09 — o
            // filtro por clube/quadra/fase só tornou frequente o que era raro. Por isso a aba
            // Chaves passou a ler daqui, e não de `JogosQueVem`: aquela é a lista da ABA JOGOS,
            // e é dela que os filtros são donos.
            ViewBag.ProjecaoCompleta = projetados;

            // ⚠️ O QUE A TELA OFERECE E O QUE AS OUTRAS ABAS LEEM SAI DA GRADE INTEIRA DO
            // TORNEIO, e não da lista da tela. `todasAsPartidas` parece a grade inteira e não é:
            // o filtro de time e o de categoria viram SQL lá em cima, antes de qualquer lista
            // existir. Montar as opções em cima dela faz o select perder justamente a opção que
            // está ligada (marcar uma categoria de grupos apagava "Quartas de Final" do select,
            // com o faseFiltro=Quartas ainda valendo) e o número da fase renumerar.
            //
            // Passa pelo MESMO portão da chave não aprovada: sem ele, o select de quadras
            // mostraria a grade não publicada a visitante deslogado (Regra 0).
            var gradeDoTorneio = chaveAindaNaoPublicada
                ? new List<(int Id, int CategoriaId, string Fase, string? NomeQuadra, string Status)>()
                : (await _context.Partidas.AsNoTracking()
                        .Where(p => p.TorneioId == torneioId)
                        .Select(p => new { p.Id, p.CategoriaId, p.Fase, p.NomeQuadra, p.Status })
                        .ToListAsync())
                    .Select(p => (p.Id, p.CategoriaId, p.Fase, p.NomeQuadra, p.Status))
                    .ToList();

            ViewBag.FiltroDeJogos = filtro;
            ViewBag.ClubesDoTorneio = sedes.MaisDeUmClube ? sedes.Clubes : Array.Empty<(int Id, string Nome)>();
            ViewBag.FasesDoTorneio = FiltroDeJogos.FasesParaEscolher(
                gradeDoTorneio.Select(p => p.Fase).Concat(projetados.Select(j => j.Fase)));

            // ⚠️ AS QUADRAS DO FILTRO SÃO AS QUE ALGUM JOGO USA — e não `QuadrasDoTorneio`, que
            // completa pelo CADASTRO quando os jogos não têm quadra (ver NomesDeQuadra). No
            // torneio POR ORDEM o motor apaga a quadra de todo jogo de propósito
            // (OrdemDeLiberacao.ApagarAsQuadras), então aquela lista vinha cheia e QUALQUER
            // escolha esvaziava a tela — o filtro quebrado justamente no torneio do Er, que é o
            // que originou o pedido. Sem quadra em jogo nenhum, o select não aparece.
            ViewBag.QuadrasParaFiltrar = gradeDoTorneio
                .Select(p => (p.NomeQuadra ?? "").Trim())
                .Where(q => q.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(q => q, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            // ⚠️ QUANTOS JOGOS O "RECALCULAR HORÁRIOS" REALMENTE REFAZ. O aviso vermelho dele
            // contava a lista da TELA: com o filtro no Radar ele prometia refazer "11 jogos" e
            // refazia os 88 do torneio. Diálogo de ação destrutiva que mente sobre o tamanho do
            // estrago é o defeito que os PRs #123 e #125 foram escritos pra evitar.
            ViewBag.AgendadasNoTorneio = gradeDoTorneio.Count(p => p.Status == "Agendada");

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

            // O TETO DE GAMES DE CADA JOGO AO VIVO, calculado aqui e entregue pronto pra tela
            // (21/08/2026, pedido do Felipe: "está permitindo marcar mais do que 9 num torneio
            // que é até 9"). O `max` do input era 99 cravado.
            //
            // ⚠️ O CÁLCULO NÃO PODE IR PRA VIEW nem pro JavaScript: a régua tem soma × "até",
            // o desempate do "vencer por dois" e teto POR LADO (Services/FormatoDaPartida) —
            // é a régua que existe justamente porque o `limiteGames: 9` já viveu cravado no JS.
            // Aqui só se PERGUNTA a ela. O servidor continua sendo a palavra final no POST.
            var tetos = new Dictionary<int, (int Lado1, int Lado2)>();
            if (torneioDaTela != null)
            {
                foreach (var p in (List<Partida>)ViewBag.AoVivo)
                {
                    var f = FormatoDaPartida.De(torneioDaTela, p.Fase);
                    int g1 = p.GamesDupla1 ?? 0, g2 = p.GamesDupla2 ?? 0;
                    tetos[p.Id] = (FormatoDaPartida.TetoDoLado(f, g1, g2), FormatoDaPartida.TetoDoLado(f, g2, g1));
                }
            }
            ViewBag.TetoDeGames = tetos;

            // Quem organiza vê os botões de mexer no jogo ("colocar no ar", editar placar).
            // Fica FORA do if do Americano de propósito: nasceu lá dentro, quando só o
            // desempate precisava dele, e o resultado era que num torneio de duplas — a
            // maioria — a flag nem existia, e o organizador não via botão nenhum.
            // O MARCADOR vê os mesmos botões: tudo que esta flag liga na tela de jogos
            // (colocar no ar, placar, W.O., trocar quadra/horário, refazer grade, salvar em
            // lote) é exatamente o trabalho dele — e cada POST confere de novo no servidor.
            ViewBag.EhOrganizador = await PodeOperarODiaDeJogoAsync(torneioId, ObterJogadorIdLogado() ?? 0);

            // Torneio por ordem de liberação não tem horário pra recalcular — o servidor já
            // recusava, mas só DEPOIS do clique, e a recusa voltava como faixa vermelha em cima
            // da tela. Oferecer o botão e negá-lo em seguida é fazer o organizador descobrir a
            // regra pelo erro.
            ViewBag.SemHorarioPrevisto = torneioDaTela?.SemHorarioPrevisto == true;

            // OS HORÁRIOS QUE O "DEFINIR HORÁRIO" OFERECE (10/09/2026, Services/HorariosDaGrade).
            // 🗣️ *"ao alterar o horario, deixe para que fique mais facil seguindo a ordem padrão do
            // jogo (nesse torneio é de 50 em 50 min)"* — em vez de um relógio em branco, a lista
            // dos horários que este torneio usa, com quantas quadras sobram em cada um.
            //
            // ⚠️ DO TORNEIO INTEIRO (`jogosDoTorneio`), e não da lista filtrada da tela: o modal é
            // um só e serve qualquer linha, e a ocupação de um horário conta os jogos de TODAS as
            // categorias — contar só as que passaram no filtro diria "3 quadras livres" onde não
            // há nenhuma. Os horários das prévias entram por `tambem`: elas têm hora, são
            // remarcáveis, e não são jogo no banco.
            ViewBag.SlotsDaGrade = torneioDaTela == null
                ? new List<HorariosDaGrade.Slot>()
                : HorariosDaGrade.Montar(torneioDaTela, jogosDoTorneio, sedes,
                    projetados.Select(j => j.Horario));

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
                //
                // ⚠️ `todasAsPartidas`, e não a lista da tela: quem APARECE na tabela é a grade
                // inteira (a dupla existe desde o sorteio, zerada), e a lista `partidas` já veio
                // filtrada por time/categoria/"só meus jogos" — a classificação de um recorte é
                // a classificação de outro torneio.
                var doAmericano = todasAsPartidas.Where(p => p.Fase.StartsWith("Americano")).ToList();
                var finalizadas = doAmericano.Where(p => p.Status == "Finalizada");

                ViewBag.ClassificacaoAmericanoDuplas = categoriasDoTorneio.ToDictionary(
                    c => c.Nome,
                    c => TabelaDoAmericanoDeDuplas.Montar(
                        finalizadas.Where(p => p.CategoriaId == c.Id),
                        doAmericano.Where(p => p.CategoriaId == c.Id)
                            .SelectMany(p => new[] { p.Dupla1, p.Dupla2 })
                            .Where(d => d != null)));

                ViewBag.DesempateAmericano = torneioDaTela.DesempateAmericano;
            }

            // PALPITRÔMETRO: resumo de votos de cada partida exibida, num único lote.
            int? meuId = User.Identity?.IsAuthenticated == true
                ? int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
                : null;
            ViewBag.MeuId = meuId;
            ViewBag.Palpites = await _palpites.ObterResumosAsync(partidas.Select(p => p.Id), meuId);

            // QUEM MARCA PLACAR: a escolha do organizador (Services/QuemMarcaOPlacar) chega
            // às listas pra desenhar o lápis de quem a régua liberar — e cada POST confere
            // tudo de novo no servidor (PartidasController). "Sou inscrito?" só é perguntado
            // ao banco no modo Inscritos, que é o único em que a resposta muda algo; nos
            // outros a régua da view decide por MeuId e pelas duplas já carregadas.
            ViewBag.QuemMarcaPlacar = torneioDaTela?.QuemMarcaPlacar;
            ViewBag.EhInscritoDoTorneio = meuId != null
                && torneioDaTela?.QuemMarcaPlacar == QuemMarcaOPlacar.Inscritos
                && (await _context.Duplas.AnyAsync(d =>
                        d.Categoria.TorneioId == torneioId && !d.EmListaDeEspera
                        && (d.Jogador1Id == meuId || d.Jogador2Id == meuId))
                    || await _context.InscricoesAmericanas.AnyAsync(i =>
                        i.Categoria.TorneioId == torneioId && !i.EmListaDeEspera
                        && i.JogadorId == meuId)
                    || await _context.Jogadores.AnyAsync(j => j.Id == meuId && j.IsAssistente));


            // O NÚMERO de cada jogo dentro da fase dele ("Quartas de Final 2"). Os jogos que
            // ainda vão acontecer se descrevem citando esse número ("Vencedor Quartas de
            // Final 2"), e sem ele na etiqueta do jogo REAL a referência apontaria pro nada.
            //
            // A ordem é a de Id — a mesma que o avanço de verdade usa pra parear vencedores, e a
            // mesma chave da reserva de horário (Services/ReservasDeHorario).
            //
            // ⚠️ A GRADE INTEIRA, e não a lista da tela nem `todasAsPartidas`: a Semifinal 2
            // continua sendo a 2 quando a Semifinal 1 sumiu por QUALQUER filtro. `todasAsPartidas`
            // não serve porque o filtro de TIME já a recortou em SQL, e ele tira jogos de dentro
            // da mesma categoria e fase — que é exatamente o que renumera.
            ViewBag.NumeroNaFase = ReservasDeHorario.NumeroNaFase(
                gradeDoTorneio.Select(p => (p.Id, p.CategoriaId, p.Fase)));
        }
    }
}
