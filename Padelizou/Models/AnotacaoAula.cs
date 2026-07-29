using System.ComponentModel.DataAnnotations.Schema;
using padelizou.Models;

namespace Padelizou.Models;

// Anotação sobre UMA aula: "trabalhamos bandeja e saída de parede", "aluno evoluiu no
// voleio", "reagendar o horário da próxima". Professor e aluno escrevem no mesmo fio, e
// cada linha guarda o autor — é um caderno de aula compartilhado, não um chat.
[Table("AnotacaoAula")]
public class AnotacaoAula
{
    public int Id { get; set; }

    public int AulaId { get; set; }

    // Quem escreveu: o professor ou o aluno da aula (a regra de quem pode está em
    // Services/AnotacoesDeAula). Aluno avulso (sem conta) não escreve — não tem como logar.
    public int AutorId { get; set; }

    public string Texto { get; set; } = null!;

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    [ForeignKey("AulaId")]
    public virtual Aula Aula { get; set; } = null!;

    [ForeignKey("AutorId")]
    public virtual Jogador Autor { get; set; } = null!;
}
