using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

[Table("QuadraClube")]
public partial class QuadraClube
{
    public int Id { get; set; }
    public int ClubeId { get; set; }
    public string Nome { get; set; } = null!;
    public bool Ativa { get; set; } = true;

    [ForeignKey("ClubeId")]
    public virtual Clube Clube { get; set; } = null!;
}
