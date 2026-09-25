using Godot;

namespace Padel.Godot;

/// <summary>Cena de teste do transporte ENet: "-- --conferir" roda a <see cref="ConferenciaDaRede"/> e fecha.</summary>
public partial class TesteRedeNode : Node
{
    public override void _Ready()
    {
        if (OS.GetCmdlineUserArgs().Contains("--conferir")) ConferenciaDaRede.RodarESair(this);
        else GD.Print("TesteRede: rode com \"-- --conferir\".");
    }
}
