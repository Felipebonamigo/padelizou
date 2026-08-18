namespace Padelizou.Services;

// MUDAR O PREÇO COM O TORNEIO JÁ ABERTO.
//
// Felipe, 18/08/2026: "permita alterar os valores de impedimento e da inscrição depois de
// aberto, mas avise que esse valor já foi feito antes".
//
// ⚠️ O QUE MUDA E O QUE NÃO MUDA — é isso que o aviso precisa dizer, e é o oposto do que a
// pessoa supõe. Cada inscrição guarda o que ELA custou (`Dupla.ValorInscricao`), gravado na
// hora em que foi feita. Trocar o preço agora **não** reescreve nenhuma delas: vale pra quem
// entrar daqui pra frente.
//
// Sem esse aviso o organizador faz uma de duas contas erradas, e as duas doem: ou acha que
// subiu o preço de todo mundo e vai cobrar a diferença de quem já pagou, ou acha que quem já
// se inscreveu vai pagar o valor novo e conta com um dinheiro que não vem.
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

        return $"{quantas} com os valores atuais. Mudar aqui vale só pra quem se inscrever "
            + "daqui pra frente — quem já entrou continua com o valor que combinou.";
    }
}
