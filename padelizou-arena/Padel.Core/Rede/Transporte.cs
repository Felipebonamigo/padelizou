namespace Padel.Core.Rede;

/// <summary>Confiável: chega tudo, em ordem, uma vez (sala, início). Não confiável: pode perder, repetir e trocar a ordem (entradas, instantâneos).</summary>
public enum Canal : byte { Confiavel, NaoConfiavel }

public enum TipoDeEventoDoTransporte { Conectou, Desconectou, Pacote }

/// <summary>O que o transporte entrega: um par conectou, desconectou, ou mandou um pacote (Dados) por um canal.</summary>
public readonly record struct EventoDoTransporte(TipoDeEventoDoTransporte Tipo, int Par, Canal Canal = Canal.Confiavel, ReadOnlyMemory<byte> Dados = default);

/// <summary>
/// O transporte visto pelo jogo (D5): ENet no desenvolvimento, Steam Datagram Relay no lançamento, memória nos
/// testes. Par é um inteiro que o transporte dá a cada conexão. Nada chega sem <see cref="Processar"/>: é ele
/// que bombeia a rede e enfileira os eventos que <see cref="TentarReceber"/> devolve, na ordem em que chegaram.
/// Como conectar (endereço, SteamId) é de cada implementação e fica fora daqui.
/// </summary>
public interface ITransporte
{
    /// <summary>Copia os dados; par desconhecido ou desconectado é ignorado sem erro (a rede é assim).</summary>
    void Enviar(int par, Canal canal, ReadOnlySpan<byte> dados);
    void Desconectar(int par);
    void Processar();
    bool TentarReceber(out EventoDoTransporte evento);
}
