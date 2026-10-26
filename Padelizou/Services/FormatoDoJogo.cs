using Padelizou.Models;

namespace Padelizou.Services;

// Trocar o formato de UM jogo na hora — "a final vai ser set de 9" (26/10/2026).
//
// O formato em si é lido por FormatoDaPartida.De(torneio, partida); aqui ficam só as regras da
// troca: quando pode e o que se aceita gravar.
public static class FormatoDoJogo
{
    // Tetos do que se aceita gravar. Nenhum jogo de padel passa disso; o número existe pra um POST
    // montado à mão não gravar 99 sets e travar a Mesa.
    public const int MaxSets = 5;
    public const int MaxGames = 15;

    // Null = pode. Texto = o motivo, na língua de quem organiza.
    //
    // ⚠️ SÓ ANTES DO PRIMEIRO PONTO: com placar marcado, trocar o formato reinterpretaria o que já
    // foi lançado — um 6x4 que era fim de jogo viraria meio de set. "AoVivo" sem placar ainda vale:
    // é exatamente o "na hora de pôr o jogo começou".
    public static string? MotivoParaNaoAlterar(Partida? jogo, int torneioId)
    {
        if (jogo == null) return "Não encontrei o jogo.";
        if (jogo.TorneioId != torneioId) return "O jogo precisa ser deste torneio.";
        if (jogo.Status == "Finalizada") return $"O jogo {jogo.Codigo} já terminou — o formato dele é história.";
        if (TemPlacar(jogo))
            return $"O jogo {jogo.Codigo} já tem placar marcado. Troque o formato antes do primeiro ponto — " +
                   "depois, o placar lançado mudaria de sentido.";
        return null;
    }

    public static bool TemPlacar(Partida jogo) =>
        (jogo.GamesDupla1 ?? 0) > 0 || (jogo.GamesDupla2 ?? 0) > 0
        || (jogo.SetsDupla1 ?? 0) > 0 || (jogo.SetsDupla2 ?? 0) > 0
        || TieBreakDoJogo.Houve(jogo.PontosTieBreak1, jogo.PontosTieBreak2);

    // Grava o formato próprio — ou limpa, quando `games` vem vazio ("voltar ao da fase").
    public static void Aplicar(Partida jogo, int? sets, int? games, int? pontosTieBreak)
    {
        if (games is not int g || g <= 0)
        {
            jogo.SetsDoJogo = null;
            jogo.GamesDoJogo = null;
            jogo.PontosTieBreakDoJogo = null;
            return;
        }

        jogo.GamesDoJogo = Math.Clamp(g, 1, MaxGames);
        jogo.SetsDoJogo = Math.Clamp(sets ?? FormatoDaPartida.SetsPadrao, 1, MaxSets);
        // Só os dois alvos de quadra (7 e 10) ou desligado; o resto cai em desligado.
        jogo.PontosTieBreakDoJogo = pontosTieBreak is TieBreakDoJogo.PontosPadrao or TieBreakDoJogo.SuperTieBreak
            ? pontosTieBreak
            : TieBreakDoJogo.Desligado;
    }

    // "Set único até 9", "3 sets de 6 · super tie-break de 10" — pra tela e pra mensagem.
    public static string Descrever(FormatoDaPartida.Formato formato)
    {
        var games = formato.EhSoma ? $"soma de {formato.Games} games" : $"até {formato.Games}";
        var corpo = formato.Sets <= 1 ? $"Set único {games}" : $"{formato.Sets} sets {(formato.EhSoma ? games : $"de {formato.Games}")}";
        return formato.PontosTieBreak switch
        {
            TieBreakDoJogo.SuperTieBreak => corpo + " · super tie-break de 10",
            TieBreakDoJogo.PontosPadrao => corpo + " · tie-break de 7",
            _ => corpo,
        };
    }
}
