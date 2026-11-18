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
}
