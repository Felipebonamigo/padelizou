using Padelizou.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

[Table("TorneioOrganizador")]
public partial class TorneioOrganizador
{
    public int TorneioId { get; set; }
    public int JogadorId { get; set; }
    public string NivelAcesso { get; set; } = "Ajudante";

    public virtual Torneio Torneio { get; set; } = null!;
    public virtual Jogador Jogador { get; set; } = null!;
}