using Godot;

namespace Padel.Godot;

/// <summary>Câmera de TV atrás do time da casa; segue a bola de leve no eixo x, com a rede sempre visível.</summary>
public partial class CameraNode : Camera3D
{
    [Export] public float Altura = 7.5f;
    [Export] public float Recuo = 17.5f;
    [Export] public float SeguirBola = 0.25f;

    private float _xAlvo;

    /// <summary>Câmera do replay: de lado, fora do vidro, na altura do jogo, acompanhando a bola ao longo da quadra.</summary>
    public void Replay(float bolaX, float bolaY, float bolaZ, float delta)
    {
        // Alta e afastada da lateral, como a câmera de trilho das transmissões: vê o vidro, a rede e os dois
        // jogadores do lance sem coluna na frente. O z acompanha a bola só em parte, pra não chacoalhar.
        float z = Mathf.Clamp(bolaY * 0.5f, -5f, 5f);   // Core y é Godot z
        var alvo = new Vector3(12.5f, 4.6f, z);
        Position = Position.Lerp(alvo, Mathf.Clamp(delta * 2.5f, 0, 1));
        LookAt(new Vector3(Mathf.Clamp(bolaX, -3, 3) * 0.3f, Mathf.Clamp(bolaZ, 0.5f, 2.5f) * 0.5f, z * 1.2f), Vector3.Up);
    }

    public void Seguir(float bolaX, float delta)
    {
        _xAlvo = Mathf.Clamp(bolaX * SeguirBola, -1.2f, 1.2f);
        var alvo = new Vector3(_xAlvo, Altura, Recuo);
        Position = Position.Lerp(alvo, Mathf.Clamp(delta * 3f, 0, 1));
        LookAt(new Vector3(_xAlvo * 0.5f, 0.6f, -1.5f), Vector3.Up);
    }
}
