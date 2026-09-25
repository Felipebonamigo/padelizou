using Godot;

namespace Padel.Godot;

/// <summary>
/// O Core mede em (x = largura, y = comprimento, z = altura). No Godot o eixo pra cima é Y e a
/// profundidade é Z: a câmera fica atrás do time da casa (Core y &gt; 0), olhando pra -Z.
/// </summary>
public static class Coordenadas
{
    public static Vector3 ParaGodot(float x, float y, float z) => new(x, z, y);
    public static Vector3 NoChao(float x, float y) => new(x, 0, y);
}
