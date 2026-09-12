namespace Padelizou.Services;

// O TIE-BREAK EM PONTOS — quando ele existe, quando ele fecha, e com que placar de games.
//
// 🗣️ Felipe, 12/09/2026: *"no placar ao vivo, ao ficar 8x8, deveria aparecer uma contagem de
// tie break, que pode ir até 7 ou até 10, depende do torneio"*.
//
// Até aqui "tie-break" aqui dentro era só uma PALAVRA em comentário: o 9º game do jogo até 9
// ganhou esse apelido porque é ele que desempata o 8x8, e ponto nenhum era contado em lugar
// nenhum. Na quadra conta-se 7 (ou 10) pontos de verdade, e o placar ao vivo não tinha onde
// mostrá-los.
//
// ⚠️ A DECISÃO QUE SEGURA O RESTO DO SISTEMA: quem fecha o tie-break leva o ÚLTIMO GAME
// (9x8). Os games continuam sendo a verdade — `QuemVenceu`, `ClassificacaoDeGrupos`, saldo de
// games, desempate de grupo, Padelímetro, chave e a API de torneios não sabem que tie-break
// existe, e não precisam saber. Os pontos são informação a mais, gravada pra tela poder
// contar a história ("9 x 8, tie-break 7-5").
//
// ⚠️ E A RÉGUA DOS GAMES NÃO MUDA: 8x8 continua sendo placar que não encerra jogo
// (FormatoDaPartida.PodeEncerrar segue dizendo não). O fechamento do tie-break ESCREVE o 9º
// game; não existe caminho novo pra decidir partida.
public static class TieBreakDoJogo
{
    // Zero = sem contagem, que é o comportamento de todo torneio que já existia quando isto
    // nasceu (a migration grava zero nas linhas antigas): o 8x8 segue se resolvendo no 9º
    // game marcado na mão.
    public const int Desligado = 0;

    // Os dois alvos que as telas oferecem. 7 é o tie-break normal; 10 é o super tie-break,
    // que costuma aparecer na decisão.
    public const int PontosPadrao = 7;
    public const int SuperTieBreak = 10;

    // Teto do que se aceita GRAVAR como ponto. Nenhum tie-break chega perto disso — o número
    // existe pra um POST montado à mão (ou um dedo preso no "+") não gravar 4000 no placar.
    public const int TetoDosPontos = 99;

    // O alvo como ele vale. Coluna antiga, negativa ou absurda cai em "desligado": errar para
    // o comportamento de sempre é o único erro seguro aqui.
    public static int AlvoValido(int pontos) =>
        pontos > 0 ? Math.Min(pontos, TetoDosPontos) : Desligado;

    // O ponto como ele é gravado.
    public static int PontoValido(int ponto) => Math.Clamp(ponto, 0, TetoDosPontos);

    // Teve tie-break neste jogo? 0-0 é "não começou" — um card não ganha uma linha de
    // tie-break só porque as colunas existem.
    public static bool Houve(int? pontos1, int? pontos2) => (pontos1 ?? 0) > 0 || (pontos2 ?? 0) > 0;

    // Neste formato o tie-break PODE acontecer algum dia? É a pergunta da tela de
    // configuração, que precisa avisar quando o número escolhido vai ficar inerte.
    //
    // ⚠️ A paridade NÃO é recalculada aqui. Quem responde "este limite estende?" é o
    // `TetoDeGames`, e a resposta dele no empate a um game do fim é o critério: no até 9 o
    // teto segue sendo 9 (o desempate é ESTE jogo, e é onde o tie-break entra); no até 4 o
    // teto vira 5 (o desempate é um game a mais, o "vencer por dois" que já está em quadra e
    // que o Felipe decidiu não mexer). Escrever `% 2` de novo aqui seria a segunda cópia da
    // regra — exatamente como o `limiteGames: 9` cravado no JavaScript sobreviveu tanto tempo.
    public static bool PodeAcontecer(FormatoDaPartida.Formato formato) =>
        formato.PontosTieBreak > 0
        && !formato.EhSoma
        && formato.Games > 1
        && FormatoDaPartida.TetoDeGames(formato.Games, formato.Games - 1, formato.Games - 1) == formato.Games;

    // A FASE comporta tie-break, esquecendo o que foi configurado? É a pergunta da TELA DE
    // CONFIGURAÇÃO, e ela é diferente do `PodeAcontecer`: ali o organizador ainda não escolheu o
    // alvo, e mesmo assim precisa ser avisado de que naquela fase o tie-break nunca vai
    // acontecer — o padrão da final é 6 games, e 6 é par. Sem separar as duas perguntas, o
    // aviso só apareceria DEPOIS de configurar, que é tarde.
    public static bool AFaseComporta(FormatoDaPartida.Formato formato) =>
        PodeAcontecer(formato with { PontosTieBreak = PontosPadrao });

    // O jogo está EM tie-break agora? É o empate a um game do fim — 8x8 no jogo até 9.
    public static bool EmAndamento(FormatoDaPartida.Formato formato, int games1, int games2) =>
        PodeAcontecer(formato)
        && games1 == formato.Games - 1
        && games2 == formato.Games - 1;

    // Dá pra fechar? ⚠️ Alcançar o alvo NÃO basta: precisa de 2 pontos de frente (decisão do
    // Felipe, 12/09/2026) — 7-6 continua, 8-6 fecha. É o "vencer por dois" que o projeto já
    // aplica aos games, e é o que faz o alvo não ser um teto seco: a contagem passa de 7.
    public static bool PodeFechar(FormatoDaPartida.Formato formato, int pontos1, int pontos2)
    {
        int alvo = formato.PontosTieBreak;
        if (alvo <= 0) return false;

        return (pontos1 >= alvo || pontos2 >= alvo) && Math.Abs(pontos1 - pontos2) >= 2;
    }

    // Que LADO fechou (1, 2) — nulo enquanto não fechou.
    public static int? LadoQueFechou(FormatoDaPartida.Formato formato, int pontos1, int pontos2) =>
        PodeFechar(formato, pontos1, pontos2) ? (pontos1 > pontos2 ? 1 : 2) : null;

    // O placar de GAMES que o fechamento escreve: o último game pra quem fechou.
    //
    // ⚠️ Nulo quando não dá pra fechar, e não um 9x8 de consolo: devolver placar pronto num
    // 7-6 escreveria o fim de um jogo que ainda está sendo jogado. Quem chama trata o nulo.
    public static (int Games1, int Games2)? GamesAoFechar(FormatoDaPartida.Formato formato, int pontos1, int pontos2) =>
        LadoQueFechou(formato, pontos1, pontos2) switch
        {
            1 => (formato.Games, formato.Games - 1),
            2 => (formato.Games - 1, formato.Games),
            _ => null
        };

    // Como a tela escreve a contagem ao lado do placar: "tie-break 7-5".
    public static string Etiqueta(int? pontos1, int? pontos2) => $"tie-break {pontos1 ?? 0}-{pontos2 ?? 0}";
}
