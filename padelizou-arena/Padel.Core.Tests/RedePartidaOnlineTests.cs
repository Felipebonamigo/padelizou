using Padel.Core.Rede;

namespace Padel.Core.Tests;

/// <summary>
/// Host + 3 clientes numa rede de internet ruim (150 ms de ida e volta, 20 ms de jitter, 10 % de perda), 30 s de
/// partida com entradas roteirizadas e vários apertos. É o teste do online inteiro: aperto aplicado uma vez só,
/// bola interpolada colada na do host, predição que confirma, placar e mensagem que batem, instantâneo pequeno,
/// evento que não duplica nem some, e o par que cai virando IA.
/// </summary>
public class RedePartidaOnlineTests
{
    private static readonly CondicoesDaRede Internet = new() { Latencia = 0.075, Jitter = 0.02, Perda = 0.10 };

    /// <summary>Entrada roteirizada do jogador k no tick n: anda em curvas e aperta ação e lob de tempos em tempos.</summary>
    private static Entrada Roteiro(int k, int n)
    {
        float angulo = n * 0.011f * (k + 1) + k * 1.7f;
        float dx = MathF.Cos(angulo), dy = MathF.Sin(angulo * 0.63f + k);
        bool acao = n % (37 + 11 * k) == 5;
        bool lob = n % (173 + 29 * k) == 17;
        return new Entrada(dx, dy, acao, acao, lob);
    }

    /// <summary>
    /// O transporte do host com um espião no meio: anota quando o host recebeu o último pacote de cada par — é o
    /// relógio do "3 s sem pacote", medido de fora do servidor. Não muda nada do que passa.
    /// </summary>
    private sealed class TransporteQueAnota(TransporteEmMemoria interno) : ITransporte
    {
        public Dictionary<int, double> UltimoPacoteDe { get; } = [];

        public void Enviar(int par, Canal canal, ReadOnlySpan<byte> dados) => interno.Enviar(par, canal, dados);
        public void Desconectar(int par) => interno.Desconectar(par);
        public void Processar() => interno.Processar();

        public bool TentarReceber(out EventoDoTransporte evento)
        {
            if (!interno.TentarReceber(out evento)) return false;
            if (evento.Tipo == TipoDeEventoDoTransporte.Pacote) UltimoPacoteDe[evento.Par] = interno.Rede.Agora;
            return true;
        }
    }

    private sealed class Partidona
    {
        private readonly TransporteEmMemoria _host;
        private readonly List<int> _passosDoCliente = [];

        public Partidona(uint sementeDaRede = 2026, CondicoesDaRede? condicoes = null)
        {
            Rede = new RedeEmMemoria(sementeDaRede, condicoes ?? Internet);
            _host = Rede.NovoPonto();
            TransporteDoHost = new TransporteQueAnota(_host);
            Servidor = new ServidorDaPartida(TransporteDoHost, "Host", new OpcoesDaPartida { Semente = 42 });
            for (int k = 0; k < 3; k++)
            {
                var t = Rede.NovoPonto();
                var (_, parNoHost) = Rede.Conectar(t, _host);
                ParesNoHost.Add(parNoHost);
                Clientes.Add(new ClienteDaPartida(t, $"Cliente {k}"));
                _passosDoCliente.Add(0);
                Rodar(0.4, (_, _) => Entrada.Vazia);   // um de cada vez: vagas 2, 1, 3
            }
            Servidor.EntradaAplicada += (indice, e) =>
            {
                if (e.AcaoPressionada) AcoesAplicadas[indice]++;
                if (e.LobPressionada) LobsAplicados[indice]++;
            };
            Servidor.EventoRegistrado += EventosDoHost.Add;
            Rede.AoEnviar += p =>
            {
                if (p.Dados.Length > 2 && p.Dados.Span[2] == (byte)TipoDeMensagem.Instantaneo) Tamanhos.Add(p.Dados.Length);
            };
            for (int k = 0; k < 3; k++) { EventosEntregues.Add([]); Reconciliacoes.Add([]); int kk = k; Clientes[k].Reconciliou += r => Reconciliacoes[kk].Add(r); }
            Assert.Equal([2, 1, 3], Clientes.Select(c => c.Indice));
            Assert.All(Clientes, c => Assert.Equal(FaseDoCliente.NaSala, c.Fase));
            Partida = Servidor.Iniciar();
            Rodar(0.5, (_, _) => Entrada.Vazia);
            Assert.All(Clientes, c => Assert.Equal(FaseDoCliente.Jogando, c.Fase));
        }

        public RedeEmMemoria Rede { get; }
        public TransporteQueAnota TransporteDoHost { get; }
        /// <summary>O par de cada cliente visto pelo host, na ordem de <see cref="Clientes"/>.</summary>
        public List<int> ParesNoHost { get; } = [];
        public ServidorDaPartida Servidor { get; }
        public Partida Partida { get; }
        public List<ClienteDaPartida> Clientes { get; } = [];
        public bool[] Ativos { get; } = [true, true, true];
        public double[] UltimoPassoEm { get; } = new double[3];

        // A verdade, no host, por tick.
        public Dictionary<uint, EstadoDaBola> BolaNoHost { get; } = [];
        public Dictionary<uint, EstadoDoPlacar> PlacarNoHost { get; } = [];
        public Dictionary<uint, Mensagem?> MensagemNoHost { get; } = [];
        public Dictionary<uint, EstadoDaPartida> EstadoNoHost { get; } = [];
        public List<EventoNumerado> EventosDoHost { get; } = [];
        public int[] AcoesAplicadas { get; } = new int[4];
        public int[] LobsAplicados { get; } = new int[4];
        public int[] AcoesEnviadas { get; } = new int[4];
        public int[] LobsEnviados { get; } = new int[4];
        public List<int> Tamanhos { get; } = [];

        // O que cada cliente viu.
        public List<List<EventoNumerado>> EventosEntregues { get; } = [];
        public List<List<Reconciliacao>> Reconciliacoes { get; } = [];
        public float MaiorErroDaBola { get; private set; }
        public int AmostrasDaBola { get; private set; }
        public int AmostrasExtrapoladas { get; private set; }
        public int ConferenciasDePlacar { get; private set; }
        public double[] UltimoTickDesenhado { get; } = new double[3];
        /// <summary>Tick do host menos o tick que o cliente estima pro host, no mesmo instante (depois de 3 s de jogo).</summary>
        public double MenorAtrasoDoRelogio { get; private set; } = double.PositiveInfinity;
        public double MaiorAtrasoDoRelogio { get; private set; } = double.NegativeInfinity;

        public void Rodar(double segundos, Func<int, int, Entrada> roteiro)
        {
            int ticks = (int)Math.Round(segundos * Protocolo.TicksPorSegundo);
            for (int i = 0; i < ticks; i++) Passo(k => Ativos[k], roteiro);
        }

        private void Passo(Func<int, bool> ativo, Func<int, int, Entrada> roteiro)
        {
            Rede.AvancarRelogio(Protocolo.Passo);
            if (Servidor.Fase == FaseDoServidor.Jogando)
            {
                Servidor.Passo(roteiro(3, (int)Servidor.Tick));
                uint tick = Servidor.Tick;
                BolaNoHost[tick] = EstadoDaBola.De(Partida.Bola);
                PlacarNoHost[tick] = EstadoDoPlacar.De(Partida.Placar);
                MensagemNoHost[tick] = Partida.Mensagem;
                EstadoNoHost[tick] = Partida.Estado;
            }
            else Servidor.Passo();

            for (int k = 0; k < Clientes.Count; k++)
            {
                if (!ativo(k)) continue;
                var c = Clientes[k];
                bool jogando = c.Fase == FaseDoCliente.Jogando;
                var entrada = jogando ? roteiro(k, _passosDoCliente[k]++) : Entrada.Vazia;
                c.Passo(entrada);
                UltimoPassoEm[k] = Rede.Agora;
                if (!jogando) continue;
                if (entrada.AcaoPressionada) AcoesEnviadas[c.Indice]++;
                if (entrada.LobPressionada) LobsEnviados[c.Indice]++;
                Conferir(k, c);
            }
        }

        private void Conferir(int k, ClienteDaPartida c)
        {
            var desenho = c.ParaDesenhar();
            if (desenho is null) return;
            EventosEntregues[k].AddRange(desenho.Eventos);
            UltimoTickDesenhado[k] = desenho.Tick;

            // O quadro desenhado mostra placar, mensagem e estado do instantâneo de onde saiu — os do host naquele tick.
            Assert.Equal(PlacarNoHost[desenho.TickDoEstado], desenho.Placar);
            Assert.Equal(MensagemNoHost[desenho.TickDoEstado], desenho.Mensagem);
            Assert.Equal(EstadoNoHost[desenho.TickDoEstado], desenho.Estado);

            // O tick do host estimado pelos instantâneos: nunca à frente do host, nunca mais atrás que a demora da rede.
            if (Servidor.Tick > 3 * Protocolo.TicksPorSegundo)
            {
                double atraso = Servidor.Tick - c.TickEstimadoDoServidor;
                MenorAtrasoDoRelogio = Math.Min(MenorAtrasoDoRelogio, atraso);
                MaiorAtrasoDoRelogio = Math.Max(MaiorAtrasoDoRelogio, atraso);
            }

            // (b) a bola desenhada, no tick de servidor que está sendo desenhado, contra a bola do host naquele tick.
            // Interpolada = já chegou instantâneo daquele tick ou de depois. Sem isso (dois instantâneos seguidos
            // perdidos) o cliente extrapola pela física, e nada prevê um golpe ou um ponto novo que ainda não chegou:
            // essas amostras só contam pra provar que são raras.
            uint t = (uint)Math.Floor(c.TickDeDesenho());
            var visao = c.Visao(t);
            if (visao is not null && BolaNoHost.TryGetValue(t, out var bolaDoHost))
            {
                if (c.UltimoInstantaneo is { } maisNovo && maisNovo.Tick >= t)
                {
                    MaiorErroDaBola = MathF.Max(MaiorErroDaBola, visao.Bola.DistanciaAte(bolaDoHost));
                    AmostrasDaBola++;
                }
                else AmostrasExtrapoladas++;
            }

            // (d) placar e mensagem do último instantâneo que chegou batem com os do host naquele tick.
            var ultimo = c.UltimoInstantaneo;
            if (ultimo is not null && c.Visao(ultimo.Tick) is VisaoDaPartida doUltimo)
            {
                Assert.Equal(PlacarNoHost[ultimo.Tick], doUltimo.Placar);
                Assert.Equal(MensagemNoHost[ultimo.Tick], doUltimo.Mensagem);
                ConferenciasDePlacar++;
            }
        }
    }

    // Três sementes de rede: a mesma partida (semente 42) com perdas e atrasos sorteados de outro jeito — o que passa
    // não é sorte de um sorteio. 29 foi a pior de 30 sementes medidas pra predição (0,116 m).
    [Theory]
    [InlineData(2026u)]
    [InlineData(29u)]
    [InlineData(7u)]
    public void Host_e_3_clientes_jogam_30_s_numa_internet_ruim_e_tudo_bate(uint sementeDaRede)
    {
        var p = new Partidona(sementeDaRede);
        p.Rodar(30, Roteiro);
        p.Rodar(1.5, (_, _) => Entrada.Vazia);   // escoa o que ainda está a caminho, sem apertos novos

        Assert.True(p.Partida.Estatisticas.Pontos >= 3, $"poucos pontos em 30 s: {p.Partida.Estatisticas.Pontos}");
        Assert.True(p.Rede.PacotesPerdidos > 0.08 * p.Rede.PacotesEnviados, "a rede não estava perdendo pacotes");

        // (a) cada aperto mandado por um cliente foi aplicado no host exatamente uma vez.
        foreach (var c in p.Clientes)
        {
            Assert.True(p.AcoesEnviadas[c.Indice] > 20 && p.LobsEnviados[c.Indice] > 5, "o roteiro devia apertar bastante");
            Assert.Equal(p.AcoesEnviadas[c.Indice], p.AcoesAplicadas[c.Indice]);
            Assert.Equal(p.LobsEnviados[c.Indice], p.LobsAplicados[c.Indice]);
        }

        // (b) a bola interpolada fica a menos de 0,3 m da bola do host no mesmo tick de servidor.
        Assert.True(p.AmostrasDaBola > 3 * 3000, $"poucas amostras da bola: {p.AmostrasDaBola}");
        Assert.True(p.MaiorErroDaBola < 0.3f, $"a bola desenhada chegou a {p.MaiorErroDaBola:F3} m da bola do host");
        Assert.True(p.AmostrasExtrapoladas < 0.02 * p.AmostrasDaBola, $"o atraso de 100 ms não segurou: {p.AmostrasExtrapoladas} quadros extrapolados");

        // (c) o jogador predito fica a menos de 0,15 m da posição autoritativa quando a entrada é confirmada.
        // Só as confirmações em que a predição partiu de um rally e o host ainda estava no rally: nas viradas de
        // estado o erro é da natureza do online, não da predição — o saque de OUTRO jogador (o cliente só sabe dele
        // meia ida e volta depois; o host já andou com ele), o fim do ponto (o host para os humanos) e o ponto novo
        // (o host recoloca todo mundo). Nesses casos a confirmação seguinte corrige.
        for (int k = 0; k < 3; k++)
        {
            var noRally = p.Reconciliacoes[k].Where(r => r.EstadoNaPredicao == EstadoDaPartida.Rally && r.EstadoConfirmado == EstadoDaPartida.Rally).ToList();
            Assert.True(noRally.Count > 100, $"cliente {k}: só {noRally.Count} confirmações durante o rally");
            float pior = noRally.Max(r => r.Erro);
            Assert.True(pior < 0.15f, $"cliente {k}: a predição errou {pior:F3} m na confirmação");
        }

        // (d) placar e mensagem conferidos a cada tick, com a partida mudando de placar no caminho.
        Assert.True(p.ConferenciasDePlacar > 3 * 3000, $"poucas conferências: {p.ConferenciasDePlacar}");
        Assert.True(p.PlacarNoHost.Values.Select(x => x.ToString()).Distinct().Count() > 2, "o placar nem mudou");

        // O tick do servidor estimado pelos instantâneos anda com o host, atrás só da demora de ida: nunca menos que a
        // latência (nada chega antes dela), nunca mais que latência + jitter. 1 tick de folga pra arredondar o passo.
        double idaMinima = Internet.Latencia * Protocolo.TicksPorSegundo, idaMaxima = (Internet.Latencia + Internet.Jitter) * Protocolo.TicksPorSegundo;
        Assert.True(p.MenorAtrasoDoRelogio >= idaMinima - 1, $"o cliente estimou o host só {p.MenorAtrasoDoRelogio:F2} ticks atrás (latência: {idaMinima:F1})");
        Assert.True(p.MaiorAtrasoDoRelogio <= idaMaxima + 1, $"o cliente estimou o host {p.MaiorAtrasoDoRelogio:F2} ticks atrás (latência + jitter: {idaMaxima:F1})");

        // (e) instantâneo típico < 300 bytes.
        var tamanhos = p.Tamanhos.OrderBy(t => t).ToList();
        Assert.True(tamanhos.Count > 3 * 800, $"poucos instantâneos: {tamanhos.Count}");
        Assert.True(tamanhos.Average() < 300, $"média de {tamanhos.Average():F0} bytes");
        Assert.True(tamanhos[(int)(tamanhos.Count * 0.95)] < 300, $"p95 de {tamanhos[(int)(tamanhos.Count * 0.95)]} bytes");

        // (g) nenhum evento chega duplicado nem se perde: cada cliente recebeu os eventos do host, na ordem, uma vez.
        Assert.True(p.EventosDoHost.Count > 50, $"poucos eventos: {p.EventosDoHost.Count}");
        for (int k = 0; k < 3; k++)
        {
            var entregues = p.EventosEntregues[k];
            Assert.Equal(p.EventosDoHost.Take(entregues.Count), entregues);
            int devidos = p.EventosDoHost.Count(e => e.Tick + 30 <= p.UltimoTickDesenhado[k]);
            Assert.True(entregues.Count >= devidos, $"cliente {k}: {entregues.Count} eventos entregues, esperava ao menos {devidos}");
        }
    }

    [Fact]
    public void Um_cliente_que_para_de_responder_vira_IA_no_host_em_ate_3_s_e_a_partida_continua()
    {
        var p = new Partidona(sementeDaRede: 77);
        var viraramIA = new List<(int indice, double quando)>();
        p.Servidor.JogadorVirouIA += i => viraramIA.Add((i, p.Rede.Agora));
        p.Rodar(8, Roteiro);
        var calado = p.Clientes[1];
        int indice = calado.Indice;
        p.Ativos[1] = false;   // o jogo do cliente travou: nem Passo, nem pacote
        double parouEm = p.UltimoPassoEm[1];
        int pontosAntes = p.Partida.Estatisticas.Pontos;
        float tempoAntes = p.Partida.TempoDeJogo;
        var posicoes = new List<(float x, float y)>();
        for (int s = 0; s < 12 * 10; s++)
        {
            p.Rodar(0.1, Roteiro);
            var j = p.Partida.Jogadores[indice];
            posicoes.Add((j.X, j.Y));
        }

        var (quem, quando) = Assert.Single(viraramIA);
        Assert.Equal(indice, quem);
        // Os 3 s são da especificação e vão escritos aqui, não lidos de Protocolo.SegundosAteVirarIA: comparar a
        // constante com ela mesma deixava passar qualquer valor (com 6 s a suíte ficava verde).
        const double TresSegundos = 3.0;
        const double UmTick = 1.0 / Protocolo.TicksPorSegundo;
        // No host: do último pacote que chegou do cliente calado até a IA assumir, 3 s cravados — mais o tick em que
        // o host confere, e nada de folga pra rede, que aqui já ficou de fora.
        double ultimoPacoteNoHost = p.TransporteDoHost.UltimoPacoteDe[p.ParesNoHost[1]];
        Assert.True(ultimoPacoteNoHost > parouEm, "o último pacote do cliente calado chegou antes de ele se calar?");
        Assert.InRange(quando - ultimoPacoteNoHost, TresSegundos - 1e-6, TresSegundos + UmTick + 1e-6);
        // Visto do cliente: 3 s mais a viagem do último pacote (latência + jitter) e o tick em que o host o processa.
        Assert.InRange(quando - parouEm, TresSegundos, TresSegundos + Internet.Latencia + Internet.Jitter + 2 * UmTick);
        Assert.False(p.Partida.Jogadores[indice].Humano);
        Assert.All(p.Clientes.Where(c => c != calado), c => Assert.True(p.Partida.Jogadores[c.Indice].Humano));

        // A partida seguiu: o tempo andou, saíram pontos, e a IA mexeu o jogador que era do cliente calado.
        Assert.InRange(p.Partida.TempoDeJogo - tempoAntes, 11.9f, 12.1f);
        Assert.True(p.Partida.Estatisticas.Pontos > pontosAntes, "nenhum ponto depois que o cliente caiu");
        float andou = posicoes.Zip(posicoes.Skip(1)).Sum(par => Util.Distancia(par.First.x, par.First.y, par.Second.x, par.Second.y));
        Assert.True(andou > 3, $"a IA quase não mexeu o jogador: {andou:F1} m");
        // E os outros clientes ficam sabendo pelo instantâneo.
        foreach (var c in p.Clientes.Where(c => c != calado))
        {
            var ultimo = c.UltimoInstantaneo;
            Assert.NotNull(ultimo);
            Assert.False(ultimo.Jogadores[indice].Humano);
        }
    }

    [Fact]
    public void O_host_desenha_a_propria_partida_pela_mesma_visao_sem_atraso()
    {
        var p = new Partidona(condicoes: CondicoesDaRede.Perfeita);
        var entregues = new List<EventoNumerado>();
        for (int i = 0; i < 6 * Protocolo.TicksPorSegundo; i++)
        {
            p.Rodar(1.0 / Protocolo.TicksPorSegundo, Roteiro);
            var visao = p.Servidor.ParaDesenhar();
            Assert.NotNull(visao);
            Assert.Equal(p.Servidor.Tick, visao.Tick);
            Assert.Equal(0, visao.IndiceLocal);
            Assert.Equal(EstadoDaBola.De(p.Partida.Bola), visao.Bola);
            Assert.Equal(p.Partida.Jogadores[0].X, visao.Jogadores[0].X);
            Assert.Equal(EstadoDoPlacar.De(p.Partida.Placar), visao.Placar);
            entregues.AddRange(visao.Eventos);
        }
        Assert.Equal(p.EventosDoHost, entregues);
    }
}
