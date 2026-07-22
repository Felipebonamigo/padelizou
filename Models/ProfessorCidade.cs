using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Cidades onde o professor atua. Um professor pode ter várias, uma cidade pode ter vários professores.
[Table("ProfessorCidade")]
public partial class ProfessorCidade
{
    public int ProfessorId { get; set; }
    public int CidadeId { get; set; }

    [ForeignKey("ProfessorId")]
    public virtual Jogador Professor { get; set; } = null!;

    [ForeignKey("CidadeId")]
    public virtual Cidade Cidade { get; set; } = null!;
}
