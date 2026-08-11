namespace Padelizou.Models;

// Um repasse já pago a um parceiro comercial.
//
// ⚠️ ISTO NÃO MOVIMENTA DINHEIRO. O Pix quem faz é o Felipe, no banco dele; esta linha é o
// REGISTRO de que ele fez. Sem ela, a única resposta pra "quanto eu já te paguei?" era o
// extrato bancário — e a pergunta vem do parceiro, todo mês.
//
// O acerto é um SALDO, não uma marcação por pagamento: a comissão pinga o mês inteiro, cliente
// a cliente, e o repasse cobre "tudo que fechou até agora". Marcar pagamento por pagamento
// obrigaria a escolher quais entram em cada Pix e criaria uma segunda contabilidade pra manter
// de pé.
public class RepasseAoParceiro
{
    public int Id { get; set; }

    public int ParceiroId { get; set; }
    public Jogador Parceiro { get; set; } = null!;

    public decimal Valor { get; set; }

    // Quando o dinheiro saiu de verdade — pode não ser hoje. O Felipe pode estar lançando na
    // segunda um Pix que fez no sábado, e a data que importa pro parceiro é a do Pix.
    public DateTime PagoEm { get; set; }

    public DateTime RegistradoEm { get; set; } = DateTime.Now;

    // Quem lançou. A pergunta que vem depois de um valor estranho é "quem registrou isso?".
    public int? RegistradoPorId { get; set; }

    // "Pix 11/08", "abatido do que ele devia", "acerto de dois meses juntos". É o que explica
    // uma linha fora do padrão seis meses depois.
    public string? Observacao { get; set; }
}
