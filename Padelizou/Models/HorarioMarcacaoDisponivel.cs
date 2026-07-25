using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Regra recorrente semanal — mesmo formato de HorarioDisponivel, mas escopado a Clube/
// QuadraClube (marcação de jogo) em vez de Professor/LocalAula (aula particular).
[Table("HorarioMarcacaoDisponivel")]
public partial class HorarioMarcacaoDisponivel
{
    public int Id { get; set; }
    public int ClubeId { get; set; }
    public int QuadraClubeId { get; set; }

    // 0 = Domingo ... 6 = Sábado (mesmo mapeamento de DayOfWeek)
    public int DiaSemana { get; set; }
    public TimeSpan HoraInicio { get; set; }
    public TimeSpan HoraFim { get; set; }
    public int DuracaoMinutos { get; set; } = 60;
    public bool Ativo { get; set; } = true;

    // Preço do slot inteiro (não por hora) — uma regra de 1h30 a R$ 120 cobra os R$ 120.
    // Horário nobre é só outra regra, com faixa de hora e preço próprios. Null ou zero =
    // quadra gratuita ou paga por fora, e nenhuma cobrança é gerada.
    public decimal? Preco { get; set; }

    [ForeignKey("ClubeId")]
    public virtual Clube Clube { get; set; } = null!;

    [ForeignKey("QuadraClubeId")]
    public virtual QuadraClube QuadraClube { get; set; } = null!;
}
