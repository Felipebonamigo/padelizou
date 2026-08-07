using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// O acerto de contas com o Ranking RS de UM torneio: quantas pessoas foram cobradas, quanto
// deu e quando foi pago.
//
// Tabela própria (e não uma coluna `RankingRsAcertadoEm` no Torneio) por dois motivos:
//
//  1. É uma FOTOGRAFIA. A lista de inscritos continua se mexendo depois — gente desiste, o
//     organizador aceita alguém atrasado. O que foi combinado com eles naquele dia não pode
//     ser recalculado toda vez que a tela abre, senão o histórico muda sozinho.
//  2. O preço por inscrito é de tabela e pode ser renegociado. Guardar o valor fechado é o
//     que faz o acerto de junho continuar dizendo o que disse em junho.
[Table("AcertoRankingRs")]
public class AcertoRankingRs
{
    public int Id { get; set; }

    public int TorneioId { get; set; }
    public virtual Torneio Torneio { get; set; } = null!;

    // Pessoas DISTINTAS cobradas — quem jogou duas categorias conta uma vez (ver
    // Services/AcertoComORankingRs).
    public int PessoasCobradas { get; set; }

    public decimal Valor { get; set; }

    public DateTime AcertadoEm { get; set; } = DateTime.Now;

    public int RegistradoPorJogadorId { get; set; }

    // A despesa que este acerto gerou no caixa. Nulo só se a despesa for apagada depois —
    // o acerto continua de pé, porque o dinheiro foi pago de qualquer jeito.
    public int? DespesaId { get; set; }
}
