using Padelizou.Models;

namespace Padelizou.Services;

// O LINK DA CÂMERA É DA QUADRA, NÃO DO JOGO (Felipe, 08/08/2026: "quando eu troco a quadra e
// tem transmissão salva no 'usar este link para todas' deveria trazer ao trocar de quadra,
// sempre que trocar de quadra").
//
// A câmera fica pendurada na quadra e transmite o dia inteiro — o link é uma propriedade do
// LUGAR. Só que ele é gravado em cada partida, e o único jeito de espalhá-lo era o "usar este
// link em todos os próximos jogos desta quadra", que é uma FOTOGRAFIA: aplica no que existe
// naquele instante. Todo jogo que nasce depois (a rodada seguinte do Americano, o desempate,
// a fase que o robô monta quando a anterior fecha) e todo jogo que MUDA de quadra chega sem
// link — e o organizador tem que abrir o Controle de Partida, achar o link e colar de novo,
// jogo a jogo, no meio do torneio.
//
// Aqui a pergunta é respondida ao contrário: "qual é o link DESTA quadra?" sai dos próprios
// jogos dela, e a tela oferece isso sozinha ao escolher a quadra.
//
// ⚠️ Ordenação TOTAL de propósito: com dois jogos da mesma quadra guardando links diferentes
// (a transmissão trocou de canal no meio do dia), "qual vale" não pode depender da ordem em
// que o banco devolveu as linhas. Vale o que está EM QUADRA agora; sem ninguém em quadra, o
// que está por vir; e só então o histórico — desempatando sempre pelo jogo mais novo.
public static class TransmissaoDaQuadra
{
    // Nome da quadra → link que a câmera dela está usando. Quadra sem nenhum link fica de
    // fora: ausência é resposta, e um valor vazio no mapa viraria "apague o que você digitou".
    public static Dictionary<string, string> PorQuadra(IEnumerable<Partida> doTorneio) =>
        doTorneio
            .Where(p => !string.IsNullOrWhiteSpace(p.NomeQuadra)
                     && !string.IsNullOrWhiteSpace(p.LinkTransmissao))
            .GroupBy(p => p.NomeQuadra!)
            .ToDictionary(
                porQuadra => porQuadra.Key,
                porQuadra => porQuadra
                    .OrderByDescending(Prioridade)
                    .ThenByDescending(p => p.Id)
                    .First().LinkTransmissao!);

    // O link de UMA quadra. Vazio quando ninguém transmitiu de lá ainda.
    public static string? De(string? quadra, IEnumerable<Partida> doTorneio) =>
        string.IsNullOrWhiteSpace(quadra)
            ? null
            : PorQuadra(doTorneio).GetValueOrDefault(quadra);

    private static int Prioridade(Partida p) => p.Status switch
    {
        "AoVivo" => 2,
        "Agendada" => 1,
        _ => 0,
    };
}
