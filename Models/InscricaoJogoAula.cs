using Padelizou.Models;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace padelizou.Models;

[Table("InscricaoJogoAula")]
public partial class InscricaoJogoAula
{
    public int JogoAulaId { get; set; }
    public int JogadorId { get; set; }
    public bool EmListaDeEspera { get; set; }
    public DateTime InscritoEm { get; set; } = DateTime.Now;

    [ForeignKey("JogoAulaId")]
    public virtual JogoAula JogoAula { get; set; } = null!;

    [ForeignKey("JogadorId")]
    public virtual Jogador Jogador { get; set; } = null!;
}
