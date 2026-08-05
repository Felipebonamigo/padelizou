using System.ComponentModel.DataAnnotations.Schema;
using padelizou.Models;

namespace Padelizou.Models;

// Uma ficha de avaliação técnica: o professor olha o aluno num momento e dá nota por
// fundamento. Várias fichas ao longo do tempo é o que vira EVOLUÇÃO.
//
// O aluno só vê o que o professor liberar, ficha por ficha — pedido do Felipe: "o professor
// vai colocar lá a avaliação dele (se quiser) e vai permitir (se quiser) o aluno ver, tanto
// a avaliação total como depois a evolução". Por isso `VisivelParaAluno` nasce FALSO: a ficha
// é rascunho até alguém dizer o contrário.
[Table("AvaliacaoDeAluno")]
public class AvaliacaoDeAluno
{
    public int Id { get; set; }

    public int ProfessorId { get; set; }

    // Só aluno COM conta: sem conta não há ninguém pra ver a ficha, e o vínculo por nome
    // solto não sobrevive a duas pessoas com o mesmo primeiro nome.
    public int AlunoId { get; set; }

    // O módulo em que o aluno está sendo avaliado (Iniciante, Intermediário, Avançado).
    public string Modulo { get; set; } = null!;

    public DateTime CriadoEm { get; set; } = DateTime.Now;
    public DateTime AtualizadoEm { get; set; } = DateTime.Now;

    // Nasce falso de propósito — ver o comentário da classe.
    public bool VisivelParaAluno { get; set; }

    // Quando foi liberada. Serve pro aluno saber que a ficha é nova e pro professor lembrar
    // desde quando ela está no ar.
    public DateTime? LiberadaEm { get; set; }

    // O que o aluno lê junto com as notas, quando a ficha está liberada.
    public string? RecadoAoAluno { get; set; }

    // O que o aluno NUNCA lê, nem com a ficha liberada: "tem medo de bola alta, ir devagar".
    // A trava é no servidor (o campo nem sai da consulta do lado do aluno) e não em esconder
    // na tela — texto escondido em HTML é texto publicado.
    public string? AnotacaoPrivada { get; set; }

    [ForeignKey("ProfessorId")]
    public virtual Jogador Professor { get; set; } = null!;

    [ForeignKey("AlunoId")]
    public virtual Jogador Aluno { get; set; } = null!;

    public virtual ICollection<NotaDeFundamento> Notas { get; set; } = new List<NotaDeFundamento>();
}
