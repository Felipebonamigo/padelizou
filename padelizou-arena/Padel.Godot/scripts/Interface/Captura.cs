using Godot;

namespace Padel.Godot.Interface;

/// <summary>"--screenshot ARQ.png" das cenas de interface: espera a animação assentar, salva a tela e fecha o jogo.</summary>
public static class Captura
{
    public static async void SalvarESair(Node no, string arquivo, double esperaEmSegundos = 0.8)
    {
        var arvore = no.GetTree();
        if (esperaEmSegundos > 0) await no.ToSignal(arvore.CreateTimer(esperaEmSegundos, processAlways: true), SceneTreeTimer.SignalName.Timeout);
        // Dois quadros desenhados: o que está na tela é o que foi montado, não um quadro pela metade.
        await no.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        await no.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var imagem = no.GetViewport().GetTexture().GetImage();
        var erro = imagem is null ? Error.Unavailable : imagem.SavePng(arquivo);
        GD.Print($"Screenshot {(erro == Error.Ok ? "salvo em" : "FALHOU: " + erro + " —")} {arquivo}");
        arvore.Quit(erro == Error.Ok ? 0 : 1);
    }
}
