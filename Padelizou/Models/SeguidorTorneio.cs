using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Quem pediu pra ser avisado a cada inscrição NESTE torneio.
//
// É irmão do SeguidorJogador e resolve outra pergunta: lá se segue uma PESSOA ("me avise
// quando o Lucas entrar num torneio"), aqui se segue um TORNEIO ("me avise quem mais está
// entrando neste"). Quem já se inscreveu quer o segundo — é ele que fica olhando a chave
// encher pra saber contra quem vai jogar.
//
// ⚠️ Seguir é a PRÓPRIA preferência: não existe flag global de "quero avisos de torneio
// seguido". Quem não quer mais, deixa de seguir — o botão é o mesmo, e é o único lugar.
// Uma segunda chave de liga/desliga faria a pessoa seguir e não receber nada, sem pista.
[Table("SeguidorTorneio")]
public class SeguidorTorneio
{
    public int TorneioId { get; set; }
    public int JogadorId { get; set; }
    public DateTime CriadoEm { get; set; } = DateTime.Now;

    [ForeignKey("TorneioId")]
    public virtual Torneio Torneio { get; set; } = null!;

    [ForeignKey("JogadorId")]
    public virtual Jogador Jogador { get; set; } = null!;
}
