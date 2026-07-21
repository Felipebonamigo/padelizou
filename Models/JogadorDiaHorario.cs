using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Dias/períodos preferidos do jogador. Nenhuma linha para um jogador = sem restrição (aceita qualquer dia/horário).
[Table("JogadorDiaHorario")]
public partial class JogadorDiaHorario
{
    public int JogadorId { get; set; }

    // 0 = Domingo ... 6 = Sábado (mesmo mapeamento de DayOfWeek)
    public int DiaSemana { get; set; }

    // "Manhã" / "Tarde" / "Noite"
    public string Periodo { get; set; } = null!;

    [ForeignKey("JogadorId")]
    public virtual Jogador Jogador { get; set; } = null!;
}
