using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Uma conta do clube: a pagar (fornecedor de bebida, aluguel, luz, professor) ou a receber
// (mensalista que ficou devendo, patrocínio, aluguel de espaço pra evento).
//
// É MANUAL de propósito, e isso é a decisão mais importante deste arquivo.
//
// O sistema já sabe o que entrou de comanda fechada e de reserva paga — esse dinheiro já
// aconteceu. Transformá-lo em "a receber" contaria a mesma receita duas vezes: uma no caixa
// do dia e outra na lista de contas. O que o clube precisa registrar aqui é justamente o que
// o Padelizou NÃO tem como saber: a conta de luz, o boleto da distribuidora, o cachê do DJ
// do torneio, o cliente que levou fiado.
//
// Na tela de resumo os dois aparecem lado a lado — o que o sistema viu e o que o dono
// digitou — mas nunca somados no mesmo balde.
[Table("LancamentoFinanceiro")]
public class LancamentoFinanceiro
{
    public const string Pagar = "Pagar";
    public const string Receber = "Receber";

    public int Id { get; set; }

    public int ClubeId { get; set; }
    public virtual Clube Clube { get; set; } = null!;

    // "Pagar" ou "Receber". Um campo só, e não duas tabelas: as duas listas têm exatamente
    // as mesmas colunas e as mesmas ações, e separá-las duplicaria tudo pra sempre.
    public string Tipo { get; set; } = Pagar;

    public string Descricao { get; set; } = string.Empty;

    // Sempre positivo. O sinal quem dá é o Tipo — valor negativo num lançamento "a pagar"
    // significaria "a receber", e aí a mesma informação teria dois jeitos de ser escrita.
    public decimal Valor { get; set; }

    public DateTime Vencimento { get; set; }

    // Quando quitou. Null = em aberto. É este campo, e não um "Status" de texto, que manda:
    // status escrito à mão sai do ar com a data e vira mentira ("Pago" sem data de quando).
    public DateTime? QuitadoEm { get; set; }
    public int? QuitadoPorId { get; set; }

    // Com quem é a conta ("Ambev", "CEEE", "Prof. Jonatas"). Texto livre: cadastro de
    // fornecedor é uma tela inteira que ninguém pediu, e o nome digitado resolve a pergunta
    // "quanto eu pago de cerveja por mês?" com um filtro.
    public string? Contraparte { get; set; }

    // Pra agrupar no resumo ("Bebidas", "Estrutura", "Pessoal"). Mesma escolha do cardápio:
    // texto livre, porque lista fixa vira pedido de "adiciona a categoria X" na 1ª semana.
    public string? Categoria { get; set; }

    public string? Observacao { get; set; }

    // Todas as parcelas de uma recorrência ("aluguel, 12 meses") nascem juntas com o mesmo
    // grupo. É o que permite apagar a série inteira depois sem caçar linha por linha — e é
    // Guid, não int, pra não precisar de tabela nova só pra numerar grupo.
    public Guid? GrupoRecorrencia { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.Now;
    public int? CriadoPorId { get; set; }

    [NotMapped]
    public bool EstaQuitado => QuitadoEm != null;

    [NotMapped]
    public bool EhPagar => Tipo == Pagar;
}
