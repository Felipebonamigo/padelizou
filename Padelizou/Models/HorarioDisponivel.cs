using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

[Table("HorarioDisponivel")]
public partial class HorarioDisponivel
{
    public int Id { get; set; }
    public int ProfessorId { get; set; }
    public int LocalAulaId { get; set; }

    // 0 = Domingo ... 6 = Sábado (mesmo mapeamento de DayOfWeek)
    public int DiaSemana { get; set; }
    public TimeSpan HoraInicio { get; set; }
    public TimeSpan HoraFim { get; set; }
    public int DuracaoMinutos { get; set; } = 60;
    public bool Ativo { get; set; } = true;

    [ForeignKey("ProfessorId")]
    public virtual Jogador Professor { get; set; } = null!;

    [ForeignKey("LocalAulaId")]
    public virtual LocalAula LocalAula { get; set; } = null!;
}
