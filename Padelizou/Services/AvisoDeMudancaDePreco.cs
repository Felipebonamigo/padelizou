namespace Padelizou.Services;

// MUDAR O PREÇO COM O TORNEIO JÁ ABERTO.
//
// Felipe, 18/08/2026: "permita alterar os valores de impedimento e da inscrição depois de
// aberto, mas avise que esse valor já foi feito antes".
//
// ⚠️ O QUE MUDA E O QUE NÃO MUDA — é isso que o aviso precisa dizer. Até 18/11/2026 nada
// mudava: cada inscrição guardava o valor do dia em que foi feita, e a 3ª ETAPA WALLAU cobrou
// R$ 110 num torneio de R$ 140 (*"alterei lá e não mudou nada"*). Desde então, salvar leva
// junto quem ainda não pagou; quem pagou fica com o combinado
// (Services/ReajusteDoPrecoDasInscricoes).
public static class AvisoDeMudancaDePreco
{
    // Nulo quando não há o que avisar — torneio sem ninguém inscrito é preço que ainda não
    // valeu pra ninguém, e aí o aviso seria só ruído na tela.
    public static string? Texto(int inscricoesJaFeitas)
    {
        if (inscricoesJaFeitas <= 0) return null;

        var quantas = inscricoesJaFeitas == 1
            ? "1 inscrição já foi feita"
            : $"{inscricoesJaFeitas} inscrições já foram feitas";

        return $"{quantas}. Ao salvar, quem ainda não pagou passa pro valor novo — "
            + "quem já pagou (ou já pagou a parte dele) continua com o valor que combinou.";
    }
}
