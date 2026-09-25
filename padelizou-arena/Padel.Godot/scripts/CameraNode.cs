using Godot;

namespace Padel.Godot;

/// <summary>Câmera de TV atrás do time da casa; segue a bola de leve no eixo x, com a rede sempre visível.</summary>
public partial class CameraNode : Camera3D
{
    [Export] public float Altura = 7.5f;
    [Export] public float Recuo = 17.5f;
    [Export] public float SeguirBola = 0.25f;

    private float _xAlvo;

    public void Seguir(float bolaX, float delta)
    {
        _xAlvo = Mathf.Clamp(bolaX * SeguirBola, -1.2f, 1.2f);
        var alvo = new Vector3(_xAlvo, Altura, Recuo);
        Position = Position.Lerp(alvo, Mathf.Clamp(delta * 3f, 0, 1));
        LookAt(new Vector3(_xAlvo * 0.5f, 0.6f, -1.5f), Vector3.Up);
    }
}
