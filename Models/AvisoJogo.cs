using System.ComponentModel.DataAnnotations.Schema;
using padelizou.Models;

namespace Padelizou.Models;

[Table("AvisoJogo")]
public partial class AvisoJogo
{
    public int Id { get; set; }
    public int CriadorId { get; set; }
    public int ClubeId { get; set; }
    public int CategoriaPadraoId { get; set; }
    public DateTime DataHora { get; set; }
    public string? Observacoes { get; set; }
    public string Status { get; set; } = "Ativo";
    public DateTime CriadoEm { get; set; } = DateTime.Now;

    [ForeignKey("CriadorId")]
    public virtual Jogador Criador { get; set; } = null!;

    [ForeignKey("ClubeId")]
    public virtual Clube Clube { get; set; } = null!;

    [ForeignKey("CategoriaPadraoId")]
    public virtual CategoriaPadrao CategoriaPadrao { get; set; } = null!;
}
