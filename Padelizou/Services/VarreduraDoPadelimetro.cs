using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// O JOGO QUE FICOU FORA DO PADELÍMETRO ENTRA DEPOIS, SOZINHO (13/11/2026).
//
// 🕳️ O Padelímetro é aplicado DEPOIS de o placar ser gravado (`EncerramentoDaPartida`, passo 1,
// num try/catch — falha ali não pode travar a Mesa no meio do torneio). Um restart no meio (o
// mesmo que deixou a chave do ER sem montar em 12/09/2026, ver VarreduraDaChave) ou uma exceção
// deixava o jogo FORA do número dos quatro, pra sempre e em silêncio. O comentário do
// encerramento dizia "o replay do admin reconstrói qualquer jogo perdido" — e reconstrói, mas
// só se alguém SOUBER que precisa. Ninguém sabia.
//
// ✅ NENHUMA RÉGUA NOVA, igual à VarreduraDaChave: chama o MESMO `AplicarAsync`, que já decide o
// que conta (`PadelimetroService.Conta`) e já é guardado — partida com linha no extrato não
// aplica de novo. O que muda é tentar de novo.
//
// ⏱️ A JANELA:
//   - de 10 MINUTOS pra trás: o que terminou agora é do encerramento, que pode estar no meio do
//     trabalho; disputar com ele seria o caminho pra aplicar duas vezes (o extrato não tem índice
//     único por partida).
//   - até 48 HORAS: aplicar um jogo mais velho poria ele DEPOIS de outros que vieram depois dele,
//     e o Elo depende da ordem. Isso é trabalho do replay do admin, que refaz tudo na ordem.
//   - "quando terminou" é o `HorarioFimReal` ou, sem ele, o `PlacarMarcadoEm`: o restart pode
//     cair antes de o encerramento carimbar a hora de fim (o passo 0 dele).
//
// 🔒 Com a trava do torneio (a mesma do encerramento e da VarreduraDaChave), um jogo por vez, na
// ordem em que terminaram.
public class VarreduraDoPadelimetro
{
    public static readonly TimeSpan EsperaMinima = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan JanelaMaxima = TimeSpan.FromHours(48);

    private readonly DbPadelContext _context;
    private readonly IPadelimetroService _padelimetro;
    private readonly ILogger<VarreduraDoPadelimetro> _logger;

    // ⚠️ PELA INTERFACE: o contêiner só conhece `IPadelimetroService`. Pedir a classe concreta
    // compila e passa na suíte inteira — e quebra no CI, no passo que valida o contêiner (a lição
    // da VarreduraDaChave com o RoboDoChaveamento).
    public VarreduraDoPadelimetro(DbPadelContext context, IPadelimetroService padelimetro,
        ILogger<VarreduraDoPadelimetro> logger)
    {
        _context = context;
        _padelimetro = padelimetro;
        _logger = logger;
    }

    // Uma passada: aplica o que ficou pra trás e devolve as partidas que ENTRARAM no número agora.
    // Lista vazia é o normal — cada partida que aparece aqui é uma entrega que se perdeu antes.
    public async Task<List<int>> PassarAsync(DateTime agora, CancellationToken ct)
    {
        var aplicadas = new List<int>();
        foreach (var candidata in await Candidatas(agora).ToListAsync(ct))
        {
            if (ct.IsCancellationRequested) break;

            using var trava = await EncerramentoDaPartida.UmDeCadaVezPorTorneioAsync(candidata.TorneioId);
            try
            {
                // Dentro da trava, de novo: o encerramento pode ter terminado enquanto esperávamos.
                if (await _context.HistoricosDePadelimetro.AnyAsync(h => h.PartidaId == candidata.Id, ct)) continue;

                await _padelimetro.AplicarAsync(candidata.Id);

                // Só entra na lista o que de fato virou número: partida que não conta (restrito,
                // W.O., time...) passa por aqui e sai sem extrato, como saiu no encerramento.
                if (await _context.HistoricosDePadelimetro.AnyAsync(h => h.PartidaId == candidata.Id, ct))
                {
                    aplicadas.Add(candidata.Id);
                    _logger.LogWarning(
                        "Varredura do Padelímetro: a partida {PartidaId} (torneio {TorneioId}) tinha ficado fora do "
                        + "número — a aplicação do encerramento se perdeu. Aplicada agora.",
                        candidata.Id, candidata.TorneioId);
                }
            }
            catch (Exception ex)
            {
                // Uma partida torta não pode travar as outras.
                _logger.LogError(ex, "Varredura do Padelímetro falhou na partida {PartidaId}", candidata.Id);
            }
        }
        return aplicadas;
    }

    // Finalizadas, com placar, dentro da janela, SEM linha no extrato — na ordem em que terminaram.
    // Pública pra conferir a tradução no Postgres (VarreduraDoPadelimetroTests).
    public IQueryable<Candidata> Candidatas(DateTime agora)
    {
        var de = agora - JanelaMaxima;
        var ate = agora - EsperaMinima;
        return _context.Partidas
            .Where(p => p.Status == "Finalizada"
                        && p.GamesDupla1 != null && p.GamesDupla2 != null
                        && (p.HorarioFimReal ?? p.PlacarMarcadoEm) >= de
                        && (p.HorarioFimReal ?? p.PlacarMarcadoEm) <= ate
                        && !_context.HistoricosDePadelimetro.Any(h => h.PartidaId == p.Id))
            .OrderBy(p => p.HorarioFimReal ?? p.PlacarMarcadoEm)
            .ThenBy(p => p.Id)
            .Select(p => new Candidata(p.Id, p.Categoria.TorneioId));
    }

    public sealed record Candidata(int Id, int? TorneioId);
}
