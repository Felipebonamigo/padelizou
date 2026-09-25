using Padel.Core;

namespace Padel.Godot;

/// <summary>
/// Onde a partida roda. A tela chama Avancar a cada passo de física com as entradas de quem joga
/// nesta máquina e desenha o Retrato. Implementações: SessaoLocal (contra a IA, coop no sofá, demonstração),
/// SessaoHost e SessaoCliente (online, sobre o Padel.Core.Rede).
/// </summary>
public interface ISessao : IDisposable
{
    /// <summary>Índices (time*2 + índice) dos jogadores controlados nesta máquina, na ordem dos controles.</summary>
    IReadOnlyList<int> JogadoresLocais { get; }
    RetratoDaPartida Retrato { get; }
    bool Acabou { get; }
    /// <summary>Uma linha pro log (o CI procura "Saindo após" seguido disto).</summary>
    string ResumoParaLog();
    /// <summary>entradas é indexado pelo jogador (time*2 + índice); só as posições locais são lidas.</summary>
    void Avancar(double delta, ReadOnlySpan<Entrada> entradas);
    /// <summary>O que um jogador desta máquina vê — pro humano simulado (--bot). Null enquanto não há partida.</summary>
    EstadoVisivel? EstadoParaOBot();
    /// <summary>
    /// A sessão acabou sem partida de verdade — sala que não abriu, recusa do host, conexão que caiu: o que dizer ao
    /// jogador. Null quando a partida acabou (ou segue) normalmente.
    /// </summary>
    string? MotivoDoFim => null;
}
