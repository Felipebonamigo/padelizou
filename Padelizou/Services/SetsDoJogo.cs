using Padelizou.Models;

namespace Padelizou.Services;

// JOGO DE MAIS DE UM SET NO PLACAR AO VIVO (19/11/2026).
//
// 🗣️ Felipe, na final do Chakra (2 sets de 6): *"Como que eu coloco o segundo set em jogo? E não
// esqueças que se for 1-1 em sets tem o tie break"*. Até aqui o placar guardava só os games do set
// em jogo e o NÚMERO de sets de cada lado — não havia como fechar um set pela lista ao vivo, o 6x3
// do primeiro set se perdia, e o 1x1 não tinha desempate.
//
// A régua (decisão do Felipe):
//   • "Fechar set" guarda o placar do set em `Partida.PlacarDosSets` ("6-3 4-6 [10-8]"), soma o
//     set pra quem venceu e zera os games — menos no set que DECIDE o jogo: ali os games ficam, e
//     são o placar final que o Finalizar lê.
//   • Um set termina com 2 games de frente a partir do limite (6-4), ou em limite+1 (7-5, 7-6).
//   • No 1x1 de um jogo de 2 sets — ou de 3 sets com super tie-break no formato, que é a mesma
//     coisa dita de outro jeito ("Dois sets de 6 com super tie = 3 sets, 6 games, super
//     tie-break") — o desempate é um SUPER TIE-BREAK DE 10, vencido por 2. Ele conta nos campos
//     de pontos do tie-break que o card já tem.
//   • Jogo de UM set não muda nada: nem botão, nem régua.
public static class SetsDoJogo
{
    public const int PontosDoSuperTieBreak = TieBreakDoJogo.SuperTieBreak;

    public static bool TemMaisDeUmSet(FormatoDaPartida.Formato f) => f.Sets > 1;

    // Quantos sets ganha o jogo: melhor de 2 com desempate e melhor de 3 dão os dois 2.
    public static int SetsParaVencer(FormatoDaPartida.Formato f) =>
        f.Sets <= 1 ? 1 : f.Sets == 2 ? 2 : (f.Sets + 1) / 2;

    public static bool DecideNoSuperTieBreak(FormatoDaPartida.Formato f) =>
        f.Sets == 2 || (f.Sets == 3 && f.PontosTieBreak == TieBreakDoJogo.SuperTieBreak);

    public static bool EmSuperTieBreak(FormatoDaPartida.Formato f, int? sets1, int? sets2)
    {
        if (!DecideNoSuperTieBreak(f)) return false;
        int falta = SetsParaVencer(f) - 1;
        return (sets1 ?? 0) == falta && (sets2 ?? 0) == falta;
    }

    public static bool JogoDecidido(FormatoDaPartida.Formato f, int? sets1, int? sets2) =>
        TemMaisDeUmSet(f) && Math.Max(sets1 ?? 0, sets2 ?? 0) >= SetsParaVencer(f);

    // Quem levou o set em jogo, ou null se ele ainda não terminou.
    public static int? LadoDoSet(FormatoDaPartida.Formato f, Partida p)
    {
        if (EmSuperTieBreak(f, p.SetsDupla1, p.SetsDupla2))
        {
            int p1 = p.PontosTieBreak1 ?? 0, p2 = p.PontosTieBreak2 ?? 0;
            if (Math.Max(p1, p2) < PontosDoSuperTieBreak || Math.Abs(p1 - p2) < 2) return null;
            return p1 > p2 ? 1 : 2;
        }

        int g1 = p.GamesDupla1 ?? 0, g2 = p.GamesDupla2 ?? 0;
        if (g1 == g2) return null;
        int a = Math.Max(g1, g2), b = Math.Min(g1, g2), limite = f.Games;
        bool terminou = f.EhSoma
            ? FormatoDaPartida.PodeEncerrar(f, g1, g2)
            : (a >= limite && a - b >= 2) || (a == limite + 1 && b >= limite - 1);
        return terminou ? (g1 > g2 ? 1 : 2) : null;
    }

    public static bool PodeFecharSet(FormatoDaPartida.Formato f, Partida p) =>
        TemMaisDeUmSet(f) && !JogoDecidido(f, p.SetsDupla1, p.SetsDupla2) && LadoDoSet(f, p) != null;

    public static void Fechar(FormatoDaPartida.Formato f, Partida p)
    {
        if (!PodeFecharSet(f, p) || LadoDoSet(f, p) is not int lado) return;

        bool superTieBreak = EmSuperTieBreak(f, p.SetsDupla1, p.SetsDupla2);
        var placar = superTieBreak
            ? $"[{p.PontosTieBreak1 ?? 0}-{p.PontosTieBreak2 ?? 0}]"
            : $"{p.GamesDupla1 ?? 0}-{p.GamesDupla2 ?? 0}";
        p.PlacarDosSets = string.IsNullOrWhiteSpace(p.PlacarDosSets) ? placar : $"{p.PlacarDosSets} {placar}";

        if (lado == 1) p.SetsDupla1 = (p.SetsDupla1 ?? 0) + 1;
        else p.SetsDupla2 = (p.SetsDupla2 ?? 0) + 1;
        p.SetsDupla1 ??= 0;
        p.SetsDupla2 ??= 0;

        // O set que decide fica na tela (é o placar final); os outros zeram pro próximo.
        if (JogoDecidido(f, p.SetsDupla1, p.SetsDupla2)) return;
        p.GamesDupla1 = 0;
        p.GamesDupla2 = 0;
        p.PontosTieBreak1 = 0;
        p.PontosTieBreak2 = 0;
    }

    // Finalizar um jogo de vários sets antes de alguém vencê-los entregaria o jogo a quem levou o
    // primeiro set (a régua antiga lia "sets diferentes" como vitória).
    public static string? MotivoParaNaoFinalizar(FormatoDaPartida.Formato f, Partida p) =>
        TemMaisDeUmSet(f) && !JogoDecidido(f, p.SetsDupla1, p.SetsDupla2)
            ? $"o jogo é de {f.Sets} sets e ninguém venceu {SetsParaVencer(f)} ainda — use \"Fechar set\" quando o set terminar."
            : null;

    // Os sets FECHADOS, pra tela: "6-3 4-6 [10-8]". Só eles — o set em jogo está nos contadores do
    // card, e repeti-lo aqui deixava um número velho a cada toque (a linha não é reescrita pelo
    // salvamento). Null quando nenhum set fechou.
    public static string? Texto(Partida p) =>
        string.IsNullOrWhiteSpace(p.PlacarDosSets) ? null : p.PlacarDosSets.Trim();
}
