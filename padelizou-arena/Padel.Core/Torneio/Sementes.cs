namespace Padel.Core.Torneio;

/// <summary>
/// De onde sai o acaso do torneio. Uma semente por torneio; cada jogo e cada sorteio derivam
/// dela por conta, e não pela ORDEM em que alguém pediu número — assim o resultado do jogo 7
/// não muda se o humano informar o dele antes ou depois, e o torneio salvo continua o mesmo.
/// </summary>
internal static class Sementes
{
    /// <summary>A semente de uma parte do torneio (um jogo, uma etapa). O misturador do MurmurHash3.</summary>
    public static uint Misturar(uint semente, uint parte)
    {
        uint h = semente ^ (parte * 0x9E3779B1u);
        h ^= h >> 16;
        h *= 0x85EBCA6Bu;
        h ^= h >> 13;
        h *= 0xC2B2AE35u;
        h ^= h >> 16;
        return h;
    }

    /// <summary>
    /// O sorteio estável de uma dupla — espelho do <c>ClassificacaoDeGrupos.Sorteio</c> do
    /// Padelizou: FNV-1a escrito à mão, e não <c>string.GetHashCode()</c>, que o .NET aleatoriza
    /// por processo (o mesmo empate daria uma resposta antes e outra depois de reabrir o jogo).
    /// Lá o hash é do Id da dupla, que muda a cada torneio; aqui o nome se repete de etapa em
    /// etapa, então a semente do torneio entra no hash — senão o mesmo empate daria sempre a
    /// mesma dupla na carreira inteira.
    /// </summary>
    public static uint Sorteio(uint semente, string nome)
    {
        uint hash = 2166136261;
        for (int i = 0; i < 4; i++) hash = (hash ^ ((semente >> (8 * i)) & 0xFF)) * 16777619;
        foreach (char c in nome) hash = (hash ^ c) * 16777619;
        return hash;
    }
}
