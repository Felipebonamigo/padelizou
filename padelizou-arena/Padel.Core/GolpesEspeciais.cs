namespace Padel.Core;

/// <summary>
/// Os golpes que dependem de parede, de rede baixa ou de a bola sair da quadra: chiquita, contrapared, remate por 3 e
/// por 4. Nenhum solucionador sabe a altura das paredes (elas mudam de desenho: vidro escalonado, grade, portas): cada um
/// lança candidatos — pelo <see cref="Golpes.Calcular"/> ou com velocidade direta —, SIMULA a <see cref="Bola"/> e confere
/// a sequência de eventos (CruzouRede, Quique, Parede, Saiu). Uma solução só vale se sobreviver a pequenas variações
/// (passo de simulação maior, bola um pouco mais fraca ou mais forte, um pouco mais alta ou mais baixa): acertar por um
/// fio é sorte, não golpe. Sem solução daquela posição, devolvem null e quem chamou escolhe outro golpe.
/// O Golpe devolvido carrega a velocidade simulada em <see cref="Golpe.Lancamento"/> e, em AlvoX/AlvoY/TempoDeVoo,
/// onde e quando a bola quica do outro lado.
/// <para>
/// Custo: roda dentro de um passo da partida (no host online, inclusive), então nenhum solucionador varre a grade inteira
/// de candidatos — era força bruta, até ~1.900 simulações e 10 ms por chamada. A chiquita procura o lançamento por
/// secantes na inclinação e na rapidez (poucas simulações por alvo), com orçamento de sub-passos por chamada (<see
/// cref="Orcamento"/>). A contrapared percorre a MESMA grade de antes e devolve o mesmo golpe, usando a forma dela
/// (em cada linha: as que não chegam, as que valem, as que passam) pra achá-lo por busca binária. Os remates percorrem a
/// MESMA grade na MESMA ordem, mas só simulam a candidata que cortes exatos não descartam (<see cref="Remate"/>). O
/// trabalho fica contado em <see cref="SubPassosSimulados"/> — toda bola simulada na chamada —, e há teste com teto.
/// </para>
/// </summary>
public static class GolpesEspeciais
{
    private const float Passo = 1f / 120f;
    private const float PassoGrosso = 1f / 60f;

    /// <summary>A chiquita cai a até isso da rede (nos pés de quem está lá).</summary>
    public const float AlcanceDaChiquita = 3.5f;
    /// <summary>A chiquita passa a no máximo isso acima da rede.</summary>
    public const float FolgaDaChiquitaSobreARede = 0.5f;
    /// <summary>
    /// Com a bola a menos disto da rede não há chiquita: a raquete tocaria a rede (falta na regra do padel) e o que a busca
    /// achava ali era degenerado — balão de 2,9 s ou "chiquita" de 87 m/s. O humano que pede chiquita ali recebe o lob.
    /// Decisão tomada depois de três correções seguidas do solucionador brigando com essa faixa (CLAUDE.md, regra 6).
    /// </summary>
    public const float DistanciaMinimaDaChiquitaDaRede = 0.10f;
    /// <summary>Remate de verdade: até ~145 km/h. Acima disso é canhão, não padel.</summary>
    public const float RapidezMaximaDoRemate = 40f;
    /// <summary>A contrapared sai de posição ruim (bola atrás, junto ao vidro): não dá pra bater com tudo.</summary>
    public const float RapidezMaximaDaContrapared = 22f;

    /// <summary>A velocidade com que um golpe sai: a do solucionador, se veio; senão, a do Golpes.Calcular pelo alvo. É o que a Partida usa.</summary>
    public static Velocidade VelocidadeDe(Golpe golpe, float x0, float y0, float z0) =>
        golpe.Lancamento ?? Golpes.Calcular(x0, y0, z0, golpe.AlvoX, golpe.AlvoY, golpe.TempoDeVoo, ignorarRede: golpe.IgnorarRede, efeito: golpe.Efeito);

    // ───────────── Diagnóstico ─────────────

    /// <summary>
    /// Diagnóstico de desempenho (testes e a ferramenta Desempenho): quantos sub-passos de integração da bola (de até
    /// <see cref="Bola.PassoMaximo"/>) os solucionadores simularam NESTA thread até agora. É a medida do trabalho que não
    /// depende da máquina: leia antes e depois de uma chamada. Sai do <see cref="Bola.SubPassosNestaThread"/> — o quanto
    /// ele andou durante cada chamada —, então conta toda bola simulada nela, inclusive a de um Golpes.Calcular: não há
    /// busca que escape do teto. Só conta; não muda nenhum resultado.
    /// </summary>
    public static long SubPassosSimulados => _subPassos;
    [ThreadStatic] private static long _subPassos;

    /// <summary>
    /// Modo de coleta (diagnóstico): com uma lista aqui, cada chamada a um solucionador NESTA thread é anotada nela, com os
    /// argumentos exatos, o resultado e o trabalho — pra cronometrar a chamada isolada depois, fora da partida. Nula (o
    /// padrão) desliga. Não muda nenhum resultado.
    /// </summary>
    public static List<ChamadaDeSolucionador>? Coleta { get => _coleta; set => _coleta = value; }
    [ThreadStatic] private static List<ChamadaDeSolucionador>? _coleta;

    /// <summary>Fecha a conta de uma chamada: o trabalho é o que a bola andou nesta thread desde subPassosAntes (lido na entrada).</summary>
    private static Golpe? Anotar(Solucionador qual, float x0, float y0, float z0, int ladoDoAlvo, float argumento, long subPassosAntes, Golpe? golpe)
    {
        long trabalho = Bola.SubPassosNestaThread - subPassosAntes;
        _subPassos += trabalho;
        _coleta?.Add(new ChamadaDeSolucionador(qual, x0, y0, z0, ladoDoAlvo, argumento, golpe, trabalho));
        return golpe;
    }

    // ───────────── Simulação e robustez ─────────────

    private readonly record struct Marca(EventoDaBola Evento, float T);

    /// <summary>
    /// Simula até a bola parar, sair, dar maximoDeEventos eventos ou passar o tempo. Devolve os eventos com o instante de
    /// cada um. Com prefixo, para assim que os eventos até ali já não podem dar um golpe que vale: o "vale" dá o mesmo
    /// resultado, com menos simulação (o evento que reprovou fica na lista). Com orçamento, cobra cada passo dele e para
    /// quando ele acaba — a simulação cortada não vale, e quem chamou olha <see cref="Orcamento.Esgotado"/>.
    /// </summary>
    private static List<Marca> Simular(float x0, float y0, float z0, Velocidade v, float segundos, int maximoDeEventos, float passo = Passo,
        Func<List<Marca>, bool>? prefixo = null, Orcamento? orcamento = null)
    {
        var bola = new Bola();
        bola.Posicionar(x0, y0, z0);
        bola.Lancar(v);
        var eventos = new List<EventoDaBola>(4);
        var marcas = new List<Marca>(maximoDeEventos + 2);
        int subPassosPorPasso = (int)MathF.Ceiling(passo / Bola.PassoMaximo - 1e-3f);
        for (float t = 0; t < segundos && bola.EmJogo && !bola.Parada && marcas.Count < maximoDeEventos;)
        {
            if (orcamento is not null && !orcamento.Cobrar(subPassosPorPasso)) break;
            eventos.Clear();
            bola.Avancar(passo, eventos);
            t += passo;
            if (eventos.Count == 0) continue;
            foreach (var e in eventos) marcas.Add(new Marca(e, t));
            if (prefixo is not null && !prefixo(marcas)) break;
        }
        return marcas;
    }

    private static Velocidade Escalar(Velocidade v, float f) => v with { Vx = v.Vx * f, Vy = v.Vy * f, Vz = v.Vz * f };
    private static float Rapidez(Velocidade v) => MathF.Sqrt(v.Vx * v.Vx + v.Vy * v.Vy + v.Vz * v.Vz);

    /// <summary>A margem de força: golpe de potência aguenta 3 % e 0,4 m/s de tremida; golpe de toque (chiquita), 2 % e 0,2 m/s.</summary>
    private readonly record struct Margem(float Rapidez, float Vz)
    {
        public static readonly Margem DePotencia = new(0.03f, 0.4f);
        public static readonly Margem DeToque = new(0.02f, 0.2f);
    }

    /// <summary>
    /// A solução vale se continua valendo saindo um pouco mais alta e mais baixa, com a bola um pouco mais forte e mais
    /// fraca e com o passo de simulação do jogo a 60 Hz — a margem de um golpe bem dado, sem a qual qualquer tremida muda o
    /// resultado. Devolve null se aguenta todas; senão, os eventos da primeira variação que não vale (a chiquita olha se
    /// passou do limite por cima ou por baixo). A ordem só muda o custo: as que mais reprovam primeiro; o passo grosso,
    /// quase a mesma trajetória, por último.
    /// </summary>
    private static List<Marca>? PrimeiraVariacaoQueNaoVale(float x0, float y0, float z0, Velocidade v, float segundos, int maximoDeEventos,
        Margem margem, Func<List<Marca>, bool> vale, Func<List<Marca>, bool>? prefixo, Orcamento? orcamento = null)
    {
        var m = Simular(x0, y0, z0, v with { Vz = v.Vz + margem.Vz }, segundos, maximoDeEventos, Passo, prefixo, orcamento);
        if (!vale(m)) return m;
        m = Simular(x0, y0, z0, v with { Vz = v.Vz - margem.Vz }, segundos, maximoDeEventos, Passo, prefixo, orcamento);
        if (!vale(m)) return m;
        m = Simular(x0, y0, z0, Escalar(v, 1 + margem.Rapidez), segundos, maximoDeEventos, Passo, prefixo, orcamento);
        if (!vale(m)) return m;
        m = Simular(x0, y0, z0, Escalar(v, 1 - margem.Rapidez), segundos, maximoDeEventos, Passo, prefixo, orcamento);
        if (!vale(m)) return m;
        m = Simular(x0, y0, z0, v, segundos, maximoDeEventos, PassoGrosso, prefixo, orcamento);
        return vale(m) ? null : m;
    }

    private static bool Robusta(float x0, float y0, float z0, Velocidade v, float segundos, int maximoDeEventos, Margem margem,
        Func<List<Marca>, bool> vale, Func<List<Marca>, bool>? prefixo) =>
        PrimeiraVariacaoQueNaoVale(x0, y0, z0, v, segundos, maximoDeEventos, margem, vale, prefixo) is null;

    /// <summary>
    /// Orçamento de trabalho, em sub-passos de bola: o <see cref="Simular"/> cobra cada passo antes de dá-lo e para quando
    /// não cabe mais. Quem recebe um não passa dele, por construção — o teto não depende de amostra. Com pai, cobra dos dois
    /// (a parte de uma família de alvo dentro do orçamento da chamada).
    /// </summary>
    private sealed class Orcamento(int subPassos, Orcamento? pai = null)
    {
        private int _restante = subPassos;
        public bool Esgotado { get; private set; }
        /// <summary>Tira n do que resta; false (e esgotado dali em diante) se não cabe.</summary>
        public bool Cobrar(int n)
        {
            if (Esgotado || n > _restante || (pai is not null && !pai.Cobrar(n))) { Esgotado = true; return false; }
            _restante -= n;
            return true;
        }
    }

    // ───────────── Chiquita ─────────────

    /// <summary>
    /// Chiquita: bola baixa e lenta nos pés de quem está na rede — cai a até <see cref="AlcanceDaChiquita"/> da rede do
    /// lado do alvo, passa a no máximo <see cref="FolgaDaChiquitaSobreARede"/> acima da rede, com topspin (que a derruba
    /// depois da rede). Entre as que servem, a mais lenta. ladoDoAlvo: o lado da quadra onde ela deve quicar (+1/-1).
    /// </summary>
    public static Golpe? Chiquita(float x0, float y0, float z0, int ladoDoAlvo, float alvoX) =>
        Anotar(Solucionador.Chiquita, x0, y0, z0, ladoDoAlvo, alvoX, Bola.SubPassosNestaThread, ResolverChiquita(x0, y0, z0, ladoDoAlvo, alvoX));

    private static readonly float[] DistanciasDaChiquita = [2.0f, 2.5f, 3.0f];
    /// <summary>
    /// O Golpes.Calcular parava a até 12 cm do alvo, quase sempre do lado de cá: a chiquita de antes caía ~0,1 m antes da
    /// distância pedida. Mirar ali mantém a mais lenta (cair mais perto pede menos velocidade).
    /// </summary>
    private const float AquemDoAlvoDaChiquita = 0.1f;
    private static readonly float[] TopspinsDaChiquita = [800f, 1500f];
    /// <summary>
    /// A inclinação mais alta que se tenta (vz/vh, ~89°). Colada na rede e abaixo dela, a única chiquita é o balão quase
    /// vertical — a busca antiga achava com vz de 14 m/s e vh de 0,4 (inclinação 36), andando pro alvo no empurrão do
    /// Magnus do topspin na subida. O limite de verdade é o tempo: ele tem de quicar dentro dos 3 s da simulação.
    /// </summary>
    private const float InclinacaoMaximaDaChiquita = 60f;
    /// <summary>
    /// A folga na rede além do que as variações da robustez mexem na altura ali: a conta delas é linear, erra pouco —
    /// colada na rede, onde elas mexem décimos de milímetro, a chiquita de antes passava a 0,4 mm do limite.
    /// </summary>
    private const float FolgaNaRedeDaChiquita = 0.001f, FolgaRelativaNaRedeDaChiquita = 0.1f;
    private const int IteracoesDaChiquita = 24;
    /// <summary>
    /// O tempo de voo do balão (<see cref="ChiquitaDaFamilia"/>): o mais longo com folga pras variações da robustez
    /// (+0,2 m/s em vz e +2 % de rapidez alongam o voo ~0,05 s) dentro dos 3 s da simulação.
    /// </summary>
    private const float TempoDoBalao = 2.8f;
    /// <summary>
    /// Orçamento de uma chamada, em sub-passos de bola (<see cref="Orcamento"/>), e de cada família dentro dela (uma que não
    /// acha não deixa as outras sem): nenhuma chiquita passa de 20 mil, por construção — 0,73–0,76 ms isolada, com a
    /// máquina carregada (load 7–8). Quem gasta tudo é a posição SEM chiquita, colada na rede e rente ao chão: sem
    /// orçamento, a busca levava até 45 mil pra concluir isso. Onde a antiga tinha chiquita, a nova gastou no máximo
    /// 17.642 (208 mil posições dentro da quadra, ver <see cref="ResolverChiquita"/>). atalho: se uma chiquita de verdade
    /// precisar de mais, sai null e quem chamou joga outro golpe; o teste de equivalência pega. Saída: subir o orçamento
    /// (até ~25 mil cabe em 1 ms) ou descartar antes, sem simular, a família que não tem balão possível.
    /// </summary>
    private const int OrcamentoDaChiquita = 20_000, OrcamentoPorFamiliaDaChiquita = 10_000;

    /// <summary>
    /// Famílias de alvo: quicar a 2,0, 2,5 ou 3,0 m da rede na direção de alvoX, com 800 ou 1.500 rpm de topspin. Em cada
    /// uma, <see cref="ChiquitaDaFamilia"/> acha direto a mais lenta que serve: o arco de rapidez mínima até o alvo, preso
    /// entre a rede e a folga sobre ela.
    /// </summary>
    private static Golpe? ResolverChiquita(float x0, float y0, float z0, int ladoDoAlvo, float alvoX)
    {
        alvoX = Util.Limitar(alvoX, -Quadra.MeiaLargura + 0.5f, Quadra.MeiaLargura - 0.5f);
        if (Quadra.LadoDe(y0) == ladoDoAlvo) return null;   // já do lado do alvo: não há rede pra cruzar
        if (MathF.Abs(y0) < DistanciaMinimaDaChiquitaDaRede) return null;   // colada na rede: raquete na rede (ver a constante)
        var orcamento = new Orcamento(OrcamentoDaChiquita);
        // atalho: para na primeira distância que tem chiquita, em vez de comparar as três — com o arco livre, cair mais
        // perto pede menos velocidade. Só segue pras mais longas se a melhor até ali ficou presa no teto da rede com a rede
        // no primeiro quinto do caminho (colada nela e acima: um alvo mais longe cruza a rede mais cedo no caminho, e o
        // arco pode subir mais). E o orçamento: acabou, devolve o que achou (ou nada). Contra a busca antiga (as 42
        // combinações, a mais lenta), em 208 mil posições DENTRO da quadra — a grade da revisão (38 mil), uma deslocada
        // (52,8 mil), colada na rede (10.368, |y| de 5 mm a 0,7 m), a bola que acabou de cruzar (7.200, |y| de 2 mm a
        // 12 cm) e 100 mil aleatórias (1/3 a menos de 30 cm da rede) —: nenhuma posição com chiquita antes ficou sem,
        // nenhuma inválida, e a rapidez nova/antiga teve mediana 0,978–0,985 e máxima 1,048. Fora das paredes (|x| > 5 ou
        // |y| > 10) a antiga "achava" chiquita atravessando a parede no primeiro sub-passo; a Partida não chama ali (a bola
        // em jogo nunca passa delas). Se a mais lenta passar a importar mais, a saída é comparar sempre as três distâncias
        // (até 3x o custo, e o orçamento junto).
        Golpe? melhor = null;
        float menorRapidez = float.PositiveInfinity;
        bool melhorPresaNoTeto = false;
        foreach (float distancia in DistanciasDaChiquita)
        {
            foreach (float topspin in TopspinsDaChiquita)
            {
                var daFamilia = new Orcamento(OrcamentoPorFamiliaDaChiquita, orcamento);
                if (ChiquitaDaFamilia(x0, y0, z0, ladoDoAlvo, alvoX, distancia, topspin, daFamilia, out bool presaNoTeto) is not Golpe g
                    || g.Lancamento is not Velocidade v || !(Rapidez(v) < menorRapidez)) continue;
                (melhor, menorRapidez, melhorPresaNoTeto) = (g, Rapidez(v), presaNoTeto);
            }
            if ((melhor is not null && !melhorPresaNoTeto) || orcamento.Esgotado) return melhor;
        }
        return melhor;
    }

    /// <summary>
    /// A chiquita que quica a <c>distancia</c> da rede na direção de alvoX: a do arco mais lento até lá cuja altura na rede
    /// cabe entre a rede e o limite, com a folga do que as variações da robustez mexem na bola ali.
    /// <para>
    /// A família é parametrizada pela inclinação do lançamento, p = vz/vh — exata, sem medida no meio. Pra cada p, a
    /// rapidez que faz a bola quicar no alvo sai de uma secante com intervalo (a distância do quique cresce com a rapidez);
    /// e p anda por uma secante na altura medida na rede, que cresce com p (no vácuo, pra quicar no alcance L com a rede a
    /// uma fração f do caminho, z na rede = z0 (1 − f²) + p·L·f (1 − f): reta). O chute é o p do arco mais lento no vácuo
    /// (tg θ = (√(L² + z0²) − z0)/L), preso na janela da rede. Colada na rede, a altura ali é ~z0 + p·(caminho até a
    /// rede), quase a mesma pra qualquer rapidez: mirar uma altura na rede com (vh, vz) — a busca da primeira correção —
    /// pedia metros por segundo pra mexer milímetros, e saía a 87 m/s; em p, é só um p maior (o balão) ou nenhum.
    /// Depois, a robustez: variação que passou por cima desce p; por baixo, sobe. E quando o que prende é o tempo (o
    /// balão não quica dentro dos 3 s), <c>Balao</c> resolve direto o tempo de voo.
    /// </para>
    /// </summary>
    private static Golpe? ChiquitaDaFamilia(float x0, float y0, float z0, int ladoDoAlvo, float alvoX, float distancia, float topspin, Orcamento orcamento,
        out bool presaNoTeto)
    {
        presaNoTeto = false;
        const float Limite = Quadra.AlturaDaRede + FolgaDaChiquitaSobreARede;
        const float g = Bola.G;
        bool Vale(List<Marca> m) =>
            m.Count >= 2
            && m[0].Evento.Tipo == TipoDeEventoDaBola.CruzouRede && m[0].Evento.Para == ladoDoAlvo
            && m[0].Evento.Z <= Limite
            && m[1].Evento.Tipo == TipoDeEventoDaBola.Quique && m[1].Evento.Lado == ladoDoAlvo
            && MathF.Abs(m[1].Evento.Y) <= AlcanceDaChiquita;
        bool Prefixo(List<Marca> m) =>
            m[0].Evento.Tipo == TipoDeEventoDaBola.CruzouRede && m[0].Evento.Para == ladoDoAlvo && m[0].Evento.Z <= Limite
            && (m.Count < 2 || (m[1].Evento.Tipo == TipoDeEventoDaBola.Quique && m[1].Evento.Lado == ladoDoAlvo && MathF.Abs(m[1].Evento.Y) <= AlcanceDaChiquita));
        // O quanto as variações da robustez mexem na altura na rede, tRede segundos depois do toque, com a bola a essa
        // rapidez: ±2 % de rapidez, ±0,02·g·t²; ±0,2 m/s em vz, ±0,2·t no vácuo — mas o arrasto (−K·v·v⃗) amortece a
        // diferença de vz a uma taxa de pelo menos K·v, então no máximo ±0,2·(1 − e^(−K·v·t))/(K·v).
        static float Variacao(float tRede, float rapidez)
        {
            float kv = Bola.KArrasto * rapidez;
            float porVz = kv * tRede > 1e-4f ? Margem.DeToque.Vz * (1 - MathF.Exp(-kv * tRede)) / kv : Margem.DeToque.Vz * tRede;
            return (1 + FolgaRelativaNaRedeDaChiquita) * MathF.Max(porVz, Margem.DeToque.Rapidez * g * tRede * tRede) + FolgaNaRedeDaChiquita;
        }

        // Direção e efeito como o Golpes.Calcular: topspin de quem vai em d̂ é o eixo (up × d̂) = (−dy, dx, 0). Sem efeito
        // de lado, a bola fica no plano vertical de d̂ (o Magnus do topspin é perpendicular à velocidade, nesse plano).
        float dx = alvoX - x0, dy = ladoDoAlvo * distancia - y0;
        float distanciaAoAlvo = MathF.Sqrt(dx * dx + dy * dy);
        float dirX = dx / distanciaAoAlvo, dirY = dy / distanciaAoAlvo;
        float w = topspin * (2 * MathF.PI / 60f);
        var efeito = new Efeito(topspin, 0);
        float alcance = distanciaAoAlvo - AquemDoAlvoDaChiquita;
        float ateARede = distanciaAoAlvo * MathF.Abs(y0) / MathF.Abs(dy);
        float fracao = ateARede / alcance;
        if (!(fracao > 0 && fracao < 1)) return null;   // em cima da rede (y0 = 0) a Bola não vê a bola cruzar
        // Gravidade e Magnus do topspin só viram a velocidade pra baixo (o arrasto é ao longo dela): o caminho fica acima
        // da corda até o quique. Se ela já passa acima do limite na rede, não há chiquita pra esse alvo.
        if (z0 * (1 - fracao) > Limite + 0.02f) return null;

        // O modelo no vácuo, em p: a rapidez horizontal pra quicar no alcance e a altura na rede.
        float VhNoVacuo(float p) => alcance * MathF.Sqrt(g / (2 * (z0 + p * alcance)));
        float ZNaRedeNoVacuo(float p) => z0 * (1 - fracao * fracao) + p * ateARede * (1 - fracao);
        // O intervalo aberto de p onde pode haver chiquita. Embaixo: a rapidez máxima do remate (no vácuo) e — exato — a
        // reta do lançamento: o caminho fica abaixo dela, então se ela passa abaixo da rede, a bola bate. Em cima, ~89°.
        float pBaixo = MathF.Max((g * alcance * alcance / (2 * RapidezMaximaDoRemate * RapidezMaximaDoRemate) - z0) / alcance,
            (Quadra.AlturaDaRede - z0) / ateARede);
        float pAlto = InclinacaoMaximaDaChiquita;
        if (!(pBaixo < pAlto)) return null;
        float PrimeiroP(Func<float, bool> verdadeiro)   // num predicado falso… verdadeiro em p: onde vira (+∞: nunca)
        {
            float lo = pBaixo, hi = pAlto;
            if (verdadeiro(lo)) return lo;
            if (!verdadeiro(hi)) return float.PositiveInfinity;
            for (int i = 0; i < 30; i++) { float meio = 0.5f * (lo + hi); if (verdadeiro(meio)) hi = meio; else lo = meio; }
            return hi;
        }
        float pMaisLento = (MathF.Sqrt(alcance * alcance + z0 * z0) - z0) / alcance;
        float VariacaoNoVacuo(float p) => Variacao(ateARede / VhNoVacuo(p), VhNoVacuo(p) * MathF.Sqrt(1 + p * p));
        float piso = PrimeiroP(p => ZNaRedeNoVacuo(p) - VariacaoNoVacuo(p) >= Quadra.AlturaDaRede);
        float teto = PrimeiroP(p => ZNaRedeNoVacuo(p) + VariacaoNoVacuo(p) > Limite);
        float inclinacao = piso <= teto ? Util.Limitar(pMaisLento, piso, teto) : 0.5f * (MathF.Min(piso, pAlto) + MathF.Min(teto, pAlto));
        if (!(inclinacao > pBaixo && inclinacao < pAlto)) inclinacao = 0.5f * (pBaixo + pAlto);

        // Pra p fixo: a rapidez horizontal entre uma curta (bateu na rede, quicou antes do alcance) e uma longa (quicou
        // depois, ou ainda no ar aos 3 s). O arrasto encurta a bola como no Golpes.Calcular.
        float vh = VhNoVacuo(inclinacao) * (1 + Bola.KArrasto * alcance * 0.5f);
        float vhCurta = 0, erroCurta = float.NaN, vhLonga = float.PositiveInfinity, erroLonga = float.NaN;
        float zNaRedeDaCurta = float.NaN;   // a altura na rede da curta (NaN: quicou antes dela)
        bool longaNoAr = false;
        float pAnterior = float.NaN, zAnterior = float.NaN;   // o último (p, altura na rede) medido, pra secante
        int mesmoSentido = 0;   // quantas mudanças de p seguidas no mesmo sentido

        // Muda p. subir: esse p é baixo demais (vira o piso do intervalo); senão, alto demais (vira o teto). Com a altura
        // medida na rede e a querida, a secante (ou a derivada no vácuo) diz pra onde; fora do intervalo, o meio dele.
        // false se o intervalo fechou: essa família não tem chiquita.
        bool MudarInclinacao(bool subir, float zNaRede, float zQuerido)
        {
            // Colada na rede, a margem das variações muda um pouco com a rapidez que o alcance pede: mirando a borda da
            // janela, p andaria aos pouquinhos, refazendo a rapidez a cada passo. Cada mudança seguida no mesmo sentido
            // mira mais pra dentro: 0, 2, 8, 26 mm… até 10 cm.
            mesmoSentido = subir == (mesmoSentido > 0) ? mesmoSentido + (subir ? 1 : -1) : (subir ? 1 : -1);
            zQuerido += (subir ? 1 : -1) * MathF.Min(0.1f, FolgaNaRedeDaChiquita * (MathF.Pow(3, MathF.Abs(mesmoSentido) - 1) - 1));
            if (subir) pBaixo = MathF.Max(pBaixo, inclinacao); else pAlto = MathF.Min(pAlto, inclinacao);
            if (!(pAlto - pBaixo > 1e-3f * (1 + MathF.Abs(inclinacao)))) return false;
            float nova = float.NaN;
            if (float.IsFinite(zNaRede) && float.IsFinite(zQuerido))
            {
                float derivada = ateARede * (1 - fracao);   // no vácuo
                if (float.IsFinite(pAnterior) && MathF.Abs(inclinacao - pAnterior) > 1e-4f)
                {
                    float secante = (zNaRede - zAnterior) / (inclinacao - pAnterior);
                    if (secante > 0.2f * derivada) derivada = secante;
                }
                pAnterior = inclinacao; zAnterior = zNaRede;
                nova = inclinacao + (zQuerido - zNaRede) / derivada;
            }
            if (!(nova > pBaixo && nova < pAlto)) nova = 0.5f * (pBaixo + pAlto);
            vh *= VhNoVacuo(nova) / VhNoVacuo(inclinacao);
            inclinacao = nova;
            vhCurta = 0; erroCurta = float.NaN; vhLonga = float.PositiveInfinity; erroLonga = float.NaN; zNaRedeDaCurta = float.NaN; longaNoAr = false;
            return true;
        }

        // O candidato que cruzou e quica no alvo: a altura na rede cabe na janela da robustez, e ele aguenta as variações?
        // Devolve o golpe; senão, em zQuerido, a altura na rede que ele devia ter (NaN: o que falhou não é a altura na rede).
        Golpe? Avaliar(Velocidade v, List<Marca> m, float vhDoLancamento, out float zQuerido)
        {
            float zNaRede = m[0].Evento.Z;
            float variacao = Variacao(Util.Limitar(ateARede / vhDoLancamento, m[0].T - Passo, m[0].T), Rapidez(v));
            zQuerido = float.NaN;
            if (zNaRede > Limite - variacao) { zQuerido = Limite - variacao - FolgaNaRedeDaChiquita; return null; }
            if (zNaRede < Quadra.AlturaDaRede + variacao) { zQuerido = Quadra.AlturaDaRede + variacao + FolgaNaRedeDaChiquita; return null; }
            var falha = PrimeiraVariacaoQueNaoVale(x0, y0, z0, v, 3, 2, Margem.DeToque, Vale, Prefixo, orcamento);
            if (orcamento.Esgotado) return null;
            if (falha is null) return new Golpe(m[1].Evento.X, m[1].Evento.Y, m[1].T, TipoDeGolpe.Chiquita, Efeito: efeito, Lancamento: v);
            // Passou por cima do limite numa variação: mais baixo. Bateu na rede ou quicou antes dela: mais alto. Outra coisa
            // (quicou longe demais, ainda no ar aos 3 s): não é a altura na rede.
            var primeiro = falha[0].Evento;
            if (primeiro.Tipo == TipoDeEventoDaBola.CruzouRede && primeiro.Z > Limite) zQuerido = zNaRede - (primeiro.Z - Limite) - FolgaNaRedeDaChiquita;
            else if (primeiro.Tipo == TipoDeEventoDaBola.Rede) zQuerido = zNaRede + MathF.Max(variacao, 5 * FolgaNaRedeDaChiquita);
            else if (primeiro.Tipo == TipoDeEventoDaBola.Quique) zQuerido = zNaRede + 0.05f;
            return null;
        }

        // O balão: colada na rede e abaixo dela, a rede prende p lá em cima e o que manda é o tempo — quicar no alvo pede
        // mais de 3 s de voo com esse p. Newton direto em (vh, vz) pra quicar no alvo em TempoDoBalao (no vácuo o tempo de
        // voo só depende de vz, ∂T/∂vz = T/(gT − vz), e o alcance é vh·T): todo balão até o alvo nesse tempo tem ~o mesmo
        // vz, o mais lento que cabe. O p que sair tem de caber na janela da rede; uma tentativa só.
        Golpe? Balao(float vh, float vz)
        {
            for (int iteracao = 0; iteracao < 6; iteracao++)
            {
                var v = new Velocidade(vh * dirX, vh * dirY, vz, 0, -dirY * w, dirX * w, 0);
                var m = Simular(x0, y0, z0, v, 3, 2, Passo, null, orcamento);
                if (orcamento.Esgotado || m.Count == 0 || m[0].Evento.Tipo != TipoDeEventoDaBola.CruzouRede || m[0].Evento.Para != ladoDoAlvo) return null;
                if (m.Count < 2) { vz -= 0.5f; continue; }   // ainda no ar aos 3 s
                if (m[1].Evento.Tipo != TipoDeEventoDaBola.Quique) return null;
                float tempo = m[1].T - Passo / 2;   // quicou em algum ponto do último passo
                float erroNoQuique = (m[1].Evento.X - x0) * dirX + (m[1].Evento.Y - y0) * dirY - alcance, erroNoTempo = tempo - TempoDoBalao;
                if (MathF.Abs(erroNoQuique) < 0.05f && MathF.Abs(erroNoTempo) < 0.06f) return Avaliar(v, m, vh, out _);
                float tempoPorVz = tempo / MathF.Max(0.5f, g * tempo - vz);
                float dvz = -erroNoTempo / tempoPorVz;
                vh += (-erroNoQuique - vh * tempoPorVz * dvz) / tempo;
                vz += dvz;
                if (!(vh > 0.02f) || !float.IsFinite(vh) || !float.IsFinite(vz)) return null;
            }
            return null;
        }

        for (int iteracao = 0; iteracao < IteracoesDaChiquita; iteracao++)
        {
            var v = new Velocidade(vh * dirX, vh * dirY, inclinacao * vh, 0, -dirY * w, dirX * w, 0);
            var m = Simular(x0, y0, z0, v, 3, 2, Passo, null, orcamento);
            if (orcamento.Esgotado) return null;

            // Onde ela caiu: erro (quique − alcance) quando cruzou e quicou; senão, só o lado — curta ou longa.
            float erro = float.NaN, zNaRede = float.NaN;
            bool curta, noAr = false;
            if (m.Count == 0) { curta = false; noAr = true; }
            else if (m[0].Evento.Tipo == TipoDeEventoDaBola.CruzouRede && m[0].Evento.Para == ladoDoAlvo)
            {
                zNaRede = m[0].Evento.Z;
                if (m.Count < 2) { curta = false; noAr = true; }
                else if (m[1].Evento.Tipo == TipoDeEventoDaBola.Quique)
                {
                    erro = (m[1].Evento.X - x0) * dirX + (m[1].Evento.Y - y0) * dirY - alcance;
                    curta = erro < 0;
                }
                else curta = false;   // parede ou saída antes de quicar
            }
            // Bateu na rede: passou abaixo dela (a Z do evento é a do fim do sub-passo, não a da rede — subindo a 16 m/s, 7 cm acima).
            else if (m[0].Evento.Tipo == TipoDeEventoDaBola.Rede) { curta = true; zNaRede = Quadra.AlturaDaRede - FolgaNaRedeDaChiquita; }
            else if (m[0].Evento.Tipo == TipoDeEventoDaBola.Quique) curta = true;
            else return null;   // parede ou saída antes da rede

            if (MathF.Abs(erro) < 0.05f)
            {
                if (Avaliar(v, m, vh, out float zQuerido) is Golpe golpe)
                {
                    // Colada na rede (ela no primeiro quinto do caminho), o arco mais lento passava alto demais: desceu. O quinto é
                    // medido: com um décimo, uma posição a 0,104 saía 6 % mais rápida que a antiga; com um quinto, nenhuma passa de 5 %.
                    presaNoTeto = inclinacao < pMaisLento && fracao < 0.2f;
                    return golpe;
                }
                if (orcamento.Esgotado) return null;
                // O que falhou não foi a altura na rede: perto do fim dos 3 s, é o tempo (uma variação ainda no ar) — o balão.
                if (!float.IsFinite(zQuerido)) return m[1].T >= TempoDoBalao ? Balao(vh, inclinacao * vh) : null;
                if (!MudarInclinacao(zQuerido > zNaRede, zNaRede, zQuerido)) return null;
                continue;
            }
            // Curta e já quase no fim dos 3 s: quicar no alvo com esse p passa do tempo — é o balão.
            if (curta && float.IsFinite(erro) && m[1].T >= TempoDoBalao + 0.1f) return Balao(vh, inclinacao * vh);

            // Fora do alvo. Com p fixo, a altura na rede cresce com a rapidez (menos tempo pra cair até lá) e nunca passa da
            // reta do lançamento. Então: alta demais e curta (pede mais rapidez), ou baixa demais e longa (pede menos) — ou
            // baixa demais sem que nem a reta alcance a janela —, não há rapidez que sirva com esse p: muda p já.
            if (float.IsFinite(zNaRede))
            {
                float variacao = Variacao(Util.Limitar(ateARede / vh, 0, m[0].T), vh * MathF.Sqrt(1 + inclinacao * inclinacao));
                float reta = z0 + inclinacao * ateARede;
                // Bateu na rede com a reta do lançamento mal passando a janela (colada na rede, o que a bola cai até lá é
                // milímetros): quem resolve é p, não rapidez — subir a rapidez até passar dá balão a 50 m/s.
                if (m[0].Evento.Tipo == TipoDeEventoDaBola.Rede && reta < Quadra.AlturaDaRede + variacao + 0.05f)
                {
                    if (!MudarInclinacao(subir: true, reta - 0.05f, Quadra.AlturaDaRede + variacao + FolgaNaRedeDaChiquita)) return null;
                    continue;
                }
                if (zNaRede > Limite - variacao && curta)
                {
                    if (!MudarInclinacao(subir: false, zNaRede, Limite - variacao - FolgaNaRedeDaChiquita)) return null;
                    continue;
                }
                if (zNaRede < Quadra.AlturaDaRede + variacao && (!curta || reta < Quadra.AlturaDaRede + variacao))
                {
                    if (!MudarInclinacao(subir: true, zNaRede, Quadra.AlturaDaRede + variacao + FolgaNaRedeDaChiquita)) return null;
                    continue;
                }
            }
            // Aperta o intervalo da rapidez.
            if (curta && vh > vhCurta) { vhCurta = vh; erroCurta = erro; zNaRedeDaCurta = zNaRede; }
            if (!curta && vh < vhLonga) { vhLonga = vh; erroLonga = erro; longaNoAr = noAr; }
            if (vhLonga < vhCurta * 1.003f)
            {
                // O intervalo fechou sem quicar no alvo. Ainda no ar aos 3 s do lado longo: com esse p ela não chega a tempo —
                // é o balão. Bateu na rede do lado curto: quicar no alvo passa por ela — sobe p.
                if (longaNoAr) return Balao(vhCurta, inclinacao * vhCurta);
                if (!MudarInclinacao(subir: true, zNaRedeDaCurta, Quadra.AlturaDaRede + Variacao(ateARede / vhCurta, vhCurta * MathF.Sqrt(1 + inclinacao * inclinacao)) + FolgaNaRedeDaChiquita)) return null;
                continue;
            }
            // Próxima rapidez: secante entre as duas pontas quando as duas quicaram; senão, pela lei de potência (no vácuo o
            // alcance vai com ~vh^1,5 nessa faixa), dentro do intervalo; senão, o meio geométrico dele.
            float proxima;
            if (float.IsFinite(erroCurta) && float.IsFinite(erroLonga) && float.IsFinite(vhLonga))
                proxima = vhCurta + (vhLonga - vhCurta) * (-erroCurta) / (erroLonga - erroCurta);
            else if (float.IsFinite(erro))
                proxima = vh * MathF.Pow(alcance / MathF.Max(0.05f, alcance + erro), 0.67f);
            else proxima = curta ? vh * 1.3f : vh * 0.75f;
            if (float.IsFinite(vhLonga) && vhCurta > 0 && !(proxima > vhCurta && proxima < vhLonga)) proxima = MathF.Sqrt(vhCurta * vhLonga);
            else if (float.IsFinite(vhLonga) && !(proxima < vhLonga)) proxima = 0.75f * vhLonga;
            else if (vhCurta > 0 && !(proxima > vhCurta)) proxima = 1.3f * vhCurta;
            if (!(proxima > 0.02f) || !float.IsFinite(proxima) || proxima * MathF.Sqrt(1 + inclinacao * inclinacao) > RapidezMaximaDoRemate) return null;
            vh = proxima;
        }
        return null;
    }

    // ───────────── Contrapared ─────────────

    /// <summary>
    /// Contrapared: a bola passou e está junto ao próprio vidro de fundo; bate-se NELE (é permitido: a própria parede antes
    /// de cruzar) pra ela voltar por cima, cruzar a rede e quicar do outro lado antes de qualquer parede de lá. Último recurso.
    /// Entre as que servem, a que cai mais perto de ~5 m da rede (funda o bastante pra não ser bola de matar), até
    /// <see cref="RapidezMaximaDaContrapared"/>. ladoDoAlvo: o lado onde ela deve quicar; a própria parede é a do outro lado.
    /// </summary>
    public static Golpe? Contrapared(float x0, float y0, float z0, int ladoDoAlvo, float alvoX) =>
        Anotar(Solucionador.Contrapared, x0, y0, z0, ladoDoAlvo, alvoX, Bola.SubPassosNestaThread, ResolverContrapared(x0, y0, z0, ladoDoAlvo, alvoX));

    private const int ColunasDaContrapared = 8;   // vy = 6, 8, …, 20 m/s
    private const int LinhasDaContrapared = 10;   // vz = 4, 5, …, 13 m/s

    /// <summary>
    /// A grade de sempre — vy de 6 a 20 m/s, vz de 4 a 13, vx mirando alvoX em ~1,3 s — e a nota de sempre: |quique − 5 m|
    /// + 0,05·rapidez, a menor entre as robustas. Numa linha de vz, com vy crescendo, os pontos são: curtos (não cruzam),
    /// os que valem com o quique cada vez mais fundo, longos (parede de lá, fora, rápidos demais). Então a menor nota da
    /// linha está junto do quique a 5 m, que a busca binária acha; uma fila por nota confere a robustez da melhor pra pior,
    /// abrindo a linha pra fora só quando a candidata dela não aguenta. Dá o mesmo golpe da varredura inteira (conferido
    /// em 5.113 posições; há teste) enquanto as linhas tiverem essa forma; se uma fugir dela, pode sair outra contrapared
    /// robusta, ou nenhuma — nunca uma que não vale. Teto medido: ~10 mil sub-passos (a varredura passava de 40 mil).
    /// </summary>
    private static Golpe? ResolverContrapared(float x0, float y0, float z0, int ladoDoAlvo, float alvoX)
    {
        int meuLado = -ladoDoAlvo;
        bool Vale(List<Marca> m) =>
            m.Count >= 3
            && m[0].Evento.Tipo == TipoDeEventoDaBola.Parede && m[0].Evento.Parede == QualParede.Fundo && m[0].Evento.Lado == meuLado
            && m[1].Evento.Tipo == TipoDeEventoDaBola.CruzouRede && m[1].Evento.Para == ladoDoAlvo
            && m[2].Evento.Tipo == TipoDeEventoDaBola.Quique && m[2].Evento.Lado == ladoDoAlvo;
        bool Prefixo(List<Marca> m) =>
            m[0].Evento.Tipo == TipoDeEventoDaBola.Parede && m[0].Evento.Parede == QualParede.Fundo && m[0].Evento.Lado == meuLado
            && (m.Count < 2 || (m[1].Evento.Tipo == TipoDeEventoDaBola.CruzouRede && m[1].Evento.Para == ladoDoAlvo))
            && (m.Count < 3 || (m[2].Evento.Tipo == TipoDeEventoDaBola.Quique && m[2].Evento.Lado == ladoDoAlvo));

        // A bola vai à parede e volta: ~1,3 s até quicar do outro lado. Mira o x do alvo com isso; a simulação diz onde cai.
        float vx = Util.Limitar(alvoX, -Quadra.MeiaLargura + 0.5f, Quadra.MeiaLargura - 0.5f) - x0;
        vx /= 1.3f;
        Velocidade Lancamento(int coluna, int linha) => new(vx, meuLado * (6 + 2 * coluna), 4 + linha, 0);

        var simuladas = new List<Marca>?[ColunasDaContrapared, LinhasDaContrapared];
        List<Marca>? Eventos(int coluna, int linha)
        {
            var v = Lancamento(coluna, linha);
            if (Rapidez(v) > RapidezMaximaDaContrapared) return null;
            return simuladas[coluna, linha] ??= Simular(x0, y0, z0, v, 4, 3, Passo, Prefixo);
        }
        // Longa: rápida demais, ou cruzou a rede e não quicou do outro lado antes de parede ou saída.
        bool Longa(List<Marca>? m) => m is null
            || (m.Count >= 2 && m[1].Evento.Tipo == TipoDeEventoDaBola.CruzouRede && m[1].Evento.Para == ladoDoAlvo && !Vale(m));
        // Já passou do quique a 5 m (ou virou longa): falso… verdadeiro ao longo da linha.
        bool Passou(int coluna, int linha)
        {
            var m = Eventos(coluna, linha);
            return Longa(m) || (m is not null && Vale(m) && MathF.Abs(m[2].Evento.Y) >= 5f);
        }
        float Nota(List<Marca> m, Velocidade v) => MathF.Abs(MathF.Abs(m[2].Evento.Y) - 5f) + 0.05f * Rapidez(v);

        // Fila por (nota, coluna, linha): o empate fica com quem vinha antes na varredura antiga (vy por fora, vz por dentro).
        var fila = new PriorityQueue<(int Coluna, int Linha, int Sentido), (float Nota, int Coluna, int Linha)>();
        void Enfileirar(int coluna, int linha, int sentido)
        {
            if (coluna < 0 || coluna >= ColunasDaContrapared) return;
            if (Eventos(coluna, linha) is not List<Marca> m || !Vale(m)) return;
            fila.Enqueue((coluna, linha, sentido), (Nota(m, Lancamento(coluna, linha)), coluna, linha));
        }
        // De vz alto pra baixo: vz menor pede vy maior, e a coluna da linha anterior é a dica.
        int dica = ColunasDaContrapared / 2;
        for (int linha = LinhasDaContrapared - 1; linha >= 0; linha--)
        {
            int primeira = PrimeiroVerdadeiro(ColunasDaContrapared, dica, coluna => Passou(coluna, linha));
            dica = primeira;
            Enfileirar(primeira - 1, linha, -1);
            Enfileirar(primeira, linha, +1);
        }
        while (fila.TryDequeue(out var c, out _))
        {
            var v = Lancamento(c.Coluna, c.Linha);
            if (Eventos(c.Coluna, c.Linha) is List<Marca> m && Robusta(x0, y0, z0, v, 4, 3, Margem.DePotencia, Vale, Prefixo))
            {
                var quique = m[2];
                return new Golpe(quique.Evento.X, quique.Evento.Y, quique.T, TipoDeGolpe.Contrapared, Lancamento: v);
            }
            Enfileirar(c.Coluna + c.Sentido, c.Linha, c.Sentido);   // não aguentou: a próxima da linha, pra fora
        }
        return null;
    }

    /// <summary>
    /// Num predicado falso… verdadeiro sobre 0..n−1, o primeiro índice verdadeiro (n se nenhum), galopando a partir da dica:
    /// com dica boa, duas avaliações; no pior caso, ~2·log2(n).
    /// </summary>
    private static int PrimeiroVerdadeiro(int n, int dica, Func<int, bool> verdadeiro)
    {
        int h = Math.Clamp(dica, 0, n - 1);
        int lo, hi;   // falso em lo − 1 (ou lo = 0), verdadeiro em hi (ou hi = n)
        if (verdadeiro(h))
        {
            lo = 0; hi = h;
            for (int salto = 1; hi > 0; salto *= 2)
            {
                int k = Math.Max(0, h - salto);
                if (!verdadeiro(k)) { lo = k + 1; break; }
                hi = k;
                if (k == 0) break;
            }
        }
        else
        {
            lo = h + 1; hi = n;
            for (int salto = 1; lo < n; salto *= 2)
            {
                int k = Math.Min(n - 1, h + salto);
                if (verdadeiro(k)) { hi = k; break; }
                lo = k + 1;
                if (k == n - 1) break;
            }
        }
        while (lo < hi)
        {
            int meio = (lo + hi) / 2;
            if (verdadeiro(meio)) hi = meio; else lo = meio + 1;
        }
        return lo;
    }

    // ───────────── Remate por 3 e por 4 ─────────────

    /// <summary>
    /// Remate por 4: plano, forte e pra baixo, quica do outro lado e sai por cima da parede de fundo de lá (ponto de quem
    /// bateu). Só existe perto da rede e com a bola alta — de longe o remate não desce a tempo de quicar perto e subir.
    /// Entre as que servem, a mais lenta; empatando, a que sai mais perto de alvoX.
    /// </summary>
    public static Golpe? SmashPor4(float x0, float y0, float z0, int ladoDoAlvo, float alvoX) =>
        Anotar(Solucionador.SmashPor4, x0, y0, z0, ladoDoAlvo, alvoX, Bola.SubPassosNestaThread, ResolverSmashPor4(x0, y0, z0, ladoDoAlvo, alvoX));

    private static Golpe? ResolverSmashPor4(float x0, float y0, float z0, int ladoDoAlvo, float alvoX)
    {
        float distanciaAoFundo = MathF.Max(1f, Quadra.MeioComprimento + MathF.Abs(y0));
        float preferido = MathF.Atan2(alvoX - x0, distanciaAoFundo);
        var azimutes = new List<float>();
        foreach (float graus in new[] { 0f, -8f, 8f, -16f, 16f, -24f, 24f }) azimutes.Add(preferido + graus * MathF.PI / 180f);
        return Remate(x0, y0, z0, ladoDoAlvo, TipoDeGolpe.SmashPor4, azimutes, lateralDaSaida: 0,
            saiu => Quadra.LadoDe(saiu.Y) == ladoDoAlvo && MathF.Abs(saiu.Y) >= Quadra.MeioComprimento && MathF.Abs(saiu.X) < Quadra.MeiaLargura);
    }

    /// <summary>
    /// Remate por 3: quica do outro lado e sai pela lateral (por cima dela ou por onde a quadra deixar) do lado ladoDaSaida
    /// (sinal de x NO MUNDO: +1 sai por x = +5, -1 por x = -5). Ponto de quem bateu. Entre as que servem, a mais lenta;
    /// empatando, a menos cruzada.
    /// </summary>
    public static Golpe? SmashPor3(float x0, float y0, float z0, int ladoDoAlvo, int ladoDaSaida) =>
        Anotar(Solucionador.SmashPor3, x0, y0, z0, ladoDoAlvo, ladoDaSaida, Bola.SubPassosNestaThread, ResolverSmashPor3(x0, y0, z0, ladoDoAlvo, ladoDaSaida));

    private static Golpe? ResolverSmashPor3(float x0, float y0, float z0, int ladoDoAlvo, int ladoDaSaida)
    {
        int sinal = ladoDaSaida >= 0 ? 1 : -1;
        var azimutes = new List<float>();
        for (float graus = 10; graus <= 60; graus += 5) azimutes.Add(sinal * graus * MathF.PI / 180f);
        return Remate(x0, y0, z0, ladoDoAlvo, TipoDeGolpe.SmashPor3, azimutes, lateralDaSaida: sinal,
            saiu => saiu.X * sinal >= Quadra.MeiaLargura);
    }

    private const int NiveisDeRapidez = 10;   // 22, 24, …, 40 m/s
    private const int Elevacoes = 17;         // -8°, -10°, …, -40°

    /// <summary>
    /// Folgas dos cortes do remate. Na reta: o ponto em que ela cruza um plano a bola só erra por arredondamento (a
    /// direção dela deriva ~1e-5 rad em 300 sub-passos: 0,2 mm em 12 m; 1 cm cobre com sobra). Na altura
    /// do limite analítico: o sub-passo do quique afunda a bola até |vz|/240 abaixo do chão, e ela volta dali. No perfil: a
    /// bola de verdade, noutro azimute, difere dele só por arredondamento (~0,1 mm) — 3 cm é folga de sobra. Todas só
    /// alargam o que se simula; nunca cortam o que a varredura antiga acharia.
    /// </summary>
    private const float FolgaNaReta = 0.01f, FolgaNaAltura = 0.25f, FolgaNaRede = 0.01f, FolgaDoPerfil = 0.03f;

    /// <summary>
    /// Qual plano a bola cruza primeiro (rede, lateral, fundo): se dois caem no mesmo sub-passo, a Bola decide pela ordem
    /// dela (rede, chão, lateral, fundo), não por qual veio antes na reta. Então a dúvida vai até um sub-passo de caminho
    /// na rapidez máxima do remate.
    /// </summary>
    private const float FolgaDaQuina = RapidezMaximaDoRemate * Bola.PassoMaximo + 0.02f;

    /// <summary>
    /// A grade de sempre — rapidez de 22 a 40 m/s, os azimutes dados em ordem de preferência (0 = reto pro outro lado;
    /// positivo gira pra +x), elevação de -8° a -40°; remate plano, sem efeito — e a escolha de sempre: a primeira que vale e
    /// aguenta as margens, na ordem rapidez → azimute → elevação. Vale: cruza a rede, quica do outro lado antes de qualquer
    /// parede e a próxima coisa é sair do jeito pedido (lateralDaSaida ±1 pela lateral daquele x; 0 pelo fundo do alvo).
    /// <para>
    /// Percorre a MESMA grade na MESMA ordem e devolve o mesmo golpe, mas só simula de verdade a candidata que pode valer.
    /// O resto é descartado por cortes exatos — nenhum tira uma candidata que valeria:
    /// <list type="bullet">
    /// <item>Sem efeito a bola não curva (o arrasto e o atrito do quique são ao longo do movimento; o spin que o quique dá
    /// só empurra no plano vertical dele): vista de cima ela anda na reta do azimute. O azimute cuja reta bate noutra parede
    /// antes da de saída não tem remate (<see cref="Reta"/>).</item>
    /// <item>Gravidade e arrasto só baixam a inclinação: a bola fica abaixo da reta do lançamento, e a elevação cuja reta
    /// passa abaixo da rede vai na rede. Depois do quique ela fica abaixo de um limite analítico (<see cref="NoQuique"/>).</item>
    /// <item>E o principal: de lado — altura contra distância —, a trajetória é a MESMA em todos os azimutes; só muda onde
    /// ficam a rede e a parede de saída na reta. Um <see cref="Perfil"/> por (rapidez, elevação) diz, pra todos os azimutes
    /// de uma vez, quais com certeza não valem.</item>
    /// </list>
    /// A candidata que sobra passa pelo MESMO Simular e pela MESMA Robusta de antes: o golpe devolvido é o da varredura
    /// antiga (há teste de equivalência; a correção O4-8 conferiu em 123 mil posições — duas grades finas de 47.520 e
    /// 28 mil de grade larga e aleatórias —: nenhuma solução perdida, nenhum golpe diferente). Custa uma
    /// simulação por (rapidez, elevação) que sobra dos cortes, mais as de verdade das candidatas que o perfil não descarta —
    /// a varredura fazia uma por (rapidez, azimute, elevação), até 1.870, mais a robustez de cada uma que valia.
    /// </para>
    /// Teto medido (150 mil posições aleatórias alcançáveis por remate, z de 1,5 a 2,7): 20,5 mil sub-passos no por 3 e
    /// 17,4 mil no por 4, contra 350 mil da varredura; acima do alcance (z até 3,7, que a Partida não pede) chega a 26 mil.
    /// O pior caso é resposta em nível alto ou nenhuma, com os dez níveis a percorrer. Se crescer (paredes novas, mais
    /// níveis), a saída é apertar o <see cref="NoQuique"/> com o Magnus pra baixo depois do quique, que ele hoje ignora — é
    /// o que mais sobra nos perfis de remate raso que bate baixo na parede.
    /// </summary>
    private static Golpe? Remate(float x0, float y0, float z0, int ladoDoAlvo, TipoDeGolpe tipo, List<float> azimutes, int lateralDaSaida,
        Func<EventoDaBola, bool> saidaCerta)
    {
        bool Vale(List<Marca> m) =>
            m.Count >= 3
            && m[0].Evento.Tipo == TipoDeEventoDaBola.CruzouRede && m[0].Evento.Para == ladoDoAlvo
            && m[1].Evento.Tipo == TipoDeEventoDaBola.Quique && m[1].Evento.Lado == ladoDoAlvo
            && m[2].Evento.Tipo == TipoDeEventoDaBola.Saiu && saidaCerta(m[2].Evento);
        bool Prefixo(List<Marca> m) =>
            m[0].Evento.Tipo == TipoDeEventoDaBola.CruzouRede && m[0].Evento.Para == ladoDoAlvo
            && (m.Count < 2 || (m[1].Evento.Tipo == TipoDeEventoDaBola.Quique && m[1].Evento.Lado == ladoDoAlvo))
            && (m.Count < 3 || (m[2].Evento.Tipo == TipoDeEventoDaBola.Saiu && saidaCerta(m[2].Evento)));
        // Exatamente as contas da varredura antiga (22 + 2·nível e -8 - 2·k são exatos em float).
        float ElevacaoDe(int k) => (-8 - 2 * k) * MathF.PI / 180f;
        Velocidade Lancamento(int nivel, int azimute, int k)
        {
            float rapidez = 22 + 2 * nivel;
            float elevacao = ElevacaoDe(k);
            float horizontal = rapidez * MathF.Cos(elevacao);
            float a = azimutes[azimute];
            return new Velocidade(horizontal * MathF.Sin(a), ladoDoAlvo * horizontal * MathF.Cos(a), rapidez * MathF.Sin(elevacao), 0);
        }

        // A reta de cada azimute e, por ela, a elevação mais funda que ainda passa a rede (-1: nenhuma). Fora da quadra as
        // contas da reta não valem: sem corte, tudo se simula.
        bool naCaixa = MathF.Abs(x0) < Quadra.MeiaLargura && MathF.Abs(y0) < Quadra.MeioComprimento && z0 > 0;
        var retas = new Reta?[azimutes.Count];
        var maisFunda = new int[azimutes.Count];
        bool temPorta = false;
        for (int a = 0; a < azimutes.Count; a++)
        {
            maisFunda[a] = naCaixa ? -1 : Elevacoes - 1;
            if (!naCaixa || Reta.Do(x0, y0, ladoDoAlvo, azimutes[a], lateralDaSaida) is not Reta reta) continue;
            retas[a] = reta;
            temPorta |= reta.PodeSairBaixo;
            for (int k = 0; k < Elevacoes; k++)
            {
                float e = ElevacaoDe(k);
                if (z0 + reta.AteARede * MathF.Sin(e) / MathF.Cos(e) < Quadra.AlturaDaRede - FolgaNaRede) break;   // a reta do lançamento na rede
                maisFunda[a] = k;
            }
        }

        // Perfis por (nível, elevação), feitos quando um azimute precisa e estendidos até a parede de saída de quem pede.
        var perfis = new Perfil?[NiveisDeRapidez, Elevacoes];
        Perfil PerfilDe(int nivel, int k)
        {
            if (perfis[nivel, k] is Perfil pronto) return pronto;
            float rapidez = 22 + 2 * nivel, elevacao = ElevacaoDe(k);
            return perfis[nivel, k] = new Perfil(rapidez * MathF.Cos(elevacao), rapidez * MathF.Sin(elevacao), z0, temPorta);
        }
        var noQuique = new NoQuique[Elevacoes];
        for (int nivel = 0; nivel < NiveisDeRapidez; nivel++)
        {
            if (naCaixa) for (int k = 0; k < Elevacoes; k++) noQuique[k] = NoQuique.Limites(22 + 2 * nivel, -ElevacaoDe(k), z0);
            for (int a = 0; a < azimutes.Count; a++)
            for (int k = 0; k <= maisFunda[a]; k++)
            {
                if (retas[a] is Reta reta && (!noQuique[k].PodeValer(reta) || !PerfilDe(nivel, k).PodeValer(reta))) continue;
                var v = Lancamento(nivel, a, k);
                var m = Simular(x0, y0, z0, v, 2.5f, 3, Passo, Prefixo);
                if (!Vale(m) || !Robusta(x0, y0, z0, v, 2.5f, 3, Margem.DePotencia, Vale, Prefixo)) continue;
                var quique = m[1];
                return new Golpe(quique.Evento.X, quique.Evento.Y, quique.T, tipo, Lancamento: v);
            }
        }
        return null;
    }

    /// <summary>
    /// A reta, vista de cima, de um remate sem efeito num azimute: a distância (na reta) até a rede e até a parede de saída,
    /// a altura mais baixa do topo da parede ali e se dá pra sair por baixo (a porta) — ou nada, se ela não cruza a rede
    /// pro lado do alvo ou chega antes a outra parede. Nas bordas (porta, canto, quina entre lateral e fundo), a dúvida fica
    /// com o lado que corta menos.
    /// </summary>
    private readonly record struct Reta(float AteARede, float AteASaida, float AlturaMinima, bool PodeSairBaixo)
    {
        public static Reta? Do(float x0, float y0, int ladoDoAlvo, float azimute, int lateralDaSaida)
        {
            float dx = MathF.Sin(azimute), du = MathF.Cos(azimute);   // du: rumo ao fundo do lado do alvo
            float u0 = ladoDoAlvo * y0;                                // < 0: do lado de cá da rede
            if (u0 >= 0 || du <= 1e-3f) return null;                   // já do lado do alvo, ou nunca chega à rede
            float ateARede = -u0 / du;
            float ateOFundo = (Quadra.MeioComprimento - u0) / du;
            float ateALateral = dx > 1e-6f ? (Quadra.MeiaLargura - x0) / dx : dx < -1e-6f ? (-Quadra.MeiaLargura - x0) / dx : float.PositiveInfinity;
            if (ateARede > ateALateral + FolgaDaQuina) return null;    // bate na própria lateral antes da rede
            // Na quina (fundo e lateral quase juntos) não se sabe qual parede vem primeiro: vale a mais perto e o topo mais baixo.
            float ateASaida = MathF.Min(ateOFundo, ateALateral);
            float yNaLateral = MathF.Abs(u0 + du * ateALateral);       // |y| de onde a reta cruza a lateral
            float topoDaLateral = yNaLateral >= Quadra.InicioDoCanto + FolgaNaReta ? Quadra.AlturaDaParede : MathF.Min(Quadra.AlturaDoMeio, Quadra.AlturaDoDegrau);
            bool porta = yNaLateral >= Quadra.InicioDaPorta - FolgaNaReta && yNaLateral <= Quadra.FimDaPorta + FolgaNaReta;
            bool quina = MathF.Abs(ateOFundo - ateALateral) <= FolgaDaQuina;
            if (lateralDaSaida == 0)
                return ateOFundo < ateALateral + FolgaDaQuina
                    ? new Reta(ateARede, ateASaida, quina ? MathF.Min(Quadra.AlturaDaParede, topoDaLateral) : Quadra.AlturaDaParede, quina && porta)
                    : null;
            if (MathF.Sign(dx) != lateralDaSaida || ateALateral > ateOFundo + FolgaDaQuina) return null;
            return new Reta(ateARede, ateASaida, quina ? MathF.Min(Quadra.AlturaDaParede, topoDaLateral) : topoDaLateral, porta);
        }
    }

    /// <summary>
    /// A trajetória de lado — altura contra distância horizontal — de um remate sem efeito, que é a mesma em todos os
    /// azimutes. É a <see cref="Bola"/> de verdade, com o passo e o limite de tempo do Simular, num corredor livre: sai de
    /// y = -0,05 rumo a -y (sem cruzar a rede) e, perto do fundo, é reposta mais atrás com a mesma velocidade e o mesmo spin
    /// (a física não depende de x nem de y). Guarda os pontos de cada passo e onde ela quicou; só simula o trecho que algum
    /// azimute pediu.
    /// </summary>
    private sealed class Perfil
    {
        private const float Inicio = -0.05f;
        /// <summary>O topo mais baixo de parede (fora da porta): descendo abaixo dele depois do quique, a bola não sai mais por cima.</summary>
        private static readonly float MenorTopo = MathF.Min(Quadra.AlturaDaParede, MathF.Min(Quadra.AlturaDoMeio, Quadra.AlturaDoDegrau));

        private readonly Bola _bola = new();
        private readonly List<EventoDaBola> _eventos = new(4);
        private readonly float _folgaNaReta;
        private readonly bool _temPorta;
        private readonly List<float> _s = new(96), _z = new(96);
        private readonly List<bool> _quicouNoPasso = new(96);
        private float _quique1 = float.PositiveInfinity, _quique2 = float.PositiveInfinity, _rolou = float.PositiveInfinity;
        private float _desceuAbaixoDoMenorTopo = float.PositiveInfinity;
        private float _t, _reposto;
        private bool _acabou;   // o tempo do Simular acabou ou a bola parou
        private bool _parado;   // não adianta seguir: quicou duas vezes, rolou, ou desceu abaixo do menor topo (sem porta)

        /// <summary>temPorta: algum azimute sai pela porta — aí o perfil segue mesmo com a bola baixa.</summary>
        public Perfil(float horizontal, float vz, float z0, bool temPorta)
        {
            // Um sub-passo de caminho: o quique e a passagem por um plano podem cair no mesmo sub-passo e em ordens diferentes.
            _folgaNaReta = horizontal * Bola.PassoMaximo + 0.02f;
            _temPorta = temPorta;
            _bola.Posicionar(0, Inicio, z0);
            _bola.Lancar(new Velocidade(0, -horizontal, vz, 0));
            _s.Add(0); _z.Add(z0); _quicouNoPasso.Add(false);
        }

        /// <summary>Simula (com o passo e o limite de tempo do Simular) até passar da distância ate, ou até não adiantar mais.</summary>
        private void SimularAte(float ate)
        {
            while (!_acabou && !_parado && _s[^1] <= ate)
            {
                if (!(_t < 2.5f && _bola.EmJogo && !_bola.Parada)) { _acabou = true; return; }
                _eventos.Clear();
                _bola.Avancar(Passo, _eventos);
                _t += Passo;
                bool quicou = false;
                foreach (var e in _eventos)
                {
                    if (e.Tipo != TipoDeEventoDaBola.Quique) continue;
                    quicou = true;
                    float sq = _reposto + (Inicio - e.Y);
                    if (float.IsPositiveInfinity(_quique1)) _quique1 = sq;
                    else if (float.IsPositiveInfinity(_quique2)) _quique2 = sq;
                    if (_bola.Rolando) _rolou = MathF.Min(_rolou, sq);
                }
                float s = _reposto + (Inicio - _bola.Y);
                _s.Add(s); _z.Add(_bola.Z); _quicouNoPasso.Add(quicou);
                if (!float.IsPositiveInfinity(_quique1) && !_temPorta && _bola.Vz < 0 && _bola.Z < MenorTopo - FolgaDoPerfil)
                    _desceuAbaixoDoMenorTopo = MathF.Min(_desceuAbaixoDoMenorTopo, s);
                if (_bola.Rolando || !float.IsPositiveInfinity(_quique2) || !float.IsPositiveInfinity(_desceuAbaixoDoMenorTopo)) { _parado = true; return; }
                if (_bola.Y < -9.5f)
                {
                    var v = new Velocidade(_bola.Vx, _bola.Vy, _bola.Vz, 0, _bola.Wx, _bola.Wy, _bola.Wz);
                    float y = _bola.Y + 9.4f;
                    _reposto += y - _bola.Y;
                    _bola.Posicionar(_bola.X, y, _bola.Z);
                    _bola.Lancar(v);
                }
            }
        }

        /// <summary>A altura na distância s, se ela está no trecho simulado e não há quique no passo que a contém.</summary>
        private float? AlturaEm(float s)
        {
            if (s < 0 || s > _s[^1]) return null;
            int lo = 0, hi = _s.Count - 1;   // _s cresce: a bola nunca volta
            while (hi - lo > 1) { int meio = (lo + hi) / 2; if (_s[meio] <= s) lo = meio; else hi = meio; }
            if (_quicouNoPasso[hi] || _s[hi] <= _s[lo]) return null;
            return _z[lo] + (_z[hi] - _z[lo]) * (s - _s[lo]) / (_s[hi] - _s[lo]);
        }

        /// <summary>
        /// False só quando é CERTO que o remate nesse azimute não vale: quica antes da rede, passa abaixo dela, chega à
        /// parede de saída sem quicar, quica duas vezes antes dela, chega nela rolando, abaixo do topo ou descendo abaixo do
        /// menor topo, ou o tempo acaba antes. Na dúvida (dentro das folgas), true — e quem chamou simula de verdade.
        /// </summary>
        public bool PodeValer(Reta reta)
        {
            float ms = _folgaNaReta;
            SimularAte(reta.AteASaida + 2 * ms);
            if (_quique1 < reta.AteARede - ms) return false;
            if (_quique1 > reta.AteARede + ms && AlturaEm(reta.AteARede) is float naRede && naRede < Quadra.AlturaDaRede - FolgaDoPerfil) return false;
            if (_quique1 > reta.AteASaida + ms && _s[^1] >= reta.AteASaida + ms) return false;
            if (_quique2 < reta.AteASaida - ms) return false;
            if (_rolou < reta.AteASaida - ms && !reta.PodeSairBaixo) return false;
            if (_acabou && _s[^1] < reta.AteASaida - ms) return false;
            if (!reta.PodeSairBaixo && _quique1 < reta.AteASaida - ms)
            {
                if (_desceuAbaixoDoMenorTopo < reta.AteASaida - ms) return false;
                if (AlturaEm(reta.AteASaida) is float naParede && naParede < reta.AlturaMinima - FolgaDoPerfil) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Limites RIGOROSOS do estado de um remate sem efeito no quique e depois dele, pra rapidez e ângulo abaixo da horizontal
    /// dados, saindo de z0 — o que a física da <see cref="Bola"/> garante, sem simular (ver os passos em <see cref="Limites"/>).
    /// </summary>
    private readonly record struct NoQuique(float QuicaDepoisDe, float QuicaAte, float Apice, float Inclinacao, float HorizontalMaxima, float UmSubPasso)
    {
        /// <summary>
        /// Antes do quique (sem spin, sem Magnus): |vz| nunca cai abaixo de min(vz0, g/(K·vmax)) — então a queda dura no máximo
        /// z0 sobre isso —, a horizontal nunca cai abaixo de vh0·e^(−K·vmax·t), e |vz| no quique fica abaixo da solução de
        /// |vz|' = g − K·vhMin·|vz|; a inclinação cai a d(vz/vh)/ds = −g/vh², entre −g/vh0² e −g/vhMin², então a bola quica
        /// entre as duas parábolas (± um sub-passo: a Bola quica no fim do sub-passo em que passou do chão).
        /// No quique: vz vira e·|vz|; o atrito tira da horizontal entre min(I/(1+I)·vh, μ(1+e)|vz|) — no máximo I/(1+I) dela.
        /// Depois: o topspin do quique só puxa pra baixo, e com ele e o arrasto a inclinação continua caindo mais que −g/vh²;
        /// a horizontal só cresce (Magnus, subindo) até KMagnus·½·v·(altura subida).
        /// </summary>
        public static NoQuique Limites(float rapidez, float angulo, float z0)
        {
            const float g = Bola.G;
            float k = Bola.KArrasto, e = Bola.RestituicaoDoChao, fracaoDoAtrito = Bola.MomentoDeInercia / (1 + Bola.MomentoDeInercia);
            float vh0 = rapidez * MathF.Cos(angulo), vz0 = rapidez * MathF.Sin(angulo);
            float vmax = MathF.Sqrt(rapidez * rapidez + 2 * g * z0);
            float vzMin = MathF.Min(vz0, g / (k * vmax));
            float queda = z0 / vzMin;
            float vhMin = vh0 * MathF.Exp(-k * vmax * queda);
            float assintota = g / (k * vhMin);
            float vzNoQuique = vz0 < assintota ? assintota + (vz0 - assintota) * MathF.Exp(-k * vhMin * queda) : vz0;
            float inclinacao0 = vz0 / vh0, umSubPasso = vh0 * Bola.PassoMaximo;
            float Raiz(float curvatura) => (-inclinacao0 + MathF.Sqrt(inclinacao0 * inclinacao0 + 4 * curvatura * z0)) / (2 * curvatura);
            float quicaAte = Raiz(g / (2 * vh0 * vh0)) + umSubPasso;
            float quicaDepoisDe = Raiz(g / (2 * vhMin * vhMin)) - umSubPasso;
            float vzDepois = e * vzNoQuique;
            float vhDepoisMin = (1 - fracaoDoAtrito) * vhMin;
            float vhDepoisMax = MathF.Max((1 - fracaoDoAtrito) * vh0, vh0 - Bola.AtritoDoChao * (1 + e) * vzMin);
            float apice = vzDepois * vzDepois / (2 * g);
            float vDepois = MathF.Sqrt(vhDepoisMax * vhDepoisMax + vzDepois * vzDepois);
            return new NoQuique(quicaDepoisDe, quicaAte, apice, vzDepois / vhDepoisMin, vhDepoisMax + Bola.KMagnus * 0.5f * vDepois * apice, umSubPasso);
        }

        /// <summary>
        /// False só se é certo que a bola, nessa reta, não quica entre a rede e a parede de saída nem passa acima do topo dela:
        /// quica antes da rede, chega à parede antes de quicar, ou (sem porta) não tem como estar acima do topo ali.
        /// </summary>
        public bool PodeValer(Reta reta)
        {
            float folga = UmSubPasso + 0.02f;
            if (QuicaAte < reta.AteARede - folga || QuicaDepoisDe > reta.AteASaida + folga) return false;
            return reta.PodeSairBaixo || AlturaMaximaNaParede(reta.AteASaida) >= reta.AlturaMinima - FolgaNaAltura;
        }

        /// <summary>
        /// A altura máxima da bola ao chegar à parede de saída, ateAParede metros (na reta) depois do ponto de partida: depois
        /// do quique ela fica abaixo de s·inclinação − g·s²/(2·vh²) (s: o quanto andou desde o quique, pelo menos ateAParede −
        /// QuicaAte), e nunca acima do ápice.
        /// </summary>
        public float AlturaMaximaNaParede(float ateAParede)
        {
            float depoisDoQuique = MathF.Max(0, ateAParede - QuicaAte);
            float curvatura = Bola.G / (2 * HorizontalMaxima * HorizontalMaxima);
            float topo = Inclinacao / (2 * curvatura);   // onde a parábola-limite para de subir
            float naParede = depoisDoQuique >= topo
                ? Inclinacao * depoisDoQuique - curvatura * depoisDoQuique * depoisDoQuique
                : Inclinacao * topo - curvatura * topo * topo;
            return MathF.Min(Apice, naParede);
        }
    }
}

/// <summary>Os solucionadores de <see cref="GolpesEspeciais"/> (diagnóstico: modo de coleta).</summary>
public enum Solucionador { Chiquita, Contrapared, SmashPor3, SmashPor4 }

/// <summary>
/// Uma chamada anotada no modo de coleta (<see cref="GolpesEspeciais.Coleta"/>): qual solucionador, de onde, pra que lado,
/// o argumento (alvoX; no SmashPor3, ladoDaSaida), o golpe devolvido e quantos sub-passos de bola ela simulou.
/// </summary>
public readonly record struct ChamadaDeSolucionador(Solucionador Qual, float X0, float Y0, float Z0, int LadoDoAlvo, float Argumento, Golpe? Resultado, long SubPassos);
