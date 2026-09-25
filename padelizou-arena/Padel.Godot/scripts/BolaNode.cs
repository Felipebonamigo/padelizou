using Godot;
using Padel.Core;

namespace Padel.Godot;

public partial class BolaNode : Node3D
{
    private MeshInstance3D _esfera = null!;
    private MeshInstance3D _sombra = null!;

    public override void _Ready()
    {
        _esfera = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.06f, Height = 0.12f },   // maior que os 3,5 cm reais, pra ler na câmera de TV
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 1f, 0.25f), Roughness = 0.6f },
        };
        AddChild(_esfera);
        _sombra = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.09f, Height = 0.004f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0, 0, 0, 0.45f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha },
        };
        AddChild(_sombra);
    }

    public void Atualizar(RetratoDaBola bola)
    {
        _esfera.Position = Coordenadas.ParaGodot(bola.X, bola.Y, bola.Z + 0.06f);
        _sombra.Position = Coordenadas.NoChao(bola.X, bola.Y) + new Vector3(0, 0.003f, 0);
        float escala = Mathf.Clamp(1f - bola.Z / 8f, 0.3f, 1f);
        _sombra.Scale = new Vector3(escala, 1, escala);
    }
}
