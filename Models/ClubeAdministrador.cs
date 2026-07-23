using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

[Table("ClubeAdministrador")]
public partial class ClubeAdministrador
{
    public int ClubeId { get; set; }
    public int JogadorId { get; set; }
    public DateTime AdicionadoEm { get; set; } = DateTime.Now;

    public virtual Clube Clube { get; set; } = null!;
    public virtual Jogador Jogador { get; set; } = null!;
}
