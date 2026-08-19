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

    // ---- Estoque (opcional, por produto) ----
    //
    // Ligado produto a produto, e não no bar inteiro, porque estoque só é honesto onde a
    // conta fecha: lata de cerveja entra e sai inteira. Porção de fritas e caipirinha não —
    // a saída delas depende da mão de quem serve, e um saldo que mente é pior que saldo
    // nenhum, porque o dono para de confiar e volta pro caderno.
    public bool ControlaEstoque { get; set; }

    // Quantas UNIDADES DE VENDA vêm em uma embalagem de compra (caixa com 24 latas → 24).
    // 1 = compra e vende na mesma unidade. Serve só pra conversão na hora da entrada: o
    // estoque em si é sempre contado em unidade de venda, que é o que o cliente pede.
    public int UnidadesPorEmbalagem { get; set; } = 1;

    // Abaixo disso a tela avisa. Zero desliga o aviso.
    public int EstoqueMinimo { get; set; }

    // ---- Dados fiscais (ver Services/FiscalDoProduto) ----
    //
    // Nulos até alguém preencher: o clube que só usa o bar como controle interno nunca toca
    // nisso, e o cardápio continua funcionando exatamente como antes.
    //
    // ⚠️ A pergunta que precisa vir ANTES de todas: o que este item é? O cardápio de um bar
    // de clube mistura três naturezas diferentes na mesma lista — a cerveja é mercadoria
    // revendida, a porção de fritas o próprio bar produziu, e o "aluguel de raquete" que este
    // arquivo cita lá em cima não é nem uma coisa nem outra. Cada uma sai (ou não sai) num
    // documento diferente, e sem esse campo nenhuma camada fiscal consegue nem escolher qual.
    // Os valores estão em FiscalDoProduto.
    public string? TipoFiscal { get; set; }

    // NCM (8 dígitos) — a classificação da mercadoria. Obrigatório na NFC-e.
    public string? Ncm { get; set; }

    // CFOP (4 dígitos) — a natureza da operação. Muda com o tipo: revenda dentro do estado é
    // 5102, produção do próprio estabelecimento é 5101. Sugerido, nunca imposto.
    public string? Cfop { get; set; }

    // CEST (7 dígitos) — só existe pra quem está em substituição tributária, que é
    // justamente o caso da bebida no Brasil. Fica em branco até o contador do clube dizer.
    public string? Cest { get; set; }

    // Código de barras (GTIN). OPCIONAL na nota, mas conferido pela SEFAZ quando vem
    // preenchido — por isso a gravação recusa o inválido (Documentos.CodigoDeBarrasEhValido).
    public string? CodigoBarras { get; set; }

    // Unidade comercial da nota: UN, LT, KG, CX... É o que o cliente compra, e por isso
    // combina com o estoque, que também conta em unidade de venda.
    public string? UnidadeComercial { get; set; }

    // Origem da mercadoria (0 a 8; 0 = nacional). Vai junto do CSOSN/CST no item da nota.
    public int? OrigemMercadoria { get; set; }

    // CSOSN (Simples) ou CST (Regime Normal) do ICMS — qual dos dois vale é decidido pelo
    // regime do CLUBE, não por este campo. Em branco até o contador preencher: chutar
    // tributação alheia é o tipo de "ajuda" que vira autuação no nome do cliente.
    public string? CsosnOuCst { get; set; }
}
