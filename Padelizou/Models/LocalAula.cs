using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

[Table("LocalAula")]
public partial class LocalAula
{
    public int Id { get; set; }
    public int ProfessorId { get; set; }
    public string Nome { get; set; } = null!;

    // Opcional: muito professor dá aula num clube que todo aluno já sabe onde fica, e exigir
    // o endereço travava o cadastro do primeiro local por um dado que ninguém ia ler.
    public string? Endereco { get; set; }

    // O preço da aula INDIVIDUAL — o valor cheio de uma pessoa sozinha na quadra.
    public decimal PrecoPadrao { get; set; }

    // Os preços das TURMAS deste local — a aula inteira com 2, 3, ... alunos dividindo a
    // quadra. Aula quase nunca é um-a-um: dois amigos treinam juntos e pagam um valor
    // diferente do dobro do individual (o professor gasta a mesma hora, então costuma cobrar
    // mais barato por cabeça e mais caro no total), e no beach a turma vai até seis.
    //
    // Eram duas colunas aqui dentro, `PrecoDupla` e `PrecoTrio`, e era nelas que morava o
    // teto de três alunos. Ver Models/PrecoDeTurma. Tamanho sem linha = o professor não faz
    // esse tamanho; quem lê a tabela é Services/PrecoDaAula.
    public virtual ICollection<PrecoDeTurma> PrecosDeTurma { get; set; } = new List<PrecoDeTurma>();

    public bool Ativo { get; set; } = true;

    // Como o local aparece escrito num convite, num e-mail ou no Google Agenda. Existe porque
    // o endereço é opcional: montar "Nome, Endereco" na mão em cada lugar produzia "Chakra, "
    // com a vírgula pendurada no dia em que alguém não preencheu.
    [NotMapped]
    public string NomeComEndereco =>
        string.IsNullOrWhiteSpace(Endereco) ? Nome : $"{Nome}, {Endereco}";

    // Opcional: quanto o professor paga ao local por aula, usado no relatório de gastos.
    public decimal? CustoPorAula { get; set; }

    // Os pacotes de aula deste local (ver Models/PacoteDeAulas). Já foi UM pacote em três
    // colunas aqui dentro; virou coleção porque o professor quase sempre anuncia mais de uma
    // oferta na mesma quadra (4, 8, 12 aulas) e tinha que escolher qual mostrar.
    public virtual ICollection<PacoteDeAulas> Pacotes { get; set; } = new List<PacoteDeAulas>();

    [ForeignKey("ProfessorId")]
    public virtual Jogador Professor { get; set; } = null!;

    public virtual ICollection<HorarioDisponivel> Horarios { get; set; } = new List<HorarioDisponivel>();
}
