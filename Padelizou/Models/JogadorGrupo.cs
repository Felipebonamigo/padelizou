using Padelizou.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace padelizou.Models;

[Table("JogadorGrupo")]
public partial class JogadorGrupo
{
    public int JogadorId { get; set; }
    public int GrupoId { get; set; }
    public int PontuacaoInterna { get; set; }

    // Relacionamentos
    [ForeignKey("JogadorId")]
    public virtual Jogador Jogador { get; set; } = null!;

    [ForeignKey("GrupoId")]
    public virtual GrupoPrivado GrupoPrivado { get; set; } = null!;
}