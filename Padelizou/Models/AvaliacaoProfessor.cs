using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Nota e depoimento que o aluno deixa pro professor. Só quem teve aula REALIZADA com
// ele pode avaliar (regra no controller) — é o que separa isso de comentário aberto.
[Table("AvaliacaoProfessor")]
public class AvaliacaoProfessor
{
    public int Id { get; set; }

    public int ProfessorId { get; set; }
    public int AlunoId { get; set; }

    // De 0 a 10, como nota de escola — foi assim que o Felipe pediu, e é a mesma escala do
    // canal de opinião do site. (Era 1 a 5 até 29/07/2026; as notas antigas foram
    // multiplicadas por 2 na migração, então 5 estrelas viraram nota 10.)
    public int Nota { get; set; }

    // Opcional — muita gente dá nota e não escreve nada.
    public string? Depoimento { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    // O aluno pode reescrever a avaliação; guardamos quando mexeu pela última vez.
    public DateTime? AtualizadoEm { get; set; }

    [ForeignKey("ProfessorId")]
    public virtual Jogador Professor { get; set; } = null!;

    [ForeignKey("AlunoId")]
    public virtual Jogador Aluno { get; set; } = null!;
}
