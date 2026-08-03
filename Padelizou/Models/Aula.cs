using Padelizou.Models;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace padelizou.Models;

[Table("Aula")]
public partial class Aula
{
    public int Id { get; set; }
    public int ProfessorId { get; set; }
    public int? AlunoId { get; set; }
    public int LocalAulaId { get; set; }
    public DateTime DataHora { get; set; }
    public decimal Preco { get; set; }
    public string Status { get; set; } = null!;

    // Preenchidos só quando o professor adiciona a aula manualmente para um aluno
    // sem conta no sistema (AlunoId fica null nesse caso).
    public string? NomeAlunoAvulso { get; set; }
    public string? TelefoneAlunoAvulso { get; set; }

    // Agrupa as aulas geradas de uma mesma série semanal recorrente (null se avulsa/única)
    public Guid? RecorrenciaId { get; set; }

    // Nome com que o aluno se apresenta NESTA aula. O cadastro pode dizer "Felipe C. B. dos
    // Santos" e o professor conhecer outro nome — e o professor precisa saber quem chega na
    // quadra, não quem está no banco.
    public string? NomeCompletoAluno { get; set; }

    // Quem mais vem nesta aula. Texto livre de propósito: aula de padel é frequentemente em
    // dupla ou trio, e exigir que cada acompanhante tenha conta no site travaria a marcação
    // por causa de quem nem usa o app. O professor só precisa saber quantos e quem.
    public string? Acompanhantes { get; set; }

    // QUANTOS alunos nesta aula (1, 2 ou 3). Acompanhantes diz quem vem; isto diz o tamanho
    // — e tamanho é preço: Services/PrecoDaAula lê daqui qual valor do local se aplica.
    //
    // Nasce em 1 porque toda aula anterior a 03/08/2026 era individual: era o único preço
    // que existia.
    public int QuantidadeAlunos { get; set; } = 1;

    // Token opaco usado no link de aceitar/recusar enviado por e-mail (sem exigir login)
    public Guid TokenConfirmacao { get; set; } = Guid.NewGuid();

    // Preenchido quando o evento é criado na Google Agenda do professor
    public string? GoogleEventId { get; set; }

    // ---- Presença ----
    // null = professor ainda não marcou; true = veio; false = faltou.
    // Quem falta com aviso dentro do prazo é cancelamento (Status), não falta.
    public bool? Compareceu { get; set; }

    // ---- Cancelamento ----
    public DateTime? CanceladaEm { get; set; }
    public string? CanceladaPor { get; set; }   // "Aluno" | "Professor"

    // Falta/cancelamento fora do prazo que o professor decidiu cobrar assim mesmo.
    // Entra na previsão financeira como valor a receber.
    public bool CobrarMesmoFaltando { get; set; }

    // Quantas horas antes da aula o cancelamento foi feito. Guardado no momento do
    // cancelamento porque a política do professor pode mudar depois — o que valeu
    // pro aluno foi a regra do dia.
    public int? HorasDeAntecedenciaCancelamento { get; set; }

    // Quem vai dar a aula
    [ForeignKey("ProfessorId")]
    [InverseProperty("AulasDadas")]
    public virtual Jogador Professor { get; set; } = null!;

    // Quem vai receber a aula (null quando é aluno avulso, ver NomeAlunoAvulso)
    [ForeignKey("AlunoId")]
    [InverseProperty("AulasRecebidas")]
    public virtual Jogador? Aluno { get; set; }

    [ForeignKey("LocalAulaId")]
    public virtual LocalAula LocalAula { get; set; } = null!;
}