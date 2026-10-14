using System.Globalization;
using System.Text;
using Padel.Core;

/// <summary>
/// Gabarito dos solucionadores dos golpes especiais (chiquita, contrapared, remate por 3 e por 4), pro PadelSim (C++)
/// reproduzir bit a bit. Por chamada: os argumentos, o golpe devolvido inteiro (ou nada, "-") e quantos sub-passos de bola
/// ela simulou (Bola.SubPassosNestaThread antes e depois). Os sub-passos entram porque o orçamento da chiquita conta por
/// eles: uma conta diferente muda o resultado. Mais o GolpesEspeciais.VelocidadeDe, com e sem Lancamento.
/// Grava golpes-especiais.csv e velocidade-de.csv.
/// </summary>
static class GabaritoDosGolpesEspeciais
{
    private static readonly CultureInfo Ci = CultureInfo.InvariantCulture;
    private static string F(float f) => f.ToString("R", Ci);
    private static string B(bool b) => b ? "1" : "0";
    private const string Nada = "-";

    /// <summary>Uma chamada: Argumento é o alvoX (chiquita, contrapared, por 4) ou o ladoDaSaida (por 3, inteiro).</summary>
    private readonly record struct Chamada(Solucionador Qual, float X0, float Y0, float Z0, int LadoDoAlvo, float Argumento);

    private static uint _semente = 20261014;
    /// <summary>LCG daqui; o que vale é o número gravado no CSV — o C++ não precisa reproduzir o LCG.</summary>
    private static float Sorteio(float a, float b)
    {
        _semente = _semente * 1664525u + 1013904223u;
        return a + (b - a) * ((_semente >> 8) / 16777216f);
    }

    private static List<Chamada> Casos()
    {
        var c = new List<Chamada>();
        int i;

        // ── Chiquita: a bola do lado oposto ao alvo. Grade pelos dois lados, de perto da rede ao fundo, três alturas;
        // x e alvo girando (-4,8 e 4,9 passam do limite de ±4,5, que o solucionador aplica).
        float[] xs = [-3.5f, 0.4f, 4.2f];
        float[] alvos = [-4.8f, -2f, 0f, 1.6f, 4.9f];
        i = 0;
        foreach (int lado in new[] { 1, -1 })
            foreach (float d in new[] { 0.3f, 1.2f, 2.5f, 4.5f, 7f, 9.5f })
                foreach (float z in new[] { 0.35f, 0.9f, 1.7f })
                {
                    c.Add(new(Solucionador.Chiquita, xs[i % 3], -lado * d, z, lado, alvos[i % 5]));
                    i++;
                }
        // Colada na rede (logo acima da distância mínima de 0,10): onde o balão e o orçamento entram.
        i = 0;
        foreach (float d in new[] { 0.105f, 0.13f, 0.2f, 0.5f })
            foreach (float z in new[] { 0.2f, 0.6f, 1.3f })
            {
                int lado = i % 2 == 0 ? 1 : -1;
                c.Add(new(Solucionador.Chiquita, xs[(i + 1) % 3], -lado * d, z, lado, alvos[(i + 2) % 5]));
                i++;
            }
        // Colada na rede e rente ao chão: sem chiquita, é quem gasta o orçamento (20 mil sub-passos; 10 mil por família).
        i = 0;
        foreach (float d in new[] { 0.101f, 0.11f, 0.16f })
            foreach (float z in new[] { 0.04f, 0.1f })
            {
                int lado = i % 2 == 0 ? -1 : 1;
                c.Add(new(Solucionador.Chiquita, xs[i % 3], -lado * d, z, lado, alvos[(i + 3) % 5]));
                i++;
            }
        // Esgotam o orçamento da chamada (20 mil), achadas por busca em 40 mil posições: cruzadas, perto da rede, z ~1,5 a
        // 2,5. Umas devolvem o que acharam antes de esgotar, outras null; a última esgota famílias (10 mil) e não a chamada.
        c.Add(new(Solucionador.Chiquita, 4.9457817f, 1.0026315f, 2.033754f, -1, -4.2519064f));
        c.Add(new(Solucionador.Chiquita, -4.468962f, -0.1101024f, 1.4819953f, 1, 4.977705f));
        c.Add(new(Solucionador.Chiquita, -4.5094967f, 0.17039041f, 1.4816934f, -1, 2.8137503f));
        c.Add(new(Solucionador.Chiquita, 4.426832f, 0.27000386f, 1.5355074f, -1, -2.9654193f));
        c.Add(new(Solucionador.Chiquita, -1.9380748f, -0.2136451f, 1.517379f, 1, 3.1911535f));
        c.Add(new(Solucionador.Chiquita, 4.810794f, -1.6306535f, 2.5354192f, 1, -4.921081f));
        c.Add(new(Solucionador.Chiquita, 3.972313f, 0.13194162f, 1.4797467f, -1, -4.977766f));
        c.Add(new(Solucionador.Chiquita, -4.8f, -0.4f, 0.3f, 1, 4.5f));
        // Null pelas guardas: já do lado do alvo, colada na rede (|y| < 0,10, inclusive y = 0) e fora das paredes.
        c.Add(new(Solucionador.Chiquita, 1f, 3f, 0.8f, 1, 0f));
        c.Add(new(Solucionador.Chiquita, -2f, -5f, 1.1f, -1, 2f));
        c.Add(new(Solucionador.Chiquita, 0.5f, -0.05f, 0.6f, 1, 0f));
        c.Add(new(Solucionador.Chiquita, 0.5f, 0f, 0.6f, -1, 0f));
        c.Add(new(Solucionador.Chiquita, 0.5f, 0f, 0.6f, 1, 0f));
        c.Add(new(Solucionador.Chiquita, 0f, -10.3f, 1f, 1, 0f));
        c.Add(new(Solucionador.Chiquita, 5.4f, 4f, 1f, -1, 0f));
        for (int k = 0; k < 16; k++)
        {
            int lado = k % 2 == 0 ? 1 : -1;
            c.Add(new(Solucionador.Chiquita, Sorteio(-4.8f, 4.8f), -lado * Sorteio(0.1f, 9.8f), Sorteio(0.15f, 2.4f), lado, Sorteio(-5f, 5f)));
        }

        // ── Contrapared: a bola junto ao próprio vidro de fundo (o lado -ladoDoAlvo).
        float[] xsC = [-3.2f, 0f, 3.6f];
        float[] alvosC = [0f, -2.5f, 3f];
        i = 0;
        foreach (int lado in new[] { 1, -1 })
            foreach (float yy in new[] { 8.6f, 9.3f, 9.8f })
                foreach (float z in new[] { 0.4f, 1f, 1.6f })
                {
                    c.Add(new(Solucionador.Contrapared, xsC[i % 3], -lado * yy, z, lado, alvosC[(i / 3) % 3]));
                    i++;
                }
        // Longe do vidro, alta demais ou já do lado do alvo: o mais provável é null.
        foreach (int lado in new[] { 1, -1 })
            foreach (float yy in new[] { 2f, 5f, 7f })
                c.Add(new(Solucionador.Contrapared, 1f, -lado * yy, 1f, lado, 0f));
        c.Add(new(Solucionador.Contrapared, 0f, -9.5f, 2.8f, 1, 0f));
        c.Add(new(Solucionador.Contrapared, 2f, 9.5f, 3.5f, -1, 0f));
        c.Add(new(Solucionador.Contrapared, 0f, 9.5f, 1f, 1, 0f));
        c.Add(new(Solucionador.Contrapared, -1f, -9.5f, 1f, -1, 0f));
        for (int k = 0; k < 12; k++)
        {
            int lado = k % 2 == 0 ? 1 : -1;
            c.Add(new(Solucionador.Contrapared, Sorteio(-4.7f, 4.7f), -lado * Sorteio(6f, 9.9f), Sorteio(0.2f, 2.2f), lado, Sorteio(-5f, 5f)));
        }

        // ── Remates: a bola alta do lado oposto ao alvo, de perto da rede ao meio da quadra.
        float[] xsR = [-2.5f, 0.3f, 3f];
        i = 0;
        foreach (int lado in new[] { 1, -1 })
            foreach (float d in new[] { 0.8f, 2f, 3.5f, 6f })
                foreach (float z in new[] { 2f, 2.6f, 3.2f })
                {
                    float x = xsR[i % 3];
                    float alvo = (i % 3) switch { 0 => x, 1 => 0f, _ => -4f };
                    c.Add(new(Solucionador.SmashPor4, x, -lado * d, z, lado, alvo));
                    c.Add(new(Solucionador.SmashPor3, xsR[(i + 1) % 3], -lado * d, z, lado, i % 2 == 0 ? 1 : -1));
                    i++;
                }
        for (int k = 0; k < 10; k++)
        {
            int lado = k % 2 == 0 ? 1 : -1;
            c.Add(new(Solucionador.SmashPor4, Sorteio(-4.5f, 4.5f), -lado * Sorteio(0.3f, 8f), Sorteio(1.4f, 3.6f), lado, Sorteio(-5f, 5f)));
            c.Add(new(Solucionador.SmashPor3, Sorteio(-4.5f, 4.5f), -lado * Sorteio(0.3f, 8f), Sorteio(1.4f, 3.6f), lado, k % 4 < 2 ? 1 : -1));
        }
        // ladoDaSaida 0 conta como +1; já do lado do alvo (nenhuma reta cruza a rede); fora da quadra (sem cortes: tudo se simula).
        c.Add(new(Solucionador.SmashPor3, 0f, -2f, 2.6f, 1, 0));
        c.Add(new(Solucionador.SmashPor3, 1f, 3f, 2.4f, -1, 0));
        c.Add(new(Solucionador.SmashPor4, 0f, 2f, 2.6f, 1, 0f));
        c.Add(new(Solucionador.SmashPor3, 0f, -2f, 2.6f, -1, 1));
        c.Add(new(Solucionador.SmashPor4, 5.2f, -2f, 2.6f, 1, 0f));
        c.Add(new(Solucionador.SmashPor3, -5.3f, 2.5f, 2.8f, -1, -1));
        return c;
    }

    private static string Nome(Solucionador s) => s switch
    {
        Solucionador.Chiquita => "chiquita",
        Solucionador.Contrapared => "contrapared",
        Solucionador.SmashPor3 => "por3",
        _ => "por4",
    };

    private static string Lancamento(Velocidade? l) => l is Velocidade v
        ? string.Join(',', "1", F(v.Vx), F(v.Vy), F(v.Vz), F(v.Tempo), F(v.Wx), F(v.Wy), F(v.Wz))
        : string.Join(',', "0", Nada, Nada, Nada, Nada, Nada, Nada, Nada);

    public static void Gravar(string pasta, string commit)
    {
        var chamadas = Casos();
        var sb = new StringBuilder($"# gabarito do Padel.Core em {commit}\n");
        sb.AppendLine("id,solucionador,x0,y0,z0,ladoDoAlvo,argumento,subPassos,achou,alvoX,alvoY,tempoDeVoo,tipo,ignorarRede,topspinRpm,sidespinRpm,lado,temLancamento,vx,vy,vz,tempo,wx,wy,wz");
        var achados = new List<(float X0, float Y0, float Z0, Golpe G)>();
        var resumo = new Dictionary<Solucionador, (int Com, int Sem, long SubPassosMaximo)>();
        for (int id = 0; id < chamadas.Count; id++)
        {
            var c = chamadas[id];
            long antes = Bola.SubPassosNestaThread;
            Golpe? golpe = c.Qual switch
            {
                Solucionador.Chiquita => GolpesEspeciais.Chiquita(c.X0, c.Y0, c.Z0, c.LadoDoAlvo, c.Argumento),
                Solucionador.Contrapared => GolpesEspeciais.Contrapared(c.X0, c.Y0, c.Z0, c.LadoDoAlvo, c.Argumento),
                Solucionador.SmashPor3 => GolpesEspeciais.SmashPor3(c.X0, c.Y0, c.Z0, c.LadoDoAlvo, (int)c.Argumento),
                _ => GolpesEspeciais.SmashPor4(c.X0, c.Y0, c.Z0, c.LadoDoAlvo, c.Argumento),
            };
            long subPassos = Bola.SubPassosNestaThread - antes;
            string argumento = c.Qual == Solucionador.SmashPor3 ? ((int)c.Argumento).ToString(Ci) : F(c.Argumento);
            string resultado = golpe is Golpe g
                ? string.Join(',', "1", F(g.AlvoX), F(g.AlvoY), F(g.TempoDeVoo), (int)g.Tipo, B(g.IgnorarRede), F(g.Efeito.TopspinRpm), F(g.Efeito.SidespinRpm), (int)g.Lado, Lancamento(g.Lancamento))
                : string.Join(',', "0", Nada, Nada, Nada, Nada, Nada, Nada, Nada, Nada, Lancamento(null));
            sb.AppendLine(string.Join(',', id, Nome(c.Qual), F(c.X0), F(c.Y0), F(c.Z0), c.LadoDoAlvo, argumento, subPassos, resultado));

            var r = resumo.GetValueOrDefault(c.Qual);
            resumo[c.Qual] = golpe is null ? (r.Com, r.Sem + 1, Math.Max(r.SubPassosMaximo, subPassos)) : (r.Com + 1, r.Sem, Math.Max(r.SubPassosMaximo, subPassos));
            if (golpe is Golpe achado) achados.Add((c.X0, c.Y0, c.Z0, achado));
        }

        // VelocidadeDe: cada golpe achado com o Lancamento (devolve ele) e sem (Golpes.Calcular pelo alvo, com o efeito do
        // golpe), mais golpes feitos à mão, sem Lancamento, com e sem ignorarRede.
        var golpes = new List<(float X0, float Y0, float Z0, Golpe G)>();
        foreach (var a in achados)
        {
            golpes.Add(a);
            golpes.Add((a.X0, a.Y0, a.Z0, a.G with { Lancamento = null }));
        }
        for (int k = 0; k < 12; k++)
        {
            int lado = k % 2 == 0 ? 1 : -1;
            float x0 = Sorteio(-4f, 4f), y0 = -lado * Sorteio(1f, 9.5f), z0 = Sorteio(0.4f, 2.6f);
            var g = new Golpe(Sorteio(-4.5f, 4.5f), lado * Sorteio(1f, 9.5f), Sorteio(0.4f, 1.8f), TipoDeGolpe.Normal,
                IgnorarRede: k % 3 == 2, Efeito: new Efeito(Sorteio(-1500f, 2200f), k % 4 == 0 ? Sorteio(-1800f, 1800f) : 0f));
            golpes.Add((x0, y0, z0, g));
        }
        var sbV = new StringBuilder($"# gabarito do Padel.Core em {commit}\n");
        sbV.AppendLine("id,x0,y0,z0,alvoX,alvoY,tempoDeVoo,ignorarRede,topspinRpm,sidespinRpm,temLancamento,lvx,lvy,lvz,ltempo,lwx,lwy,lwz,vx,vy,vz,tempo,wx,wy,wz");
        for (int id = 0; id < golpes.Count; id++)
        {
            var (x0, y0, z0, g) = golpes[id];
            var v = GolpesEspeciais.VelocidadeDe(g, x0, y0, z0);
            sbV.AppendLine(string.Join(',', id, F(x0), F(y0), F(z0), F(g.AlvoX), F(g.AlvoY), F(g.TempoDeVoo), B(g.IgnorarRede),
                F(g.Efeito.TopspinRpm), F(g.Efeito.SidespinRpm), Lancamento(g.Lancamento), F(v.Vx), F(v.Vy), F(v.Vz), F(v.Tempo), F(v.Wx), F(v.Wy), F(v.Wz)));
        }

        File.WriteAllText(Path.Combine(pasta, "golpes-especiais.csv"), sb.ToString());
        File.WriteAllText(Path.Combine(pasta, "velocidade-de.csv"), sbV.ToString());
        Console.WriteLine($"golpes especiais: {chamadas.Count} chamadas; {golpes.Count} de VelocidadeDe");
        foreach (var (qual, r) in resumo.OrderBy(p => p.Key))
            Console.WriteLine($"  {Nome(qual)}: {r.Com + r.Sem} chamadas, {r.Com} com solução, {r.Sem} null, até {r.SubPassosMaximo} sub-passos");
    }
}
