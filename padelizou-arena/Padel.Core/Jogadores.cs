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

/// <summary>
/// ChanceDeRemateParaFora: com a bola muito alta perto da rede, quanto tenta o remate por 3 / por 4 (se a física deixar).
/// <para>
/// A formação da dupla (<see cref="IA"/>) — o que mais separa os níveis, porque é o que mais separa quem joga padel de quem
/// só bate na bola. Os padrões são os do Médio.
/// </para>
/// <list type="bullet">
/// <item><b>AtrasoDaFormacao</b> (s): quanto quem não bateu demora pra acompanhar a mudança da dupla (subir, recuar) e o
///   deslocamento da bola — é também a constante de tempo com que o centro da dupla segue o x da bola e com que o parceiro
///   de um humano lê a profundidade dele. Tempo de reação de escolha de gente é 0,3–0,4 s; quem joga bem antecipa pelo
///   golpe do parceiro.</item>
/// <item><b>AcompanhamentoDaBola</b> (0 a 1): quanto o centro da dupla segue o x da bola (a "cremallera"): 0 = cada um parado no
///   meio da sua metade; 0,5 = com a bola junto à lateral (4,7 m), o centro vai 2,35 m pro lado dela e o parceiro de longe
///   fica perto do meio da quadra, fechando a diagonal.</item>
/// <item><b>SeparacaoDaDupla</b> (m): distância lateral entre os dois. A quadra tem 10 m e o alcance de um jogador é ~1 m de
///   cada lado: 5 m (o antigo, cada um no meio da sua metade) deixa 3 m de buraco no meio; 3,6 m deixa 1,6 m.</item>
/// <item><b>FolgaDoBloco</b> (m): a maior diferença de profundidade que um aceita do parceiro quando a dupla anda — quem está
///   mais à frente espera o outro pra subirem juntos, e quem não vai na bola recua (ou sobe) até ficar a essa distância da
///   profundidade de quem vai.</item>
/// <item><b>ChanceDeDesencontro</b> (0 a 1): em cada mudança da dupla, a chance de quem não bateu nem perceber e ficar onde
///   estava (um na rede e outro no fundo) até a próxima — erro de quem começa.</item>
/// </list>
/// </summary>
public sealed record PerfilDeIA(float Velocidade, float Reacao, float ErroDeMira, float ErroDePrevisao, float ChanceDeErro, float ChanceDeLob, float ChanceDeRemateParaFora = 0,
    float AtrasoDaFormacao = 0.35f, float AcompanhamentoDaBola = 0.35f, float SeparacaoDaDupla = 4.0f, float FolgaDoBloco = 1.0f, float ChanceDeDesencontro = 0);

/// <summary>
/// Formação, por nível (números explicados em <see cref="PerfilDeIA"/>):
/// <list type="bullet">
/// <item><b>Fácil</b> desencontra: acompanha tarde (0,7 s — o dobro da reação de escolha), segue pouco a bola (0,15: com ela na
///   lateral o centro anda 0,7 m), fica aberto (5 m, cada um no meio da sua metade como a IA de antes: o meio aberto), aceita 2,5 m de
///   diferença de profundidade (sobe e recua sem olhar o parceiro) e em 3 de cada 10 mudanças um fica pra trás.</item>
/// <item><b>Médio</b> acompanha com atraso: 0,35 s (a reação de escolha de gente), 0,35 da bola, 4 m, 1 m de folga, sem desencontro.</item>
/// <item><b>Difícil</b> fecha o meio e sobe/recua junto na hora: 0,10 s (o mesmo da reação à bola — lê o golpe do parceiro), 0,5
///   da bola, 3,6 m, 0,5 m de folga.</item>
/// <item><b>Parceiro</b> (do humano): entre o Médio e o Difícil. Com parceiro humano, a profundidade sai do humano (rede ou
///   fundo, como ele) e a largura é a metade da IA; os números de formação valem quando ele joga com outra IA.</item>
/// </list>
/// </summary>
public static class Perfis
{
    public static readonly PerfilDeIA Facil = new(Velocidade: 4.6f, Reacao: 0.45f, ErroDeMira: 1.5f, ErroDePrevisao: 1.1f, ChanceDeErro: 0.14f, ChanceDeLob: 0.2f, ChanceDeRemateParaFora: 0.05f,
        AtrasoDaFormacao: 0.7f, AcompanhamentoDaBola: 0.15f, SeparacaoDaDupla: 5.0f, FolgaDoBloco: 2.5f, ChanceDeDesencontro: 0.3f);
    public static readonly PerfilDeIA Medio = new(Velocidade: 5.6f, Reacao: 0.25f, ErroDeMira: 0.9f, ErroDePrevisao: 0.7f, ChanceDeErro: 0.07f, ChanceDeLob: 0.3f, ChanceDeRemateParaFora: 0.15f,
        AtrasoDaFormacao: 0.35f, AcompanhamentoDaBola: 0.35f, SeparacaoDaDupla: 4.0f, FolgaDoBloco: 1.0f, ChanceDeDesencontro: 0);
    public static readonly PerfilDeIA Dificil = new(Velocidade: 6.6f, Reacao: 0.10f, ErroDeMira: 0.5f, ErroDePrevisao: 0.35f, ChanceDeErro: 0.025f, ChanceDeLob: 0.35f, ChanceDeRemateParaFora: 0.30f,
        AtrasoDaFormacao: 0.10f, AcompanhamentoDaBola: 0.5f, SeparacaoDaDupla: 3.6f, FolgaDoBloco: 0.5f, ChanceDeDesencontro: 0);
    /// <summary>O parceiro do humano: confiável sem ser um muro.</summary>
    public static readonly PerfilDeIA Parceiro = new(Velocidade: 5.8f, Reacao: 0.18f, ErroDeMira: 0.8f, ErroDePrevisao: 0.55f, ChanceDeErro: 0.04f, ChanceDeLob: 0.3f, ChanceDeRemateParaFora: 0.15f,
        AtrasoDaFormacao: 0.2f, AcompanhamentoDaBola: 0.4f, SeparacaoDaDupla: 3.8f, FolgaDoBloco: 0.8f, ChanceDeDesencontro: 0);

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
    /// <summary>
    /// Instante do balanço em que a raquete passa pelo ponto de contato — o único em que ela pode tocar a bola (no modo
    /// manual): o golpe é perfeito se a bola estiver no ponto ideal (<see cref="PontoDeContato"/>) nesse instante. Apertar
    /// cedo = a bola ainda longe (esticado, ou raquete no ar se fora do alcance); tarde = a bola já passou ou está no corpo.
    /// </summary>
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
    /// <summary>
    /// Até aqui a bola está perto do corpo sem esticar muito (os pontos ideais de drive e de revés ficam dentro). Era onde a
    /// raquete batia; desde que o contato sai no instante do balanço (manual) ou no ponto mais perto do ideal (IA e
    /// Automático), é só referência de distância — quem mede a qualidade é <see cref="DificuldadeDoGolpe"/>.
    /// </summary>
    public float AlcanceConfortavel { get; } = 0.85f;
    /// <summary>Distância do corpo em que o contato é perfeito; abaixo disso não conta como esticada.
    /// O ponto bom fica AO LADO do corpo, a essa distância, do lado da raquete (drive) ou do outro (revés): <see cref="PontoDeContato"/>.</summary>
    public const float DistanciaIdealDoContato = 0.6f;
    /// <summary>Bola a menos disso da linha do corpo, na lateral, é "bola no corpo": o braço não tem espaço e o golpe sai pior.</summary>
    public const float DistanciaDoCorpo = 0.3f;
    /// <summary>Abaixo disso (m) a bola é baixa: o golpe sai pior (<see cref="DificuldadeDoGolpe"/>), e a IA não espera por ela (<see cref="PodeBaterAgora(Bola, float, bool)"/>).</summary>
    public const float AlturaDaBolaBaixa = 0.3f;
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
    /// <summary>
    /// Verdadeiro só no passo do balanço mais perto de <see cref="MomentoIdealDoBalanco"/>: a raquete passando pelo ponto de
    /// contato. No modo manual o contato só acontece nesse passo; a bola que chega ao alcance depois dele encontra a raquete
    /// já passada.
    /// </summary>
    public bool NoInstanteDoContato { get; private set; }

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
    /// Distância (no chão) de (x, y) ao ponto ideal de contato do lado em que ele está — o mesmo que
    /// <see cref="PontoDeContato"/>(<see cref="LadoDoGolpePara(float)"/>), que é sempre o mais perto dos dois.
    /// </summary>
    public float DistanciaAoPontoIdeal(float x, float y) => DistanciaAoPontoIdeal(X, Y, x, y);

    /// <summary>Abaixo disso (m/s, no chão, relativa ao corpo) a bola quase cai na vertical: o atraso vai a zero junto.</summary>
    private const float RapidezPlenaDoAtraso = 1f;

    /// <summary>
    /// Quanto (m) a bola já passou do ponto da trajetória em que fica mais perto do ponto ideal de contato: a projeção de
    /// (bola − ponto ideal) na direção em que ela anda em relação ao corpo, no chão; 0 se ela ainda está chegando. É o
    /// "tarde" medido em espaço — a <see cref="DificuldadeDoGolpe"/> é simétrica (e plana até DistanciaIdealDoContato do
    /// corpo): não distingue a bola 40 cm antes do ponto da bola 40 cm depois, e a raquete que pega a bola passada é a que
    /// manda na rede. Bola caindo quase na vertical (menos de 1 m/s no chão) não tem antes nem depois: o atraso some junto.
    /// </summary>
    public float AtrasoDoContato(Bola bola)
    {
        var (px, py) = PontoDeContato(LadoDoGolpePara(bola));
        float vx = bola.Vx - Vx, vy = bola.Vy - Vy;
        float v = MathF.Sqrt(vx * vx + vy * vy);
        if (v < 1e-3f) return 0;
        float passou = ((bola.X - px) * vx + (bola.Y - py) * vy) / v;
        return MathF.Max(0, passou) * MathF.Min(1, v / RapidezPlenaDoAtraso);
    }

    /// <summary>A mesma distância, com o corpo em (corpoX, corpoY).</summary>
    private float DistanciaAoPontoIdeal(float corpoX, float corpoY, float x, float y)
    {
        float foraDaLinha = DistanciaIdealDoContato - MathF.Abs((x - corpoX) * Lado), frontal = (corpoY - y) * Lado;
        return MathF.Sqrt(foraDaLinha * foraDaLinha + frontal * frontal);
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

    /// <summary>Passo da previsão de <see cref="PodeBaterAgora(Bola)"/> sem passo explícito: o da partida (120 Hz).</summary>
    public const float PassoPadraoDaPrevisao = 1f / 120f;

    /// <summary>
    /// <see cref="PodeBaterAgora(Bola, float, bool)"/> com o passo de 120 Hz da partida, sem saber se a bola já quicou do
    /// nosso lado: supõe que sim (o próximo chão encerra o ponto). Esperar um quique que fosse o segundo entrega o ponto;
    /// bater antes de um primeiro quique só piora o golpe. Quem sabe (a partida, pelo árbitro) passa a informação.
    /// </summary>
    public bool PodeBaterAgora(Bola bola) => PodeBaterAgora(bola, PassoPadraoDaPrevisao, jaQuicou: true);

    /// <summary>
    /// Quando a IA e o modo Automático batem: com a bola ao alcance, no passo em que ela está mais perto do ponto ideal de
    /// contato (<see cref="DistanciaAoPontoIdeal"/>) — se no passo seguinte (dt) ela ainda vai estar mais perto, espera; não
    /// bate na borda do alcance. A exceção é a última chance: se no passo seguinte a bola sai do alcance (passa longe, sobe
    /// demais, ou toca o chão pela segunda vez), bate agora. O PRIMEIRO quique do nosso lado (jaQuicou falso: o árbitro
    /// ainda não viu a bola quicar depois do último golpe) não é saída: a bola volta a subir no mesmo lugar, e a espera
    /// segue pelo ponto ideal — tratar esse chão como saída fazia a IA bater a bola a 1–4 cm do chão (revisão de 25/09).
    /// A altura também conta, com o mesmo limite da <see cref="DificuldadeDoGolpe"/> (<see cref="AlturaDaBolaBaixa"/>): a
    /// bola rente ao chão subindo (acabou de quicar) espera subir; a que já quicou e desce rumo ao segundo chão é batida
    /// antes de ficar baixa, mesmo longe do ponto ideal — esperar por ele com a bola descendo era chegar lá a 1–4 cm do
    /// chão, na última chance. Coerente com o corpo: bola que vem em cima do jogador tem o ponto mais perto do ideal no
    /// próprio corpo, e esperar não a tira de lá — a <see cref="DificuldadeDoGolpe"/> cobra o preço. O que tira a bola do corpo
    /// é o posicionamento (na IA: <see cref="XParaBater(float, LadoDoGolpe)"/> com a leitura que afina enquanto a bola chega),
    /// não a espera. No modo manual o humano não passa por aqui: o contato sai em <see cref="NoInstanteDoContato"/>.
    /// atalho: a previsão é linear num passo (bola e corpo seguem a velocidade que têm) — a 120 Hz, até ~25 cm de bola. O
    /// chão ela vê pelo sinal de z (e decide pelo jaQuicou); a parede, não: a bola que bate no vidro dentro do passo aparece
    /// do lado de fora dele, quase sempre mais longe do ponto ideal, e é batida um passo antes do rebote. Se isso pesar, a
    /// saída é prever com Bola.Clonar().Avancar(dt) e olhar os eventos (Quique e Parede), ao custo de uma cópia da bola por
    /// jogador por passo.
    /// </summary>
    public bool PodeBaterAgora(Bola bola, float dt, bool jaQuicou)
    {
        if (!Alcanca(bola)) return false;
        float bx = bola.X + bola.Vx * dt, by = bola.Y + bola.Vy * dt, bz = bola.Z + bola.Vz * dt;
        float cx = X + Vx * dt, cy = Y + Vy * dt;
        bool encerraNoChao = bz < 0 && jaQuicou;   // o primeiro quique devolve a bola pra cima
        bool continuaAoAlcance = Util.Distancia(cx, cy, bx, by) <= Alcance && !encerraNoChao && bz <= AlturaMaxima;
        if (!continuaAoAlcance) return true;   // última chance
        if (bola.Z < AlturaDaBolaBaixa && bola.Vz > 0) return false;   // rente ao chão e subindo: espera ela subir
        if (jaQuicou && bola.Vz < 0 && bz < AlturaDaBolaBaixa) return true;   // descendo pro segundo chão: bate antes de ficar baixa
        return DistanciaAoPontoIdeal(cx, cy, bx, by) >= DistanciaAoPontoIdeal(bola.X, bola.Y);
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
        NoInstanteDoContato = false;
        if (Balanco > 0)
        {
            float antes = TempoNoBalanco;
            Balanco = MathF.Max(0, Balanco - dt);
            TempoNoBalanco += dt;
            // O passo mais perto do momento ideal (a 120 Hz, o 14º: 0,1167 s): o que cruza o momento menos meio passo.
            float meioPasso = dt / 2;
            NoInstanteDoContato = antes + meioPasso < MomentoIdealDoBalanco && TempoNoBalanco + meioPasso >= MomentoIdealDoBalanco;
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
        return Util.Limitar(esticado * 0.8f + (bola.Z < AlturaDaBolaBaixa ? 0.4f : 0) + (bola.Rapidez > 18 ? 0.4f : 0) + noCorpo + reves, 0, 1.6f);
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
/// <para>
/// Quem não vai na bola (e quem vai, enquanto não há plano) joga em <b>formação</b>: os dois andam como um bloco. Na
/// profundidade, a dupla está na rede (<see cref="ProfundidadeDaRede"/>) ou no fundo (<see cref="ProfundidadeDoFundo"/>):
/// sobe depois de um golpe que dá tempo (lob, bandeja, víbora, chiquita, smash) ou batido da frente, e recua quando a bola
/// do adversário vai passar por cima (o plano de quem vai nela fica atrás da linha de saque). Quem não bateu acompanha a
/// mudança depois de <see cref="PerfilDeIA.AtrasoDaFormacao"/> (ou, no desencontro, não acompanha); quem está mais à frente
/// espera o parceiro pra subirem juntos; e, enquanto o parceiro vai na bola, quem fica anda até a
/// <see cref="PerfilDeIA.FolgaDoBloco"/> da profundidade dele (recuam juntos, até <see cref="ProfundidadeMaximaDoBloco"/>).
/// Na largura, o centro da dupla segue o x da bola (<see cref="PerfilDeIA.AcompanhamentoDaBola"/>) e cada um fica a meia
/// <see cref="PerfilDeIA.SeparacaoDaDupla"/> do centro, do seu lado; se o parceiro foi buscar a bola bem do lado de cá, quem
/// fica troca de lado e cobre o que ficou vazio (o "cambio"). O parceiro IA de um humano segue a formação do humano — sobe
/// quando ele sobe, fica no fundo quando ele fica — e não a própria: o humano não segue a IA.
/// </para>
/// </summary>
public sealed class IA
{
    /// <summary>Profundidade (m da rede) da dupla na rede: a ~3 m, onde o voleio alcança a bola baixa e o lob ainda se busca.</summary>
    public const float ProfundidadeDaRede = 3.2f;
    /// <summary>Profundidade da dupla no fundo (a de sempre da IA; a linha de saque está a 6,95 m).</summary>
    public const float ProfundidadeDoFundo = 6.5f;
    /// <summary>O mais fundo que quem não vai na bola recua junto com o parceiro: ~1 m atrás da linha de saque, a posição de defesa do padel.</summary>
    public const float ProfundidadeMaximaDoBloco = 8.0f;
    /// <summary>Até onde o corpo vai na largura (o mesmo limite do <see cref="Jogador"/>).</summary>
    private const float LimiteLateralDoCorpo = 4.7f;
    /// <summary>O meio de cada metade da largura (<see cref="Jogador.XDaMetade"/>): onde fica o parceiro de um humano.</summary>
    private const float MeioDaMetade = 2.5f;
    /// <summary>
    /// Como a IA lê a formação do parceiro humano: a menos de ZonaDeRede da rede ele subiu (é a zona em que a IA conta
    /// adversário na rede); a mais de LinhaDaFrente ele voltou pro fundo (é de onde a IA deixa de contar um golpe como
    /// "batido da frente").
    /// </summary>
    private const float ZonaDeRede = 4.5f, LinhaDaFrente = 5.5f;

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
    /// <summary>A formação que a dupla quer (a de quem bateu por último); cada jogador a segue com atraso — ou desencontra.</summary>
    public Formacao Posicao { get; private set; } = Formacao.Fundo;
    private readonly Aleatorio _aleatorio;
    private Plano? _plano;
    private float _relogio;

    // Formação de cada jogador (pelo Indice): a que ele segue agora, a que vai seguir e quando.
    private readonly Formacao[] _formacao = [Formacao.Fundo, Formacao.Fundo];
    private readonly Formacao?[] _pendente = new Formacao?[2];
    private readonly float[] _mudaEm = new float[2];
    /// <summary>Depois de um golpe da dupla, quem está mais à frente espera o parceiro (uma vez) pra andarem juntos.</summary>
    private readonly bool[] _esperaOParceiro = new bool[2];
    /// <summary>Verdadeiro se o Indice 0 está na vaga da esquerda (no referencial da dupla) e o 1 na da direita: saque do outro lado ou cambio.</summary>
    private bool _ladosTrocados;
    /// <summary>O CriadoEm do plano (a trajetória da bola) em que a dupla fez o último cambio: um por trajetória.</summary>
    private float _cambioNaTrajetoria = float.NegativeInfinity;
    /// <summary>O x da bola como a dupla o acompanha (atrasado de <see cref="PerfilDeIA.AtrasoDaFormacao"/>).</summary>
    private float _xDaBolaLido;
    /// <summary>A profundidade do parceiro humano como a IA a acompanha (idem), e se ela o lê na rede ou no fundo.</summary>
    private float _profundidadeDoHumanoLida;
    private bool _humanoNaRede;

    public IA(int time, PerfilDeIA perfil, Aleatorio aleatorio)
    {
        Time = time; Perfil = perfil; _aleatorio = aleatorio;
    }

    /// <summary>Jogador que está indo buscar a bola, se houver (pra depuração e pra animação).</summary>
    public Jogador? Responsavel => _plano?.Responsavel;
    /// <summary>Onde (x, no mundo) quem vai na bola põe o corpo agora, pela leitura do momento — null sem plano (depuração e teste).</summary>
    public float? AlvoXDoResponsavel => _plano is Plano p ? XDoCorpo(p, p.Responsavel) : null;
    /// <summary>A dupla está com os lados trocados (o Indice 0 na vaga da esquerda): saque do outro lado ou cambio (depuração e teste).</summary>
    public bool LadosTrocados => _ladosTrocados;
    /// <summary>A formação que o jogador (deste time) segue agora; difere de <see cref="Posicao"/> enquanto ele não acompanhou a dupla.</summary>
    public Formacao FormacaoDe(Jogador j) => _formacao[j.Indice];

    public void AoSacar(Partida partida, int timeSacador)
    {
        Posicao = timeSacador == Time ? Formacao.Rede : Formacao.Fundo;   // quem saca sobe; quem recebe fica atrás
        // Ponto novo: os dois sabem onde a dupla fica, e cada um joga do lado em que começou (quem saca da metade do
        // parceiro fica nela; a partida põe o parceiro na outra).
        for (int i = 0; i < 2; i++) { _formacao[i] = Posicao; _pendente[i] = null; _esperaOParceiro[i] = false; }
        var meus = partida.JogadoresDoTime(Time);
        var (indice0, indice1) = meus[0].Indice == 0 ? (meus[0], meus[1]) : (meus[1], meus[0]);
        _ladosTrocados = indice0.LateralDe(indice1.X) > 0;   // o 1 está à direita do 0
        _xDaBolaLido = partida.Bola.X;
        foreach (var j in meus)
        {
            if (!j.Humano) continue;
            _profundidadeDoHumanoLida = MathF.Abs(j.Y);
            _humanoNaRede = _profundidadeDoHumanoLida < ZonaDeRede;
        }
        Planejar(partida, novaTrajetoria: true);
    }

    public void AoGolpear(Partida partida, Golpe golpe, Jogador jogador)
    {
        if (jogador.Time == Time)
        {
            bool naFrente = MathF.Abs(jogador.Y) < 5.5f;
            bool sobe = golpe.Tipo is TipoDeGolpe.Lob or TipoDeGolpe.Smash or TipoDeGolpe.Bandeja or TipoDeGolpe.Vibora
                or TipoDeGolpe.Chiquita or TipoDeGolpe.SmashPor3 or TipoDeGolpe.SmashPor4;   // a chiquita é o passaporte pra rede
            MudarFormacao(partida, sobe || naFrente ? Formacao.Rede : Formacao.Fundo, jogador);
            _esperaOParceiro[0] = _esperaOParceiro[1] = true;
        }
        else
        {
            _esperaOParceiro[0] = _esperaOParceiro[1] = false;
        }
        Planejar(partida, novaTrajetoria: true);
    }

    /// <summary>
    /// A dupla muda (ou confirma) a formação. Quem mandou (quem bateu, ou quem vai na bola que passa por cima) segue na hora;
    /// o outro segue depois de <see cref="PerfilDeIA.AtrasoDaFormacao"/> — ou, com <see cref="PerfilDeIA.ChanceDeDesencontro"/>,
    /// nem percebe e fica na que estava até a próxima. No time de um humano os dois seguem na hora, sem sorteio: a IA de lá
    /// segue a formação do humano (LugarNaFormacao), e esta só vale se ele virar IA no meio do ponto.
    /// </summary>
    private void MudarFormacao(Partida partida, Formacao nova, Jogador quem)
    {
        Posicao = nova;
        var meus = partida.JogadoresDoTime(Time);
        foreach (var j in meus)
        {
            int i = j.Indice;
            if (j == quem || _formacao[i] == nova || j.Humano || Parceiro(meus, j).Humano) { _formacao[i] = nova; _pendente[i] = null; continue; }
            if (_pendente[i] == nova) continue;
            if (Perfil.ChanceDeDesencontro > 0 && _aleatorio.Proximo() < Perfil.ChanceDeDesencontro) { _pendente[i] = null; continue; }
            _pendente[i] = nova;
            _mudaEm[i] = _relogio + Perfil.AtrasoDaFormacao;
        }
    }

    private static Jogador Parceiro(Jogador[] meus, Jogador j) => meus[0] == j ? meus[1] : meus[0];

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
        // A bola vai passar por cima da dupla na rede (quem vai nela vai buscá-la atrás da linha de saque): recuam juntos.
        if (Posicao == Formacao.Rede && MathF.Abs(_plano.AlvoY) >= Quadra.LinhaDeSaque) MudarFormacao(partida, Formacao.Fundo, escolhido);
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
        var meus = partida.JogadoresDoTime(Time);
        // O que a dupla acompanha anda atrasado de AtrasoDaFormacao (filtro de primeira ordem): o x da bola e a profundidade
        // do parceiro humano.
        float alfa = dt / (MathF.Max(Perfil.AtrasoDaFormacao, dt) + dt);
        _xDaBolaLido += (partida.Bola.X - _xDaBolaLido) * alfa;
        foreach (var j in meus) if (j.Humano) _profundidadeDoHumanoLida += (MathF.Abs(j.Y) - _profundidadeDoHumanoLida) * alfa;
        for (int i = 0; i < 2; i++)
        {
            if (_pendente[i] is Formacao pendente && _relogio >= _mudaEm[i]) { _formacao[i] = pendente; _pendente[i] = null; }
        }

        foreach (var j in meus)
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
                var (x, profundidade) = LugarNaFormacao(j, Parceiro(meus, j), plano);
                j.IrPara(x, j.Lado * profundidade, dt);
            }
        }
    }

    /// <summary>
    /// Onde o jogador fica na formação: x no mundo e profundidade (m da rede). Ver o resumo da classe; os números por nível
    /// estão em <see cref="PerfilDeIA"/>.
    /// </summary>
    private (float X, float Profundidade) LugarNaFormacao(Jogador j, Jogador parceiro, Plano? plano)
    {
        int i = j.Indice;
        float separacao = Perfil.SeparacaoDaDupla;
        float limiteDoCentro = MathF.Max(0, LimiteLateralDoCorpo - separacao / 2);
        float centro = Util.Limitar(Perfil.AcompanhamentoDaBola * _xDaBolaLido, -limiteDoCentro, limiteDoCentro);
        if (parceiro.Humano)
        {
            // O humano não segue a IA: a IA lê a formação DELE. Subiu = entrou na zona de rede; voltou = passou da linha da
            // frente; entre as duas fica o que estava (histerese). A IA vai pra profundidade dela de rede ou de fundo — não
            // copia cada passo do humano: a corrida dele atrás de uma bola não é formação. Na largura não há bloco (o humano
            // não anda com a bola junto dela): a IA fica no meio da sua metade, sem roubar a bola dele no meio, e troca de
            // metade se o humano tomou a dela.
            if (_profundidadeDoHumanoLida < ZonaDeRede) _humanoNaRede = true;
            else if (_profundidadeDoHumanoLida > LinhaDaFrente) _humanoNaRede = false;
            CobrirOOutroLado(j, parceiro.X, centro: 0);
            return (j.Lado * VagaDe(j) * MeioDaMetade, _humanoNaRede ? ProfundidadeDaRede : ProfundidadeDoFundo);
        }

        float minha = _formacao[i] == Formacao.Rede ? ProfundidadeDaRede : ProfundidadeDoFundo;
        float profundidade = minha;
        float folga = Perfil.FolgaDoBloco;
        if (plano is not null && plano.Responsavel == parceiro)
        {
            // O parceiro vai na bola: quem fica anda até a folga da profundidade dele (recuam e sobem juntos) e, se ele foi
            // buscá-la bem do lado de cá, troca de lado. Quem desencontrou (fora da formação da dupla) nem olha.
            if (_formacao[i] == Posicao && _relogio - plano.CriadoEm >= Perfil.AtrasoDaFormacao)
            {
                float dele = MathF.Abs(plano.AlvoY);
                profundidade = Util.Limitar(Util.Limitar(minha, dele - folga, dele + folga), ProfundidadeDaRede, ProfundidadeMaximaDoBloco);
                // Um cambio por trajetória: a releitura (quique, parede, o x afinando) não desfaz a troca no meio do caminho.
                if (plano.CriadoEm != _cambioNaTrajetoria && CobrirOOutroLado(j, XDoCorpo(plano, parceiro), centro))
                    _cambioNaTrajetoria = plano.CriadoEm;
            }
        }
        else if (_esperaOParceiro[i] && _formacao[i] == _formacao[parceiro.Indice])
        {
            // Indo pra formação depois de um golpe da dupla: quem está mais à frente (no sentido em que a dupla anda) espera
            // o outro chegar à folga, uma vez, e daí vão juntos. O outro é visto onde vai estar daqui a V/(2a) — o que quem
            // sai parado perde pra quem já corre (0,37 s no Difícil) —, senão quem esperou chega depois.
            float eu = MathF.Abs(j.Y);
            float ele = MathF.Abs(parceiro.Y) + parceiro.Vy * parceiro.Lado * j.Velocidade / (2 * j.Aceleracao);
            float frente = (eu - ele) * MathF.Sign(minha - eu);
            if (frente > folga) profundidade = eu;
            else _esperaOParceiro[i] = false;
        }
        return (centro + j.Lado * VagaDe(j) * separacao / 2, profundidade);
    }

    /// <summary>+1 se o jogador está na vaga da direita da dupla (no referencial dela), -1 se na da esquerda.</summary>
    private int VagaDe(Jogador j) => (j.Indice == 0 ? 1 : -1) * (_ladosTrocados ? -1 : 1);

    /// <summary>
    /// O "cambio": o parceiro (xDele, no mundo) invadiu a metade da quadra da vaga de j e passou do centro da dupla por mais
    /// de um quarto da separação — e j troca de vaga, cobrindo o lado que ficou vazio. O quarto da separação (~1 m) é a
    /// histerese: bola no meio não faz a dupla trocar de lado a cada golpe. A metade conta porque o centro anda com a bola:
    /// com ela junto à lateral, o parceiro que vai buscá-la no meio da metade DELE já passou do centro, e não é cambio.
    /// Devolve se trocou.
    /// </summary>
    private bool CobrirOOutroLado(Jogador j, float xDele, float centro)
    {
        int ladoDaMinhaVaga = j.Lado * VagaDe(j);   // no mundo
        if (xDele * ladoDaMinhaVaga <= 0 || (xDele - centro) * ladoDaMinhaVaga <= Perfil.SeparacaoDaDupla / 4) return false;
        _ladosTrocados = !_ladosTrocados;
        return true;
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
