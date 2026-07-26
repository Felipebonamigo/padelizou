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
    // "Confirmada" | "Cancelada" | "Faltou" (no-show) | "Realizada"
    public string Status { get; set; } = "Confirmada";
    public DateTime CriadoEm { get; set; } = DateTime.Now;

    // Agrupa as reservas geradas de um mesmo horário fixo (mensalista). Null = avulsa.
    public Guid? MensalidadeId { get; set; }

    // Bloqueio do clube (manutenção, evento, aula): ocupa a quadra sem ser reserva de
    // jogador. JogadorId aponta pro dono/admin que criou, pra não mexer no FK.
    public bool EhBloqueio { get; set; }
    public string? MotivoBloqueio { get; set; }

    // ---- Cancelamento / no-show ----
    public DateTime? CanceladaEm { get; set; }
    public string? CanceladaPor { get; set; }   // "Jogador" | "Clube"
    public bool CobrarMesmoAssim { get; set; }

    [ForeignKey("ClubeId")]
    public virtual Clube Clube { get; set; } = null!;

    [ForeignKey("QuadraClubeId")]
    public virtual QuadraClube QuadraClube { get; set; } = null!;

    [ForeignKey("JogadorId")]
    public virtual Jogador Jogador { get; set; } = null!;
}
