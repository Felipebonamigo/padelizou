using Padelizou.Models;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace padelizou.Models;

[Table("Aula")]
public partial class Aula
{
    public int Id { get; set; }
    public int ProfessorId { get; set; }
    public int AlunoId { get; set; }
    public DateTime DataHora { get; set; }
    public string Local { get; set; } = null!;
    public decimal Preco { get; set; }
    public string Status { get; set; } = null!;

    // Quem vai dar a aula
    [ForeignKey("ProfessorId")]
    [InverseProperty("AulasDadas")]
    public virtual Jogador Professor { get; set; } = null!;

    // Quem vai receber a aula
    [ForeignKey("AlunoId")]
    [InverseProperty("AulasRecebidas")]
    public virtual Jogador Aluno { get; set; } = null!;
}