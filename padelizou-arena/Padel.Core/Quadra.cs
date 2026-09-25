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
    public const float AlturaDoVidro = 3f;
    public const float AlturaDaParede = 4f;    // 3 m de vidro + 1 m de grade; acima disso a bola sai

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
}

public readonly record struct Caixa(float XMin, float XMax, float YMin, float YMax)
{
    public bool Contem(float x, float y) => x >= XMin && x <= XMax && y >= YMin && y <= YMax;
    public float CentroX => (XMin + XMax) / 2;
    public float CentroY => (YMin + YMax) / 2;
}
