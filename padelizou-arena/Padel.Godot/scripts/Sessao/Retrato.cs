using Padel.Core;

namespace Padel.Godot;

/// <summary>
/// Tudo o que a tela precisa pra desenhar e tocar som num quadro, venha a partida de onde vier:
/// rodando aqui (SessaoLocal), hospedada aqui pra outros (host) ou recebida pela rede (cliente).
/// Os nós de tela leem SÓ isto — nunca a Partida — e é isso que deixa o mesmo desenho servir aos três.
/// É mutável e reaproveitado a cada quadro (sem lixo pro coletor a 120 Hz).
/// </summary>
public sealed class RetratoDaPartida
{
    public float TempoDeJogo;
    public EstadoDaPartida Estado;
    public readonly RetratoDaBola Bola = new();
    public readonly RetratoDoJogador[] Jogadores = [new(), new(), new(), new()];
    public readonly RetratoDoPlacar Placar = new();
    public string Mensagem = "";
    public bool MensagemEmDestaque;
    public bool MensagemSuave;
    public Caixa? CaixaDoSaque;
    /// <summary>Latência até o host, em ms (null quando a partida roda aqui).</summary>
    public int? PingMs;
    /// <summary>Acontecimentos desde o quadro anterior (golpe, quique, parede, ponto…), pra som e animação. Quem desenha esvazia.</summary>
    public readonly List<Acontecimento> Acontecimentos = [];
}

public sealed class RetratoDaBola
{
    public float X, Y, Z;
    public bool EmJogo;
}

public sealed class RetratoDoJogador
{
    public int Indice, Time, Lado;
    public string Nome = "";
    public float X, Y, Vx, Vy;
    /// <summary>Controlado por alguém (humano), aqui ou do outro lado da rede.</summary>
    public bool Humano;
    /// <summary>Controlado nesta máquina — ganha o anel no chão.</summary>
    public bool Local;
    public bool Destro = true;
    /// <summary>0 = sem balanço; 0..1 = do aperto até o fim do balanço manual.</summary>
    public float FaseDoBalanco;
    public bool BalancoDeLob;
    public TipoDeGolpe? UltimoGolpe;
    public float InstanteDoUltimoGolpe = float.NegativeInfinity;
}

public sealed class RetratoDoPlacar
{
    public readonly int[] Sets = new int[2];
    public readonly int[] Games = new int[2];
    public readonly string[] Pontos = ["0", "0"];
    public readonly List<SetEncerrado> SetsAnteriores = [];
    public int TimeSacando;
    public bool EmTieBreak;
    public bool EmPontoDecisivo;
    public int? Vencedor;
    public string Resumo = "";
}

/// <summary>Um acontecimento da partida com o lugar onde aconteceu (pra som 3D e efeito).</summary>
public readonly record struct Acontecimento(TipoDeEventoDaPartida Tipo, int Jogador, int Time, TipoDeGolpe? Golpe, Motivo? Motivo, float X, float Y, float Z);
