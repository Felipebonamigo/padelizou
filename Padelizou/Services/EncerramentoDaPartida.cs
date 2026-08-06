using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// TUDO QUE ACONTECE QUANDO UM JOGO ACABA — num lugar só, para as duas telas.
//
// ⚠️ Este arquivo é a segunda metade da correção de 06/08/2026. A primeira unificou o robô
// que monta a chave (Services/RoboDoChaveamento); auditando o resto do encerramento, as duas
// telas ainda divergiam em TRÊS pontos, cada um invisível de um lado:
//
//   • a TELA CHEIA (Controle de Placar) não movia o PADELÍMETRO — o nível dos 4 jogadores
//     simplesmente não mudava, e o extrato do perfil ficava sem a linha daquele jogo;
//   • a MESA / botão do card não gerava a FINAL DO AMERICANO — o torneio terminava as
//     rodadas e ficava parado, esperando uma final que nenhum robô ia criar;
//   • a MESA / botão do card não disparava o "SEU JOGO É O PRÓXIMO" — justamente a tela que
//     se usa no dia do torneio era a que deixava o próximo par sem aviso.
//
// Ou seja: o que acontecia no fim de um jogo dependia de por onde o placar foi lançado. É o
// mesmo defeito de sempre neste projeto (ver Services/QuemVenceu, AvancoDaChave,
// RoboDoChaveamento): a MESMA regra escrita em dois lugares, e os dois discordando.
//
// A partir daqui só existe um caminho. Quem finaliza chama isto e pronto.
public class EncerramentoDaPartida
{
    private readonly DbPadelContext _context;
    private readonly RoboDoChaveamento _robo;
    private readonly IPadelimetroService _padelimetro;
    private readonly IPushNotificationService _push;
    private readonly ILogger<EncerramentoDaPartida> _logger;

    public EncerramentoDaPartida(DbPadelContext context, IPadelimetroService padelimetro,
        IPushNotificationService push, ILogger<EncerramentoDaPartida> logger)
    {
        _context = context;
        _robo = new RoboDoChaveamento(context);
        _padelimetro = padelimetro;
        _push = push;
        _logger = logger;
    }

    // Os endereços que o push leva. Vêm de fora porque quem sabe montar rota é o controller;
    // este serviço não carrega IUrlHelper só pra isso.
    public record LinksDoAviso(string? DoTorneio, string? DaListaDeJogos);

    // `partida` já está com Status "Finalizada" e VencedorId gravados.
    // `acabouDeTerminar` separa "o jogo ACABOU agora" de "alguém corrigiu um placar antigo":
    // sem essa distinção, cada correção chamaria os jogadores do jogo seguinte de novo.
    public async Task AplicarAsync(Partida partida, bool acabouDeTerminar, LinksDoAviso links)
    {
        if (partida.VencedorId == null) return;

        int vencedorId = partida.VencedorId.Value;
        int perdedorId = vencedorId == partida.Dupla1Id ? partida.Dupla2Id : partida.Dupla1Id;

        // 1. PADELÍMETRO — o placar acabou de virar oficial, move o nível dos 4 jogadores
        //    (regras em RANKING.md; restrito/time/W.O. o serviço filtra). Em try/catch porque
        //    falha aqui não pode travar a Mesa no meio do torneio: o replay do admin
        //    reconstrói qualquer jogo perdido.
        try
        {
            await _padelimetro.AplicarAsync(partida.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Padelímetro falhou na partida {PartidaId}", partida.Id);
        }

        // 2. OS ROBÔS. A fase de grupos existe em duas grafias ("Fase de Grupos" nos seeds
        //    antigos, "Grupo A/B/..." no GerarChaves) — o gatilho aceita as duas, senão o
        //    mata-mata nunca é gerado.
        if (partida.TorneioId is int torneioId)
        {
            if (FasesTorneio.EhFaseDeGrupos(partida.Fase))
            {
                await _robo.MontarMataMataDosGruposAsync(partida.CategoriaId, torneioId);
            }
            else if (ChaveamentoMataMata.ProximaFase(partida.Fase) != null)
            {
                await _robo.AvancarFaseAsync(partida.CategoriaId, torneioId, partida.Fase);
            }
            else if (partida.Fase.StartsWith("Americano"))
            {
                await _robo.MontarFinalDoAmericanoAsync(partida.CategoriaId, torneioId);
            }
            else if (partida.Fase == "Final")
            {
                await CoroarCampeaoAsync(partida, vencedorId, torneioId);
            }
        }

        // 3. AVISOS. Nesta ordem: primeiro o resultado (pra quem jogou), depois a chamada do
        //    próximo — que só faz sentido quando o jogo REALMENTE acabou agora.
        await NotificarResultadoAsync(partida, vencedorId, perdedorId, links.DoTorneio);

        if (acabouDeTerminar) await AvisarProximoDaQuadraAsync(partida, links.DaListaDeJogos);
    }

    private async Task CoroarCampeaoAsync(Partida partida, int vencedorId, int torneioId)
    {
        var campeao = await _context.Duplas.FindAsync(vencedorId);
        if (campeao != null) campeao.UltimaFase = "Campeao";

        var torneio = await _context.Torneios.FindAsync(torneioId);
        if (torneio != null) torneio.Status = "Finalizado";

        await _context.SaveChangesAsync();
    }

    // Fim de jogo: avisa quem jogou e quem acompanha esses jogadores. É o momento em que o
    // app tem algo a dizer — antes disso a pessoa precisava abrir a tela pra descobrir.
    private async Task NotificarResultadoAsync(Partida partida, int vencedorId, int perdedorId, string? url)
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

            // Push é lido de relance: apelido identifica mais rápido que nome completo.
            string Nomes(Dupla d) => $"{d.Jogador1?.ComoChamar} e {d.Jogador2?.ComoChamar}";
            var placar = $"{partida.GamesDupla1}x{partida.GamesDupla2}";
            var ondeFoi = torneio != null ? $" · {torneio.Nome}" : "";
            bool ehFinal = partida.Fase == "Final";

            // Dupla incompleta não chega a jogar, mas o filtro protege o push de nulo.
            var idsVencedores = new[] { vencedora.Jogador1Id, vencedora.Jogador2Id }
                .Where(id => id != null).Select(id => id!.Value).ToArray();
            var idsPerdedores = new[] { perdedora.Jogador1Id, perdedora.Jogador2Id }
                .Where(id => id != null).Select(id => id!.Value).ToArray();

            foreach (var id in idsVencedores)
            {
                await _push.EnviarParaJogadorAsync(id,
                    ehFinal ? "🏆 Campeões!" : "Vitória!",
                    ehFinal
                        ? $"Vocês venceram a final{ondeFoi}!"
                        : $"Vocês venceram {Nomes(perdedora)} ({placar}){ondeFoi}.",
                    url);
            }

            foreach (var id in idsPerdedores)
            {
                await _push.EnviarParaJogadorAsync(id,
                    "Resultado do seu jogo",
                    $"{Nomes(vencedora)} venceu ({placar}){ondeFoi}.",
                    url);
            }

            // Quem SEGUE os jogadores fica sabendo — mas só do mata-mata. Num dia de torneio a
            // fase de grupos tem dezenas de jogos; avisar seguidor a cada um viraria spam e a
            // pessoa desligaria a notificação de vez.
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
                await _push.EnviarParaJogadorAsync(seguidorId,
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

    // Push de "seu jogo é o próximo", disparado pelo FIM do jogo anterior — não por relógio.
    // Torneio atrasa, e um aviso preso ao horário previsto chegaria com o jogador ainda
    // almoçando, ou depois de ele já ter jogado. Quem sabe de verdade que a quadra vagou é a
    // partida que acabou de terminar nela.
    private async Task AvisarProximoDaQuadraAsync(Partida terminada, string? url)
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

            foreach (var jogadorId in AvisosDoDiaDeJogo.JogadoresDa(proxima))
            {
                await _push.EnviarParaJogadorAsync(jogadorId,
                    "Seu jogo é o próximo!",
                    AvisosDoDiaDeJogo.CorpoDoProximo(proxima),
                    // O aviso mais importante do sistema: a pessoa está no clube, o jogo é
                    // agora, e ela não vai abrir e-mail. Se um só aviso valesse o WhatsApp,
                    // seria este.
                    url, AlcanceDoAviso.AppEWhatsApp);
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
}
