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

    // De 1 a 5. Sem meia-estrela: simplifica a média e a leitura.
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
