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

        public PartidasController(DbPadelContext context, IPalpiteService palpites)
        {
            _context = context;
            _palpites = palpites;
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

            ViewBag.Quadras = await _context.Quadras
                .Where(q => q.TorneioId == partida.TorneioId)
                .OrderBy(q => q.Nome)
                .ToListAsync();

            return View(partida);
        }

        // POST: Partidas/ControlePlacar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ControlePlacar(int id, string status, int? gamesDupla1, int? gamesDupla2, string? nomeQuadra, string? linkTransmissao)
        {
            var partida = await _context.Partidas.FindAsync(id);
            if (partida == null) return NotFound();

            partida.GamesDupla1 = gamesDupla1;
            partida.GamesDupla2 = gamesDupla2;
            partida.NomeQuadra = nomeQuadra;
            partida.LinkTransmissao = linkTransmissao;

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

                // Carimba a fase em que o perdedor caiu (útil para o perfil do jogador depois)
                if (!partida.Fase.StartsWith("Grupo"))
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
                if (partida.Fase.StartsWith("Grupo"))
                {
                    // Fim da Fase de Grupos -> Gera as Quartas/Semis
                    await VerificarEGerarMataMataAutomatico(partida.TorneioId.Value, partida.CategoriaId);
                }
                else if (partida.Fase == "Quartas de Final" || partida.Fase == "Semifinal")
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
            }

            return RedirectToAction("Jogos", "Torneios", new { id = partida.TorneioId });
        }

        // --- ROBÔ 1: GERA O INÍCIO DO MATA-MATA (Pós-Grupos) ---
        private async Task VerificarEGerarMataMataAutomatico(int torneioId, int categoriaId)
        {
            bool temJogoPendente = await _context.Partidas.AnyAsync(p =>
                p.TorneioId == torneioId && p.CategoriaId == categoriaId && p.Fase.StartsWith("Grupo") && p.Status != "Finalizada");

            if (temJogoPendente) return;

            bool mataMataJaGerado = await _context.Partidas.AnyAsync(p =>
                p.TorneioId == torneioId && p.CategoriaId == categoriaId && !p.Fase.StartsWith("Grupo"));

            if (mataMataJaGerado) return;

            var torneio = await _context.Torneios.FindAsync(torneioId);
            var duplas = await _context.Duplas.Where(d => d.Categoria.TorneioId == torneioId && d.CategoriaId == categoriaId && d.Grupo != null).ToListAsync();
            var partidasGrupos = await _context.Partidas.Where(p => p.TorneioId == torneioId && p.CategoriaId == categoriaId && p.Fase.StartsWith("Grupo")).ToListAsync();

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

            var primeirosColocados = new List<Dupla>();
            var segundosColocados = new List<Dupla>();

            foreach (var grupo in classificacao)
            {
                var rankingDoGrupo = grupo.OrderByDescending(c => c.Vitorias).ThenByDescending(c => c.Saldo).ToList();
                if (rankingDoGrupo.Count > 0) primeirosColocados.Add(rankingDoGrupo[0].Dupla);
                if (rankingDoGrupo.Count > 1) segundosColocados.Add(rankingDoGrupo[1].Dupla);
            }

            if (primeirosColocados.Count == 0 || primeirosColocados.Count != segundosColocados.Count) return;

            int totalJogos = primeirosColocados.Count;
            string nomeFase = totalJogos switch { 1 => "Final", 2 => "Semifinal", 4 => "Quartas de Final", 8 => "Oitavas de Final", _ => "Mata-Mata" };

            var novasPartidas = new List<Partida>();
            for (int i = 0; i < totalJogos; i++)
            {
                novasPartidas.Add(new Partida
                {
                    TorneioId = torneioId,
                    CategoriaId = categoriaId,
                    Dupla1Id = primeirosColocados[i].Id,
                    Dupla2Id = segundosColocados[totalJogos - 1 - i].Id,
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

        // --- ROBÔ 2: GERA O AVANÇO DAS CHAVES (Quartas -> Semis -> Final) ---
        private async Task ProcessarAvancoMataMataAutomatico(int categoriaId, int torneioId, string faseConcluida)
        {
            // Busca os vencedores da fase que acabou de jogar
            var vencedores = await _context.Partidas
                .Where(p => p.CategoriaId == categoriaId && p.Fase == faseConcluida && p.Status == "Finalizada")
                .Select(p => p.VencedorId.Value)
                .ToListAsync();

            // Só avança se TODOS os jogos daquela fase acabaram
            if (faseConcluida == "Quartas de Final" && vencedores.Count == 4)
            {
                // Verifica se já não foi gerado para evitar duplicação
                if (!await _context.Partidas.AnyAsync(p => p.CategoriaId == categoriaId && p.Fase == "Semifinal"))
                {
                    _context.Partidas.Add(new Partida { TorneioId = torneioId, CategoriaId = categoriaId, Fase = "Semifinal", Status = "Agendada", Dupla1Id = vencedores[0], Dupla2Id = vencedores[3], HorarioPrevisto = DateTime.Now.AddHours(2), Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper() });
                    _context.Partidas.Add(new Partida { TorneioId = torneioId, CategoriaId = categoriaId, Fase = "Semifinal", Status = "Agendada", Dupla1Id = vencedores[1], Dupla2Id = vencedores[2], HorarioPrevisto = DateTime.Now.AddHours(2), Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper() });
                }
            }
            else if (faseConcluida == "Semifinal" && vencedores.Count == 2)
            {
                if (!await _context.Partidas.AnyAsync(p => p.CategoriaId == categoriaId && p.Fase == "Final"))
                {
                    _context.Partidas.Add(new Partida { TorneioId = torneioId, CategoriaId = categoriaId, Fase = "Final", Status = "Agendada", Dupla1Id = vencedores[0], Dupla2Id = vencedores[1], HorarioPrevisto = DateTime.Now.AddHours(2), Codigo = Guid.NewGuid().ToString().Substring(0, 6).ToUpper() });
                }
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
                Somar(p.Dupla1.Jogador1Id, p.GamesDupla1 ?? 0);
                Somar(p.Dupla1.Jogador2Id, p.GamesDupla1 ?? 0);
                Somar(p.Dupla2.Jogador1Id, p.GamesDupla2 ?? 0);
                Somar(p.Dupla2.Jogador2Id, p.GamesDupla2 ?? 0);
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