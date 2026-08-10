namespace Padelizou.Services;

// QUANTO VALE UMA CAMPANHA DE TORNEIO, num lugar só.
//
// Espec completa em RANKING.md (Trilha B → "O peso por tamanho da categoria"). Mudou a
// régua? Muda LÁ primeiro, depois aqui.
//
// A régua em uma frase: todo mundo leva `pontos da fase × peso`, e o peso é 1,0 com 5
// duplas, +0,1 por dupla — mas o ponto só existe DEPOIS que o torneio começou.
//
// ── Por que o peso existe (10/08/2026) ────────────────────────────────────────────────
// A tabela antiga era fixa e CEGA AO TAMANHO: campeão de 4 duplas e campeão de 32 levavam
// os mesmos 100 pontos — um tendo ganho 2 jogos, o outro 5 ou 6 contra um funil muito
// maior. E tudo abaixo de Quartas caía no mesmo "participou 10", então quanto MAIOR o
// torneio, mais fases o ranking ignorava.
//
// ⚠️ É o tamanho da CATEGORIA, não do torneio: é contra o funil da SUA chave que se jogou.
// Um torneio de 60 duplas em 6 categorias não é um torneio de 60 duplas pra ninguém.
public static class PontosDoTorneio
{
    // O ponto de quem JOGOU e caiu na fase de grupos. Multiplica igual às outras fases.
    //
    // ⚠️ Decisão do Felipe (10/08/2026), revendo a primeira versão desta régua, em que os 10
    // eram fixos: "tem q dar mais pontos tambem para quem é eliminado na chave de um torneio
    // mais forte". É a lógica do circuito profissional — perder na estreia de um Slam paga
    // mais que vencer uma rodada de torneio pequeno, porque entrar naquele campo já é mais
    // difícil. O degrau pra quem sobrevive à chave continua: numa categoria de 20 duplas,
    // cair no grupo vale 25 e passar pros 16-avos vale 30.
    public const int PontosDeParticipacao = 10;

    // Abaixo disto a categoria não gera ponto de campanha — todo mundo leva os 10.
    //
    // ⚠️ Com 1 dupla o "campeão" não jogou NADA e com 2 ganhou um jogo só: é resultado
    // fabricável em cinco minutos, a mesma porta que o piso de 8 fecha no Americano.
    public const int DuplasParaValerCampanha = 3;

    // A escada das fases que valem campanha.
    //
    // ⚠️ Oitavas e a primeira rodada do quadro de 32 são NOVAS aqui (10/08/2026). Elas já
    // existiam no chaveamento e não existiam na pontuação: quem sobrevivia aos grupos de
    // uma categoria grande e caía nas oitavas pontuava igual a quem perdeu tudo no grupo.
    //
    // ⚠️ Quem NÃO está neste dicionário é participação — inclusive `null`, "Grupos" e o
    // nome de grupo ("Grupo A"). É a única definição de "vale campanha" do sistema; testar
    // por "a base é 10" seria frágil no dia em que uma fase valer 10.
    private static readonly Dictionary<string, int> Escada = new()
    {
        ["Campeao"] = 100,
        ["Final"] = 60,                                 // perdeu a final = vice
        ["Semifinal"] = 35,
        ["Quartas de Final"] = 20,
        ["Oitavas de Final"] = 15,
        [ChaveamentoMataMata.PrimeiraRodada] = 12,      // "Primeira Rodada" = 16-avos na tela
    };

    // Essa fase é campanha (multiplica) ou é participação (10 fixo)?
    public static bool ValeCampanha(string? fase) =>
        fase != null && Escada.ContainsKey(fase);

    // Os pontos da fase ANTES do peso. Serve pra tela explicar a conta.
    public static int PontosBase(string? fase) =>
        fase != null && Escada.TryGetValue(fase, out var pontos) ? pontos : PontosDeParticipacao;

    // 1,0 com 5 duplas, +0,1 por dupla.
    //
    // ⚠️ SEM TETO (decisão do Felipe, 10/08/2026): 25 duplas = 3,0, 26 = 3,1, 40 = 4,5. Um
    // teto criaria uma zona plana onde 25 e 40 duplas valem igual — exatamente a injustiça
    // que este cálculo existe pra consertar.
    //
    // ⚠️ Linear e sem degraus, a pedido do Felipe ("muitas vezes não são múltiplos de 4"):
    // degrau cria fronteira ("com 11 vale menos que com 12"), e fronteira em régua de ponto
    // vira briga e vira manipulação de inscrição.
    //
    // `decimal` e não `double`: 0,1 não existe em binário, e uma soma de ponto que às vezes
    // arredonda pro lado errado é a discussão que ninguém consegue encerrar.
    public static decimal Peso(int duplasNaCategoria) =>
        0.5m + (duplasNaCategoria / 10m);

    // O TORNEIO JÁ COMEÇOU? Enquanto não começou, ninguém tem ponto nenhum dele.
    //
    // ⚠️ Pedido do Felipe (10/08/2026): "só dê esses pontos quando o torneio começar e a
    // pessoa tiver inscrita". Era um buraco de verdade — `Dupla.UltimaFase` NASCE valendo
    // "Grupos", então a inscrição sozinha já valia ponto: quem se inscrevesse num torneio
    // marcado pra dezembro subia no ranking hoje, sem ter tocado numa bola. Com a
    // participação passando a multiplicar pelo tamanho, isso viraria a maneira mais barata
    // de subir — bastava entrar na categoria mais cheia que existisse.
    //
    // ⚠️ Lista de NEGAÇÃO, e não uma lista de status "bons": "começou" é o normal e é o que
    // vale pro resto da vida do torneio (Fase de Grupos, Finalizado). Escrito ao contrário,
    // um status novo qualquer nasceria SEM ponto e o defeito seria mudo — pontos que somem.
    // Assim, um status novo nasce pontuando, que é o comportamento óbvio; e os três que não
    // pontuam estão cada um com o seu motivo escrito.
    public static bool TorneioJaComecou(string? status) =>
        // Inscrição aberta: ninguém jogou ainda, o sorteio nem aconteceu.
        status != PortaDaInscricao.Aberta
        // "Chaves em Sorteio" = inscrição ENCERRADA, chave ainda não sorteada. O nome engana
        // (parece que está sorteando); é o torneio parado esperando o organizador.
        && status != PortaDaInscricao.Fechada
        // ⚠️ Cancelado não paga nem participação, mesmo tendo sorteado antes de cancelar: o
        // evento não aconteceu. É o único caminho em que o total de alguém DIMINUI sozinho.
        && !CancelamentoDoTorneio.EstaCancelado(status);

    // A conta inteira: fase + tamanho da categoria + status do torneio → pontos de ranking.
    //
    // ⚠️ O status é PARÂMETRO, e não filtro de quem chama, de propósito. São oito lugares
    // somando ponto; um deles esquecer o filtro não daria erro nenhum, daria ranking errado.
    // Aqui é impossível esquecer — a assinatura obriga.
    public static int Pontos(string? fase, int duplasNaCategoria, string? statusDoTorneio)
    {
        if (!TorneioJaComecou(statusDoTorneio)) return 0;

        // Abaixo do piso a campanha desaba pra participação: todo mundo da categoria sai com
        // a mesma pontuação, em vez de o "campeão" de 2 duplas levar 100.
        int baseDaFase = ValeCampanha(fase) && duplasNaCategoria >= DuplasParaValerCampanha
            ? PontosBase(fase)
            : PontosDeParticipacao;

        // ⚠️ AwayFromZero, nunca o ToEven padrão do .NET: com ToEven, 12,5 vira 12 e 15,5
        // vira 16, e dois jogadores com a MESMA conta receberiam pontos diferentes conforme
        // a paridade. É a mesma armadilha já documentada no PontosDoAmericano.
        return (int)Math.Round(baseDaFase * Peso(duplasNaCategoria), MidpointRounding.AwayFromZero);
    }
}
