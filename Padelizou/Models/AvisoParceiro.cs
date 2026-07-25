using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

[Table("AvisoParceiro")]
public partial class AvisoParceiro
{
    public int Id { get; set; }
    public int CriadorId { get; set; }
    public string Local { get; set; } = null!;
    public DateTime DataHora { get; set; }

    // Qual app/plataforma hospeda o torneio (ex: Playtomic, Matchpoint)
    public string NomeTorneio { get; set; } = null!;
    public string? Observacoes { get; set; }
    public string Status { get; set; } = "Ativo";
    public DateTime CriadoEm { get; set; } = DateTime.Now;

    [ForeignKey("CriadorId")]
    public virtual Jogador Criador { get; set; } = null!;

    public virtual ICollection<CandidaturaParceiro> Candidaturas { get; set; } = new List<CandidaturaParceiro>();
}
