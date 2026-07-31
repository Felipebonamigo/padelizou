using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Uma linha da comanda: "2 × Heineken lata, R$ 12,00 cada".
//
// Guarda o nome e o preço COPIADOS do produto no momento do lançamento, de propósito. Se a
// comanda somasse lendo ProdutoBar.Preco, aumentar a cerveja às 20h mudaria o valor de tudo
// que foi vendido às 15h — e das comandas já fechadas do mês inteiro. O preço que vale é o
// que estava na parede quando a pessoa pediu.
[Table("ItemComanda")]
public class ItemComanda
{
    public int Id { get; set; }

    public int ComandaId { get; set; }
    public virtual Comanda Comanda { get; set; } = null!;

    // Qual produto era. Opcional porque o balcão precisa poder lançar algo que não está no
    // cardápio ("2 fichas de estacionamento") sem parar a venda pra cadastrar produto.
    public int? ProdutoBarId { get; set; }
    public virtual ProdutoBar? ProdutoBar { get; set; }

    // Cópia do nome no momento do lançamento: o produto pode ser renomeado depois, e a
    // comanda de ontem tem que continuar dizendo o que foi vendido ontem.
    public string Descricao { get; set; } = string.Empty;

    // Cópia do preço. É este valor que soma — nunca o preço atual do produto.
    public decimal PrecoUnitario { get; set; }

    public int Quantidade { get; set; } = 1;

    public DateTime LancadoEm { get; set; } = DateTime.Now;
    public int? LancadoPorId { get; set; }

    // Cancelar NUNCA apaga a linha. Item que some da comanda é a forma mais comum de furto
    // num bar; deixando o registro com quem cancelou e por quê, o dono consegue auditar
    // ("por que sumiram 6 cervejas do turno da sexta?").
    public DateTime? CanceladoEm { get; set; }
    public int? CanceladoPorId { get; set; }
    public string? MotivoCancelamento { get; set; }

    [NotMapped]
    public decimal Total => PrecoUnitario * Quantidade;

    [NotMapped]
    public bool Vale => CanceladoEm == null;
}
