namespace Padelizou.Services;

// O PLACAR DE UM JOGO QUE AINDA NÃO COMEÇOU NÃO É ZERO — É NADA.
//
// Régua única de como o placar de uma partida aparece na CHAVE (`_ChaveVaga` e
// `_ChaveDoMataMata`). Existe porque a regra tem três casos e estava escrita em duas views,
// nas duas do mesmo jeito e nas duas incompleta.
public static class PlacarNaTela
{
    public const string SemPlacar = "–";

    // Três casos, e cada um diz a verdade sobre o seu:
    //   • AGENDADA  → não começou, não há placar. Mesmo que o banco tenha um 0 gravado de
    //                 antes da limpeza: a tela não depende da migration ter alcançado a linha.
    //   • AO VIVO   → está em quadra, e 0 x 0 é um placar de verdade. É o que o card grande
    //                 do AO VIVO já mostra (`?? 0`); discordar dele seria a mesma informação
    //                 em duas telas com dois valores.
    //   • o resto   → mostra o que tem, e não inventa 0 no que não tem.
    public static string DaChave(string? status, int? games) =>
        status == "Agendada" ? SemPlacar
        : status == "AoVivo" ? (games ?? 0).ToString()
        : games?.ToString() ?? SemPlacar;
}
