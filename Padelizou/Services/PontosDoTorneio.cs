namespace Padelizou.Services;

// QUANTO VALE UMA CAMPANHA DE TORNEIO, num lugar só.
//
// Espec completa em RANKING.md (Trilha B → "O peso por tamanho da categoria"). Mudou a
// régua? Muda LÁ primeiro, depois aqui.
//
// A régua em duas frases: quem cai na fase de grupos leva 10, sempre. Quem sobrevive à
// chave leva `pontos da fase × peso`, e o peso é 1,0 com 5 duplas, +0,1 por dupla.
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
    // O ponto DA INSCRIÇÃO: é o que se ganha por estar lá, e ele NÃO multiplica.
    //
    // ⚠️ Decisão do Felipe (10/08/2026). Multiplicar a participação premiaria aparecer num
    // torneio grande e perder tudo; do jeito que ficou, o peso só começa a valer quando a
    // pessoa SOBREVIVE À CHAVE. O degrau é nítido de propósito: numa categoria de 20
    // duplas, cair no grupo vale 10 e passar pros 16-avos vale 30.
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

    // A conta inteira: fase + tamanho da categoria → pontos de ranking.
    public static int Pontos(string? fase, int duplasNaCategoria)
    {
        if (!ValeCampanha(fase)) return PontosDeParticipacao;
        if (duplasNaCategoria < DuplasParaValerCampanha) return PontosDeParticipacao;

        // ⚠️ AwayFromZero, nunca o ToEven padrão do .NET: com ToEven, 12,5 vira 12 e 15,5
        // vira 16, e dois jogadores com a MESMA conta receberiam pontos diferentes conforme
        // a paridade. É a mesma armadilha já documentada no PontosDoAmericano.
        return (int)Math.Round(PontosBase(fase) * Peso(duplasNaCategoria), MidpointRounding.AwayFromZero);
    }
}
