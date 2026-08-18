using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Quem pediu pra ACOMPANHAR um jogo AO VIVO (16/08/2026, pedido do Felipe: "algo parecido com
// o placar que o Google mostra na tela de bloqueio"). Não é SeguirTorneio (isso é o torneio
// inteiro, semanas) nem é o aviso de resultado de quem jogou — é quem está de fora e quer o
// placar chegando no aparelho sem precisar manter o site aberto.
//
// Nasce quando a pessoa toca "Seguir este jogo" no card AO VIVO e morre quando o jogo termina
// (Services/AvisoDePlacarAoVivo apaga a linha ao mandar o placar final) — depois disso não há
// mais "ao vivo" pra acompanhar.
[Table("SeguidorDePartida")]
public class SeguidorDePartida
{
    public int Id { get; set; }

    public int JogadorId { get; set; }
    public virtual Jogador Jogador { get; set; } = null!;

    public int PartidaId { get; set; }
    public virtual Partida Partida { get; set; } = null!;

    public DateTime CriadoEm { get; set; } = DateTime.Now;
}
