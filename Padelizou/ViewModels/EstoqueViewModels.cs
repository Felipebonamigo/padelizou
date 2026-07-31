using Padelizou.Models;

namespace Padelizou.ViewModels;

// Uma linha da tela de estoque: o produto com tudo que se pergunta sobre ele de uma vez —
// quanto tem, quanto custou da última vez, quanto sobra e se está na hora de comprar.
public class EstoqueLinhaVM
{
    public ProdutoBar Produto { get; set; } = null!;

    public int Saldo { get; set; }

    // Da ÚLTIMA entrada, não média: é o preço que o dono vai pagar na próxima compra, e é com
    // ele que a decisão de reajustar é tomada.
    public decimal? UltimoCusto { get; set; }

    // null quando ainda não houve compra registrada — sem custo não há margem que se calcule.
    public (decimal Lucro, decimal? Percentual)? Margem { get; set; }

    public bool PrecisaRepor { get; set; }
}
