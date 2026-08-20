using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// A régua dos marcos, pura: quais totais valem comemoração, e o texto de cada uma.
//
// A régua é EXATA (total == marco), e é isso que dispensa tabela de controle: o total de
// jogos/vitórias só passa por cada número UMA vez subindo. Correção de placar pode até fazer
// o total revisitar um marco — mas quem chama só confere marco quando o jogo ACABOU DE
// TERMINAR (acabouDeTerminar), nunca em correção, então o aviso não repete.
public static class MarcoPessoal
{
    // Os números que valem festa. A 1ª vitória fica de fora de propósito: ela já tem o
    // próprio aviso ("Vitória!") e comemorar "1ª vitória" pra quem acabou de ganhar o
    // primeiro jogo de grupo diria pouco — os marcos começam onde a contagem vira história.
    public static readonly int[] DeVitorias = { 10, 25, 50, 100, 150, 200, 300, 400, 500 };
    public static readonly int[] DeJogos = { 25, 50, 100, 150, 200, 300, 400, 500 };

    // O marco que este total de vitórias/jogos atingiu AGORA — nulo na esmagadora maioria.
    //
    // ⚠️ VITÓRIA GANHA DE JOGO quando os dois caem juntos (a 50ª vitória pode ser o 100º
    // jogo): dois pushes de festa no mesmo segundo viram ruído, e vitória é a notícia maior.
    public static TextoDoAviso? Da(int totalJogos, int totalVitorias, bool venceu)
    {
        if (venceu && DeVitorias.Contains(totalVitorias))
            return new("Marco pessoal! 🎉",
                $"Essa foi sua {totalVitorias}ª vitória em torneios no Padelizou. Bora pra próxima!");

        if (DeJogos.Contains(totalJogos))
            return new("Marco pessoal! 🎾",
                $"Você completou {totalJogos} jogos de torneio no Padelizou. Que campanha!");

        return null;
    }
}

// Confere, no fim de cada jogo, se algum dos quatro cruzou um marco — e comemora com ele.
// É o aviso mais raro do sistema (cada jogador cruza cada número uma vez na vida), e é
// exatamente isso que o faz funcionar: festa rara é festa.
public class AvisoDeMarcoPessoal
{
    private readonly DbPadelContext _context;
    private readonly IPushNotificationService _push;

    public AvisoDeMarcoPessoal(DbPadelContext context, IPushNotificationService push)
    {
        _context = context;
        _push = push;
    }

    public async Task ConferirAsync(Partida partida)
    {
        if (partida.VencedorId == null || partida.TorneioId == null) return;

        var duplas = await _context.Duplas
            .Where(d => d.Id == partida.Dupla1Id || d.Id == partida.Dupla2Id)
            .ToListAsync();

        foreach (var dupla in duplas)
        {
            // Linha de TIME não tem quem comemorar: o Jogador1 é quem cadastrou o time.
            if (dupla.NomeTime != null) continue;
            bool venceu = dupla.Id == partida.VencedorId;

            foreach (var id in new[] { (int?)dupla.Jogador1Id, dupla.Jogador2Id })
            {
                if (id == null) continue;
                await ConferirJogadorAsync(id.Value, venceu);
            }
        }
    }

    private async Task ConferirJogadorAsync(int jogadorId, bool venceu)
    {
        // As duplas do jogador primeiro, a contagem depois — é a mesma conta em qualquer
        // tela: jogo de TORNEIO, finalizado e com vencedor. Linha de time fica fora (lá o
        // Jogador1 é o organizador).
        var minhasDuplas = await _context.Duplas
            .Where(d => d.NomeTime == null
                     && (d.Jogador1Id == jogadorId || d.Jogador2Id == jogadorId))
            .Select(d => d.Id)
            .ToListAsync();
        if (minhasDuplas.Count == 0) return;

        var totalJogos = await _context.Partidas.CountAsync(p =>
            p.TorneioId != null && p.Status == "Finalizada" && p.VencedorId != null
            && (minhasDuplas.Contains(p.Dupla1Id) || minhasDuplas.Contains(p.Dupla2Id)));

        var totalVitorias = await _context.Partidas.CountAsync(p =>
            p.TorneioId != null && p.Status == "Finalizada" && p.VencedorId != null
            && minhasDuplas.Contains(p.VencedorId.Value));

        var marco = MarcoPessoal.Da(totalJogos, totalVitorias, venceu);
        if (marco == null) return;

        // Festa é bilhete social: bom de ver, ninguém age por causa dele — AppSemEmail.
        await _push.EnviarParaJogadorAsync(jogadorId, marco.Titulo, marco.Corpo,
            $"/Jogadores/Perfil/{jogadorId}", AlcanceDoAviso.AppSemEmail);
    }
}
