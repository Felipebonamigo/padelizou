using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// A quadra que uma categoria PREFERE. Escolha do organizador, na criação do torneio ou
// depois, na gestão (Felipe, 12/08/2026).
//
// O pedido veio do jeito que o torneio funciona de verdade: existe a quadra do meio, a que
// tem arquibancada, a que tem câmera — e é nela que o clube quer a 1ª categoria, não o jogo
// de grupo da 6ª que caiu ali por ordem de fila. Até aqui a grade escolhia sempre a PRIMEIRA
// quadra livre do horário (ver Services/GradeDeJogos.Encaixar), e quem jogava na melhor
// quadra era quem tivesse entrado antes na fila.
//
// ⚠️ É PREFERÊNCIA, NÃO RESERVA. Uma quadra preferida por alguém continua recebendo jogo de
// outra categoria quando não sobrou mais nada livre naquele horário — quadra parada atrasa o
// torneio inteiro, e o organizador que quiser exclusividade tem a troca de quadra na mão. A
// regra de desempate mora em Services/PreferenciaDeQuadra.
//
// Muitos-para-muitos porque as duas pontas são plurais na vida real: uma categoria pode ter
// duas quadras boas ("as duas cobertas"), e a mesma quadra central costuma ser a preferida da
// 1ª masculina E da 1ª feminina — as finais das duas querem o mesmo lugar, em horas
// diferentes.
[Table("QuadraDaCategoria")]
public class QuadraDaCategoria
{
    public int CategoriaId { get; set; }
    public int QuadraId { get; set; }

    public virtual Categoria Categoria { get; set; } = null!;
    public virtual Quadra Quadra { get; set; } = null!;
}
