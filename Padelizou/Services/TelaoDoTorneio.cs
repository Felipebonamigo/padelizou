using Padelizou.Models;

namespace Padelizou.Services;

// O TELÃO DO TORNEIO — o que a TV do clube mostra (18/11/2026).
//
// 🗣️ Felipe, pelo organizador da Wallau: *"quer por em uma tv, os jogos aovivo, agendado e
// finalizado para exibir"*. Três blocos, lidos de longe: o que está em quadra, o que vem, o que
// acabou. A página Jogos já tem os três, mas em ABAS, com botões e feita pro celular; numa TV
// ninguém toca na tela.
//
// ⚠️ O CLUBE É A MESMA RÉGUA DA PÁGINA JOGOS (FiltroDeJogos → LugarDoJogo.ClubeDoJogo): a quadra
// manda. Reescrever aqui faria a TV do clube A mostrar um jogo que a lista etiqueta como do B.
//
// ⚠️ OS LIMITES SÃO DE PROPÓSITO: a TV não rola. Melhor mostrar os próximos 8 inteiros e legíveis
// do que os 30 do dia espremidos.
public static class TelaoDoTorneio
{
    public sealed record Quadro(List<Partida> AoVivo, List<Partida> Proximos, List<Partida> Resultados);

    public static Quadro Montar(IEnumerable<Partida> jogos, SedesDoTorneio? sedes, int? clubeId,
        int maxProximos = 8, int maxResultados = 8)
    {
        var filtro = new FiltroDeJogos(clubeId);
        var doClube = jogos.Where(j => clubeId == null || filtro.Aceita(sedes, j)).ToList();

        var aoVivo = doClube
            .Where(j => j.Status == "AoVivo")
            .OrderBy(j => j.NomeQuadra ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(j => j.HorarioPrevisto)
            .ToList();

        // Sem hora não entra: na TV, "próximo" sem hora não diz nada a quem está olhando.
        var proximos = doClube
            .Where(j => j.Status == "Agendada" && j.HorarioPrevisto != null)
            .OrderBy(j => j.HorarioPrevisto)
            .ThenBy(j => j.OrdemNoHorario ?? int.MaxValue)
            .ThenBy(j => j.Id)
            .Take(maxProximos)
            .ToList();

        // O que acabou POR ÚLTIMO primeiro — é o resultado que quem chega ao clube quer saber.
        var resultados = doClube
            .Where(j => j.Status == "Finalizada")
            .OrderByDescending(j => j.HorarioFimReal ?? j.HorarioPrevisto)
            .ThenByDescending(j => j.Id)
            .Take(maxResultados)
            .ToList();

        return new Quadro(aoVivo, proximos, resultados);
    }

    // A BOLINHA DO SAQUE NA TV (21/11/2026). 🗣️ Felipe: *"aqui não tá aparecendo o sacador"*. O dado
    // é o mesmo do card do celular (`Partida.DuplaSacandoId`, que a Mesa e o card ao vivo marcam);
    // sem saque marcado, nenhuma bolinha — inventar um lado seria pior que não mostrar.
    public static bool Saca(Partida jogo, int duplaId) =>
        jogo.DuplaSacandoId is int quemSaca && quemSaca == duplaId;

    // OS SETS JÁ FECHADOS NO CARD DA TV (22/11/2026, Wallau). 🗣️ Vini: *"Telão não mostra que a
    // dupla ganhou o primeiro set"* — só havia os games do set em jogo. Lê `Partida.PlacarDosSets`
    // ("6-3 4-6 [10-8]", o colchete é o super tie-break; Services/SetsDoJogo grava). Texto torto
    // não vira número inventado: o pedaço que não lê é pulado.
    public sealed record SetFechado(int Games1, int Games2, bool SuperTieBreak)
    {
        public int Vencedor => Games1 > Games2 ? 1 : Games2 > Games1 ? 2 : 0;
    }

    public static IReadOnlyList<SetFechado> SetsFechados(Partida jogo)
    {
        if (string.IsNullOrWhiteSpace(jogo.PlacarDosSets)) return Array.Empty<SetFechado>();
        var sets = new List<SetFechado>();
        foreach (var pedaco in jogo.PlacarDosSets.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            bool super = pedaco.StartsWith('[') && pedaco.EndsWith(']');
            var lados = pedaco.Trim('[', ']').Split('-');
            if (lados.Length == 2 && int.TryParse(lados[0], out var g1) && int.TryParse(lados[1], out var g2))
                sets.Add(new SetFechado(g1, g2, super));
        }
        return sets;
    }
}
