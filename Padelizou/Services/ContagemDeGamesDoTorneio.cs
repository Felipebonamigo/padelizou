namespace Padelizou.Services;

// Os dois jeitos de contar os games de uma partida, num lugar só.
//
// Os nomes viram valor de coluna e valor de <select>, então digitá-los à mão pelas telas e
// pelos serviços é a receita conhecida de bug calado — foi exatamente o que aconteceu com os
// formatos de torneio antes de existir FormatoDoTorneio.
public static class ContagemDeGamesDoTorneio
{
    // Quem chegar primeiro no número vence — com o "vencer por dois" do padel embutido
    // (ver FormatoDaPartida.TetoDeGames). É o padrão histórico e o de todo torneio antigo.
    public const string Ate = "Ate";

    // Jogam-se exatamente N games e acabou; vence quem fizer mais. A rodada tem tamanho
    // fixo, que é o que faz a grade de um rodízio não escorregar.
    public const string Soma = "Soma";

    public static bool EhSoma(string? contagem) => contagem == Soma;

    // Valor desconhecido (coluna antiga, POST montado à mão) cai no comportamento de sempre.
    // Errar para "Ate" é seguro: é o que o torneio já fazia antes desta opção existir.
    public static string Valido(string? contagem) => EhSoma(contagem) ? Soma : Ate;

    public static bool Existe(string? contagem) => contagem is Ate or Soma;

    // ⚠️ Numa SOMA PAR o empate é alcançável (soma de 8 fecha em 4x4) — e o sistema recusa
    // finalizar partida empatada de propósito ("no padel alguém tem que fechar o jogo", ver
    // QuemVenceu.MotivoParaNaoFinalizar). Um total ímpar SEMPRE aponta um vencedor, então é
    // ele que a tela recomenda. Não é proibição: grupo e Americano somam games e convivem
    // bem com empate no meio do caminho — quem não convive é o mata-mata.
    public static bool PodeEmpatar(string? contagem, int games) =>
        EhSoma(contagem) && games > 0 && games % 2 == 0;
}
