using System.Globalization;
using Padel.Core;
using Padel.Core.Rede;

// Justiça na rede (CRONOGRAMA, M2: "o jogo tem que ser justo a 150"). O MESMO humano simulado — mesmo perfil, mesmas
// sementes — joga com um parceiro IA contra a dupla de IA Média em três arranjos: local, direto na Partida; online como
// HOST (vaga 0, vendo a Partida como a SessaoHost, com o cliente só assistindo — a vaga dele é da IA, então nada da
// rede entra na Partida e a linha repete a local por construção); online como CLIENTE (vaga 2, vendo só o quadro do
// ClienteDaPartida, pela VisaoParaOHumano). Ida e volta de 0, 80, 150 e 250 ms, jitter de 10 % da latência, perda de
// 0 e 2 %, pela RedeEmMemoria. Depois: o 1x1 frente a frente (humano no host x humano no cliente, mesmo perfil) e o que
// cada saída de compensação compraria (menos atraso de interpolação; a bola adiantada até o tick em que a entrada chega;
// o host lendo com o atraso do cliente) — as três últimas EMULADAS aqui, sem mexer no netcode.
// Uso: dotnet run -c Release --project ferramentas/JusticaNaRede [-- --pontos N] [--semente S]
// (300 pontos por célula: ~3 min em 4 núcleos).
// Imprime tabelas em markdown (as de docs/JUSTICA-NA-REDE.md). Determinístico: as mesmas sementes dão as mesmas tabelas.
// --semente S (padrão 1000) troca a primeira partida (S, S+1, …) e com ela a rede (7 × semente + 2026): é a réplica
// com que se confere se uma conclusão é do jogo ou do sorteio.

int pontosPorCelula = 300;
uint primeiraSemente = 1000;
for (int i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--pontos") pontosPorCelula = int.Parse(args[i + 1], CultureInfo.InvariantCulture);
    if (args[i] == "--semente") primeiraSemente = uint.Parse(args[i + 1], CultureInfo.InvariantCulture);
}
var cultura = CultureInfo.GetCultureInfo("pt-BR");
PerfilDeHumano[] perfis = [PerfilDeHumano.Intermediario, PerfilDeHumano.Avancado];
int[] idasEVoltas = [0, 80, 150, 250];
double[] perdas = [0, 0.02];
const float AtrasoPadrao = 0.1f;
float[] atrasosMenores = [0.05f, 1f / 30f, 0f];

// As células, sem repetir (a mesma célula aparece em mais de uma tabela).
var celulas = new List<Celula>();
void Pedir(Celula c) { if (!celulas.Contains(c)) celulas.Add(c); }
foreach (var perfil in perfis)
{
    Pedir(new Celula(Arranjo.Local, 0, perfil, 0, 0, AtrasoPadrao, false));
    Pedir(new Celula(Arranjo.Local, 2, perfil, 0, 0, AtrasoPadrao, false));
    foreach (var arranjo in new[] { Arranjo.Host, Arranjo.Cliente, Arranjo.FrenteAFrente })
    foreach (int ms in idasEVoltas)
    foreach (double perda in perdas)
        Pedir(new Celula(arranjo, arranjo == Arranjo.Host ? 0 : 2, perfil, ms, perda, AtrasoPadrao, false));
    foreach (int ms in idasEVoltas)
    {
        foreach (float atraso in atrasosMenores) Pedir(new Celula(Arranjo.Cliente, 2, perfil, ms, 0, atraso, false));
        Pedir(new Celula(Arranjo.Cliente, 2, perfil, ms, 0, AtrasoPadrao, true));
        Pedir(new Celula(Arranjo.Cliente, 2, perfil, ms, 0, 0f, true));
        Pedir(new Celula(Arranjo.FrenteAFrente, 2, perfil, ms, 0, 0f, true));
        Pedir(new Celula(Arranjo.FrenteAFrente, 2, perfil, ms, 0, 0f, true, HostComAtraso: true));
    }
}
var resultados = new Resultado[celulas.Count];
Parallel.For(0, celulas.Count, i => resultados[i] = Medir(celulas[i], pontosPorCelula, primeiraSemente));
Resultado De(Celula c) => resultados[celulas.IndexOf(c)];

Console.WriteLine($"# Justiça na rede — {pontosPorCelula} pontos por célula (partidas inteiras de 1 set, sementes {primeiraSemente}, {primeiraSemente + 1}, …)\n");
Console.WriteLine("Δt = tempo no balanço no contato − 0,12 s (negativo: apertou tarde; positivo: cedo). Golpe bom: erro < 0,5.");
Console.WriteLine("Δt nunca passa de −120 ms (contato no tick do aperto): \"bola já no alcance ao apertar\" é a fração dos golpes nesse chão.");
Console.WriteLine("Bolas que passaram: das bolas que chegaram batíveis ao jogador medido (e o parceiro não bateu), as que viraram ponto contra sem golpe.");
Console.WriteLine("Host: o cliente só assiste — a vaga dele fica com a IA, então nada que vem da rede entra na Partida do host e a linha repete a");
Console.WriteLine("\"Local (vaga 0)\" POR CONSTRUÇÃO: só confere que a entrada do próprio host não atrasa. Com gente do outro lado, é o frente a frente.\n");

foreach (var perfil in perfis)
{
    Console.WriteLine($"## {perfil.Nome} com parceiro IA contra a IA Média\n");
    Console.WriteLine("| Arranjo | Ida e volta | Perda | Δt médio | Δt mediano | σ(Δt) | p90 de \\|Δt\\| | Bola já no alcance ao apertar | Golpes bons | Bolas que passaram | Pontos ganhos | Golpes/ponto | Pontos |");
    Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
    LinhaPrincipal("Local (vaga 0)", "—", "—", De(new Celula(Arranjo.Local, 0, perfil, 0, 0, AtrasoPadrao, false)).Medido);
    LinhaPrincipal("Local (vaga 2)", "—", "—", De(new Celula(Arranjo.Local, 2, perfil, 0, 0, AtrasoPadrao, false)).Medido);
    foreach (var arranjo in new[] { Arranjo.Host, Arranjo.Cliente })
    foreach (int ms in idasEVoltas)
    foreach (double perda in perdas)
    {
        var c = new Celula(arranjo, arranjo == Arranjo.Host ? 0 : 2, perfil, ms, perda, AtrasoPadrao, false);
        LinhaPrincipal(arranjo == Arranjo.Host ? "Host (vaga 0)" : "Cliente (vaga 2)", $"{ms} ms", Porcento(perda), De(c).Medido);
    }

    Console.WriteLine($"\n### {perfil.Nome}: frente a frente (humano no host x humano no cliente, parceiros IA Parceiro)\n");
    Console.WriteLine("| Ida e volta | Perda | Cliente com | Pontos do host | Δt médio host / cliente | Golpes bons host / cliente | Passaram host / cliente | Golpes/ponto host / cliente | Pontos |");
    Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
    foreach (int ms in idasEVoltas)
    {
        foreach (double perda in perdas) LinhaFrenteAFrente(ms, perda, "hoje", new Celula(Arranjo.FrenteAFrente, 2, perfil, ms, perda, AtrasoPadrao, false));
        LinhaFrenteAFrente(ms, 0, "saída (ii) emulada", new Celula(Arranjo.FrenteAFrente, 2, perfil, ms, 0, 0f, true));
        LinhaFrenteAFrente(ms, 0, "(ii) + host lendo com o atraso do cliente (iv)", new Celula(Arranjo.FrenteAFrente, 2, perfil, ms, 0, 0f, true, HostComAtraso: true));
    }

    Console.WriteLine($"\n### {perfil.Nome}: o que cada saída compraria pro cliente (sem perda)\n");
    Console.WriteLine("| Ida e volta | Cliente com | Δt médio | Δt mediano | p90 de \\|Δt\\| | Bola já no alcance ao apertar | Golpes bons | Bolas que passaram | Pontos ganhos | Golpes/ponto |");
    Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|");
    foreach (int ms in idasEVoltas)
    {
        LinhaDeSaida(ms, "interpolação de 100 ms (hoje)", De(new Celula(Arranjo.Cliente, 2, perfil, ms, 0, AtrasoPadrao, false)).Medido);
        foreach (float atraso in atrasosMenores)
            LinhaDeSaida(ms, $"interpolação de {atraso * 1000:F0} ms", De(new Celula(Arranjo.Cliente, 2, perfil, ms, 0, atraso, false)).Medido);
        LinhaDeSaida(ms, "bola adiantada, leitura a 100 ms (≈ saída i)", De(new Celula(Arranjo.Cliente, 2, perfil, ms, 0, AtrasoPadrao, true)).Medido);
        LinhaDeSaida(ms, "bola adiantada, leitura sem atraso (≈ saída ii)", De(new Celula(Arranjo.Cliente, 2, perfil, ms, 0, 0f, true)).Medido);
    }
    Console.WriteLine();
}

void LinhaPrincipal(string arranjo, string idaEVolta, string perda, Resumo r) =>
    Console.WriteLine($"| {arranjo} | {idaEVolta} | {perda} | {Ms(r.DtMedio)} | {Ms(r.DtMediano)} | {Ms(r.DesvioDoDt)} | {Ms(r.P90DoDtAbsoluto)} | {Porcento(r.FracaoNoAperto)} | {Porcento(r.FracaoBons)} | {Porcento(r.FracaoPassaram)} | {Porcento(r.FracaoGanhos)} | {Num(r.GolpesPorPonto)} | {r.Pontos} |");

void LinhaFrenteAFrente(int ms, double perda, string cliente, Celula c)
{
    var r = De(c);
    var h = r.Host ?? throw new InvalidOperationException("frente a frente sem a medição do host");
    var m = r.Medido;
    Console.WriteLine($"| {ms} ms | {Porcento(perda)} | {cliente} | {Porcento(h.FracaoGanhos)} | {Ms(h.DtMedio)} / {Ms(m.DtMedio)} | {Porcento(h.FracaoBons)} / {Porcento(m.FracaoBons)} | {Porcento(h.FracaoPassaram)} / {Porcento(m.FracaoPassaram)} | {Num(h.GolpesPorPonto)} / {Num(m.GolpesPorPonto)} | {m.Pontos} |");
}

void LinhaDeSaida(int ms, string saida, Resumo r) =>
    Console.WriteLine($"| {ms} ms | {saida} | {Ms(r.DtMedio)} | {Ms(r.DtMediano)} | {Ms(r.P90DoDtAbsoluto)} | {Porcento(r.FracaoNoAperto)} | {Porcento(r.FracaoBons)} | {Porcento(r.FracaoPassaram)} | {Porcento(r.FracaoGanhos)} | {Num(r.GolpesPorPonto)} |");

string Ms(double segundos) => double.IsNaN(segundos) ? "—" : $"{(segundos * 1000).ToString("F0", cultura)} ms";
string Porcento(double fracao) => double.IsNaN(fracao) ? "—" : $"{(fracao * 100).ToString("F0", cultura)} %";
string Num(double valor) => valor.ToString("F2", cultura);

static Resultado Medir(Celula c, int pontos, uint primeiraSemente)
{
    var medido = new Medidor(c.Medido);
    var host = c.Arranjo == Arranjo.FrenteAFrente ? new Medidor(0) : null;
    for (uint semente = primeiraSemente; medido.Pontos < pontos; semente++)
    {
        if (c.Arranjo == Arranjo.Local) JogarLocal(c, semente, medido);
        else JogarOnline(c, semente, medido, host);
    }
    return new Resultado(medido.Resumir(), host?.Resumir());
}

static void JogarLocal(Celula c, uint semente, Medidor medidor)
{
    var partida = new Partida(new OpcoesDaPartida { Semente = semente, Dificuldade = Dificuldade.Medio, Humanos = [true, false, true, false] });
    Montagem.EntregarAIA(partida, c.Medido);
    var humano = Montagem.NovoHumano(c.Medido, c.Perfil, semente);
    medidor.Acompanhar(partida);
    var entradas = new Entrada[4];
    while (!partida.Acabou && partida.TempoDeJogo < Montagem.TempoMaximo)
    {
        entradas[c.Medido] = humano.Decidir(EstadoVisivel.De(partida), Protocolo.Passo);
        partida.Avancar(Protocolo.Passo, entradas);
        medidor.DepoisDoTick();
    }
    medidor.Encerrar();
}

static void JogarOnline(Celula c, uint semente, Medidor medido, Medidor? medidoNoHost)
{
    double latencia = c.IdaEVoltaMs / 2000.0;
    var rede = new RedeEmMemoria(semente * 7u + 2026u, new CondicoesDaRede { Latencia = latencia, Jitter = 0.1 * latencia, Perda = c.Perda });
    var pontoDoHost = rede.NovoPonto();
    var servidor = new ServidorDaPartida(pontoDoHost, "Host", new OpcoesDaPartida { Semente = semente, Dificuldade = Dificuldade.Medio });
    var pontoDoCliente = rede.NovoPonto();
    rede.Conectar(pontoDoCliente, pontoDoHost);
    var cliente = new ClienteDaPartida(pontoDoCliente, "Cliente", c.Atraso);
    var visao = new VisaoParaOHumano();
    VisaoDaPartida? quadro = null;
    EstadoVisivel? visto = null;

    void Passo(Entrada doHost, Entrada doCliente)
    {
        rede.AvancarRelogio(Protocolo.Passo);
        servidor.Passo(doHost);
        cliente.Passo(doCliente);
        if (cliente.Fase == FaseDoCliente.Jogando && cliente.ParaDesenhar() is VisaoDaPartida q)
        {
            quadro = q;
            visto = visao.Ver(q);
        }
    }

    for (int i = 0; i < 5 * Protocolo.TicksPorSegundo && cliente.Fase != FaseDoCliente.NaSala; i++) Passo(Entrada.Vazia, Entrada.Vazia);
    if (cliente.Fase != FaseDoCliente.NaSala || cliente.Indice != 2) throw new InvalidOperationException($"o cliente não entrou na vaga 2 ({cliente.Fase}, vaga {cliente.Indice})");
    var partida = servidor.Iniciar();
    if (c.Arranjo == Arranjo.FrenteAFrente) Montagem.ParceirosIguais(partida);
    else Montagem.EntregarAIA(partida, c.Medido);

    bool humanoNoHost = c.Arranjo is Arranjo.Host or Arranjo.FrenteAFrente;
    bool humanoNoCliente = c.Arranjo is Arranjo.Cliente or Arranjo.FrenteAFrente;
    var doHost = humanoNoHost ? Montagem.NovoHumano(0, c.Perfil, semente) : null;
    var doCliente = humanoNoCliente ? Montagem.NovoHumano(2, c.Perfil, semente) : null;
    medido.Acompanhar(partida);
    medidoNoHost?.Acompanhar(partida);
    var passadoDoHost = new Queue<EstadoVisivel>();
    while (!partida.Acabou && partida.TempoDeJogo < Montagem.TempoMaximo)
    {
        var entradaDoHost = Entrada.Vazia;
        if (doHost is not null)
        {
            var agora = EstadoVisivel.De(partida);
            var paraOHost = agora;
            if (c.HostComAtraso)
            {
                // EMULAÇÃO da saída (iv): o host lê a partida com o mesmo atraso com que o cliente a lê agora (o quadro do
                // cliente está tantos ticks atrás do host), com o próprio jogador no presente e a bola adiantada até agora
                // pela física — como o cliente na saída (ii). O golpe do cliente o host só "vê" depois desse atraso.
                int atraso = quadro is VisaoDaPartida doQuadro ? Math.Max(0, (int)Math.Round(servidor.Tick - doQuadro.Tick)) : 0;
                passadoDoHost.Enqueue(agora);
                while (passadoDoHost.Count > atraso + 1) passadoDoHost.Dequeue();
                var antes = passadoDoHost.Peek();
                var jogadores = antes.Jogadores.ToArray();
                jogadores[0] = agora.Jogadores[0];
                paraOHost = Montagem.Adiantar(antes with { Jogadores = jogadores }, passadoDoHost.Count - 1);
            }
            entradaDoHost = doHost.Decidir(paraOHost, Protocolo.Passo);
        }
        var entradaDoCliente = Entrada.Vazia;
        if (doCliente is not null && visto is EstadoVisivel v && quadro is VisaoDaPartida q)
        {
            if (c.BolaAdiantada) v = Montagem.Adiantar(v, cliente.TickEstimadoDoServidor - q.Tick + cliente.Ping * Protocolo.TicksPorSegundo);
            entradaDoCliente = doCliente.Decidir(v, Protocolo.Passo);
        }
        Passo(entradaDoHost, entradaDoCliente);
        medido.DepoisDoTick();
        medidoNoHost?.DepoisDoTick();
    }
    medido.Encerrar();
    medidoNoHost?.Encerrar();
}

enum Arranjo { Local, Host, Cliente, FrenteAFrente }

/// <summary>Uma célula das tabelas. Medido: a vaga do humano medido (0 no host, 2 no cliente; no frente a frente, 2, e o host também é medido).</summary>
sealed record Celula(Arranjo Arranjo, int Medido, PerfilDeHumano Perfil, int IdaEVoltaMs, double Perda, float Atraso, bool BolaAdiantada, bool HostComAtraso = false);

sealed record Resultado(Resumo Medido, Resumo? Host);

sealed record Resumo(int Pontos, int Golpes, double DtMedio, double DtMediano, double DesvioDoDt, double P90DoDtAbsoluto,
    double FracaoNoAperto, double FracaoBons, double FracaoPassaram, double FracaoGanhos, double GolpesPorPonto);

static class Montagem
{
    /// <summary>Teto de uma partida (1 set) em segundos de jogo: nenhuma chega perto; é só pra não girar pra sempre.</summary>
    public const float TempoMaximo = 3600;

    /// <summary>
    /// A partida como o host monta o 1x1 online (humanos nas vagas 0 e 2), com a vaga humana que não é medida entregue à
    /// IA Média (como o ServidorDaPartida faz com quem cai) e o time do medido com o perfil Parceiro. O jogador entregue
    /// à IA fica com o Alcance de humano (1,35 m): Alcance só se define no construtor do Jogador — a mesma limitação do
    /// ServidorDaPartida.ConferirSilencio, igual no local e no online. Idêntica à de JusticaNaRedeTests.
    /// </summary>
    public static void EntregarAIA(Partida partida, int medido)
    {
        partida.Jogadores[2 - medido].Humano = false;
        if (medido == 2)
        {
            partida.IAs[0] = new IA(0, Perfis.Medio, partida.Aleatorio);
            partida.IAs[1] = new IA(1, Perfis.Parceiro, partida.Aleatorio);
            partida.Jogadores[3].Velocidade = Perfis.Parceiro.Velocidade;
        }
        foreach (var j in partida.JogadoresDoTime(1 - medido / 2)) j.Velocidade = Perfis.Medio.Velocidade;
    }

    /// <summary>Frente a frente: os dois humanos com o mesmo parceiro (perfil Parceiro); sem isso o do time 1 teria a IA da dificuldade.</summary>
    public static void ParceirosIguais(Partida partida)
    {
        partida.IAs[1] = new IA(1, Perfis.Parceiro, partida.Aleatorio);
        partida.Jogadores[3].Velocidade = Perfis.Parceiro.Velocidade;
    }

    public static HumanoSimulado NovoHumano(int indice, PerfilDeHumano perfil, uint semente) => new(indice, perfil, new Aleatorio(semente * 31u + 7u));

    /// <summary>
    /// EMULAÇÃO das saídas (i) e (ii) do documento, sem mexer no netcode: a bola que o humano do cliente vê é levada pela
    /// mesma física até o tick do host em que a entrada dele vai chegar (o quadro está ticks atrás do host estimado, e a
    /// entrada leva mais uma ida e volta). A leitura "quicou" acompanha o quique do lado de quem recebe. Não adianta
    /// golpe que o cliente ainda não sabe — o mesmo limite das duas saídas de verdade. Com a interpolação de 100 ms, o
    /// golpe do adversário continua sendo lido 100 ms depois de o instantâneo chegar (os eventos tocam no tick desenhado):
    /// é a saída (i), em que o host julga o timing pelo que o cliente via e o cliente continua vendo o passado. Com
    /// interpolação 0, lê assim que chega: é a (ii) inteira, a bola do lado do cliente sem esperar a interpolação.
    /// </summary>
    public static EstadoVisivel Adiantar(EstadoVisivel v, double ticks)
    {
        if (!(ticks > 0) || !v.BolaEmJogo) return v;
        // atalho: no máximo 90 ticks (0,75 s) de adiantamento; ida e volta acima de ~650 ms fica mal emulada — nenhuma célula chega lá.
        double limitado = Math.Min(ticks, 90);
        int inteiros = (int)Math.Floor(limitado);
        float fracao = (float)(limitado - inteiros);
        var bola = v.CopiaDaBola();
        var eventos = new List<EventoDaBola>(4);
        bool quicou = v.QuicouDepoisDoUltimoGolpe;
        int ladoDeQuemBateu = v.TimeDoUltimoGolpe >= 0 ? Quadra.LadoDoTime(v.TimeDoUltimoGolpe) : 0;
        for (int k = 0; k <= inteiros && bola.EmJogo; k++)
        {
            float dt = k < inteiros ? Protocolo.Passo : fracao * Protocolo.Passo;
            if (dt <= 0) break;
            eventos.Clear();
            bola.Avancar(dt, eventos);
            foreach (var e in eventos)
                if (e.Tipo == TipoDeEventoDaBola.Quique && ladoDeQuemBateu != 0 && e.Lado != ladoDeQuemBateu) quicou = true;
        }
        return v with
        {
            BolaX = bola.X, BolaY = bola.Y, BolaZ = bola.Z,
            BolaVx = bola.Vx, BolaVy = bola.Vy, BolaVz = bola.Vz,
            BolaWx = bola.Wx, BolaWy = bola.Wy, BolaWz = bola.Wz,
            BolaEmJogo = bola.EmJogo,
            QuicouDepoisDoUltimoGolpe = quicou,
        };
    }
}

/// <summary>
/// O que se mede de um jogador, na partida de verdade (a local, ou a do host): o timing de cada golpe (fora o saque), a
/// qualidade (erro do golpe &lt; 0,5), os pontos, e as bolas que passaram. "Bola" = do golpe do adversário até o golpe
/// seguinte ou o fim do ponto. Ela chegou batível ao medido se, em algum tick, estava onde a raquete dele alcança e o
/// árbitro deixava bater (o mesmo critério da Partida pra quem está balançando: no alcance confortável, ou no alcance
/// esticado indo embora). Das batíveis — tirando as que o parceiro bateu —, passou a que virou ponto contra sem golpe.
/// </summary>
sealed class Medidor(int indice)
{
    private readonly List<float> _dt = [];
    private Partida? _partida;
    private Jogador? _jogador;
    private bool _bolaAberta, _bativel;
    private int _bons, _noAperto, _oportunidades, _passaram;

    public int Pontos { get; private set; }
    private int _ganhos;

    public void Acompanhar(Partida partida)
    {
        _partida = partida;
        _jogador = partida.Jogadores[indice];
        _bolaAberta = false;
        partida.Evento += AoEvento;
    }

    public void Encerrar()
    {
        if (_partida is Partida p) p.Evento -= AoEvento;
        _partida = null;
        _jogador = null;
    }

    private void AoEvento(EventoDaPartida ev)
    {
        if (_partida is not Partida partida || _jogador is not Jogador eu) return;
        switch (ev.Tipo)
        {
            case TipoDeEventoDaPartida.Golpe when ev.Time != eu.Time:
                _bolaAberta = true;
                _bativel = false;
                break;
            case TipoDeEventoDaPartida.Golpe:
                if (ev.Jogador == eu && ev.Golpe != TipoDeGolpe.Saque)
                {
                    _dt.Add(eu.TempoNoBalanco - Jogador.MomentoIdealDoBalanco);
                    if (partida.UltimoErroDoHumano < 0.5f) _bons++;
                    if (eu.TempoNoBalanco < Protocolo.Passo / 2) _noAperto++;   // contato no tick do aperto: a bola já estava no alcance
                    if (_bolaAberta) _oportunidades++;
                }
                _bolaAberta = false;
                break;
            case TipoDeEventoDaPartida.Ponto or TipoDeEventoDaPartida.Game or TipoDeEventoDaPartida.Set or TipoDeEventoDaPartida.Partida:
                Pontos++;
                if (ev.Time == eu.Time) _ganhos++;
                if (_bolaAberta && _bativel)
                {
                    _oportunidades++;
                    if (ev.Time != eu.Time) _passaram++;
                }
                _bolaAberta = false;
                break;
            case TipoDeEventoDaPartida.Falta or TipoDeEventoDaPartida.Let or TipoDeEventoDaPartida.SaquePreparado:
                _bolaAberta = false;
                break;
            default:
                break;
        }
    }

    /// <summary>Depois de cada tick: a bola aberta ficou batível pro medido?</summary>
    public void DepoisDoTick()
    {
        if (!_bolaAberta || _bativel || _partida is not Partida partida || _jogador is not Jogador eu) return;
        var bola = partida.Bola;
        if (partida.Estado != EstadoDaPartida.Rally || !bola.EmJogo || bola.Rolando || !partida.Arbitro.PodeGolpear(eu.Time)) return;
        float indoEmbora = (bola.X - eu.X) * bola.Vx + (bola.Y - eu.Y) * bola.Vy;
        if (eu.AlcancaConfortavelmente(bola) || (eu.Alcanca(bola) && indoEmbora > 0)) _bativel = true;
    }

    public Resumo Resumir()
    {
        var ordenados = _dt.Select(d => (double)d).Order().ToList();
        var absolutos = _dt.Select(d => Math.Abs((double)d)).Order().ToList();
        double media = ordenados.Count > 0 ? ordenados.Average() : double.NaN;
        double desvio = ordenados.Count > 1 ? Math.Sqrt(ordenados.Sum(d => (d - media) * (d - media)) / (ordenados.Count - 1)) : double.NaN;
        return new Resumo(
            Pontos, _dt.Count, media, Quantil(ordenados, 0.5), desvio, Quantil(absolutos, 0.9),
            _dt.Count > 0 ? (double)_noAperto / _dt.Count : double.NaN,
            _dt.Count > 0 ? (double)_bons / _dt.Count : double.NaN,
            _oportunidades > 0 ? (double)_passaram / _oportunidades : double.NaN,
            Pontos > 0 ? (double)_ganhos / Pontos : double.NaN,
            Pontos > 0 ? (double)_dt.Count / Pontos : double.NaN);
    }

    private static double Quantil(List<double> ordenados, double q) =>
        ordenados.Count == 0 ? double.NaN : ordenados[Math.Min(ordenados.Count - 1, (int)(q * ordenados.Count))];
}
