using System.Diagnostics.CodeAnalysis;

namespace Padel.Core.Rede;

// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
// PROTOCOLO DO ONLINE — versão 1 (Protocolo.Versao)
//
// Arquitetura (DECISOES.md, D2): host autoritativo. O host é sempre o jogador 0 e roda a Partida em passo fixo de
// 1/120 s. Os clientes mandam ENTRADAS; o host manda INSTANTÂNEOS. O cliente prediz o próprio jogador e desenha o
// resto 100 ms no passado (interpolação). O transporte é abstrato (ITransporte): ENet no desenvolvimento, Steam
// Datagram Relay no lançamento — os dois têm canal confiável (ordenado, sem perda) e não confiável.
//
// ENVELOPE (todo pacote, little-endian):
//   [0]      Magico = 0xAD
//   [1]      Versao do protocolo de quem escreveu
//   [2]      TipoDeMensagem
//   [3..n-4] corpo (abaixo)
//   [n-4..n] CRC-32 (IEEE 802.3, o do zip) de tudo o que vem antes
//   = 7 bytes fixos. Pacote curto, com magia errada, CRC errado, tipo desconhecido, byte sobrando ou campo fora da
//   faixa: o leitor devolve falso. Nunca lança.
//   Ola e Recusado têm layout CONGELADO entre versões — é o que deixa o host recusar um cliente de outra versão
//   com uma mensagem que o cliente velho entende. Os demais tipos exigem Versao igual.
//
// Tipos primitivos: U8/I8/U16/I16/U32 fixos; VarU = inteiro sem sinal em 7 bits por byte (LEB128: 1 byte até 127);
// Texto = U8 com o tamanho em bytes + UTF-8 (corta em 255 bytes, na fronteira de um caractere).
//
// SALA (lobby) — canal CONFIÁVEL
//   Ola       cliente→host   Texto nome. A versão é a do cabeçalho.                        7 + 1 + nome  (~15 B)
//   BemVindo  host→cliente   U8 índice (2, 1, 3 nessa ordem — rival primeiro), U8 dificuldade, U8 flags
//                            (bit0 ponto de ouro), VarU sets pra vencer, U8 modo de golpe, U32 semente,
//                            U8 humanos (bit i = vaga i humana, provisório).                         17 B
//   Recusado  host→cliente   U8 motivo (1 versão incompatível, 2 sala cheia, 3 partida em andamento);
//                            a versão do host vai no cabeçalho.                                        8 B
//   Sala      host→todos     4 × Texto nome por vaga ("" = livre). A cada entrada/saída.       11 + nomes
//   Comecou   host→todos     U8 humanos (bit i), 4 × Texto nome. Depois dele o host já está jogando.
//
// JOGO
//   Entradas  cliente→host, NÃO confiável, A CADA TICK (120 Hz)
//             U8 n (1..8), U32 seq da mais nova; n × [I8 dx, I8 dy (−127..127, referencial do jogador: dy < 0 é
//             rumo à rede), U8 flags (bit0 ação segurada), U8 contador de ação, U8 contador de lob], da mais velha
//             pra mais nova, seqs consecutivas. As 8 últimas vão em todo pacote: perder até 7 seguidos não perde
//             nada. Os apertos são CONTADORES mod 256 — o host aplica "apertou" quando o contador sobe desde a
//             última entrada processada, então um aperto nunca se perde nem se duplica.         12 + 5n = 52 B
//             ≈ 6,2 KB/s de subida por cliente.
//   Instantaneo  host→cada cliente, NÃO confiável, a cada 4 ticks (30 Hz)
//             U32 tick do host; U8 EstadoDaPartida; I16 temporizador (ms);
//             bola: 3 × I16 posição (mm), 3 × I16 velocidade (cm/s), 3 × I16 spin (1/20 rad/s),
//                   U8 flags (bit0 em jogo, bit1 rolando, bit2 parada)                                19 B
//             4 × jogador: 2 × I16 posição (mm), 2 × I16 velocidade (cm/s), U8 balanço, U8 tempo no balanço,
//                   U8 cooldown (4 ms cada), U8 flags (bit0 balanço de lob, bit1 humano)              48 B
//             placar: 6 × VarU (pontos, games, sets de cada time), U8 flags (bit0 tie-break, bit1 ponto de ouro,
//                   bit2 tem vencedor, bit3 vencedor é o time 1, bit4 sacador é o time 1, bit5 sacador é o
//                   jogador 1), VarU sets pra vencer, VarU nº de sets encerrados × [VarU games, VarU games,
//                   U8 teve tie-break, (VarU, VarU pontos do tie-break)]                         ~9 B + 3–5/set
//             mensagem: U8 flags (bit0 tem, bit1 destaque, bit2 suave) + Texto                       1 + texto
//             caixa do saque: 4 × I16 (cm)                                                              8 B
//             confirmações: U8 n + n × [U8 índice, U32 última seq de entrada processada]            1 + 5/humano
//             eventos dos últimos 0,5 s (até 32), do mais velho pro mais novo: U8 n; se n > 0, U32 id do primeiro;
//                   n × [VarU salto de id desde o anterior (só do 2º em diante; ≥ 1), VarU ticks atrás, U8 tipo,
//                   U8 jogador (0xFF = nenhum), U8 time + 1, U8 golpe (0xFF), U8 motivo (0xFF)]. Cada evento sai em
//                   ~15 instantâneos; o cliente descarta id já visto.              1 B sem evento; 4 + 7/evento
//             descontinuidades da bola dos últimos 0,5 s (até 3): U8 n, n × [VarU ticks atrás, bola (19 B)] —
//                   a bola logo depois de golpe/saque/recolocação: o cliente simula a partir dela e a bola
//                   desenhada fica na trajetória exata do host.                                       1 + 20/marco
//             Típico: ~165 B, máximo ~260 B (medido nos testes: média e p95 < 300 B) ≈ 5 KB/s de descida por cliente.
//
// TEMPOS: passo 1/120 s; instantâneo a 30 Hz; entrada a 120 Hz com redundância 8; eventos e descontinuidades
// repetidos por 0,5 s; um par sem mandar nada por 3 s vira IA e a partida segue; interpolação padrão de 100 ms.
// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>Constantes, envelope e (de)serialização de todas as mensagens do online. Ver o cabeçalho deste arquivo.</summary>
public static class Protocolo
{
    public const byte Magico = 0xAD;
    public const byte Versao = 1;

    public const int TicksPorSegundo = 120;
    public const float Passo = 1f / TicksPorSegundo;
    /// <summary>Instantâneo a cada 4 ticks = 30 Hz.</summary>
    public const int TicksPorInstantaneo = 4;
    /// <summary>Quantas entradas (as mais recentes) vão em cada pacote do cliente.</summary>
    public const int EntradasPorPacote = 8;
    /// <summary>Por quantos ticks (0,5 s) um evento e uma descontinuidade continuam indo nos instantâneos.</summary>
    public const int TicksDeRepeticaoDeEventos = 60;
    public const int MaximoDeEventos = 32;
    public const int MaximoDeDescontinuidades = 3;
    public const float SegundosAteVirarIA = 3f;
    public const int Jogadores = 4;

    public const int TamanhoDoCabecalho = 3;
    public const int TamanhoDoCrc = 4;

    // ───── envelope ─────

    private static readonly uint[] TabelaDoCrc = CriarTabelaDoCrc();

    private static uint[] CriarTabelaDoCrc()
    {
        var tabela = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            tabela[n] = c;
        }
        return tabela;
    }

    /// <summary>CRC-32 IEEE (polinômio refletido 0xEDB88320) — detecta todo erro de até 32 bits seguidos.</summary>
    public static uint Crc32(ReadOnlySpan<byte> dados)
    {
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in dados) crc = TabelaDoCrc[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return ~crc;
    }

    private static EscritorBinario Comecar(TipoDeMensagem tipo, byte versao = Versao)
    {
        var e = new EscritorBinario();
        e.U8(Magico);
        e.U8(versao);
        e.U8((byte)tipo);
        return e;
    }

    private static byte[] Fechar(EscritorBinario e)
    {
        e.U32(Crc32(e.Escrito));
        return e.ParaArray();
    }

    /// <summary>Embrulha um corpo pronto no envelope (cabeçalho + CRC). Pra transporte de teste e ferramentas.</summary>
    public static byte[] Envelopar(TipoDeMensagem tipo, ReadOnlySpan<byte> corpo, byte versao = Versao)
    {
        var e = Comecar(tipo, versao);
        e.Bytes(corpo);
        return Fechar(e);
    }

    /// <summary>Confere magia, tamanho, CRC e tipo. Não confere a versão — quem lê o corpo decide.</summary>
    private static bool Abrir(ReadOnlySpan<byte> pacote, out byte versao, out TipoDeMensagem tipo, out ReadOnlySpan<byte> corpo)
    {
        versao = 0; tipo = 0; corpo = default;
        if (pacote.Length < TamanhoDoCabecalho + TamanhoDoCrc || pacote[0] != Magico) return false;
        var semCrc = pacote[..^TamanhoDoCrc];
        uint crc = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(pacote[^TamanhoDoCrc..]);
        if (Crc32(semCrc) != crc) return false;
        tipo = (TipoDeMensagem)pacote[2];
        if (!Enum.IsDefined(tipo)) return false;
        versao = pacote[1];
        corpo = semCrc[TamanhoDoCabecalho..];
        return true;
    }

    private static bool AbrirDoTipo(ReadOnlySpan<byte> pacote, TipoDeMensagem esperado, out LeitorBinario leitor, bool qualquerVersao = false)
    {
        leitor = default;
        if (!Abrir(pacote, out byte versao, out var tipo, out var corpo) || tipo != esperado) return false;
        if (!qualquerVersao && versao != Versao) return false;
        leitor = new LeitorBinario(corpo);
        return true;
    }

    /// <summary>O tipo de um pacote íntegro (de qualquer versão) — pra despachar.</summary>
    public static bool TentarLerTipo(ReadOnlySpan<byte> pacote, out TipoDeMensagem tipo) => Abrir(pacote, out _, out tipo, out _);

    // ───── sala ─────

    public static byte[] EscreverOla(Ola ola)
    {
        var e = Comecar(TipoDeMensagem.Ola, ola.Versao);
        e.Texto(ola.Nome);
        return Fechar(e);
    }

    public static bool TentarLerOla(ReadOnlySpan<byte> pacote, [NotNullWhen(true)] out Ola? ola)
    {
        ola = null;
        if (!Abrir(pacote, out byte versao, out var tipo, out var corpo) || tipo != TipoDeMensagem.Ola) return false;
        var l = new LeitorBinario(corpo);
        if (!l.Texto(out string nome) || l.Restante != 0) return false;
        ola = new Ola(nome, versao);
        return true;
    }

    public static byte[] EscreverRecusado(Recusado recusado)
    {
        var e = Comecar(TipoDeMensagem.Recusado, recusado.VersaoDoHost);
        e.U8((byte)recusado.Motivo);
        return Fechar(e);
    }

    public static bool TentarLerRecusado(ReadOnlySpan<byte> pacote, [NotNullWhen(true)] out Recusado? recusado)
    {
        recusado = null;
        if (!Abrir(pacote, out byte versao, out var tipo, out var corpo) || tipo != TipoDeMensagem.Recusado) return false;
        var l = new LeitorBinario(corpo);
        if (!l.U8(out byte m) || l.Restante != 0) return false;
        var motivo = (MotivoDaRecusa)m;
        if (!Enum.IsDefined(motivo)) return false;
        recusado = new Recusado(motivo, versao);
        return true;
    }

    private static byte BitsDosHumanos(bool[] humanos)
    {
        byte bits = 0;
        for (int i = 0; i < Math.Min(humanos.Length, Jogadores); i++) if (humanos[i]) bits |= (byte)(1 << i);
        return bits;
    }

    private static bool LerHumanos(ref LeitorBinario l, out bool[] humanos)
    {
        humanos = new bool[Jogadores];
        if (!l.U8(out byte bits) || bits > 0x0F) return false;
        for (int i = 0; i < Jogadores; i++) humanos[i] = (bits & (1 << i)) != 0;
        return true;
    }

    public static byte[] EscreverBemVindo(BemVindo bemVindo)
    {
        var o = bemVindo.Opcoes;
        var e = Comecar(TipoDeMensagem.BemVindo);
        e.U8((byte)bemVindo.Indice);
        e.U8((byte)o.Dificuldade);
        e.U8((byte)(o.PontoDeOuro ? 1 : 0));
        e.VarU((uint)o.SetsParaVencer);
        e.U8((byte)o.ModoDeGolpe);
        e.U32(bemVindo.Semente);
        e.U8(BitsDosHumanos(o.Humanos));
        return Fechar(e);
    }

    public static bool TentarLerBemVindo(ReadOnlySpan<byte> pacote, [NotNullWhen(true)] out BemVindo? bemVindo)
    {
        bemVindo = null;
        if (!AbrirDoTipo(pacote, TipoDeMensagem.BemVindo, out var l)) return false;
        if (!l.U8(out byte indice) || indice >= Jogadores) return false;
        if (!l.U8(out byte d) || !Enum.IsDefined((Dificuldade)d)) return false;
        if (!l.U8(out byte flags) || flags > 1) return false;
        if (!l.VarU(out int sets, 99) || sets < 1) return false;
        if (!l.U8(out byte modo) || !Enum.IsDefined((ModoDeGolpe)modo)) return false;
        if (!l.U32(out uint semente)) return false;
        if (!LerHumanos(ref l, out var humanos) || l.Restante != 0) return false;
        var opcoes = new OpcoesDaPartida
        {
            Dificuldade = (Dificuldade)d,
            PontoDeOuro = flags == 1,
            SetsParaVencer = sets,
            ModoDeGolpe = (ModoDeGolpe)modo,
            Semente = semente,
            Humanos = humanos,
        };
        bemVindo = new BemVindo(indice, opcoes, semente);
        return true;
    }

    private static void EscreverNomes(EscritorBinario e, string[] nomes)
    {
        for (int i = 0; i < Jogadores; i++) e.Texto(i < nomes.Length ? nomes[i] : "");
    }

    private static bool LerNomes(ref LeitorBinario l, out string[] nomes)
    {
        nomes = new string[Jogadores];
        for (int i = 0; i < Jogadores; i++) if (!l.Texto(out nomes[i])) return false;
        return true;
    }

    public static byte[] EscreverSala(Sala sala)
    {
        var e = Comecar(TipoDeMensagem.Sala);
        EscreverNomes(e, sala.Nomes);
        return Fechar(e);
    }

    public static bool TentarLerSala(ReadOnlySpan<byte> pacote, [NotNullWhen(true)] out Sala? sala)
    {
        sala = null;
        if (!AbrirDoTipo(pacote, TipoDeMensagem.Sala, out var l)) return false;
        if (!LerNomes(ref l, out var nomes) || l.Restante != 0) return false;
        sala = new Sala(nomes);
        return true;
    }

    public static byte[] EscreverComecou(Comecou comecou)
    {
        var e = Comecar(TipoDeMensagem.Comecou);
        e.U8(BitsDosHumanos(comecou.Humanos));
        EscreverNomes(e, comecou.Nomes);
        return Fechar(e);
    }

    public static bool TentarLerComecou(ReadOnlySpan<byte> pacote, [NotNullWhen(true)] out Comecou? comecou)
    {
        comecou = null;
        if (!AbrirDoTipo(pacote, TipoDeMensagem.Comecou, out var l)) return false;
        if (!LerHumanos(ref l, out var humanos) || !LerNomes(ref l, out var nomes) || l.Restante != 0) return false;
        comecou = new Comecou(humanos, nomes);
        return true;
    }

    // ───── entradas ─────

    /// <summary>As entradas precisam ter seqs consecutivas, da mais velha pra mais nova (é o que o cliente manda).</summary>
    public static byte[] EscreverEntradas(ReadOnlySpan<EntradaDeRede> entradas)
    {
        if (entradas.Length is < 1 or > 255) throw new ArgumentException("entre 1 e 255 entradas", nameof(entradas));
        for (int i = 1; i < entradas.Length; i++)
            if (entradas[i].Seq != entradas[i - 1].Seq + 1) throw new ArgumentException("as seqs precisam ser consecutivas", nameof(entradas));
        var e = Comecar(TipoDeMensagem.Entradas);
        e.U8((byte)entradas.Length);
        e.U32(entradas[^1].Seq);
        foreach (var en in entradas)
        {
            e.I8(en.Dx);
            e.I8(en.Dy);
            e.U8((byte)(en.AcaoSegurada ? 1 : 0));
            e.U8(en.ContadorDeAcao);
            e.U8(en.ContadorDeLob);
        }
        return Fechar(e);
    }

    public static bool TentarLerEntradas(ReadOnlySpan<byte> pacote, out EntradaDeRede[] entradas)
    {
        entradas = [];
        if (!AbrirDoTipo(pacote, TipoDeMensagem.Entradas, out var l)) return false;
        if (!l.U8(out byte n) || n == 0 || !l.U32(out uint ultima) || ultima < n - 1u) return false;
        var lidas = new EntradaDeRede[n];
        for (int i = 0; i < n; i++)
        {
            if (!l.I8(out sbyte dx) || !l.I8(out sbyte dy) || !l.U8(out byte flags) || flags > 1 || !l.U8(out byte acao) || !l.U8(out byte lob)) return false;
            lidas[i] = new EntradaDeRede(ultima - (uint)(n - 1 - i), dx, dy, flags == 1, acao, lob);
        }
        if (l.Restante != 0) return false;
        entradas = lidas;
        return true;
    }

    // ───── instantâneo ─────

    private static void EscreverBola(EscritorBinario e, EstadoDaBola b)
    {
        e.I16(Quantizacao.Para16(b.X, Quantizacao.PorMetro));
        e.I16(Quantizacao.Para16(b.Y, Quantizacao.PorMetro));
        e.I16(Quantizacao.Para16(b.Z, Quantizacao.PorMetro));
        e.I16(Quantizacao.Para16(b.Vx, Quantizacao.PorMetroPorSegundo));
        e.I16(Quantizacao.Para16(b.Vy, Quantizacao.PorMetroPorSegundo));
        e.I16(Quantizacao.Para16(b.Vz, Quantizacao.PorMetroPorSegundo));
        e.I16(Quantizacao.Para16(b.Wx, Quantizacao.PorRadianoPorSegundo));
        e.I16(Quantizacao.Para16(b.Wy, Quantizacao.PorRadianoPorSegundo));
        e.I16(Quantizacao.Para16(b.Wz, Quantizacao.PorRadianoPorSegundo));
        e.U8((byte)((b.EmJogo ? 1 : 0) | (b.Rolando ? 2 : 0) | (b.Parada ? 4 : 0)));
    }

    private static bool LerBola(ref LeitorBinario l, out EstadoDaBola bola)
    {
        bola = default;
        Span<short> v = stackalloc short[9];
        for (int i = 0; i < 9; i++) if (!l.I16(out v[i])) return false;
        if (!l.U8(out byte flags) || flags > 7) return false;
        const float p = Quantizacao.PorMetro, vel = Quantizacao.PorMetroPorSegundo, w = Quantizacao.PorRadianoPorSegundo;
        bola = new EstadoDaBola(
            Quantizacao.De16(v[0], p), Quantizacao.De16(v[1], p), Quantizacao.De16(v[2], p),
            Quantizacao.De16(v[3], vel), Quantizacao.De16(v[4], vel), Quantizacao.De16(v[5], vel),
            Quantizacao.De16(v[6], w), Quantizacao.De16(v[7], w), Quantizacao.De16(v[8], w),
            (flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0);
        return true;
    }

    private static void EscreverJogador(EscritorBinario e, EstadoDoJogador j)
    {
        e.I16(Quantizacao.Para16(j.X, Quantizacao.PorMetro));
        e.I16(Quantizacao.Para16(j.Y, Quantizacao.PorMetro));
        e.I16(Quantizacao.Para16(j.Vx, Quantizacao.PorMetroPorSegundo));
        e.I16(Quantizacao.Para16(j.Vy, Quantizacao.PorMetroPorSegundo));
        e.U8(Quantizacao.Para8(j.Balanco, Quantizacao.TempoCurtoPorSegundo));
        e.U8(Quantizacao.Para8(j.TempoNoBalanco, Quantizacao.TempoCurtoPorSegundo));
        e.U8(Quantizacao.Para8(j.Cooldown, Quantizacao.TempoCurtoPorSegundo));
        e.U8((byte)((j.BalancoDeLob ? 1 : 0) | (j.Humano ? 2 : 0)));
    }

    private static bool LerJogador(ref LeitorBinario l, out EstadoDoJogador jogador)
    {
        jogador = default;
        if (!l.I16(out short x) || !l.I16(out short y) || !l.I16(out short vx) || !l.I16(out short vy)) return false;
        if (!l.U8(out byte balanco) || !l.U8(out byte tempo) || !l.U8(out byte cooldown) || !l.U8(out byte flags) || flags > 3) return false;
        const float p = Quantizacao.PorMetro, vel = Quantizacao.PorMetroPorSegundo, t = Quantizacao.TempoCurtoPorSegundo;
        jogador = new EstadoDoJogador(Quantizacao.De16(x, p), Quantizacao.De16(y, p), Quantizacao.De16(vx, vel), Quantizacao.De16(vy, vel),
            Quantizacao.De8(balanco, t), Quantizacao.De8(tempo, t), (flags & 1) != 0, (flags & 2) != 0, Quantizacao.De8(cooldown, t));
        return true;
    }

    private const int MaximoDePontos = 10_000;   // sanidade: tie-break ou vantagem que não acaba nunca passa disso
    private const int MaximoDeSets = 32;

    private static void EscreverPlacar(EscritorBinario e, EstadoDoPlacar p)
    {
        e.VarU((uint)p.Pontos[0]); e.VarU((uint)p.Pontos[1]);
        e.VarU((uint)p.Games[0]); e.VarU((uint)p.Games[1]);
        e.VarU((uint)p.Sets[0]); e.VarU((uint)p.Sets[1]);
        int flags = (p.EmTieBreak ? 1 : 0) | (p.PontoDeOuro ? 2 : 0) | (p.Vencedor is not null ? 4 : 0) | (p.Vencedor == 1 ? 8 : 0)
            | (p.Sacador.Time == 1 ? 16 : 0) | (p.Sacador.Jogador == 1 ? 32 : 0);
        e.U8((byte)flags);
        e.VarU((uint)p.SetsParaVencer);
        e.VarU((uint)p.SetsAnteriores.Count);
        foreach (var s in p.SetsAnteriores)
        {
            e.VarU((uint)s.Games[0]); e.VarU((uint)s.Games[1]);
            e.U8((byte)(s.TieBreak is null ? 0 : 1));
            if (s.TieBreak is not null) { e.VarU((uint)s.TieBreak[0]); e.VarU((uint)s.TieBreak[1]); }
        }
    }

    private static bool LerPlacar(ref LeitorBinario l, out EstadoDoPlacar placar)
    {
        placar = EstadoDoPlacar.Inicial;
        if (!l.VarU(out int p0, MaximoDePontos) || !l.VarU(out int p1, MaximoDePontos)) return false;
        if (!l.VarU(out int g0, MaximoDePontos) || !l.VarU(out int g1, MaximoDePontos)) return false;
        if (!l.VarU(out int s0, MaximoDeSets) || !l.VarU(out int s1, MaximoDeSets)) return false;
        if (!l.U8(out byte flags) || flags > 63) return false;
        if ((flags & 4) == 0 && (flags & 8) != 0) return false;   // "vencedor é o time 1" sem vencedor
        if (!l.VarU(out int setsParaVencer, MaximoDeSets) || setsParaVencer < 1) return false;
        if (!l.VarU(out int n, MaximoDeSets)) return false;
        var sets = new SetEncerrado[n];
        for (int i = 0; i < n; i++)
        {
            if (!l.VarU(out int a, MaximoDePontos) || !l.VarU(out int b, MaximoDePontos) || !l.U8(out byte temTieBreak) || temTieBreak > 1) return false;
            int[]? tieBreak = null;
            if (temTieBreak == 1)
            {
                if (!l.VarU(out int ta, MaximoDePontos) || !l.VarU(out int tb, MaximoDePontos)) return false;
                tieBreak = [ta, tb];
            }
            sets[i] = new SetEncerrado([a, b], tieBreak);
        }
        placar = new EstadoDoPlacar([p0, p1], [g0, g1], [s0, s1], sets, (flags & 1) != 0, (flags & 4) != 0 ? ((flags & 8) != 0 ? 1 : 0) : null,
            new Sacador((flags & 16) != 0 ? 1 : 0, (flags & 32) != 0 ? 1 : 0), (flags & 2) != 0, setsParaVencer);
        return true;
    }

    private const byte Nenhum = 0xFF;

    public static byte[] EscreverInstantaneo(Instantaneo i)
    {
        if (i.Jogadores.Length != Jogadores) throw new ArgumentException("o instantâneo precisa de 4 jogadores", nameof(i));
        var e = Comecar(TipoDeMensagem.Instantaneo);
        e.U32(i.Tick);
        e.U8((byte)i.Estado);
        e.I16(Quantizacao.Para16(i.Temporizador, 1000f));
        EscreverBola(e, i.Bola);
        foreach (var j in i.Jogadores) EscreverJogador(e, j);
        EscreverPlacar(e, i.Placar);

        var m = i.Mensagem;
        e.U8((byte)(m is null ? 0 : 1 | (m.Destaque ? 2 : 0) | (m.Suave ? 4 : 0)));
        if (m is not null) e.Texto(m.Texto);

        var c = i.CaixaDoSaque;
        e.I16(Quantizacao.Para16(c.XMin, Quantizacao.PorMetroNaCaixa));
        e.I16(Quantizacao.Para16(c.XMax, Quantizacao.PorMetroNaCaixa));
        e.I16(Quantizacao.Para16(c.YMin, Quantizacao.PorMetroNaCaixa));
        e.I16(Quantizacao.Para16(c.YMax, Quantizacao.PorMetroNaCaixa));

        if (i.Confirmacoes.Count > Jogadores) throw new ArgumentException("confirmações demais", nameof(i));
        e.U8((byte)i.Confirmacoes.Count);
        foreach (var conf in i.Confirmacoes) { e.U8((byte)conf.Indice); e.U32(conf.Seq); }

        if (i.Eventos.Count > MaximoDeEventos) throw new ArgumentException("eventos demais", nameof(i));
        e.U8((byte)i.Eventos.Count);
        uint idAnterior = 0;
        for (int k = 0; k < i.Eventos.Count; k++)
        {
            var ev = i.Eventos[k];
            if (ev.Tick > i.Tick) throw new ArgumentException("evento do futuro", nameof(i));
            if (k == 0) e.U32(ev.Id);
            else if (ev.Id <= idAnterior) throw new ArgumentException("ids de evento precisam crescer", nameof(i));
            else e.VarU(ev.Id - idAnterior);
            idAnterior = ev.Id;
            e.VarU(i.Tick - ev.Tick);
            e.U8((byte)ev.Tipo);
            e.U8(ev.Jogador is >= 0 and < Jogadores ? (byte)ev.Jogador : Nenhum);
            e.U8((byte)(ev.Time + 1));
            e.U8(ev.Golpe is TipoDeGolpe g ? (byte)g : Nenhum);
            e.U8(ev.Motivo is Motivo mo ? (byte)mo : Nenhum);
        }

        if (i.Descontinuidades.Count > MaximoDeDescontinuidades) throw new ArgumentException("descontinuidades demais", nameof(i));
        e.U8((byte)i.Descontinuidades.Count);
        foreach (var d in i.Descontinuidades)
        {
            if (d.Tick > i.Tick) throw new ArgumentException("descontinuidade do futuro", nameof(i));
            e.VarU(i.Tick - d.Tick);
            EscreverBola(e, d.Bola);
        }
        return Fechar(e);
    }

    public static bool TentarLerInstantaneo(ReadOnlySpan<byte> pacote, [NotNullWhen(true)] out Instantaneo? instantaneo)
    {
        instantaneo = null;
        if (!AbrirDoTipo(pacote, TipoDeMensagem.Instantaneo, out var l)) return false;
        if (!l.U32(out uint tick) || !l.U8(out byte estadoBruto)) return false;
        var estado = (EstadoDaPartida)estadoBruto;
        if (!Enum.IsDefined(estado) || !l.I16(out short temporizador)) return false;
        if (!LerBola(ref l, out var bola)) return false;
        var jogadores = new EstadoDoJogador[Jogadores];
        for (int k = 0; k < Jogadores; k++) if (!LerJogador(ref l, out jogadores[k])) return false;
        if (!LerPlacar(ref l, out var placar)) return false;

        if (!l.U8(out byte flagsDaMensagem) || flagsDaMensagem > 7 || (flagsDaMensagem != 0 && (flagsDaMensagem & 1) == 0)) return false;
        Mensagem? mensagem = null;
        if ((flagsDaMensagem & 1) != 0)
        {
            if (!l.Texto(out string texto)) return false;
            mensagem = new Mensagem(texto, (flagsDaMensagem & 2) != 0, (flagsDaMensagem & 4) != 0);
        }

        if (!l.I16(out short xMin) || !l.I16(out short xMax) || !l.I16(out short yMin) || !l.I16(out short yMax)) return false;
        const float cx = Quantizacao.PorMetroNaCaixa;
        var caixa = new Caixa(Quantizacao.De16(xMin, cx), Quantizacao.De16(xMax, cx), Quantizacao.De16(yMin, cx), Quantizacao.De16(yMax, cx));

        if (!l.U8(out byte nConfirmacoes) || nConfirmacoes > Jogadores) return false;
        var confirmacoes = new List<Confirmacao>(nConfirmacoes);
        for (int k = 0; k < nConfirmacoes; k++)
        {
            if (!l.U8(out byte indice) || indice >= Jogadores || !l.U32(out uint seq)) return false;
            confirmacoes.Add(new Confirmacao(indice, seq));
        }

        if (!l.U8(out byte nEventos) || nEventos > MaximoDeEventos) return false;
        var eventos = new List<EventoNumerado>(nEventos);
        uint id = 0;
        for (int k = 0; k < nEventos; k++)
        {
            if (k == 0) { if (!l.U32(out id)) return false; }
            else
            {
                if (!l.VarU(out uint salto) || salto == 0 || salto > uint.MaxValue - id) return false;
                id += salto;
            }
            if (!l.VarU(out uint atras) || atras > tick) return false;
            if (!l.U8(out byte tipoBruto) || !l.U8(out byte jogador) || !l.U8(out byte time) || !l.U8(out byte golpeBruto) || !l.U8(out byte motivoBruto)) return false;
            var tipo = (TipoDeEventoDaPartida)tipoBruto;
            if (!Enum.IsDefined(tipo) || (jogador >= Jogadores && jogador != Nenhum) || time > 2) return false;
            TipoDeGolpe? golpe = golpeBruto == Nenhum ? null : (TipoDeGolpe)golpeBruto;
            Motivo? motivo = motivoBruto == Nenhum ? null : (Motivo)motivoBruto;
            if ((golpe is TipoDeGolpe g && !Enum.IsDefined(g)) || (motivo is Motivo mo && !Enum.IsDefined(mo))) return false;
            eventos.Add(new EventoNumerado(id, tick - atras, tipo, jogador == Nenhum ? -1 : jogador, time - 1, golpe, motivo));
        }

        if (!l.U8(out byte nDescontinuidades) || nDescontinuidades > MaximoDeDescontinuidades) return false;
        var descontinuidades = new List<Descontinuidade>(nDescontinuidades);
        for (int k = 0; k < nDescontinuidades; k++)
        {
            if (!l.VarU(out uint atras) || atras > tick || !LerBola(ref l, out var b)) return false;
            descontinuidades.Add(new Descontinuidade(tick - atras, b));
        }
        if (l.Restante != 0) return false;

        instantaneo = new Instantaneo
        {
            Tick = tick,
            Estado = estado,
            Temporizador = temporizador / 1000f,
            Bola = bola,
            Jogadores = jogadores,
            Placar = placar,
            Mensagem = mensagem,
            CaixaDoSaque = caixa,
            Confirmacoes = confirmacoes,
            Eventos = eventos,
            Descontinuidades = descontinuidades,
        };
        return true;
    }
}
