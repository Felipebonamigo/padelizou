using System.Globalization;
using System.Text;
using Padel.Core;

// Gabarito da física da bola: casos fixos (lançamentos e golpes) simulados pelo Padel.Core, gravados em CSV com
// todos os dígitos de cada float ("R"), pro PadelSim em C++ provar que reproduz a mesma física.
// Este arquivo: a bola e golpes::Calcular. Os golpes especiais e a partida têm arquivo próprio.
// Uso: dotnet run -- PASTA_DE_SAIDA COMMIT_DO_CORE
if (args.Length != 2) { Console.Error.WriteLine("uso: Gabarito PASTA_DE_SAIDA COMMIT_DO_CORE"); return 2; }
string pasta = args[0], commit = args[1];
Directory.CreateDirectory(pasta);
var ci = CultureInfo.InvariantCulture;
string F(float f) => f.ToString("R", ci);
string B(bool b) => b ? "1" : "0";

const float Quadro = 1f / 60f;        // o jogo avança a física em quadros de 1/60 s
const int QuadrosPorAmostra = 3;      // uma linha a cada 0,05 s
const int QuadrosMaximos = 4 * 60;    // até 4 s de voo

// ---- Casos de lançamento: (descrição, posição, velocidade, spin). Valores fixos; os "variados" saem de um LCG
// daqui, mas o que vale é o número gravado no CSV — o C++ não precisa reproduzir o LCG.
var casos = new List<(string Nome, float X, float Y, float Z, float Vx, float Vy, float Vz, float Wx, float Wy, float Wz)>();
const float Rpm = 2 * MathF.PI / 60f;
// Queda livre (a regra FIP: de 2,54 m a bola sobe 1,35–1,45 m).
foreach (float z in new[] { 2.54f, 1f, 3.5f }) casos.Add(($"queda de {z} m", 2, 5, z, 0, 0, 0, 0, 0, 0));
// Rumo ao vidro de fundo, com e sem efeito.
foreach (float wt in new[] { 0f, 2500f, -2500f })
    casos.Add(($"fundo, topspin {wt} rpm", 0, -3, 1, 0, 22, 2, -wt * Rpm, 0, 0));
// Vidro lateral do canto, com sidespin.
foreach (float ws in new[] { 0f, 1800f, -1800f })
    casos.Add(($"lateral do canto, sidespin {ws} rpm", 2, 7, 1, 14, 4, 2, 0, 0, ws * Rpm));
// Grade: acima dos 3 m do fundo e no meio da lateral.
casos.Add(("grade do fundo", 1, 2, 2.5f, 0, 18, 4, 0, 0, 0));
casos.Add(("grade do meio da lateral", 0, 3, 1.2f, 16, 0.5f, 3, 0, 0, 0));
casos.Add(("grade do degrau", 0, 6.8f, 2.2f, 15, 0, 1, 0, 0, 0));
// Saídas: pela porta, por cima do fundo, por cima do degrau.
casos.Add(("porta", 0, 0.85f, 0.8f, 14, 0, 1, 0, 0, 0));
casos.Add(("por cima do fundo", 0, 4, 2, 0, 15, 12, 0, 0, 0));
casos.Add(("por cima do degrau", 0, 7, 2, 12, 0, 6, 0, 0, 0));
// Rede: bate e cruza.
casos.Add(("bate na rede", 1, 6, 0.5f, 0, -15, 0.5f, 0, 0, 0));
casos.Add(("cruza a rede", -1, 6, 1, 0, -14, 3, 0, 0, 0));
// Rolando até parar.
casos.Add(("rola até parar", 0, 5, 0.3f, 1, 2, -0.5f, 0, 0, 0));
// Variados: posição e velocidade espalhadas pela quadra.
uint semente = 12345;
float Sorteio(float a, float b) { semente = semente * 1664525u + 1013904223u; return a + (b - a) * ((semente >> 8) / 16777216f); }
for (int i = 0; casos.Count < 120; i++)
{
    int lado = i % 2 == 0 ? 1 : -1;
    casos.Add(($"variado {i}",
        Sorteio(-4.5f, 4.5f), lado * Sorteio(0.5f, 9.5f), Sorteio(0.3f, 2.5f),
        Sorteio(-12, 12), -lado * Sorteio(-6, 30), Sorteio(-4, 14),
        Sorteio(-300, 300), Sorteio(-300, 300), Sorteio(-200, 200)));
}

var sbCasos = new StringBuilder($"# gabarito do Padel.Core em {commit}\nid,nome,x,y,z,vx,vy,vz,wx,wy,wz\n");
var sbTraj = new StringBuilder($"# gabarito do Padel.Core em {commit}\nid,quadro,x,y,z,vx,vy,vz,wx,wy,wz,emJogo,rolando,parada\n");
var sbEventos = new StringBuilder($"# gabarito do Padel.Core em {commit}\nid,quadro,tipo,x,y,z,lado,parede,superficie,pelaPorta\n");
var eventos = new List<EventoDaBola>();
for (int id = 0; id < casos.Count; id++)
{
    var c = casos[id];
    sbCasos.AppendLine(string.Join(',', id, c.Nome.Replace(',', ';'), F(c.X), F(c.Y), F(c.Z), F(c.Vx), F(c.Vy), F(c.Vz), F(c.Wx), F(c.Wy), F(c.Wz)));
    var bola = new Bola();
    bola.Posicionar(c.X, c.Y, c.Z);
    bola.Lancar(new Velocidade(c.Vx, c.Vy, c.Vz, 0, c.Wx, c.Wy, c.Wz));
    for (int q = 0; q <= QuadrosMaximos; q++)
    {
        if (q > 0)
        {
            eventos.Clear();
            bola.Avancar(Quadro, eventos);
            foreach (var e in eventos)
                sbEventos.AppendLine(string.Join(',', id, q, (int)e.Tipo, F(e.X), F(e.Y), F(e.Z), e.Lado, (int)e.Parede, (int)e.Superficie, B(e.PelaPorta)));
        }
        bool terminou = !bola.EmJogo || bola.Parada;
        if (q % QuadrosPorAmostra == 0 || terminou)   // o último estado sempre entra: é onde estão a saída e a parada
            sbTraj.AppendLine(string.Join(',', id, q, F(bola.X), F(bola.Y), F(bola.Z), F(bola.Vx), F(bola.Vy), F(bola.Vz), F(bola.Wx), F(bola.Wy), F(bola.Wz), B(bola.EmJogo), B(bola.Rolando), B(bola.Parada)));
        if (terminou) break;
    }
}

// ---- Golpes.Calcular: o lançamento que leva a bola ao alvo, por efeito (os números de golpe do REALISMO.md).
var efeitos = new (string Nome, Efeito E, float Tempo)[]
{
    ("drive", new Efeito(1200, 0), 0.9f), ("ataque", new Efeito(2200, 0), 0.7f), ("defesa", new Efeito(-1200, 0), 1.1f),
    ("lob", new Efeito(-400, 0), 1.8f), ("bandeja", new Efeito(-1500, 500), 1.0f), ("vibora", new Efeito(-800, 1800), 0.9f),
    ("smash", new Efeito(1500, 0), 0.5f), ("saque", new Efeito(-600, 0), 1.0f), ("chapado", Efeito.Nenhum, 0.9f),
};
var sbGolpes = new StringBuilder($"# gabarito do Padel.Core em {commit}\nid,nome,x0,y0,z0,alvoX,alvoY,tempoDeVoo,folgaNaRede,ignorarRede,topspinRpm,sidespinRpm,vx,vy,vz,tempo,wx,wy,wz\n");
int g = 0;
foreach (var (nome, efeito, tempo) in efeitos)
    for (int k = 0; k < 10; k++)
    {
        int lado = k % 2 == 0 ? 1 : -1;
        float x0 = Sorteio(-4, 4), y0 = lado * Sorteio(1, 9.5f), z0 = Sorteio(0.4f, 2.6f);
        float ax = Sorteio(-4.5f, 4.5f), ay = -lado * Sorteio(1, 9.5f);
        bool ignorar = k == 9;
        var v = Golpes.Calcular(x0, y0, z0, ax, ay, tempo, 0.3f, ignorar, efeito);
        sbGolpes.AppendLine(string.Join(',', g++, nome, F(x0), F(y0), F(z0), F(ax), F(ay), F(tempo), F(0.3f), B(ignorar), F(efeito.TopspinRpm), F(efeito.SidespinRpm),
            F(v.Vx), F(v.Vy), F(v.Vz), F(v.Tempo), F(v.Wx), F(v.Wy), F(v.Wz)));
    }

var sbConst = new StringBuilder($"# gabarito do Padel.Core em {commit}\nnome,valor\n");
sbConst.AppendLine($"KArrasto,{F(Bola.KArrasto)}");
sbConst.AppendLine($"KMagnus,{F(Bola.KMagnus)}");

// As outras áreas, cada uma no seu arquivo (e nos seus CSVs).
GabaritoDosGolpesEspeciais.Gravar(pasta, commit);
GabaritoDaPartida.Gravar(pasta, commit);
GabaritoDoPassoFixo.Gravar(pasta, commit);

File.WriteAllText(Path.Combine(pasta, "casos.csv"), sbCasos.ToString());
File.WriteAllText(Path.Combine(pasta, "trajetorias.csv"), sbTraj.ToString());
File.WriteAllText(Path.Combine(pasta, "eventos.csv"), sbEventos.ToString());
File.WriteAllText(Path.Combine(pasta, "golpes.csv"), sbGolpes.ToString());
File.WriteAllText(Path.Combine(pasta, "constantes.csv"), sbConst.ToString());
Console.WriteLine($"{casos.Count} casos, {g} golpes → {pasta}");
return 0;
