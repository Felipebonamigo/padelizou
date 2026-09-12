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

            // SEGURANÇA: organizador, MARCADOR do torneio ou admin — a Mesa é exatamente o
            // posto de trabalho do marcador.
            if (!await PodeOperarODiaDeJogoAsync(id, userId)) return Forbid();

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
            // Onde cada jogo está acontecendo. A Mesa é a tela de quem chama pra quadra: num
            // torneio de dois clubes, sem isto o mesário vê os jogos do outro prédio misturados
            // aos dele. Ver Services/LugarDoJogo.
            ViewData[LugarDoJogo.ChaveNaTela] = await SedesDoTorneio.CarregarAsync(_context, id);
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
            int sets1, int sets2, long marcadoEm,
            // A CONTAGEM DO TIE-BREAK (12/09/2026). Nulos = fila gravada antes deste deploy (ou
            // Mesa de torneio sem tie-break): o placar de games continua entrando e o que está
            // gravado aqui fica. Zerar por ausência apagaria a contagem que está na quadra.
            int? pontosTieBreak1 = null, int? pontosTieBreak2 = null)
        {
            var partida = await _context.Partidas.FindAsync(partidaId);
            if (partida == null) return NotFound();
            if (partida.TorneioId == null || !await PodeOperarODiaDeJogoAsync(partida.TorneioId.Value, ObterJogadorIdLogado() ?? 0)) return Forbid();

            // O formato da FASE só é buscado quando o aparelho mandou ponto de tie-break: a Mesa
            // chama esta rota a cada toque, e uma consulta a mais por game é latência na quadra
            // — que é justamente o que o caminho offline-first existe pra não pagar.
            var formatoDaMesa = pontosTieBreak1 != null || pontosTieBreak2 != null
                ? FormatoDaPartida.De(await _context.Torneios.FindAsync(partida.TorneioId.Value), partida.Fase)
                : null;

            var resultado = PlacarDaMesa.Aplicar(partida, games1, games2, sets1, sets2,
                DateTimeOffset.FromUnixTimeMilliseconds(marcadoEm).LocalDateTime,
                pontosTieBreak1, pontosTieBreak2, formatoDaMesa);

            if (resultado.Aplicado)
            {
                await _context.SaveChangesAsync();
                // PLACAR AO VIVO: quem segue este jogo recebe a atualização. Só enfileira
                // (Services/AvisoDePlacarAoVivo) — não custa a latência da Mesa, que é o
                // ponto de partida deste caminho (offline-first, um toque por vez).
                await _avisoDePlacar.AvisarSeguidoresAsync(partidaId,
                    Url.Action("Details", "Torneios", new { id = partida.TorneioId }));
            }

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
                // A contagem volta junto: quando o servidor recusa (já tem placar mais novo), a
                // Mesa adota o estado dele — e o tie-break faz parte do placar.
                pontos1 = partida.PontosTieBreak1 ?? 0,
                pontos2 = partida.PontosTieBreak2 ?? 0,
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
            int id, int[] partidaId, int[] games1, int[] games2, string? voltarPara = null,
            // OS PONTOS DO TIE-BREAK (12/09/2026), array paralelo aos de cima — o card manda
            // sempre, inclusive escondido, pra não desalinhar os índices do lote (ver o
            // comentário em _JogosDoTorneio.cshtml).
            //
            // Nulo = tela aberta ANTES deste deploy: o placar de games continua salvando
            // normalmente e os pontos gravados ficam como estão. Tratar ausência como zero
            // apagaria um tie-break em andamento no primeiro toque de quem não recarregou.
            int[]? pontos1 = null, int[]? pontos2 = null)
        {
            if (!await PodeOperarODiaDeJogoAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            // Quem chamou por `fetch` (o −/+ do card, que não recarrega mais a página) leva o
            // placar APLICADO de volta em JSON. ⚠️ Isso não é enfeite: o servidor CORRIGE o
            // que recebeu — o teto da fase manda, e numa soma de 5 um "6" vira 4. Com a
            // recarga, a tela voltava do servidor já certa; sem ela, o número na tela ficaria
            // mentindo sobre o que foi gravado, e é justamente o número que o organizador usa
            // pra decidir se o jogo acabou.
            //
            // O tipo da resposta também é o SINAL DE SUCESSO do outro lado: sessão vencida
            // responde 302 pra tela de login, que o `fetch` segue e entrega como 200 — "ok"
            // sem ter salvo nada. Só JSON conta como salvo.
            bool porFetch = Request.Headers.XRequestedWith == "XMLHttpRequest";

            if (partidaId.Length == 0 || partidaId.Length != games1.Length || partidaId.Length != games2.Length)
            {
                if (porFetch) return BadRequest();
                TempData["Erro"] = "Não recebi os placares — tente de novo.";
                return VoltarPara(voltarPara, id);
            }

            var doTorneio = await _context.Partidas
                .Where(p => p.TorneioId == id && partidaId.Contains(p.Id))
                .ToListAsync();

            int mexidos = 0;
            // Só os jogos que REALMENTE mudaram — avisar quem segue os outros 4 do lote
            // seria acordar gente pra um placar que não se moveu.
            var partidasMudadas = new List<int>();
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

                // OS PONTOS DO TIE-BREAK. ⚠️ Só entram onde o tie-break PODE acontecer
                // (Services/TieBreakDoJogo): num torneio com a contagem desligada, ou numa fase
                // de número par, um `pontos1` montado à mão não grava nada.
                //
                // ⚠️ E SÓ COM O LOTE ALINHADO (12/09/2026): os pontos viajam em arrays
                // paralelos casados por ÍNDICE com `partidaId[]`, e array de tamanho diferente
                // não é "um pouco menos de dado" — é a contagem de um jogo gravada no outro,
                // calada. É a mesma trava que os games já tinham na entrada; a diferença é que
                // `i < pontos1.Length` aceitava array mais COMPRIDO, que é exatamente o que um
                // card com dois campos `pontos1` produzia (ver _JogosDoTorneio.cshtml).
                bool mexeuNosPontos = false;
                if (TieBreakDoJogo.PodeAcontecer(formato)
                    && pontos1 != null && pontos2 != null
                    && pontos1.Length == partidaId.Length && pontos2.Length == partidaId.Length)
                {
                    int p1 = TieBreakDoJogo.PontoValido(pontos1[i]);
                    int p2 = TieBreakDoJogo.PontoValido(pontos2[i]);

                    mexeuNosPontos = partida.PontosTieBreak1 != p1 || partida.PontosTieBreak2 != p2;
                    partida.PontosTieBreak1 = p1;
                    partida.PontosTieBreak2 = p2;
                }

                if (partida.GamesDupla1 == g1 && partida.GamesDupla2 == g2 && !mexeuNosPontos) continue;

                partida.GamesDupla1 = g1;
                partida.GamesDupla2 = g2;
                mexidos++;
                partidasMudadas.Add(partida.Id);
            }

            if (mexidos > 0)
            {
                await _context.SaveChangesAsync();

                // PLACAR AO VIVO: um aviso por jogo que de fato mudou, não um por linha do lote.
                foreach (var partidaMudadaId in partidasMudadas)
                    await _avisoDePlacar.AvisarSeguidoresAsync(partidaMudadaId,
                        Url.Action("Details", "Torneios", new { id }));
            }

            if (porFetch)
            {
                // O placar de CADA jogo como ele ficou gravado — inclusive o dos que não
                // mudaram e o dos que saíram do ar no meio (aí a tela para de oferecer o
                // número de um jogo que já acabou).
                // ⚠️ O TETO VIAJA JUNTO COM O PLACAR (21/08/2026), e é ele que impede a tela de
                // deixar marcar 12 num jogo até 9. O botão "+" NÃO pode calcular o limite
                // sozinho: a régua tem soma × "até", o desempate do "vencer por dois" e teto
                // POR LADO — reescrever isso em JavaScript seria a segunda cópia da regra, que
                // é exatamente como o `limiteGames: 9` cravado no JS sobreviveu tanto tempo.
                // O servidor calcula (FormatoDaPartida, uma régua só) e a tela só obedece.
                //
                // E vem A CADA resposta porque o teto MUDA com o placar: num jogo até 4, o
                // 3x3 estende o limite pra 5.
                return Json(new
                {
                    salvos = mexidos,
                    placares = doTorneio.Select(p =>
                    {
                        var f = FormatoDaPartida.De(torneio, p.Fase);
                        int g1 = p.GamesDupla1 ?? 0, g2 = p.GamesDupla2 ?? 0;
                        return new
                        {
                            partidaId = p.Id,
                            games1 = g1,
                            games2 = g2,
                            teto1 = FormatoDaPartida.TetoDoLado(f, g1, g2),
                            teto2 = FormatoDaPartida.TetoDoLado(f, g2, g1),
                            // E QUEM JÁ VENCEU — 1, 2 ou 0 pra "ainda tem jogo". É o verde do
                            // card (11/09/2026). Sem ele na resposta, a cor só mudaria no HTML
                            // da atualização automática, que NÃO roda com o cursor dentro do
                            // campo (`estaOcupado` em jogos-ao-vivo-atualiza.js): quem digitasse
                            // 9 e corrigisse pra 8 ficaria com o verde aceso do lado errado por
                            // tempo indeterminado. Mesma régua do teto, mesmo motivo de ela vir
                            // pronta do servidor.
                            vencedor = QuemVenceu.LadoJaDecidido(f, p.SetsDupla1, p.SetsDupla2, g1, g2) ?? 0,
                            aoVivo = p.Status == "AoVivo",
                            // E O TIE-BREAK (12/09/2026), pelo mesmo motivo do teto e do
                            // vencedor: "este jogo está em tie-break?" é pergunta de régua, e a
                            // resposta VIRA no instante em que o 9º game é escrito. Sem ela na
                            // resposta, o bloco "TIE-BREAK" ficaria em cima de um 9 x 8 já
                            // decidido até a próxima atualização automática — que nem roda com o
                            // cursor dentro de um campo (`estaOcupado`).
                            tieBreak = TieBreakNaResposta(f, p, g1, g2),
                        };
                    }),
                });
            }

            TempData["Sucesso"] = mexidos == 0
                ? "Nenhum placar mudou."
                : $"{mexidos} placar(es) salvos.";

            return VoltarPara(voltarPara, id);
        }

        // TUDO O QUE A TELA PRECISA SABER SOBRE O TIE-BREAK DESTE JOGO, respondido pelo servidor
        // (12/09/2026). Vai na resposta de cada salvamento pelo mesmo motivo do teto e do
        // vencedor: as três perguntas — "está em tie-break?", "já dá pra fechar?" e "com que
        // placar?" — são de régua (Services/TieBreakDoJogo), e a resposta VIRA no mesmo toque
        // que marca o ponto.
        //
        // ⚠️ Reescrever isso em JavaScript seria a segunda cópia da regra, que é como o
        // `limiteGames: 9` cravado no JS sobreviveu tanto tempo. Até a ETIQUETA vem daqui, pra
        // "tie-break 7-5" ter um formato só no projeto.
        private static object TieBreakNaResposta(FormatoDaPartida.Formato formato, Partida p, int games1, int games2)
        {
            bool emAndamento = TieBreakDoJogo.EmAndamento(formato, games1, games2);
            int pontos1 = p.PontosTieBreak1 ?? 0, pontos2 = p.PontosTieBreak2 ?? 0;

            // O placar do fechamento só existe DENTRO do tie-break: fora dele, um 7-5 guardado de
            // um jogo que já fechou não pode voltar a oferecer "fechar em 9x8".
            var fechamento = emAndamento
                ? TieBreakDoJogo.GamesAoFechar(formato, pontos1, pontos2)
                : null;

            return new
            {
                emAndamento,
                pontos1,
                pontos2,
                houve = TieBreakDoJogo.Houve(p.PontosTieBreak1, p.PontosTieBreak2),
                etiqueta = TieBreakDoJogo.Etiqueta(pontos1, pontos2),
                fecha1 = fechamento?.Games1,
                fecha2 = fechamento?.Games2,
            };
        }

        // Pra onde ir depois de encerrar. Quem finaliza pela MESA continua na Mesa; quem
        // finaliza pelo card da lista volta pra lista — mandá-lo pra Mesa seria despejá-lo
        // numa tela que ele não pediu, e ele teria que voltar pra chamar o próximo jogo.
        private IActionResult DepoisDeFinalizar(string? voltarPara, int? torneioId) =>
            string.IsNullOrEmpty(voltarPara) || torneioId == null
                ? RedirectToAction("MesaControle", new { id = torneioId })
                : VoltarPara(voltarPara, torneioId.Value);

        // `voltarPara` existe pra quem finaliza DE FORA da Mesa — o botão no card do Ao Vivo.
        // ⚠️ `games1`/`games2` são O PLACAR QUE ESTÁ NA TELA, e não decoração.
        //
        // O card do jogo ao vivo tem os números editáveis, mas eles pertencem ao formulário de
        // SALVAR EM LOTE — então quem digitava 6 x 1 e apertava Finalizar direto encerrava com
        // o que estava GRAVADO (0 x 1), e o próprio aviso de confirmação repetia o número
        // velho. O comentário da view dizia "o placar já foi salvo (cada toque no −/+ grava)",
        // premissa que envelheceu quando a edição virou em lote — e ninguém voltou aqui.
        //
        // Agora finalizar SIGNIFICA "encerra com o que estou vendo": o placar recebido é
        // gravado antes de decidir o vencedor. Nulo (tela antiga em cache, Mesa, fila offline)
        // mantém o comportamento de sempre — usa o que está no banco.
        //
        // ⚠️ Era a única ação de placar sem [HttpPost]/[Authorize] — respondia a GET e escapava
        // do carimbo antifalsificação global (que só vale para POST/PUT/PATCH/DELETE). E o
        // "partida.TorneioId != null" na checagem de baixo deixava passar sem autorização
        // nenhuma qualquer partida com TorneioId nulo — hoje nunca acontece (toda Partida nasce
        // de um torneio), mas a checagem tinha que RECUSAR esse caso, não pulá-lo.
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> FinalizarPartida(int partidaId, string? voltarPara = null,
            int? games1 = null, int? games2 = null)
        {
            // UM FINALIZAR DE CADA VEZ POR TORNEIO (ensaio do Er, 10/09/2026, anomalia C1 — o
            // porquê inteiro está em EncerramentoDaPartida.UmDeCadaVezPorTorneioAsync). A trava
            // vem ANTES de carregar a partida e segura até o último `return`: a guarda "já
            // finalizada" logo abaixo só enxerga o que a outra requisição gravou porque a carga
            // de baixo é a primeira leitura desta partida neste contexto — a chave sai de uma
            // projeção, que não rastreia nada.
            var torneioDaPartida = await _context.Partidas
                .Where(p => p.Id == partidaId)
                .Select(p => p.TorneioId)
                .FirstOrDefaultAsync();
            using var travaDoTorneio = await EncerramentoDaPartida.UmDeCadaVezPorTorneioAsync(torneioDaPartida);

            // Usando _context.Partidas (Plural)
            var partida = await _context.Partidas
                .Include(p => p.Dupla1)
                .Include(p => p.Dupla2)
                .FirstOrDefaultAsync(p => p.Id == partidaId);

            if (partida != null && (partida.TorneioId == null
                || !await PodeOperarODiaDeJogoAsync(partida.TorneioId.Value, ObterJogadorIdLogado() ?? 0)))
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

            // O placar da TELA entra antes de qualquer decisão — inclusive antes da recusa por
            // empate, senão o organizador seria barrado por um 0 x 1 que ele já tinha corrigido
            // para 6 x 1 na frente dele. Passa pelo mesmo PlacarValido do salvar em lote: o
            // teto da fase vale igual aqui, e numa soma de 7 um 5 x 5 não pode entrar por esta
            // porta só por ser a porta do "finalizar".
            if (partida != null && partida.Status == "AoVivo" && (games1.HasValue || games2.HasValue))
            {
                var formatoDaPartida = FormatoDaPartida.De(
                    await _context.Torneios.FindAsync(partida.TorneioId ?? 0), partida.Fase);

                var (g1, g2) = FormatoDaPartida.PlacarValido(formatoDaPartida,
                    games1 ?? partida.GamesDupla1 ?? 0,
                    games2 ?? partida.GamesDupla2 ?? 0);

                partida.GamesDupla1 = g1;
                partida.GamesDupla2 = g2;
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

                // PLACAR AO VIVO: manda o placar FINAL — a última atualização da notificação
                // que vinha trocando de conteúdo — e para de seguir (Services/AvisoDePlacarAoVivo).
                await _avisoDePlacar.AvisarFimEPararDeSeguirAsync(partida.Id,
                    Url.Action("Details", "Torneios", new { id = partida.TorneioId }));

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

        // ── PLACAR AO VIVO NA TELA DE BLOQUEIO ──────────────────────────────────────────────
        //
        // "Seguir este jogo": quem está de fora, sem abrir o app, quer o placar chegando
        // sozinho (Felipe, 16/08/2026 — "algo parecido com o placar que o Google mostra na
        // tela de bloqueio"). Ver Models/SeguidorDePartida e Services/AvisoDePlacarAoVivo.
        //
        // Só JSON, sem alternativa de formulário: o botão só existe pra quem já concedeu push,
        // e sem JavaScript a notificação nunca chegaria de qualquer jeito — não há "modo sem
        // JS" que faça sentido pra esta ação específica.
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> SeguirPartidaAoVivo(int partidaId)
        {
            var jogadorId = ObterJogadorIdLogado();
            if (jogadorId == null) return Forbid();

            // Só jogo EM QUADRA agora: seguir uma partida agendada ou já finalizada é uma
            // requisição velha de tela em cache, e não há placar mudando pra avisar.
            var aoVivo = await _context.Partidas
                .Where(p => p.Id == partidaId)
                .Select(p => p.Status == "AoVivo")
                .FirstOrDefaultAsync();
            if (!aoVivo) return NotFound();

            var jaSegue = await _context.Set<SeguidorDePartida>()
                .AnyAsync(s => s.JogadorId == jogadorId && s.PartidaId == partidaId);

            if (!jaSegue)
            {
                _context.Add(new SeguidorDePartida { JogadorId = jogadorId.Value, PartidaId = partidaId });
                await _context.SaveChangesAsync();
            }

            return Json(new { seguindo = true });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> PararDeSeguirPartidaAoVivo(int partidaId)
        {
            var jogadorId = ObterJogadorIdLogado();
            if (jogadorId == null) return Forbid();

            var seguindo = await _context.Set<SeguidorDePartida>()
                .Where(s => s.JogadorId == jogadorId && s.PartidaId == partidaId)
                .ToListAsync();

            if (seguindo.Count > 0)
            {
                _context.RemoveRange(seguindo);
                await _context.SaveChangesAsync();
            }

            return Json(new { seguindo = false });
        }

        // O CARD QUE A NOTIFICAÇÃO ABRE (12/09/2026).
        //
        // 🗣️ Felipe, com a notificação já chegando e dois prints na mão — a bolha de placar do
        // app do Google e o placar da Copa na Dynamic Island: *"as notificações estao
        // acontecendo, mas eu queria algo tipo esses prints, tem como ?"*. Os dois exigem app
        // NATIVO (ver Services/CartaoDoPlacarAoVivo); o que a notificação da web tem é a
        // `image`, e é este PNG.
        //
        // ⚠️ ABERTO, SEM `[Authorize]`, e de propósito: quem busca esta imagem é o NAVEGADOR ao
        // desenhar a notificação, não uma tela com sessão — exigir login aqui deixaria a
        // notificação sem imagem justamente em quem ela é pra alcançar. O placar ao vivo já é
        // público na página do torneio; aqui ele sai desenhado.
        //
        // ⚠️ TORNEIO OCULTO NÃO SAI, e nem pra quem ENXERGA o torneio (organizador, admin,
        // inscrito — os três escapes do VisibilidadeDoTorneio). A resposta é
        // `Cache-Control: public` — é ela que faz um desenho servir os N seguidores em vez de
        // um por pedido —, e resposta pública que muda conforme quem pede é exatamente como um
        // cache no caminho entrega o card de um torneio escondido pra quem não devia ver. O
        // teto: seguidor de torneio oculto recebe o aviso com o placar no texto, sem a imagem.
        [HttpGet]
        public async Task<IActionResult> CartaoDoPlacarAoVivo(int id, [FromServices] FonteDoCartao fontes)
        {
            // A recusa por falta de fonte vem ANTES de qualquer consulta, e é 404 e não uma
            // imagem em branco — mesma régua de todos os cards (ver FonteDoCartao).
            if (!fontes.Disponivel) return NotFound();

            var partida = await _context.Partidas
                .AsNoTracking()
                .Include(p => p.Categoria)
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
                .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
                .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (partida?.TorneioId == null) return NotFound();

            var torneio = await _context.Torneios.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == partida.TorneioId);
            if (torneio == null || torneio.Oculto) return NotFound();

            var contexto = string.Join(" · ", new[]
                {
                    CategoriaNaTela.Curto(partida.Categoria?.Nome),
                    partida.Fase,
                }.Where(t => !string.IsNullOrWhiteSpace(t)));

            // Qualificado inteiro porque a AÇÃO tem o mesmo nome do serviço — e o nome da ação
            // é o endereço que o push manda no `image`, então quem cede é a chamada, não a rota.
            var png = Services.CartaoDoPlacarAoVivo.Desenhar(new PlacarParaCard(
                // Os nomes saem da MESMA régua do texto da notificação — ver
                // AvisoDePlacarAoVivo.NomeDaDupla.
                AvisoDePlacarAoVivo.NomeDaDupla(partida.Dupla1),
                AvisoDePlacarAoVivo.NomeDaDupla(partida.Dupla2),
                partida.GamesDupla1 ?? 0,
                partida.GamesDupla2 ?? 0,
                Encerrado: partida.Status == "Finalizada",
                Contexto: contexto), fontes);

            return EntregaDeCard.Png(Response, png, $"placar-{partida.Id}.png");
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
            var hist = await _estatisticas.ObterTitulosPorCategoriaAsync(nomes, excluirTorneioId: torneioId);
            // Aceita nulo (dupla ainda sem parceiro) devolvendo 0 — a transmissão não pode
            // quebrar por causa de uma inscrição incompleta.
            int Titulos(int? jogadorId) => jogadorId != null && hist.TryGetValue(jogadorId.Value, out var porCategoria)
                ? porCategoria.Values.Sum(v => v.Titulos) : 0;

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
