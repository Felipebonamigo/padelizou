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
    // Dia de jogo, quadra: Mesa de Controle, placar sincronizado (offline-first) e finalizaÃ§Ã£o com robÃ´s.
    public partial class TorneiosController
    {
        // 1. TELA DA MESA DE CONTROLE (Onde o ajudante fica no celular)
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> MesaControle(int id)
        {
            // `!`: ação [Authorize], e o cookie sempre carrega o identificador (IdentidadeJogador.ClaimsDe).
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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

        // 1. SINCRONIZAR O PLACAR DA MESA (funciona offline)
        //
        // Recebe o placar INTEIRO, não o "+1" — era assim que o endpoint antigo trabalhava, e
        // incremento não sobrevive a fila offline: o mesmo toque reentregue dobra o game.
        // Placar absoluto reenviado dá sempre no mesmo lugar. `marcadoEm` é o relógio do
        // aparelho de quem marcou (epoch ms): entre dois placares vence o marcado por último
        // NA QUADRA, mesmo que o mais velho chegue depois por ter ficado sem sinal.
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> SincronizarPlacar(int partidaId, int games1, int games2,
            int sets1, int sets2, long marcadoEm)
        {
            var partida = await _context.Partidas.FindAsync(partidaId);
            if (partida == null) return NotFound();
            if (partida.TorneioId == null || !await EhOrganizadorAsync(partida.TorneioId.Value, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var resultado = PlacarDaMesa.Aplicar(partida, games1, games2, sets1, sets2,
                DateTimeOffset.FromUnixTimeMilliseconds(marcadoEm).LocalDateTime);

            if (resultado.Aplicado) await _context.SaveChangesAsync();

            // Recusa também responde 200 com o placar vigente: pro aparelho da fila, "o
            // servidor já tem coisa mais nova" é sucesso — pode esvaziar a fila em paz.
            return Json(new
            {
                aplicado = resultado.Aplicado,
                motivo = resultado.Motivo,
                games1 = partida.GamesDupla1,
                games2 = partida.GamesDupla2,
                sets1 = partida.SetsDupla1,
                sets2 = partida.SetsDupla2,
                finalizada = partida.Status == "Finalizada"
            });
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

            // Já finalizada = nada a fazer. A fila offline da Mesa pode reentregar o mesmo
            // "finalizar" (rede que cai entre o servidor aplicar e o aparelho confirmar), e
            // rodar isto duas vezes redispararia robôs de mata-mata e avisos.
            if (partida != null && partida.Status == "Finalizada")
            {
                return RedirectToAction("MesaControle", new { id = partida.TorneioId });
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
    }
}
