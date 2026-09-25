using Padel.Core.Rede;

namespace Padel.Core.Tests;

/// <summary>
/// Como o host reconstrói a entrada de cada tick a partir dos pacotes do cliente: apertos como contadores (nunca
/// perdidos, nunca duplicados), redundância de 8, tick sem entrada repete a direção sem inventar aperto, e o
/// buffer que encolhe quando sobra entrada.
/// </summary>
public class RedeEntradasTests
{
    /// <summary>O lado do cliente, mínimo: numera as entradas, conta os apertos e empacota as 8 últimas.</summary>
    private sealed class ClienteDeMentira
    {
        private readonly List<EntradaDeRede> _enviadas = [];
        private byte _acao, _lob;

        public uint Seq { get; private set; }
        public List<(float dx, float dy)> Direcoes { get; } = [];

        public EntradaDeRede[] Tick(float dx, float dy = 0, bool acao = false, bool lob = false)
        {
            Seq++;
            if (acao) _acao++;
            if (lob) _lob++;
            var e = new EntradaDeRede(Seq, EntradaDeRede.Quantizar(dx), EntradaDeRede.Quantizar(dy), acao, _acao, _lob);
            _enviadas.Add(e);
            Direcoes.Add((e.DirecaoX, e.DirecaoY));
            return _enviadas.TakeLast(Protocolo.EntradasPorPacote).ToArray();
        }
    }

    private static (int acoes, int lobs) Contar(IEnumerable<Entrada> entradas) =>
        (entradas.Count(e => e.AcaoPressionada), entradas.Count(e => e.LobPressionada));

    [Fact]
    public void Pacote_repetido_nao_duplica_o_aperto()
    {
        var cliente = new ClienteDeMentira();
        var fila = new FilaDeEntradas();
        var aplicadas = new List<Entrada>();
        EntradaDeRede[]? anterior = null;
        for (int t = 0; t < 30; t++)
        {
            var pacote = cliente.Tick(0.5f, acao: t == 3);
            fila.Receber(pacote);
            fila.Receber(pacote);   // duplicado pela rede
            if (anterior is not null) fila.Receber(anterior);   // e o pacote anterior chegando atrasado, fora de ordem
            anterior = pacote;
            aplicadas.Add(fila.Proxima());
        }
        Assert.Equal((1, 0), Contar(aplicadas));
    }

    [Fact]
    public void Ate_7_pacotes_perdidos_seguidos_nao_perdem_nada_pela_redundancia()
    {
        var cliente = new ClienteDeMentira();
        var fila = new FilaDeEntradas();
        var aCaminho = new Queue<EntradaDeRede[]>();
        var aplicadas = new List<Entrada>();
        var comEntrada = new List<Entrada>();
        var aleatorio = new Aleatorio(4);
        for (int t = 0; t < 200; t++)
        {
            var pacote = cliente.Tick(aleatorio.Proximo() * 2 - 1, aleatorio.Proximo() * 2 - 1, acao: t % 17 == 5, lob: t % 41 == 9);
            bool perdido = t % 40 is >= 10 and < 17;   // 7 seguidos a cada 40
            aCaminho.Enqueue(perdido ? [] : pacote);
            if (aCaminho.Count > 3) fila.Receber(aCaminho.Dequeue());   // 3 ticks de latência
            var entrada = fila.Proxima();
            aplicadas.Add(entrada);
            if (fila.UltimaTeveEntrada) comEntrada.Add(entrada);
        }
        Assert.Equal(0, fila.EntradasPerdidas);
        int acoes = Enumerable.Range(0, 200).Count(t => t % 17 == 5);
        int lobs = Enumerable.Range(0, 200).Count(t => t % 41 == 9);
        var processadas = (int)fila.UltimaSeqProcessada;
        var (acoesAplicadas, lobsAplicados) = Contar(aplicadas);
        // Tudo o que foi processado veio na direção exata que o cliente mandou (nenhuma entrada substituída).
        Assert.Equal(processadas, comEntrada.Count);
        for (int i = 0; i < processadas; i++)
        {
            Assert.Equal(cliente.Direcoes[i].dx, comEntrada[i].Dx);
            Assert.Equal(cliente.Direcoes[i].dy, comEntrada[i].Dy);
        }
        // Os apertos das entradas já processadas foram aplicados, uma vez cada.
        int acoesProcessadas = Enumerable.Range(0, processadas).Count(t => t % 17 == 5);
        int lobsProcessados = Enumerable.Range(0, processadas).Count(t => t % 41 == 9);
        Assert.Equal(acoesProcessadas, acoesAplicadas);
        Assert.Equal(lobsProcessados, lobsAplicados);
        Assert.True(processadas > 150 && acoes >= acoesProcessadas && lobs >= lobsProcessados);
    }

    [Fact]
    public void Mais_de_8_pacotes_perdidos_perdem_a_direcao_mas_o_aperto_chega_uma_vez()
    {
        var cliente = new ClienteDeMentira();
        var fila = new FilaDeEntradas();
        var aplicadas = new List<Entrada>();
        for (int seq = 1; seq <= 30; seq++)
        {
            var pacote = cliente.Tick(0.3f, acao: seq == 7);
            if (seq is < 5 or > 16) fila.Receber(pacote);   // 12 pacotes seguidos perdidos: as entradas 5–9 somem de vez
        }
        for (int t = 0; t < 40; t++) aplicadas.Add(fila.Proxima());
        Assert.Equal(5, fila.EntradasPerdidas);
        Assert.Equal((1, 0), Contar(aplicadas));
        Assert.Equal(30u, fila.UltimaSeqProcessada);
    }

    [Fact]
    public void Tick_sem_entrada_repete_a_direcao_da_ultima_e_nao_inventa_aperto()
    {
        var cliente = new ClienteDeMentira();
        var fila = new FilaDeEntradas();
        fila.Receber(cliente.Tick(1f, -0.5f, acao: true));
        var primeira = fila.Proxima();
        Assert.True(primeira.AcaoPressionada);
        Assert.Equal(1f, primeira.Dx);
        for (int i = 0; i < 3; i++)
        {
            var repetida = fila.Proxima();
            Assert.False(repetida.AcaoPressionada);
            Assert.False(repetida.LobPressionada);
            Assert.Equal(primeira.Dx, repetida.Dx);
            Assert.Equal(primeira.Dy, repetida.Dy);
        }
        Assert.Equal(3, fila.TicksSemEntrada);
        Assert.Equal(1u, fila.UltimaSeqProcessada);

        var antesDeTudo = new FilaDeEntradas().Proxima();   // nada recebido ainda: parado, sem aperto
        Assert.Equal(Entrada.Vazia, antesDeTudo);
    }

    [Fact]
    public void Dois_apertos_que_chegam_juntos_saem_em_dois_ticks_seguidos()
    {
        var cliente = new ClienteDeMentira();
        var fila = new FilaDeEntradas();
        for (int seq = 1; seq <= 20; seq++)
        {
            var pacote = cliente.Tick(0, acao: seq is 3 or 5, lob: seq == 4);
            if (seq is 1 or 20) fila.Receber(pacote);   // o pacote 20 traz 13..20: 2..12 somem, e os apertos 3, 4 e 5 só chegam no contador da 13
        }
        var aplicadas = Enumerable.Range(0, 20).Select(_ => fila.Proxima()).ToList();
        Assert.Equal(11, fila.EntradasPerdidas);
        Assert.Equal((2, 1), Contar(aplicadas));
        int primeiro = aplicadas.FindIndex(e => e.AcaoPressionada);
        Assert.True(aplicadas[primeiro + 1].AcaoPressionada, "o segundo aperto devia sair no tick seguinte");
        Assert.True(aplicadas[primeiro].LobPressionada, "o lob sai junto com o primeiro aperto de ação");
    }

    [Fact]
    public void O_contador_que_da_a_volta_em_256_nao_perde_nem_inventa_aperto()
    {
        var cliente = new ClienteDeMentira();
        var fila = new FilaDeEntradas();
        var aplicadas = new List<Entrada>();
        for (int t = 0; t < 700; t++)
        {
            fila.Receber(cliente.Tick(0, acao: t % 2 == 0 && t < 600, lob: t % 3 == 0 && t < 600));
            aplicadas.Add(fila.Proxima());
        }
        Assert.Equal((300, 200), Contar(aplicadas));   // os contadores deram a volta em 256 no caminho
    }

    [Fact]
    public void Um_salto_absurdo_de_seq_nao_trava_o_host_e_a_fila_segue_dali()
    {
        // Cliente bugado ou malicioso: depois da seq 10, manda a seq 3 bilhões. O host não pode varrer 3 bilhões de
        // posições pra "pular" até lá — a fila salta em tempo constante e continua aceitando o que vem depois.
        var fila = new FilaDeEntradas();
        for (uint s = 1; s <= 10; s++) fila.Receber([new EntradaDeRede(s, 0, 0, false, 0, 0)]);
        const uint longe = 3_000_000_000;
        var relogio = System.Diagnostics.Stopwatch.StartNew();
        fila.Receber([new EntradaDeRede(longe, 127, 0, false, 1, 0)]);
        relogio.Stop();
        Assert.True(relogio.ElapsedMilliseconds < 200, $"receber o salto levou {relogio.ElapsedMilliseconds} ms");

        var aplicadas = Enumerable.Range(0, FilaDeEntradas.Capacidade).Select(_ => fila.Proxima()).ToList();
        Assert.Equal(longe, fila.UltimaSeqProcessada);
        Assert.Equal(1f, aplicadas[^1].Dx);
        Assert.Equal(1, Contar(aplicadas).acoes);   // o aperto que o contador trouxe, uma vez
        fila.Receber([new EntradaDeRede(longe + 1, -127, 0, false, 1, 0)]);
        Assert.Equal(-1f, fila.Proxima().Dx);
    }

    [Fact]
    public void Quando_sobra_entrada_por_um_segundo_o_buffer_encolhe_sem_perder_aperto()
    {
        var cliente = new ClienteDeMentira();
        var fila = new FilaDeEntradas();
        var aplicadas = new List<Entrada>();
        // Um pico de latência deixou 20 entradas acumuladas no host; depois a rede volta ao normal.
        for (int i = 0; i < 20; i++) fila.Receber(cliente.Tick(0.1f, acao: i == 10));
        int apertos = 1;
        for (int t = 0; t < 3 * Protocolo.TicksPorSegundo; t++)
        {
            bool aperta = t % 30 == 7;
            apertos += aperta ? 1 : 0;
            fila.Receber(cliente.Tick(0.1f, acao: aperta));
            aplicadas.Add(fila.Proxima());
        }
        Assert.True(fila.Acumuladas <= 2, $"o buffer não encolheu: {fila.Acumuladas} entradas acumuladas");
        Assert.True(fila.EntradasPuladas >= 17, $"pulou só {fila.EntradasPuladas}");
        for (int t = 0; t < 10; t++) aplicadas.Add(fila.Proxima());
        Assert.Equal(apertos, Contar(aplicadas).acoes);
    }
}
