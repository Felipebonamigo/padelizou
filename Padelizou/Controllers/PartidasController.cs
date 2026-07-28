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

        public PartidasController(
            DbPadelContext context,
            IPalpiteService palpites,
            IPushNotificationService pushService,
            ILogger<PartidasController> logger)
        {
            _context = context;
            _palpites = palpites;
            _pushService = pushService;
            _logger = logger;
        }

        private int? ObterJogadorIdLogado()
        {
            var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claim, out var id) ? id : null;
        }

        // Só quem organiza o torneio mexe no placar. Partida sem torneio (jogo avulso)
        // não tem dono definido, então fica com o mesmo critério: precisa estar logado.
        private async Task<bool> PodeControlarPlacarAsync(Partida partida)
        {
            var jogadorId = ObterJogadorIdLogado();
            if (jogadorId == null) return false;
            if (partida.TorneioId == null) return true;

            return await _context.TorneioOrganizadores
                .AnyAsync(o => o.TorneioId == partida.TorneioId && o.JogadorId == jogadorId);
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

            return View(partida);
        }

        // Push de "seu jogo é o próximo", disparado pelo FIM do jogo anterior — não por
        // relógio. Torneio atrasa, e um aviso preso ao horário previsto chegaria com o
        // jogador ainda almoçando, ou depois de ele já ter jogado. Quem sabe de verdade que
        // a quadra vagou é a partida que acabou de terminar nela.
        private async Task AvisarProximoDaQuadraAsync(Partida terminada)
        {
            if (terminada.TorneioId == null) return;

            try
            {
                var agendadas = await _context.Partidas
                    .Include(p => p.Dupla1)
                    .Include(p => p.Dupla2)
                    .Where(p => p.TorneioId == terminada.TorneioId && p.Status == "Agendada")
                    .ToListAsync();

                var proxima = AvisosDoDiaDeJogo.ProximaAposTerminar(terminada, agendadas);
                if (proxima == null) return;

                var url = Url.Action("Jogos", "Torneios", new { id = terminada.TorneioId });

                foreach (var jogadorId in AvisosDoDiaDeJogo.JogadoresDa(proxima))
                {
                    await _pushService.EnviarParaJogadorAsync(jogadorId,
                        "Seu jogo é o próximo!",
                        AvisosDoDiaDeJogo.CorpoDoProximo(proxima),
                        url);
                }

                proxima.AvisoProximoEnviadoEm = DateTime.Now;
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // O placar já está salvo e o mata-mata já avançou. Push é acessório.
                _logger.LogWarning(ex, "Falha ao avisar o próximo jogo da quadra depois da partida {PartidaId}.", terminada.Id);
            }
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
        public async Task<IActionResult> ColocarNoAr(int id)
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
            return partida.TorneioId.HasValue
                ? RedirectToAction("Jogos", "Torneios", new { id = partida.TorneioId.Value })
                : RedirectToAction("ControlePlacar", new { id });
        }

        // POST: Partidas/ControlePlacar/5
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ControlePlacar(int id, string status, int? gamesDupla1, int? gamesDupla2, string? nomeQuadra, string? linkTransmissao, bool aplicarLinkNaQuadra = false)
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

                // INTELIGÊNCIA ABSORVIDA: Define o Vencedor automaticamente pelo placar
                // (Se houver Sets, você pode adicionar a lógica de Sets aqui também)
                int vencedorId = (partida.GamesDupla1 > partida.GamesDupla2) ? partida.Dupla1Id : partida.Dupla2Id;
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
            // O GATILHO DA AUTOMAÇÃO MÁSTER
            // ====================================================================
            if (status == "Finalizada" && partida.TorneioId.HasValue)
            {
                if (FasesTorneio.EhFaseDeGrupos(partida.Fase))
                {
                    // Fim da Fase de Grupos -> Gera as Quartas/Semis
                    await VerificarEGerarMataMataAutomatico(partida.TorneioId.Value, partida.CategoriaId);
                }
                else if (ChaveamentoMataMata.ProximaFase(partida.Fase) != null) // Oitavas, Quartas ou Semifinal
                {
                    // Fim de um jogo de Mata-Mata -> Empurra o vencedor pra próxima fase
                    await ProcessarAvancoMataMataAutomatico(partida.CategoriaId, partida.TorneioId.Value, partida.Fase);
                }
                else if (partida.Fase.StartsWith("Americano"))
                {
                    // Fim de uma rodada do Torneio Americano -> se todas as rodadas da categoria
                    // já acabaram, gera a final automática com os 4 melhores individualmente
                    await VerificarEGerarFinalAmericano(partida.TorneioId.Value, partida.CategoriaId);
                }
                else if (partida.Fase == "Final")
                {
                    // Fim do Torneio -> Coroa os Campeões
                    var campeao = await _context.Duplas.FindAsync(partida.VencedorId);
                    if (campeao != null) campeao.UltimaFase = "Campeao";

                    var torneio = await _context.Torneios.FindAsync(partida.TorneioId);
                    if (torneio != null) torneio.Status = "Finalizado";

                    await _context.SaveChangesAsync();
                }

                // A quadra acabou de vagar: chama quem joga nela agora. Só na transição —
                // ver `acabouDeTerminar` lá em cima.
                if (acabouDeTerminar) await AvisarProximoDaQuadraAsync(partida);
            }

            return RedirectToAction("Jogos", "Torneios", new { id = partida.TorneioId });
        }

        // --- ROBÔ 1: GERA O INÍCIO DO MATA-MATA (Pós-Grupos) ---
        private async Task VerificarEGerarMataMataAutomatico(int torneioId, int categoriaId)
        {
            bool temJogoPendente = await _context.Partidas.AnyAsync(p =>
                p.TorneioId == torneioId && p.CategoriaId == categoriaId
                && (p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo ")) && p.Status != "Finalizada");

            if (temJogoPendente) return;

            bool mataMataJaGerado = await _context.Partidas.AnyAsync(p =>
                p.TorneioId == torneioId && p.CategoriaId == categoriaId
                && !(p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo ")));

            if (mataMataJaGerado) return;

            var torneio = await _context.Torneios.FindAsync(torneioId);
            var duplas = await _context.Duplas.Where(d => d.Categoria.TorneioId == torneioId && d.CategoriaId == categoriaId && d.Grupo != null).ToListAsync();
            var partidasGrupos = await _context.Partidas.Where(p => p.TorneioId == torneioId && p.CategoriaId == categoriaId
                && (p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo "))).ToListAsync();

            var ultimoJogo = partidasGrupos.OrderByDescending(p => p.HorarioPrevisto).FirstOrDefault();
            int tempoPartida = torneio.TempoPrevistoPartidaMinutos > 0 ? torneio.TempoPrevistoPartidaMinutos : 50;

            DateTime horarioAtual = DateTime.Now;
            if (ultimoJogo != null && ultimoJogo.HorarioPrevisto.HasValue)
            {
                horarioAtual = ultimoJogo.HorarioPrevisto.Value.AddMinutes(tempoPartida);
            }

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
            .GroupBy(c => c.Grupo).OrderBy(g => g.Key).ToList();

            // Motor único de chaveamento (mesmo do TorneiosController): funciona pra
            // QUALQUER nº de grupos — todos os 1ºs + melhores 2ºs completando o quadro.
            var classificados = new List<ChaveamentoMataMata.Classificado>();
            foreach (var grupo in classificacao)
            {
                var rankingDoGrupo = grupo.OrderByDescending(c => c.Vitorias).ThenByDescending(c => c.Saldo).ToList();
                for (int pos = 0; pos < rankingDoGrupo.Count && pos < 2; pos++)
                {
                    classificados.Add(new ChaveamentoMataMata.Classificado(
                        rankingDoGrupo[pos].Dupla.Id, grupo.Key ?? "?", rankingDoGrupo[pos].Vitorias, rankingDoGrupo[pos].Saldo, pos + 1));
                }
            }

            var (nomeFase, confrontos) = ChaveamentoMataMata.MontarPrimeiraFase(classificados);
            if (confrontos.Count == 0) return;

            var novasPartidas = new List<Partida>();
            foreach (var confronto in confrontos)
            {
                novasPartidas.Add(new Partida
                {
                    TorneioId = torneioId,
                    CategoriaId = categoriaId,
                    Dupla1Id = confronto.Dupla1Id,
                    Dupla2Id = confronto.Dupla2Id,
                    Fase = nomeFase,
                    Status = "Agendada",
                    HorarioPrevisto = horarioAtual,
                    Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper()
                });
                horarioAtual = horarioAtual.AddMinutes(tempoPartida);
            }

            _context.Partidas.AddRange(novasPartidas);
            await _context.SaveChangesAsync();
        }

        // --- ROBÔ 2: GERA O AVANÇO DAS CHAVES (Oitavas -> Quartas -> Semis -> Final) ---
        private async Task ProcessarAvancoMataMataAutomatico(int categoriaId, int torneioId, string faseConcluida)
        {
            var proximaFase = ChaveamentoMataMata.ProximaFase(faseConcluida);
            if (proximaFase == null) return;

            // Busca os vencedores da fase que acabou de jogar
            var vencedores = await _context.Partidas
                .Where(p => p.CategoriaId == categoriaId && p.Fase == faseConcluida && p.Status == "Finalizada")
                .OrderBy(p => p.Id)
                .Select(p => p.VencedorId!.Value)
                .ToListAsync();

            // Só avança com a fase completa, e nunca gera a próxima em duplicidade.
            if (vencedores.Count != ChaveamentoMataMata.JogosDaFase(faseConcluida)) return;
            if (await _context.Partidas.AnyAsync(p => p.CategoriaId == categoriaId && p.Fase == proximaFase)) return;

            foreach (var confronto in ChaveamentoMataMata.ParearVencedores(vencedores))
            {
                _context.Partidas.Add(new Partida
                {
                    TorneioId = torneioId,
                    CategoriaId = categoriaId,
                    Fase = proximaFase,
                    Status = "Agendada",
                    Dupla1Id = confronto.Dupla1Id,
                    Dupla2Id = confronto.Dupla2Id,
                    HorarioPrevisto = DateTime.Now.AddHours(2),
                    Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper()
                });
            }
            await _context.SaveChangesAsync();
        }

        // --- ROBÔ 3: TORNEIO AMERICANO — gera a final automática assim que todas as rodadas acabam ---
        private async Task VerificarEGerarFinalAmericano(int torneioId, int categoriaId)
        {
            bool temRodadaPendente = await _context.Partidas.AnyAsync(p =>
                p.TorneioId == torneioId && p.CategoriaId == categoriaId && p.Fase.StartsWith("Americano") && p.Status != "Finalizada");
            if (temRodadaPendente) return;

            bool finalJaGerada = await _context.Partidas.AnyAsync(p => p.CategoriaId == categoriaId && p.Fase == "Final");
            if (finalJaGerada) return;

            var partidas = await _context.Partidas
                .Include(p => p.Dupla1).Include(p => p.Dupla2)
                .Where(p => p.TorneioId == torneioId && p.CategoriaId == categoriaId && p.Fase.StartsWith("Americano"))
                .ToListAsync();

            if (partidas.Count == 0) return;

            var pontos = new Dictionary<int, int>();
            void Somar(int jogadorId, int games) => pontos[jogadorId] = pontos.GetValueOrDefault(jogadorId) + games;
            foreach (var p in partidas)
            {
                // No americano as duplas são sorteadas pelo sistema, então Jogador2Id nunca
                // é nulo aqui — mas a checagem evita quebrar se algum dado vier torto.
                Somar(p.Dupla1.Jogador1Id, p.GamesDupla1 ?? 0);
                if (p.Dupla1.Jogador2Id != null) Somar(p.Dupla1.Jogador2Id.Value, p.GamesDupla1 ?? 0);
                Somar(p.Dupla2.Jogador1Id, p.GamesDupla2 ?? 0);
                if (p.Dupla2.Jogador2Id != null) Somar(p.Dupla2.Jogador2Id.Value, p.GamesDupla2 ?? 0);
            }

            var top4 = pontos.OrderByDescending(kv => kv.Value).Take(4).Select(kv => kv.Key).ToList();
            if (top4.Count < 4) return; // não tem jogadores suficientes pra formar a final

            // Cruzamento: 1º colocado + 4º colocado x 2º colocado + 3º colocado
            var duplaFinal1 = new Dupla { CategoriaId = categoriaId, Jogador1Id = top4[0], Jogador2Id = top4[3] };
            var duplaFinal2 = new Dupla { CategoriaId = categoriaId, Jogador1Id = top4[1], Jogador2Id = top4[2] };
            _context.Duplas.Add(duplaFinal1);
            _context.Duplas.Add(duplaFinal2);
            await _context.SaveChangesAsync();

            _context.Partidas.Add(new Partida
            {
                TorneioId = torneioId,
                CategoriaId = categoriaId,
                Dupla1Id = duplaFinal1.Id,
                Dupla2Id = duplaFinal2.Id,
                Fase = "Final",
                Status = "Agendada",
                HorarioPrevisto = DateTime.Now.AddHours(2),
                Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper()
            });
            await _context.SaveChangesAsync();
        }
    }
}