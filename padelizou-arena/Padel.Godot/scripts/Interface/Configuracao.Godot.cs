using Godot;

namespace Padel.Godot.Interface;

/// <summary>A parte da <see cref="Configuracao"/> que depende do Godot: o arquivo em user:// e o volume.</summary>
public static partial class Configuracao
{
    public const string ArquivoDoUsuario = "user://configuracao.json";

    public static string CaminhoDoArquivo => ProjectSettings.GlobalizePath(ArquivoDoUsuario);

    /// <summary>Lê user://configuracao.json; ausente ou corrompido volta ao padrão e segue (com aviso no log, se corrompido).</summary>
    public static ResultadoDaCarga Carregar()
    {
        var resultado = Carregar(CaminhoDoArquivo);
        if (resultado == ResultadoDaCarga.Corrompido)
            GD.PushWarning($"Configuração em {CaminhoDoArquivo} ilegível — voltando ao padrão. O arquivo é reescrito na próxima vez que salvar.");
        return resultado;
    }

    /// <summary>Grava user://configuracao.json. Falha vira aviso no log: perder a preferência não pode derrubar o jogo.</summary>
    public static bool Salvar()
    {
        bool ok = Salvar(CaminhoDoArquivo, out var erro);
        if (!ok) GD.PushWarning($"Não consegui salvar a configuração em {CaminhoDoArquivo}: {erro}");
        return ok;
    }

    /// <summary>Aplica <see cref="Volume"/> no barramento Master (0 é mudo).</summary>
    public static void AplicarVolume()
    {
        int master = AudioServer.GetBusIndex("Master");
        if (master < 0) return;
        AudioServer.SetBusMute(master, Volume <= 0f);
        AudioServer.SetBusVolumeDb(master, Volume <= 0f ? -80f : Mathf.LinearToDb(Volume));
    }
}
