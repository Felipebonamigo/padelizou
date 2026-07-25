using System.ComponentModel.DataAnnotations.Schema;
using padelizou.Models;

namespace Padelizou.Models;

// Categorias que o jogador aceita jogar. Nenhuma linha para um jogador = sem restrição (aceita qualquer categoria).
[Table("JogadorCategoria")]
public partial class JogadorCategoria
{
    public int JogadorId { get; set; }
    public int CategoriaPadraoId { get; set; }

    [ForeignKey("JogadorId")]
    public virtual Jogador Jogador { get; set; } = null!;

    [ForeignKey("CategoriaPadraoId")]
    public virtual CategoriaPadrao CategoriaPadrao { get; set; } = null!;
}
