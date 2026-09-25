using Godot;
using Padel.Core;
using Padel.Core.Torneio;

namespace Padel.Godot;

/// <summary>
/// A carreira em andamento, entre a tela de carreira e a partida: o circuito do Padel.Core.Torneio, salvo em
/// user://carreira.json a cada mudança, e o jogo que a próxima partida decide. A lógica é toda do Core; aqui só se
/// guarda, se carrega e se traduz força de dupla em dificuldade de IA.
/// </summary>
public static class EstadoDaCarreira
{
    public const string Arquivo = "user://carreira.json";
    public const int ForcaDaDuplaDoJogador = 60;

    public static Carreira? Atual { get; private set; }
    /// <summary>O jogo da etapa que a partida em curso decide; null = partida avulsa.</summary>
    public static JogoDoTorneio? JogoEmDisputa { get; set; }
    /// <summary>Modo de teste: a tela aperta "Jogar" sozinha e a partida volta pra ela sozinha (humano simulado).</summary>
    public static bool Automatico { get; set; }

    public static string Caminho => ProjectSettings.GlobalizePath(Arquivo);

    public static bool Carregar()
    {
        try
        {
            if (!File.Exists(Caminho)) return false;
            Atual = Carreira.Carregar(File.ReadAllText(Caminho));
            return true;
        }
        catch (Exception erro) when (erro is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            // Arquivo de outra versão ou estragado: não derruba o jogo; a tela oferece uma carreira nova.
            GD.PushWarning($"Carreira em {Caminho} não abriu ({erro.Message}). Comece outra.");
            Atual = null;
            return false;
        }
    }

    public static void Nova(string nomeDaDupla, uint semente)
    {
        Atual = Carreira.Nova(new DuplaParticipante(nomeDaDupla, ForcaDaDuplaDoJogador, humana: true), semente);
        Salvar();
    }

    public static void Salvar()
    {
        if (Atual is null) return;
        try { File.WriteAllText(Caminho, Atual.Salvar()); }
        catch (Exception erro) when (erro is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"Não consegui salvar a carreira em {Caminho}: {erro.Message}");
        }
    }

    /// <summary>O próximo jogo da dupla do jogador na etapa em andamento, andando as rodadas das IAs até ele (ou até a etapa acabar).</summary>
    public static JogoDoTorneio? ProximoJogoDoJogador()
    {
        if (Atual?.EtapaEmAndamento is not TorneioDeDuplas torneio) return null;
        string eu = Atual.DuplaDoJogador.Nome;
        for (int guarda = 0; guarda < 64 && !torneio.Encerrado; guarda++)
        {
            var meu = torneio.JogosPendentes.FirstOrDefault(j => j.Envolve(eu));
            if (meu is not null) return meu;
            if (!torneio.AvancarRodada()) break;
        }
        Salvar();
        return null;
    }

    /// <summary>Guarda o resultado da partida jogada de verdade (a dupla do jogador foi o time 0).</summary>
    public static void InformarResultado(Placar placar)
    {
        if (Atual?.EtapaEmAndamento is not TorneioDeDuplas torneio || JogoEmDisputa is not JogoDoTorneio jogo) return;
        torneio.InformarResultado(jogo.Numero, placar, Atual.DuplaDoJogador.Nome);
        JogoEmDisputa = null;
        Salvar();
    }

    /// <summary>A força da dupla rival vira a dificuldade da IA na partida de verdade.</summary>
    public static Dificuldade DificuldadePara(int forcaDoRival) => forcaDoRival switch
    {
        < 50 => Dificuldade.Facil,
        < 75 => Dificuldade.Medio,
        _ => Dificuldade.Dificil,
    };

    /// <summary>Semente da partida de um jogo da carreira: mistura (splitmix32) da semente da carreira, da etapa e do número do jogo.</summary>
    public static uint SementeDoJogo(uint semente, int etapa, int numeroDoJogo)
    {
        uint x = semente ^ (uint)(etapa * 1000 + numeroDoJogo) * 0x9E3779B9u;
        x = (x ^ (x >> 16)) * 0x85EBCA6Bu;
        x = (x ^ (x >> 13)) * 0xC2B2AE35u;
        return x ^ (x >> 16);
    }

    /// <summary>A fase como a transmissão diz (o enum do Core é identificador, não texto de tela).</summary>
    public static string NomeDaFase(FaseAlcancada fase) => fase switch
    {
        FaseAlcancada.FaseDeGrupos => "fase de grupos",
        FaseAlcancada.PrimeiraRodada => "primeira rodada",
        FaseAlcancada.Oitavas => "oitavas",
        FaseAlcancada.Quartas => "quartas",
        FaseAlcancada.Semifinal => "semifinal",
        FaseAlcancada.Final => "final (vice)",
        FaseAlcancada.Campeao => "campeões",
        _ => fase.ToString(),
    };

    public static string NomeDaCategoria(CategoriaDaEtapa c) => c switch { CategoriaDaEtapa.Major => "Major", _ => c.ToString() };
}
