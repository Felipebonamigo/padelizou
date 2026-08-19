using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// A conta do MÊS de um aluno: "abril, 8 aulas, R$ 880, vence dia 10 de maio".
//
// POR QUE EXISTE: o professor que cobra por mês fecha as aulas do mês e cobra no mês
// seguinte. O financeiro sabia somar um período qualquer, mas somar não é cobrar — não havia
// nada que dissesse "esta conta foi fechada, é deste valor, vence neste dia e ainda não foi
// paga". Sem isso, o controle do que cada aluno deve vivia numa planilha ao lado.
//
// A identidade do aluno é a MESMA do cadastro e do acordo de preço (conta ou nome anotado),
// para as três coisas se encontrarem sem conversão no meio.
[Table("FaturaDoAluno")]
public class FaturaDoAluno
{
    public int Id { get; set; }

    public int ProfessorId { get; set; }

    public int? AlunoId { get; set; }
    public string? NomeAvulso { get; set; }

    // A COMPETÊNCIA: o mês em que as aulas aconteceram, que não é o mês em que se cobra.
    // Guardar os dois separados é o que deixa "fechei abril agora em maio" ser dito sem
    // ambiguidade — e é o que impede fechar abril duas vezes.
    public int Ano { get; set; }
    public int Mes { get; set; }

    public decimal Valor { get; set; }
    public int QuantidadeAulas { get; set; }

    // ---- Quem paga, CONGELADO no fechamento ----
    // Copiado do cadastro em vez de lido dele na hora de mostrar, de propósito: a fatura é um
    // documento. Trocar o responsável do aluno em junho não pode reescrever a conta de abril
    // que já foi mandada pra outra pessoa.
    public string PagadorNome { get; set; } = null!;
    public string? PagadorCelular { get; set; }
    public string? PagadorCpf { get; set; }

    public DateTime FechadaEm { get; set; }
    public DateTime Vencimento { get; set; }

    // "Aberta" | "Paga" | "Cancelada" — ver Services/FechamentoDoMes.
    public string Status { get; set; } = null!;

    public DateTime? PagaEm { get; set; }

    // A cobrança no gateway, quando ela existe. Nula na fatura acertada por fora (Pix na mão,
    // dinheiro), que é como a maior parte das aulas ainda é paga hoje.
    public int? PagamentoId { get; set; }

    [ForeignKey("ProfessorId")]
    public virtual Jogador Professor { get; set; } = null!;

    [ForeignKey("AlunoId")]
    public virtual Jogador? Aluno { get; set; }

    [ForeignKey("PagamentoId")]
    public virtual Pagamento? Pagamento { get; set; }
}
