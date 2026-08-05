using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// A nota de UM fundamento dentro de uma ficha.
//
// São duas réguas independentes, as mesmas da planilha do professor:
//  • Execução (A/B/C/D): COMO o golpe sai — D longa e lenta sem direção, C com direção,
//    B técnica, A plástica.
//  • Acertos (1 a 10): QUANTO entra — 1-3 baixo, 3-5 médio, 5-7 bom, 8-10 aplicável em jogo.
//
// As duas são opcionais: o professor avalia o que trabalhou na aula, não os 146 itens. Linha
// sem nenhuma das duas simplesmente não é gravada (ver Services/EscalaDeAvaliacao).
[Table("NotaDeFundamento")]
public class NotaDeFundamento
{
    public int Id { get; set; }

    public int AvaliacaoDeAlunoId { get; set; }

    public int FundamentoDoProfessorId { get; set; }

    // "A", "B", "C" ou "D" — null quando o professor só marcou acertos.
    public string? Execucao { get; set; }

    // 1 a 10 — null quando ele só marcou execução.
    public int? Acertos { get; set; }

    [ForeignKey("AvaliacaoDeAlunoId")]
    public virtual AvaliacaoDeAluno Avaliacao { get; set; } = null!;

    [ForeignKey("FundamentoDoProfessorId")]
    public virtual FundamentoDoProfessor Fundamento { get; set; } = null!;
}
