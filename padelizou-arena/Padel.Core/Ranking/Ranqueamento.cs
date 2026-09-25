namespace Padel.Core.Ranking;

/// <summary>Por que o número de um jogador andou (ou não) numa partida. Enum e não texto: a tela
/// traduz (PT/EN/ES no lançamento), e o extrato do Padelizou em português não serve lá fora.</summary>
public enum MotivoDoMovimento
{
    Vitoria,
    Derrota,
    /// <summary>Este jogador abandonou: derrota cobrada como 6x0.</summary>
    Abandonou,
    /// <summary>O parceiro abandonou: o time perdeu por abandono, e este paga a mesma derrota de
    /// 6x0 (senão a dupla escolheria quem absorve a derrota — ver <c>Ranqueamento</c>).</summary>
    ParceiroAbandonou,
    /// <summary>Um adversário abandonou: este não anda.</summary>
    AdversarioAbandonou,
    /// <summary>Havia IA num dos lugares desde o começo: a partida não é ranqueada.</summary>
    PartidaComIA,
    /// <summary>Sem vencedor e sem ninguém que abandonou: não há o que medir.</summary>
    SemResultado,
    /// <summary>A mesma conta em dois lugares: dado torto, não conta.</summary>
    JogadorRepetido,
}

/// <summary>O que uma partida fez com o nível de um jogador. <see cref="Antes"/> e
/// <see cref="Depois"/> iguais quando a partida não contou pra ele.</summary>
public sealed record MovimentoNoRanking(NivelNoRanking Antes, NivelNoRanking Depois, MotivoDoMovimento Motivo)
{
    /// <summary>O que o número de fato andou, já preso às pontas da régua —
    /// <c>PadelimetroService.Mover</c> grava o delta pós-clamp: "o extrato nunca mente".</summary>
    public int Delta => Depois.Pdz - Antes.Pdz;
}

/// <summary>
/// Aplica uma partida ranqueada à régua: devolve, pra cada um dos 4 lugares, o movimento do
/// jogador (nulo nos lugares da IA). Puro: não guarda nada — quem tem a autoridade (o host no M2,
/// o servidor dedicado no 1.0, DECISOES.md D2) chama UMA vez por partida e salva os
/// <see cref="MovimentoNoRanking.Depois"/>.
/// </summary>
/// <remarks>
/// <para><b>O que move o número</b> — RANKING.md "O que move o número"; <c>PadelimetroService.Aplicar</c>:
/// partida terminada com 4 humanos distintos. Nível da dupla = média; expectativa entre as duas
/// médias; cada jogador anda <c>K_dele × fator_de_games × (resultado − expectativa)</c> com o PRÓPRIO
/// K; os quatro deltas saem dos níveis de ANTES da partida (lá a expectativa é calculada antes do
/// primeiro <c>Mover</c>; aqui o estado é imutável e isso vem de graça).</para>
///
/// <para><b>O que não move</b> — RANKING.md "O que NÃO move o número"; <c>PadelimetroService.Conta</c>:</para>
/// <list type="bullet">
/// <item><b>Partida sem placar</b> (lá: games nulos) → aqui: sem vencedor e sem abandono
/// (<see cref="MotivoDoMovimento.SemResultado"/>).</item>
/// <item><b>Mesmo jogador em dois lugares</b> → <c>PadelimetroService.IdsDosJogadores</c> exige 4 pessoas
/// distintas (<see cref="MotivoDoMovimento.JogadorRepetido"/>).</item>
/// <item><b>W.O.</b> ("jogo que não aconteceu não mede nada") → no jogo, partida que não começou nem
/// chega aqui: o lobby cancela, e quem some antes do saque é problema da fila, não da régua.</item>
/// <item><b>Partida com IA</b> — o Padelizou não tem; DECISÃO DO JOGO: IA em qualquer lugar desde o
/// começo (treino, amistoso, coop contra a IA, 3 humanos + 1 IA) não mexe em ninguém, nem na
/// contagem de jogos. A IA não tem número pra entrar na expectativa, e vitória sobre ela é
/// fabricável à vontade — a mesma razão do Torneio Restrito e do Americano ficarem fora lá ("evento
/// fechado não mede padel contra o mundo"; "fabricavam ranking sem enfrentar ninguém de fora").
/// Vale mesmo se alguém abandonar: o que nunca foi ranqueado não cobra abandono.</item>
/// </list>
///
/// <para><b>Abandono</b> — o Padelizou não tem (o mais perto é o W.O., que não move ninguém; copiar isso
/// faria do rage-quit o jeito grátis de não perder). DECISÃO DO JOGO: <b>o time de quem abandona perde
/// por 6x0, e o outro time não anda</b>.</para>
/// <list type="bullet">
/// <item><b>Quem abandona perde como se fosse 6x0</b>: derrota com o fator MÁXIMO (1,6), contra a
/// expectativa do time dele, com o K dele, contando como jogo (entra na calibração e na histerese
/// como qualquer derrota). O placar da hora não alivia.</item>
/// <item><b>O parceiro de quem abandonou paga a mesma derrota de 6x0</b> (com o K dele). A primeira
/// versão deixava o parceiro parado ("ele não escolheu sair"), e a revisão achou o abandono POR
/// PROCURAÇÃO: a dupla escolhia quem absorve a derrota — quando ia perder, a conta descartável
/// abandonava e a principal não andava (com 50% de aproveitamento, 700 → 815 em 20 partidas), e com
/// o descartável no chão da régua a derrota sumia de graça. Punir só quem sai não fecha isso, porque
/// quem sai é justamente o que não importa. O custo aceito — o parceiro inocente de um estranho que
/// cai paga o 6x0 — não dá a ninguém um poder novo: um parceiro que quer te derrubar já consegue o
/// mesmo 6x0 parado na quadra; e a reconexão de 30 s do M2 (CRONOGRAMA.md) absorve a queda honesta.</item>
/// <item><b>Os adversários não andam</b>: a régua só paga o que foi medido. Pagar vitória a quem
/// estava do outro lado abriria a fraude clássica de ranqueada — um amigo entra no time adversário e
/// sai no primeiro game. O custo aceito: quem ia ganhando de verdade não leva a vitória.</item>
/// <item><b>Um vencedor que venha junto com o abandono é ignorado por todos</b>: é o que mantém as
/// duas regras acima de pé — nem o time de quem saiu ganha porque a IA terminou a partida por ele,
/// nem o outro time ganha com a saída.</item>
/// </list>
/// Com isso abandonar nunca sai mais barato que perder jogando, pra NENHUM dos dois do time de quem
/// saiu — o pior placar possível custa o mesmo, qualquer outro custa menos. Se os dois times tiverem
/// alguém que abandonou, os dois perdem. Como no ajuste de campanha do site, a régua deixa de ser
/// soma-zero aqui: o abandono tira ponto sem dar a ninguém. Punição de fila (tempo de espera pra quem
/// abandona muito) é do matchmaking.
/// </remarks>
public static class Ranqueamento
{
    public const int Lugares = 4;

    public static IReadOnlyList<MovimentoNoRanking?> Aplicar(PartidaRanqueada partida)
    {
        ArgumentNullException.ThrowIfNull(partida);
        var lugares = partida.Lugares;
        if (lugares is null || lugares.Count != Lugares)
            throw new ArgumentException("A partida ranqueada tem 4 lugares, na ordem time * 2 + índice.", nameof(partida));
        if (partida.TimeVencedor is not (null or 0 or 1))
            throw new ArgumentOutOfRangeException(nameof(partida), partida.TimeVencedor, "O vencedor é o time 0 ou o time 1.");
        if (partida.GamesDoTime0 < 0 || partida.GamesDoTime1 < 0)
            throw new ArgumentOutOfRangeException(nameof(partida), "Games não podem ser negativos.");
        if (partida.SetsJogados < 1)
            throw new ArgumentOutOfRangeException(nameof(partida), partida.SetsJogados, "Uma partida tem pelo menos um set.");

        var niveis = new NivelNoRanking[Lugares];
        for (int i = 0; i < Lugares; i++)
        {
            if (lugares[i].Nivel is not { } nivel) return NinguemAnda(lugares, MotivoDoMovimento.PartidaComIA);
            niveis[i] = nivel;
        }

        if (lugares.Select(l => l.Id).Distinct(StringComparer.Ordinal).Count() != Lugares)
            return NinguemAnda(lugares, MotivoDoMovimento.JogadorRepetido);

        double expectativaDoTime0 = Padelimetro.Expectativa(
            Padelimetro.NivelDaDupla(niveis[0].Pdz, niveis[1].Pdz),
            Padelimetro.NivelDaDupla(niveis[2].Pdz, niveis[3].Pdz));
        double ExpectativaDoTime(int time) => time == 0 ? expectativaDoTime0 : 1.0 - expectativaDoTime0;

        var movimentos = new MovimentoNoRanking?[Lugares];

        if (lugares.Any(l => l.Abandonou))
        {
            for (int i = 0; i < Lugares; i++)
            {
                int time = i / 2;
                bool timeAbandonou = lugares[i].Abandonou || lugares[i ^ 1].Abandonou;
                movimentos[i] = timeAbandonou
                    ? Mover(niveis[i], venceu: false, ExpectativaDoTime(time), Padelimetro.FatorMaximo,
                        lugares[i].Abandonou ? MotivoDoMovimento.Abandonou : MotivoDoMovimento.ParceiroAbandonou)
                    : Parado(niveis[i], MotivoDoMovimento.AdversarioAbandonou);
            }
            return movimentos;
        }

        if (partida.TimeVencedor is not { } vencedor)
            return NinguemAnda(lugares, MotivoDoMovimento.SemResultado);

        // O fator olha a margem do VENCEDOR, não o módulo: em melhor de 3 quem vence pode ter menos
        // games (Padelimetro.FatorDaPartida). Com set único é o FatorDeGames do site.
        double fator = vencedor == 0
            ? Padelimetro.FatorDaPartida(partida.GamesDoTime0, partida.GamesDoTime1, partida.SetsJogados)
            : Padelimetro.FatorDaPartida(partida.GamesDoTime1, partida.GamesDoTime0, partida.SetsJogados);
        for (int i = 0; i < Lugares; i++)
        {
            int time = i / 2;
            bool venceu = time == vencedor;
            movimentos[i] = Mover(niveis[i], venceu, ExpectativaDoTime(time), fator,
                venceu ? MotivoDoMovimento.Vitoria : MotivoDoMovimento.Derrota);
        }
        return movimentos;
    }

    // PadelimetroService.Mover: o K é o de ANTES do jogo (Padelimetro.K(JogosDePadelimetro)).
    private static MovimentoNoRanking Mover(NivelNoRanking antes, bool venceu, double expectativaDoTime,
        double fator, MotivoDoMovimento motivo)
    {
        int delta = Padelimetro.Variacao(Padelimetro.K(antes.Jogos), fator, venceu, expectativaDoTime);
        return new MovimentoNoRanking(antes, antes.DepoisDoJogo(delta), motivo);
    }

    private static MovimentoNoRanking Parado(NivelNoRanking nivel, MotivoDoMovimento motivo) =>
        new(nivel, nivel, motivo);

    private static MovimentoNoRanking?[] NinguemAnda(IReadOnlyList<LugarRanqueado> lugares, MotivoDoMovimento motivo) =>
        [.. lugares.Select(l => l.Nivel is { } nivel ? Parado(nivel, motivo) : null)];
}
