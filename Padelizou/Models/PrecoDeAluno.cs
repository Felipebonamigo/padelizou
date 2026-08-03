using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// O valor que o professor combinou com UM aluno específico, diferente da tabela do local.
// Existe porque isso é a regra, não a exceção: o aluno antigo que nunca teve reajuste, o
// filho do amigo, quem fecha o mês inteiro. Sem um lugar pra guardar, o professor corrigia
// o preço na mão em toda aula que marcava — e esquecia em uma.
//
// Vale pra aula INDIVIDUAL daquele aluno. Aula em dupla ou trio é outra conta (o valor do
// local pro tamanho), porque o desconto foi dado a uma pessoa e não à quadra inteira.
// A decisão mora em Services/PrecoDaAula, que é onde dá pra testá-la sem banco.
[Table("PrecoDeAluno")]
public class PrecoDeAluno
{
    public int Id { get; set; }

    public int ProfessorId { get; set; }

    // O aluno é identificado de um jeito OU de outro, nunca dos dois: quem tem conta entra
    // por AlunoId; quem o professor só anotou o nome (aula lançada à mão) entra por
    // NomeAvulso. É a mesma divisão que a agenda já faz em Aula.AlunoId/NomeAlunoAvulso —
    // ver PrecoDaAula.Chave, que transforma os dois casos numa chave só.
    public int? AlunoId { get; set; }
    public string? NomeAvulso { get; set; }

    public decimal Preco { get; set; }

    // Por que este aluno paga diferente ("aluno desde 2019", "pacote fechado do mês").
    // Opcional, mas é o que responde a pergunta seis meses depois.
    public string? Observacao { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    [ForeignKey("ProfessorId")]
    public virtual Jogador Professor { get; set; } = null!;

    [ForeignKey("AlunoId")]
    public virtual Jogador? Aluno { get; set; }
}
