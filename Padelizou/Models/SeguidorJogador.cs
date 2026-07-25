using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

[Table("SeguidorJogador")]
public partial class SeguidorJogador
{
    public int SeguidorId { get; set; }
    public int SeguidoId { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.Now;

    [ForeignKey("SeguidorId")]
    public virtual Jogador Seguidor { get; set; } = null!;

    [ForeignKey("SeguidoId")]
    public virtual Jogador Seguido { get; set; } = null!;
}
