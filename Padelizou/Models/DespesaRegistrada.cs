using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Uma despesa do Padelizou lançada à mão pelo admin raiz — a fatura do gateway é o caso que
// motivou, mas a descrição é livre (VPS, domínio, o que for).
//
// Tabela própria, e NÃO uma linha em Pagamento, de propósito: Pagamento é lido por meia
// dúzia de consultas que somam receita (métricas, alerta do MEI, extrato) e uma despesa
// disfarçada de pagamento inflaria todas elas caladas. Despesa não é faturamento — o teto
// do MEI conta o que ENTRA, não o líquido.
[Table("DespesaRegistrada")]
public class DespesaRegistrada
{
    public int Id { get; set; }

    public string Descricao { get; set; } = null!;

    public decimal Valor { get; set; }

    // O dia em que o dinheiro saiu — é por ela que o período do financeiro recorta.
    public DateTime Data { get; set; }

    // Quem lançou. Hoje só o admin raiz chega aqui, mas o registro fica.
    public int RegistradoPorJogadorId { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;
}
