namespace Padelizou.ViewModels;

// O que o `_BotaoDoCheckIn` precisa saber pra desenhar UM jogador (12/09/2026).
//
// Virou registro em vez de tupla quando o quinto campo apareceu: `(Jogador, int, DateTime?, bool,
// string?)` na chamada do parcial não diz qual bool é qual, e a linha do jogo desenha quatro
// deles seguidos.
//
// ⚠️ `PartidaId`, e não `TorneioId`, desde 12/09/2026: a presença é do JOGO (Models/PresencaNoJogo).
// 🗣️ Felipe: *"o checkin ... nao deveria [herdar], tem q ser separado jogo a jogo"*.
//
// `Bolinha` é a ROUPA, não a regra: `true` desenha o círculo de 30px da aba Jogos, `false` a
// pílula escrita "Chegou"/"Desfazer" da tela de Check-in. O POST é o mesmo nos dois.
public record BotaoDeCheckInVM(
    int JogadorId,
    int PartidaId,
    string Nome,
    DateTime? ChegouEm,
    bool Bolinha,
    string? VoltarPara);
