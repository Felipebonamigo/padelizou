using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

[Table("InscricaoRaqueteLivre")]
public partial class InscricaoRaqueteLivre
{
    public int AvisoRaqueteLivreId { get; set; }
    public int JogadorId { get; set; }
    public bool EmListaDeEspera { get; set; }
    public DateTime InscritoEm { get; set; } = DateTime.Now;

    [ForeignKey("AvisoRaqueteLivreId")]
    public virtual AvisoRaqueteLivre AvisoRaqueteLivre { get; set; } = null!;

    [ForeignKey("JogadorId")]
    public virtual Jogador Jogador { get; set; } = null!;
}
