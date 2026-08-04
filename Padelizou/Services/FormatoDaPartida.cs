using Padelizou.Models;

namespace Padelizou.Services;

// Quantos sets e quantos games vale CADA partida, conforme a fase em que ela é jogada.
//
// O torneio guarda três formatos desde sempre (grupos, mata-mata, final) e a tela de criação
// pergunta os três — mas ninguém lia: a Mesa de Controle tinha `limiteGames: 9` cravado no
// JavaScript. O organizador escolhia "4 games" no cadastro e a Mesa deixava marcar até 9,
// calada. Este é o único lugar que traduz fase → formato; se aparecer uma segunda cópia,
// volta o mesmo bug com outro número.
//
// A régua das fases:
//   • grupos e Americano  → o formato "geral" (é a maioria esmagadora dos jogos);
//   • Semifinal e Final   → o formato de decisão. Semifinal entra aqui de propósito: quem
//     escreve "as semis e a final são mais longas" está descrevendo as duas, e ninguém
//     configura uma semifinal com regra de oitavas;
//   • o resto do mata-mata (Primeira Rodada, Oitavas, Quartas) → o formato eliminatório.
public static class FormatoDaPartida
{
    public const int GamesPadrao = 9;
    public const int SetsPadrao = 1;

    public record Formato(int Sets, int Games);

    public static Formato De(Torneio? torneio, string? fase)
    {
        if (torneio == null) return new Formato(SetsPadrao, GamesPadrao);

        if (fase is "Semifinal" or "Final")
            return Valido(torneio.SetsFaseFinal, torneio.GamesFaseFinal);

        if (FasesTorneio.EhFaseDeGrupos(fase) || (fase?.StartsWith("Americano") ?? false))
            return Valido(torneio.SetsFaseGrupos, torneio.GamesFaseGrupos);

        return Valido(torneio.SetsFaseMataMata, torneio.GamesFaseMataMata);
    }

    // Torneio antigo (ou criado por formulário incompleto) tem zero gravado nessas colunas.
    // Zero viraria uma Mesa onde não dá pra marcar nem um game — melhor cair no padrão do
    // que travar a quadra.
    private static Formato Valido(int sets, int games) =>
        new(sets > 0 ? sets : SetsPadrao, games > 0 ? games : GamesPadrao);

    // Até onde o placar pode ir NESTE momento — o limite não é um teto seco.
    //
    // A regra da quadra (Felipe, 05/08/2026): jogo até 4 que empata em 3x3 vai até 5; jogo
    // até 6 que empata em 5x5 vai até 7. É o "vencer por dois" do padel — empatou na
    // penúltima, joga o desempate.
    //
    // Já o jogo até 9 NÃO estende: 8x8 se resolve no tie-break, e o 9º game é ele. Quem
    // separa os dois casos é a PARIDADE: limite par pede desempate (o número ímpar de cima
    // é o game decisivo), limite ímpar já embute o desempate no próprio número.
    //
    // O teto sobe só DEPOIS de o empate acontecer. Sem essa condição, um jogo até 4 poderia
    // terminar 5x0 — e ninguém joga um quinto game numa partida decidida por 4x0.
    public static int TetoDeGames(int limite, int games1, int games2)
    {
        if (limite <= 1 || limite % 2 != 0) return limite;

        bool empatouNaPenultima = games1 >= limite - 1 && games2 >= limite - 1;
        return empatouNaPenultima ? limite + 1 : limite;
    }

    // O placar já dá pra encerrar? Alguém chegou no número que vence — respeitando o
    // desempate: em 3x3 (jogo até 4) ninguém venceu ainda, apesar de o 3 ser "quase lá".
    public static bool PodeEncerrar(int limite, int games1, int games2)
    {
        int teto = TetoDeGames(limite, games1, games2);
        return games1 >= teto || games2 >= teto
            || (games1 >= limite && games1 > games2)
            || (games2 >= limite && games2 > games1);
    }
}
