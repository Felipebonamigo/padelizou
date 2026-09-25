using System.Globalization;
using Padel.Core;

// Calibração da janela do balanço: o humano simulado (com parceiro IA) contra a dupla de IA, com sementes fixas.
// Uso: dotnet run -c Release --project ferramentas/Calibracao [-- --partidas N --games G]
// Imprime tabelas em markdown: por perfil x dificuldade, e por desvio de timing (resto do Intermediário) x Médio.

int partidas = 6, gamesParaVencer = 4;
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--partidas") partidas = int.Parse(args[i + 1], CultureInfo.InvariantCulture);
    if (args[i] == "--games") gamesParaVencer = int.Parse(args[i + 1], CultureInfo.InvariantCulture);
}
var cultura = CultureInfo.GetCultureInfo("pt-BR");

Console.WriteLine($"# Calibração do balanço — {partidas} partidas por célula, até {gamesParaVencer} games\n");
Console.WriteLine("## Perfil do humano x dificuldade da IA\n");
Console.WriteLine("| Humano | IA | % pontos | % partidas | golpes do humano/ponto | balanços no ar/ponto | erro médio do golpe |");
Console.WriteLine("|---|---|---|---|---|---|---|");
foreach (var perfil in PerfilDeHumano.Todos)
foreach (var d in new[] { Dificuldade.Facil, Dificuldade.Medio, Dificuldade.Dificil })
    Linha(perfil.Nome, d.ToString(), Medir(perfil, d));

Console.WriteLine("\n## Desvio de timing (resto do Intermediário) x IA Médio\n");
Console.WriteLine("| Desvio (ms) | % pontos | % partidas | balanços no ar/ponto | erro médio do golpe |");
Console.WriteLine("|---|---|---|---|---|");
foreach (int ms in new[] { 10, 20, 30, 45, 60, 90 })
{
    var r = Medir(PerfilDeHumano.Intermediario with { Nome = $"{ms} ms", DesvioDoTempo = ms / 1000f }, Dificuldade.Medio);
    Console.WriteLine($"| {ms} | {r.PercPontos.ToString("F1", cultura)} | {r.PercPartidas.ToString("F0", cultura)} | {r.NoArPorPonto.ToString("F2", cultura)} | {r.ErroMedio.ToString("F2", cultura)} |");
}

void Linha(string humano, string ia, Resultado r) =>
    Console.WriteLine($"| {humano} | {ia} | {r.PercPontos.ToString("F1", cultura)} | {r.PercPartidas.ToString("F0", cultura)} | {r.GolpesPorPonto.ToString("F2", cultura)} | {r.NoArPorPonto.ToString("F2", cultura)} | {r.ErroMedio.ToString("F2", cultura)} |");

Resultado Medir(PerfilDeHumano perfil, Dificuldade d)
{
    int pontosCasa = 0, pontosTotal = 0, vitorias = 0, golpes = 0, noAr = 0, golpesComErro = 0;
    float somaErro = 0;
    for (int n = 0; n < partidas; n++)
    {
        uint semente = (uint)(1000 + n);
        var partida = new Partida(new OpcoesDaPartida { Semente = semente, Dificuldade = d, Humanos = [true, false, false, false] });
        var humano = new HumanoSimulado(0, perfil, new Aleatorio(semente * 7919u + 17u));
        partida.Evento += ev =>
        {
            if (ev.Tipo is TipoDeEventoDaPartida.Ponto or TipoDeEventoDaPartida.Game or TipoDeEventoDaPartida.Set or TipoDeEventoDaPartida.Partida)
            {
                pontosTotal++;
                if (ev.Time == 0) pontosCasa++;
            }
            if (ev.Tipo == TipoDeEventoDaPartida.Golpe && ev.Jogador == partida.Jogadores[0] && ev.Golpe != TipoDeGolpe.Saque)
            {
                golpes++;
                golpesComErro++;
                somaErro += partida.UltimoErroDoHumano;
            }
        };
        var entradas = new Entrada[4];
        float t = 0;
        const float passo = 1f / 120f;
        while (!partida.Acabou && t < 3600)
        {
            if (partida.Placar.Games[0] >= gamesParaVencer || partida.Placar.Games[1] >= gamesParaVencer) break;
            entradas[0] = humano.Decidir(EstadoVisivel.De(partida), passo);
            partida.Avancar(passo, entradas);
            t += passo;
        }
        if (partida.Placar.Vencedor == 0 || partida.Placar.Games[0] >= gamesParaVencer || partida.Placar.Sets[0] > partida.Placar.Sets[1]) vitorias++;
        noAr += partida.Jogadores[0].BalancosNoAr;
    }
    return new Resultado(
        100f * pontosCasa / Math.Max(1, pontosTotal),
        100f * vitorias / partidas,
        (float)golpes / Math.Max(1, pontosTotal),
        (float)noAr / Math.Max(1, pontosTotal),
        golpesComErro > 0 ? somaErro / golpesComErro : float.NaN);
}

readonly record struct Resultado(float PercPontos, float PercPartidas, float GolpesPorPonto, float NoArPorPonto, float ErroMedio);
