using Godot;
using Padel.Godot.Interface;

namespace Padel.Godot;

/// <summary>
/// Vitrine dos modelos baixados da comunidade (cenas/VitrineDeModelos.tscn). Não entra no jogo: serve pro Felipe
/// escolher pelo olho antes de qualquer modelo trocar uma primitiva. "-- --screenshot ARQ.png" salva a tela e fecha.
/// </summary>
public partial class VitrineNode : Node3D
{
    public override void _Ready()
    {
        var args = OS.GetCmdlineUserArgs();
        int i = System.Array.IndexOf(args, "--screenshot");
        if (i >= 0 && i + 1 < args.Length) Captura.SalvarESair(this, args[i + 1]);
    }
}
