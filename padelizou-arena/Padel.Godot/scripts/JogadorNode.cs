using Godot;
using Padel.Core;

namespace Padel.Godot;

/// <summary>Cápsula colorida por time com uma raquete; anel no chão pro jogador desta máquina. Lê só o Retrato.
/// O boneco articulado (scripts/Boneco) substitui a cápsula quando entrar.</summary>
public partial class JogadorNode : Node3D
{
    private static readonly Color Casa = new(0.64f, 0.85f, 0.15f);
    private static readonly Color Visitante = new(1f, 0.42f, 0.24f);

    private int _indice;
    private Node3D _corpo = null!;
    private Label3D _nome = null!;
    private MeshInstance3D _raquete = null!;

    public void Ligar(RetratoDoJogador jogador)
    {
        _indice = jogador.Indice;
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
        _raquete = raquete;
        if (jogador.Local)
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
            Modulate = jogador.Local ? Colors.White : new Color(1, 1, 1, 0.7f),
            NoDepthTest = true,
        };
        _corpo.AddChild(_nome);
        // Todo mundo olha pra rede.
        _corpo.RotationDegrees = new Vector3(0, jogador.Lado > 0 ? 0 : 180, 0);
    }

    public void Atualizar(RetratoDaPartida retrato, float delta)
    {
        var j = retrato.Jogadores[_indice];
        Position = Coordenadas.NoChao(j.X, j.Y);
        // Corpo vira pra onde anda; raquete gira durante o balanço (placeholder da animação de verdade).
        if (MathF.Sqrt(j.Vx * j.Vx + j.Vy * j.Vy) > 0.5f)
        {
            float alvo = Mathf.Atan2(j.Vx, j.Vy) + Mathf.Pi;   // Godot olha pra -Z; Core y é Godot z
            _corpo.Rotation = new Vector3(0, Mathf.LerpAngle(_corpo.Rotation.Y, alvo, 0.2f), 0);
        }
        // Fase do golpe: o balanço manual do humano, ou os 0,3 s depois de um golpe da IA (que não tem balanço).
        float fase = j.FaseDoBalanco;
        float desdeOGolpe = retrato.TempoDeJogo - j.InstanteDoUltimoGolpe;
        if (fase <= 0 && desdeOGolpe is >= 0 and < Jogador.DuracaoDoBalanco) fase = 0.4f + 0.6f * desdeOGolpe / Jogador.DuracaoDoBalanco;
        _raquete.RotationDegrees = new Vector3(90, 0, fase > 0 ? -70 + 140 * fase : 0);
        _raquete.Position = new Vector3(0.42f, 0.95f + (j.BalancoDeLob && fase > 0 ? 0.3f * fase : 0), -0.15f * j.Lado);
    }
}
