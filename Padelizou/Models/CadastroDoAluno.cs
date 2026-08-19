using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// O que o professor sabe sobre UM aluno dele — separado das aulas, que são o que aconteceu.
//
// POR QUE EXISTE: até aqui o aluno sem conta era um NOME digitado dentro de cada aula
// (`Aula.NomeAlunoAvulso`), e o telefone era redigitado a cada marcação. Não havia onde
// guardar nada sobre a pessoa: nem um celular estável, nem — e isso é o que trava a cobrança
// mensal — quem paga por ela. Aluno criança não paga a própria aula.
//
// A IDENTIDADE É A MESMA DE `PrecoDeAluno`, de propósito: conta (`AlunoId`) quando existe,
// nome anotado (`NomeAvulso`) quando não, e `Services/PrecoDaAula.Chave` transforma os dois
// num só. Repetir o par em vez de inventar uma terceira forma de dizer "esta pessoa" é o que
// deixa a ficha, o acordo de preço e as aulas se encontrarem sem nenhuma conversão no meio.
[Table("CadastroDoAluno")]
public class CadastroDoAluno
{
    public int Id { get; set; }

    public int ProfessorId { get; set; }

    public int? AlunoId { get; set; }
    public string? NomeAvulso { get; set; }

    // O cadastro rápido: nome e ESTE número, e acabou. É o dado que o professor tem na mão na
    // beira da quadra — CPF ele não tem, e exigir trava a marcação (foi o pedido do Rafael,
    // que cadastra aluno no meio da aula).
    //
    // Guardado só com dígitos (ver Services/CadastrosDeAlunos): é assim que ele casa com o
    // celular de uma conta do Padelizou no dia em que a pessoa se cadastrar.
    public string? Celular { get; set; }

    // ---- Quem paga ----
    // Vazio = o próprio aluno paga. Preenchido = a cobrança do mês sai no nome de outra
    // pessoa (o pai, a mãe, a empresa) sem que o aluno deixe de ser o aluno na agenda.
    public string? ResponsavelNome { get; set; }
    public string? ResponsavelCelular { get; set; }

    // ⚠️ O CPF é do PAGADOR, e é exigência do gateway: sem ele não dá pra emitir cobrança
    // (ver AsaasService.ObterOuCriarClienteAsync, que recusa cliente sem documento). Do
    // ALUNO o sistema nunca pede CPF — é justamente o dado que o professor não tem.
    public string? ResponsavelCpf { get; set; }

    // Alergia, horário que não pode, o que combinaram. Texto livre porque é caderno.
    public string? Observacao { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    [ForeignKey("ProfessorId")]
    public virtual Jogador Professor { get; set; } = null!;

    [ForeignKey("AlunoId")]
    public virtual Jogador? Aluno { get; set; }
}
