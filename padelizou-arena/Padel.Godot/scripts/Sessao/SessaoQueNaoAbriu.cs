using Padel.Core;

namespace Padel.Godot;

/// <summary>
/// A sala não abriu (porta ocupada, endereço que não resolve): nada roda, e a tela de fim diz por quê. Sem ela a
/// exceção do transporte nascia no _Ready da partida, nada era montado e o jogo estourava a cada quadro.
/// </summary>
public sealed class SessaoQueNaoAbriu : ISessao
{
    private readonly string _motivo;

    public SessaoQueNaoAbriu(string motivo)
    {
        _motivo = motivo;
        Retrato.Mensagem = motivo;
        Retrato.MensagemEmDestaque = true;
    }

    public IReadOnlyList<int> JogadoresLocais => [];
    public RetratoDaPartida Retrato { get; } = new();
    public bool Acabou => true;
    public string? MotivoDoFim => _motivo;
    public string ResumoParaLog() => $"sala não abriu: {_motivo}";
    public void Avancar(double delta, ReadOnlySpan<Entrada> entradas) { }
    public EstadoVisivel? EstadoParaOBot() => null;
    public void Dispose() { }
}
