using Godot;
using Padel.Core;

namespace Padel.Godot;

/// <summary>
/// Mapa de entrada definido em código (idempotente), pra não depender da serialização do project.godot:
/// setas/WASD + analógico esquerdo movem; Espaço/Enter/botão A é a ação (sacar; balanço); Shift/L/botão B é o lob.
/// Devolve a Entrada no referencial do jogador: Dy &lt; 0 é "rumo à rede".
/// </summary>
public static class EntradaLocal
{
    public const string Esquerda = "mover_esquerda";
    public const string Direita = "mover_direita";
    public const string Frente = "mover_frente";
    public const string Tras = "mover_tras";
    public const string Acao = "acao";
    public const string Lob = "lob";
    public const string Pausa = "pausa";

    /// <summary>
    /// Monta o mapa do primeiro jogador (refaz a cada chamada: o InputMap é global e a partida anterior pode ter sido
    /// de outro modo). Sozinho, qualquer controle joga e o L também é lob. No coop, o controle 1 e o IJKL são do segundo
    /// jogador (<see cref="LerSegundo"/>): o primeiro fica com o controle 0 e perde o L, que é a "direita" do IJKL — sem
    /// isso, o segundo andar pra direita fazia o primeiro dar lob. A pausa é de todo mundo: Start de qualquer controle.
    /// </summary>
    public static void ConfigurarMapa(bool coop = false)
    {
        int controle = coop ? PrimeiroControle : QualquerControle;
        Definir(Esquerda, Tecla(Key.Left), Tecla(Key.A), Eixo(JoyAxis.LeftX, -1, controle));
        Definir(Direita, Tecla(Key.Right), Tecla(Key.D), Eixo(JoyAxis.LeftX, 1, controle));
        Definir(Frente, Tecla(Key.Up), Tecla(Key.W), Eixo(JoyAxis.LeftY, -1, controle));
        Definir(Tras, Tecla(Key.Down), Tecla(Key.S), Eixo(JoyAxis.LeftY, 1, controle));
        Definir(Acao, Tecla(Key.Space), Tecla(Key.Enter), Botao(JoyButton.A, controle));
        if (coop) Definir(Lob, Tecla(Key.Shift), Botao(JoyButton.B, controle));
        else Definir(Lob, Tecla(Key.Shift), Tecla(Key.L), Botao(JoyButton.B, controle));
        Definir(Pausa, Tecla(Key.Escape), Tecla(Key.P), Botao(JoyButton.Start, QualquerControle));
    }

    // Um InputEvent de controle novo nasce com Device = 0 (só o primeiro controle); -1 é "qualquer um"
    // (InputMap::ALL_DEVICES, que o C# não expõe) — o mesmo que TemaPadelizou usa no A/B da interface.
    private const int QualquerControle = -1;
    private const int PrimeiroControle = 0;

    private static void Definir(string acao, params InputEvent[] eventos)
    {
        if (InputMap.HasAction(acao)) InputMap.ActionEraseEvents(acao);
        else InputMap.AddAction(acao, deadzone: 0.2f);
        foreach (var e in eventos) InputMap.ActionAddEvent(acao, e);
    }

    private static InputEventKey Tecla(Key tecla) => new() { PhysicalKeycode = tecla };
    private static InputEventJoypadMotion Eixo(JoyAxis eixo, float valor, int controle) => new() { Axis = eixo, AxisValue = valor, Device = controle };
    private static InputEventJoypadButton Botao(JoyButton botao, int controle) => new() { ButtonIndex = botao, Device = controle };

    public static Entrada Ler()
    {
        var v = Input.GetVector(Esquerda, Direita, Frente, Tras);   // y negativo = frente
        return new Entrada(v.X, v.Y, Input.IsActionJustPressed(Acao), Input.IsActionPressed(Acao), Input.IsActionJustPressed(Lob));
    }

    // Segundo jogador local (coop no sofá): o segundo controle (dispositivo 1) ou, no teclado, IJKL + U (ação) e O (lob).
    // O primeiro jogador usa as ações do InputMap, que atendem o teclado principal e o controle 0.
    private const int ControleDoSegundo = 1;
    private static bool _acao2Antes, _lob2Antes;

    public static Entrada LerSegundo()
    {
        float dx = Input.GetJoyAxis(ControleDoSegundo, JoyAxis.LeftX);
        float dy = Input.GetJoyAxis(ControleDoSegundo, JoyAxis.LeftY);
        if (MathF.Sqrt(dx * dx + dy * dy) < 0.2f) { dx = 0; dy = 0; }   // zona morta, como a das ações
        if (Input.IsPhysicalKeyPressed(Key.J)) dx -= 1;
        if (Input.IsPhysicalKeyPressed(Key.L)) dx += 1;
        if (Input.IsPhysicalKeyPressed(Key.I)) dy -= 1;
        if (Input.IsPhysicalKeyPressed(Key.K)) dy += 1;
        float n = MathF.Sqrt(dx * dx + dy * dy);
        if (n > 1) { dx /= n; dy /= n; }
        bool acao = Input.IsJoyButtonPressed(ControleDoSegundo, JoyButton.A) || Input.IsPhysicalKeyPressed(Key.U);
        bool lob = Input.IsJoyButtonPressed(ControleDoSegundo, JoyButton.B) || Input.IsPhysicalKeyPressed(Key.O);
        bool acaoApertou = acao && !_acao2Antes, lobApertou = lob && !_lob2Antes;
        _acao2Antes = acao; _lob2Antes = lob;
        return new Entrada(dx, dy, acaoApertou, acao, lobApertou);
    }

    /// <summary>Entrada do n-ésimo jogador desta máquina (0 = teclado principal/controle 0; 1 = controle 1/IJKL).</summary>
    public static Entrada LerJogadorLocal(int n) => n == 0 ? Ler() : LerSegundo();
}
