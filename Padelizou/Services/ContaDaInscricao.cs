using Padelizou.Models;

namespace Padelizou.Services;

// "São R$ 250,00" — de onde saem esses R$ 250?
//
// 🗣️ Pedido de um jogador (29/09/2026), que estava em duas categorias e leu o total de UMA
// delas como sendo o das duas: *"talvez fosse interessante dizer aqui as infos sobre esse
// valor... as vezes é preciso dizer o óbvio"*.
//
// ⚠️ A REGRA DESTA CLASSE É NÃO AFIRMAR O QUE NÃO FECHA. O total vem gravado na inscrição
// (`ValorInscricao`) e pode ter nascido de coisas que nenhuma conta aqui conhece: estorno
// parcial (Services/EstornoParcial), ajuste na mão do organizador, preço que mudou depois.
// Quando nenhuma combinação conhecida reproduz o número, a resposta é NULA — o valor continua
// na tela, só não vem acompanhado de uma explicação inventada. Uma conta errada ao lado do
// número certo é pior que número nenhum: ela ensina o jogador a desconfiar do total.
public static class ContaDaInscricao
{
    public static string? Frase(Torneio torneio, int pessoas, int impedimentos, decimal valorGravado)
    {
        if (pessoas < 1) return null;

        // O impedimento é da INSCRIÇÃO, não do atleta (ver Services/PrecoDaInscricao), mas
        // está DENTRO do total — uma conta que o ignora não fecha com o número ao lado.
        impedimentos = Math.Max(impedimentos, 0);
        var taxa = torneio.TaxaPorImpedimento * impedimentos;
        var extra = impedimentos > 0 && torneio.TaxaPorImpedimento > 0
            ? $" + {impedimentos} {(impedimentos == 1 ? "impedimento" : "impedimentos")}"
            : "";

        if (valorGravado == torneio.PrecoInscricao * pessoas + taxa)
            return $"{pessoas} × {torneio.PrecoInscricao:C}{extra}";

        // O desconto de segunda categoria é POR PESSOA: numa dupla em que um repete e o outro
        // não, um lado paga cheio e o outro paga o desconto. Por isso a conta é procurada em
        // todas as combinações, e não só no "os dois repetem".
        if (torneio.PermiteMultiplasCategorias && torneio.PrecoSegundaInscricao is decimal segunda)
        {
            for (int repetem = 1; repetem <= pessoas; repetem++)
            {
                if (valorGravado != torneio.PrecoInscricao * (pessoas - repetem) + segunda * repetem + taxa)
                    continue;

                return repetem == pessoas
                    ? $"{pessoas} × {segunda:C}, preço de 2ª categoria{extra}"
                    : $"{pessoas - repetem} × {torneio.PrecoInscricao:C} + {repetem} × {segunda:C}"
                      + $", preço de 2ª categoria{extra}";
            }
        }

        return null;
    }
}
