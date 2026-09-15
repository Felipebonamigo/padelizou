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
// ⚠️ AQUI HAVIA UM `Bolinha`, a ROUPA do botão: `true` desenhava o círculo de 30px da aba Jogos,
// `false` a pílula escrita "Chegou"/"Desfazer" da tela de Check-in do dia. Essa tela saiu em
// 13/09/2026 e a pílula ficou sem chamador — o campo saiu junto com ela em 15/09, quando o
// clique virou `fetch`: uma roupa que ninguém desenha obrigaria o JavaScript a saber pintar as
// duas, e a metade que ninguém vê é a que quebra calada.
public record BotaoDeCheckInVM(
    int JogadorId,
    int PartidaId,
    string Nome,
    DateTime? ChegouEm,
    string? VoltarPara);
