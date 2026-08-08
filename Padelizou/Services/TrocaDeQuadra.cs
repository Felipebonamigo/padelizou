using Padelizou.Models;

namespace Padelizou.Services;

// Mudar a QUADRA de um jogo sem mexer na hora dele.
//
// A irmã de TrocaDeHorario, e pelo mesmo motivo: a grade acerta a conta, mas não conhece a
// vida. A quadra 3 molhou, a 1 é a única coberta, a final merece a quadra do meio onde cabe
// o público. Até agora o organizador só conseguia isso trocando o SLOT INTEIRO com outro
// jogo — o que arrastava o horário junto, e quase nunca era o que ele queria.
//
// ⚠️ Duas quadras iguais no mesmo horário é a única coisa que a grade não pode produzir: são
// duas duplas mandadas pro mesmo lugar. Por isso, quando a quadra pedida já tem dono NAQUELE
// horário, isto não recusa — TROCA as duas de lugar. Recusar deixaria o organizador no
// mesmo beco ("então como eu ponho esse jogo na quadra 1?"); trocar resolve o pedido e
// mantém a grade íntegra, exatamente como a troca de horário faz.
public static class TrocaDeQuadra
{
    // Null = pode mudar. Texto = o motivo, na língua de quem organiza.
    public static string? MotivoParaNaoMudar(Partida? jogo, string? quadra, int torneioId,
        IReadOnlyCollection<string> quadrasDoTorneio)
    {
        if (jogo == null) return "Não encontrei o jogo.";
        if (jogo.TorneioId != torneioId) return "O jogo precisa ser deste torneio.";

        // Jogo já jogado tem quadra de HISTÓRIA: foi ali que a bola rolou. O que está em
        // quadra AGORA pode mudar de lugar — quadra que molha ou luz que apaga acontece com
        // jogo em andamento, e é justamente aí que remanejar importa.
        if (jogo.Status == "Finalizada")
            return $"O jogo {jogo.Codigo} já terminou — a quadra dele é história, não agenda.";

        if (string.IsNullOrWhiteSpace(quadra)) return "Escolha uma quadra.";

        if (quadrasDoTorneio.Count > 0 && !quadrasDoTorneio.Contains(quadra))
            return $"\"{quadra}\" não é uma quadra deste torneio.";

        if (quadra == jogo.NomeQuadra) return $"O jogo {jogo.Codigo} já está na {quadra}.";

        return null;
    }

    // Quem está ocupando a quadra pedida no MESMO horário — se houver, é com ele que a troca
    // acontece. Jogo sem horário não disputa quadra com ninguém: ele entra quando a Mesa
    // chamar.
    public static Partida? QuemOcupa(Partida jogo, string quadra, IEnumerable<Partida> doTorneio) =>
        jogo.HorarioPrevisto == null
            ? null
            : doTorneio.FirstOrDefault(p => p.Id != jogo.Id
                                         && p.HorarioPrevisto == jogo.HorarioPrevisto
                                         && p.NomeQuadra == quadra);

    // A mudança em si. Com ocupante, as duas quadras trocam de dono; sem ocupante, o jogo
    // simplesmente muda de quadra e a antiga fica livre.
    //
    // ⚠️ A CÂMERA NÃO VAI JUNTO COM O JOGO — ela fica pendurada na quadra (Felipe,
    // 08/08/2026). Sem `cameras`, o jogo que saía da Quadra 1 pra Quadra 2 levava o link da
    // Quadra 1 no bolso: quem abrisse a transmissão desse jogo veria OUTRA partida, ao vivo,
    // achando que era esta. Errado calado é pior que vazio, então quadra de destino sem
    // câmera conhecida fica sem link. Mapa em Services/TransmissaoDaQuadra.
    public static void Mudar(Partida jogo, string quadra, Partida? ocupante,
        IReadOnlyDictionary<string, string>? cameras = null)
    {
        var daOrigem = jogo.NomeQuadra;

        if (ocupante != null) ocupante.NomeQuadra = daOrigem;
        jogo.NomeQuadra = quadra;

        if (cameras == null) return;

        ApontarCamera(jogo, cameras);
        if (ocupante != null) ApontarCamera(ocupante, cameras);
    }

    private static void ApontarCamera(Partida jogo, IReadOnlyDictionary<string, string> cameras)
    {
        var link = jogo.NomeQuadra != null && cameras.TryGetValue(jogo.NomeQuadra, out var dela)
            ? dela
            : null;

        jogo.LinkTransmissao = link;
        jogo.SendoTransmitida = jogo.Status == "AoVivo" && !string.IsNullOrEmpty(link);
    }
}
