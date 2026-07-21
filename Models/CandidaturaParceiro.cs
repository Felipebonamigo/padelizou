using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

[Table("CandidaturaParceiro")]
public partial class CandidaturaParceiro
{
    public int Id { get; set; }
    public int AvisoParceiroId { get; set; }
    public int CandidatoId { get; set; }
    public string Status { get; set; } = "Pendente";
    public DateTime CriadoEm { get; set; } = DateTime.Now;

    [ForeignKey("AvisoParceiroId")]
    public virtual AvisoParceiro AvisoParceiro { get; set; } = null!;

    [ForeignKey("CandidatoId")]
    public virtual Jogador Candidato { get; set; } = null!;
}
