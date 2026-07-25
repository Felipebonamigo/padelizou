using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

[Table("Quadra")]
public partial class Quadra
{
    public int Id { get; set; }
    public int TorneioId { get; set; }
    public string Nome { get; set; } = null!;

    // Relacionamento
    public virtual Torneio Torneio { get; set; } = null!;
}