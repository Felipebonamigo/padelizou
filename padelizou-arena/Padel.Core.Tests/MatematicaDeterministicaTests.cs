using System.Text.RegularExpressions;

namespace Padel.Core.Tests;

/// <summary>
/// O motor sem libm (14/10/2026, decisão do Felipe): seno, cosseno, arco-tangente, exponencial, logaritmo e potência da
/// libm mudam de bits entre a glibc e a UCRT da Microsoft — e entre versões da glibc —, e o PadelSim (o porte em C++ do
/// repositório padelizou-game) precisa sair igual em qualquer máquina. <see cref="MatematicaDeterministica"/> faz as
/// seis só com soma, multiplicação, divisão, raiz e manipulação de bits, na mesma ordem nas duas linguagens.
/// Aqui: a precisão de cada uma contra o Math em double (tolerâncias decididas antes de escrever o código, de ~2 ulps) e
/// o portão que impede a libm de voltar aos arquivos do motor.
/// </summary>
public class MatematicaDeterministicaTests
{
    private static IEnumerable<float> Grade(float de, float ate, int pontos)
    {
        for (int i = 0; i <= pontos; i++) yield return de + (ate - de) * i / pontos;
    }

    [Fact]
    public void Seno_e_cosseno_erram_no_maximo_3e7_entre_menos_e_mais_dois_pi()
    {
        float pior = 0;
        foreach (float x in Grade(-2 * MathF.PI, 2 * MathF.PI, 200_000))
        {
            pior = MathF.Max(pior, (float)Math.Abs(MatematicaDeterministica.Seno(x) - Math.Sin(x)));
            pior = MathF.Max(pior, (float)Math.Abs(MatematicaDeterministica.Cosseno(x) - Math.Cos(x)));
        }
        Assert.True(pior <= 3e-7f, $"pior erro {pior:E3}");
    }

    [Fact]
    public void Seno_e_cosseno_de_zero_e_dos_angulos_do_remate()
    {
        Assert.Equal(0f, MatematicaDeterministica.Seno(0));
        Assert.Equal(1f, MatematicaDeterministica.Cosseno(0));
        // As 17 elevações do remate (−8° a −40°) e os azimutes (±8°, ±16°, ±24°): o uso de verdade.
        for (int graus = -40; graus <= 24; graus++)
        {
            float a = graus * MathF.PI / 180f;
            Assert.True(Math.Abs(MatematicaDeterministica.Seno(a) - Math.Sin(a)) <= 1.2e-7, $"sen {graus}°");
            Assert.True(Math.Abs(MatematicaDeterministica.Cosseno(a) - Math.Cos(a)) <= 1.2e-7, $"cos {graus}°");
        }
    }

    [Fact]
    public void ArcoTangente2_erra_no_maximo_3e7_nos_quatro_quadrantes()
    {
        float pior = 0;
        foreach (float y in Grade(-20, 20, 400))
            foreach (float x in Grade(-20, 20, 400))
            {
                if (x == 0 && y == 0) continue;
                pior = MathF.Max(pior, (float)Math.Abs(MatematicaDeterministica.ArcoTangente2(y, x) - Math.Atan2(y, x)));
            }
        Assert.True(pior <= 3e-7f, $"pior erro {pior:E3}");
        Assert.Equal(MathF.PI / 2, MatematicaDeterministica.ArcoTangente2(1, 0));
        Assert.Equal(-MathF.PI / 2, MatematicaDeterministica.ArcoTangente2(-1, 0));
        Assert.Equal(0f, MatematicaDeterministica.ArcoTangente2(0, 1));
        Assert.Equal(MathF.PI, MatematicaDeterministica.ArcoTangente2(0, -1));
    }

    [Fact]
    public void Exp_erra_no_maximo_3e7_relativo_e_satura_nas_pontas()
    {
        float pior = 0;
        foreach (float x in Grade(-87, 88, 400_000))
            pior = MathF.Max(pior, (float)(Math.Abs(MatematicaDeterministica.Exp(x) - Math.Exp(x)) / Math.Exp(x)));
        Assert.True(pior <= 3e-7f, $"pior erro relativo {pior:E3}");
        Assert.Equal(1f, MatematicaDeterministica.Exp(0));
        Assert.Equal(0f, MatematicaDeterministica.Exp(-200));
        Assert.Equal(float.PositiveInfinity, MatematicaDeterministica.Exp(200));
    }

    [Fact]
    public void Log_erra_no_maximo_3e7_e_trata_zero_negativo_e_subnormal()
    {
        float pior = 0;
        foreach (float x in Grade(1e-6f, 4, 400_000).Concat(Grade(4, 1e6f, 100_000)))
        {
            double exato = Math.Log(x);
            double erro = Math.Abs(MatematicaDeterministica.Log(x) - exato) / Math.Max(1, Math.Abs(exato));
            pior = MathF.Max(pior, (float)erro);
        }
        Assert.True(pior <= 3e-7f, $"pior erro {pior:E3}");
        Assert.Equal(0f, MatematicaDeterministica.Log(1));
        Assert.Equal(float.NegativeInfinity, MatematicaDeterministica.Log(0));
        Assert.True(float.IsNaN(MatematicaDeterministica.Log(-1)));
        // O menor número do aleatório (2^-24) e um subnormal.
        Assert.True(Math.Abs(MatematicaDeterministica.Log(1f / 16777216f) - Math.Log(1.0 / 16777216.0)) <= 3e-6);
        Assert.True(Math.Abs(MatematicaDeterministica.Log(1e-40f) - Math.Log(1e-40f)) <= 1e-4);
    }

    [Fact]
    public void Potencia_erra_no_maximo_2e6_relativo_e_a_inteira_e_exata()
    {
        float pior = 0;
        foreach (float b in Grade(0.05f, 20, 400))
            foreach (float e in Grade(-3, 3, 120))
                pior = MathF.Max(pior, (float)(Math.Abs(MatematicaDeterministica.Potencia(b, e) - Math.Pow(b, e)) / Math.Pow(b, e)));
        Assert.True(pior <= 2e-6f, $"pior erro relativo {pior:E3}");
        for (int k = 0; k <= 15; k++) Assert.Equal((float)Math.Pow(3, k), MatematicaDeterministica.PotenciaInteira(3, k));
    }

    /// <summary>
    /// O portão: nenhum arquivo do motor da partida (a raiz de Padel.Core — bola, quadra, golpes, golpes especiais,
    /// jogadores, IA, partida, árbitro, placar, humano simulado, passo fixo, aleatório) chama função da libm. Torneio,
    /// Rodizio, Ranking e Perfil ficam de fora: simulam resultados da carreira fora da partida e não vão pro PadelSim.
    /// </summary>
    [Fact]
    public void Nenhum_arquivo_do_motor_chama_funcao_da_libm()
    {
        var libm = new Regex(@"\bMathF?\.(Sin|Cos|Tan|Asin|Acos|Atan|Atan2|Exp|Log|Log2|Log10|Pow|Cbrt|Sinh|Cosh|Tanh|SinCos|FusedMultiplyAdd|ScaleB)\s*\(");
        var achados = new List<string>();
        foreach (string arquivo in Directory.GetFiles(PastaDoCore(), "*.cs", SearchOption.TopDirectoryOnly))
        {
            if (Path.GetFileName(arquivo) == "MatematicaDeterministica.cs") continue;
            var linhas = File.ReadAllLines(arquivo);
            for (int i = 0; i < linhas.Length; i++)
            {
                string codigo = linhas[i];
                int comentario = codigo.IndexOf("//", StringComparison.Ordinal);
                if (comentario >= 0) codigo = codigo[..comentario];
                if (libm.IsMatch(codigo)) achados.Add($"{Path.GetFileName(arquivo)}:{i + 1}: {linhas[i].Trim()}");
            }
        }
        Assert.True(achados.Count == 0, "libm no motor (use MatematicaDeterministica):\n" + string.Join('\n', achados));
    }

    private static string PastaDoCore()
    {
        for (var pasta = new DirectoryInfo(AppContext.BaseDirectory); pasta is not null; pasta = pasta.Parent)
        {
            string candidato = Path.Combine(pasta.FullName, "Padel.Core");
            if (File.Exists(Path.Combine(candidato, "Padel.Core.csproj"))) return candidato;
        }
        throw new DirectoryNotFoundException("Padel.Core não encontrado subindo a partir de " + AppContext.BaseDirectory);
    }
}
