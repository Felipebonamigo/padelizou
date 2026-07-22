using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Controle informal de pagamento da mensalidade de um mensalista num mês/ano — igual ao modelo de
// confiança já usado no pacote de aulas (LocalAula.PacoteAtivo etc): sem gateway de pagamento real,
// o admin do grupo só marca manualmente quem já pagou (combinado por fora, Pix etc).
[Table("MensalidadeGrupo")]
public partial class MensalidadeGrupo
{
    public int GrupoId { get; set; }
    public int JogadorId { get; set; }
    public int Ano { get; set; }
    public int Mes { get; set; }

    public bool Pago { get; set; }
    public DateTime? DataPagamento { get; set; }

    [ForeignKey("GrupoId")]
    public virtual padelizou.Models.GrupoPrivado Grupo { get; set; } = null!;

    [ForeignKey("JogadorId")]
    public virtual Jogador Jogador { get; set; } = null!;
}
