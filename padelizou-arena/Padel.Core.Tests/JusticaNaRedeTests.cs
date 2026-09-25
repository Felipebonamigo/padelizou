using Padel.Core.Rede;

namespace Padel.Core.Tests;

/// <summary>
/// A régua da justiça do online (docs/JUSTICA-NA-REDE.md). O humano simulado do CLIENTE joga só com o que o cliente
/// vê: <see cref="VisaoParaOHumano"/> monta o EstadoVisivel a partir do quadro do ClienteDaPartida (bola interpolada,
/// próprio jogador predito, os outros interpolados, e quem bateu por último pelos eventos numerados). Estes testes
/// provam que a régua não entorta: sem latência e sem atraso de interpolação o cliente vê o que o host vê e joga como o
/// local; com atraso, vê o passado, não o host. E que o host não sente a rede: joga como no local contra as mesmas
/// entradas do cliente. O QUANTO a latência piora o jogo do cliente não é teste — a suíte não fica vermelha com o
/// netcode atual: é número, medido por ferramentas/JusticaNaRede e escrito no documento.
/// </summary>
public class JusticaNaRedeTests
{
    private const float Passo = Protocolo.Passo;
    private const float UmCentimetro = 0.01f;
    /// <summary>O instantâneo quantiza a velocidade do jogador em passos de 1 cm/s: até √2 × 0,5 cm/s ≈ 0,71 cm/s de erro no plano.</summary>
    private const float ToleranciaDaVelocidadeDoJogador = 0.01f;
    /// <summary>
    /// A velocidade do PRÓPRIO jogador, predita: parte do instantâneo (a quantização acima) e passa pelo Jogador.Mover,
    /// que acelera (9 m/s²) se |desejado| &gt; |V| e freia (14 m/s²) se não. Inverter a direção na mesma rapidez — o que o
    /// humano simulado faz o tempo todo — deixa |V| = |desejado| exato no host, que freia; o instantâneo arredonda |V| e o
    /// cliente pode cair do outro lado do limiar e acelerar: (14 − 9) × 1/120 s = 4,17 cm/s num passo. O cliente não tem
    /// como saber de que lado o host estava (só vê a velocidade arredondada): é limite da predição sobre o instantâneo
    /// quantizado, não defeito. Visto: semente 11, tick 2713, Vx 2,87244 m/s no host e 2,87 no instantâneo; o host freia
    /// pra 2,75577, o predito acelera pra 2,79500 — 3,92 cm/s. Tolerância: 0,71 + 4,17 = 4,88 → 5 cm/s. Medido em 40
    /// sementes (1 a 40) × os dois atrasos destes testes, fora do limite da quadra: até 4,56 cm/s.
    /// </summary>
    private const float ToleranciaDaVelocidadePredita = 0.05f;

    /// <summary>
    /// O jogador está a menos de 1 cm de onde o Jogador.Limitar o segura: |x| = 4,7 m, |y| = 0,5 m junto à rede e 9,7 m
    /// no fundo (os números de Jogadores.cs). Ali a velocidade PREDITA não se compara com a do host: o Limitar zera a
    /// componente de uma vez, e a posição de onde a predição parte — a do instantâneo, arredondada ao milímetro, mais o
    /// erro de velocidade de <see cref="ToleranciaDaVelocidadePredita"/> levado pelos até 4 passos reaplicados: ~2 mm,
    /// medido 1,8 mm — põe o predito na parede um passo antes ou depois do host, e a velocidade desse passo difere da
    /// componente inteira. Visto: semente 13, tick 1684, X −4,60432 no host e −4,604 no instantâneo; 4 passos depois o
    /// host passa de −4,7 e para (Vx 0), o predito fica em −4,69975 (Vx −2,87 m/s). A posição segue conferida — a
    /// diferença é a do arredondamento. Medido em 40 sementes × os dois atrasos: até 2,5 % das amostras caem aqui.
    /// </summary>
    private static bool JuntoAoLimiteDaQuadra(JogadorVisivel j) =>
        4.7f - MathF.Abs(j.X) < UmCentimetro || MathF.Abs(j.Y) - 0.5f < UmCentimetro || 9.7f - MathF.Abs(j.Y) < UmCentimetro;

    /// <summary>
    /// Entre o instantâneo <paramref name="antes"/> e o seguinte, a velocidade do jogador <paramref name="k"/> no host mudou,
    /// num passo, mais do que o Jogador.Mover deixa (a maior entre aceleração e frenagem × 1/120 s = 11,7 cm/s; 1 % de folga
    /// pro arredondamento do float): foi o Jogador.Limitar parando-o no limite da quadra — a única outra coisa que mexe na
    /// velocidade de um jogador no rally.
    /// </summary>
    private static bool ParouNoLimiteDaQuadra(Dictionary<uint, EstadoVisivel> noHost, Jogador jogador, int k, uint antes)
    {
        float maiorMudanca = MathF.Max(jogador.Aceleracao, jogador.Frenagem) * Passo * 1.01f;
        for (uint t = antes + 1; t <= antes + Protocolo.TicksPorInstantaneo; t++)
            if (DiferencaDaVelocidadeDoJogador(noHost[t - 1].Jogadores[k], noHost[t].Jogadores[k]) > maiorMudanca) return true;
        return false;
    }
    /// <summary>E o efeito da bola em passos de 1/20 rad/s: até √3 × 0,025 ≈ 0,043 rad/s de erro.</summary>
    private const float ToleranciaDoEfeito = 0.05f;

    /// <summary>
    /// A partida como o host monta o 1x1 online (humanos nas vagas 0 e 2), com a vaga humana que não é medida entregue à
    /// IA Média (como faz o ServidorDaPartida com quem cai) e o time do medido com o perfil Parceiro: o medido, com um
    /// parceiro IA, contra a dupla Média. A mesma montagem vale pra partida do host e pra partida local de comparação.
    /// O jogador entregue à IA fica com o Alcance de humano (1,35 m): Alcance só se define no construtor do Jogador —
    /// a mesma limitação do ServidorDaPartida.ConferirSilencio, e igual dos dois lados da comparação.
    /// </summary>
    private static void EntregarAIA(Partida partida, int medido)
    {
        var outro = partida.Jogadores[2 - medido];
        outro.Humano = false;
        if (medido == 2)
        {
            // O time 1 é o do medido: troca os perfis pra ele ter o Parceiro e o time 0 ter a Média.
            partida.IAs[0] = new IA(0, Perfis.Medio, partida.Aleatorio);
            partida.IAs[1] = new IA(1, Perfis.Parceiro, partida.Aleatorio);
            partida.Jogadores[3].Velocidade = Perfis.Parceiro.Velocidade;
        }
        foreach (var j in partida.JogadoresDoTime(1 - medido / 2)) j.Velocidade = Perfis.Medio.Velocidade;
    }

    private static Partida PartidaLocal(uint semente, int medido)
    {
        var partida = new Partida(new OpcoesDaPartida { Semente = semente, Dificuldade = Dificuldade.Medio, Humanos = [true, false, true, false] });
        EntregarAIA(partida, medido);
        return partida;
    }

    /// <summary>Frente a frente: os dois humanos com o mesmo parceiro (perfil Parceiro); sem isso o do time 1 teria a IA da dificuldade. Igual à da ferramenta.</summary>
    private static void ParceirosIguais(Partida partida)
    {
        partida.IAs[1] = new IA(1, Perfis.Parceiro, partida.Aleatorio);
        partida.Jogadores[3].Velocidade = Perfis.Parceiro.Velocidade;
    }

    private static HumanoSimulado NovoHumano(int indice, PerfilDeHumano perfil, uint semente) => new(indice, perfil, new Aleatorio(semente * 31u + 7u));

    /// <summary>
    /// Host + um cliente (vaga 2) numa RedeEmMemoria. O humano simulado do cliente vê só o quadro dele, pela
    /// VisaoParaOHumano. Normal: a vaga do host vai pra IA (EntregarAIA) e só o cliente é medido. frenteAFrente: um humano
    /// simulado em cada ponta — o do host vendo a Partida como o host vê (EstadoVisivel.De, como a SessaoHost do Godot) —,
    /// os dois com parceiro IA Parceiro, como o frente a frente da ferramenta; a vaga do cliente continua humana, então
    /// a entrada dele entra no jogo do host.
    /// A cada passo: a rede anda 1/120 s, o host dá um tick, o cliente dá um tick e desenha um quadro.
    /// </summary>
    private sealed class Mesa
    {
        private readonly VisaoParaOHumano _visao = new();
        private readonly HumanoSimulado? _noHost;
        private readonly HumanoSimulado _noCliente;

        public Mesa(uint semente, CondicoesDaRede condicoes, float atraso, PerfilDeHumano perfil, bool frenteAFrente = false, uint sementeDaRede = 99)
        {
            Rede = new RedeEmMemoria(sementeDaRede, condicoes);
            var host = Rede.NovoPonto();
            Servidor = new ServidorDaPartida(host, "Host", new OpcoesDaPartida { Semente = semente, Dificuldade = Dificuldade.Medio });
            var ponto = Rede.NovoPonto();
            Rede.Conectar(ponto, host);
            Cliente = new ClienteDaPartida(ponto, "Cliente", atraso);
            for (int i = 0; i < 3 * Protocolo.TicksPorSegundo && Cliente.Fase != FaseDoCliente.NaSala; i++) Avancar(Entrada.Vazia, Entrada.Vazia);
            Assert.Equal(FaseDoCliente.NaSala, Cliente.Fase);
            Assert.Equal(2, Cliente.Indice);
            Partida = Servidor.Iniciar();
            if (frenteAFrente)
            {
                ParceirosIguais(Partida);
                _noHost = NovoHumano(0, perfil, semente);
            }
            else EntregarAIA(Partida, medido: 2);
            _noCliente = NovoHumano(2, perfil, semente);
        }

        public RedeEmMemoria Rede { get; }
        public ServidorDaPartida Servidor { get; }
        public ClienteDaPartida Cliente { get; }
        public Partida Partida { get; }
        public VisaoDaPartida? Quadro { get; private set; }
        public EstadoVisivel? Visto { get; private set; }

        public void Passo()
        {
            var doHost = _noHost is HumanoSimulado noHost ? noHost.Decidir(EstadoVisivel.De(Partida), JusticaNaRedeTests.Passo) : Entrada.Vazia;
            var doCliente = Visto is EstadoVisivel visto ? _noCliente.Decidir(visto, JusticaNaRedeTests.Passo) : Entrada.Vazia;
            Avancar(doHost, doCliente);
        }

        private void Avancar(Entrada doHost, Entrada doCliente)
        {
            Rede.AvancarRelogio(Protocolo.Passo);
            Servidor.Passo(doHost);
            Cliente.Passo(doCliente);
            if (Cliente.Fase == FaseDoCliente.Jogando && Cliente.ParaDesenhar() is VisaoDaPartida quadro)
            {
                Quadro = quadro;
                Visto = _visao.Ver(quadro);
            }
        }
    }

    /// <summary>O que se mede do jogador medido, na partida de verdade (a do host, ou a local).</summary>
    private sealed class Medicao
    {
        /// <summary>Tempo no balanço no contato menos o momento ideal (0,12 s): negativo = apertou tarde.</summary>
        public List<float> Dt { get; } = [];
        public int Bons, Pontos, Ganhos;

        public void Acompanhar(Partida partida, int medido)
        {
            var jogador = partida.Jogadores[medido];
            partida.Evento += ev =>
            {
                if (ev.Tipo is TipoDeEventoDaPartida.Ponto or TipoDeEventoDaPartida.Game or TipoDeEventoDaPartida.Set or TipoDeEventoDaPartida.Partida)
                {
                    Pontos++;
                    if (ev.Time == jogador.Time) Ganhos++;
                }
                if (ev.Tipo == TipoDeEventoDaPartida.Golpe && ev.Jogador == jogador && ev.Golpe != TipoDeGolpe.Saque)
                {
                    Dt.Add(jogador.TempoNoBalanco - Jogador.MomentoIdealDoBalanco);
                    if (partida.UltimoErroDoHumano < 0.5f) Bons++;
                }
            };
        }

        public float FracaoBons => (float)Bons / Math.Max(1, Dt.Count);
        public float FracaoGanhos => (float)Ganhos / Math.Max(1, Pontos);
    }

    private static float DistanciaDaBola(EstadoVisivel a, EstadoVisivel b)
    {
        float dx = a.BolaX - b.BolaX, dy = a.BolaY - b.BolaY, dz = a.BolaZ - b.BolaZ;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static float DiferencaDaVelocidadeDaBola(EstadoVisivel a, EstadoVisivel b)
    {
        float dx = a.BolaVx - b.BolaVx, dy = a.BolaVy - b.BolaVy, dz = a.BolaVz - b.BolaVz;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static float DiferencaDoEfeitoDaBola(EstadoVisivel a, EstadoVisivel b)
    {
        float dx = a.BolaWx - b.BolaWx, dy = a.BolaWy - b.BolaWy, dz = a.BolaWz - b.BolaWz;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static float DistanciaDoJogador(JogadorVisivel a, JogadorVisivel b) => Util.Distancia(a.X, a.Y, b.X, b.Y);

    private static float DiferencaDaVelocidadeDoJogador(JogadorVisivel a, JogadorVisivel b) => Util.Distancia(a.Vx, a.Vy, b.Vx, b.Vy);

    /// <summary>
    /// Os ticks do host em que a bola quicou, bateu na parede ou na rede. O relógio do cliente desenha uma fração ínfima
    /// de tick fora do tick inteiro (~1e-5 tick, arredondamento do relógio), e a bola dele anda essa fração pela física.
    /// Se a bola toca o chão, a parede ou a rede justo nessa fração, o cliente já mostra a velocidade e o efeito de
    /// depois do contato — é o instante que ele desenha, não um defeito da régua —, e a comparação com o tick inteiro do
    /// host não diz nada. Visto de verdade: com o efeito zerado de propósito o jogo mudou e caiu num quique assim
    /// ("a velocidade da bola vista errou 10.583 m/s"). Posição não precisa disso: é contínua no contato.
    /// </summary>
    private static HashSet<uint> ContatosDaBola(ServidorDaPartida servidor)
    {
        var ticks = new HashSet<uint>();
        servidor.EventoRegistrado += e =>
        {
            if (e.Tipo is TipoDeEventoDaPartida.Quique or TipoDeEventoDaPartida.Parede or TipoDeEventoDaPartida.Rede) ticks.Add(e.Tick);
        };
        return ticks;
    }

    /// <summary>
    /// A bola tocou chão, parede ou rede no host entre o instantâneo anterior ao tick desenhado (exclusive) e ele. O
    /// cliente simula esse trecho a partir do marco, que é quantizado (1 mm, 1 cm/s), e a Bola acha o contato no fim de
    /// cada subpasso de 1/240 s, sem interpolar o instante: um milímetro de diferença na partida põe o contato um subpasso
    /// antes ou depois do host, e a bola anda meio tick com a velocidade de depois (ou de antes) do contato. É limite da
    /// simulação sobre o marco quantizado, não defeito (o cliente não tem o estado exato). Visto: semente 9, quique no tick
    /// 4026 — o cliente, a partir do instantâneo 4024, quica um subpasso antes; (16,24 − 11,26) m/s / 240 = 2,1 cm em y e
    /// 3,9 m/s / 240 = 1,6 cm em z: 2,58 cm. Semente 8, vidro de fundo no 3534: 6,37 cm. Em 40 sementes (1 a 40), 7 passam
    /// de 1 cm por isso (até 6,37 cm) e 3 estouram a tolerância do efeito (até 0,31 rad/s acima); fora desse trecho, a
    /// bola fica a até 0,15 cm e o efeito dentro da tolerância em todas — e o trecho tira no máximo 78 de ~6 960 amostras.
    /// O mesmo que o teste de cima faz entre instantâneos: lá, qualquer evento do host desde o último instantâneo tira a amostra.
    /// </summary>
    private static bool ContatoDesdeOInstantaneo(HashSet<uint> contatos, uint desenhado)
    {
        for (uint t = desenhado - desenhado % Protocolo.TicksPorInstantaneo + 1; t <= desenhado; t++)
            if (contatos.Contains(t)) return true;
        return false;
    }

    /// <summary>Time e lado de cada jogador: o humano simulado sabe por eles quem é parceiro, quem é adversário e pra que lado bater.</summary>
    private static void MesmosTimesELados(EstadoVisivel esperado, EstadoVisivel visto, uint tick)
    {
        for (int k = 0; k < Protocolo.Jogadores; k++)
        {
            JogadorVisivel e = esperado.Jogadores[k], v = visto.Jogadores[k];
            Assert.True(e.Time == v.Time && e.Lado == v.Lado,
                $"tick {tick}, jogador {k}: host (time {e.Time}, lado {e.Lado}), cliente (time {v.Time}, lado {v.Lado})");
        }
    }

    private static void MesmaLeitura(EstadoVisivel esperado, EstadoVisivel visto, uint tick)
    {
        Assert.True(esperado.TimeDoUltimoGolpe == visto.TimeDoUltimoGolpe && esperado.TipoDoUltimoGolpe == visto.TipoDoUltimoGolpe
            && esperado.QuicouDepoisDoUltimoGolpe == visto.QuicouDepoisDoUltimoGolpe,
            $"tick {tick}: host leu ({esperado.TimeDoUltimoGolpe}, {esperado.TipoDoUltimoGolpe}, quicou {esperado.QuicouDepoisDoUltimoGolpe}), " +
            $"cliente ({visto.TimeDoUltimoGolpe}, {visto.TipoDoUltimoGolpe}, quicou {visto.QuicouDepoisDoUltimoGolpe})");
    }

    /// <summary>
    /// Ida e volta 0 e atraso de interpolação 0: o que o humano do cliente vê é o EstadoVisivel da Partida do host.
    /// Três ressalvas que são da natureza do online e não da régua, e por isso o teste as separa:
    /// - o instantâneo sai a 30 Hz: no tick em que um chega (o quadro sai dele), tudo bate — bola (posição, velocidade e
    ///   efeito), os outros jogadores (posição e velocidade), estado, sacador, caixa e a leitura de quem bateu; entre dois
    ///   instantâneos, a bola em voo (sem golpe, quique ou ponto novo no host desde o último instantâneo) também bate,
    ///   efeito incluído, porque o cliente simula com a mesma física;
    /// - o próprio jogador é PREDITO: está onde o host vai pô-lo ao aplicar a entrada que o cliente acabou de mandar —
    ///   um tick depois, mesmo a 0 ms (o passo é a granularidade). Compara-se com o host no tick seguinte, no rally —
    ///   posição e velocidade (o humano simulado copia a velocidade do próprio jogador pra prever o corpo). A predição parte
    ///   do instantâneo quantizado e passa pelos limiares do Jogador.Mover: a velocidade tem a tolerância própria
    ///   (<see cref="ToleranciaDaVelocidadePredita"/>) e não se compara junto ao limite da quadra (<see cref="JuntoAoLimiteDaQuadra"/>);
    /// - o tick desenhado sai ~1e-5 tick fora do inteiro: velocidade e efeito da bola se conferem um tick depois, e só
    ///   se a bola não tocou chão, parede ou rede nesse passo (<see cref="ContatosDaBola"/>).
    /// Time e lado de cada jogador batem em todo quadro.
    /// </summary>
    [Fact]
    public void Sem_latencia_e_sem_atraso_de_interpolacao_o_humano_do_cliente_ve_o_que_o_host_ve()
    {
        var mesa = new Mesa(semente: 11, CondicoesDaRede.Perfeita, atraso: 0f, PerfilDeHumano.Avancado);
        uint ultimoEventoDoHost = 0;
        mesa.Servidor.EventoRegistrado += e => ultimoEventoDoHost = e.Tick;
        var contatos = ContatosDaBola(mesa.Servidor);
        (uint Tick, EstadoVisivel Host, EstadoVisivel Visto, bool Fresco)? aConferir = null;
        int velocidadesConferidas = 0;
        var estadoNoHost = new Dictionary<uint, EstadoDaPartida>();
        JogadorVisivel? preditoAntes = null;
        uint tickDoPredito = 0;
        int frescos = 0, entreInstantaneos = 0, preditos = 0, velocidadesPreditas = 0, leituras = 0, quadros = 0;
        float piorBola = 0, piorBolaEntre = 0, piorVelocidade = 0, piorEfeito = 0;
        float piorOutros = 0, piorVelocidadeDosOutros = 0, piorPredito = 0, piorVelocidadePredita = 0;
        uint tickDaPiorVelocidadePredita = 0;

        for (int i = 0; i < 90 * Protocolo.TicksPorSegundo; i++)
        {
            mesa.Passo();
            uint tick = mesa.Servidor.Tick;
            var host = EstadoVisivel.De(mesa.Partida);
            estadoNoHost[tick] = host.Estado;
            if (aConferir is { } c && !contatos.Contains(c.Tick + 1))
            {
                if (c.Fresco) piorVelocidade = MathF.Max(piorVelocidade, DiferencaDaVelocidadeDaBola(c.Host, c.Visto));
                piorEfeito = MathF.Max(piorEfeito, DiferencaDoEfeitoDaBola(c.Host, c.Visto));
                velocidadesConferidas++;
            }
            aConferir = null;
            if (mesa.Visto is not EstadoVisivel visto || mesa.Quadro is not VisaoDaPartida quadro) continue;
            MesmosTimesELados(host, visto, tick);
            quadros++;

            // O próprio jogador, predito no passo anterior, contra onde o host o pôs neste tick (rally seguido nos dois).
            bool rallySeguido = tick > 6;
            for (uint t = tick - 6; rallySeguido && t <= tick; t++) rallySeguido = estadoNoHost.TryGetValue(t, out var e) && e == EstadoDaPartida.Rally;
            if (preditoAntes is JogadorVisivel predito && tickDoPredito + 1 == tick && rallySeguido)
            {
                var noHost = host.Jogadores[2];
                piorPredito = MathF.Max(piorPredito, DistanciaDoJogador(predito, noHost));
                preditos++;
                if (!JuntoAoLimiteDaQuadra(noHost))
                {
                    float erro = DiferencaDaVelocidadeDoJogador(predito, noHost);
                    if (erro > piorVelocidadePredita) (piorVelocidadePredita, tickDaPiorVelocidadePredita) = (erro, tick);
                    velocidadesPreditas++;
                }
            }
            preditoAntes = visto.Jogadores[2];
            tickDoPredito = tick;

            uint ultimoInstantaneo = tick - tick % Protocolo.TicksPorInstantaneo;
            if (quadro.TickDoEstado == tick)
            {
                frescos++;
                Assert.Equal(host.Estado, visto.Estado);
                Assert.Equal(host.IndiceDoSacador, visto.IndiceDoSacador);
                Assert.Equal(host.CaixaDoSaque, visto.CaixaDoSaque);
                Assert.Equal(host.BolaEmJogo, visto.BolaEmJogo);
                piorBola = MathF.Max(piorBola, DistanciaDaBola(host, visto));
                aConferir = (tick, host, visto, true);
                foreach (int k in new[] { 0, 1, 3 })
                {
                    piorOutros = MathF.Max(piorOutros, DistanciaDoJogador(host.Jogadores[k], visto.Jogadores[k]));
                    piorVelocidadeDosOutros = MathF.Max(piorVelocidadeDosOutros, DiferencaDaVelocidadeDoJogador(host.Jogadores[k], visto.Jogadores[k]));
                }
                if (host.Estado == EstadoDaPartida.Rally)
                {
                    MesmaLeitura(host, visto, tick);
                    leituras++;
                }
            }
            else if (ultimoEventoDoHost <= ultimoInstantaneo)
            {
                piorBolaEntre = MathF.Max(piorBolaEntre, DistanciaDaBola(host, visto));
                aConferir = (tick, host, visto, false);
                entreInstantaneos++;
            }
        }

        Assert.True(mesa.Partida.Estatisticas.Pontos >= 8, $"poucos pontos em 90 s: {mesa.Partida.Estatisticas.Pontos}");
        Assert.True(mesa.Partida.Jogadores[2].Golpes >= 8, $"o humano do cliente bateu só {mesa.Partida.Jogadores[2].Golpes} vezes");
        Assert.True(frescos > 2000, $"poucos quadros saídos do instantâneo do próprio tick: {frescos}");
        Assert.True(entreInstantaneos > 4000, $"poucas amostras entre instantâneos: {entreInstantaneos}");
        Assert.True(preditos > 2000 && velocidadesPreditas > 2000, $"poucas amostras do jogador predito: {preditos} (velocidade fora do limite da quadra: {velocidadesPreditas})");
        Assert.True(leituras > 500, $"poucas leituras no rally: {leituras}");
        Assert.True(quadros > 8000, $"poucos quadros com time e lado conferidos: {quadros}");
        Assert.True(velocidadesConferidas > 6000, $"poucas amostras de velocidade e efeito da bola: {velocidadesConferidas}");
        Assert.True(piorBola < UmCentimetro, $"a bola vista ficou a {piorBola * 100:F2} cm da do host");
        Assert.True(piorVelocidade < 0.05f, $"a velocidade da bola vista errou {piorVelocidade:F3} m/s");
        Assert.True(piorEfeito < ToleranciaDoEfeito, $"o efeito da bola vista errou {piorEfeito:F3} rad/s (no tick do instantâneo ou entre dois)");
        Assert.True(piorBolaEntre < UmCentimetro, $"entre instantâneos, a bola vista ficou a {piorBolaEntre * 100:F2} cm da do host");
        Assert.True(piorOutros < UmCentimetro, $"os outros jogadores ficaram a {piorOutros * 100:F2} cm dos do host");
        Assert.True(piorVelocidadeDosOutros < ToleranciaDaVelocidadeDoJogador, $"a velocidade dos outros jogadores errou {piorVelocidadeDosOutros:F3} m/s");
        Assert.True(piorPredito < UmCentimetro, $"o jogador predito ficou a {piorPredito * 100:F2} cm de onde o host o pôs");
        Assert.True(piorVelocidadePredita < ToleranciaDaVelocidadePredita, $"a velocidade do jogador predito errou {piorVelocidadePredita:F3} m/s (tick {tickDaPiorVelocidadePredita})");
    }

    /// <summary>
    /// Com o atraso de interpolação padrão (100 ms = 12 ticks) e rede perfeita, o humano do cliente vê o mundo do host
    /// 12 ticks atrás — a bola é a interpolada (a do quadro), não a do host agora — e a si mesmo agora (predito). É
    /// exatamente a assimetria que a ferramenta mede: o corpo no presente, a bola no passado. Estado, sacador, time e
    /// lado de cada um e a leitura de quem bateu também são os do tick desenhado, em todo tick: os eventos corrigem o
    /// estado que o instantâneo anterior ao tick desenhado ainda não tinha. A bola do tick desenhado bate em posição e
    /// em efeito, fora do trecho em que ela tocou chão, parede ou rede (<see cref="ContatoDesdeOInstantaneo"/>). Os outros jogadores se comparam, em posição, entre dois instantâneos do rally: a interpolação linear
    /// do cliente não segue a parada seca do fim do ponto (o host congela todo mundo com a velocidade que tinha —
    /// medido: até 3 cm no último tick do rally) nem o teletransporte do ponto novo. A velocidade deles não se compara
    /// aqui: interpolada entre dois instantâneos, ela anda em rampa onde a do host muda de uma vez (medido: até 1,2 m/s
    /// no arranque); a velocidade exata é o teste de cima, no tick do instantâneo.
    /// A reta entre dois instantâneos (T = 4 ticks = 1/30 s) erra, com a aceleração que o Jogador.Mover deixa (até
    /// 14 m/s²), no máximo a·T²/8 = 14 × (1/30)² / 8 = 1,9 mm, mais o arredondamento do instantâneo (√2 × 0,5 mm) —
    /// medido em 40 sementes (1 a 40): até 2,6 mm. A parada no limite da quadra, não: o Jogador.Limitar zera a componente
    /// da velocidade num passo, e uma quina no meio do intervalo tira a reta do caminho em até v⊥·T/4 = v⊥ × 1/120 s (4,8
    /// cm a 5,8 m/s, a IA Parceiro). É limite da interpolação linear, não defeito: o cliente desenha a reta entre os dois
    /// instantâneos por construção. Visto: semente 12, tick 5825, o jogador 0 recuando a 2,78 m/s para em y = 9,70; a reta
    /// entre 5824 e 5828 o põe em 9,684 — 1,6 cm. Medido em 40 sementes: até 2,45 cm, em no máximo 16 de ~13 000
    /// comparações. Esse intervalo fica de fora (<see cref="ParouNoLimiteDaQuadra"/>), como a parada do fim do ponto, e
    /// o teste confere que ele é raro — menos de 1 % das comparações: o filtro não pode engolir o teste.
    /// </summary>
    [Fact]
    public void Com_atraso_de_interpolacao_o_humano_do_cliente_ve_a_bola_interpolada_e_nao_a_do_host()
    {
        var mesa = new Mesa(semente: 12, CondicoesDaRede.Perfeita, atraso: 0.1f, PerfilDeHumano.Avancado);
        var contatos = ContatosDaBola(mesa.Servidor);
        int efeitosConferidos = 0, bolasConferidas = 0;
        var noHost = new Dictionary<uint, EstadoVisivel>();
        EstadoVisivel? antes = null;
        uint tickAntes = 0;
        int amostras = 0, noRally = 0, preditos = 0, velocidadesPreditas = 0, outrosNoRally = 0, outrosConferidos = 0, outrosParados = 0;
        float piorNoPassado = 0, piorOutros = 0, piorPredito = 0, piorVelocidadePredita = 0;
        string ondeOsOutros = "", ondeAVelocidadePredita = "";
        float piorFolgaDoEfeito = float.NegativeInfinity;
        string piorEfeito = "";
        double somaAteOHostAgora = 0;

        for (int i = 0; i < 60 * Protocolo.TicksPorSegundo; i++)
        {
            mesa.Passo();
            uint tick = mesa.Servidor.Tick;
            var host = EstadoVisivel.De(mesa.Partida);
            noHost[tick] = host;
            if (antes is EstadoVisivel predito && tickAntes + 1 == tick && host.Estado == EstadoDaPartida.Rally
                && noHost.TryGetValue(tick - 6, out var h6) && h6.Estado == EstadoDaPartida.Rally)
            {
                piorPredito = MathF.Max(piorPredito, DistanciaDoJogador(predito.Jogadores[2], host.Jogadores[2]));
                preditos++;
                if (!JuntoAoLimiteDaQuadra(host.Jogadores[2]))
                {
                    float erro = DiferencaDaVelocidadeDoJogador(predito.Jogadores[2], host.Jogadores[2]);
                    if (erro > piorVelocidadePredita) (piorVelocidadePredita, ondeAVelocidadePredita) = (erro, $"tick {tick}");
                    velocidadesPreditas++;
                }
            }
            antes = mesa.Visto;
            tickAntes = tick;
            if (tick < 2 * Protocolo.TicksPorSegundo || mesa.Visto is not EstadoVisivel visto || mesa.Quadro is not VisaoDaPartida quadro) continue;

            // A bola que o humano vê é a do quadro, e o quadro é de 12 ticks atrás.
            Assert.Equal(quadro.Bola.X, visto.BolaX);
            Assert.Equal(quadro.Bola.Y, visto.BolaY);
            Assert.Equal(quadro.Bola.Z, visto.BolaZ);
            uint desenhado = (uint)Math.Round(quadro.Tick);
            Assert.Equal(tick - 12, desenhado);
            var passado = noHost[desenhado];
            amostras++;
            // A bola do quadro é simulada desde o último marco (o instantâneo antes do tick desenhado, ou um golpe depois
            // dele) — quantizado. Contato com chão, parede ou rede nesse trecho fica de fora: ver ContatoDesdeOInstantaneo.
            bool contatoNoTrecho = ContatoDesdeOInstantaneo(contatos, desenhado);
            if (!contatoNoTrecho)
            {
                piorNoPassado = MathF.Max(piorNoPassado, DistanciaDaBola(passado, visto));
                bolasConferidas++;
            }
            // O efeito: a quantização, e depois de um quique ou parede — o efeito sai da velocidade no impacto — até ~0,1 %
            // dele (medido: 0,25 rad/s num efeito de 243 rad/s). Tolerância: a quantização + 0,5 % do efeito do host.
            // O tick desenhado é fracionário (desenhado ± ~1e-5): contato da bola no passo que sai dele também fica de
            // fora (ver ContatosDaBola), além do trecho simulado.
            if (!contatoNoTrecho && !contatos.Contains(desenhado + 1))
            {
                float erroDoEfeito = DiferencaDoEfeitoDaBola(passado, visto);
                float efeitoNoHost = MathF.Sqrt(passado.BolaWx * passado.BolaWx + passado.BolaWy * passado.BolaWy + passado.BolaWz * passado.BolaWz);
                float folgaDoEfeito = erroDoEfeito - (ToleranciaDoEfeito + 0.005f * efeitoNoHost);
                if (folgaDoEfeito > piorFolgaDoEfeito)
                {
                    piorFolgaDoEfeito = folgaDoEfeito;
                    piorEfeito = $"tick {desenhado}: efeito {efeitoNoHost:F2} rad/s no host, erro {erroDoEfeito:F3} rad/s";
                }
                efeitosConferidos++;
            }
            MesmosTimesELados(passado, visto, desenhado);
            Assert.Equal(passado.Estado, visto.Estado);
            Assert.Equal(passado.BolaEmJogo, visto.BolaEmJogo);
            if (passado.Estado == EstadoDaPartida.Saque) Assert.Equal(passado.IndiceDoSacador, visto.IndiceDoSacador);
            if (passado.Estado == EstadoDaPartida.Rally) MesmaLeitura(passado, visto, desenhado);
            uint instantaneoAntes = desenhado - desenhado % Protocolo.TicksPorInstantaneo;
            if (noHost[instantaneoAntes].Estado == EstadoDaPartida.Rally && noHost[instantaneoAntes + Protocolo.TicksPorInstantaneo].Estado == EstadoDaPartida.Rally)
            {
                foreach (int k in new[] { 0, 1, 3 })
                {
                    if (ParouNoLimiteDaQuadra(noHost, mesa.Partida.Jogadores[k], k, instantaneoAntes)) { outrosParados++; continue; }
                    float erro = DistanciaDoJogador(passado.Jogadores[k], visto.Jogadores[k]);
                    if (erro > piorOutros) (piorOutros, ondeOsOutros) = (erro, $"tick {desenhado}, jogador {k}");
                    outrosConferidos++;
                }
                outrosNoRally++;
            }
            if (host.Estado == EstadoDaPartida.Rally && passado.Estado == EstadoDaPartida.Rally)
            {
                somaAteOHostAgora += DistanciaDaBola(host, visto);
                noRally++;
            }
        }

        Assert.True(amostras > 6000 && bolasConferidas > 6000 && noRally > 2000 && preditos > 2000 && velocidadesPreditas > 2000 && outrosNoRally > 2000 && efeitosConferidos > 6000,
            $"amostras: {amostras} (bola sem contato no trecho simulado: {bolasConferidas}), no rally: {noRally}, preditos: {preditos} " +
            $"(velocidade fora do limite da quadra: {velocidadesPreditas}), outros no rally: {outrosNoRally}, efeitos: {efeitosConferidos}");
        Assert.True(outrosParados * 100 < outrosConferidos,
            $"{outrosParados} comparações dos outros ficaram de fora por parada no limite da quadra, contra {outrosConferidos} conferidas: passou de 1 %");
        Assert.True(piorNoPassado < UmCentimetro, $"a bola vista ficou a {piorNoPassado * 100:F2} cm da do host no tick desenhado");
        Assert.True(piorOutros < UmCentimetro, $"os outros jogadores ficaram a {piorOutros * 100:F2} cm dos do host no tick desenhado ({ondeOsOutros})");
        double mediaAteOHostAgora = somaAteOHostAgora / noRally;
        Assert.True(mediaAteOHostAgora > 0.5, $"a bola vista devia estar longe da do host agora (100 ms de voo); ficou a {mediaAteOHostAgora:F2} m em média");
        Assert.True(piorFolgaDoEfeito < 0, $"o efeito da bola vista passou da tolerância — {piorEfeito}");
        Assert.True(piorPredito < UmCentimetro, $"o próprio jogador devia ser o de agora (predito); ficou a {piorPredito * 100:F2} cm");
        Assert.True(piorVelocidadePredita < ToleranciaDaVelocidadePredita, $"a velocidade do próprio jogador devia ser a de agora (predita); errou {piorVelocidadePredita:F3} m/s ({ondeAVelocidadePredita})");
    }

    /// <summary>
    /// A 0 ms e sem atraso de interpolação, o humano simulado no cliente joga como o mesmo humano no local (a mesma
    /// montagem, as mesmas sementes). "Parecido" com a variância medida, não com um palpite:
    /// medido (Intermediário, 6 partidas locais nesta montagem): σ(Δt) = 63–69 ms; golpes bons ≈ 41 %; pontos ganhos
    /// ≈ 57 %. As duas amostras são independentes (as partidas divergem no primeiro tick diferente), então o erro padrão
    /// da diferença é σ·√(1/n₁ + 1/n₂) pra médias e √(p(1−p)·(1/n₁ + 1/n₂)) pra proporções. Com pelo menos 200 golpes e
    /// 200 pontos de cada lado (cada braço joga partidas até ter os dois; dá 3 a 5 partidas, conforme as sementes):
    /// - Δt médio: 3 × 69 × √(2/200) = 20,7 ms, mais 1 tick (8,3 ms) que o online sempre tem — a entrada do cliente é
    ///   aplicada no tick seguinte do host — = 29 ms;
    /// - golpes bons: 3 × √(0,41 × 0,59 × 2/200) = 0,15;
    /// - pontos ganhos: 3 × √(0,57 × 0,43 × 2/200) = 0,15 (o placar é a régua mais frouxa: a IA Média erra sozinha).
    /// Um cliente que visse a bola 100 ms no passado (o atraso de interpolação padrão) aperta ~100 ms tarde: Δt médio
    /// perto de −100 ms e golpes bons abaixo de 10 % (docs/JUSTICA-NA-REDE.md) — muito fora das duas primeiras.
    /// Conferido com sete faixas de sementes (1000, 2000, …, 7000): todas chegam ao piso em 3 a 5 partidas e ficam
    /// dentro das três tolerâncias (pior caso: Δt −10,7 contra −27,0 ms; golpes bons a 3,1 pp; pontos a 7,1 pp).
    /// </summary>
    [Fact]
    public void A_zero_ms_e_sem_atraso_de_interpolacao_o_humano_do_cliente_joga_como_o_local()
    {
        const float ToleranciaDoDt = 0.029f, ToleranciaDosBons = 0.15f, ToleranciaDosPontos = 0.15f;
        const int AmostraMinima = 200;
        const uint PrimeiraSemente = 1000;
        // atalho: teto de 12 partidas por braço, o triplo das ~4 que a amostra pede. Se não bastar, o humano parou de
        // bater ou os pontos ficaram curtos demais: o assert do piso diz quanto faltou, e a saída é olhar o porquê.
        const int MaximoDePartidas = 12;
        var perfil = PerfilDeHumano.Intermediario;
        var local = new Medicao();
        var cliente = new Medicao();
        int locaisSemFim = 0, onlineSemFim = 0, partidasLocais = 0, partidasOnline = 0;
        // Cada braço joga partidas (sementes PrimeiraSemente, +1, …) até ter o piso de golpes E de pontos que a conta
        // da tolerância pede — e não um número fixo delas: 4 partidas dão de 167 a 262 golpes conforme as sementes, e
        // um número fixo deixava o teste passar ou não pelo tamanho da amostra, não pela régua.
        static bool FaltaAmostra(Medicao m) => m.Dt.Count < AmostraMinima || m.Pontos < AmostraMinima;
        // Os dois braços não dividem nada (cada um com as suas partidas, humanos e medição): rodam em paralelo — é
        // o teste mais longo da suíte (~7 s de CPU cada braço em Debug) e a suíte espera por ele.
        Parallel.Invoke(
            () =>
            {
                for (uint semente = PrimeiraSemente; FaltaAmostra(local) && partidasLocais < MaximoDePartidas; semente++, partidasLocais++)
                {
                    var partida = PartidaLocal(semente, medido: 2);
                    local.Acompanhar(partida, 2);
                    var humano = NovoHumano(2, perfil, semente);
                    var entradas = new Entrada[4];
                    while (!partida.Acabou && partida.TempoDeJogo < 3600)
                    {
                        entradas[2] = humano.Decidir(EstadoVisivel.De(partida), Passo);
                        partida.Avancar(Passo, entradas);
                    }
                    if (!partida.Acabou) locaisSemFim++;
                }
            },
            () =>
            {
                for (uint semente = PrimeiraSemente; FaltaAmostra(cliente) && partidasOnline < MaximoDePartidas; semente++, partidasOnline++)
                {
                    var mesa = new Mesa(semente, CondicoesDaRede.Perfeita, atraso: 0f, perfil);
                    cliente.Acompanhar(mesa.Partida, 2);
                    while (!mesa.Partida.Acabou && mesa.Partida.TempoDeJogo < 3600) mesa.Passo();
                    if (!mesa.Partida.Acabou) onlineSemFim++;
                }
            });

        Assert.True(locaisSemFim == 0 && onlineSemFim == 0, $"partidas que não terminaram: {locaisSemFim} locais, {onlineSemFim} online");
        Assert.True(!FaltaAmostra(local) && !FaltaAmostra(cliente),
            $"em {MaximoDePartidas} partidas: golpes local {local.Dt.Count}, cliente {cliente.Dt.Count}; pontos local {local.Pontos}, cliente {cliente.Pontos} — a conta da tolerância pede {AmostraMinima} de cada");
        float dtLocal = local.Dt.Average(), dtCliente = cliente.Dt.Average();
        Assert.True(MathF.Abs(dtCliente - dtLocal) < ToleranciaDoDt, $"Δt médio: local {dtLocal * 1000:F1} ms, cliente {dtCliente * 1000:F1} ms");
        Assert.True(MathF.Abs(cliente.FracaoBons - local.FracaoBons) < ToleranciaDosBons, $"golpes bons: local {local.FracaoBons:P1}, cliente {cliente.FracaoBons:P1}");
        Assert.True(MathF.Abs(cliente.FracaoGanhos - local.FracaoGanhos) < ToleranciaDosPontos, $"pontos ganhos: local {local.FracaoGanhos:P1}, cliente {cliente.FracaoGanhos:P1}");
    }

    /// <summary>
    /// O host não sente a rede. Frente a frente a 150 ms com 2 % de perda: um humano simulado no host e outro no
    /// cliente, e a vaga do cliente continua humana — a entrada dele, atrasada e com perda, entra na Partida do host. O
    /// teste grava a entrada que o host aplicou à vaga do cliente em cada tick (ServidorDaPartida.EntradaAplicada) e joga
    /// de novo, numa Partida local com a mesma montagem, o mesmo humano do host contra essa gravação: tem que dar a mesma
    /// partida, bit a bit. A rede muda o jogo, mas só pelo que o cliente faz (e quando a entrada dele chega); a entrada
    /// do próprio host não atrasa, e nada mais da rede mexe na Partida dele. Trava que uma compensação de latência que
    /// mude a Partida do host por outro caminho — a saída (i) do documento, que refaz o golpe do cliente "no passado" —
    /// só entre de propósito, reescrevendo este teste, e nunca sem querer.
    /// Visto falhar: com a entrada do host um tick atrasada no ServidorDaPartida; com o balanço do cliente adiantado um
    /// tick pelo host ao receber o aperto (um arremedo da saída (i)); e, de controle, com a gravação do cliente trocada
    /// por entrada vazia (a partida muda: a igualdade não é de graça).
    /// </summary>
    [Fact]
    public void O_humano_no_host_joga_como_no_local_contra_as_mesmas_entradas_do_cliente_a_150_ms()
    {
        const uint semente = 1003;
        const int ticks = 120 * Protocolo.TicksPorSegundo;
        var perfil = PerfilDeHumano.Avancado;
        var mesa = new Mesa(semente, new CondicoesDaRede { Latencia = 0.075, Jitter = 0.0075, Perda = 0.02 }, atraso: 0.1f, perfil, frenteAFrente: true);
        var doCliente = new List<Entrada>(ticks);
        mesa.Servidor.EntradaAplicada += (indice, entrada) =>
        {
            if (indice == 2) doCliente.Add(entrada);
        };
        for (int i = 0; i < ticks; i++) mesa.Passo();

        // O evento só sai pra jogador humano: uma entrada por tick prova que a vaga do cliente não virou IA no caminho.
        Assert.Equal(ticks, doCliente.Count);
        Assert.True(mesa.Rede.PacotesPerdidos > 0, "a rede não perdeu nada");
        Assert.True(mesa.Partida.Jogadores[2].Golpes >= 8, $"o humano do cliente bateu só {mesa.Partida.Jogadores[2].Golpes} vezes: a entrada dele tem que estar no jogo");

        var partida = new Partida(new OpcoesDaPartida { Semente = semente, Dificuldade = Dificuldade.Medio, Humanos = [true, false, true, false] });
        ParceirosIguais(partida);
        var humano = NovoHumano(0, perfil, semente);
        var entradas = new Entrada[4];
        for (int i = 0; i < ticks; i++)
        {
            entradas[0] = humano.Decidir(EstadoVisivel.De(partida), Passo);
            entradas[2] = doCliente[i];
            partida.Avancar(Passo, entradas);
        }
        Assert.True(partida.Estatisticas.Pontos >= 10, $"poucos pontos: {partida.Estatisticas.Pontos}");
        Assert.Equal(mesa.Partida.Placar.Resumo(), partida.Placar.Resumo());
        Assert.Equal(mesa.Partida.Estatisticas.Pontos, partida.Estatisticas.Pontos);
        Assert.Equal(mesa.Partida.Jogadores[0].Golpes, partida.Jogadores[0].Golpes);
        Assert.Equal(mesa.Partida.Jogadores[2].Golpes, partida.Jogadores[2].Golpes);
        Assert.Equal(mesa.Partida.Jogadores[0].X, partida.Jogadores[0].X);
        Assert.Equal(mesa.Partida.Jogadores[2].X, partida.Jogadores[2].X);
        Assert.Equal(mesa.Partida.Bola.X, partida.Bola.X);
    }
}
