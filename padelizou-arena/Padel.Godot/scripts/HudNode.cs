using Godot;
using Padel.Core;

namespace Padel.Godot;

/// <summary>Placar e mensagem do ponto. Layout mínimo em código; a UI de verdade é do M3.</summary>
public partial class HudNode : CanvasLayer
{
    private Label _placar = null!;
    private Label _mensagem = null!;
    private Label _dica = null!;
    private string _ultimaMensagem = "";

    public override void _Ready()
    {
        var fundo = new PanelContainer { Position = new Vector2(16, 12) };
        var estilo = new StyleBoxFlat { BgColor = new Color(0.08f, 0.1f, 0.2f, 0.85f), CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8, ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 6, ContentMarginBottom = 6 };
        fundo.AddThemeStyleboxOverride("panel", estilo);
        _placar = new Label { Text = "" };
        _placar.AddThemeFontSizeOverride("font_size", 22);
        fundo.AddChild(_placar);
        AddChild(fundo);

        _mensagem = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AnchorLeft = 0, AnchorRight = 1, AnchorTop = 0.38f, AnchorBottom = 0.38f,
            Modulate = new Color(1, 1, 1, 0),
        };
        _mensagem.AddThemeFontSizeOverride("font_size", 34);
        _mensagem.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
        _mensagem.AddThemeConstantOverride("outline_size", 8);
        AddChild(_mensagem);

        _dica = new Label
        {
            Text = "Setas/WASD ou analógico movem · Espaço/A saca e balança (o timing decide) · Shift/B dá lob · ←/→ no golpe escolhem o canto · ↑ ataca, ↓ joga fundo",
            HorizontalAlignment = HorizontalAlignment.Center,
            AnchorLeft = 0, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1,
            OffsetTop = -34,
            Modulate = new Color(1, 1, 1, 0.7f),
        };
        _dica.AddThemeFontSizeOverride("font_size", 15);
        AddChild(_dica);
    }

    public void Atualizar(Partida partida)
    {
        var p = partida.Placar;
        string saque0 = p.Sacador.Time == 0 ? "●" : " ";
        string saque1 = p.Sacador.Time == 1 ? "●" : " ";
        _placar.Text = $"{saque0} Casa    {p.Sets[0]}  {p.Games[0]}  {p.TextoDosPontos(0),2}\n{saque1} Rivais  {p.Sets[1]}  {p.Games[1]}  {p.TextoDosPontos(1),2}";

        string texto = partida.Mensagem?.Texto ?? "";
        if (texto != _ultimaMensagem)
        {
            _ultimaMensagem = texto;
            _mensagem.Text = texto;
            _mensagem.Modulate = new Color(1, 1, 1, texto.Length > 0 ? 1 : 0);
            _mensagem.AddThemeColorOverride("font_color", partida.Mensagem?.Destaque == true ? new Color(0.64f, 0.85f, 0.15f) : Colors.White);
        }
    }
}
