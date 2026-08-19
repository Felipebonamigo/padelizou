namespace Padelizou.Services;

// QUANTO VALE UM PALPITE, num lugar só.
//
// A régua em uma frase: **cravou o placar 3, chegou perto 2, acertou só o vencedor 1, errou 0**
// — e o ranking ordena por PONTOS, com o aproveitamento só desempatando.
//
// ── Por que ordenar por pontos, e não por aproveitamento (decisão do Felipe) ───────────
// O caso que define a régua: **quem acerta 9 de 11 fica na frente de quem acerta 8 de 8**.
// Aproveitamento puro inverteria os dois, e junto exigiria um piso mínimo de palpites (sem
// ele, 1 de 1 = 100% lideraria pra sempre). Somando ponto, o piso não precisa existir: quem
// acertou 1 de 1 tem 1 ponto e cai pro fim da lista sozinho.
//
// 📌 Consequência aceita, de propósito: **quem palpita em mais jogos leva vantagem** — mesmo
// espírito da presença que pontua nos Desafios. ⚠️ **Se um dia alguém trocar isto por uma
// média, o piso mínimo tem que voltar junto.**
//
// ⚠️ A faixa do MEIO ("chegou perto") é o que faz o placar valer a pena palpitar. Sem ela,
// errar o placar por um game paga o mesmo que chutar 6x0 num jogo que terminou 6x5 — e aí
// ninguém se dá ao trabalho de pensar no placar, só no vencedor.
public static class PontosDoPalpite
{
    // Cravou: o placar palpitado é exatamente o que aconteceu.
    public const int Cravou = 3;

    // Chegou perto: acertou o vencedor e errou o placar por **um game em cada lado**.
    public const int ChegouPerto = 2;

    // Acertou o vencedor — e é também o que vale o palpite SEM placar, que é o palpite que
    // existia até aqui e continua valendo (ver `PalpiteConferido.PalpitouOPlacar`).
    public const int SoOVencedor = 1;

    // A distância que ainda conta como "chegou perto", **em cada lado do placar**.
    //
    // ⚠️ É por LADO e não pela SOMA das duas diferenças, e a razão é o formato de SOMA (o
    // torneio em que se jogam N games e acabou — ver ContagemDeGamesDoTorneio). Ali um game
    // que muda de lado mexe nos DOIS números ao mesmo tempo: quem palpita 5x3 num jogo que
    // termina 6x2 erra por 1 de cada lado, e uma régua pela soma diria "errou por 2" e mataria
    // a faixa do meio inteira nesse formato — caladinha, só nele.
    public const int GamesDeFolga = 1;

    // Quantos pontos vale UM palpite. Zero é a resposta pra tudo que não acertou o vencedor —
    // inclusive pro jogo que terminou sem vencedor nenhum, que não pontua ninguém.
    public static int De(PalpiteConferido palpite)
    {
        if (palpite.VencedorId is not int vencedor) return 0;
        if (palpite.DuplaEscolhidaId != vencedor) return 0;

        // Palpite antigo (ou de quem só quis dizer quem ganha): vale o acerto do vencedor.
        // ⚠️ É isto que faz o ranking nascer com o HISTÓRICO INTEIRO, em vez de começar zerado
        // no dia em que o placar entrar na tela.
        if (!palpite.PalpitouOPlacar || !palpite.TemPlacarReal) return SoOVencedor;

        // ⚠️ W.O. NÃO tem placar pra cravar. O jogo encerrado por não comparecimento é gravado
        // com o placar convencional da fase (ver Services/EncerramentoPorWo) — 6x0 que ninguém
        // jogou. Quem acertou o vencedor leva o ponto; premiar a "cravada" de um placar que a
        // quadra nunca viu seria sortear 3 pontos entre quem chutou o placar mais comum.
        if (palpite.PorWo) return SoOVencedor;

        int erroLado1 = Math.Abs(palpite.PalpitouLado1!.Value - palpite.PlacarLado1!.Value);
        int erroLado2 = Math.Abs(palpite.PalpitouLado2!.Value - palpite.PlacarLado2!.Value);

        if (erroLado1 == 0 && erroLado2 == 0) return Cravou;
        if (erroLado1 <= GamesDeFolga && erroLado2 <= GamesDeFolga) return ChegouPerto;

        return SoOVencedor;
    }

    // Cravou o placar? Serve à tela ("Fulano cravou o placar") sem repetir a conta lá.
    public static bool CravouOPlacar(PalpiteConferido palpite) => De(palpite) == Cravou;
}

// UM palpite pronto pra conferência: o que a pessoa disse × o que aconteceu.
//
// ⚠️ Os dois placares vêm na MESMA orientação — lado 1 é sempre a Dupla1 da partida, dos dois
// lados da comparação. Sem isso a conferência precisaria adivinhar de quem é cada número, e
// "6x4" viraria uma resposta diferente conforme quem lê.
//
// ⚠️ `PalpitouLado1/2` são NULÁVEIS de propósito e vão continuar sendo: até 08/2026 o palpite
// era só `DuplaEscolhidaId`, e todo palpite gravado antes do placar entrar na tela chega aqui
// com os dois nulos. Nulo quer dizer "não palpitou o placar" — nunca "palpitou 0x0", que é
// placar que não existe.
public sealed record PalpiteConferido
{
    public required int DuplaEscolhidaId { get; init; }

    // Nulo = jogo sem vencedor (não pontua ninguém).
    public required int? VencedorId { get; init; }

    public int? PalpitouLado1 { get; init; }
    public int? PalpitouLado2 { get; init; }

    public int? PlacarLado1 { get; init; }
    public int? PlacarLado2 { get; init; }

    // O jogo acabou por W.O.? (`Partida.MotivoDoEncerramento`) — ver a régua acima.
    public bool PorWo { get; init; }

    public bool PalpitouOPlacar => PalpitouLado1 != null && PalpitouLado2 != null;
    public bool TemPlacarReal => PlacarLado1 != null && PlacarLado2 != null;
}
