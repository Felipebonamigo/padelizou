using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Um item que o bar do clube vende: cerveja, água, porção, aluguel de raquete.
//
// O cardápio é POR CLUBE — cada um tem os seus produtos e os seus preços, e não existe
// catálogo central. Um "Heineken lata" do Chakra não é o mesmo do Golden Point: preço
// diferente, e um pode ter e o outro não.
[Table("ProdutoBar")]
public class ProdutoBar
{
    public int Id { get; set; }

    public int ClubeId { get; set; }
    public virtual Clube Clube { get; set; } = null!;

    public string Nome { get; set; } = string.Empty;

    // O preço de HOJE. A comanda nunca lê daqui na hora de somar: cada item guarda o preço
    // que valia quando foi lançado (ver ItemComanda.PrecoUnitario). Sem isso, aumentar a
    // cerveja no sábado mudaria o valor de todas as comandas fechadas do mês.
    public decimal Preco { get; set; }

    // Só pra agrupar os botões na tela do balcão ("Bebidas", "Comidas", "Outros"). Texto
    // livre de propósito: cada clube organiza o cardápio do jeito dele, e uma lista fixa
    // viraria pedido de "adiciona a categoria X" na primeira semana.
    public string? Categoria { get; set; }

    // Produto que saiu de linha fica INATIVO, nunca apagado: as comandas antigas continuam
    // apontando pra ele, e apagar levaria junto o histórico de venda.
    public bool Ativo { get; set; } = true;

    // Ordem manual na tela do balcão. Quem trabalha no bar sabe que a cerveja e a água são
    // 80% dos toques do dia; deixá-las no fim da lista alfabética custa tempo em cada venda.
    public int Ordem { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;
}
