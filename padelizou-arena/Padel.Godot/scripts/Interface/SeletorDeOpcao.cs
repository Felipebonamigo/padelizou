using Godot;

namespace Padel.Godot.Interface;

/// <summary>
/// Linha de opção no jeito de console: "Dificuldade      ‹ Médio ›". Esquerda/direita (teclado, direcional
/// ou analógico) trocam o valor; Enter/A avança; o mouse clica na metade esquerda ou direita do valor.
/// É um Button pra herdar foco, estilo e navegação; o texto é desenhado aqui, com as setas feitas de triângulo
/// (a fonte padrão não tem ◀ ▶).
/// </summary>
public partial class SeletorDeOpcao : Button
{
    [Signal] public delegate void ValorMudouEventHandler(int indice);

    private const float LarguraDoValor = 300f;
    private const float TamanhoDaFonte = 20f;

    private string _rotulo = "";
    private string[] _valores = [""];
    private int _indice;
    private bool _analogicoEsquerda, _analogicoDireita;

    /// <summary>Volta do último pro primeiro (e vice-versa). Desligado pra escala, como o volume.</summary>
    public bool Circular { get; set; } = true;

    /// <summary>Desenha uma barra de 0 a 100 % embaixo do valor (volume).</summary>
    public bool MostrarBarra { get; set; }

    public int Indice => _indice;

    public SeletorDeOpcao()
    {
        CustomMinimumSize = new Vector2(0, 46);
        FocusMode = FocusModeEnum.All;
        Text = "";
        Pressed += () => Mudar(+1);
        FocusEntered += QueueRedraw;
        FocusExited += QueueRedraw;
        MouseExited += QueueRedraw;
        TemaPadelizou.FocoSegueMouse(this);
    }

    public void Definir(string rotulo, string[] valores, int indice)
    {
        _rotulo = rotulo;
        _valores = valores.Length > 0 ? valores : [""];
        _indice = Math.Clamp(indice, 0, _valores.Length - 1);
        QueueRedraw();
    }

    /// <summary>Muda o valor sem avisar (pra sincronizar com a Configuracao).</summary>
    public void Selecionar(int indice)
    {
        _indice = Math.Clamp(indice, 0, _valores.Length - 1);
        QueueRedraw();
    }

    private void Mudar(int passo)
    {
        int n = _valores.Length;
        int novo = Circular ? ((_indice + passo) % n + n) % n : Math.Clamp(_indice + passo, 0, n - 1);
        if (novo == _indice) return;
        _indice = novo;
        QueueRedraw();
        EmitSignal(SignalName.ValorMudou, _indice);
    }

    public override void _GuiInput(InputEvent evento)
    {
        // Clique: metade esquerda do valor volta, o resto avança. Aceitar aqui evita o Pressed (+1) em dobro.
        if (evento is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } clique)
        {
            float inicioDoValor = Size.X - LarguraDoValor;
            if (clique.Position.X >= inicioDoValor)
            {
                Mudar(clique.Position.X < inicioDoValor + LarguraDoValor / 2 ? -1 : +1);
                AcceptEvent();
            }
            return;
        }

        // Analógico: manda um evento a cada tremida do eixo; conta só a passagem de solto pra inclinado.
        if (evento is InputEventJoypadMotion)
        {
            if (evento.IsAction("ui_left")) AnalogicoPara(ref _analogicoEsquerda, evento.IsActionPressed("ui_left"), -1);
            if (evento.IsAction("ui_right")) AnalogicoPara(ref _analogicoDireita, evento.IsActionPressed("ui_right"), +1);
            if (evento.IsAction("ui_left") || evento.IsAction("ui_right")) AcceptEvent();
            return;
        }

        // Tecla e direcional: segurar repete (eco).
        if (evento.IsActionPressed("ui_left", allowEcho: true)) { Mudar(-1); AcceptEvent(); }
        else if (evento.IsActionPressed("ui_right", allowEcho: true)) { Mudar(+1); AcceptEvent(); }
        else if (evento.IsAction("ui_left") || evento.IsAction("ui_right")) AcceptEvent();   // a soltura também não navega
    }

    private void AnalogicoPara(ref bool inclinado, bool agora, int passo)
    {
        if (agora && !inclinado) Mudar(passo);
        inclinado = agora;
    }

    public override void _Draw()
    {
        // O Button já desenhou o fundo (normal, foco ou hover); aqui vão o rótulo, o valor e as setas.
        bool emDestaque = HasFocus() || IsHovered();
        var corDoRotulo = emDestaque ? TemaPadelizou.Marinho : TemaPadelizou.Branco;
        var corDoValor = emDestaque ? TemaPadelizou.Marinho : TemaPadelizou.Lima;
        var fonte = TemaPadelizou.Forte;
        int tamanho = (int)TamanhoDaFonte;

        float linhaBase = (Size.Y - fonte.GetHeight(tamanho)) / 2 + fonte.GetAscent(tamanho);
        if (MostrarBarra) linhaBase -= 3;
        DrawString(fonte, new Vector2(24, linhaBase), _rotulo, HorizontalAlignment.Left, Size.X - LarguraDoValor - 32, tamanho, corDoRotulo);

        float inicio = Size.X - LarguraDoValor;
        DrawString(fonte, new Vector2(inicio + 28, linhaBase), _valores[_indice], HorizontalAlignment.Center, LarguraDoValor - 56 - 16, tamanho, corDoValor);

        float meio = MostrarBarra ? Size.Y / 2 - 3 : Size.Y / 2;
        bool temAnterior = Circular || _indice > 0;
        bool temProximo = Circular || _indice < _valores.Length - 1;
        Triangulo(new Vector2(inicio + 16, meio), -1, new Color(corDoValor, temAnterior ? 1f : 0.25f));
        Triangulo(new Vector2(Size.X - 32, meio), +1, new Color(corDoValor, temProximo ? 1f : 0.25f));

        if (MostrarBarra && _valores.Length > 1)
        {
            var trilho = new Rect2(inicio + 40, Size.Y - 9, LarguraDoValor - 96, 3);
            DrawRect(trilho, new Color(corDoValor, 0.25f));
            DrawRect(new Rect2(trilho.Position, new Vector2(trilho.Size.X * _indice / (_valores.Length - 1), trilho.Size.Y)), corDoValor);
        }
    }

    private void Triangulo(Vector2 centro, int sentido, Color cor)
    {
        const float meiaAltura = 8f, largura = 10f;
        var ponta = centro + new Vector2(sentido * largura / 2, 0);
        var baseCima = centro + new Vector2(-sentido * largura / 2, -meiaAltura);
        var baseBaixo = centro + new Vector2(-sentido * largura / 2, meiaAltura);
        DrawColoredPolygon([ponta, baseCima, baseBaixo], cor);
    }
}
