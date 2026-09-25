namespace Padel.Core;

/// <summary>
/// Quem é o jogador simulado, em números de gente. Referências (aproximadas; o que importa é a ordem de grandeza,
/// e a calibração em ferramentas/Calibracao mede o efeito de cada uma):
/// - tempo de reação simples de um adulto fica em 0,2–0,25 s; o de escolha (pra onde vou?) em 0,3–0,4 s;
/// - o desvio de timing de um golpe interceptivo cai de ~70 ms (quem começa) pra ~10–15 ms (profissional);
///   num botão de videogame, jogador bom de ritmo acerta dentro de ±20–30 ms;
/// - erro de leitura: onde a pessoa acha que a bola vai estar, em metros, antes de a bola quicar (quem começa
///   erra a profundidade do lob por quase um metro; quem joga bem, por palmos).
/// </summary>
public sealed record PerfilDeHumano(string Nome, float TempoDeReacao, float DesvioDoTempo, float ErroDeLeitura, float Agressividade)
{
    public static readonly PerfilDeHumano Iniciante = new("Iniciante", TempoDeReacao: 0.35f, DesvioDoTempo: 0.070f, ErroDeLeitura: 0.9f, Agressividade: 0.2f);
    public static readonly PerfilDeHumano Intermediario = new("Intermediário", TempoDeReacao: 0.27f, DesvioDoTempo: 0.030f, ErroDeLeitura: 0.5f, Agressividade: 0.45f);
    public static readonly PerfilDeHumano Avancado = new("Avançado", TempoDeReacao: 0.22f, DesvioDoTempo: 0.020f, ErroDeLeitura: 0.3f, Agressividade: 0.6f);
    public static readonly PerfilDeHumano Profissional = new("Profissional", TempoDeReacao: 0.18f, DesvioDoTempo: 0.012f, ErroDeLeitura: 0.15f, Agressividade: 0.75f);

    public static IReadOnlyList<PerfilDeHumano> Todos { get; } = [Iniciante, Intermediario, Avancado, Profissional];
}

/// <summary>O que um jogador vê de um companheiro ou adversário.</summary>
public readonly record struct JogadorVisivel(float X, float Y, float Vx, float Vy, int Time, int Lado);

/// <summary>
/// Só o que um jogador VÊ da partida — nada de dentro da IA nem do árbitro além do que é visível na quadra
/// (quem bateu por último, se a bola já quicou do lado de quem recebe). O cliente de rede monta o mesmo
/// record a partir do instantâneo; aqui ele sai da Partida local.
/// </summary>
public sealed record EstadoVisivel(
    EstadoDaPartida Estado,
    int IndiceDoSacador,
    Caixa CaixaDoSaque,
    float BolaX, float BolaY, float BolaZ,
    float BolaVx, float BolaVy, float BolaVz,
    float BolaWx, float BolaWy, float BolaWz,
    bool BolaEmJogo,
    int TimeDoUltimoGolpe,
    TipoDeGolpe? TipoDoUltimoGolpe,
    bool QuicouDepoisDoUltimoGolpe,
    IReadOnlyList<JogadorVisivel> Jogadores)
{
    public static EstadoVisivel De(Partida p)
    {
        var b = p.Bola;
        var jogadores = new JogadorVisivel[4];
        for (int i = 0; i < 4; i++)
        {
            var j = p.Jogadores[i];
            jogadores[i] = new JogadorVisivel(j.X, j.Y, j.Vx, j.Vy, j.Time, j.Lado);
        }
        return new EstadoVisivel(
            p.Estado, p.Placar.Sacador.Time * 2 + p.Placar.Sacador.Jogador, p.CaixaDoSaque,
            b.X, b.Y, b.Z, b.Vx, b.Vy, b.Vz, b.Wx, b.Wy, b.Wz, b.EmJogo,
            p.UltimoGolpe?.Jogador.Time ?? -1, p.UltimoGolpe?.Tipo, p.Arbitro.QuicouNoReceptor,
            jogadores);
    }

    public Bola CopiaDaBola()
    {
        var bola = new Bola();
        bola.Posicionar(BolaX, BolaY, BolaZ);
        bola.Lancar(new Velocidade(BolaVx, BolaVy, BolaVz, 0, BolaWx, BolaWy, BolaWz));
        bola.EmJogo = BolaEmJogo;
        return bola;
    }
}

/// <summary>
/// Um jogador automático que joga PELA ENTRADA, como uma pessoa, com defeitos de pessoa: lê a bola com erro,
/// reage com atraso, aperta o botão com erro de timing e usa os mesmos comandos do controle (Partida.GolpeDoHumano).
/// Não é a IA: a IA move o Jogador direto e escolhe o golpe por dentro; este só produz uma Entrada por tick, e a
/// Partida trata essa Entrada exatamente como a de alguém no controle (modo Manual).
/// Serve pra calibrar a janela do balanço com números (ferramentas/Calibracao), pro "--bot" do Godot nos testes de
/// rede, e pro modo demonstração. Determinístico com a mesma semente.
/// </summary>
public sealed class HumanoSimulado
{
    private const float PassoDaLeitura = 1f / 60f;
    private const float Magnitudedamira = 0.45f;   // mira sem sair correndo: passa dos limiares (0,3) e anda devagar

    private readonly int _indice;
    private readonly Aleatorio _aleatorio;
    private readonly Jogador _eu;   // cópia local pra usar a mesma geometria do corpo (drive/revés, ponto de contato)

    // Memória da bola que vem: muda quando alguém bate ou quando a bola quica/bate na parede.
    private static readonly (int time, TipoDeGolpe? tipo, bool quicou) SemLeitura = (-2, null, false);
    private (int time, TipoDeGolpe? tipo, bool quicou) _ultimaLeitura = SemLeitura;
    private float _desdeALeitura;
    private float _erroX, _erroY, _erroDoTempo;
    private bool _apertouNestaBola;
    private bool _segurarAcao;
    private PlanoDeGolpe _plano;
    private float _esperaDoSaque = -1;

    private enum PlanoDeGolpe { Normal, Ataque, Defesa, Lob, Chiquita, Remate, RemateForte, Bandeja, Vibora }

    public HumanoSimulado(int indiceDoJogador, PerfilDeHumano perfil, Aleatorio aleatorio, bool destro = true)
    {
        if (indiceDoJogador is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(indiceDoJogador));
        _indice = indiceDoJogador;
        Perfil = perfil;
        _aleatorio = aleatorio;
        _eu = new Jogador(indiceDoJogador / 2, indiceDoJogador % 2, perfil.Nome, humano: true, Jogador.VelocidadeDoHumano) { Destro = destro };
    }

    public PerfilDeHumano Perfil { get; }
    public int Indice => _indice;

    /// <summary>A entrada deste tick, no referencial do jogador (Dy &lt; 0 = rumo à rede). Sempre dentro de -1..1.</summary>
    public Entrada Decidir(EstadoVisivel estado, float dt)
    {
        var meu = estado.Jogadores[_indice];
        _eu.X = meu.X; _eu.Y = meu.Y; _eu.Vx = meu.Vx; _eu.Vy = meu.Vy;
        _desdeALeitura += dt;

        switch (estado.Estado)
        {
            case EstadoDaPartida.Saque:
                _apertouNestaBola = false;
                _ultimaLeitura = SemLeitura;   // saque repetido depois de falta é bola nova, mesmo com o mesmo time e tipo
                if (estado.IndiceDoSacador != _indice) { _esperaDoSaque = -1; return Entrada.Vazia; }
                if (_esperaDoSaque < 0) _esperaDoSaque = Perfil.TempoDeReacao + 0.4f + _aleatorio.Proximo() * 0.6f;
                _esperaDoSaque -= dt;
                if (_esperaDoSaque > 0) return Entrada.Vazia;
                _esperaDoSaque = -1;
                return new Entrada(Limitar((_aleatorio.Proximo() - 0.5f) * 1.2f), 0, AcaoPressionada: true, AcaoSegurada: true);
            case EstadoDaPartida.Rally:
                return NoRally(estado);
            default:
                _esperaDoSaque = -1;
                return Entrada.Vazia;
        }
    }

    private Entrada NoRally(EstadoVisivel e)
    {
        var leitura = (e.TimeDoUltimoGolpe, e.TipoDoUltimoGolpe, e.QuicouDepoisDoUltimoGolpe);
        if (leitura != _ultimaLeitura)
        {
            bool novoGolpe = leitura.TimeDoUltimoGolpe != _ultimaLeitura.time || leitura.TipoDoUltimoGolpe != _ultimaLeitura.tipo;
            _ultimaLeitura = leitura;
            if (novoGolpe)
            {
                // Bola nova: leitura nova com erro (a releitura no quique corrige parte dele, como no olho de verdade).
                _desdeALeitura = 0;
                _apertouNestaBola = false;
                _segurarAcao = false;
                _erroX = _aleatorio.Gaussiana() * Perfil.ErroDeLeitura;
                _erroY = _aleatorio.Gaussiana() * Perfil.ErroDeLeitura;
                _erroDoTempo = _aleatorio.Gaussiana() * Perfil.DesvioDoTempo;
            }
            else
            {
                _erroX *= 0.35f; _erroY *= 0.35f;
            }
        }

        bool bolaVemPraMim = e.TimeDoUltimoGolpe >= 0 && e.TimeDoUltimoGolpe != _eu.Time && e.BolaEmJogo;
        if (!bolaVemPraMim) return Posicionar(e, atacando: e.TimeDoUltimoGolpe == _eu.Time);

        var alvo = PreverContato(e);
        if (alvo is not (float tx, float ty, float tempo, float zContato)) return Posicionar(e, atacando: false);

        // Só quem está mais perto do ponto de contato vai buscar; o parceiro cobre.
        if (!SouQuemBusca(e, tx, ty)) return Posicionar(e, atacando: false);

        bool bolaAlta = zContato > 1.5f;
        float xCorpo = _eu.XParaBater(tx + _erroX, bolaAlta);
        float yCorpo = ty + _erroY * 0.7f;
        float dxMundo = xCorpo - _eu.X, dyMundo = yCorpo - _eu.Y;
        float distancia = MathF.Sqrt(dxMundo * dxMundo + dyMundo * dyMundo);

        // Timing: a raquete encontra a bola quando ela ENTRA no alcance confortável do corpo (Jogador.PodeBaterAgora), não
        // quando chega ao ponto ao lado dele — então o aperto se mede por essa entrada, como o jogador de verdade sente.
        // Aperta quando faltar o momento ideal do balanço, mais o erro de timing da pessoa — e nunca antes de reagir.
        float tempoAteOAlcance = TempoAteEntrarNoAlcance(e, xCorpo, yCorpo) ?? tempo;
        float faltaPraApertar = tempoAteOAlcance - Jogador.MomentoIdealDoBalanco + _erroDoTempo;
        bool podeReagir = _desdeALeitura >= Perfil.TempoDeReacao;
        if (!_apertouNestaBola && podeReagir && faltaPraApertar <= 0)
        {
            _apertouNestaBola = true;
            _plano = EscolherGolpe(e, zContato);
            _segurarAcao = _plano == PlanoDeGolpe.RemateForte;
            var (mx, my) = Mira(e, _plano);
            bool lob = _plano is PlanoDeGolpe.Lob or PlanoDeGolpe.Chiquita;
            return new Entrada(mx, my, AcaoPressionada: !lob, AcaoSegurada: _segurarAcao, LobPressionada: lob);
        }
        if (_apertouNestaBola)
        {
            // Balançando: a direção agora é a mira (é assim que o controle funciona), e o corpo quase não anda.
            var (mx, my) = Mira(e, _plano);
            return new Entrada(mx, my, false, _segurarAcao);
        }
        // Indo buscar: corre e freia pra chegar parado (se não reagiu ainda, fica onde está).
        if (!podeReagir || distancia < 0.08f) return Entrada.Vazia;
        float velocidadeQueCabe = MathF.Sqrt(2 * _eu.Frenagem * distancia) / _eu.Velocidade;
        float forca = MathF.Min(1, velocidadeQueCabe);
        return ParaEntrada(dxMundo / distancia * forca, dyMundo / distancia * forca, false, false);
    }

    /// <summary>Primeiro ponto em que a bola fica batível perto do corpo: (x, y, segundos até lá, altura).</summary>
    private (float, float, float, float)? PreverContato(EstadoVisivel e)
    {
        var bola = e.CopiaDaBola();
        var eventos = new List<EventoDaBola>(4);
        bool precisaQuicar = e.TipoDoUltimoGolpe == TipoDeGolpe.Saque;   // não se voleia o saque
        bool naRede = MathF.Abs(_eu.Y) < 4.5f;
        bool quicou = e.QuicouDepoisDoUltimoGolpe;
        float t = 0;
        (float, float, float, float)? primeiroAlcancavel = null;
        for (int i = 0; i < 180 && bola.EmJogo && !bola.Parada; i++)
        {
            eventos.Clear();
            bola.Avancar(PassoDaLeitura, eventos);
            t += PassoDaLeitura;
            foreach (var ev in eventos)
            {
                if (ev.Tipo != TipoDeEventoDaBola.Quique) continue;
                if (ev.Lado != _eu.Lado) return primeiroAlcancavel;   // quicou do outro lado: não é minha
                if (quicou) return primeiroAlcancavel;              // segundo quique: acabou
                quicou = true;
            }
            if (Quadra.LadoDe(bola.Y) != _eu.Lado || bola.Rolando) continue;
            if (!quicou && (precisaQuicar || !naRede)) continue;   // do fundo, espera quicar (voleio é coisa de rede)
            if (bola.Z < 0.4f || bola.Z > 2.4f) continue;
            if (quicou && bola.Vz > 0 && bola.Z < 0.9f) continue;   // subindo logo depois do quique: espera cair
            float d = Util.Distancia(_eu.X, _eu.Y, bola.X, bola.Y);
            if (_eu.TempoParaChegar(MathF.Max(0, d - Jogador.DistanciaIdealDoContato)) + Perfil.TempoDeReacao <= t + 0.15f)
                return (bola.X, bola.Y, t, bola.Z);
            primeiroAlcancavel ??= (bola.X, bola.Y, t, bola.Z);
        }
        return primeiroAlcancavel;
    }

    /// <summary>Segundos até a bola batível entrar no alcance confortável de um corpo parado em (x, y); null se não entra.</summary>
    private float? TempoAteEntrarNoAlcance(EstadoVisivel e, float x, float y)
    {
        var bola = e.CopiaDaBola();
        var eventos = new List<EventoDaBola>(4);
        bool precisaQuicar = e.TipoDoUltimoGolpe == TipoDeGolpe.Saque || MathF.Abs(y) >= 4.5f;
        bool quicou = e.QuicouDepoisDoUltimoGolpe;
        float t = 0;
        const float passo = 1f / 240f;   // mais fino que a leitura: o timing se mede em centésimos
        for (int i = 0; i < 720 && bola.EmJogo && !bola.Parada; i++)
        {
            eventos.Clear();
            bola.Avancar(passo, eventos);
            t += passo;
            foreach (var ev in eventos)
                if (ev.Tipo == TipoDeEventoDaBola.Quique) { if (ev.Lado != _eu.Lado || quicou) return null; quicou = true; }
            if (!quicou && precisaQuicar) continue;
            if (bola.Z < 0 || bola.Z > _eu.AlturaMaxima) continue;
            if (Util.Distancia(x, y, bola.X, bola.Y) <= _eu.AlcanceConfortavel) return t;
        }
        return null;
    }

    private bool SouQuemBusca(EstadoVisivel e, float x, float y)
    {
        int parceiro = _indice ^ 1;
        var p = e.Jogadores[parceiro];
        float eu = Util.Distancia(_eu.X, _eu.Y, x, y), ele = Util.Distancia(p.X, p.Y, x, y);
        return eu <= ele + 0.3f;   // empate: vou eu (o humano não confia no parceiro)
    }

    private PlanoDeGolpe EscolherGolpe(EstadoVisivel e, float zContato)
    {
        var r = _aleatorio;
        float a = Perfil.Agressividade;
        bool naRede = MathF.Abs(_eu.Y) < 4.5f;
        int adversariosNaRede = 0;
        foreach (var j in e.Jogadores) if (j.Time != _eu.Time && MathF.Abs(j.Y) < 4.5f) adversariosNaRede++;

        if (zContato > 1.5f)
        {
            if (naRede && zContato > 2.2f && r.Proximo() < a * 0.4f) return PlanoDeGolpe.RemateForte;
            if (naRede && r.Proximo() < a) return PlanoDeGolpe.Remate;
            return r.Proximo() < 0.3f * a ? PlanoDeGolpe.Vibora : PlanoDeGolpe.Bandeja;
        }
        if (adversariosNaRede == 2 && !naRede)
        {
            float s = r.Proximo();
            if (s < 0.35f) return PlanoDeGolpe.Lob;
            if (s < 0.35f + 0.25f * a) return PlanoDeGolpe.Chiquita;
        }
        if (naRede && r.Proximo() < a) return PlanoDeGolpe.Ataque;
        if (!naRede && r.Proximo() < 0.25f * (1 - a)) return PlanoDeGolpe.Defesa;
        return PlanoDeGolpe.Normal;
    }

    /// <summary>Direção da mira no referencial do jogador: canto oposto ao adversário mais perto dele, e frente/trás pelo plano.</summary>
    private (float, float) Mira(EstadoVisivel e, PlanoDeGolpe plano)
    {
        // Canto: o lado da quadra adversária mais vazio (no referencial do jogador, direita = +).
        float somaX = 0;
        foreach (var j in e.Jogadores) if (j.Time != _eu.Time) somaX += j.X;
        float vazioNoMundo = somaX > 0 ? -1 : 1;
        float canto = vazioNoMundo * _eu.Lado * Magnitudedamira;
        return plano switch
        {
            PlanoDeGolpe.Ataque or PlanoDeGolpe.Remate or PlanoDeGolpe.RemateForte or PlanoDeGolpe.Chiquita => (canto, -Magnitudedamira),
            PlanoDeGolpe.Defesa => (canto, Magnitudedamira),
            PlanoDeGolpe.Vibora => (MathF.Sign(canto) * 0.8f, 0),
            PlanoDeGolpe.Bandeja or PlanoDeGolpe.Lob => (canto * 0.5f, 0),
            _ => (canto, 0),
        };
    }

    /// <summary>Sem a bola vindo: volta pra formação — rede se o time está atacando, fundo se defendendo.</summary>
    private Entrada Posicionar(EstadoVisivel e, bool atacando)
    {
        _apertouNestaBola = false;
        bool lobOuRemate = e.TipoDoUltimoGolpe is TipoDeGolpe.Lob or TipoDeGolpe.Smash or TipoDeGolpe.Bandeja or TipoDeGolpe.Vibora or TipoDeGolpe.Chiquita;
        float profundidade = atacando && (lobOuRemate || MathF.Abs(_eu.Y) < 5.5f) ? 3.2f : 6.5f;
        float tx = _eu.XDaMetade, ty = _eu.Lado * profundidade;
        float dx = tx - _eu.X, dy = ty - _eu.Y;
        float d = MathF.Sqrt(dx * dx + dy * dy);
        if (d < 0.15f) return Entrada.Vazia;
        float forca = MathF.Min(1, MathF.Sqrt(2 * _eu.Frenagem * d) / _eu.Velocidade) * 0.8f;
        return ParaEntrada(dx / d * forca, dy / d * forca, false, false);
    }

    /// <summary>Do mundo pro referencial do jogador (a Partida faz o caminho inverso multiplicando pelo Lado).</summary>
    private Entrada ParaEntrada(float dxMundo, float dyMundo, bool apertou, bool segurando) =>
        new(Limitar(dxMundo * _eu.Lado), Limitar(dyMundo * _eu.Lado), apertou, segurando);

    private static float Limitar(float v) => Util.Limitar(v, -1, 1);
}
