using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

[Table("MarcacaoJogo")]
public partial class MarcacaoJogo
{
    public int Id { get; set; }
    public int ClubeId { get; set; }
    public int QuadraClubeId { get; set; }
    public int JogadorId { get; set; }
    public DateTime DataHora { get; set; }
    public int DuracaoMinutos { get; set; }
    public string Status { get; set; } = "Confirmada";
    public DateTime CriadoEm { get; set; } = DateTime.Now;

    [ForeignKey("ClubeId")]
    public virtual Clube Clube { get; set; } = null!;

    [ForeignKey("QuadraClubeId")]
    public virtual QuadraClube QuadraClube { get; set; } = null!;

    [ForeignKey("JogadorId")]
    public virtual Jogador Jogador { get; set; } = null!;
}
