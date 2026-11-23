using Padelizou.Models;

namespace Padelizou.Services;

// O PLACAR DO CARD DE JOGO FINALIZADO (22/11/2026, Wallau). 🗣️ Felipe, com o print da final
// "3-6 2-6": *"tá errado esse visual do placar, como eram dois sets, pense na melhor forma"*.
//
// 🕳️ O número grande era o placar de games do ÚLTIMO set (2 × 6), lido como o resultado do jogo; e
// quem ganhou saía desses mesmos games — num jogo decidido no super tie-break eles estão 0 × 0, e
// ninguém ficava em destaque.
//
// ✅ Como num placar de padel: em jogo de mais de um set, o número grande são os SETS vencidos e ao
// lado vão os games de cada set (o super tie-break com os pontos dele), o vencedor de cada set
// aceso. Jogo de um set continua mostrando os games. O vencedor GRAVADO (VencedorId — o W.O., por
// exemplo) vale mais que qualquer conta.
public static class PlacarNoCard
{
    public sealed record Lado(int Grande, IReadOnlyList<(int Games, bool Ganhou)> Sets);
    public sealed record Placar(Lado Dupla1, Lado Dupla2, int? Vencedor, bool PorSets);

    public static Placar De(Partida jogo)
    {
        var sets = TelaoDoTorneio.SetsFechados(jogo);
        bool porSets = sets.Count > 0 && (jogo.SetsDupla1 != null || jogo.SetsDupla2 != null);

        // AO VIVO o número grande é o set EM JOGO (os fechados vão ao lado) — o mesmo do telão; só
        // depois de acabar ele vira a contagem de sets.
        bool aoVivo = jogo.Status == "AoVivo";
        int grande1 = porSets && !aoVivo ? jogo.SetsDupla1 ?? 0 : jogo.GamesDupla1 ?? 0;
        int grande2 = porSets && !aoVivo ? jogo.SetsDupla2 ?? 0 : jogo.GamesDupla2 ?? 0;

        int? vencedor = aoVivo ? null : jogo.VencedorId is int id && id == jogo.Dupla1Id ? 1
            : jogo.VencedorId is int id2 && id2 == jogo.Dupla2Id ? 2
            : grande1 > grande2 ? 1 : grande2 > grande1 ? 2 : null;

        var lado1 = porSets ? sets.Select(s => (s.Games1, s.Vencedor == 1)).ToList() : new List<(int, bool)>();
        var lado2 = porSets ? sets.Select(s => (s.Games2, s.Vencedor == 2)).ToList() : new List<(int, bool)>();
        return new Placar(new Lado(grande1, lado1), new Lado(grande2, lado2), vencedor, porSets);
    }

    // O PLACAR EM TEXTO (23/11/2026) — a arte da chave, o compartilhar: jogo de vários sets sai set a
    // set ("6-3 7-6 [10-8]"), que é o que diz como foi; jogo de um set, "9 x 7" como sempre.
    public static string Texto(Partida jogo) =>
        De(jogo).PorSets && SetsDoJogo.Texto(jogo) is string sets
            ? sets
            : $"{jogo.GamesDupla1 ?? 0} x {jogo.GamesDupla2 ?? 0}";
}
