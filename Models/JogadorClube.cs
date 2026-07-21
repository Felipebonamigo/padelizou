using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Clubes/locais preferidos do jogador. Nenhuma linha para um jogador = sem restrição (aceita qualquer local).
[Table("JogadorClube")]
public partial class JogadorClube
{
    public int JogadorId { get; set; }
    public int ClubeId { get; set; }

    [ForeignKey("JogadorId")]
    public virtual Jogador Jogador { get; set; } = null!;

    [ForeignKey("ClubeId")]
    public virtual Clube Clube { get; set; } = null!;
}
