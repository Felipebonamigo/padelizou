using System.ComponentModel.DataAnnotations.Schema;

namespace padelizou.Models;

[Table("CategoriaPadrao")]
public partial class CategoriaPadrao
{
    public int Id { get; set; }
    public string Nome { get; set; } = null!;
    public string Codigo { get; set; } = null!;
    public string Tipo { get; set; } = null!;
}