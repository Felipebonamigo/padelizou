using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

[Table("LocalAula")]
public partial class LocalAula
{
    public int Id { get; set; }
    public int ProfessorId { get; set; }
    public string Nome { get; set; } = null!;
    public string Endereco { get; set; } = null!;
    public decimal PrecoPadrao { get; set; }
    public bool Ativo { get; set; } = true;

    // Opcional: quanto o professor paga ao local por aula, usado no relatório de gastos.
    public decimal? CustoPorAula { get; set; }

    [ForeignKey("ProfessorId")]
    public virtual Jogador Professor { get; set; } = null!;

    public virtual ICollection<HorarioDisponivel> Horarios { get; set; } = new List<HorarioDisponivel>();
}
