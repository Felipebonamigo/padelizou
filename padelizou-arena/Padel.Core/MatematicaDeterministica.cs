namespace Padel.Core;

/// <summary>
/// Seno, cosseno, arco-tangente, exponencial, logaritmo e potência sem a libm (decisão do Felipe, 14/10/2026). A libm
/// muda de bits entre a glibc e a UCRT da Microsoft — e entre versões da glibc —, e o motor precisa dar o mesmo
/// resultado em qualquer máquina: o PadelSim (o porte em C++, repositório padelizou-game) reproduz o Padel.Core bit a
/// bit, no Linux e no Windows. Aqui só há soma, subtração, multiplicação, divisão, raiz, floor, conversão int↔float e
/// manipulação de bits — operações que o IEEE 754 define exatas em qualquer máquina. O PadelSim tem as mesmas contas na
/// MESMA ordem (em float, (a·b)·c ≠ a·(b·c)): mudou uma linha aqui, muda lá, e o gabarito confere.
/// <para>
/// Os algoritmos são os da Cephes (Stephen L. Moshier, domínio público: sinf, cosf, atanf, expf, logf), com os
/// coeficientes de lá. Precisão de ~1 a 2 ulps nas faixas que o motor usa (MatematicaDeterministicaTests).
/// </para>
/// </summary>
public static class MatematicaDeterministica
{
    private const float Pi = 3.14159265358979323846f;
    private const float PiSobre2 = 1.57079632679489661923f;
    private const float PiSobre4 = 0.78539816339744830962f;

    // ───────────── Seno e cosseno ─────────────

    private const float QuatroSobrePi = 1.27323954473516f;
    // π/4 em três partes: x − y·π/4 sai com ~24 bits a mais de precisão (redução de Cody e Waite).
    private const float DP1 = 0.78515625f, DP2 = 2.4187564849853515625e-4f, DP3 = 3.77489497744594108e-8f;

    /// <summary>Seno (rad). atalho: a redução em três partes vale até |x| ≈ 8192; o motor usa poucos radianos.</summary>
    public static float Seno(float x)
    {
        if (!float.IsFinite(x)) return float.NaN;
        int sinal = 1;
        if (x < 0) { x = -x; sinal = -1; }
        int j = (int)(QuatroSobrePi * x);
        float y = j;
        if ((j & 1) != 0) { j += 1; y += 1; }
        j &= 7;
        if (j > 3) { sinal = -sinal; j -= 4; }
        float r = ((x - y * DP1) - y * DP2) - y * DP3;
        float z = r * r;
        float s = j == 1 || j == 2 ? PolinomioDoCosseno(z) : PolinomioDoSeno(r, z);
        return sinal < 0 ? -s : s;
    }

    /// <summary>Cosseno (rad). Mesma redução do <see cref="Seno"/>.</summary>
    public static float Cosseno(float x)
    {
        if (!float.IsFinite(x)) return float.NaN;
        if (x < 0) x = -x;
        int j = (int)(QuatroSobrePi * x);
        float y = j;
        if ((j & 1) != 0) { j += 1; y += 1; }
        j &= 7;
        int sinal = 1;
        if (j > 3) { j -= 4; sinal = -sinal; }
        if (j > 1) sinal = -sinal;
        float r = ((x - y * DP1) - y * DP2) - y * DP3;
        float z = r * r;
        float c = j == 1 || j == 2 ? PolinomioDoSeno(r, z) : PolinomioDoCosseno(z);
        return sinal < 0 ? -c : c;
    }

    /// <summary>sen r em [−π/4, π/4], com z = r².</summary>
    private static float PolinomioDoSeno(float r, float z) =>
        ((-1.9515295891E-4f * z + 8.3321608736E-3f) * z - 1.6666654611E-1f) * z * r + r;

    /// <summary>cos r em [−π/4, π/4], com z = r².</summary>
    private static float PolinomioDoCosseno(float z) =>
        ((2.443315711809948E-5f * z - 1.388731625493765E-3f) * z + 4.166664568298827E-2f) * z * z - 0.5f * z + 1.0f;

    // ───────────── Arco-tangente ─────────────

    /// <summary>Arco-tangente de y/x no quadrante certo, em (−π, π]. ArcoTangente2(0, 0) = 0.</summary>
    public static float ArcoTangente2(float y, float x)
    {
        if (float.IsNaN(x) || float.IsNaN(y)) return float.NaN;
        if (x == 0)
        {
            if (y > 0) return PiSobre2;
            if (y < 0) return -PiSobre2;
            return 0;
        }
        if (y == 0) return x > 0 ? 0 : Pi;
        float w = ArcoTangente(y / x);
        if (x < 0) w = y < 0 ? w - Pi : w + Pi;
        return w;
    }

    private static float ArcoTangente(float x)
    {
        int sinal = 1;
        if (x < 0) { sinal = -1; x = -x; }
        float y;
        if (x > 2.414213562373095f) { y = PiSobre2; x = -(1 / x); }          // tan(3π/8)
        else if (x > 0.4142135623730950f) { y = PiSobre4; x = (x - 1) / (x + 1); }   // tan(π/8)
        else y = 0;
        float z = x * x;
        y += (((8.05374449538e-2f * z - 1.38776856032E-1f) * z + 1.99777106478E-1f) * z - 3.33329491539E-1f) * z * x + x;
        return sinal < 0 ? -y : y;
    }

    // ───────────── Exponencial ─────────────

    /// <summary>
    /// e^x. x = n·ln 2 + r, com ln 2 em duas partes; e^r por polinômio em |r| ≤ ln2/2; vezes 2^n montado nos bits.
    /// atalho: abaixo de −87,34 (onde o resultado ficaria subnormal) devolve 0 — o motor nunca chega perto.
    /// </summary>
    public static float Exp(float x)
    {
        if (float.IsNaN(x)) return float.NaN;
        if (x > 88.72283905206835f) return float.PositiveInfinity;
        if (x < -87.33654475055310898657f) return 0;
        float z = MathF.Floor(1.44269504088896341f * x + 0.5f);
        x -= z * 0.693359375f;
        x -= z * -2.12194440e-4f;
        int n = (int)z;
        float x2 = x * x;
        float p = (((((1.9875691500E-4f * x + 1.3981999507E-3f) * x + 8.3334519073E-3f) * x + 4.1665795894E-2f) * x
            + 1.6666665459E-1f) * x + 5.0000001201E-1f) * x2 + x + 1.0f;
        // n vai de −126 a 128; 2^128 não cabe num float: aí multiplica por 2^127 e por 2 (o segundo pode estourar, e deve).
        return n > 127 ? p * DoisElevadoA(127) * 2 : p * DoisElevadoA(n);
    }

    /// <summary>2^n exato, montado nos bits do expoente (n em [−126, 127]).</summary>
    private static float DoisElevadoA(int n) => BitConverter.Int32BitsToSingle((n + 127) << 23);

    // ───────────── Logaritmo ─────────────

    /// <summary>
    /// ln x. x = m·2^e com m em [√½, √2) pelos bits; ln(1 + (m − 1)) por polinômio; e·ln 2 com ln 2 em duas partes.
    /// Log(0) = −∞; negativo = NaN; subnormal entra escalado por 2^25.
    /// </summary>
    public static float Log(float x)
    {
        if (float.IsNaN(x) || x < 0) return float.NaN;
        if (x == 0) return float.NegativeInfinity;
        if (float.IsPositiveInfinity(x)) return x;
        int e = 0;
        int bits = BitConverter.SingleToInt32Bits(x);
        if ((bits >> 23) == 0)
        {
            x *= 33554432f;   // 2^25: tira do subnormal
            e = -25;
            bits = BitConverter.SingleToInt32Bits(x);
        }
        e += ((bits >> 23) & 0xff) - 126;
        float m = BitConverter.Int32BitsToSingle((bits & 0x007fffff) | 0x3f000000);   // em [0,5, 1)
        if (m < 0.707106781186547524f) { e -= 1; m = m + m - 1; }
        else m = m - 1;
        float z = m * m;
        float y = ((((((((7.0376836292E-2f * m - 1.1514610310E-1f) * m + 1.1676998740E-1f) * m - 1.2420140846E-1f) * m
            + 1.4249322787E-1f) * m - 1.6668057665E-1f) * m + 2.0000714765E-1f) * m - 2.4999993993E-1f) * m
            + 3.3333331174E-1f) * m * z;
        float fe = e;
        y += -2.12194440e-4f * fe;
        y += -0.5f * z;
        float r = m + y;
        r += 0.693359375f * fe;
        return r;
    }

    // ───────────── Potência ─────────────

    /// <summary>b^e = e^(e·ln b) para b &gt; 0. b = 0: 0 (e &gt; 0), 1 (e = 0) ou +∞ (e &lt; 0); b &lt; 0: NaN.</summary>
    public static float Potencia(float b, float e)
    {
        if (e == 0) return 1;
        if (b > 0) return Exp(e * Log(b));
        if (b == 0) return e > 0 ? 0 : float.PositiveInfinity;
        return float.NaN;
    }

    /// <summary>b^n por multiplicações seguidas: exato enquanto couber nos 24 bits (3^15 cabe).</summary>
    public static float PotenciaInteira(float b, int n)
    {
        if (n < 0) return 1 / PotenciaInteira(b, -n);
        float r = 1;
        for (int i = 0; i < n; i++) r *= b;
        return r;
    }
}
