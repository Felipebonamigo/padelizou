using Padelizou.Models;

namespace Padelizou.Services;

// A Mesa de Controle funciona SEM internet: cada toque atualiza o placar no aparelho na hora,
// e o que vai (ou fica guardado pra ir depois) é o PLACAR INTEIRO, não o "+1".
//
// Essa troca é o coração do offline. Reenviar "+1" depois que a rede volta é uma bomba: o
// mesmo toque entregue duas vezes dobra o game, e fila de rede repete entrega o tempo todo.
// Reenviar "games 5x3, sets 1x0" quantas vezes for dá sempre no mesmo lugar — e de uma fila
// de vinte toques presos, só o ÚLTIMO estado de cada partida precisa viajar.
//
// Aqui mora a regra de aceitação do servidor. O tempo vem do relógio do APARELHO de quem
// marcou (não do servidor): entre dois placares, vence o marcado por último na quadra —
// mesmo que tenha chegado primeiro o mais velho, atrasado por estar sem sinal.
public static class PlacarDaMesa
{
    public sealed record Resultado(bool Aplicado, string Motivo)
    {
        public static readonly Resultado Ok = new(true, "aplicado");
        public static Resultado Recusado(string motivo) => new(false, motivo);
    }

    // Mesma trava que sempre existiu na Mesa (contagem de games não passa de 9).
    public const int LimiteDeGames = 9;

    public static Resultado Aplicar(Partida partida, int games1, int games2, int sets1, int sets2,
        DateTime marcadoEm)
    {
        // Partida encerrada não aceita placar da fila: finalizar dispara mata-mata, carimba
        // fases e avisa gente — um placar velho preso num celular não pode reabrir nada disso.
        // Corrigir jogo encerrado continua existindo, mas é outra tela e outra intenção.
        if (partida.Status == "Finalizada")
            return Resultado.Recusado("a partida já foi finalizada");

        // O que chegou é mais velho do que o que já está gravado: ignora. É isso que impede
        // uma fila atrasada (ou um segundo aparelho esquecido aberto) de atropelar o placar
        // que o organizador está marcando AGORA.
        if (partida.PlacarMarcadoEm != null && marcadoEm <= partida.PlacarMarcadoEm.Value)
            return Resultado.Recusado("já existe um placar mais novo");

        partida.GamesDupla1 = Math.Clamp(games1, 0, LimiteDeGames);
        partida.GamesDupla2 = Math.Clamp(games2, 0, LimiteDeGames);
        partida.SetsDupla1 = Math.Max(0, sets1);
        partida.SetsDupla2 = Math.Max(0, sets2);
        partida.PlacarMarcadoEm = marcadoEm;
        partida.SendoTransmitida = true;

        return Resultado.Ok;
    }
}
