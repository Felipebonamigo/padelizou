using Godot;
using Padel.Core;

namespace Padel.Godot;

/// <summary>Cápsula colorida por time com uma raquete; anel no chão pro humano. Animação de verdade entra no M1/M3.</summary>
public partial class JogadorNode : Node3D
{
    private static readonly Color Casa = new(0.64f, 0.85f, 0.15f);
    private static readonly Color Visitante = new(1f, 0.42f, 0.24f);

    private Jogador _jogador = null!;
    private Node3D _corpo = null!;
    private Label3D _nome = null!;

    public void Ligar(Jogador jogador)
    {
        _jogador = jogador;
        var cor = jogador.Time == 0 ? Casa : Visitante;
        _corpo = new Node3D();
        AddChild(_corpo);
        _corpo.AddChild(new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.28f, Height = 1.7f },
            Position = new Vector3(0, 0.85f, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = cor, Roughness = 0.7f },
        });
        _corpo.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.16f, Height = 0.32f },
            Position = new Vector3(0, 1.82f, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.85f, 0.65f, 0.5f) },
        });
        // Raquete: um disco na mão direita, apontando pra rede.
        var raquete = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.13f, BottomRadius = 0.13f, Height = 0.03f },
            Position = new Vector3(0.42f, 0.95f, -0.15f * jogador.Lado),
            RotationDegrees = new Vector3(90, 0, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.1f, 0.12f) },
        };
        _corpo.AddChild(raquete);
        if (jogador.Humano)
        {
            _corpo.AddChild(new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = 0.42f, OuterRadius = 0.5f },
                Position = new Vector3(0, 0.02f, 0),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = Colors.White, EmissionEnabled = true, Emission = Colors.White, EmissionEnergyMultiplier = 0.6f },
            });
        }
        _nome = new Label3D
        {
            Text = jogador.Nome.ToUpperInvariant(),
            FontSize = 48,
            PixelSize = 0.005f,
            Position = new Vector3(0, 2.25f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Modulate = jogador.Humano ? Colors.White : new Color(1, 1, 1, 0.7f),
            NoDepthTest = true,
        };
        _corpo.AddChild(_nome);
        // Todo mundo olha pra rede.
        _corpo.RotationDegrees = new Vector3(0, jogador.Lado > 0 ? 0 : 180, 0);
    }

    public void Atualizar()
    {
        Position = Coordenadas.NoChao(_jogador.X, _jogador.Y);
    }
}
