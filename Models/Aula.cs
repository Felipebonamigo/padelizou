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
    public int LocalAulaId { get; set; }
    public DateTime DataHora { get; set; }
    public decimal Preco { get; set; }
    public string Status { get; set; } = null!;

    // Token opaco usado no link de aceitar/recusar enviado por e-mail (sem exigir login)
    public Guid TokenConfirmacao { get; set; } = Guid.NewGuid();

    // Preenchido quando o evento é criado na Google Agenda do professor
    public string? GoogleEventId { get; set; }

    // Quem vai dar a aula
    [ForeignKey("ProfessorId")]
    [InverseProperty("AulasDadas")]
    public virtual Jogador Professor { get; set; } = null!;

    // Quem vai receber a aula
    [ForeignKey("AlunoId")]
    [InverseProperty("AulasRecebidas")]
    public virtual Jogador Aluno { get; set; } = null!;

    [ForeignKey("LocalAulaId")]
    public virtual LocalAula LocalAula { get; set; } = null!;
}