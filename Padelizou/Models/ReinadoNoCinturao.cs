using System.ComponentModel.DataAnnotations.Schema;
using padelizou.Models;

namespace Padelizou.Models;

// UM REINADO no cinturão de uma categoria: quem foi dono, de quando até quando, e quantas vezes
// defendeu. Regras completas em DESAFIOS.md, seção 4.
//
// ⚠️ UMA TABELA SÓ, e não "cinturão atual" + "histórico". O dono de hoje é o reinado com
// `TerminouEm` nulo. Duas tabelas obrigariam toda troca de mão a escrever nas duas, e o dia em
// que uma gravação falhasse deixaria um dono no cinturão e outro no histórico — sem ninguém
// saber qual está certo.
//
// ⚠️ É DA DUPLA, NÃO DO JOGADOR. Trocou de parceiro, começa de novo: o cinturão é do par, e é
// isso que faz a dupla virar um personagem. Os dois ids ficam em ordem canônica (menor primeiro,
// ver Services/ChaveDaDupla) pra que "Felipe & Lucas" case com "Lucas & Felipe".
//
// 🔒 O recorte é a CATEGORIA, sem cidade (decisão do Felipe, 11/08/2026). "Categoria × cidade"
// era o desenho da espec, mas `Clube.CidadeId` existe e nada preenche — o cinturão nasceria
// vazio ou errado, calado. E com a base de hoje um cinturão por categoria é o único recorte em
// que ele troca de mão com frequência: cinturão que ninguém disputa é enfeite. Virar
// categoria × cidade depois é uma coluna a mais, não uma tabela nova.
[Table("ReinadoNoCinturao")]
public class ReinadoNoCinturao
{
    // Como o reinado acabou. Nulo enquanto ele é o atual.
    public const string PerdeuNaQuadra = "Perdeu na quadra";
    public const string PerdeuPorNaoDefender = "Não defendeu";

    public int Id { get; set; }

    public int CategoriaPadraoId { get; set; }

    // A dupla dona, sempre com Jogador1Id < Jogador2Id.
    public int Jogador1Id { get; set; }
    public int Jogador2Id { get; set; }

    public DateTime ComecouEm { get; set; }

    // Nulo = é o reinado ATUAL. É esta coluna que responde "quem tem o cinturão?".
    public DateTime? TerminouEm { get; set; }

    // Quantas vezes esta dupla ganhou um desafio JÁ COM o cinturão na mão. É o número que dá
    // peso ao reinado — "3 defesas" conta uma história que "desde 04/08" não conta.
    public int Defesas { get; set; }

    // O desafio que deu o cinturão. Nulo quando a dupla o pegou VAGO (primeiro desafio
    // confirmado da categoria) ou quando ganhou por omissão do dono anterior.
    public int? DesafioDeConquistaId { get; set; }

    // O desafio que tirou o cinturão. Nulo enquanto o reinado corre.
    public int? DesafioDaPerdaId { get; set; }

    public string? ComoTerminou { get; set; }

    [ForeignKey(nameof(CategoriaPadraoId))]
    public virtual CategoriaPadrao CategoriaPadrao { get; set; } = null!;

    [ForeignKey(nameof(Jogador1Id))]
    public virtual Jogador Jogador1 { get; set; } = null!;

    [ForeignKey(nameof(Jogador2Id))]
    public virtual Jogador Jogador2 { get; set; } = null!;

    [NotMapped]
    public bool Atual => TerminouEm == null;

    [NotMapped]
    public IEnumerable<int> Donos => new[] { Jogador1Id, Jogador2Id };
}
