namespace Padel.Core.Tests;

/// <summary>
/// A dupla da IA joga como dupla de padel (CRONOGRAMA, M1: "formação de verdade — dupla sobe junto, defende junto"): os dois
/// andam como um bloco — à mesma profundidade e deslocados pro lado da bola —, sobem juntos depois de um golpe que dá tempo,
/// recuam juntos quando o adversário lobeia por cima deles, e o parceiro cobre o lado de quem foi na bola (o "cambio"). A
/// formação é o que mais separa os níveis: o Difícil fecha o meio e anda junto na hora; o Fácil desencontra. O parceiro da
/// IA de um humano segue a profundidade do humano, porque o humano não segue a IA.
/// </summary>
public class FormacaoTests
{
    private const float Passo = 1f / 120f;

    // ───────────── Partidas IA x IA: a dupla medida é a rival (time 1), com o perfil da dificuldade ─────────────

    /// <summary>
    /// DiferencaMedia: |profundidade de um − profundidade do outro| (m), média dos passos de rally fora de ±<see cref="JanelaDoGolpe"/>
    /// de cada golpe da dupla (no instante do golpe quem bate está onde a bola está, e isso não é formação) e fora do começo do
    /// ponto (<see cref="JanelaDoSaque"/>). Separados: fração desses passos com a dupla a mais de 2,5 m de profundidade um do
    /// outro — um na rede, outro no fundo. Inclinacao: regressão do x do centro da dupla no x da bola, em todos os passos de
    /// rally (1 = o centro vai aonde a bola vai; 0 = parado no meio). Correlacao: a mesma relação sem a escala.
    /// </summary>
    private sealed record MedidaDaFormacao(double DiferencaMedia, double Separados, double Inclinacao, double Correlacao, int Amostras)
    {
        public override string ToString() =>
            $"diferença média {DiferencaMedia:F2} m, separados {100 * Separados:F1} %, inclinação {Inclinacao:F3}, correlação {Correlacao:F3} ({Amostras} amostras)";
    }

    private const float JanelaDoGolpe = 0.3f;
    /// <summary>
    /// O começo do ponto não é formação, é a regra do saque: a partida põe quem saca a 8,3 m com o parceiro na rede (3,6 m) e
    /// quem recebe a 8 m com o parceiro a 4,2 m — as duas duplas começam separadas por 4 m. Juntar leva ~1 s; 1,5 s cobre o
    /// Médio. Medido em 25/09 (sementes 1 a 4, 300 s): com o começo, o Difícil ficava separado 7,0 % do tempo, 5,9 pontos
    /// disso no primeiro 1,5 s depois do saque.
    /// </summary>
    private const float JanelaDoSaque = 1.5f;
    private static readonly uint[] Sementes = [1, 2, 3, 4];
    private const float SegundosPorPartida = 300;

    private static readonly Lazy<MedidaDaFormacao> NoDificil = new(() => Medir(Dificuldade.Dificil));
    private static readonly Lazy<MedidaDaFormacao> NoFacil = new(() => Medir(Dificuldade.Facil));

    private static MedidaDaFormacao Medir(Dificuldade dificuldade)
    {
        double somaDif = 0, sb = 0, sc = 0, sbb = 0, sbc = 0, scc = 0;
        long nDif = 0, separados = 0, n = 0;
        foreach (uint semente in Sementes)
        {
            var partida = new Partida(new OpcoesDaPartida { Humanos = OpcoesDaPartida.NinguemHumano, Semente = semente, Dificuldade = dificuldade });
            var golpes = new List<float>();
            var amostras = new List<(float T, float DesdeOSaque, float Dif, float Centro, float Bola)>();
            float t = 0, saque = 0;
            partida.Evento += ev =>
            {
                if (ev.Tipo != TipoDeEventoDaPartida.Golpe) return;
                if (ev.Time == 1) golpes.Add(t);
                if (ev.Golpe == TipoDeGolpe.Saque) saque = t;
            };
            var (a, b) = (partida.Jogadores[2], partida.Jogadores[3]);
            while (!partida.Acabou && t < SegundosPorPartida)
            {
                partida.Avancar(Passo);
                t += Passo;
                if (partida.Estado != EstadoDaPartida.Rally) continue;
                amostras.Add((t, t - saque, MathF.Abs(MathF.Abs(a.Y) - MathF.Abs(b.Y)), (a.X + b.X) / 2, partida.Bola.X));
            }
            foreach (var (ta, desdeOSaque, dif, centro, bola) in amostras)
            {
                sb += bola; sc += centro; sbb += bola * bola; sbc += bola * centro; scc += centro * centro; n++;
                if (desdeOSaque < JanelaDoSaque || golpes.Any(g => MathF.Abs(g - ta) < JanelaDoGolpe)) continue;
                somaDif += dif; nDif++;
                if (dif > 2.5f) separados++;
            }
        }
        double mb = sb / n, mc = sc / n;
        double cov = sbc / n - mb * mc, vb = sbb / n - mb * mb, vc = scc / n - mc * mc;
        return new MedidaDaFormacao(somaDif / nDif, (double)separados / nDif, cov / vb, cov / Math.Sqrt(vb * vc), (int)nDif);
    }

    /// <summary>
    /// Medido em 25/09 (sementes 1 a 4, 300 s de cada partida). Antes da formação, o Difícil andava tão separado quanto os
    /// outros: diferença média de 1,27 m e 19,5 % do tempo um na rede e outro no fundo (Fácil 1,00 m / 12,6 %; Médio 1,25 m /
    /// 18,3 %). Com a formação: Difícil 0,52 m / 1,2 %; Médio 0,77 m / 7,0 %; Fácil 1,09 m / 17,5 % (6 partidas de 600 s dão o
    /// mesmo: 0,53 / 1,1; 0,76 / 6,7; 1,10 / 17,8). Os limites ficam a um terço do caminho de volta pro de antes no Difícil
    /// (0,52 + (1,27 − 0,52) / 3 ≈ 0,77 → 0,75 m; 1,2 + (19,5 − 1,2) / 3 ≈ 7 → 5 %, porque 5 % já é um em vinte) e pedem do
    /// Fácil o dobro da diferença do Difícil que sobra hoje com folga (1,09 − 0,52 = 0,57 → 0,4 m) e 3 vezes o tempo separado.
    /// </summary>
    [Fact]
    public void No_dificil_os_dois_andam_a_mesma_profundidade_e_no_facil_desencontram()
    {
        var (dificil, facil) = (NoDificil.Value, NoFacil.Value);
        string contexto = $"Difícil: {dificil}; Fácil: {facil}";
        Assert.True(dificil.DiferencaMedia <= 0.75, $"no Difícil a dupla devia andar à mesma profundidade — {contexto}");
        Assert.True(dificil.Separados <= 0.05, $"no Difícil um na rede e outro no fundo devia ser raro — {contexto}");
        Assert.True(facil.DiferencaMedia >= dificil.DiferencaMedia + 0.4, $"o Fácil devia desencontrar mais que o Difícil — {contexto}");
        Assert.True(facil.Separados >= 3 * dificil.Separados, $"o Fácil devia ficar mais vezes um na rede e outro no fundo — {contexto}");
    }

    /// <summary>
    /// A "cremallera": o centro da dupla vai pro lado da bola (e o de longe fecha o meio). Antes da formação o centro só se
    /// mexia porque um dos dois ia na bola: inclinação 0,018 (Fácil) e 0,036 (Difícil), correlação 0,10 e 0,16 (sementes 1 a
    /// 4, 300 s). Com a formação: 0,064 e 0,279, correlação 0,33 e 0,54 (o Médio fica no meio: 0,19). A conta do Difícil: sem
    /// ninguém na bola, o centro segue a bola com o fator do perfil (0,5); com um dos dois indo nela, perto de (1 + 0,5) / 2; mas
    /// o x que a dupla acompanha anda atrasado e o centro não passa de ±2,9 m (4,7 − 3,6 / 2) — o medido é 0,28. O limite, 0,2,
    /// fica a um terço do caminho de volta pro de antes (0,28 − (0,28 − 0,04) / 3 ≈ 0,2).
    /// </summary>
    [Fact]
    public void O_centro_da_dupla_acompanha_o_x_da_bola_mais_no_dificil_que_no_facil()
    {
        var (dificil, facil) = (NoDificil.Value, NoFacil.Value);
        string contexto = $"Difícil: {dificil}; Fácil: {facil}";
        Assert.True(dificil.Inclinacao >= 0.2, $"no Difícil o centro da dupla devia acompanhar a bola — {contexto}");
        Assert.True(dificil.Inclinacao >= facil.Inclinacao + 0.1, $"o Difícil devia acompanhar a bola mais que o Fácil — {contexto}");
        Assert.True(dificil.Correlacao > facil.Correlacao, $"idem, pela correlação — {contexto}");
    }

    // ───────────── Cenas montadas ─────────────

    /// <summary>Uma partida só de IA no rally (o time 0 sacou), com os quatro parados onde a cena manda.</summary>
    private static Partida Cena(Dificuldade dificuldade, uint semente = 5, bool[]? humanos = null)
    {
        var partida = new Partida(new OpcoesDaPartida { Humanos = humanos ?? OpcoesDaPartida.NinguemHumano, Semente = semente, Dificuldade = dificuldade });
        partida.Sacar(Entrada.Vazia);
        return partida;
    }

    private static void Posicionar(Jogador j, float x, float y)
    {
        j.X = x; j.Y = y; j.Vx = 0; j.Vy = 0; j.Cooldown = 0;
    }

    /// <summary>O jogador bate o golpe da posição (x, y, z): a bola sai, o árbitro registra e as duas IAs ficam sabendo — como no Golpear da partida.</summary>
    private static void Bater(Partida partida, Jogador quem, Golpe golpe, float z = 1.0f)
    {
        partida.Arbitro.RegistrarGolpe(quem.Time);
        partida.Bola.Posicionar(quem.X, quem.Y, z);
        partida.Bola.Lancar(GolpesEspeciais.VelocidadeDe(golpe, quem.X, quem.Y, z));
        foreach (var ia in partida.IAs) ia.AoGolpear(partida, golpe, quem);
    }

    /// <summary>O lob da IA (1,6 s de voo) — o da dupla que sobe.</summary>
    private static Golpe Lob(float alvoX, float alvoY) => new(alvoX, alvoY, 1.6f, TipoDeGolpe.Lob, Efeito: new Efeito(-400, 0));

    /// <summary>
    /// Lob que passa POR CIMA de quem está na rede: 2 s de voo até 9 m do outro lado. Ainda a 3,5 m de altura a 5,5 m da rede,
    /// e só desce a 2,3 m (o teto do voleio da IA) a ~6,8 m — longe demais pra quem está a 3,2 m bater no ar. O lob de 1,6 s
    /// até 8,5 m, não: a ~5,3 m da rede está a 2,3 m, e a IA na rede recua dois passos e bate de bandeja.
    /// </summary>
    private static Golpe LobPorCima(float alvoX) => new(alvoX, -9f, 2.0f, TipoDeGolpe.Lob, Efeito: new Efeito(-400, 0));

    /// <summary>
    /// Põe o time 1 na rede de verdade: um voleio dele (a IA passa a "Rede") e um segundo de formação sem bola vindo, com os
    /// dois a 3,2 m da rede (a profundidade de rede da IA).
    /// </summary>
    private static void TimeUmNaRede(Partida partida, float xDireita, float xEsquerda)
    {
        var (direita, esquerda) = (partida.Jogadores[2], partida.Jogadores[3]);
        Posicionar(direita, xDireita, -3.2f);
        Posicionar(esquerda, xEsquerda, -3.2f);
        Bater(partida, direita, new Golpe(0, 5, 0.8f, TipoDeGolpe.Normal, Efeito: new Efeito(1200, 0)));
        for (int i = 0; i < 120; i++) partida.IAs[1].Reagir(Passo, partida);
        Posicionar(direita, xDireita, -3.2f);
        Posicionar(esquerda, xEsquerda, -3.2f);
    }

    /// <summary>
    /// A dupla na rede toma um lob por cima: os dois recuam juntos pra trás da linha de saque — quem vai na bola e o parceiro.
    /// "Em ~1 s": do repouso a 3,2 m até a linha de saque (6,95 m) são 3,75 m, que o jogador do Difícil (6,6 m/s, 9 m/s²)
    /// corre em <see cref="Jogador.TempoParaChegar"/> = 0,93 s; mais o tempo de reação do perfil (0,10 s) e 0,15 s de folga
    /// (a frenagem perto do alvo e o passo), o limite fica em 1,18 s. Antes da formação o parceiro ficava na rede.
    /// </summary>
    [Fact]
    public void Dupla_na_rede_lobeada_por_cima_recua_junta_para_tras_da_linha_de_saque()
    {
        var partida = Cena(Dificuldade.Dificil);
        TimeUmNaRede(partida, xDireita: -1.8f, xEsquerda: 1.8f);
        var casa = partida.Jogadores[0];
        Posicionar(casa, 1.5f, 7.5f);
        Posicionar(partida.Jogadores[1], -2f, 6.5f);
        Bater(partida, casa, LobPorCima(1.5f));   // passa por cima do da esquerda do time 1 (x = 1,8) e cai funda

        var (a, b) = (partida.Jogadores[2], partida.Jogadores[3]);
        float limite = a.TempoParaChegar(Quadra.LinhaDeSaque - 3.2f) + Perfis.Dificil.Reacao + 0.15f;
        float? quando = null;
        float t = 0;
        string trilha = "";
        for (int i = 0; i < 240 && quando is null && partida.Estado == EstadoDaPartida.Rally; i++)
        {
            partida.Avancar(Passo);
            t += Passo;
            if (i % 24 == 23) trilha += $" t={t:F1}: {a.Y:F1}/{b.Y:F1}";
            if (MathF.Abs(a.Y) > Quadra.LinhaDeSaque && MathF.Abs(b.Y) > Quadra.LinhaDeSaque) quando = t;
        }
        Assert.True(quando is not null, $"os dois deviam recuar pra trás da linha de saque; y de cada um:{trilha}");
        Assert.True(quando <= limite, $"os dois atrás da linha de saque só em {quando:F2} s (limite {limite:F2} s);{trilha}");
    }

    /// <summary>
    /// O "cambio": o lob passa por cima do lado de um, e quem vai na bola é o outro (chega antes, na diagonal). Quem não foi
    /// troca de lado e cobre o lado que ficou vazio — senão os dois ficam do mesmo lado da quadra.
    /// </summary>
    [Fact]
    public void No_lob_pelo_lado_de_um_se_o_outro_vai_na_bola_o_primeiro_troca_de_lado()
    {
        var partida = Cena(Dificuldade.Dificil);
        TimeUmNaRede(partida, xDireita: -1.8f, xEsquerda: 1.8f);
        var (direita, esquerda) = (partida.Jogadores[2], partida.Jogadores[3]);
        // O da esquerda (x = +1,8) colado na rede; o da direita um pouco atrás: o lob pelo lado da esquerda é dele.
        Posicionar(esquerda, 1.8f, -1.0f);
        Posicionar(direita, -1.8f, -4.0f);
        var casa = partida.Jogadores[0];
        Posicionar(casa, 2f, 7.5f);
        Posicionar(partida.Jogadores[1], -2f, 6.5f);
        Bater(partida, casa, LobPorCima(2.2f));

        for (int i = 0; i < 180 && partida.Estado == EstadoDaPartida.Rally; i++) partida.Avancar(Passo);
        Assert.True(partida.IAs[1].Responsavel == direita,
            $"a cena pede que o da direita vá na bola (chega antes): responsável {partida.IAs[1].Responsavel?.Nome ?? "nenhum"}, direita em ({direita.X:F2}; {direita.Y:F2}), esquerda em ({esquerda.X:F2}; {esquerda.Y:F2}), bola em ({partida.Bola.X:F2}; {partida.Bola.Y:F2}; {partida.Bola.Z:F2})");
        Assert.True(esquerda.X < direita.X - 1f,
            $"quem não foi na bola devia trocar de lado e cobrir o lado vazio: esquerda em x = {esquerda.X:F2}, direita (na bola) em x = {direita.X:F2}");
        Assert.True(esquerda.X < 0, $"o da esquerda devia estar do outro lado do meio da quadra: x = {esquerda.X:F2}");
    }

    /// <summary>
    /// Lob bom da dupla que estava no fundo: os dois sobem juntos. Quem estava mais à frente não dispara sozinho pra rede —
    /// espera o parceiro, que bateu lá do fundo, e chegam juntos. Antes da formação, o da frente (a 5,5 m) chegava à zona de
    /// rede (4,5 m) 0,58 s antes do outro (que bateu a 9 m).
    /// </summary>
    [Fact]
    public void Depois_de_um_lob_bom_os_dois_sobem_juntos()
    {
        var partida = Cena(Dificuldade.Dificil);
        var (fundo, frente) = (partida.Jogadores[2], partida.Jogadores[3]);
        Posicionar(fundo, -2f, -9f);
        Posicionar(frente, 2f, -5.5f);
        Posicionar(partida.Jogadores[0], 2f, 3.2f);
        Posicionar(partida.Jogadores[1], -2f, 3.2f);
        Bater(partida, fundo, Lob(0, 8.2f));

        const float ZonaDeRede = 4.5f;
        float? chegouFundo = null, chegouFrente = null;
        float t = 0;
        for (int i = 0; i < 192 && partida.Estado == EstadoDaPartida.Rally; i++)   // 1,6 s: o voo do lob
        {
            partida.Avancar(Passo);
            t += Passo;
            if (MathF.Abs(fundo.Y) <= ZonaDeRede) chegouFundo ??= t;
            if (MathF.Abs(frente.Y) <= ZonaDeRede) chegouFrente ??= t;
        }
        Assert.True(chegouFundo is not null && chegouFrente is not null,
            $"os dois deviam subir à rede enquanto o lob voa: y {fundo.Y:F2} (quem bateu) e {frente.Y:F2}");
        float diferenca = MathF.Abs((chegouFundo ?? 0) - (chegouFrente ?? 0));
        Assert.True(diferenca <= 0.3f, $"deviam chegar juntos à rede: {chegouFrente:F2} s o da frente, {chegouFundo:F2} s quem bateu");
    }

    /// <summary>
    /// O parceiro IA de um humano segue a formação do HUMANO, não a própria: o humano parado no fundo segura o parceiro no
    /// fundo, mesmo depois de o parceiro dar um lob (que na dupla de IA leva os dois pra rede); quando o humano sobe, o
    /// parceiro sobe. Cena sem bola vindo (a IA do time do humano só forma). "Não subir" é não passar da linha da frente
    /// (5,5 m da rede, de onde a IA conta um golpe como batido da frente): o fundo da IA é a 6,5 m, e antes da formação o
    /// parceiro ia a 3,2 m, a rede.
    /// </summary>
    [Fact]
    public void O_parceiro_de_um_humano_parado_no_fundo_nao_sobe_sozinho_e_sobe_quando_o_humano_sobe()
    {
        var partida = Cena(Dificuldade.Medio, semente: 11, humanos: [true, false, false, false]);
        var (humano, parceiro) = (partida.Jogadores[0], partida.Jogadores[1]);
        var ia = partida.IAs[0];
        Posicionar(humano, 2.5f, 8.0f);
        Posicionar(parceiro, -2.5f, 8.0f);
        ia.AoGolpear(partida, Lob(0, -8.2f), parceiro);

        float menorProfundidade = float.PositiveInfinity;
        for (int i = 0; i < 180; i++)   // 1,5 s com o humano parado
        {
            ia.Reagir(Passo, partida);
            menorProfundidade = MathF.Min(menorProfundidade, MathF.Abs(parceiro.Y));
        }
        Assert.True(menorProfundidade >= 5.5f,
            $"com o humano parado no fundo (y = {humano.Y:F1}), o parceiro não devia subir sozinho: chegou a {menorProfundidade:F2} m da rede");

        float? humanoNaRede = null, parceiroNaRede = null;
        float t = 0;
        for (int i = 0; i < 360 && (parceiroNaRede is null || humanoNaRede is null); i++)
        {
            humano.IrPara(2.5f, 3.2f, Passo);
            ia.Reagir(Passo, partida);
            t += Passo;
            if (MathF.Abs(humano.Y) <= 3.5f) humanoNaRede ??= t;
            if (MathF.Abs(parceiro.Y) <= 4.5f) parceiroNaRede ??= t;
        }
        Assert.True(humanoNaRede is not null, "a cena devia levar o humano à rede");
        Assert.True(parceiroNaRede is not null, $"o humano subiu e o parceiro não: y do parceiro {parceiro.Y:F2}");
        Assert.True(parceiroNaRede <= humanoNaRede + 1.0f,
            $"o parceiro devia subir junto com o humano: humano na rede em {humanoNaRede:F2} s, parceiro em {parceiroNaRede:F2} s");
    }
}
