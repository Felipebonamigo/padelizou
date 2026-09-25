using Godot;
using Padel.Core;

namespace Padel.Godot;

/// <summary>
/// Mapa de entrada definido em código (idempotente), pra não depender da serialização do project.godot:
/// setas/WASD + analógico esquerdo movem; Espaço/Enter/botão A é a ação (sacar; segurado, lob).
/// Devolve a Entrada no referencial do jogador: Dy &lt; 0 é "rumo à rede".
/// </summary>
public static class EntradaLocal
{
    public const string Esquerda = "mover_esquerda";
    public const string Direita = "mover_direita";
    public const string Frente = "mover_frente";
    public const string Tras = "mover_tras";
    public const string Acao = "acao";
    public const string Pausa = "pausa";

    public static void ConfigurarMapa()
    {
        Definir(Esquerda, Tecla(Key.Left), Tecla(Key.A), Eixo(JoyAxis.LeftX, -1));
        Definir(Direita, Tecla(Key.Right), Tecla(Key.D), Eixo(JoyAxis.LeftX, 1));
        Definir(Frente, Tecla(Key.Up), Tecla(Key.W), Eixo(JoyAxis.LeftY, -1));
        Definir(Tras, Tecla(Key.Down), Tecla(Key.S), Eixo(JoyAxis.LeftY, 1));
        Definir(Acao, Tecla(Key.Space), Tecla(Key.Enter), Botao(JoyButton.A));
        Definir(Pausa, Tecla(Key.Escape), Tecla(Key.P), Botao(JoyButton.Start));
    }

    private static void Definir(string acao, params InputEvent[] eventos)
    {
        if (InputMap.HasAction(acao)) return;
        InputMap.AddAction(acao, deadzone: 0.2f);
        foreach (var e in eventos) InputMap.ActionAddEvent(acao, e);
    }

    private static InputEventKey Tecla(Key tecla) => new() { PhysicalKeycode = tecla };
    private static InputEventJoypadMotion Eixo(JoyAxis eixo, float valor) => new() { Axis = eixo, AxisValue = valor };
    private static InputEventJoypadButton Botao(JoyButton botao) => new() { ButtonIndex = botao };

    public static Entrada Ler()
    {
        var v = Input.GetVector(Esquerda, Direita, Frente, Tras);   // y negativo = frente
        return new Entrada(v.X, v.Y, Input.IsActionJustPressed(Acao), Input.IsActionPressed(Acao));
    }
}
