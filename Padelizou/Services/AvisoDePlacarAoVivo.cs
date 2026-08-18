using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Avisa quem está SEGUINDO um jogo ao vivo que o placar mudou (16/08/2026, pedido do Felipe:
// "algo parecido com o placar que o Google mostra na tela de bloqueio"). Chamado depois de
// QUALQUER gravação de placar que pegou — Mesa de Controle (SincronizarPlacar) e a lista em
// lote (SalvarPlacaresAoVivo) escrevem o mesmo jogo por dois caminhos diferentes, e os dois
// precisam concordar; ficar num lugar só é o que garante isso (ver feedback sobre regra
// duplicada: a segunda cópia é sempre a que ninguém lembra de atualizar).
public class AvisoDePlacarAoVivo
{
    private readonly DbPadelContext _context;
    private readonly IPushNotificationService _push;

    public AvisoDePlacarAoVivo(DbPadelContext context, IPushNotificationService push)
    {
        _context = context;
        _push = push;
    }

    private static string TagDe(int partidaId) => $"partida-{partidaId}";

    // `url` é montado por quem chama: o serviço não carrega IUrlHelper só pra isso (mesmo
    // motivo do LinksDoAviso, em TorneiosController.Placar).
    public async Task AvisarSeguidoresAsync(int partidaId, string? url)
    {
        var seguidores = await _context.Set<SeguidorDePartida>()
            .Where(s => s.PartidaId == partidaId)
            .Select(s => s.JogadorId)
            .ToListAsync();

        // A consulta mais cara (a partida com as duas duplas carregadas) só roda quando existe
        // alguém pra avisar — a maioria dos jogos não tem seguidor nenhum, e cada game marcado
        // não pode custar um JOIN de quatro tabelas à toa.
        if (seguidores.Count == 0) return;

        var partida = await _context.Partidas
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla2).ThenInclude(d => d.Jogador2)
            .FirstOrDefaultAsync(p => p.Id == partidaId);

        if (partida == null) return;

        var (titulo, corpo) = Mensagem(partida);
        var tag = TagDe(partidaId);

        foreach (var jogadorId in seguidores)
            await _push.EnviarPlacarAoVivoAsync(jogadorId, titulo, corpo, url ?? "/", tag);
    }

    // O jogo acabou: manda o placar FINAL (a última atualização da notificação que vinha
    // trocando de conteúdo) e para de seguir — não há mais "ao vivo" pra acompanhar, e manter
    // a linha só acumularia lixo que nunca mais dispara nada.
    public async Task AvisarFimEPararDeSeguirAsync(int partidaId, string? url)
    {
        await AvisarSeguidoresAsync(partidaId, url);

        var seguidores = await _context.Set<SeguidorDePartida>()
            .Where(s => s.PartidaId == partidaId)
            .ToListAsync();

        if (seguidores.Count > 0)
        {
            _context.RemoveRange(seguidores);
            await _context.SaveChangesAsync();
        }
    }

    // A MESMA régua de nome curto do resto do sistema (chip do jogo, tabela de classificação)
    // — cortar no primeiro espaço aqui seria a segunda cópia, e no Americano ela erra (duas
    // Carolines, duas Natálias — ver Views/Torneios/_JogoEmLinha.cshtml).
    private static (string titulo, string corpo) Mensagem(Partida partida)
    {
        string Nome(Jogador? j) => j == null ? "?" : NomeBonito.Curto(j.Nome);

        string Dupla(Padelizou.Models.Dupla? d) => d == null ? "A definir"
            : d.EhTime ? (d.NomeTime ?? "Time")
            : $"{Nome(d.Jogador1)}/{Nome(d.Jogador2)}";

        var titulo = partida.Status == "Finalizada" ? "Jogo encerrado" : "Placar ao vivo";
        var corpo = $"{Dupla(partida.Dupla1)} {partida.GamesDupla1 ?? 0} x {partida.GamesDupla2 ?? 0} {Dupla(partida.Dupla2)}";

        return (titulo, corpo);
    }
}
