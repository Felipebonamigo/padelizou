using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Inscrição individual (não em dupla) de um jogador numa categoria de Torneio Americano —
// os parceiros mudam a cada rodada, então não faz sentido usar Dupla aqui.
[Table("InscricaoAmericana")]
public class InscricaoAmericana
{
    public int Id { get; set; }
    public int CategoriaId { get; set; }
    public virtual Categoria Categoria { get; set; } = null!;
    public int JogadorId { get; set; }
    public virtual Jogador Jogador { get; set; } = null!;

    // Mesmo esquema de lista de espera do Dupla — ver Dupla.EmListaDeEspera.
    public bool EmListaDeEspera { get; set; }

    // Nulo = inscrição feita antes de 25/07/2026 (quando a coluna nasceu) — usado nas
    // métricas de uso do admin (inscrições por semana).
    public DateTime? CriadoEm { get; set; } = DateTime.Now;
}
