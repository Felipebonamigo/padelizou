using Padelizou.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace padelizou.Models; // Use o namespace que já está no seu projeto

[Table("GrupoPrivado")]
public partial class GrupoPrivado
{
    public int Id { get; set; }
    public string Nome { get; set; } = null!;
    public string CodigoConvite { get; set; } = null!;
    public int AdministradorId { get; set; }

    // Relacionamento com o Jogador (Admin)
    public virtual Jogador Administrador { get; set; } = null!;
}