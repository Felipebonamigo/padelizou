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

    // `pontosTieBreak1/2` e `formato` (12/09/2026): a contagem do 8x8 também atravessa a fila
    // offline — o mesário marca ponto no celular sem sinal como marca game. Nulos = fila
    // gravada antes deste deploy, ou Mesa de torneio sem tie-break: o que está no banco fica.
    public static Resultado Aplicar(Partida partida, int games1, int games2, int sets1, int sets2,
        DateTime marcadoEm, int? pontosTieBreak1 = null, int? pontosTieBreak2 = null,
        FormatoDaPartida.Formato? formato = null)
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

        // ⚠️ LADO NEGATIVO É "NÃO TOQUEI NESTE" (12/09/2026), e fica com o que está gravado.
        // 🗣️ Felipe: *"quando um de um lado marcava e o outro junto as vezes, um deles nao
        // pegava"*. A fila mandava o placar INTEIRO em todo toque, então o aparelho do vizinho
        // reescrevia o lado que ninguém tinha tocado com o número da tela DELE — de minutos
        // atrás, se ele estava sem sinal. É a mesma correção da lista AO VIVO.
        //
        // ⚠️ E o placar ABSOLUTO continua: o que muda é quais lados ele afirma, não o "+1" —
        // incremento reentregue dobraria o game, que é o motivo de esta fila existir assim.
        partida.GamesDupla1 = games1 < 0 ? partida.GamesDupla1 : Math.Clamp(games1, 0, LimiteDeGames);
        partida.GamesDupla2 = games2 < 0 ? partida.GamesDupla2 : Math.Clamp(games2, 0, LimiteDeGames);
        partida.SetsDupla1 = sets1 < 0 ? partida.SetsDupla1 : Math.Max(0, sets1);
        partida.SetsDupla2 = sets2 < 0 ? partida.SetsDupla2 : Math.Max(0, sets2);
        partida.PlacarMarcadoEm = marcadoEm;
        partida.SendoTransmitida = true;

        // ⚠️ Os pontos só entram onde o tie-break PODE acontecer (TieBreakDoJogo.PodeAcontecer):
        // alvo configurado, contagem "até" e fase de número ímpar. Sem o formato na mão, não se
        // grava — é a mesma recusa que o POST em lote e a tela cheia fazem, e ela vale aqui
        // também porque a fila pode reentregar um corpo montado à mão.
        if ((pontosTieBreak1 >= 0 || pontosTieBreak2 >= 0)
            && formato != null && TieBreakDoJogo.PodeAcontecer(formato))
        {
            // Mesmo "não toquei" dos games: no 8x8 cada mesário conta o ponto do seu lado.
            if (pontosTieBreak1 >= 0) partida.PontosTieBreak1 = TieBreakDoJogo.PontoValido(pontosTieBreak1.Value);
            if (pontosTieBreak2 >= 0) partida.PontosTieBreak2 = TieBreakDoJogo.PontoValido(pontosTieBreak2.Value);
        }

        return Resultado.Ok;
    }
}
