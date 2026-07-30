using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

[Table("MarcacaoJogo")]
public partial class MarcacaoJogo
{
    public int Id { get; set; }
    public int ClubeId { get; set; }
    public int QuadraClubeId { get; set; }
    public int JogadorId { get; set; }
    public DateTime DataHora { get; set; }
    public int DuracaoMinutos { get; set; }
    // "Confirmada" | "Cancelada" | "Faltou" (no-show) | "Realizada"
    public string Status { get; set; } = "Confirmada";
    public DateTime CriadoEm { get; set; } = DateTime.Now;

    // Agrupa as reservas geradas de um mesmo horário fixo (mensalista). Null = avulsa.
    public Guid? MensalidadeId { get; set; }

    // Bloqueio do clube (manutenção, evento, aula): ocupa a quadra sem ser reserva de
    // jogador. JogadorId aponta pro dono/admin que criou, pra não mexer no FK.
    public bool EhBloqueio { get; set; }
    public string? MotivoBloqueio { get; set; }

    // ---- Cancelamento / no-show ----
    public DateTime? CanceladaEm { get; set; }
    public string? CanceladaPor { get; set; }   // "Jogador" | "Clube"
    public bool CobrarMesmoAssim { get; set; }

    // ---- Balcão ----
    // Reserva que o CLUBE registrou pra quem ligou ou chamou no WhatsApp — a cena mais
    // comum da vida real. NomeClienteBalcao preenchido = reserva de balcão; JogadorId
    // aponta pra quem registrou (o mesmo truque do bloqueio) ou, se o celular bater com
    // uma conta, pro cliente de verdade — mas o nome digitado continua mandando na tela,
    // porque é o nome que o dono conhece.
    public string? NomeClienteBalcao { get; set; }
    public string? CelularClienteBalcao { get; set; }

    // ---- Pagamento da reserva ----
    // PagaNoLocal: o acerto é no balcão (reserva de balcão, ou do site quando o clube não
    // cobra online). PagoEm: quando o dinheiro entrou — o checkout online carimba na hora;
    // no balcão, quando o dono toca em "recebi". Reserva antiga (antes de 30/07/2026) fica
    // com os dois vazios e não ganha selo: não dá pra saber como foi acertada.
    public bool PagaNoLocal { get; set; }
    public DateTime? PagoEm { get; set; }

    [ForeignKey("ClubeId")]
    public virtual Clube Clube { get; set; } = null!;

    [ForeignKey("QuadraClubeId")]
    public virtual QuadraClube QuadraClube { get; set; } = null!;

    [ForeignKey("JogadorId")]
    public virtual Jogador Jogador { get; set; } = null!;
}
