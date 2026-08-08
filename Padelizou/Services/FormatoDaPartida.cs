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

    // `Contagem` diz o que o número de Games significa: "Ate" (quem chegar primeiro) ou
    // "Soma" (joga-se esse total e acabou). Ver ContagemDeGamesDoTorneio.
    public record Formato(int Sets, int Games, string Contagem = ContagemDeGamesDoTorneio.Ate)
    {
        public bool EhSoma => ContagemDeGamesDoTorneio.EhSoma(Contagem);
    }

    public static Formato De(Torneio? torneio, string? fase)
    {
        if (torneio == null) return new Formato(SetsPadrao, GamesPadrao);

        // A contagem é do TORNEIO inteiro: o número de games muda por fase, o significado
        // dele não. "Grupos na soma e final no até" seria uma regra por fase, e ninguém
        // pediu isso — quando pedirem, vira coluna por fase como Sets e Games já são.
        var contagem = ContagemDeGamesDoTorneio.Valido(torneio.ContagemDeGames);

        if (fase is "Semifinal" or "Final")
            return Valido(torneio.SetsFaseFinal, torneio.GamesFaseFinal, contagem);

        if (FasesTorneio.EhFaseDeGrupos(fase) || (fase?.StartsWith("Americano") ?? false))
            return Valido(torneio.SetsFaseGrupos, torneio.GamesFaseGrupos, contagem);

        return Valido(torneio.SetsFaseMataMata, torneio.GamesFaseMataMata, contagem);
    }

    // Torneio antigo (ou criado por formulário incompleto) tem zero gravado nessas colunas.
    // Zero viraria uma Mesa onde não dá pra marcar nem um game — melhor cair no padrão do
    // que travar a quadra.
    private static Formato Valido(int sets, int games, string contagem) =>
        new(sets > 0 ? sets : SetsPadrao, games > 0 ? games : GamesPadrao, contagem);

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

    // ── As duas perguntas acima, agora cientes da CONTAGEM ────────────────────────────────
    //
    // ⚠️ Na SOMA o teto é POR LADO, e é isso que a versão "até" não sabia fazer: numa soma de
    // 7 com o adversário em 3, eu só posso chegar a 4 — o meu teto depende do placar DELE. No
    // "até" o teto é simétrico (os dois miram o mesmo número), e foi por isso que um número só
    // bastou até aqui. Quem chama precisa pedir o teto de cada lado separadamente.

    // O máximo que ESTE lado pode marcar agora, dado o que o outro já tem.
    public static int TetoDoLado(Formato formato, int meusGames, int gamesDoOutro) =>
        formato.EhSoma
            ? Math.Max(0, formato.Games - gamesDoOutro)
            : TetoDeGames(formato.Games, meusGames, gamesDoOutro);

    // Na soma o jogo fecha quando os games ACABAM (o total foi jogado), não quando alguém
    // chega num número — 5x2 e 4x3 fecham a mesma soma de 7. Já 6x0 numa soma de 7 ainda tem
    // um game pra jogar, e encerrar ali seria dar o jogo por acabado antes da hora.
    public static bool PodeEncerrar(Formato formato, int games1, int games2) =>
        formato.EhSoma
            ? games1 + games2 >= formato.Games
            : PodeEncerrar(formato.Games, games1, games2);

    // Um placar inteiro ajustado ao formato. Existe pra Mesa, tela cheia e salvar-em-lote não
    // terem três versões da mesma conta — que é exatamente como o `limiteGames: 9` cravado no
    // JavaScript sobreviveu tanto tempo.
    //
    // ⚠️ Na soma os dois lados NÃO podem ser limitados pelo mesmo teto: 5x5 numa soma de 7
    // passaria, porque 5 "cabe" nos dois cálculos feitos em separado. O bolo é fixo — o
    // primeiro lado pega até o total e o segundo fica com o que sobrou.
    public static (int Games1, int Games2) PlacarValido(Formato formato, int games1, int games2)
    {
        games1 = Math.Max(0, games1);
        games2 = Math.Max(0, games2);

        if (formato.EhSoma)
        {
            int g1 = Math.Min(games1, formato.Games);
            return (g1, Math.Min(games2, formato.Games - g1));
        }

        int teto = TetoDeGames(formato.Games, games1, games2);
        return (Math.Min(games1, teto), Math.Min(games2, teto));
    }
}
