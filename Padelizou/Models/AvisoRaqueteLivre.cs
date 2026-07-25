using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

[Table("AvisoRaqueteLivre")]
public partial class AvisoRaqueteLivre
{
    public int Id { get; set; }
    public int ClubeId { get; set; }
    public int CriadorId { get; set; }
    public DateTime DataHoraInicio { get; set; }
    public DateTime DataHoraFim { get; set; }
    public decimal? Preco { get; set; }
    public string? Observacoes { get; set; }

    // Vagas máximas — null = sem limite. Mesmo padrão de Categoria.LimiteDuplas.
    public int? LimiteVagas { get; set; }
    public string Status { get; set; } = "Ativo";
    public DateTime CriadoEm { get; set; } = DateTime.Now;

    [ForeignKey("ClubeId")]
    public virtual Clube Clube { get; set; } = null!;

    [ForeignKey("CriadorId")]
    public virtual Jogador Criador { get; set; } = null!;
}
