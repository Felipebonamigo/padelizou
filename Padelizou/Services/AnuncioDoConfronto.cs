using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// O texto do confronto, puro — texto que precisa estar certo se testa (mesmo padrão do
// TextoDoApito).
public static class TextoDoConfronto
{
    public const string Titulo = "Confronto definido! 🎾";

    // "Semifinal · Copa de Verão: vocês contra Carlos e Dani. 24/08 às 14:30, Quadra 2."
    //
    // ⚠️ Uma frase só, sem quebra de linha — a notificação do celular corta o que passa de
    // duas linhas (mesma régua do TextoDoApito). Hora e quadra só entram quando existem:
    // torneio por ordem de liberação não tem grade, e "às 00:00, quadra a definir" é o tipo
    // de texto quebrado que a pessoa lembra do produto.
    public static string Corpo(string fase, string? torneio, string adversarios,
        DateTime? horario, string? quadra)
    {
        var onde = string.IsNullOrWhiteSpace(torneio) ? fase : $"{fase} · {torneio}";
        var frase = $"{onde}: vocês contra {adversarios}.";

        if (horario == null) return frase;

        frase += $" {horario.Value:dd/MM} às {horario.Value:HH:mm}";
        if (!string.IsNullOrWhiteSpace(quadra)) frase += $", {quadra}";
        return frase + ".";
    }
}

// A CHAMADA DA PRÓXIMA FASE: quando o robô monta uma rodada nova do mata-mata, os jogadores
// dela ficam sabendo contra quem (e quando) jogam — sem precisar abrir a chave de meia em
// meia hora.
//
// O anúncio sai no NASCIMENTO da fase, e não no fim de cada jogo, de propósito: o robô só
// cria a rodada quando a fase anterior INTEIRA fecha (ver RoboDoChaveamento), então é neste
// momento que os confrontos existem — e anunciar aqui cobre os dois lados de uma vez, quem
// acabou de vencer e quem já estava classificado esperando adversário. Um push por pessoa
// por fase; os robôs nunca criam a mesma fase duas vezes, então o anúncio também não repete.
public class AnuncioDoConfronto
{
    private readonly DbPadelContext _context;
    private readonly IPushNotificationService _push;

    public AnuncioDoConfronto(DbPadelContext context, IPushNotificationService push)
    {
        _context = context;
        _push = push;
    }

    // `novas` são as partidas que o robô ACABOU de criar. Vazio não faz nada — que é o caso
    // da maioria dos encerramentos (a fase ainda não fechou).
    public async Task AnunciarAsync(IReadOnlyList<Partida> novas, string? url)
    {
        if (novas.Count == 0) return;

        var duplaIds = novas.SelectMany(p => new[] { p.Dupla1Id, p.Dupla2Id }).Distinct().ToList();
        var duplas = await _context.Duplas
            .Include(d => d.Jogador1).Include(d => d.Jogador2)
            .Where(d => duplaIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id);

        var torneioNome = novas[0].TorneioId is int torneioId
            ? await _context.Torneios.Where(t => t.Id == torneioId).Select(t => t.Nome).FirstOrDefaultAsync()
            : null;

        foreach (var partida in novas)
        {
            var d1 = duplas.GetValueOrDefault(partida.Dupla1Id);
            var d2 = duplas.GetValueOrDefault(partida.Dupla2Id);

            await AvisarLadoAsync(partida, d1, d2, torneioNome, url);
            await AvisarLadoAsync(partida, d2, d1, torneioNome, url);
        }
    }

    private async Task AvisarLadoAsync(Partida partida, Dupla? lado, Dupla? adversario,
        string? torneioNome, string? url)
    {
        // Dupla-TIME não tem quem avisar: o Jogador1Id dela é o organizador que cadastrou o
        // time (mesma régua do AvisosDoDiaDeJogo.JogadoresDa).
        if (lado == null || lado.NomeTime != null) return;

        var corpo = TextoDoConfronto.Corpo(partida.Fase, torneioNome, NomeDoAdversario(adversario),
            partida.HorarioPrevisto, partida.NomeQuadra);

        foreach (var jogadorId in new[] { lado.Jogador1Id, lado.Jogador2Id })
        {
            if (jogadorId == null) continue;
            // Bilhete de chave: quem estava lá já vai ver na tela, e ninguém age agora por
            // causa dele — a régua do AppSemEmail.
            await _push.EnviarParaJogadorAsync(jogadorId.Value, TextoDoConfronto.Titulo, corpo,
                url, AlcanceDoAviso.AppSemEmail);
        }
    }

    // O nome do outro lado, do jeito que se lê de relance: apelido, time pelo nome do time, e
    // o caso de dado torto (dupla apagada no meio) vira "a definir" em vez de estourar.
    private static string NomeDoAdversario(Dupla? adversario)
    {
        if (adversario == null) return "adversário a definir";
        if (adversario.NomeTime != null) return adversario.NomeTime;

        var nomes = new[] { adversario.Jogador1?.ComoChamar, adversario.Jogador2?.ComoChamar }
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        return nomes.Count > 0 ? string.Join(" e ", nomes) : "adversário a definir";
    }
}
