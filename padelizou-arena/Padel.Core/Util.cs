namespace Padel.Core;

/// <summary>Aleatório com semente (mulberry32): a mesma semente reproduz a mesma partida.</summary>
public sealed class Aleatorio
{
    private uint _estado;

    public Aleatorio(uint semente) => _estado = semente == 0 ? 0x9e3779b9u : semente;

    /// <summary>Número em [0, 1).</summary>
    public float Proximo()
    {
        _estado += 0x6d2b79f5u;
        uint t = _estado;
        t = (t ^ (t >> 15)) * (t | 1u);
        t ^= t + (t ^ (t >> 7)) * (t | 61u);
        return ((t ^ (t >> 14)) >> 8) / 16777216f;
    }

    /// <summary>Box-Muller: média 0, desvio 1.</summary>
    public float Gaussiana()
    {
        float u = 0, v = 0;
        while (u == 0) u = Proximo();
        while (v == 0) v = Proximo();
        return MathF.Sqrt(-2 * MathF.Log(u)) * MathF.Cos(2 * MathF.PI * v);
    }
}

public static class Util
{
    public static float Limitar(float valor, float minimo, float maximo) => MathF.Min(maximo, MathF.Max(minimo, valor));
    public static float Distancia(float x1, float y1, float x2, float y2) => MathF.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
}
