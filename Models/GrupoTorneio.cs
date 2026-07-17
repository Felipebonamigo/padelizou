using Padelizou.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace padelizou.Models;

[Table("GrupoTorneio")]
public partial class GrupoTorneio
{
    public int Id { get; set; }
    public int CategoriaId { get; set; }
    public string Nome { get; set; } = null!;

    public virtual Categoria Categoria { get; set; } = null!;

    // As duplas que caíram neste grupo
    public virtual ICollection<Dupla> Duplas { get; set; } = new List<Dupla>();
}