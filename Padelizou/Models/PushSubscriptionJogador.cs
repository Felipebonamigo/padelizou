using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Uma "assinatura" de push por dispositivo/navegador em que o jogador clicou em "Ativar
// notificações". Um mesmo jogador pode ter várias (celular + notebook, por ex).
[Table("PushSubscriptionJogador")]
public class PushSubscriptionJogador
{
    public int Id { get; set; }

    public int JogadorId { get; set; }
    public virtual Jogador Jogador { get; set; } = null!;

    public string Endpoint { get; set; } = null!;
    public string P256dh { get; set; } = null!;
    public string Auth { get; set; } = null!;

    public DateTime CriadoEm { get; set; } = DateTime.Now;
}
