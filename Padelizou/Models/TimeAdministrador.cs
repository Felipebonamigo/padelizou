namespace Padelizou.Models;

// Quem pode administrar um time — mais de uma pessoa por time, de propósito.
//
// Substitui o antigo Time.DonoId, que era um só. A regra virou "vários administradores,
// todos com o mesmo poder", e manter as duas noções deixaria duas fontes de verdade pra
// mesma pergunta ("quem manda neste time?") — foi assim que o PontuacaoGlobal virou bug.
public class TimeAdministrador
{
    public int TimeId { get; set; }
    public Time Time { get; set; } = null!;

    public int JogadorId { get; set; }
    public Jogador Jogador { get; set; } = null!;

    // Quem concedeu: um admin do sistema, ou outro administrador do próprio time.
    // Fica guardado porque quando alguém reclamar de acesso, a primeira pergunta é
    // "quem te colocou aí?" — e sem isso não há resposta.
    public int? ConcedidoPorId { get; set; }
    public DateTime ConcedidoEm { get; set; }
}
