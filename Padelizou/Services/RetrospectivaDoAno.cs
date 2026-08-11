namespace Padelizou.Services;

// "SEU ANO NO PADEL" — os números de um jogador num ano, do jeito que ele conta pros amigos.
//
// Por que existe: o sistema guarda tudo que cada pessoa jogou e nunca devolve isso como uma
// HISTÓRIA. O perfil mostra números de sempre, o ranking mostra a posição de hoje — e nenhum
// dos dois dá aquilo que a pessoa quer postar: "meu ano". É a mecânica da retrospectiva do
// Spotify, e ela funciona porque quem divulga é o próprio usuário, sobre ele mesmo, pra amigos
// que ainda não estão aqui.
//
// ⚠️ AQUI NÃO É RANKING, e a diferença é de propósito. O ranking oficial só conta torneio (nem
// Americano, nem restrito) porque ele decide quem é melhor. A retrospectiva conta TAMBÉM o jogo
// de quinta com a panelinha, porque ela responde outra pergunta: quanto padel você jogou. Somar
// só torneio deixaria a maioria das pessoas com um card vazio — que é exatamente o estado da
// produção hoje.
//
// ⚠️ Os PONTOS, esses sim, continuam saindo da régua oficial (`PontosDoTorneio`, via
// EstatisticasService): um número chamado "pontos" que discorde do ranking seria a segunda
// versão da mesma verdade, que é o defeito mais caro deste projeto.
//
// Este arquivo é REGRA PURA — nada aqui toca o banco. A coleta mora em
// EstatisticasService.ObterRetrospectivaAsync, mesma divisão de CatalogoConquistas.
public static class RetrospectivaDoAno
{
    // Abaixo disto não vira card: um jogo solto não é um ano, e a arte sairia dizendo
    // "1 jogo, 0 vitórias" pra pessoa postar. Quem não alcança vê a página com o convite pra
    // jogar, que é o que serve pra ela.
    public const int JogosMinimosParaCard = 3;

    // O primeiro ano em que houve Padelizou. Antes disso não existe retrospectiva pra pedir —
    // e sem um piso a URL aceita ?ano=1500 e devolve um card vazio com cara de defeito.
    public const int PrimeiroAno = 2025;

    // Os anos que dá pra pedir. Nunca o futuro: "seu ano de 2028" é uma página que só sabe
    // estar vazia.
    public static bool AnoValido(int ano, DateTime agora) =>
        ano >= PrimeiroAno && ano <= agora.Year;

    public static int AnoPadrao(DateTime agora) => agora.Year;

    // Monta a retrospectiva a partir dos jogos já carregados.
    public static RetrospectivaVM Montar(DadosDaRetrospectiva d)
    {
        var jogos = d.Jogos;

        int vitorias = jogos.Count(j => j.Venceu == true);
        // ⚠️ Jogo semanal pode EMPATAR em games (VencedorLado == 0), e torneio não. Contar
        // empate como derrota daria um aproveitamento que não bate com "vitórias + derrotas".
        int decididos = jogos.Count(j => j.Venceu != null);

        var parceiro = jogos
            .Where(j => j.ParceiroId != null)
            .GroupBy(j => j.ParceiroId!.Value)
            .Select(g => new CompanheiroDoAno(
                g.First().ParceiroNome ?? "",
                g.Count(),
                g.Count(x => x.Venceu == true)))
            .OrderByDescending(p => p.Jogos)
            .ThenByDescending(p => p.Vitorias)
            // Desempate pelo nome pra a mesma pessoa não trocar de lugar entre dois
            // carregamentos da mesma página — a lição da ordenação total do ranking.
            .ThenBy(p => p.Nome, StringComparer.Ordinal)
            .FirstOrDefault();

        var clube = jogos
            .Where(j => !string.IsNullOrWhiteSpace(j.Clube))
            .GroupBy(j => j.Clube!)
            .Select(g => new CasaDoAno(g.Key, g.Count()))
            .OrderByDescending(c => c.Jogos)
            .ThenBy(c => c.Nome, StringComparer.Ordinal)
            .FirstOrDefault();

        var mes = jogos
            .GroupBy(j => j.Quando.Month)
            .Select(g => new { Mes = g.Key, Jogos = g.Count() })
            .OrderByDescending(m => m.Jogos)
            .ThenBy(m => m.Mes)
            .FirstOrDefault();

        return new RetrospectivaVM
        {
            Ano = d.Ano,
            Jogador = d.Jogador,
            Jogos = jogos.Count,
            Vitorias = vitorias,
            JogosDecididos = decididos,
            JogosDeTorneio = jogos.Count(j => j.DeTorneio),
            Torneios = d.Torneios,
            Titulos = d.Titulos,
            Finais = d.Finais,
            Pontos = d.Pontos,
            MelhorPosicao = d.MelhorPosicao,
            CategoriaDaPosicao = d.CategoriaDaPosicao,
            Parceiro = parceiro,
            Clube = clube,
            MesMaisAtivo = mes?.Mes,
            JogosNoMesMaisAtivo = mes?.Jogos ?? 0,
        };
    }

    // O nome do mês por extenso, em português, sem depender da cultura do servidor.
    //
    // ⚠️ O VPS roda em UTC e o app é configurado pra Brasília por drop-in do systemd — mas
    // CULTURA é outra configuração, e ela não vem junto. `ToString("MMMM")` num servidor em
    // en-US devolve "August" no meio de um card em português, e ninguém testa isso porque aqui
    // a máquina é pt-BR.
    public static string NomeDoMes(int mes) => mes switch
    {
        1 => "janeiro", 2 => "fevereiro", 3 => "março", 4 => "abril",
        5 => "maio", 6 => "junho", 7 => "julho", 8 => "agosto",
        9 => "setembro", 10 => "outubro", 11 => "novembro", 12 => "dezembro",
        _ => "",
    };
}

// Um jogo do ano, reduzido ao que a retrospectiva precisa. `Venceu` é nulo no empate em games
// do jogo semanal — torneio não empata.
public sealed record JogoDoAno(
    DateTime Quando,
    bool? Venceu,
    int? ParceiroId,
    string? ParceiroNome,
    string? Clube,
    bool DeTorneio);

// O que a coleta entrega pra regra. Espelha `DadosParaConquistas`.
public sealed record DadosDaRetrospectiva(
    int Ano,
    Models.Jogador Jogador,
    IReadOnlyList<JogoDoAno> Jogos,
    int Torneios,
    int Titulos,
    int Finais,
    int Pontos,
    int? MelhorPosicao,
    string? CategoriaDaPosicao);

public sealed record CompanheiroDoAno(string Nome, int Jogos, int Vitorias);

public sealed record CasaDoAno(string Nome, int Jogos);

public sealed class RetrospectivaVM
{
    public int Ano { get; set; }
    public Models.Jogador Jogador { get; set; } = null!;

    public int Jogos { get; set; }
    public int Vitorias { get; set; }
    public int JogosDecididos { get; set; }
    public int JogosDeTorneio { get; set; }

    public int Torneios { get; set; }
    public int Titulos { get; set; }
    public int Finais { get; set; }
    public int Pontos { get; set; }

    public int? MelhorPosicao { get; set; }
    public string? CategoriaDaPosicao { get; set; }

    public CompanheiroDoAno? Parceiro { get; set; }
    public CasaDoAno? Clube { get; set; }
    public int? MesMaisAtivo { get; set; }
    public int JogosNoMesMaisAtivo { get; set; }

    // Aproveitamento sobre os jogos DECIDIDOS. Zero jogos decididos devolve 0 em vez de
    // estourar — e a tela não mostra o número quando não houve jogo.
    public int AproveitamentoPct =>
        JogosDecididos == 0 ? 0 : (int)Math.Round(100.0 * Vitorias / JogosDecididos, MidpointRounding.AwayFromZero);

    public bool TemCard => Jogos >= RetrospectivaDoAno.JogosMinimosParaCard;
}
