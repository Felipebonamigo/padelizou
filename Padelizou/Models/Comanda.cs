using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// A comanda do bar: uma conta aberta no nome de alguém, que recebe itens ao longo do dia e
// é fechada quando a pessoa paga.
//
// O NOME é a identidade real. No balcão ninguém procura por número: procura por "a comanda
// do Rafael". O número existe porque é útil gritar "comanda 7" — mas quem manda na busca é
// o nome.
//
// A vantagem que nenhum PDV genérico tem: o cliente do dia quase sempre é o jogador de uma
// reserva que o sistema JÁ conhece. Um toque e a comanda nasce com nome, celular e a quadra
// junto — sem digitar nada.
[Table("Comanda")]
public class Comanda
{
    public const string Aberta = "Aberta";
    public const string Fechada = "Fechada";
    public const string Cancelada = "Cancelada";

    public int Id { get; set; }

    public int ClubeId { get; set; }
    public virtual Clube Clube { get; set; } = null!;

    // Número curto do dia ("comanda 7"), reiniciado a cada dia em cada clube. Único por
    // (clube, dia) no banco: dois tablets no balcão abrindo comanda no mesmo segundo
    // gerariam o mesmo número, e duas "comanda 7" no mesmo turno é confusão na hora de
    // cobrar. Quem gera trata a colisão tentando o próximo número (ver ComandasDoClube).
    public int Numero { get; set; }

    // A data a que a comanda pertence, sem hora. É ela que fecha o dia e reinicia a
    // numeração — e não pode ser derivada de AbertaEm: comanda aberta 23h50 e fechada 00h30
    // pertence ao movimento de ONTEM, que é como o bar conta o caixa.
    public DateTime DiaReferencia { get; set; }

    // O nome escrito no balcão. Sempre preenchido, mesmo quando veio de um jogador
    // cadastrado — assim a comanda continua legível se a conta do jogador for excluída
    // depois (LGPD), e a busca no balcão é sempre pelo mesmo campo.
    public string NomeCliente { get; set; } = string.Empty;

    // Quando o cliente é gente do sistema. Opcional: muito cliente de bar nunca vai ter
    // conta no Padelizou, e exigir cadastro pra vender uma água mataria o balcão.
    public int? JogadorId { get; set; }
    public virtual Jogador? Jogador { get; set; }

    // A reserva que originou a comanda, quando ela nasceu de uma. Guardada pra responder
    // "quanto essa quadra rendeu de bar hoje?" — pergunta que só existe porque quadra e bar
    // vivem no mesmo sistema.
    public int? MarcacaoJogoId { get; set; }
    public virtual MarcacaoJogo? MarcacaoJogo { get; set; }

    public string? Celular { get; set; }

    // "CPF na nota?" — a pergunta do balcão. Opcional de propósito: a NFC-e sai sem
    // identificar o consumidor, e travar a venda esperando CPF pararia a fila por um campo
    // que a lei não exige. Só dígitos, validado com dígito verificador na gravação: CPF
    // inventado na nota é rejeição da SEFAZ, e aí a venda para de verdade.
    public string? CpfConsumidor { get; set; }

    public string Status { get; set; } = Aberta;

    public DateTime AbertaEm { get; set; } = DateTime.Now;
    public int? AbertaPorId { get; set; }

    public DateTime? FechadaEm { get; set; }
    public int? FechadaPorId { get; set; }

    // Como o cliente pagou. É REGISTRO, não cobrança: dinheiro e maquininha acontecem fora
    // do sistema (mesma lógica do torneio "por fora"). Só o Pix pelo app passa por aqui.
    public string? FormaPagamento { get; set; }

    // Desconto em dinheiro dado no fechamento ("cortesia", "arredondou"). Fica separado do
    // total pra continuar dando pra ver quanto foi vendido E quanto foi abatido — desconto
    // embutido no preço do item some do relatório e vira buraco no caixa.
    public decimal Desconto { get; set; }

    public string? Observacao { get; set; }

    // Comanda cancelada não some: fica com o motivo. Num sistema de bar, item que desaparece
    // é a forma mais comum de furto — o histórico é o que permite auditar.
    public string? MotivoCancelamento { get; set; }

    public virtual ICollection<ItemComanda> Itens { get; set; } = new List<ItemComanda>();

    // Soma dos itens que continuam valendo. Item cancelado não entra, e o preço vem do
    // ITEM (congelado no lançamento), nunca do produto de hoje.
    [NotMapped]
    public decimal Subtotal => Itens
        .Where(i => i.CanceladoEm == null)
        .Sum(i => i.PrecoUnitario * i.Quantidade);

    [NotMapped]
    public decimal Total => Math.Max(0m, Subtotal - Desconto);

    [NotMapped]
    public bool EstaAberta => Status == Aberta;
}
