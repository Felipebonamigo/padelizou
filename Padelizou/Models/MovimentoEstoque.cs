using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Cada entrada e saída de um produto do bar. O saldo é a SOMA disto — não existe coluna
// "quantidade em estoque" em lugar nenhum.
//
// Por que soma de movimentos e não um número guardado: um número guardado responde "tem 8"
// mas não responde "por que tem 8", que é a pergunta que o dono faz quando a prateleira
// discorda. Com os movimentos, dá pra ver que entraram 24, saíram 14 pela comanda e 2 foram
// registradas como quebra — e é isso que permite auditar. Guardar o total também abre a porta
// pro saldo divergir da soma no dia em que uma gravação falhar no meio.
//
// A Quantidade é SINALIZADA: entrada positiva, saída negativa. Assim o saldo é um SUM puro,
// sem CASE por tipo — e sem a chance de alguém somar uma perda como se fosse compra.
[Table("MovimentoEstoque")]
public class MovimentoEstoque
{
    // Compra do fornecedor. É o único tipo que carrega custo.
    public const string Entrada = "Entrada";

    // Baixa automática, lançada junto com o item na comanda. Ninguém digita.
    public const string Venda = "Venda";

    // Quebrou, venceu, funcionário consumiu, saiu de cortesia sem passar pela comanda. O
    // mundo vaza, e sem um jeito FÁCIL de registrar isso o saldo descola da prateleira em
    // duas semanas.
    public const string Perda = "Perda";

    // O acerto depois de contar a prateleira. É o que mantém o sistema honesto: contagem
    // semanal de dez minutos vale mais que qualquer precisão prometida.
    public const string Contagem = "Contagem";

    // A devolução de uma venda cancelada. Tipo próprio, e não uma "venda positiva": no
    // histórico tem que dar pra ler "saiu 1, voltou 1" em vez de duas vendas que se anulam.
    public const string Estorno = "Estorno";

    public int Id { get; set; }

    public int ProdutoBarId { get; set; }
    public virtual ProdutoBar ProdutoBar { get; set; } = null!;

    public string Tipo { get; set; } = Entrada;

    // Positiva entra, negativa sai. Em unidade de VENDA, sempre — a embalagem já foi
    // convertida antes de chegar aqui.
    public int Quantidade { get; set; }

    // Quanto custou CADA unidade de venda. Só na entrada; nos outros é null. É daqui que sai
    // a resposta pra "quanto eu gasto de cerveja por mês" e, junto com o preço, a margem.
    public decimal? CustoUnitario { get; set; }

    // A linha da comanda que gerou a baixa. Guardada pra que cancelar o item devolva a
    // unidade ao estoque sem depender de ninguém lembrar de fazer isso à mão.
    public int? ItemComandaId { get; set; }

    public string? Motivo { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;
    public int? CriadoPorId { get; set; }

    [NotMapped]
    public decimal? CustoTotal => CustoUnitario * Quantidade;
}
