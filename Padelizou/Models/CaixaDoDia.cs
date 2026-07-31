using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// O caixa de um dia de bar: abriu com X em dinheiro, vendeu Y, fechou contando Z.
//
// A pergunta que este registro responde é a única que o dono do bar faz todo dia: **bate?**
// O sistema sabe quanto foi vendido em dinheiro; só uma pessoa contando a gaveta sabe quanto
// tem lá. A diferença entre os dois é o dado que importa — e é por isso que o valor contado
// é digitado à mão em vez de calculado.
//
// Uma linha por clube por dia. O dia é o DiaReferencia das comandas, não o relógio: comanda
// aberta 23h50 e paga 00h30 entra no movimento de ontem, que é como o bar conta.
[Table("CaixaDoDia")]
public class CaixaDoDia
{
    public int Id { get; set; }

    public int ClubeId { get; set; }
    public virtual Clube Clube { get; set; } = null!;

    public DateTime Dia { get; set; }

    // Troco que estava na gaveta ao abrir. Entra na conta do fechamento: quem esquece disso
    // fecha o caixa achando que sobrou dinheiro.
    public decimal ValorAbertura { get; set; }

    public DateTime AbertoEm { get; set; } = DateTime.Now;
    public int? AbertoPorId { get; set; }

    // O que a pessoa CONTOU na gaveta ao fechar. Digitado, nunca calculado — se o sistema
    // preenchesse sozinho, a conferência não conferiria nada.
    public decimal? ValorContado { get; set; }

    public DateTime? FechadoEm { get; set; }
    public int? FechadoPorId { get; set; }

    public string? Observacao { get; set; }

    [NotMapped]
    public bool EstaAberto => FechadoEm == null;
}
