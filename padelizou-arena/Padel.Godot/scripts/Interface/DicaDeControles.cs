using Godot;

namespace Padel.Godot.Interface;

/// <summary>Uma dica de rodapé: a tecla do teclado, o botão do controle e o que eles fazem.</summary>
public readonly record struct Dica(string Teclado, string Controle, string Acao);

/// <summary>
/// Rodapé "[Enter] escolher  [Esc] voltar" que troca pra "[A] escolher  [B] voltar" quando a última mão
/// foi no controle — no Steam Deck já começa no controle.
/// </summary>
public partial class DicaDeControles : HBoxContainer
{
    private Dica[] _dicas = [];
    private bool _controle;

    public DicaDeControles()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", 22);
    }

    public override void _Ready()
    {
        _controle = Input.GetConnectedJoypads().Count > 0;
        Montar();
    }

    public void Definir(params Dica[] dicas)
    {
        _dicas = dicas;
        if (IsNodeReady()) Montar();
    }

    public override void _Input(InputEvent evento)
    {
        bool? controle = evento switch
        {
            InputEventJoypadButton => true,
            InputEventJoypadMotion movimento when Mathf.Abs(movimento.AxisValue) > 0.5f => true,
            InputEventKey or InputEventMouseButton => false,
            _ => null,
        };
        if (controle is bool agora && agora != _controle)
        {
            _controle = agora;
            Montar();
        }
    }

    private void Montar()
    {
        foreach (var filho in GetChildren())
        {
            RemoveChild(filho);
            filho.QueueFree();
        }
        foreach (var dica in _dicas)
        {
            var grupo = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            grupo.AddThemeConstantOverride("separation", 8);
            grupo.AddChild(new Label { Text = _controle ? dica.Controle : dica.Teclado, ThemeTypeVariation = TemaPadelizou.Tecla, VerticalAlignment = VerticalAlignment.Center });
            var acao = new Label { Text = dica.Acao, ThemeTypeVariation = TemaPadelizou.Descricao, VerticalAlignment = VerticalAlignment.Center };
            acao.AddThemeFontSizeOverride("font_size", 16);
            grupo.AddChild(acao);
            AddChild(grupo);
        }
    }
}
