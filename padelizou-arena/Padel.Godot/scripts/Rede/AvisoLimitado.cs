namespace Padel.Godot;

/// <summary>
/// Um aviso que não inunda o log: a primeira ocorrência avisa na hora; as seguintes, no máximo uma vez por intervalo,
/// dizendo quantas ficaram caladas desde o aviso anterior. Todas contam (<see cref="Ocorrencias"/> vai pro
/// ResumoParaLog das sessões). O relógio é de quem chama, em ms — é o que deixa a ConferenciaDaRede controlar o tempo.
/// </summary>
public sealed class AvisoLimitado(string oQue, long intervaloMs)
{
    private long _proximoAvisoEm;
    private long _caladas;

    public long Ocorrencias { get; private set; }

    /// <summary>Conta uma ocorrência; devolve o texto do aviso quando é hora de avisar, ou null.</summary>
    public string? Registrar(long agoraMs)
    {
        Ocorrencias++;
        if (Ocorrencias > 1 && agoraMs < _proximoAvisoEm)
        {
            _caladas++;
            return null;
        }
        string texto = _caladas > 0 ? $"{oQue} (mais {_caladas} desde o último aviso; {Ocorrencias} no total)" : oQue;
        _caladas = 0;
        _proximoAvisoEm = agoraMs + intervaloMs;
        return texto;
    }
}
