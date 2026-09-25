using Godot;

namespace Padel.Godot.Interface;

/// <summary>
/// Um círculo desenhado — a bola da marca de saque, a luz do ping. A fonte padrão não tem "●",
/// então o que é bolinha na interface passa por aqui.
/// </summary>
public partial class Bolinha : Control
{
    private Color _cor = TemaPadelizou.Lima;
    private bool _acesa = true;
    private float _raio = 6f;

    public Color Cor { get => _cor; set { _cor = value; QueueRedraw(); } }
    public bool Acesa { get => _acesa; set { _acesa = value; QueueRedraw(); } }
    public float Raio { get => _raio; set { _raio = value; UpdateMinimumSize(); QueueRedraw(); } }

    public Bolinha() => MouseFilter = MouseFilterEnum.Ignore;

    public override Vector2 _GetMinimumSize() => new(_raio * 2 + 2, _raio * 2 + 2);

    public override void _Draw()
    {
        if (!_acesa) return;
        var centro = Size / 2;
        DrawCircle(centro, _raio, _cor, filled: true, antialiased: true);
        // Um brilho em cima, pra ler como bola e não como ponto.
        DrawCircle(centro + new Vector2(-_raio * 0.3f, -_raio * 0.3f), _raio * 0.35f, new Color(1, 1, 1, 0.35f), filled: true, antialiased: true);
    }
}
