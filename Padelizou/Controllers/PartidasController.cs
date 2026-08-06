using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Controllers
{
    public class PartidasController : Controller
    {
        private readonly DbPadelContext _context;
        private readonly IPalpiteService _palpites;
        private readonly IPushNotificationService _pushService;
        private readonly ILogger<PartidasController> _logger;
        private readonly EncerramentoDaPartida _encerramento;

        public PartidasController(
            DbPadelContext context,
            IPalpiteService palpites,
            IPushNotificationService pushService,
            ILogger<PartidasController> logger,
            EncerramentoDaPartida encerramento)
        {
            _context = context;
            _palpites = palpites;
            _pushService = pushService;
            _logger = logger;
            _encerramento = encerramento;
        }

        private int? ObterJogadorIdLogado()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claim, out var id) ? id : null;
        }

        // Só quem organiza o torneio mexe no placar. Partida sem torneio (jogo avulso)
        // não tem dono definido, então fica com o mesmo critério: precisa estar logado.
        //
        // ⚠️ O ADMIN DA PLATAFORMA entra junto — e faltava só aqui. Todo o resto do sistema
        // (TorneiosController.EhOrganizadorAsync, DuplasController) já dava passagem a
        // IsAdminRaiz/IsAdminGeral, justamente pra socorrer organizador travado sem depender
        // dele. Este ponto ficou de fora, e o efeito era o pior possível: a Mesa de Controle
        // ABRIA pro admin (ela usa o outro critério), com todos os botões à vista, e aí
        // "Começar o jogo" e "Salvar placar" respondiam 403 — no meio do torneio, no clube,
        // com a quadra esperando.
        private async Task<bool> PodeControlarPlacarAsync(Partida partida)
        {
            var jogadorId = ObterJogadorIdLogado();
            if (jogadorId == null) return false;
            if (partida.TorneioId == null) return true;

            if (await _context.TorneioOrganizadores
                    .AnyAsync(o => o.TorneioId == partida.TorneioId && o.JogadorId == jogadorId))
                return true;

            return await _context.Jogadores
                .AnyAsync(j => j.Id == jogadorId && (j.IsAdminRaiz || j.IsAdminGeral));
        }

        // POST: Partidas/Votar — palpitrômetro (voto do jogador logado em quem vai ganhar a partida)
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Votar(int partidaId, int duplaId)
        {
            var jogadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            try
            {
                var resumo = await _palpites.RegistrarVotoAsync(partidaId, jogadorId, duplaId);
                return Json(new
                {
                    sucesso = true,
                    votosDupla1 = resumo.VotosDupla1,
                    votosDupla2 = resumo.VotosDupla2,
                    totalVotos = resumo.TotalVotos,
                    percentualDupla1 = resumo.PercentualDupla1,
                    percentualDupla2 = resumo.PercentualDupla2,
                    meuVotoDuplaId = resumo.MeuVotoDuplaId
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { sucesso = false, erro = ex.Message });
            }
        }

        // GET: Partidas/VerVotos — quem votou em quem no palpitrômetro (público, qualquer logado)
        [HttpGet]
        public async Task<IActionResult> VerVotos(int partidaId)
        {
            var votantes = await _palpites.ObterVotantesAsync(partidaId);
            return Json(votantes);
        }

        public IActionResult Index()
        {
            return View();
        }

        // GET: Partidas/ControlePlacar/5
        [Authorize]
        public async Task<IActionResult> ControlePlacar(int id)
        {
            var partida = await _context.Partidas
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
                .Include(p => p.Categoria)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (partida == null) return NotFound();
            if (!await PodeControlarPlacarAsync(partida)) return Forbid();

            ViewBag.Quadras = await _context.Quadras
                .Where(q => q.TorneioId == partida.TorneioId)
                .OrderBy(q => q.Nome)
                .ToListAsync();

            // Até quantos games vai ESTE jogo (a fase manda). A tela tinha max="9" cravado,
            // igual a Mesa tinha — o organizador configurava 4 e nada respeitava.
            var torneio = partida.TorneioId is int torneioId
                ? await _context.Torneios.FindAsync(torneioId)
                : null;
            ViewBag.Formato = FormatoDaPartida.De(torneio, partida.Fase);

            return View(partida);
        }

        // SUGESTÃO DE INÍCIO na tela: acabou o jogo da Quadra 3, a Quadra 3 já aparece com o
        // próximo dela e um botão de começar.
        //
        // Numa noite de 5 quadras e jogo curto, "achar o próximo jogo daquela quadra" era uma
        // busca visual numa lista de 80 linhas, a cada 11 minutos, cinco vezes. A quadra fica
        // parada enquanto o organizador procura — e é justamente o tempo que não sobra.
        //
        // A regra de QUAL é o próximo é a mesma do push (Services/AvisosDoDiaDeJogo): a
        // primeira agendada na mesma quadra. Uma segunda conta aqui e a tela sugeriria um jogo
        // diferente do que o jogador recebeu no celular.
        private async Task SugerirProximoDaQuadraAsync(Partida terminada)
        {
            if (terminada.TorneioId == null) return;

            var agendadas = await _context.Partidas
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2!)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2!)
                .Where(p => p.TorneioId == terminada.TorneioId && p.Status == "Agendada")
                .ToListAsync();

            var proxima = AvisosDoDiaDeJogo.ProximaNaQuadra(terminada, agendadas);
            if (proxima == null) return;

            TempData["ProximoJogoId"] = proxima.Id;
            TempData["ProximoJogoOnde"] = string.IsNullOrWhiteSpace(proxima.NomeQuadra)
                ? "A quadra vagou"
                : $"A {proxima.NomeQuadra} vagou";
            TempData["ProximoJogoQuem"] =
                $"{proxima.Dupla1.NomeDeExibicao} × {proxima.Dupla2.NomeDeExibicao}";
            TempData["ProximoJogoQuando"] =
                $"{proxima.Categoria?.Nome ?? proxima.Fase} · previsto {proxima.HorarioPrevisto:HH:mm}";
        }

        // POST: Partidas/ColocarNoAr/5
        //
        // Um clique pra começar a partida, direto da lista de Jogos. Existe porque no dia do
        // torneio o organizador tem 4 quadras virando ao mesmo tempo e gente esperando: abrir
        // a tela de placar, escolher o status e salvar, uma partida por vez, é atrito na hora
        // errada. O placar continua na tela de sempre — aqui só se dá a largada.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ColocarNoAr(int id, string? voltarPara = null)
        {
            var partida = await _context.Partidas.FindAsync(id);
            if (partida == null) return NotFound();
            if (!await PodeControlarPlacarAsync(partida)) return Forbid();

            // Idempotente: dois toques no botão (celular, dedo grande, 3G lento) não podem
            // zerar o cronômetro de uma partida que já começou.
            if (partida.Status != "AoVivo")
            {
                partida.Status = "AoVivo";
                partida.HorarioInicioReal ??= DateTime.Now;
                partida.HorarioFimReal = null;
                partida.SendoTransmitida = !string.IsNullOrEmpty(partida.LinkTransmissao);

                await _context.SaveChangesAsync();
                TempData["Sucesso"] = "Partida no ar!";
            }

            // Partida fora de torneio não tem lista de Jogos pra onde voltar.
            //
            // ⚠️ E quem veio da PÁGINA DO TORNEIO volta pra lá: mandar todo mundo pra
            // /Torneios/Jogos fazia as abas mãe (Inscritos, Grupos, Chaves) sumirem debaixo
            // do organizador — a mesma queixa que o salvar placar e o trocar quadra já
            // resolveram. Lista fechada de destinos: campo de formulário nunca vira
            // redirecionamento pra qualquer lugar.
            if (!partida.TorneioId.HasValue) return RedirectToAction("ControlePlacar", new { id });

            return voltarPara == "Details"
                ? RedirectToAction("Details", "Torneios", new { id = partida.TorneioId.Value }, fragment: "jogosDoTorneio")
                : RedirectToAction("Jogos", "Torneios", new { id = partida.TorneioId.Value });
        }

        // DESFAZER o play: o jogo volta pra fila como se nunca tivesse sido chamado.
        // De celular, no balcão, com fila esperando, tocar no play do jogo de baixo acontece —
        // e até agora não tinha volta.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VoltarParaAgendado(int id)
        {
            var partida = await _context.Partidas.FindAsync(id);
            if (partida == null) return NotFound();
            if (!await PodeControlarPlacarAsync(partida)) return Forbid();

            if (DesfazerDoJogo.MotivoParaNaoVoltarParaAgendado(partida) is { } motivo)
            {
                TempData["Erro"] = motivo;
            }
            else
            {
                DesfazerDoJogo.VoltarParaAgendado(partida);
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = $"O jogo {partida.Codigo} voltou pra agendado — hora e quadra continuam as mesmas.";
            }

            return partida.TorneioId.HasValue
                ? RedirectToAction("Jogos", "Torneios", new { id = partida.TorneioId.Value })
                : RedirectToAction("ControlePlacar", new { id });
        }

        // REABRIR uma partida finalizada.
        //
        // ⚠️ Muito mais que trocar o status: finalizar carimba a fase do perdedor, move o
        // Padelímetro dos 4 jogadores e MANDA O ROBÔ CRIAR A FASE SEGUINTE quando aquele era o
        // último jogo da fase. Reabrir sem desfazer isso deixaria a categoria com uma
        // semifinal montada a partir de um resultado apagado.
        //
        // A recusa vem de Services/DesfazerDoJogo: se algo que veio depois já saiu do papel,
        // não se reabre — nesse ponto a correção virou decisão de quem organiza, e o caminho
        // certo é corrigir o placar.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReabrirPartida(int id)
        {
            var partida = await _context.Partidas.FindAsync(id);
            if (partida == null) return NotFound();
            if (!await PodeControlarPlacarAsync(partida)) return Forbid();

            var daCategoria = await _context.Partidas
                .Where(p => p.CategoriaId == partida.CategoriaId)
                .ToListAsync();

            if (DesfazerDoJogo.MotivoParaNaoReabrir(partida, daCategoria) is { } motivo)
            {
                TempData["Erro"] = motivo;
                return partida.TorneioId.HasValue
                    ? RedirectToAction("Jogos", "Torneios", new { id = partida.TorneioId.Value })
                    : RedirectToAction("ControlePlacar", new { id });
            }

            // 1. A fase que este resultado gerou some — ela era consequência dele.
            var geradosDepois = DesfazerDoJogo.GeradosDepois(partida, daCategoria);
            if (geradosDepois.Count > 0) _context.Partidas.RemoveRange(geradosDepois);

            // 2. O carimbo de eliminação do perdedor volta a ficar em aberto, e o campeão
            //    deixa de ser campeão (ele volta a ser só finalista).
            var perdedorId = partida.VencedorId == partida.Dupla1Id ? partida.Dupla2Id : partida.Dupla1Id;
            var envolvidas = await _context.Duplas
                .Where(d => d.Id == partida.Dupla1Id || d.Id == partida.Dupla2Id)
                .ToListAsync();
            foreach (var dupla in envolvidas)
            {
                // ⚠️ "Grupos", NÃO null: a coluna é NOT NULL no banco, e o valor neutro é o
                // padrão do modelo (Dupla.UltimaFase = "Grupos"), que quer dizer "ainda não
                // foi eliminada". Isto explodiu em produção no meio do torneio — o teste
                // passou porque o provedor EM MEMÓRIA não impõe NOT NULL. Regra pra levar
                // adiante: campo obrigatório se "limpa" voltando ao PADRÃO, não pra nulo.
                if (dupla.Id == perdedorId && !FasesTorneio.EhFaseDeGrupos(partida.Fase)) dupla.UltimaFase = "Grupos";
                if (dupla.Id == partida.VencedorId && dupla.UltimaFase == "Campeao") dupla.UltimaFase = "Final";
            }

            // 3. O Padelímetro desanda o que aplicou: some a linha do extrato e o nível dos 4
            //    volta pro que era. Como o desfazer acontece minutos depois do erro, esta é a
            //    última partida aplicada e a subtração é exata; se não for, o replay do admin
            //    reconstrói tudo do zero (PadelimetroService.RecalcularTudoAsync).
            var extrato = await _context.HistoricosDePadelimetro
                .Where(h => h.PartidaId == partida.Id).ToListAsync();
            if (extrato.Count > 0)
            {
                var porJogador = await _context.Jogadores
                    .Where(j => extrato.Select(h => h.JogadorId).Contains(j.Id))
                    .ToDictionaryAsync(j => j.Id);

                foreach (var linha in extrato)
                {
                    if (!porJogador.TryGetValue(linha.JogadorId, out var jogador)) continue;
                    jogador.Padelimetro = linha.NivelAntes;
                    jogador.JogosDePadelimetro = Math.Max(0, jogador.JogosDePadelimetro - 1);
                }

                _context.HistoricosDePadelimetro.RemoveRange(extrato);
            }

            // 4. O torneio deixa de estar finalizado se era esta final que o encerrava.
            if (partida.Fase == "Final" && partida.TorneioId is int torneioId)
            {
                var torneio = await _context.Torneios.FindAsync(torneioId);
                if (torneio != null && torneio.Status == "Finalizado") torneio.Status = "Fase de Grupos";
            }

            // 5. E aí sim o jogo volta pra quadra.
            partida.Status = "AoVivo";
            partida.VencedorId = null;
            partida.HorarioFimReal = null;
            partida.HorarioInicioReal ??= DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"O jogo {partida.Codigo} voltou pra AO VIVO. " +
                (geradosDepois.Count > 0
                    ? $"A fase seguinte ({geradosDepois.Count} jogo(s)) foi desfeita e nasce de novo quando este resultado for confirmado."
                    : "O placar continua na tela pra você corrigir.");

            return partida.TorneioId.HasValue
                ? RedirectToAction("Jogos", "Torneios", new { id = partida.TorneioId.Value })
                : RedirectToAction("ControlePlacar", new { id });
        }

        // POST: Partidas/ControlePlacar/5
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        // `voltarPara`: o organizador que veio da página do torneio volta pra ela. Sem isto,
        // mudar quadra ou qualquer informação aqui o despejava em /Torneios/Jogos — as abas
        // mãe (Inscritos, Grupos, Chaves) sumiam e ele achava que tinha perdido o caminho.
        public async Task<IActionResult> ControlePlacar(int id, string status, int? gamesDupla1, int? gamesDupla2, string? nomeQuadra, string? linkTransmissao, bool aplicarLinkNaQuadra = false, int? duplaSacandoId = null, string? voltarPara = null)
        {
            var partida = await _context.Partidas.FindAsync(id);
            if (partida == null) return NotFound();

            // Sem isto, qualquer um que alcançasse a rota mudava o placar de qualquer jogo.
            if (!await PodeControlarPlacarAsync(partida)) return Forbid();

            // Guardado ANTES de mexer no status: "acabou de terminar" é diferente de "está
            // terminada". Corrigir o placar de um jogo já encerrado é rotina no meio do
            // torneio, e sem esta distinção cada correção chamaria os jogadores da partida
            // seguinte de novo — e, pior, a seguinte da seguinte, porque a primeira já
            // estaria marcada como avisada.
            bool acabouDeTerminar = status == "Finalizada" && partida.Status != "Finalizada";

            partida.GamesDupla1 = gamesDupla1;
            partida.GamesDupla2 = gamesDupla2;
            partida.NomeQuadra = nomeQuadra;
            partida.LinkTransmissao = linkTransmissao;

            // ⚠️ SET VELHO NÃO DECIDE JOGO CORRIGIDO.
            //
            // Esta tela edita só GAMES — quem escreve sets é a Mesa de Controle
            // (Services/PlacarDaMesa). E o vencedor sai dos sets primeiro (QuemVenceu), então
            // corrigir "9x3" para "3x9" aqui trocava os games e deixava o set 1x0 antigo
            // mandando: o placar dizia uma coisa e o vencedor continuava sendo o outro.
            //
            // Foi o que deixou a FINAL do Mata-Mata Geral com o campeão errado no Interno de
            // 05/08/2026 (jogo 416: 6x4 com a vitória gravada pra quem fez 4).
            //
            // Quando o set contradiz o placar que acabou de ser salvo, ele está velho: sai de
            // cena e os games decidem. Set coerente fica onde está — jogo de 2 sets lançado
            // pela Mesa não é desfeito por uma passada aqui.
            if (QuemVenceu.PorSets(partida) is { } porSets && QuemVenceu.PorGames(partida) is { } porGames
                && porSets != porGames)
            {
                partida.SetsDupla1 = null;
                partida.SetsDupla2 = null;
            }

            // Quem saca só pode ser uma das duas duplas DESTE jogo. Vazio = não mostrar.
            // Sem essa checagem, um POST montado à mão apontaria a bolinha do saque pra uma
            // dupla de outra partida, e a tela mostraria um nome que não está em quadra.
            partida.DuplaSacandoId = duplaSacandoId == partida.Dupla1Id || duplaSacandoId == partida.Dupla2Id
                ? duplaSacandoId
                : null;

            // Aplica o link (e a quadra) a todos os PRÓXIMOS jogos da mesma quadra — a câmera
            // costuma cobrir a quadra o dia inteiro. Só toca jogos ainda não finalizados; os que
            // já aconteceram ficam com o link que já tinham (histórico preservado).
            if (aplicarLinkNaQuadra && !string.IsNullOrWhiteSpace(nomeQuadra) && partida.TorneioId.HasValue)
            {
                var proximosDaQuadra = await _context.Partidas
                    .Where(p => p.TorneioId == partida.TorneioId
                             && p.NomeQuadra == nomeQuadra
                             && p.Status != "Finalizada"
                             && p.Id != partida.Id)
                    .ToListAsync();

                foreach (var outra in proximosDaQuadra)
                {
                    outra.LinkTransmissao = linkTransmissao;
                    outra.SendoTransmitida = outra.Status == "AoVivo" && !string.IsNullOrEmpty(linkTransmissao);
                }
            }

            // Transição de Status e Cronômetro
            if (status == "AoVivo" && partida.Status != "AoVivo")
            {
                partida.HorarioInicioReal ??= DateTime.Now;
                partida.HorarioFimReal = null;
                partida.SendoTransmitida = !string.IsNullOrEmpty(linkTransmissao);
            }
            else if (status == "Finalizada" && partida.Status != "Finalizada")
            {
                partida.HorarioInicioReal ??= DateTime.Now;
                partida.HorarioFimReal = DateTime.Now;
                partida.SendoTransmitida = false;

                // Quem venceu sai de Services/QuemVenceu — a mesma função que a classificação
                // de grupos usa. A conta que morava aqui comparava os campos NULÁVEIS direto
                // e ignorava sets, então divergia das outras duas cópias da mesma pergunta.
                int vencedorId = QuemVenceu.Da(partida) ?? partida.Dupla1Id;
                partida.VencedorId = vencedorId;

                int perdedorId = (vencedorId == partida.Dupla1Id) ? partida.Dupla2Id : partida.Dupla1Id;

                // Carimba a fase em que o perdedor caiu (útil para o perfil do jogador depois).
                // Aceita as DUAS grafias de fase de grupos ("Fase de Grupos" e "Grupo A/B/...").
                if (!FasesTorneio.EhFaseDeGrupos(partida.Fase))
                {
                    var perdedor = await _context.Duplas.FindAsync(perdedorId);
                    if (perdedor != null) perdedor.UltimaFase = partida.Fase;
                }
            }
            // ⚠️ CORRIGIR O PLACAR DE UM JOGO JÁ FINALIZADO.
            //
            // Este ramo não existia, e era a raiz do "atualizei o placar errado e ele não
            // corrigiu o chaveamento". O vencedor só era calculado na TRANSIÇÃO pra
            // finalizada; depois disso, mexer no placar trocava os games e deixava o
            // `VencedorId` antigo de pé — e a fase seguinte, que nasceu daquele vencedor,
            // seguia com a dupla errada. No Interno de 05/08 isso deixou dois jogos de grupo
            // e a FINAL da chave geral com vencedor contradizendo o próprio placar.
            else if (status == "Finalizada" && partida.Status == "Finalizada")
            {
                var novoVencedor = QuemVenceu.Da(partida);
                if (novoVencedor != null && novoVencedor != partida.VencedorId)
                {
                    partida.VencedorId = novoVencedor;

                    var perdedorId = novoVencedor == partida.Dupla1Id ? partida.Dupla2Id : partida.Dupla1Id;
                    if (!FasesTorneio.EhFaseDeGrupos(partida.Fase))
                    {
                        var perdedor = await _context.Duplas.FindAsync(perdedorId);
                        if (perdedor != null) perdedor.UltimaFase = partida.Fase;

                        var novoDono = await _context.Duplas.FindAsync(novoVencedor);
                        if (novoDono != null && novoDono.UltimaFase == partida.Fase) novoDono.UltimaFase = "Grupos";
                    }

                    // A fase seguinte saiu do vencedor que acabou de mudar: ela não vale mais.
                    // Só dá pra refazer se ninguém entrou em quadra nela — senão apagaríamos
                    // um jogo em andamento, e aí a correção vira um estrago maior que o erro.
                    var daCategoria = await _context.Partidas
                        .Where(p => p.CategoriaId == partida.CategoriaId).ToListAsync();
                    var depois = DesfazerDoJogo.GeradosDepois(partida, daCategoria);

                    if (depois.Count > 0 && depois.All(p => p.Status == "Agendada"))
                    {
                        _context.Partidas.RemoveRange(depois);
                        TempData["Sucesso"] = "Placar corrigido: o vencedor mudou e a fase seguinte " +
                                              "foi refeita com a dupla certa.";
                    }
                    else if (depois.Count > 0)
                    {
                        TempData["Erro"] = "Placar corrigido e vencedor acertado, MAS a fase seguinte já " +
                                           "começou e não foi refeita — confira o chaveamento à mão.";
                    }
                }
            }
            else if (status == "Agendada")
            {
                partida.HorarioInicioReal = null;
                partida.HorarioFimReal = null;
                partida.VencedorId = null;
            }

            partida.Status = status;

            _context.Update(partida);
            await _context.SaveChangesAsync();

            // ====================================================================
            // O ENCERRAMENTO — O MESMO DA MESA DE CONTROLE
            // ====================================================================
            //
            // ⚠️ Padelímetro, robôs de chaveamento e avisos moram todos em
            // Services/EncerramentoDaPartida. Até 06/08/2026 cada tela tinha a própria
            // versão disto, e o que acontecia no fim de um jogo dependia de POR ONDE o
            // placar tinha sido lançado: esta aqui, por exemplo, não movia o Padelímetro —
            // o nível dos 4 jogadores não mudava e o extrato do perfil ficava sem a linha.
            if (status == "Finalizada" && partida.TorneioId.HasValue)
            {
                await _encerramento.AplicarAsync(partida, acabouDeTerminar,
                    new EncerramentoDaPartida.LinksDoAviso(
                        Url.Action("Details", "Torneios", new { id = partida.TorneioId }),
                        Url.Action("Jogos", "Torneios", new { id = partida.TorneioId })));

                // O banner "a quadra vagou" é estado de TELA (TempData), não de domínio —
                // por isso continua aqui e não no serviço.
                if (acabouDeTerminar) await SugerirProximoDaQuadraAsync(partida);
            }

            // Volta pra tela de onde o organizador veio (a página do torneio tem as abas mãe;
            // /Torneios/Jogos é só a lista). Lista fechada de destinos: campo de formulário
            // nunca pode virar redirecionamento pra qualquer lugar.
            return voltarPara == "Details"
                ? RedirectToAction("Details", "Torneios", new { id = partida.TorneioId }, fragment: "jogosDoTorneio")
                : RedirectToAction("Jogos", "Torneios", new { id = partida.TorneioId });
        }

    }
}