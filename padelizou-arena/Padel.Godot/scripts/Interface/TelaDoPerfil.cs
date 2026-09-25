using System.Globalization;
using Godot;
using Padel.Core.Perfil;

namespace Padel.Godot.Interface;

/// <summary>
/// A tela "Perfil" do menu: os números do jogador e as 20 conquistas (docs/CONQUISTAS.md). Secreta não desbloqueada
/// aparece como "???"; cumulativa ainda não desbloqueada mostra o progresso (42/100). Só lê o perfil — quem grava é o
/// <see cref="PerfilLocal"/>.
/// </summary>
public partial class TelaDoPerfil : VBoxContainer
{
    public event Action? VoltarPedido;

    private readonly Label _titulo;
    private readonly Label _numeros;
    private readonly GridContainer _grade;
    private readonly ScrollContainer _rolagem;

    public Button Voltar { get; }

    public TelaDoPerfil()
    {
        AddThemeConstantOverride("separation", 10);
        AddChild(TemaPadelizou.Rotulo("SEU JOGO", TemaPadelizou.Secao));
        _titulo = TemaPadelizou.Rotulo("", TemaPadelizou.TituloDaTela);
        AddChild(_titulo);
        _numeros = TemaPadelizou.Rotulo("", TemaPadelizou.Subtitulo);
        _numeros.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        AddChild(_numeros);
        AddChild(TemaPadelizou.Espaco(4));

        _rolagem = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(_rolagem);
        _grade = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _grade.AddThemeConstantOverride("h_separation", 28);
        _grade.AddThemeConstantOverride("v_separation", 10);
        _rolagem.AddChild(_grade);

        Voltar = new Button { Text = "Voltar", SizeFlagsHorizontal = SizeFlags.ShrinkBegin, CustomMinimumSize = new Vector2(220, 0) };
        Voltar.AddThemeFontSizeOverride("font_size", 24);
        Voltar.Pressed += () => VoltarPedido?.Invoke();
        TemaPadelizou.FocoSegueMouse(Voltar);
        AddChild(Voltar);
    }

    /// <summary>
    /// Cima/baixo (setas, direcional, analógico) rolam a lista: o único foco da tela é o Voltar, então sem isto quem joga
    /// de controle não veria as últimas conquistas.
    /// </summary>
    public override void _UnhandledInput(InputEvent evento)
    {
        if (!IsVisibleInTree()) return;
        int passo = evento.IsActionPressed("ui_down", allowEcho: true) ? 1 : evento.IsActionPressed("ui_up", allowEcho: true) ? -1 : 0;
        if (passo == 0) return;
        Rolar(passo);
        GetViewport().SetInputAsHandled();
    }

    /// <summary>Rola a lista um passo (-1 sobe, +1 desce).</summary>
    public void Rolar(int passo) => _rolagem.ScrollVertical += passo * 90;

    /// <summary>Quanto a lista está rolada (pixels).</summary>
    public int Rolagem => _rolagem.ScrollVertical;

    /// <summary>Quantas conquistas a grade mostra (uma linha por conquista do catálogo).</summary>
    public int Linhas => _grade.GetChildCount();

    public void Preencher(PerfilDoJogador perfil)
    {
        _titulo.Text = perfil.Nome;
        var tempo = TimeSpan.FromSeconds(perfil.SegundosDeJogo);
        int desbloqueadas = CatalogoDeConquistas.Todas.Count(c => perfil.Conquistas.ContainsKey(c.Id));
        _numeros.Text = string.Create(CultureInfo.InvariantCulture,
            $"{perfil.Partidas} partidas · {perfil.Vitorias} vitórias · maior rally de {perfil.MaiorRally} golpes · " +
            $"{(int)tempo.TotalHours}h{tempo.Minutes:00} de jogo · {desbloqueadas}/{CatalogoDeConquistas.Todas.Count} conquistas");

        foreach (var filho in _grade.GetChildren()) { _grade.RemoveChild(filho); filho.QueueFree(); }
        foreach (var c in CatalogoDeConquistas.Todas) _grade.AddChild(Linha(perfil, c));
    }

    private static VBoxContainer Linha(PerfilDoJogador perfil, Conquista c)
    {
        bool tem = perfil.Conquistas.ContainsKey(c.Id);
        bool escondida = c.Secreta && !tem;
        string descricao = escondida ? "Conquista secreta." : c.Descricao.Portugues;
        if (!tem && c.Estatistica is string estatistica)
            descricao += string.Create(CultureInfo.InvariantCulture, $" ({Math.Min(CatalogoDeConquistas.ValorDaEstatistica(perfil, estatistica), c.Meta)}/{c.Meta})");

        var linha = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(400, 0) };
        linha.AddThemeConstantOverride("separation", 0);
        var nome = TemaPadelizou.Rotulo(escondida ? "???" : c.Nome.Portugues);
        nome.AddThemeColorOverride("font_color", tem ? TemaPadelizou.Lima : TemaPadelizou.TextoSuave);
        if (tem) nome.AddThemeFontOverride("font", TemaPadelizou.Forte);
        linha.AddChild(nome);
        var texto = TemaPadelizou.Rotulo(descricao, TemaPadelizou.Descricao);
        texto.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        texto.AddThemeFontSizeOverride("font_size", 16);
        linha.AddChild(texto);
        return linha;
    }

    /// <summary>Todos os textos da tela, na ordem (pra conferência).</summary>
    public List<string> Textos()
    {
        var textos = new List<string>();
        void Juntar(Node no)
        {
            if (no is Label rotulo) textos.Add(rotulo.Text);
            foreach (var filho in no.GetChildren()) Juntar(filho);
        }
        Juntar(this);
        return textos;
    }
}
