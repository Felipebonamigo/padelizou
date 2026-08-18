using Padelizou.Models;

namespace Padelizou.Services;

// W.O. — O JOGO QUE ACABOU SEM SER JOGADO.
//
// Alguém não apareceu. Até aqui o organizador não tinha o que fazer a não ser INVENTAR um
// placar: `QuemVenceu.MotivoParaNaoFinalizar` exige games pra deixar finalizar, então o
// caminho era digitar 6x0 e seguir. Funcionava pro chaveamento e mentia pro resto.
//
// ⚠️ O QUE ESSA MENTIRA CUSTAVA: o Padelímetro mede COMO a pessoa joga, e um 6x0 inventado
// entra nele como se os quatro tivessem entrado em quadra. Quem venceu sem jogar subia de
// nível; quem faltou descia. A única defesa era indireta — `Padelimetro.FatorDeGames` tem
// teto em 6 de diferença "pra que um 9x0 de W.O. disfarçado não exploda a conta" —, e teto
// limita o estrago sem impedir o erro.
//
// ⚠️ E TINHA UMA REGRA QUE SÓ EXISTIA NO COMENTÁRIO: `EncerramentoDaPartida` dizia que o
// serviço filtrava "restrito/time/W.O.". Os dois primeiros ele filtrava mesmo; o W.O. não —
// e não tinha como, porque nada no banco distinguia um W.O. de um 6x0 de verdade. É o
// defeito clássico deste projeto ao contrário: em vez de a regra estar escrita duas vezes,
// ela estava escrita ZERO vez e documentada como se existisse.
public static class EncerramentoPorWo
{
    public const string Motivo = "WO";

    public const string Rotulo = "W.O.";

    public static bool Foi(Partida partida) => partida.MotivoDoEncerramento == Motivo;

    // ⚠️ O W.O. GRAVA PLACAR, e isso é de propósito.
    //
    // A tentação é deixar o placar nulo — "ninguém jogou, não há games". Mas quem responde
    // "essa dupla venceu?" no resto do sistema é `QuemVenceu`, que LÊ O PLACAR, e a tabela
    // do grupo (`ClassificacaoDeGrupos`) conta vitória pela mesma função. Placar nulo vira
    // 0x0, que é empate: o W.O. não daria vitória a ninguém, a dupla que compareceu perderia
    // a vaga no mata-mata e nada acusaria o erro.
    //
    // Então o W.O. é registrado como o placar convencional da FASE (o mesmo que
    // FormatoDaPartida diz pra ela) — que é como torneio de verdade anota um W.O. O que
    // separa este 6x0 de um 6x0 jogado não é o placar: é `MotivoDoEncerramento`.
    //
    // Sets junto com games porque `QuemVenceu` olha SETS PRIMEIRO: um set velho de um placar
    // anterior sobreviveria e apontaria o vencedor errado.
    public static void Registrar(Partida partida, int duplaQueNaoCompareceuId,
        FormatoDaPartida.Formato formato)
    {
        bool faltouADupla1 = duplaQueNaoCompareceuId == partida.Dupla1Id;

        partida.GamesDupla1 = faltouADupla1 ? 0 : formato.Games;
        partida.GamesDupla2 = faltouADupla1 ? formato.Games : 0;
        partida.SetsDupla1 = faltouADupla1 ? 0 : formato.Sets;
        partida.SetsDupla2 = faltouADupla1 ? formato.Sets : 0;

        partida.VencedorId = faltouADupla1 ? partida.Dupla2Id : partida.Dupla1Id;
        partida.Status = "Finalizada";
        partida.MotivoDoEncerramento = Motivo;
    }

    // Deixa de ser W.O. — o jogo volta a ser um jogo comum.
    //
    // ⚠️ Isto NÃO é higiene: é o que impede o W.O. de virar permanente. Se o organizador
    // registra o W.O. e depois descobre que a dupla chegou atrasada e o jogo aconteceu, ele
    // corrige o placar pela tela de sempre. Sem limpar a marca aqui, aquele jogo — agora um
    // jogo de verdade, com placar de verdade — ficaria fora do Padelímetro para sempre, e
    // nenhuma tela mostraria o porquê.
    public static void Limpar(Partida partida) => partida.MotivoDoEncerramento = null;

    // Por que este W.O. não pode ser registrado — null quando pode.
    public static string? MotivoParaNaoRegistrar(Partida partida, int duplaQueNaoCompareceuId)
    {
        if (duplaQueNaoCompareceuId != partida.Dupla1Id && duplaQueNaoCompareceuId != partida.Dupla2Id)
            return "Essa dupla não é deste jogo.";

        // Dupla incompleta dos dois lados é jogo que nunca teve como acontecer — o W.O.
        // registraria um vencedor que ainda não existe.
        if (partida.Dupla1Id == partida.Dupla2Id)
            return "Este jogo ainda não tem as duas duplas definidas.";

        return null;
    }

    // O texto que a tela mostra no lugar do placar. Recebe o nome de quem faltou porque
    // "W.O." sozinho não conta a história pra quem lê a lista depois.
    public static string Explicacao(string nomeDeQuemFaltou) =>
        $"{nomeDeQuemFaltou} não compareceu";
}
