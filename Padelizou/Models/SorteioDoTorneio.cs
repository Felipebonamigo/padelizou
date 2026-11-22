using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// O SORTEIO DE BRINDES do torneio (21/11/2026). 🗣️ Felipe: *"alguns torneios sorteiam brindes e
// outras coisas para quem joga"*. A regra mora em Services/SorteioDeBrindes.
//
// 🧾 GRAVADO, e não só na tela: o resultado aparece na página do torneio e no telão, e é isso
// que impede o "foi armado". Refazer não apaga — o sorteio antigo fica com `SubstituidoPorId`
// e aparece riscado ("refeito às 21:40").
[Table("SorteioDoTorneio")]
public class SorteioDoTorneio
{
    public int Id { get; set; }
    public int TorneioId { get; set; }

    // O que se sorteia ("Kit Head", "2 overgrips"). Opcional: há sorteio só de nomes.
    [MaxLength(120)]
    public string? Premio { get; set; }

    public int Quantidade { get; set; }
    public DateTime FeitoEm { get; set; }

    // Quem apertou o botão. Sem navegação de propósito: um segundo caminho de cascade a partir
    // de Jogador é o conflito já visto nas tabelas de vínculo — aqui basta o registro.
    public int FeitoPorJogadorId { get; set; }

    // Preenchido = este sorteio foi REFEITO pelo de Id indicado. Sem FK, pelo mesmo motivo.
    public int? SubstituidoPorId { get; set; }
    public DateTime? SubstituidoEm { get; set; }

    public virtual Torneio Torneio { get; set; } = null!;
    public virtual ICollection<GanhadorDoSorteio> Ganhadores { get; set; } = new List<GanhadorDoSorteio>();
}

// Um nome sorteado. A PK (sorteio, jogador) é o que garante que o mesmo nome não sai duas vezes
// no mesmo sorteio — sem uma linha de C# pra isso.
[Table("GanhadorDoSorteio")]
public class GanhadorDoSorteio
{
    public int SorteioId { get; set; }
    public int JogadorId { get; set; }

    // A ordem em que saiu (1 = o primeiro nome), a mesma da animação.
    public int Ordem { get; set; }

    public virtual SorteioDoTorneio Sorteio { get; set; } = null!;
    public virtual Jogador Jogador { get; set; } = null!;
}
