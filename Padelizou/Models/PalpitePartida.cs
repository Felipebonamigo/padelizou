using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

[Table("PalpitePartida")]
public class PalpitePartida
{
    public int Id { get; set; }

    public int PartidaId { get; set; }
    public virtual Partida Partida { get; set; } = null!;

    public int JogadorId { get; set; }
    public virtual Jogador Jogador { get; set; } = null!;

    public int DuplaEscolhidaId { get; set; }
    public virtual Dupla DuplaEscolhida { get; set; } = null!;

    public DateTime DataHora { get; set; } = DateTime.Now;
}
