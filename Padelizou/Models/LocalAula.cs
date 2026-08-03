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

    // Quanto custa a aula INTEIRA quando dois ou três alunos dividem a quadra. Aula de padel
    // quase nunca é um-a-um: dois amigos treinam juntos e pagam um valor diferente do dobro
    // do individual — o professor gasta a mesma hora, então costuma cobrar mais barato por
    // cabeça e mais caro no total.
    //
    // Nulo = o professor não anunciou preço pra esse tamanho, e vale o individual (era o
    // comportamento antigo, quando só existia um preço). Quem informa, informa o TOTAL da
    // aula, não o valor por aluno: é o total que entra no financeiro e o que ele fala pro
    // aluno. Ver Services/PrecoDaAula.
    public decimal? PrecoDupla { get; set; }
    public decimal? PrecoTrio { get; set; }

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
