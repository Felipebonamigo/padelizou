using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Quanto custa a aula INTEIRA quando N alunos dividem a mesma hora de quadra.
//
// Eram duas colunas em LocalAula — `PrecoDupla` e `PrecoTrio` — e o teto de três alunos
// morava nelas: não existia onde escrever o preço do quarteto. O primeiro professor de
// beach esbarrou nisso no dia em que entrou (turma de até seis, "sexteto"). Virou tabela
// pelo mesmo motivo que PacoteDeAulas virou: com uma coluna por tamanho, esticar o limite é
// migration nova toda vez, e o `switch` que lê os preços cresce junto — em três telas.
//
// O preço é o TOTAL da aula, não o valor por aluno: é o total que entra no financeiro do
// professor e é o número que ele fala pro aluno. Tamanho sem linha aqui significa "não faço
// esse tamanho", e a sugestão cai pro maior tamanho MENOR que ele anunciou — ver
// Services/PrecoDaAula, que é onde a regra dá pra testar sem banco.
[Table("PrecoDeTurma")]
public class PrecoDeTurma
{
    public int Id { get; set; }

    public int LocalAulaId { get; set; }

    // De 2 até PrecoDaAula.MaxAlunos. A individual não entra aqui: ela é o
    // LocalAula.PrecoPadrao, que é obrigatório — todo local tem preço, nem todo local faz
    // turma. Dar linha pro tamanho 1 criaria dois lugares dizendo quanto custa a individual.
    public int QuantidadeAlunos { get; set; }

    public decimal Preco { get; set; }

    [ForeignKey("LocalAulaId")]
    public virtual LocalAula LocalAula { get; set; } = null!;
}
