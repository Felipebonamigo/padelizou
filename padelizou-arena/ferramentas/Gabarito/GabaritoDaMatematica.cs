using System.Text;
using Padel.Core;

/// <summary>
/// As funções sem libm (MatematicaDeterministica), entrada e saída em bits: a grade das faixas que o motor usa, os
/// ângulos do remate, as pontas (zero, subnormal, saturação) e entradas sorteadas em bits. O C++ tem de dar os mesmos
/// bits em todas — é a prova mais fina antes das partidas.
/// </summary>
static class GabaritoDaMatematica
{
    public static void Gravar(string pasta, string commit)
    {
        var sb = new StringBuilder($"# gabarito do Padel.Core em {commit}\nfuncao,a,b,resultado\n");
        uint Bits(float f) => BitConverter.SingleToUInt32Bits(f);
        void Um(string nome, float a, float r) => sb.AppendLine($"{nome},{Bits(a)},0,{Bits(r)}");
        void Dois(string nome, float a, float b, float r) => sb.AppendLine($"{nome},{Bits(a)},{Bits(b)},{Bits(r)}");

        var r = new Aleatorio(20261014);
        float Em(float de, float ate) => de + (ate - de) * r.Proximo();

        for (int i = 0; i <= 4000; i++)
        {
            float x = -4 * MathF.PI + 8 * MathF.PI * i / 4000;
            Um("seno", x, MatematicaDeterministica.Seno(x));
            Um("cosseno", x, MatematicaDeterministica.Cosseno(x));
        }
        for (int graus = -90; graus <= 90; graus++)
        {
            float a = graus * MathF.PI / 180f;
            Um("seno", a, MatematicaDeterministica.Seno(a));
            Um("cosseno", a, MatematicaDeterministica.Cosseno(a));
        }
        foreach (float x in new[] { 0f, -0f, 1e-30f, -1e-30f, 1e-45f, 100f, -1000f, 8000f })
        {
            Um("seno", x, MatematicaDeterministica.Seno(x));
            Um("cosseno", x, MatematicaDeterministica.Cosseno(x));
        }
        for (int i = 0; i < 3000; i++)
        {
            float y = Em(-20, 20), x = Em(-20, 20);
            Dois("atan2", y, x, MatematicaDeterministica.ArcoTangente2(y, x));
        }
        foreach (var (y, x) in new[] { (0f, 0f), (1f, 0f), (-1f, 0f), (0f, 1f), (0f, -1f), (1e-30f, 1e30f), (1e30f, 1e-30f), (-3f, -1e-20f), (2f, 1f), (1f, 2f) })
            Dois("atan2", y, x, MatematicaDeterministica.ArcoTangente2(y, x));
        for (int i = 0; i < 4000; i++)
        {
            float x = Em(-90, 90);
            Um("exp", x, MatematicaDeterministica.Exp(x));
        }
        foreach (float x in new[] { 0f, -0f, 1f, -1f, 88.7f, 88.72f, 88.73f, -87.3f, -87.34f, -87.4f, -200f, 200f, 1e-8f, -1e-8f })
            Um("exp", x, MatematicaDeterministica.Exp(x));
        for (int i = 0; i < 4000; i++)
        {
            float x = i < 2000 ? Em(1e-7f, 4) : MathF.Pow(10, Em(-38, 38));   // a grade sorteia o argumento; o Log é o testado
            Um("log", x, MatematicaDeterministica.Log(x));
        }
        foreach (float x in new[] { 1f, 0.5f, 2f, 0.70710677f, 0.7071068f, 1f / 16777216f, 1e-40f, 1e-45f, 0f, -1f, float.PositiveInfinity, float.MaxValue })
            Um("log", x, MatematicaDeterministica.Log(x));
        for (int i = 0; i < 3000; i++)
        {
            float b = Em(0.01f, 30), e = Em(-4, 4);
            Dois("potencia", b, e, MatematicaDeterministica.Potencia(b, e));
        }
        foreach (var (b, e) in new[] { (0f, 2f), (0f, 0f), (0f, -1f), (-2f, 0.5f), (3f, 0f), (1f, 1000f) })
            Dois("potencia", b, e, MatematicaDeterministica.Potencia(b, e));
        for (int k = -5; k <= 20; k++) Dois("potenciaInteira", 3, k, MatematicaDeterministica.PotenciaInteira(3, k));
        File.WriteAllText(Path.Combine(pasta, "matematica.csv"), sb.ToString());
    }
}
