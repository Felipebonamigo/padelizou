using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Um pacote de aulas de um local: "4 aulas por R$ 400".
//
// Era UM pacote por local, gravado em três colunas do próprio LocalAula. Virou tabela porque
// o professor quase sempre tem mais de uma oferta na mesma quadra — 4 aulas, 8 aulas, 12
// aulas, cada uma com desconto maior — e com campo único ele tinha que escolher qual anunciar.
//
// Continua só INFORMATIVO: o pagamento é combinado direto com o professor (ex: Pix), sem
// cobrança nem controle de créditos pelo site. O que o sistema faz é criar as N aulas da
// série já com o preço rateado.
[Table("PacoteDeAulas")]
public partial class PacoteDeAulas
{
    public int Id { get; set; }
    public int LocalAulaId { get; set; }

    public int QuantidadeAulas { get; set; }
    public decimal Preco { get; set; }

    // Desativado some da tela do aluno mas continua no cadastro do professor: apagar um pacote
    // que já foi vendido apagaria a explicação do preço das aulas que ele gerou.
    public bool Ativo { get; set; } = true;

    [ForeignKey("LocalAulaId")]
    public virtual LocalAula LocalAula { get; set; } = null!;

    // O que o aluno realmente compara. Arredonda pra baixo do centavo: a sobra da divisão vai
    // pra última aula da série (ver AulasController.Solicitar), pra fechar exatamente no Preco.
    [NotMapped]
    public decimal PrecoPorAula => QuantidadeAulas > 0
        ? Math.Round(Preco / QuantidadeAulas, 2)
        : Preco;
}
