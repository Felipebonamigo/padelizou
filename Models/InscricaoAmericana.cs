using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Inscrição individual (não em dupla) de um jogador numa categoria de Torneio Americano —
// os parceiros mudam a cada rodada, então não faz sentido usar Dupla aqui.
[Table("InscricaoAmericana")]
public class InscricaoAmericana
{
    public int Id { get; set; }
    public int CategoriaId { get; set; }
    public virtual Categoria Categoria { get; set; } = null!;
    public int JogadorId { get; set; }
    public virtual Jogador Jogador { get; set; } = null!;
}
