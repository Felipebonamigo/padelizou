using Padelizou.Models;

namespace Padelizou.Services;

// Como a lista de inscritos conta o pagamento de cada um (Felipe, 08/08/2026).
//
// Num torneio em que o pagamento é APROVADO pelo organizador — ou acertado por fora —, a
// inscrição existe antes do dinheiro. Ela aparecia na lista sem dizer nada sobre isso, sob um
// título que dizia "Duplas Confirmadas": quem olhava não tinha como saber quem já acertou.
//
// ⚠️ Quem não pagou CONTINUA APARECENDO. A vaga é dele — o que muda é só o selo. Esconder
// seria pior que não dizer nada: o parceiro não acharia a própria inscrição, e o organizador
// perderia a lista de quem tem que cobrar.
public static class PagamentoDaInscricao
{
    // Torneio de graça não tem pagamento a mostrar, e um selo "pendente" nele seria uma
    // cobrança que não existe.
    public static bool TemOQueMostrar(Torneio torneio) => torneio.PrecoInscricao > 0;

    public const string Pago = "Pago";
    public const string Pendente = "Pagamento pendente";

    public static string Selo(bool pago) => pago ? Pago : Pendente;

    // Classe do Bootstrap pra cada estado — fica aqui, e não na view, porque o selo aparece
    // na lista pública e na gestão, e as duas têm que usar a mesma cor pro mesmo significado.
    public static string Cor(bool pago) => pago ? "bg-success" : "bg-warning text-dark";
}
