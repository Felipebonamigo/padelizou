using Padelizou.Models;

namespace Padelizou.Services;

// COMO SE ESCREVE O NOME DE UMA DUPLA — a régua única.
//
// Nasceu extraída de `CampeaoDeCategoria.Nomes` (13/08/2026) no dia em que o pódio precisou
// da mesma frase pra quatro duplas em vez de uma. Duas cópias disto divergiriam no separador
// ("&" × "e" × "/") e o mesmo torneio sairia escrito de dois jeitos em dois cards da mesma
// página — que é o tipo de detalhe que faz uma arte parecer de outro produto.
//
// ⚠️ NOME DE TIME MANDA, e não é preferência de exibição: numa linha de TIME o `Jogador1Id`
// aponta pro organizador que cadastrou o time, não pra quem jogou. Escrever o nome dele seria
// coroar quem não entrou em quadra — a mesma armadilha que já tirou a dupla-TIME de todo
// somatório de ranking do sistema.
public static class NomeDaDupla
{
    // O separador tem espaço duplo dos dois lados de propósito: é o que dá respiro entre dois
    // nomes compridos numa arte, e é o que já estava no card de campeão.
    public const string Separador = "  &  ";

    public static string De(string? nomeTime, Jogador? jogador1, Jogador? jogador2)
    {
        if (!string.IsNullOrWhiteSpace(nomeTime)) return nomeTime;
        if (jogador1 == null) return "";
        return jogador2 == null ? jogador1.ComoChamar : $"{jogador1.ComoChamar}{Separador}{jogador2.ComoChamar}";
    }

    // A partir da linha do banco. Exige os `Include` de Jogador1/Jogador2 — sem eles a dupla
    // volta com as navegações nulas e o nome sai vazio, calado.
    public static string Na(Dupla dupla) =>
        De(dupla.NomeTime,
           dupla.NomeTime == null ? dupla.Jogador1 : null,
           dupla.NomeTime == null ? dupla.Jogador2 : null);
}
