using Padelizou.Models;

namespace Padelizou.Services;

// O que o admin pode fazer com um placar contestado.
public enum DesfechoDaDisputa
{
    // O placar vale — o que estava lançado, ou o que o admin corrigiu. Fecha pelo caminho
    // normal: pontua, congela e mexe no cinturão.
    ValeOPlacar,

    // O jogo aconteceu mas o placar não pôde ser estabelecido. Ninguém pontua.
    Anular,
}

// ⚖️ RESOLVER UM PLACAR CONTESTADO — as regras, sem banco.
//
// POR QUE ESTA TELA EXISTE: quando as duas duplas discordam, o desafio para em `Em disputa` e
// NINGUÉM pontua. Isso é de propósito (o sistema não escolhe um lado sozinho), mas sem uma saída
// o jogo fica preso PARA SEMPRE — e o primeiro desacordo de placar viraria um chamado no
// WhatsApp do Felipe sem nenhum botão do outro lado.
//
// ⚠️ TRÊS COISAS QUE ESTA REGRA NÃO FAZ, e cada uma é uma escolha:
//
//  1. NÃO decide sozinha quem venceu. Quem decide é a pessoa que leu os dois lados. O sistema
//     só registra QUEM decidiu e POR QUÊ — que é a única resposta possível pra "por que meu
//     placar virou 6x4?".
//  2. NÃO apaga o que a dupla alegou. `MotivoDaDisputa` fica ao lado da resolução: sem os dois,
//     não dá pra reler a história depois.
//  3. NÃO inventa um estado novo pra "anulado que na verdade foi cancelado". Cancelado é jogo
//     que não aconteceu; anulado é jogo que aconteceu e cujo placar não se estabeleceu. O
//     histórico do jogador diz coisas diferentes nos dois casos.
public static class ResolucaoDaDisputa
{
    // Só quem está `Em disputa` pode ser resolvido. Um desafio já confirmado não volta pra mesa
    // por esta porta: reabrir jogo fechado é como se reescreve ranking sem ninguém ver.
    public static bool PodeResolver(Desafio desafio) => desafio.Status == Desafio.EmDisputa;

    // A justificativa é OBRIGATÓRIA. É ela que transforma "o admin mudou" em "o admin decidiu
    // isto, por este motivo" — e é o que sobra pra responder a reclamação três semanas depois.
    public static string? MotivoParaNaoResolver(Desafio desafio, DesfechoDaDisputa desfecho,
        string? justificativa, int? setsDesafiante, int? setsDesafiado,
        int? gamesDesafiante, int? gamesDesafiado)
    {
        if (!PodeResolver(desafio))
            return "Esse desafio não está em disputa.";

        if (string.IsNullOrWhiteSpace(justificativa))
            return "Escreva o que foi decidido e por quê — é o que responde a reclamação depois.";

        if (desfecho != DesfechoDaDisputa.ValeOPlacar) return null;

        // Validar um placar que não aponta ninguém deixaria o desafio confirmado e sem vencedor,
        // que é o estado que o ranking não sabe ler.
        return EstadoDoDesafio.MotivoParaNaoLancar(setsDesafiante, setsDesafiado,
            gamesDesafiante, gamesDesafiado);
    }

    // O texto que fica gravado na linha, com o desfecho na frente pra a leitura futura não
    // depender de cruzar com o Status.
    public static string TextoDaResolucao(DesfechoDaDisputa desfecho, string justificativa) =>
        desfecho == DesfechoDaDisputa.ValeOPlacar
            ? $"Placar validado: {justificativa.Trim()}"
            : $"Desafio anulado: {justificativa.Trim()}";
}
