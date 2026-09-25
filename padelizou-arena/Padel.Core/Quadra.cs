namespace Padel.Core;

/// <summary>
/// Quadra de padel, em metros. Origem no centro da rede. x: -5..5 (largura 10 m); y: -10..10 (comprimento 20 m).
/// O time da casa (time 0) joga em y &gt; 0; o visitante (time 1) em y &lt; 0.
/// "Direita" de um lado é a direita de quem, naquele lado, olha pra rede: no lado +1 é +x, no lado -1 é -x.
/// </summary>
public static class Quadra
{
    public const float Largura = 10f;
    public const float Comprimento = 20f;
    public const float MeiaLargura = 5f;
    public const float MeioComprimento = 10f;
    public const float LinhaDeSaque = 6.95f;   // distância da rede
    public const float AlturaDaRede = 0.9f;    // 0,88 no centro e 0,92 nos postes — média
    // Paredes (regra FIP, simplificada). Medidas a calibrar com a planta de uma quadra panorâmica.
    // Fundo (|y| = 10) e canto da lateral (8 ≤ |y| ≤ 10): vidro até 3 m, grade de 3 a 4 m.
    public const float AlturaDoVidro = 3f;
    public const float AlturaDaParede = 4f;    // 3 m de vidro + 1 m de grade; acima disso a bola sai
    /// <summary>|y| a partir do qual a lateral é igual ao fundo (o canto: 2 m de vidro de 3 m em cada ponta).</summary>
    public const float InicioDoCanto = 8f;
    /// <summary>|y| em que começa o degrau do vidro (6 ≤ |y| &lt; 8): vidro até 2 m, grade de 2 a 3 m.</summary>
    public const float InicioDoDegrau = 6f;
    public const float AlturaDoVidroDoDegrau = 2f;
    public const float AlturaDoDegrau = 3f;
    /// <summary>Meio da lateral (|y| &lt; 6): só grade, até 3 m — menos nas portas.</summary>
    public const float AlturaDoMeio = 3f;
    /// <summary>Portas: uma em cada lateral de cada lado da rede, de |y| = 0,45 a 1,25 m, do chão até 2 m. A bola que passa por elas sai.</summary>
    public const float InicioDaPorta = 0.45f;
    public const float FimDaPorta = 1.25f;
    public const float AlturaDaPorta = 2f;

    public static int LadoDe(float y) => y >= 0 ? 1 : -1;
    public static int LadoDoTime(int time) => time == 0 ? 1 : -1;
    public static bool Dentro(float x, float y) => MathF.Abs(x) <= MeiaLargura && MathF.Abs(y) <= MeioComprimento;

    /// <summary>Caixa de saque no lado dado, à direita ou à esquerda de quem está naquele lado (da rede até a linha de saque).</summary>
    public static Caixa CaixaDeSaque(int lado, bool direita)
    {
        int sinalX = direita ? lado : -lado;
        return new Caixa(
            XMin: sinalX > 0 ? 0 : -MeiaLargura,
            XMax: sinalX > 0 ? MeiaLargura : 0,
            YMin: lado > 0 ? 0 : -LinhaDeSaque,
            YMax: lado > 0 ? LinhaDeSaque : 0);
    }

    /// <summary>
    /// O que há na parede no ponto dado: Vidro, Grade ou Aberto (a porta, ou acima da altura daquele trecho — a bola sai).
    /// aoLongo é a coordenada ao longo da parede (y na lateral, x no fundo); as duas paredes de cada tipo são iguais.
    /// Nas emendas vale o trecho de baixo e o de dentro: z = 3 no fundo é vidro, |y| = 8 já é canto, |y| = 6 já é degrau.
    /// É a regra que a física usa; os <see cref="Paineis"/> desenham a mesma coisa (há teste que confere ponto a ponto).
    /// </summary>
    public static Superficie SuperficieDaParede(QualParede parede, float aoLongo, float z)
    {
        if (parede == QualParede.Fundo) return VidroGradeOuAberto(z, AlturaDoVidro, AlturaDaParede);
        float a = MathF.Abs(aoLongo);
        if (a >= InicioDoCanto) return VidroGradeOuAberto(z, AlturaDoVidro, AlturaDaParede);
        if (a >= InicioDoDegrau) return VidroGradeOuAberto(z, AlturaDoVidroDoDegrau, AlturaDoDegrau);
        if (NaPorta(aoLongo, z)) return Superficie.Aberto;
        return z <= AlturaDoMeio ? Superficie.Grade : Superficie.Aberto;
    }

    /// <summary>A bola nesse ponto da lateral passa pela porta (vale pras quatro: as portas são simétricas).</summary>
    // atalho: a porta é um vão sem batente e a bola é um ponto — na quina, ou passa inteira ou bate inteira na grade ao
    // lado; se o playtest pedir, modelar o batente (poste redondo) e o raio da bola aqui.
    internal static bool NaPorta(float aoLongo, float z)
    {
        float a = MathF.Abs(aoLongo);
        return a >= InicioDaPorta && a <= FimDaPorta && z < AlturaDaPorta;
    }

    private static Superficie VidroGradeOuAberto(float z, float alturaDoVidro, float alturaDaParede) =>
        z <= alturaDoVidro ? Superficie.Vidro : z <= alturaDaParede ? Superficie.Grade : Superficie.Aberto;

    /// <summary>
    /// Todos os painéis de vidro e de grade das quatro paredes, sem sobreposição; o que não está coberto (as portas e o
    /// que fica acima de cada trecho) é aberto. É o que o renderizador desenha — exatamente o que a física usa.
    /// </summary>
    public static IReadOnlyList<Painel> Paineis { get; } = MontarPaineis();

    private static IReadOnlyList<Painel> MontarPaineis()
    {
        var paineis = new List<Painel>();
        foreach (int sinal in new[] { 1, -1 })
        {
            // Fundo: a largura inteira.
            paineis.Add(new Painel(QualParede.Fundo, sinal, -MeiaLargura, MeiaLargura, 0, AlturaDoVidro, Superficie.Vidro));
            paineis.Add(new Painel(QualParede.Fundo, sinal, -MeiaLargura, MeiaLargura, AlturaDoVidro, AlturaDaParede, Superficie.Grade));
            // Lateral, de uma ponta à outra (y de -10 a 10): canto, degrau, meio com as duas portas, degrau, canto.
            foreach (int ponta in new[] { -1, 1 })
            {
                var (canto0, canto1) = Ordenar(ponta * InicioDoCanto, ponta * MeioComprimento);
                paineis.Add(new Painel(QualParede.Lateral, sinal, canto0, canto1, 0, AlturaDoVidro, Superficie.Vidro));
                paineis.Add(new Painel(QualParede.Lateral, sinal, canto0, canto1, AlturaDoVidro, AlturaDaParede, Superficie.Grade));
                var (degrau0, degrau1) = Ordenar(ponta * InicioDoDegrau, ponta * InicioDoCanto);
                paineis.Add(new Painel(QualParede.Lateral, sinal, degrau0, degrau1, 0, AlturaDoVidroDoDegrau, Superficie.Vidro));
                paineis.Add(new Painel(QualParede.Lateral, sinal, degrau0, degrau1, AlturaDoVidroDoDegrau, AlturaDoDegrau, Superficie.Grade));
                var (meio0, meio1) = Ordenar(ponta * FimDaPorta, ponta * InicioDoDegrau);
                paineis.Add(new Painel(QualParede.Lateral, sinal, meio0, meio1, 0, AlturaDoMeio, Superficie.Grade));
                var (porta0, porta1) = Ordenar(ponta * InicioDaPorta, ponta * FimDaPorta);
                paineis.Add(new Painel(QualParede.Lateral, sinal, porta0, porta1, AlturaDaPorta, AlturaDoMeio, Superficie.Grade));   // acima da porta
            }
            paineis.Add(new Painel(QualParede.Lateral, sinal, -InicioDaPorta, InicioDaPorta, 0, AlturaDoMeio, Superficie.Grade));   // entre as portas
        }
        return paineis.AsReadOnly();
    }

    private static (float, float) Ordenar(float a, float b) => a < b ? (a, b) : (b, a);
}

/// <summary>
/// Um retângulo de parede. Sinal diz qual das duas paredes do tipo (x = +5 ou -5 na lateral; y = +10 ou -10 no fundo);
/// Inicio e Fim são a coordenada ao longo dela (y na lateral, x no fundo), ZMin e ZMax a altura; Superficie é Vidro ou Grade.
/// </summary>
public sealed record Painel(QualParede Parede, int Sinal, float Inicio, float Fim, float ZMin, float ZMax, Superficie Superficie);

public readonly record struct Caixa(float XMin, float XMax, float YMin, float YMax)
{
    public bool Contem(float x, float y) => x >= XMin && x <= XMax && y >= YMin && y <= YMax;
    public float CentroX => (XMin + XMax) / 2;
    public float CentroY => (YMin + YMax) / 2;
}
