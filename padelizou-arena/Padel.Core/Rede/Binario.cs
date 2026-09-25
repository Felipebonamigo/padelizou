using System.Buffers.Binary;
using System.Text;

namespace Padel.Core.Rede;

/// <summary>
/// Escreve um pacote little-endian num buffer que cresce sozinho. Sem reflexão, sem alocação por campo:
/// é o que mantém o instantâneo pequeno e o servidor barato.
/// </summary>
public sealed class EscritorBinario
{
    private byte[] _buffer;

    public EscritorBinario(int capacidade = 256) => _buffer = new byte[Math.Max(16, capacidade)];

    public int Tamanho { get; private set; }
    public ReadOnlySpan<byte> Escrito => _buffer.AsSpan(0, Tamanho);

    public void Limpar() => Tamanho = 0;
    public byte[] ParaArray() => Escrito.ToArray();

    private Span<byte> Reservar(int n)
    {
        if (Tamanho + n > _buffer.Length) Array.Resize(ref _buffer, Math.Max(_buffer.Length * 2, Tamanho + n));
        var span = _buffer.AsSpan(Tamanho, n);
        Tamanho += n;
        return span;
    }

    public void U8(byte v) => Reservar(1)[0] = v;
    public void I8(sbyte v) => Reservar(1)[0] = unchecked((byte)v);
    public void U16(ushort v) => BinaryPrimitives.WriteUInt16LittleEndian(Reservar(2), v);
    public void I16(short v) => BinaryPrimitives.WriteInt16LittleEndian(Reservar(2), v);
    public void U32(uint v) => BinaryPrimitives.WriteUInt32LittleEndian(Reservar(4), v);
    public void Bytes(ReadOnlySpan<byte> v) => v.CopyTo(Reservar(v.Length));

    /// <summary>Inteiro sem sinal em 7 bits por byte (LEB128): 1 byte até 127, 2 até 16.383.</summary>
    public void VarU(uint v)
    {
        while (v >= 0x80) { U8((byte)(v | 0x80)); v >>= 7; }
        U8((byte)v);
    }

    /// <summary>UTF-8 com o tamanho em 1 byte. Acima de 255 bytes corta — na fronteira de um caractere, nunca no meio.</summary>
    public void Texto(string texto)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(texto);
        int n = Math.Min(bytes.Length, 255);
        while (n < bytes.Length && n > 0 && (bytes[n] & 0xC0) == 0x80) n--;   // bytes[n] é continuação: recua até o início do caractere
        U8((byte)n);
        Bytes(bytes.AsSpan(0, n));
    }
}

/// <summary>
/// Lê um pacote little-endian. Todo método devolve falso quando falta byte ou o valor é inválido — nunca lança:
/// pacote truncado ou corrompido é coisa normal na rede, não exceção.
/// </summary>
public ref struct LeitorBinario
{
    private readonly ReadOnlySpan<byte> _dados;
    private int _posicao;

    public LeitorBinario(ReadOnlySpan<byte> dados) { _dados = dados; _posicao = 0; }

    public readonly int Restante => _dados.Length - _posicao;

    private bool Pegar(int n, out ReadOnlySpan<byte> span)
    {
        if (n < 0 || Restante < n) { span = default; return false; }
        span = _dados.Slice(_posicao, n);
        _posicao += n;
        return true;
    }

    public bool U8(out byte v)
    {
        bool ok = Pegar(1, out var s);
        v = ok ? s[0] : (byte)0;
        return ok;
    }

    public bool I8(out sbyte v)
    {
        bool ok = U8(out byte b);
        v = unchecked((sbyte)b);
        return ok;
    }

    public bool U16(out ushort v)
    {
        bool ok = Pegar(2, out var s);
        v = ok ? BinaryPrimitives.ReadUInt16LittleEndian(s) : (ushort)0;
        return ok;
    }

    public bool I16(out short v)
    {
        bool ok = Pegar(2, out var s);
        v = ok ? BinaryPrimitives.ReadInt16LittleEndian(s) : (short)0;
        return ok;
    }

    public bool U32(out uint v)
    {
        bool ok = Pegar(4, out var s);
        v = ok ? BinaryPrimitives.ReadUInt32LittleEndian(s) : 0;
        return ok;
    }

    public bool VarU(out uint v)
    {
        v = 0;
        for (int deslocamento = 0; deslocamento < 35; deslocamento += 7)
        {
            if (!U8(out byte b)) return false;
            if (deslocamento == 28 && b > 0x0F) return false;   // passaria de 32 bits
            v |= (uint)(b & 0x7F) << deslocamento;
            if ((b & 0x80) == 0) return true;
        }
        return false;
    }

    /// <summary>VarU que precisa caber em [0, maximo]: contagem e placar corrompidos param aqui.</summary>
    public bool VarU(out int v, int maximo)
    {
        bool ok = VarU(out uint u) && u <= (uint)maximo;
        v = ok ? (int)u : 0;
        return ok;
    }

    public bool Texto(out string texto)
    {
        texto = "";
        if (!U8(out byte n) || !Pegar(n, out var s)) return false;
        texto = Encoding.UTF8.GetString(s);   // o decodificador padrão troca byte inválido por U+FFFD, não lança
        return true;
    }
}

/// <summary>Quantização dos campos do instantâneo: o que cabe em 16 bits sem o olho (nem o teste) perceber.</summary>
public static class Quantizacao
{
    /// <summary>Posição: milímetros em 16 bits (±32 m).</summary>
    public const float PorMetro = 1000f;
    /// <summary>Velocidade: cm/s em 16 bits (±327 m/s).</summary>
    public const float PorMetroPorSegundo = 100f;
    /// <summary>Spin: 1/20 rad/s em 16 bits (±1.638 rad/s ≈ 15.600 rpm — a bola rolando depois de um quique a 30 m/s gira ~900 rad/s).</summary>
    public const float PorRadianoPorSegundo = 20f;
    /// <summary>Tempos curtos (balanço, cooldown): 4 ms em 8 bits (até 1,02 s).</summary>
    public const float TempoCurtoPorSegundo = 250f;
    /// <summary>Caixa do saque: centímetros em 16 bits.</summary>
    public const float PorMetroNaCaixa = 100f;

    public static short Para16(float valor, float escala)
    {
        float q = valor * escala;
        if (!float.IsFinite(q)) return 0;
        return (short)Math.Clamp(MathF.Round(q), short.MinValue, short.MaxValue);
    }

    public static float De16(short valor, float escala) => valor / escala;

    public static byte Para8(float valor, float escala)
    {
        float q = valor * escala;
        if (!float.IsFinite(q)) return 0;
        return (byte)Math.Clamp(MathF.Round(q), 0, 255);
    }

    public static float De8(byte valor, float escala) => valor / escala;
}
