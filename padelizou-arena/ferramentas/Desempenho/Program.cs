using System.Diagnostics;
using System.Globalization;
using Padel.Core;

// Desempenho: o tempo de cada Partida.Avancar em 12 partidas IA x IA com semente fixa (Difícil, Médio e Fácil; 4 sementes
// cada) e, pelo modo de coleta dos GolpesEspeciais, o de cada chamada real a um solucionador, cronometrada isolada.
// Uso: dotnet run -c Release --project ferramentas/Desempenho [-- --execucoes 3 --sementes 1,2,3,4]
// Tempo de parede numa máquina compartilhada é ruidoso: cada medida sai de N execuções, e a tabela traz a mediana delas.

int execucoes = 3;
uint[] sementes = [1, 2, 3, 4];
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--execucoes") execucoes = int.Parse(args[i + 1], CultureInfo.InvariantCulture);
    if (args[i] == "--sementes") sementes = args[i + 1].Split(',').Select(t => uint.Parse(t, CultureInfo.InvariantCulture)).ToArray();
}
var cultura = CultureInfo.GetCultureInfo("pt-BR");
var dificuldades = new[] { Dificuldade.Dificil, Dificuldade.Medio, Dificuldade.Facil };
const float passo = 1f / 120f;
double msPorTique = 1000.0 / Stopwatch.Frequency;

// Aquecimento: o JIT compila e promove os métodos quentes (compilação em camadas) antes de medir — uma partida por
// dificuldade, com semente fora das medidas. Sem isso, a primeira execução sai com dezenas de passos lentos a mais.
foreach (var d in dificuldades) Jogar(d, 77, null);

// ───────────── Partidas: o tempo de cada Avancar ─────────────

Console.WriteLine($"# Desempenho — {dificuldades.Length * sementes.Length} partidas IA x IA (sementes {string.Join(", ", sementes)}), {execucoes} execuções\n");
Console.WriteLine("## Passos da partida (Partida.Avancar, 1/120 s)\n");
Console.WriteLine("| Execução | passos | mediana (ms) | p99 (ms) | p99,9 (ms) | > 2 ms | > 4 ms | > 8,3 ms | máximo (ms) |");
Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
var linhas = new List<Linha>();
var quemNosLentos = new Dictionary<string, int>();
for (int e = 1; e <= execucoes; e++)
{
    var tempos = new List<double>(800_000);
    foreach (var d in dificuldades)
    foreach (uint s in sementes)
    {
        GC.Collect();
        Jogar(d, s, (ms, quem) =>
        {
            tempos.Add(ms);
            if (ms > 2) quemNosLentos[quem] = quemNosLentos.GetValueOrDefault(quem) + 1;
        });
    }
    var linha = Resumir(tempos);
    linhas.Add(linha);
    Console.WriteLine($"| {e} | {linha.Passos} | {F(linha.Mediana, 3)} | {F(linha.P99, 3)} | {F(linha.P999, 3)} | {linha.Mais2} | {linha.Mais4} | {linha.Mais83} | {F(linha.Maximo, 2)} |");
}
double Med(Func<Linha, double> campo) => linhas.Select(campo).OrderBy(v => v).ElementAt(linhas.Count / 2);
Console.WriteLine($"| **mediana** | {linhas[0].Passos} | {F(Med(l => l.Mediana), 3)} | {F(Med(l => l.P99), 3)} | {F(Med(l => l.P999), 3)} | {Med(l => l.Mais2)} | {Med(l => l.Mais4)} | {Med(l => l.Mais83)} | {F(Med(l => l.Maximo), 2)} |");

Console.WriteLine($"\n### Quem rodava nos passos > 2 ms (soma das {execucoes} execuções)\n");
Console.WriteLine("Pelos solucionadores chamados no passo (modo de coleta; \"→ nulo\" = sem solução); sem nenhum, pelo golpe que saiu. " +
    "\"sem golpe\": IA prevendo a bola, GC ou a própria máquina.\n");
Console.WriteLine("| No passo | passos > 2 ms |");
Console.WriteLine("|---|---|");
foreach (var (quem, n) in quemNosLentos.OrderByDescending(p => p.Value)) Console.WriteLine($"| {quem} | {n} |");

// ───────────── Solucionadores: cada chamada real, isolada ─────────────

var chamadas = new List<ChamadaDeSolucionador>();
foreach (var d in dificuldades)
foreach (uint s in sementes)
{
    GolpesEspeciais.Coleta = chamadas;
    Jogar(d, s, null);
    GolpesEspeciais.Coleta = null;
}
Console.WriteLine($"\n## Chamadas aos solucionadores ({chamadas.Count} chamadas reais das partidas, cada uma cronometrada isolada {execucoes} vezes; vale a mediana)\n");
Console.WriteLine("| Solucionador | chamadas | com solução | mediana (ms) | p99 (ms) | máximo (ms) | > 1 ms | sub-passos: mediana | sub-passos: máximo |");
Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
int divergentes = 0;
var porChamada = chamadas.Select(c =>
{
    var tempos = new double[execucoes];
    long subPassos = 0;
    for (int e = 0; e < execucoes; e++)
    {
        long trabalhoAntes = GolpesEspeciais.SubPassosSimulados;
        long antes = Stopwatch.GetTimestamp();
        var resultado = Chamar(c);
        tempos[e] = (Stopwatch.GetTimestamp() - antes) * msPorTique;
        subPassos = GolpesEspeciais.SubPassosSimulados - trabalhoAntes;
        if (resultado != c.Resultado) divergentes++;   // a física é determinística: a mesma entrada dá o mesmo golpe
    }
    Array.Sort(tempos);
    return (Chamada: c, Ms: tempos[execucoes / 2], SubPassos: subPassos);
}).ToList();
foreach (var grupo in porChamada.GroupBy(p => p.Chamada.Qual).OrderBy(g => g.Key))
{
    var ms = grupo.Select(p => p.Ms).OrderBy(v => v).ToArray();
    var sp = grupo.Select(p => p.SubPassos).OrderBy(v => v).ToArray();
    int comSolucao = grupo.Count(p => p.Chamada.Resultado is not null);
    Console.WriteLine($"| {grupo.Key} | {ms.Length} | {comSolucao} | {F(Q(ms, 0.5), 3)} | {F(Q(ms, 0.99), 3)} | {F(ms[^1], 3)} | {ms.Count(v => v > 1)} | {Q(sp, 0.5)} | {sp[^1]} |");
}
if (divergentes > 0) Console.WriteLine($"\n**ATENÇÃO: {divergentes} chamadas isoladas devolveram golpe diferente do da partida — não é determinístico.**");
if (porChamada.Count > 0)
{
    var (pc, piorMs, piorSubPassos) = porChamada.MaxBy(p => p.Ms);
    Console.WriteLine($"\nChamada mais lenta: {pc.Qual}({G(pc.X0)}; {G(pc.Y0)}; {G(pc.Z0)}; lado {pc.LadoDoAlvo}; {G(pc.Argumento)}) — {F(piorMs, 3)} ms, {piorSubPassos} sub-passos.");
}

string F(double v, int casas) => v.ToString("F" + casas, cultura);
static string G(float v) => v.ToString("R", CultureInfo.InvariantCulture);
static T Q<T>(T[] ordenados, double q) => ordenados[Math.Clamp((int)Math.Ceiling(q * ordenados.Length) - 1, 0, ordenados.Length - 1)];

static Golpe? Chamar(ChamadaDeSolucionador c) => c.Qual switch
{
    Solucionador.Chiquita => GolpesEspeciais.Chiquita(c.X0, c.Y0, c.Z0, c.LadoDoAlvo, c.Argumento),
    Solucionador.Contrapared => GolpesEspeciais.Contrapared(c.X0, c.Y0, c.Z0, c.LadoDoAlvo, c.Argumento),
    Solucionador.SmashPor3 => GolpesEspeciais.SmashPor3(c.X0, c.Y0, c.Z0, c.LadoDoAlvo, (int)c.Argumento),
    _ => GolpesEspeciais.SmashPor4(c.X0, c.Y0, c.Z0, c.LadoDoAlvo, c.Argumento),
};

// Uma partida inteira; aoMedir recebe o tempo de cada Avancar e, nos passos > 2 ms, quem rodava nele (só ali monta o
// texto: alocar a cada passo traria coleta de lixo pra dentro da medida). Com o cronômetro ligado, a coleta anota as
// chamadas do passo (a lista é esvaziada a cada passo, então não cresce).
void Jogar(Dificuldade d, uint semente, Action<double, string>? aoMedir)
{
    var partida = new Partida(new OpcoesDaPartida { Humanos = OpcoesDaPartida.NinguemHumano, Semente = semente, Dificuldade = d });
    TipoDeGolpe? golpe = null;
    partida.Evento += ev => { if (ev.Tipo == TipoDeEventoDaPartida.Golpe) golpe = ev.Golpe; };
    var doPasso = new List<ChamadaDeSolucionador>();
    if (aoMedir is not null) GolpesEspeciais.Coleta = doPasso;
    float t = 0;
    while (!partida.Acabou && t < 3600)
    {
        golpe = null;
        doPasso.Clear();
        long antes = Stopwatch.GetTimestamp();
        partida.Avancar(passo);
        long depois = Stopwatch.GetTimestamp();
        double ms = (depois - antes) * msPorTique;
        aoMedir?.Invoke(ms, ms > 2 ? Quem(doPasso, golpe) : "");
        t += passo;
    }
    if (aoMedir is not null) GolpesEspeciais.Coleta = null;
}

static string Quem(List<ChamadaDeSolucionador> doPasso, TipoDeGolpe? golpe) =>
    doPasso.Count > 0
        ? string.Join(" + ", doPasso.Select(c => c.Resultado is null ? $"{c.Qual} → nulo" : c.Qual.ToString()))
        : golpe?.ToString() ?? "sem golpe";

static Linha Resumir(List<double> tempos)
{
    var ordenados = tempos.ToArray();
    Array.Sort(ordenados);
    return new Linha(ordenados.Length, Q(ordenados, 0.5), Q(ordenados, 0.99), Q(ordenados, 0.999),
        ordenados.Count(v => v > 2), ordenados.Count(v => v > 4), ordenados.Count(v => v > 8.3), ordenados[^1]);
}

readonly record struct Linha(int Passos, double Mediana, double P99, double P999, int Mais2, int Mais4, int Mais83, double Maximo);
