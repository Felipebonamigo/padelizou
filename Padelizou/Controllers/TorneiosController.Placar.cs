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
    // Dia de jogo, quadra: Mesa de Controle, placar sincronizado (offline-first) e finalização com robôs.
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
            // O torneio inteiro porque a Mesa monta o limite de games de CADA jogo a partir
            // da fase dele (Services/FormatoDaPartida) — grupo até 4, final até 6.
            ViewBag.Torneio = await _context.Torneios.FindAsync(id);
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
        // SALVAR O PLACAR DE TODOS OS JOGOS AO VIVO DE UMA VEZ.
        //
        // Com 5 quadras rodando, marcar um game significava: abrir o Controle de Placar,
        // mexer, salvar, voltar, achar o próximo card, repetir. Cinco vezes, a cada game, a
        // noite inteira. Aqui os números são editados na própria lista e vão juntos num POST.
        //
        // A tela cheia continua existindo e não é redundante: é lá que se marca SET, quem
        // saca, quadra, link de transmissão e o FINALIZAR. Isto aqui é o atalho do que se
        // repete, não um substituto.
        //
        // ⚠️ Jogador não altera nada: a ação exige organizador (ou admin), e a tela nem
        // desenha os campos pra quem não é — oferecer um campo que o servidor vai recusar é
        // desleixo que o usuário paga.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarPlacaresAoVivo(
            int id, int[] partidaId, int[] games1, int[] games2, string? voltarPara = null)
        {
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            if (partidaId.Length == 0 || partidaId.Length != games1.Length || partidaId.Length != games2.Length)
            {
                TempData["Erro"] = "Não recebi os placares — tente de novo.";
                return VoltarPara(voltarPara, id);
            }

            var doTorneio = await _context.Partidas
                .Where(p => p.TorneioId == id && partidaId.Contains(p.Id))
                .ToListAsync();

            int mexidos = 0;
            for (int i = 0; i < partidaId.Length; i++)
            {
                var partida = doTorneio.FirstOrDefault(p => p.Id == partidaId[i]);

                // Só jogo EM QUADRA. Se ele saiu do ar entre a tela carregar e o salvar (outra
                // pessoa finalizou), o certo é não mexer: sobrescrever um placar já encerrado
                // é justamente o que faz resultado sumir sem ninguém entender.
                if (partida == null || partida.Status != "AoVivo") continue;

                // O formato da FASE manda (Services/FormatoDaPartida): num torneio até 4,
                // digitar 9 seria um placar que aquele jogo não pode ter — e numa soma de 7,
                // 5x5 também não.
                var formato = FormatoDaPartida.De(torneio, partida.Fase);
                var (g1, g2) = FormatoDaPartida.PlacarValido(formato, games1[i], games2[i]);

                if (partida.GamesDupla1 == g1 && partida.GamesDupla2 == g2) continue;

                partida.GamesDupla1 = g1;
                partida.GamesDupla2 = g2;
                mexidos++;
            }

            if (mexidos > 0) await _context.SaveChangesAsync();

            TempData["Sucesso"] = mexidos == 0
                ? "Nenhum placar mudou."
                : $"{mexidos} placar(es) salvos.";

            return VoltarPara(voltarPara, id);
        }

        // Pra onde ir depois de encerrar. Quem finaliza pela MESA continua na Mesa; quem
        // finaliza pelo card da lista volta pra lista — mandá-lo pra Mesa seria despejá-lo
        // numa tela que ele não pediu, e ele teria que voltar pra chamar o próximo jogo.
        private IActionResult DepoisDeFinalizar(string? voltarPara, int? torneioId) =>
            string.IsNullOrEmpty(voltarPara) || torneioId == null
                ? RedirectToAction("MesaControle", new { id = torneioId })
                : VoltarPara(voltarPara, torneioId.Value);

        // `voltarPara` existe pra quem finaliza DE FORA da Mesa — o botão no card do Ao Vivo.
        public async Task<IActionResult> FinalizarPartida(int partidaId, string? voltarPara = null)
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
                return DepoisDeFinalizar(voltarPara, partida.TorneioId);
            }

            // Sem placar não há vencedor. A conta antiga comparava os campos NULÁVEIS direto,
            // e `4 > null` é false em C# — então um jogo 4 x (em branco) saía com a dupla 2
            // vencedora, sem regra nenhuma por trás. Ver Services/QuemVenceu.
            if (partida != null && QuemVenceu.MotivoParaNaoFinalizar(partida) is { } motivo)
            {
                TempData["Erro"] = $"Jogo {partida.Codigo}: {motivo}";
                return DepoisDeFinalizar(voltarPara, partida.TorneioId);
            }

            if (partida != null)
            {
                partida.Status = "Finalizada";
                partida.SendoTransmitida = false;

                int vencedorId = QuemVenceu.Da(partida)!.Value;

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

                // ⚠️ DAQUI PRA FRENTE É O ENCERRAMENTO ÚNICO (Services/EncerramentoDaPartida):
                // Padelímetro, robôs de chaveamento, aviso de resultado e chamada do próximo
                // par da quadra. Esta tela tinha a própria versão disso, e faltavam nela a
                // final do Americano e o "seu jogo é o próximo" — que é justamente o aviso
                // que mais importa, disparado da tela que se usa no dia do torneio.
                await _encerramento.AplicarAsync(partida, acabouDeTerminar: true, LinksDoAviso(partida.TorneioId));
            }
            return DepoisDeFinalizar(voltarPara, partida?.TorneioId);
        }

        // Os endereços que o push do encerramento leva. Montar rota é trabalho do
        // controller; o serviço não carrega IUrlHelper só pra isso.
        private EncerramentoDaPartida.LinksDoAviso LinksDoAviso(int? torneioId) => new(
            Url.Action("Details", "Torneios", new { id = torneioId }),
            Url.Action("Jogos", "Torneios", new { id = torneioId }));

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
