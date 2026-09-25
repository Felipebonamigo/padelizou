namespace Padel.Core;

public enum Dificuldade { Facil, Medio, Dificil }
/// <summary>
/// Chiquita: bola baixa e lenta nos pés de quem está na rede. Contrapared: bater na própria parede de fundo pra bola
/// voltar e cruzar a rede (último recurso). SmashPor3 / SmashPor4: remate que quica do outro lado e sai pela lateral /
/// por cima do fundo. Os quatro saem dos solucionadores de <see cref="GolpesEspeciais"/>.
/// </summary>
public enum TipoDeGolpe { Saque, Normal, Ataque, Defesa, Lob, Smash, Bandeja, Vibora, Erro, Chiquita, Contrapared, SmashPor3, SmashPor4 }
/// <summary>No referencial de quem bate (olhando pra rede): bola do lado da raquete é drive; do outro lado, revés.</summary>
public enum LadoDoGolpe { Drive, Reves }
public enum Formacao { Fundo, Rede }

/// <summary>
/// Um golpe decidido: onde a bola deve cair, em quanto tempo, com que efeito e de que lado do corpo.
/// Lancamento, quando vem, é a velocidade exata que um solucionador já simulou (golpes especiais: a contrapared nem
/// mira o chão, mira a própria parede); sem ele, a partida calcula a velocidade pelo alvo com <see cref="Golpes.Calcular"/>.
/// </summary>
public readonly record struct Golpe(float AlvoX, float AlvoY, float TempoDeVoo, TipoDeGolpe Tipo, bool IgnorarRede = false, Efeito Efeito = default, LadoDoGolpe Lado = LadoDoGolpe.Drive, Velocidade? Lancamento = null);

/// <summary>
/// Entrada de um humano, no referencial DELE: Dy &lt; 0 é "rumo à rede", Dx &gt; 0 é "minha direita".
/// A partida converte pro mundo pelo lado do jogador — assim o cliente de cima e o de baixo mandam a mesma coisa.
/// AcaoPressionada saca e, no modo manual, começa o balanço; LobPressionada começa um balanço de lob.
/// </summary>
public readonly record struct Entrada(float Dx, float Dy, bool AcaoPressionada, bool AcaoSegurada, bool LobPressionada = false)
{
    public static readonly Entrada Vazia = default;
}

/// <summary>ChanceDeRemateParaFora: com a bola muito alta perto da rede, quanto tenta o remate por 3 / por 4 (se a física deixar).</summary>
public sealed record PerfilDeIA(float Velocidade, float Reacao, float ErroDeMira, float ErroDePrevisao, float ChanceDeErro, float ChanceDeLob, float ChanceDeRemateParaFora = 0);

public static class Perfis
{
    public static readonly PerfilDeIA Facil = new(Velocidade: 4.6f, Reacao: 0.45f, ErroDeMira: 1.5f, ErroDePrevisao: 1.1f, ChanceDeErro: 0.14f, ChanceDeLob: 0.2f, ChanceDeRemateParaFora: 0.05f);
    public static readonly PerfilDeIA Medio = new(Velocidade: 5.6f, Reacao: 0.25f, ErroDeMira: 0.9f, ErroDePrevisao: 0.7f, ChanceDeErro: 0.07f, ChanceDeLob: 0.3f, ChanceDeRemateParaFora: 0.15f);
    public static readonly PerfilDeIA Dificil = new(Velocidade: 6.6f, Reacao: 0.10f, ErroDeMira: 0.5f, ErroDePrevisao: 0.35f, ChanceDeErro: 0.025f, ChanceDeLob: 0.35f, ChanceDeRemateParaFora: 0.30f);
    /// <summary>O parceiro do humano: confiável sem ser um muro.</summary>
    public static readonly PerfilDeIA Parceiro = new(Velocidade: 5.8f, Reacao: 0.18f, ErroDeMira: 0.8f, ErroDePrevisao: 0.55f, ChanceDeErro: 0.04f, ChanceDeLob: 0.3f, ChanceDeRemateParaFora: 0.15f);

    public static PerfilDeIA Por(Dificuldade d) => d switch
    {
        Dificuldade.Facil => Facil,
        Dificuldade.Dificil => Dificil,
        _ => Medio,
    };
}

/// <summary>
/// Posição, velocidade com inércia (acelera e freia como gente), alcance e o balanço da raquete.
/// Velocidades de padel: tiro curto a 6–7 m/s, aceleração ~9 m/s², frenagem mais forte que a arrancada.
/// </summary>
public sealed class Jogador
{
    public const float VelocidadeDoHumano = 6.4f;
    public const float DuracaoDoBalanco = 0.3f;
    /// <summary>Instante do balanço em que o contato sai perfeito (apertar cedo = bola ainda longe; tarde = bola em cima do corpo).</summary>
    public const float MomentoIdealDoBalanco = 0.12f;

    public int Time { get; }
    /// <summary>0 = metade direita do time, 1 = esquerda.</summary>
    public int Indice { get; }
    public string Nome { get; }
    public bool Humano { get; set; }
    /// <summary>Mão da raquete. Destro bate de drive a bola à direita dele (olhando pra rede); canhoto, à esquerda.</summary>
    public bool Destro { get; set; } = true;
    /// <summary>Velocidade máxima, m/s.</summary>
    public float Velocidade { get; set; }
    public float Aceleracao { get; set; } = 9f;
    public float Frenagem { get; set; } = 14f;
    public int Lado { get; }
    public float X, Y;
    public float Vx, Vy;
    /// <summary>Alcance máximo (esticado).</summary>
    public float Alcance { get; }
    /// <summary>Até aqui o golpe sai confortável; além disso é esticada.</summary>
    public float AlcanceConfortavel { get; } = 0.85f;
    /// <summary>Distância do corpo em que o contato é perfeito; abaixo disso não conta como esticada.
    /// O ponto bom fica AO LADO do corpo, a essa distância, do lado da raquete (drive) ou do outro (revés): <see cref="PontoDeContato"/>.</summary>
    public const float DistanciaIdealDoContato = 0.6f;
    /// <summary>Bola a menos disso da linha do corpo, na lateral, é "bola no corpo": o braço não tem espaço e o golpe sai pior.</summary>
    public const float DistanciaDoCorpo = 0.3f;
    /// <summary>Acima disso o revés vira revés alto (a bandeja de revés): golpe de quem sabe.</summary>
    public const float AlturaDoRevesAlto = 1.6f;
    private const float PesoDaBolaNoCorpo = 0.35f, PesoDoReves = 0.1f, PesoDoRevesAlto = 0.45f;
    /// <summary>Até onde o corpo vai na largura (o resto da quadra é o braço).</summary>
    private const float LimiteLateral = 4.7f;
    /// <summary>Contrapared: a bola a até isso da própria parede de fundo (e atrás do corpo).</summary>
    public const float DistanciaDoVidroDaContrapared = 1.2f;
    public float AlturaMaxima { get; } = 2.7f;
    public float Cooldown;
    public int Golpes;
    public int BalancosNoAr;
    /// <summary>Segundos restantes do balanço; 0 = raquete parada.</summary>
    public float Balanco { get; private set; }
    public float TempoNoBalanco { get; private set; }
    public bool BalancoDeLob { get; private set; }
    /// <summary>Verdadeiro só no passo em que o balanço acabou sem tocar na bola (raquete no ar).</summary>
    public bool BalancoTerminouNoAr { get; private set; }

    public Jogador(int time, int indice, string nome, bool humano, float velocidade)
    {
        Time = time; Indice = indice; Nome = nome; Humano = humano; Velocidade = velocidade;
        Lado = Quadra.LadoDoTime(time);
        Alcance = humano ? 1.35f : 1.1f;   // braço + raquete; o humano ganha folga de propósito
        X = XDaMetade;
        Y = Lado * 6f;
    }

    public float XDaMetade => Lado * (Indice == 0 ? 2.5f : -2.5f);
    public bool Balancando => Balanco > 0;
    public float Rapidez => MathF.Sqrt(Vx * Vx + Vy * Vy);

    /// <summary>+1 se a raquete está na mão direita (no referencial dele, olhando pra rede), -1 se na esquerda.</summary>
    public int LadoDaRaquete => Destro ? 1 : -1;

    /// <summary>Quanto x está à direita (positivo) ou à esquerda (negativo) de quem olha pra rede: no lado +1 a direita é +x; no -1, -x.</summary>
    public float LateralDe(float x) => (x - X) * Lado;

    /// <summary>Quanto y está à frente (positivo, rumo à rede) ou atrás (negativo) do corpo.</summary>
    public float FrontalDe(float y) => (Y - y) * Lado;

    /// <summary>Drive se a bola está do lado da raquete (ou bem na linha do corpo), revés se do outro.</summary>
    public LadoDoGolpe LadoDoGolpePara(float x) => LateralDe(x) * LadoDaRaquete >= 0 ? LadoDoGolpe.Drive : LadoDoGolpe.Reves;
    public LadoDoGolpe LadoDoGolpePara(Bola bola) => LadoDoGolpePara(bola.X);

    /// <summary>Ponto (no mundo) em que o contato de drive ou de revés sai perfeito: na linha do corpo, a DistanciaIdealDoContato pro lado.</summary>
    public (float X, float Y) PontoDeContato(LadoDoGolpe lado)
    {
        int paraOLado = lado == LadoDoGolpe.Drive ? LadoDaRaquete : -LadoDaRaquete;   // no referencial dele
        return (X + paraOLado * Lado * DistanciaIdealDoContato, Y);
    }

    /// <summary>
    /// Onde o corpo deve ficar (x no mundo) pra bola que vai passar em xDaBola chegar ao ponto de contato, e não no peito:
    /// de drive ou de revés, o que der menos caminho — com preferência pelo drive, maior na bola alta (revés alto é o mais difícil).
    /// </summary>
    public float XParaBater(float xDaBola, bool bolaAlta) => XParaBater(xDaBola, LadoParaBater(xDaBola, bolaAlta));

    /// <summary>Onde o corpo deve ficar (x no mundo) pra bola que passa em xDaBola sair do lado dado, a DistanciaIdealDoContato.</summary>
    public float XParaBater(float xDaBola, LadoDoGolpe lado) =>
        xDaBola - (lado == LadoDoGolpe.Drive ? 1 : -1) * LadoDaRaquete * Lado * DistanciaIdealDoContato;

    /// <summary>De que lado bater a bola que vai passar em xDaBola: a escolha de <see cref="XParaBater(float, bool)"/>.</summary>
    public LadoDoGolpe LadoParaBater(float xDaBola, bool bolaAlta)
    {
        float xDrive = XParaBater(xDaBola, LadoDoGolpe.Drive), xReves = XParaBater(xDaBola, LadoDoGolpe.Reves);
        bool driveCabe = MathF.Abs(xDrive) <= LimiteLateral, revesCabe = MathF.Abs(xReves) <= LimiteLateral;
        if (driveCabe != revesCabe) return driveCabe ? LadoDoGolpe.Drive : LadoDoGolpe.Reves;
        float preferenciaPeloDrive = bolaAlta ? 1.2f : 0.4f;   // metros a mais que ele anda pra não bater de revés
        return MathF.Abs(xDrive - X) <= MathF.Abs(xReves - X) + preferenciaPeloDrive ? LadoDoGolpe.Drive : LadoDoGolpe.Reves;
    }

    /// <summary>A bola passou do corpo e está junto ao próprio vidro de fundo: a situação da contrapared.</summary>
    public bool BolaAtrasJuntoAoVidro(Bola bola) =>
        Quadra.LadoDe(bola.Y) == Lado && FrontalDe(bola.Y) < 0 && Quadra.MeioComprimento - MathF.Abs(bola.Y) <= DistanciaDoVidroDaContrapared;

    private void Limitar()
    {
        float x = Util.Limitar(X, -LimiteLateral, LimiteLateral);
        float y = Lado > 0 ? Util.Limitar(Y, 0.5f, 9.7f) : Util.Limitar(Y, -9.7f, -0.5f);
        if (x != X) { X = x; Vx = 0; }
        if (y != Y) { Y = y; Vy = 0; }
    }

    /// <summary>Direção desejada no MUNDO (módulo até 1 = velocidade máxima); a inércia faz o resto.</summary>
    public void Mover(float dx, float dy, float dt)
    {
        float n = MathF.Sqrt(dx * dx + dy * dy);
        float desejadoX = 0, desejadoY = 0;
        if (n > 1e-6f)
        {
            float forca = MathF.Min(1, n);
            desejadoX = dx / n * forca * Velocidade;
            desejadoY = dy / n * forca * Velocidade;
        }
        float ddx = desejadoX - Vx, ddy = desejadoY - Vy;
        float dd = MathF.Sqrt(ddx * ddx + ddy * ddy);
        if (dd > 1e-6f)
        {
            bool acelerando = desejadoX * desejadoX + desejadoY * desejadoY > Vx * Vx + Vy * Vy;
            float passo = (acelerando ? Aceleracao : Frenagem) * dt;
            if (dd <= passo) { Vx = desejadoX; Vy = desejadoY; }
            else { Vx += ddx / dd * passo; Vy += ddy / dd * passo; }
        }
        X += Vx * dt;
        Y += Vy * dt;
        Limitar();
    }

    /// <summary>Vai até o ponto freando pra chegar parado. Devolve true quando chegou.</summary>
    public bool IrPara(float x, float y, float dt)
    {
        float dx = x - X, dy = y - Y;
        float d = MathF.Sqrt(dx * dx + dy * dy);
        if (d < 0.05f) { Mover(0, 0, dt); return true; }
        float vDesejada = MathF.Min(Velocidade, MathF.Sqrt(2 * Frenagem * d));
        float f = vDesejada / Velocidade;
        Mover(dx / d * f, dy / d * f, dt);
        return d < 0.1f;
    }

    /// <summary>Tempo pra percorrer d metros partindo do repouso (estimativa conservadora usada pela IA).</summary>
    public float TempoParaChegar(float d)
    {
        float tAcel = Velocidade / Aceleracao;
        float dAcel = 0.5f * Aceleracao * tAcel * tAcel;
        return d <= dAcel ? MathF.Sqrt(2 * d / Aceleracao) : tAcel + (d - dAcel) / Velocidade;
    }

    public float DistanciaAte(float x, float y) => Util.Distancia(X, Y, x, y);

    public bool Alcanca(Bola bola) => DistanciaAte(bola.X, bola.Y) <= Alcance && bola.Z >= 0 && bola.Z <= AlturaMaxima;
    public bool AlcancaConfortavelmente(Bola bola) => DistanciaAte(bola.X, bola.Y) <= AlcanceConfortavel && bola.Z >= 0 && bola.Z <= AlturaMaxima;

    /// <summary>
    /// Quando bater: no ponto confortável; esticado só se a bola já está indo embora (última chance) ou,
    /// no balanço manual, se ele está acabando. É o que faz o contato sair perto do corpo em vez de na
    /// ponta do alcance — pra IA e pra humano. Coerente com o corpo: os pontos ideais de drive e de revés (ao lado,
    /// a DistanciaIdealDoContato) ficam dentro do alcance confortável; a bola no corpo também fica, e esperar não a
    /// tira de lá (ela vem em cima do jogador) — então bate na hora e a <see cref="DificuldadeDoGolpe"/> cobra o preço.
    /// O que tira a bola do corpo é o posicionamento (na IA: <see cref="XParaBater(float, LadoDoGolpe)"/> com a leitura
    /// que afina enquanto a bola chega), não a espera — e ele só vale o quanto a leitura e as pernas valem. Medido em
    /// 25/09: com ~1 s pra bola chegar, bola no corpo em 0 de 30 contatos do difícil e 12 de 30 do fácil (GolpesTests);
    /// em 10 partidas IA x IA por nível, 32 / 28 / 23 % dos golpes (fácil / médio / difícil), contra 38 / 39 / 45 % com o
    /// corpo em cima da bola — boa parte dos contatos sai com o jogador ainda correndo.
    /// </summary>
    public bool PodeBaterAgora(Bola bola)
    {
        if (!Alcanca(bola)) return false;
        if (DistanciaAte(bola.X, bola.Y) <= AlcanceConfortavel) return true;
        float indoEmbora = (bola.X - X) * bola.Vx + (bola.Y - Y) * bola.Vy;
        return indoEmbora > 0 || (Balancando && Balanco <= 0.08f);
    }

    public void IniciarBalanco(bool lob)
    {
        Balanco = DuracaoDoBalanco;
        TempoNoBalanco = 0;
        BalancoDeLob = lob;
    }

    /// <summary>A raquete tocou na bola: o balanço termina sem contar como raquete no ar.</summary>
    public void EncerrarBalanco() => Balanco = 0;

    public void AvancarTempo(float dt)
    {
        Cooldown = MathF.Max(0, Cooldown - dt);
        BalancoTerminouNoAr = false;
        if (Balanco > 0)
        {
            Balanco = MathF.Max(0, Balanco - dt);
            TempoNoBalanco += dt;
            if (Balanco == 0) BalancoTerminouNoAr = true;
        }
    }

    /// <summary>
    /// 0 = golpe confortável (drive, bola ao lado do corpo, lenta, acima do tornozelo). Cresce quando esticado, com a bola
    /// rente ao chão ou rápida, com a bola no corpo (a menos de DistanciaDoCorpo na lateral: sem espaço pro braço), de
    /// revés (um pouco) e de revés alto (muito: bandeja de revés é golpe de quem sabe).
    /// </summary>
    public float DificuldadeDoGolpe(Bola bola)
    {
        float esticado = Util.Limitar((DistanciaAte(bola.X, bola.Y) - DistanciaIdealDoContato) / (Alcance - DistanciaIdealDoContato), 0, 1);
        float lateral = MathF.Abs(LateralDe(bola.X));
        float noCorpo = lateral < DistanciaDoCorpo ? PesoDaBolaNoCorpo * (1 - lateral / DistanciaDoCorpo) : 0;
        float reves = LadoDoGolpePara(bola) == LadoDoGolpe.Reves ? (bola.Z > AlturaDoRevesAlto ? PesoDoRevesAlto : PesoDoReves) : 0;
        return Util.Limitar(esticado * 0.8f + (bola.Z < 0.3f ? 0.4f : 0) + (bola.Rapidez > 18 ? 0.4f : 0) + noCorpo + reves, 0, 1.6f);
    }
}

/// <summary>
/// Controla os jogadores de um time que não são humanos. Quando a bola muda de trajetória, simula a
/// física pra frente (mesma Bola, mesmo passo), decide quem busca e onde — com erro de leitura por
/// dificuldade, corrigido a cada quique/parede e afinando enquanto a bola chega — e o outro cobre. Quem busca não vai em
/// cima da bola: escolhe no plano o lado (drive ou revés, <see cref="Jogador.LadoParaBater"/>) e põe o corpo ao lado
/// dela (<see cref="Jogador.XParaBater(float, LadoDoGolpe)"/>) pela leitura do momento. Na hora de bater escolhe o alvo:
/// o buraco entre os adversários, lob ou chiquita quando eles estão na rede, bandeja/víbora/smash em bola alta
/// (e às vezes o remate por 3 / por 4), contrapared quando não sobra outra, erro forçado quando esticada.
/// Cada tipo de golpe imprime o efeito do padel de verdade e sai com o lado do corpo (drive/revés) certo.
/// </summary>
public sealed class IA
{
    private sealed class Plano
    {
        public required Jogador Responsavel;
        /// <summary>Onde (y) e em quanto tempo, a partir de PlanejadoEm, a bola chega ao ponto escolhido. O x do corpo sai de <see cref="XDoCorpo"/>.</summary>
        public float AlvoY, AlvoT;
        /// <summary>Início da trajetória (conta o tempo de reação); a releitura num quique/parede o mantém.</summary>
        public float CriadoEm;
        /// <summary>Quando esta leitura foi feita: AlvoT conta daqui.</summary>
        public float PlanejadoEm;
        /// <summary>x de verdade da bola no ponto escolhido e o sorteio do erro de leitura (desvios-padrão): o erro em metros
        /// é RuidoX · <see cref="ErroDeLeitura"/>(tempo que falta), então afina enquanto a bola chega.</summary>
        public float XDaBola, RuidoX;
        /// <summary>Drive ou revés, decidido no plano: o passo lateral não troca de lado no meio do caminho.</summary>
        public LadoDoGolpe Lado;
    }

    private readonly record struct Candidato(float X, float Y, float Z, float T, bool Voleio);

    public int Time { get; }
    public PerfilDeIA Perfil { get; }
    public Formacao Posicao { get; private set; } = Formacao.Fundo;
    private readonly Aleatorio _aleatorio;
    private Plano? _plano;
    private float _relogio;

    public IA(int time, PerfilDeIA perfil, Aleatorio aleatorio)
    {
        Time = time; Perfil = perfil; _aleatorio = aleatorio;
    }

    /// <summary>Jogador que está indo buscar a bola, se houver (pra depuração e pra animação).</summary>
    public Jogador? Responsavel => _plano?.Responsavel;

    public void AoSacar(Partida partida, int timeSacador)
    {
        Posicao = timeSacador == Time ? Formacao.Rede : Formacao.Fundo;   // quem saca sobe; quem recebe fica atrás
        Planejar(partida, novaTrajetoria: true);
    }

    public void AoGolpear(Partida partida, Golpe golpe, Jogador jogador)
    {
        if (jogador.Time == Time)
        {
            bool naFrente = MathF.Abs(jogador.Y) < 5.5f;
            bool sobe = golpe.Tipo is TipoDeGolpe.Lob or TipoDeGolpe.Smash or TipoDeGolpe.Bandeja or TipoDeGolpe.Vibora
                or TipoDeGolpe.Chiquita or TipoDeGolpe.SmashPor3 or TipoDeGolpe.SmashPor4;   // a chiquita é o passaporte pra rede
            Posicao = sobe || naFrente ? Formacao.Rede : Formacao.Fundo;
        }
        Planejar(partida, novaTrajetoria: true);
    }

    /// <summary>Bola quicou ou bateu na parede: releitura da trajetória, sem novo tempo de reação.</summary>
    public void AoRebater(Partida partida)
    {
        if (_plano is not null) Planejar(partida, novaTrajetoria: false);
    }

    private void Planejar(Partida partida, bool novaTrajetoria)
    {
        var bola = partida.Bola;
        var arbitro = partida.Arbitro;
        var planoAnterior = _plano;
        _plano = null;
        if (!bola.EmJogo || arbitro.Golpeador == Time) return;
        var candidatos = Prever(bola, arbitro);
        if (candidatos.Count == 0) return;
        var meus = partida.JogadoresDoTime(Time);

        Jogador? escolhido = null;
        Candidato alvo = default;
        foreach (var c in candidatos)
        {
            Jogador? melhor = null;
            float melhorFolga = float.NegativeInfinity;
            foreach (var j in meus)
            {
                float distancia = j.DistanciaAte(c.X, c.Y);
                if (c.Voleio && !(MathF.Abs(j.Y) < 5 && distancia < 2.5f)) continue;
                float folga = c.T - (j.TempoParaChegar(distancia) + (j.Humano ? 0 : Perfil.Reacao));
                if (folga >= 0 && folga > melhorFolga) { melhorFolga = folga; melhor = j; }
            }
            if (melhor is not null) { escolhido = melhor; alvo = c; break; }
        }
        if (escolhido is null)
        {
            // Ninguém chega a tempo: o mais perto do último ponto possível tenta mesmo assim.
            alvo = candidatos[^1];
            float menor = float.PositiveInfinity;
            foreach (var j in meus)
            {
                float d = j.DistanciaAte(alvo.X, alvo.Y);
                if (d < menor) { menor = d; escolhido = j; }
            }
        }

        if (escolhido is null) return;   // só se as distâncias vierem NaN: sem plano, o time fica na formação

        // Erro de leitura: maior quanto mais longe (no tempo) a bola está; a releitura (quique, parede) sorteia de novo e,
        // entre uma e outra, o x lido afina enquanto a bola chega (Reagir).
        float sigma = ErroDeLeitura(alvo.T);
        int meuLado = Quadra.LadoDoTime(Time);
        float ruidoX = _aleatorio.Gaussiana();
        float xLido = Util.Limitar(alvo.X + ruidoX * sigma, -4.7f, 4.7f);
        _plano = new Plano
        {
            Responsavel = escolhido,
            // O corpo fica ao lado da bola (drive ou revés), não em cima dela: o lado se decide aqui, o x em XDoCorpo.
            Lado = escolhido.LadoParaBater(xLido, bolaAlta: alvo.Z > Jogador.AlturaDoRevesAlto),
            AlvoY = meuLado * Util.Limitar(MathF.Abs(alvo.Y) + _aleatorio.Gaussiana() * sigma * 0.7f, 0.5f, 9.7f),
            AlvoT = alvo.T,
            CriadoEm = novaTrajetoria || planoAnterior is null ? _relogio : planoAnterior.CriadoEm,
            PlanejadoEm = _relogio,
            XDaBola = alvo.X,
            RuidoX = ruidoX,
        };
    }

    /// <summary>Desvio do erro de leitura (m) com a bola a tantos segundos do ponto de contato: o do perfil a 0,8 s ou mais, até zero.</summary>
    private float ErroDeLeitura(float segundosAteABola) => Perfil.ErroDePrevisao * Util.Limitar(segundosAteABola / 0.8f, 0, 1);

    /// <summary>
    /// Onde o corpo vai agora: ao lado do x que ele lê da bola neste instante (o erro sorteado no plano, encolhendo com o
    /// tempo que falta). O jogador bom termina o passo lateral no lugar; o fraco lê mal por mais tempo e chega atrasado.
    /// </summary>
    private float XDoCorpo(Plano plano, Jogador j)
    {
        float falta = plano.AlvoT - (_relogio - plano.PlanejadoEm);
        float xLido = Util.Limitar(plano.XDaBola + plano.RuidoX * ErroDeLeitura(falta), -4.7f, 4.7f);
        return Util.Limitar(j.XParaBater(xLido, plano.Lado), -4.7f, 4.7f);
    }

    /// <summary>Pontos em que a bola estará batível no nosso lado, em ordem de tempo.</summary>
    private List<Candidato> Prever(Bola bola, Arbitro arbitro)
    {
        var clone = bola.Clonar();
        int meuLado = Quadra.LadoDoTime(Time);
        const float passo = 1f / 60f;
        var candidatos = new List<Candidato>();
        var eventos = new List<EventoDaBola>();
        int quiques = 0;
        float t = 0;
        bool acabou = false;
        for (int i = 0; i < 240 && clone.EmJogo && !clone.Parada && !acabou; i++)
        {
            eventos.Clear();
            clone.Avancar(passo, eventos);
            t += passo;
            foreach (var e in eventos)
            {
                if (e.Tipo != TipoDeEventoDaBola.Quique) continue;
                if (e.Lado == meuLado) quiques += 1; else acabou = true;
                if (quiques >= 2) acabou = true;
            }
            if (acabou) break;
            if (Quadra.LadoDe(clone.Y) != meuLado) continue;
            if (quiques == 0 && arbitro.EmSaque) continue;
            if (clone.Rolando) break;
            if (clone.Z < 0.35f || clone.Z > 2.3f) continue;
            if (quiques > 0 && clone.Vz > 0 && clone.Z < 0.9f) continue;   // subindo logo depois do quique: espera
            candidatos.Add(new Candidato(clone.X, clone.Y, clone.Z, t, Voleio: quiques == 0));
            if (candidatos.Count > 150) break;
        }
        return candidatos;
    }

    public void Reagir(float dt, Partida partida)
    {
        _relogio += dt;
        foreach (var j in partida.JogadoresDoTime(Time))
        {
            if (j.Humano) continue;
            var plano = _plano;
            if (plano is not null && plano.Responsavel == j)
            {
                if (_relogio - plano.CriadoEm < Perfil.Reacao) { j.Mover(0, 0, dt); continue; }
                j.IrPara(XDoCorpo(plano, j), plano.AlvoY, dt);
            }
            else
            {
                float profundidade = Posicao == Formacao.Rede ? 3.2f : 6.5f;
                j.IrPara(j.XDaMetade, j.Lado * profundidade, dt);
            }
        }
    }

    /// <summary>Com a bola acima disso e o jogador a menos de DistanciaDaRedeDoRemateParaFora da rede, a IA pode tentar o por 3 / por 4.</summary>
    private const float AlturaDoRemateParaFora = 2.2f, DistanciaDaRedeDoRemateParaFora = 4f;
    /// <summary>Com os dois adversários na rede e a bola baixa no fundo, metade das vezes chiquita (a outra metade segue: lob ou fundo).</summary>
    private const float ChanceDeChiquita = 0.5f;

    /// <summary>O golpe da IA, com o lado do corpo (drive/revés) em que a bola está.</summary>
    public Golpe EscolherGolpe(Jogador jogador, Bola bola, Partida partida) =>
        Escolher(jogador, bola, partida) with { Lado = jogador.LadoDoGolpePara(bola) };

    private Golpe Escolher(Jogador jogador, Bola bola, Partida partida)
    {
        var r = _aleatorio;
        int ladoDoAlvo = -jogador.Lado;
        var adversarios = partida.JogadoresDoTime(1 - Time);
        float dificuldade = jogador.DificuldadeDoGolpe(bola);

        if (r.Proximo() < Perfil.ChanceDeErro * (1 + 2.5f * dificuldade))
        {
            return r.Proximo() < 0.5f
                ? new Golpe((r.Proximo() - 0.5f) * 6, ladoDoAlvo * 0.3f, 0.45f, TipoDeGolpe.Erro, IgnorarRede: true)     // na rede
                : new Golpe((r.Proximo() - 0.5f) * 6, ladoDoAlvo * 11.5f, 0.55f, TipoDeGolpe.Erro, IgnorarRede: true);   // no vidro sem quicar
        }
        // Último recurso: a bola passou, está junto ao próprio vidro e depois dele não volta batível — contrapared.
        if (jogador.BolaAtrasJuntoAoVidro(bola) && !VoltaBativel(bola, partida.Arbitro)
            && GolpesEspeciais.Contrapared(bola.X, bola.Y, bola.Z, ladoDoAlvo, alvoX: 0) is Golpe contrapared)
            return contrapared;
        // Bola muito alta perto da rede: às vezes (pela dificuldade) tenta tirar a bola da quadra, se a física deixar.
        if (bola.Z > AlturaDoRemateParaFora && MathF.Abs(jogador.Y) < DistanciaDaRedeDoRemateParaFora && r.Proximo() < Perfil.ChanceDeRemateParaFora)
        {
            int saidaCruzada = bola.X >= 0 ? -1 : 1;   // o por 3 sai pela lateral do outro lado: cruzado é o que a física mais deixa
            Golpe? Por3() => GolpesEspeciais.SmashPor3(bola.X, bola.Y, bola.Z, ladoDoAlvo, saidaCruzada);
            Golpe? Por4() => GolpesEspeciais.SmashPor4(bola.X, bola.Y, bola.Z, ladoDoAlvo, alvoX: bola.X);
            var remate = r.Proximo() < 0.5f ? Por3() ?? Por4() : Por4() ?? Por3();
            if (remate is Golpe paraFora) return paraFora;
        }
        // Bola alta: smash bem na rede; senão bandeja (segura, funda, com slice) ou víbora (agressiva, com sidespin).
        if (bola.Z > 1.5f && MathF.Abs(jogador.Y) < 6.5f)
        {
            bool bemAlta = bola.Z > 2.0f && MathF.Abs(jogador.Y) < 4.5f;
            if (bemAlta && r.Proximo() < 0.6f)
                return new Golpe((r.Proximo() - 0.5f) * 7, ladoDoAlvo * (3 + r.Proximo() * 3), 0.4f, TipoDeGolpe.Smash, Efeito: new Efeito(1500, 0));
            if (r.Proximo() < 0.35f)
            {
                float paraOLado = r.Proximo() < 0.5f ? -1 : 1;
                return new Golpe(3.8f * paraOLado, ladoDoAlvo * 6.5f, 0.7f, TipoDeGolpe.Vibora, Efeito: new Efeito(-800, 1800 * paraOLado * ladoDoAlvo));
            }
            return new Golpe((r.Proximo() - 0.5f) * 6, ladoDoAlvo * 7.5f, 0.95f, TipoDeGolpe.Bandeja, Efeito: new Efeito(-1500, 500));
        }
        int adversariosNaRede = adversarios.Count(a => MathF.Abs(a.Y) < 4.5f);
        // Os dois na rede, eu no fundo com a bola baixa: chiquita nos pés de um deles.
        if (adversariosNaRede == 2 && MathF.Abs(jogador.Y) > 6.5f && bola.Z < 1.0f && r.Proximo() < ChanceDeChiquita
            && GolpesEspeciais.Chiquita(bola.X, bola.Y, bola.Z, ladoDoAlvo, alvoX: adversarios[r.Proximo() < 0.5f ? 0 : 1].X) is Golpe chiquita)
            return chiquita;
        if (adversariosNaRede >= 1 && r.Proximo() < Perfil.ChanceDeLob)
        {
            return new Golpe((r.Proximo() - 0.5f) * 5, ladoDoAlvo * 8.2f, 1.6f, TipoDeGolpe.Lob, Efeito: new Efeito(-400, 0));
        }
        // Golpe de fundo pro buraco: o canto mais longe dos dois adversários.
        float melhorX = 0, melhorDistancia = -1;
        foreach (float x in new[] { -3.3f, 0f, 3.3f })
        {
            float d = adversarios.Min(a => a.DistanciaAte(x, ladoDoAlvo * 6.8f));
            if (d > melhorDistancia) { melhorDistancia = d; melhorX = x; }
        }
        float ruido = Perfil.ErroDeMira * (1 + 1.2f * dificuldade);
        float alvoX = Util.Limitar(melhorX + r.Gaussiana() * ruido, -4.5f, 4.5f);
        float alvoY = ladoDoAlvo * Util.Limitar(MathF.Abs(ladoDoAlvo * 6.8f + r.Gaussiana() * ruido * 0.6f), 2, 9.4f);
        return new Golpe(alvoX, alvoY, 0.8f, TipoDeGolpe.Normal, Efeito: new Efeito(1200, 0));
    }

    /// <summary>
    /// Depois do próprio vidro, a bola ainda fica batível do nosso lado (entre 0,35 e 2,3 m, vindo pra rede) antes do
    /// quique que encerra o ponto? Se não, bater agora é a última chance. Mesma física, simulada pra frente.
    /// </summary>
    private bool VoltaBativel(Bola bola, Arbitro arbitro)
    {
        var clone = bola.Clonar();
        int meuLado = Quadra.LadoDoTime(Time);
        int quiquesQueRestam = arbitro.QuicouNoReceptor ? 0 : 1;
        bool bateuNoVidro = false;
        var eventos = new List<EventoDaBola>(4);
        for (int i = 0; i < 180 && clone.EmJogo && !clone.Parada && !clone.Rolando; i++)
        {
            eventos.Clear();
            clone.Avancar(1f / 60f, eventos);
            foreach (var e in eventos)
            {
                if (e.Tipo is TipoDeEventoDaBola.Saiu or TipoDeEventoDaBola.CruzouRede) return false;
                if (e.Tipo == TipoDeEventoDaBola.Parede && e.Lado == meuLado) bateuNoVidro = true;
                if (e.Tipo == TipoDeEventoDaBola.Quique && e.Lado == meuLado && --quiquesQueRestam < 0) return false;
            }
            bool vemPraRede = clone.Vy * meuLado < 0;
            if (bateuNoVidro && vemPraRede && clone.Z is >= 0.35f and <= 2.3f) return true;
        }
        return false;
    }
}
