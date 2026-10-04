namespace Padel.Core.Tests;

/// <summary>
/// O engasgo dos solucionadores de golpe especial. Antes, a chiquita testava 42 alvos pelo Golpes.Calcular (~100 mil
/// sub-passos de bola, 3–7 ms) e os remates varriam até 1.870 lançamentos (até 280 mil sub-passos, ~10 ms): um passo de
/// 120 Hz tem 8,3 ms, e o host online roda a partida. Aqui fica travado:
/// <list type="bullet">
/// <item>a EQUIVALÊNCIA com a implementação antiga (copiada abaixo, em <see cref="Antes"/>): onde antes havia solução,
/// continua havendo, e a de agora vale pelos mesmos critérios (o mesmo "vale" e a mesma robustez às margens);</item>
/// <item>o TRABALHO, contado em sub-passos de bola simulados (<see cref="GolpesEspeciais.SubPassosSimulados"/>) — nada de
/// tempo de parede, que numa máquina carregada mente: o teto derruba o teste se alguém voltar à força bruta.</item>
/// </list>
/// </summary>
public class GolpesEspeciaisDesempenhoTests
{
    /// <summary>
    /// A meta é nenhuma chamada passar de 1 ms (um passo de 120 Hz pode ter duas: a IA tenta o por 3 e o por 4). A ~30 ns
    /// por sub-passo de bola, 25 mil sub-passos ≈ 0,75 ms. A força bruta de antes passava de 100 mil na chiquita, de 40 mil
    /// na contrapared e chegava a 280 mil no remate por 3 sem solução. A chiquita tem orçamento de 20 mil por construção;
    /// o conjunto tem posição que sem ele gastava 45 mil — tirar o orçamento derruba este teto.
    /// </summary>
    private const long TetoDeSubPassosPorChamada = 25_000;

    /// <summary>
    /// A chiquita nova é achada por outra busca (contínua): pode sair um pouco mais rápida que a antiga, não muito. Em 208
    /// mil posições dentro da quadra (as grades e aleatórias da segunda correção O4-8, inclusive a menos de 12 cm da rede)
    /// o pior caso é 1,048; antes chegava a 5,5 (primeira revisão) e a 8,6 (86,9 m/s contra 10,1, segunda) colada na rede.
    /// </summary>
    private const float ChiquitaNoMaximoMaisRapidaQueAntes = 1.05f;

    public readonly record struct Posicao(float X, float Y, float Z, int LadoDoAlvo, float Argumento)
    {
        public override string ToString() => $"({X}; {Y}; {Z}) lado {LadoDoAlvo} arg {Argumento}";
    }

    // ───────────── O conjunto fixo de posições ─────────────

    /// <summary>As de GolpesTests, as chamadas reais mais pesadas das partidas IA x IA, e uma grade nos dois lados.</summary>
    private static List<Posicao> PosicoesDaChiquita()
    {
        var p = new List<Posicao>
        {
            new(1.0f, 8.0f, 0.7f, -1, 0), new(-2.0f, 8.5f, 0.5f, -1, 0), new(2.5f, 7.0f, 0.9f, -1, 0), new(-1.0f, -8.0f, 0.7f, 1, 0),
            new(-1.3481025f, -8.132455f, 0.07553288f, 1, 2.4960592f), new(4.286303f, 9.446962f, 0.5074349f, -1, -2.5079615f),
            new(-1.9393849f, 8.071325f, 0.12043983f, -1, 2.4933486f),
        };
        foreach (int lado in new[] { 1, -1 })
        foreach (float x in new[] { -4.5f, -1.5f, 0.5f, 3f })
        foreach (float y in new[] { 5.7f, 7f, 8.5f, 9.8f })
        foreach (float z in new[] { 0.05f, 0.5f, 0.95f })
            p.Add(new Posicao(x * lado, y * lado, z, -lado, lado * (x > 0 ? -2.5f : 2.5f)));
        foreach (int lado in new[] { 1, -1 })
        foreach (float y in new[] { 1f, 3f })
        foreach (float z in new[] { 0.3f, 1.3f, 1.8f })
            p.Add(new Posicao(1f * lado, y * lado, z, -lado, 0));   // lob + frente perto da rede ou com a bola alta: o humano pode pedir
        // A revisão achou: colado na rede e com a bola alta, a chiquita que cruzava a rede a 1,25 m saía até 5,5x mais
        // rápida que a antiga (21,7 m/s contra 3,95: um remate, não uma chiquita).
        p.Add(new Posicao(-1.98f, -0.095f, 1.34f, 1, -0.63f));
        p.Add(new Posicao(4.4f, -0.7f, 1.4f, 1, -4.5f));
        // Colada na rede e no chão, a única chiquita é um balão quase vertical (vz ~11 m/s, 2,2 s no ar): o chute do Newton
        // tem de chegar lá, ou ela se perde.
        p.Add(new Posicao(-4.099434f, -0.06286351f, 0.22182277f, 1, 1.1594924f));
        p.Add(new Posicao(3.5889852f, 0.068017684f, 0.08369392f, -1, -1.6273835f));
        // A verificação da correção achou, a menos de 12 cm da rede (a bola que acabou de cruzar por cima, ou colada nela):
        // chiquita que a antiga achava e a nova perdia (o balão de 2,7 s; o arco de 1,1 s a 2 mm da rede, com 31 mil
        // sub-passos), e chiquita até 8,6x mais rápida (86,9 m/s contra 10,1). Os casos exatos e uma grade ali.
        p.Add(new Posicao(4.5f, 0.002f, 1.03f, -1, -4.5f));
        p.Add(new Posicao(2.7f, 0.02f, 0.5f, -1, -3.5f));
        p.Add(new Posicao(4.5f, 0.002f, 1.39f, -1, -4.5f));
        p.Add(new Posicao(-0.30705407f, 0.0035417243f, 0.92834586f, -1, 4.7633142f));
        p.Add(new Posicao(0f, 0.05f, 1.39f, -1, 4.5f));
        // Colada na rede e rente ao chão, sem chiquita (nem antes): a busca sem orçamento gastava 45 mil sub-passos pra
        // concluir isso. O orçamento corta — e o teto de trabalho pega quem tirar o orçamento.
        p.Add(new Posicao(2.7f, 0.02f, 0.12f, -1, 0));
        p.Add(new Posicao(-4.2f, 0.005f, 0.22f, -1, 2.2f));
        foreach (int lado in new[] { 1, -1 })
        foreach (float y in new[] { 0.002f, 0.005f, 0.01f, 0.02f, 0.04f, 0.08f, 0.12f })
        foreach (float z in new[] { 0.5f, 0.91f, 1.03f, 1.2f, 1.39f })
        foreach (var (x, alvoX) in new[] { (4.5f, -4.5f), (-0.3f, 4.76f), (2.7f, -3.5f) })
            p.Add(new Posicao(x * lado, y * lado, z, -lado, alvoX * lado));
        return p;
    }

    private static List<Posicao> PosicoesDaContrapared()
    {
        var p = new List<Posicao>
        {
            new(1.0f, 9.4f, 0.8f, -1, 0), new(-2.5f, 9.6f, 0.5f, -1, 0), new(3.0f, 9.2f, 1.3f, -1, 0), new(-1.0f, -9.5f, 0.7f, 1, 0),
            new(-0.9381269f, -9.047259f, 1.6478146f, 1, 0), new(-1.0693645f, 8.905787f, 1.1225421f, -1, 0),
        };
        foreach (int lado in new[] { 1, -1 })
        foreach (float x in new[] { -4.6f, -1f, 2f })
        foreach (float y in new[] { 8.9f, 9.4f, 9.9f })
        foreach (float z in new[] { 0.05f, 0.7f, 1.6f })
            p.Add(new Posicao(x * lado, y * lado, z, -lado, lado * 2.5f));
        foreach (int lado in new[] { 1, -1 })
        foreach (float z in new[] { 0.5f, 2.5f })
            p.Add(new Posicao(0, 6f * lado, z, -lado, 0));   // longe do vidro: o humano pode pedir, e pode não haver solução
        return p;
    }

    /// <summary>Argumento: alvoX (por 4) ou ladoDaSaida (por 3).</summary>
    private static List<Posicao> PosicoesDoRemate(bool por4)
    {
        var p = new List<Posicao>();
        if (por4)
        {
            p.AddRange([new(0.6f, 2.5f, 2.6f, -1, 0), new(-1.9f, 2.5f, 2.6f, -1, 0), new(3.1f, 2.0f, 2.5f, -1, 0), new(-0.6f, -2.5f, 2.6f, 1, 0),
                new(0f, 8f, 1.2f, -1, 0), new(0f, 4.5f, 2.6f, -1, 0),
                new(-0.72254574f, -4.5058784f, 2.6734807f, 1, -0.72254574f), new(2.1428816f, 2.5404427f, 2.2440965f, -1, 2.1428816f),
                new(1.9f, -3.5f, 3.3f, 1, 0),
                // O remate robusto é o de 40 m/s, o nível de cima: o juiz tem de aceitar a rapidez máxima (em float, a raiz
                // das componentes sai um ulp acima de 40).
                new(-2.7f, 3.3f, 2.7f, -1, 0), new(0.6f, 2.9f, 2.5f, -1, 0),
                // O pior trabalho em 150 mil posições aleatórias alcançáveis (z até 2,7): ~17 mil sub-passos.
                new(-1.1763335f, -3.1397095f, 2.6503935f, 1, 2.7676182f)]);
        }
        else
        {
            p.AddRange([new(1.5f, 1.5f, 2.6f, -1, -1), new(-1.5f, 1.5f, 2.6f, -1, 1), new(-1.5f, -1.5f, 2.6f, 1, 1),
                new(2.911588f, 2.360476f, 2.3159323f, -1, -1), new(-2.567122f, -4.4450564f, 2.681178f, 1, 1),
                new(0.7722496f, -3.5125897f, 2.469348f, 1, -1),
                new(4.5f, 2.0f, 2.5f, -1, -1), new(-4.5f, -2.0f, 2.5f, 1, 1), new(4.5f, 3.0f, 3.3f, -1, -1)]);
            // A revisão achou (grade fina): o único remate robusto da grade antiga num nível do meio (38 m/s a 40°), com o
            // nível de cima só "vale sem robustez" — a busca que supunha o robusto monotônico na rapidez o perdia.
            p.AddRange([new(2.4f, 2.2f, 2.3f, -1, -1), new(-2.4f, 2.2f, 2.3f, -1, 1), new(4.2f, 1.8f, 2.3f, -1, 1),
                new(1.2f, 2.7f, 2.6f, -1, -1), new(4.5f, 1.7f, 2.3f, -1, 1), new(2.4f, -2.2f, 2.3f, 1, -1)]);
            // E a posição alcançável pela IA (z > 2,2, saída cruzada) que simulava 28 mil sub-passos, acima do teto.
            p.Add(new(4.5f, 1.5f, 2.3f, -1, -1));
            // O pior trabalho em 150 mil posições aleatórias alcançáveis (z até 2,7): ~20 mil sub-passos.
            p.Add(new(2.789419f, -2.4492097f, 2.526694f, 1, 1));
            // Na quina (a reta chega à lateral 1,4 cm depois do fundo): lateral e fundo no mesmo sub-passo, a Bola decide
            // pela lateral — e o único remate robusto sai por ela. Um corte que confiava na ordem da reta o perdia.
            p.Add(new(-0.3f, 2.9f, 2.5f, -1, -1));
        }
        foreach (int lado in new[] { 1, -1 })
        foreach (float x in new[] { -3f, -1f, 0.6f, 3.1f })
        foreach (float y in new[] { 0.5f, 1.5f, 2.5f, 3.5f, 4.5f })
        foreach (float z in new[] { 2.2f, 2.6f, 3.0f })
        {
            if (por4) p.Add(new Posicao(x * lado, y * lado, z, -lado, x * lado));
            else p.Add(new Posicao(x * lado, y * lado, z, -lado, x >= 0 ? -1 : 1));
        }
        return p;
    }

    // ───────────── Equivalência ─────────────

    /// <summary>A antiga e a nova em cada posição do conjunto da chiquita (a antiga custa ~3 ms por chamada: uma vez só).</summary>
    private static readonly Lazy<List<(Posicao P, Golpe? Antes, Golpe? Agora)>> Chiquitas = new(() =>
        PosicoesDaChiquita().Select(p => (p, Antes.Chiquita(p.X, p.Y, p.Z, p.LadoDoAlvo, p.Argumento),
            GolpesEspeciais.Chiquita(p.X, p.Y, p.Z, p.LadoDoAlvo, p.Argumento))).ToList());

    [Fact]
    public void A_chiquita_acha_solucao_onde_a_antiga_achava_e_a_de_agora_vale_com_a_mesma_folga()
    {
        var falhas = new List<string>();
        foreach (var (p, antes, agora) in Chiquitas.Value)
        {
            if (ColadaNaRede(p)) continue;   // fora do domínio da chiquita (teste abaixo)
            if (antes is not null && agora is null) { falhas.Add($"{p}: antes tinha chiquita, agora não"); continue; }
            if (agora is not Golpe g) continue;
            if (Antes.PorQueNaoVale(Solucionador.Chiquita, p, g) is string motivo) { falhas.Add($"{p}: {motivo}"); continue; }
            if (g.Efeito.TopspinRpm <= 0) falhas.Add($"{p}: chiquita sem topspin");
        }
        Assert.True(falhas.Count == 0, string.Join("\n", falhas));
    }

    /// <summary>
    /// A menos de <see cref="GolpesEspeciais.DistanciaMinimaDaChiquitaDaRede"/> da rede não há chiquita: a raquete tocaria a
    /// rede (falta, na regra) e o "arco" que a busca antiga achava ali era degenerado (balões de 2,9 s, remates de 87 m/s).
    /// Três correções seguidas do solucionador brigaram com essa faixa — a decisão foi tirá-la do domínio, não a quarta.
    /// </summary>
    private static bool ColadaNaRede(Posicao p) => MathF.Abs(p.Y) < GolpesEspeciais.DistanciaMinimaDaChiquitaDaRede;

    [Fact]
    public void Com_a_bola_colada_na_rede_nao_ha_chiquita_e_o_humano_cai_no_lob()
    {
        // Os casos da última verificação (que a antiga achava e a nova perdia) e a grade colada na rede do conjunto.
        var coladas = new List<Posicao>
        {
            new(2.3f, 0.0005f, 1.3997f, -1, -2f),
            new(-2.9447238f, -0.012007457f, 0.6090169f, 1, 3.170454f),
            new(2.4961789f, 0.00030847083f, 0.8931588f, -1, -2.8761883f),
        };
        coladas.AddRange(PosicoesDaChiquita().Where(ColadaNaRede));
        Assert.True(coladas.Count > 50, "a grade colada na rede sumiu do conjunto");
        var comChiquita = coladas.Where(p => GolpesEspeciais.Chiquita(p.X, p.Y, p.Z, p.LadoDoAlvo, p.Argumento) is not null).ToList();
        Assert.True(comChiquita.Count == 0, $"{comChiquita.Count} posição(ões) colada(s) na rede com chiquita, ex.: {comChiquita.FirstOrDefault()}");
    }

    [Fact]
    public void A_chiquita_continua_a_mais_lenta_que_serve()
    {
        var razoes = new List<(float Razao, Posicao P, float Antes, float Agora)>();
        foreach (var (p, antes, agora) in Chiquitas.Value)
            if (!ColadaNaRede(p) && antes is Golpe a && agora is Golpe g)
                razoes.Add((Rapidez(Lancamento(g)) / Rapidez(Lancamento(a)), p, Rapidez(Lancamento(a)), Rapidez(Lancamento(g))));
        Assert.NotEmpty(razoes);
        razoes.Sort((a, b) => a.Razao.CompareTo(b.Razao));
        var (mediana, maxima) = (razoes[razoes.Count / 2].Razao, razoes[^1]);
        Assert.True(mediana <= 1.0f && maxima.Razao <= ChiquitaNoMaximoMaisRapidaQueAntes,
            $"a chiquita é a mais lenta que serve: rapidez agora/antes com mediana {mediana:F3} e máxima {maxima.Razao:F3} " +
            $"em {maxima.P} ({maxima.Antes:F2} → {maxima.Agora:F2} m/s)");
    }

    [Fact]
    public void A_contrapared_acha_solucao_onde_a_antiga_achava_e_devolve_o_mesmo_golpe()
    {
        var falhas = new List<string>();
        foreach (var p in PosicoesDaContrapared())
        {
            var antes = Antes.Contrapared(p.X, p.Y, p.Z, p.LadoDoAlvo, p.Argumento);
            var agora = GolpesEspeciais.Contrapared(p.X, p.Y, p.Z, p.LadoDoAlvo, p.Argumento);
            Conferir(Solucionador.Contrapared, p, antes, agora, falhas);
        }
        Assert.True(falhas.Count == 0, string.Join("\n", falhas));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void O_remate_por_3_e_por_4_acha_solucao_onde_o_antigo_achava_e_devolve_o_mesmo_golpe(bool por4)
    {
        var falhas = new List<string>();
        int comSolucao = 0;
        foreach (var p in PosicoesDoRemate(por4))
        {
            var antes = por4 ? Antes.SmashPor4(p.X, p.Y, p.Z, p.LadoDoAlvo, p.Argumento) : Antes.SmashPor3(p.X, p.Y, p.Z, p.LadoDoAlvo, (int)p.Argumento);
            var agora = por4 ? GolpesEspeciais.SmashPor4(p.X, p.Y, p.Z, p.LadoDoAlvo, p.Argumento) : GolpesEspeciais.SmashPor3(p.X, p.Y, p.Z, p.LadoDoAlvo, (int)p.Argumento);
            if (antes is not null) comSolucao++;
            Conferir(por4 ? Solucionador.SmashPor4 : Solucionador.SmashPor3, p, antes, agora, falhas);
        }
        Assert.True(falhas.Count == 0, string.Join("\n", falhas));
        Assert.True(comSolucao >= 20, $"o conjunto devia ter remates possíveis: {comSolucao}");
    }

    /// <summary>Existência preservada, validade pelos critérios de antes e — contrapared e remates — o mesmo golpe.</summary>
    private static void Conferir(Solucionador qual, Posicao p, Golpe? antes, Golpe? agora, List<string> falhas)
    {
        if (antes is not null && agora is null) { falhas.Add($"{qual} {p}: antes tinha solução, agora não"); return; }
        if (agora is Golpe g && Antes.PorQueNaoVale(qual, p, g) is string motivo) { falhas.Add($"{qual} {p}: {motivo}"); return; }
        if (antes != agora) falhas.Add($"{qual} {p}: golpe diferente do de antes — antes {antes}, agora {agora}");
    }

    // ───────────── Trabalho (determinístico) ─────────────

    [Fact]
    public void Nenhuma_chamada_simula_mais_que_o_teto_de_sub_passos()
    {
        var chamadas = new List<(Solucionador Qual, Posicao P)>();
        chamadas.AddRange(PosicoesDaChiquita().Select(p => (Solucionador.Chiquita, p)));
        chamadas.AddRange(PosicoesDaContrapared().Select(p => (Solucionador.Contrapared, p)));
        chamadas.AddRange(PosicoesDoRemate(por4: true).Select(p => (Solucionador.SmashPor4, p)));
        chamadas.AddRange(PosicoesDoRemate(por4: false).Select(p => (Solucionador.SmashPor3, p)));
        var piores = new Dictionary<Solucionador, (long SubPassos, Posicao P)>();
        foreach (var (qual, p) in chamadas)
        {
            long antes = GolpesEspeciais.SubPassosSimulados;
            var golpe = Chamar(qual, p);
            long trabalho = GolpesEspeciais.SubPassosSimulados - antes;
            // Sem solução pode não haver simulação nenhuma (os cortes exatos do remate descartam a posição de cara); com
            // solução, houve: a que foi devolvida saiu de uma simulação.
            Assert.True(trabalho > 0 || golpe is null, $"{qual} {p}: achou golpe e o contador não andou — o trabalho não está sendo contado");
            if (!piores.TryGetValue(qual, out var pior) || trabalho > pior.SubPassos) piores[qual] = (trabalho, p);
        }
        string resumo = string.Join("; ", piores.OrderBy(k => k.Key).Select(k => $"{k.Key}: {k.Value.SubPassos} em {k.Value.P}"));
        Assert.True(piores.Values.All(v => v.SubPassos <= TetoDeSubPassosPorChamada),
            $"pior caso por solucionador acima do teto de {TetoDeSubPassosPorChamada} sub-passos: {resumo}");
    }

    [Fact]
    public void O_contador_de_trabalho_e_por_thread_e_so_anda_com_os_solucionadores()
    {
        long antes = GolpesEspeciais.SubPassosSimulados;
        var bola = new Bola();
        bola.Posicionar(0, 8, 1);
        bola.Lancar(new Velocidade(0, -10, 3, 0));
        bola.Avancar(1, new List<EventoDaBola>());   // bola solta não é trabalho de solucionador
        Assert.Equal(antes, GolpesEspeciais.SubPassosSimulados);

        long outraThread = -1;
        var t = new Thread(() =>
        {
            long a = GolpesEspeciais.SubPassosSimulados;
            GolpesEspeciais.Chiquita(1.0f, 8.0f, 0.7f, -1, 0);
            outraThread = GolpesEspeciais.SubPassosSimulados - a;
        });
        t.Start();
        t.Join();
        Assert.True(outraThread > 0);
        Assert.Equal(antes, GolpesEspeciais.SubPassosSimulados);   // o trabalho da outra thread não vaza pra esta
    }

    /// <summary>
    /// O teto só derruba a volta da força bruta se o contador vir TODA bola simulada na chamada. A chiquita antiga gastava
    /// ~80 % do trabalho dentro do Golpes.Calcular, fora do Simular dos solucionadores: a revisão trocou a chiquita nova
    /// pela antiga e o teto não viu. Por isso a conta é da Bola (cada sub-passo de qualquer bola da thread) e o contador
    /// dos solucionadores é o que ela andou durante a chamada — não há simulação que escape dele.
    /// </summary>
    [Fact]
    public void O_contador_de_trabalho_ve_toda_bola_simulada_inclusive_a_do_Golpes_Calcular()
    {
        long antes = Bola.SubPassosNestaThread;
        var bola = new Bola();
        bola.Posicionar(0, 8, 1);
        bola.Lancar(new Velocidade(0, -10, 3, 0));
        for (int i = 0; i < 10; i++) bola.Avancar(Bola.PassoMaximo, new List<EventoDaBola>());
        Assert.Equal(10, Bola.SubPassosNestaThread - antes);

        antes = Bola.SubPassosNestaThread;
        Golpes.Calcular(0, 8, 1, 1, -5, 0.9f);
        Assert.True(Bola.SubPassosNestaThread - antes > 0, "o Golpes.Calcular simula a bola: isso tem de contar");

        foreach (var (qual, p) in new[] { (Solucionador.Chiquita, new Posicao(1.0f, 8.0f, 0.7f, -1, 0)), (Solucionador.Contrapared, new Posicao(1.0f, 9.4f, 0.8f, -1, 0)),
                     (Solucionador.SmashPor3, new Posicao(1.5f, 1.5f, 2.6f, -1, -1)), (Solucionador.SmashPor4, new Posicao(0.6f, 2.5f, 2.6f, -1, 0)) })
        {
            long bolaAntes = Bola.SubPassosNestaThread, solucionadoresAntes = GolpesEspeciais.SubPassosSimulados;
            Chamar(qual, p);
            Assert.Equal(Bola.SubPassosNestaThread - bolaAntes, GolpesEspeciais.SubPassosSimulados - solucionadoresAntes);
        }
    }

    [Fact]
    public void O_modo_de_coleta_anota_cada_chamada_com_os_argumentos_o_resultado_e_o_trabalho()
    {
        var coleta = new List<ChamadaDeSolucionador>();
        GolpesEspeciais.Coleta = coleta;
        try
        {
            var chiquita = GolpesEspeciais.Chiquita(1.0f, 8.0f, 0.7f, -1, 0.5f);
            var por3 = GolpesEspeciais.SmashPor3(1.5f, 1.5f, 2.6f, -1, -1);
            Assert.Equal(2, coleta.Count);
            Assert.Equal(new ChamadaDeSolucionador(Solucionador.Chiquita, 1.0f, 8.0f, 0.7f, -1, 0.5f, chiquita, coleta[0].SubPassos), coleta[0]);
            Assert.Equal(new ChamadaDeSolucionador(Solucionador.SmashPor3, 1.5f, 1.5f, 2.6f, -1, -1, por3, coleta[1].SubPassos), coleta[1]);
            Assert.All(coleta, c => Assert.True(c.SubPassos > 0));
        }
        finally
        {
            GolpesEspeciais.Coleta = null;
        }
        GolpesEspeciais.Contrapared(1.0f, 9.4f, 0.8f, -1, 0);
        Assert.Equal(2, coleta.Count);   // desligada, não anota
    }

    private static Golpe? Chamar(Solucionador qual, Posicao p) => qual switch
    {
        Solucionador.Chiquita => GolpesEspeciais.Chiquita(p.X, p.Y, p.Z, p.LadoDoAlvo, p.Argumento),
        Solucionador.Contrapared => GolpesEspeciais.Contrapared(p.X, p.Y, p.Z, p.LadoDoAlvo, p.Argumento),
        Solucionador.SmashPor3 => GolpesEspeciais.SmashPor3(p.X, p.Y, p.Z, p.LadoDoAlvo, (int)p.Argumento),
        _ => GolpesEspeciais.SmashPor4(p.X, p.Y, p.Z, p.LadoDoAlvo, p.Argumento),
    };

    private static Velocidade Lancamento(Golpe g) => g.Lancamento ?? throw new Xunit.Sdk.XunitException($"golpe especial sem Lancamento: {g}");
    private static float Rapidez(Velocidade v) => MathF.Sqrt(v.Vx * v.Vx + v.Vy * v.Vy + v.Vz * v.Vz);

    /// <summary>
    /// A implementação de ANTES (commit cb4a846), copiada: a referência da equivalência e o juiz de validade — o mesmo "vale"
    /// e a mesma robustez de sempre, sem depender do código novo.
    /// </summary>
    private static class Antes
    {
        private const float Passo = 1f / 120f;
        private const float PassoGrosso = 1f / 60f;

        private readonly record struct Marca(EventoDaBola Evento, float T);

        private static List<Marca> Simular(float x0, float y0, float z0, Velocidade v, float segundos, int maximoDeEventos, float passo = Passo)
        {
            var bola = new Bola();
            bola.Posicionar(x0, y0, z0);
            bola.Lancar(v);
            var eventos = new List<EventoDaBola>(4);
            var marcas = new List<Marca>(maximoDeEventos + 2);
            for (float t = 0; t < segundos && bola.EmJogo && !bola.Parada && marcas.Count < maximoDeEventos;)
            {
                eventos.Clear();
                bola.Avancar(passo, eventos);
                t += passo;
                foreach (var e in eventos) marcas.Add(new Marca(e, t));
            }
            return marcas;
        }

        private static Velocidade Escalar(Velocidade v, float f) => v with { Vx = v.Vx * f, Vy = v.Vy * f, Vz = v.Vz * f };

        private readonly record struct Margem(float Rapidez, float Vz)
        {
            public static readonly Margem DePotencia = new(0.03f, 0.4f);
            public static readonly Margem DeToque = new(0.02f, 0.2f);
        }

        private static bool Robusta(float x0, float y0, float z0, Velocidade v, float segundos, int maximoDeEventos, Margem margem, Func<List<Marca>, bool> vale)
        {
            return vale(Simular(x0, y0, z0, v, segundos, maximoDeEventos, PassoGrosso))
                && vale(Simular(x0, y0, z0, Escalar(v, 1 - margem.Rapidez), segundos, maximoDeEventos))
                && vale(Simular(x0, y0, z0, Escalar(v, 1 + margem.Rapidez), segundos, maximoDeEventos))
                && vale(Simular(x0, y0, z0, v with { Vz = v.Vz - margem.Vz }, segundos, maximoDeEventos))
                && vale(Simular(x0, y0, z0, v with { Vz = v.Vz + margem.Vz }, segundos, maximoDeEventos));
        }

        private static Func<List<Marca>, bool> ValeDaChiquita(int ladoDoAlvo) => m =>
            m.Count >= 2
            && m[0].Evento.Tipo == TipoDeEventoDaBola.CruzouRede && m[0].Evento.Para == ladoDoAlvo
            && m[0].Evento.Z <= Quadra.AlturaDaRede + GolpesEspeciais.FolgaDaChiquitaSobreARede
            && m[1].Evento.Tipo == TipoDeEventoDaBola.Quique && m[1].Evento.Lado == ladoDoAlvo
            && MathF.Abs(m[1].Evento.Y) <= GolpesEspeciais.AlcanceDaChiquita;

        private static Func<List<Marca>, bool> ValeDaContrapared(int ladoDoAlvo) => m =>
            m.Count >= 3
            && m[0].Evento.Tipo == TipoDeEventoDaBola.Parede && m[0].Evento.Parede == QualParede.Fundo && m[0].Evento.Lado == -ladoDoAlvo
            && m[1].Evento.Tipo == TipoDeEventoDaBola.CruzouRede && m[1].Evento.Para == ladoDoAlvo
            && m[2].Evento.Tipo == TipoDeEventoDaBola.Quique && m[2].Evento.Lado == ladoDoAlvo;

        private static Func<EventoDaBola, bool> SaidaPor4(int ladoDoAlvo) =>
            saiu => Quadra.LadoDe(saiu.Y) == ladoDoAlvo && MathF.Abs(saiu.Y) >= Quadra.MeioComprimento && MathF.Abs(saiu.X) < Quadra.MeiaLargura;

        private static Func<EventoDaBola, bool> SaidaPor3(int ladoDaSaida)
        {
            int sinal = ladoDaSaida >= 0 ? 1 : -1;
            return saiu => saiu.X * sinal >= Quadra.MeiaLargura;
        }

        private static Func<List<Marca>, bool> ValeDoRemate(int ladoDoAlvo, Func<EventoDaBola, bool> saidaCerta) => m =>
            m.Count >= 3
            && m[0].Evento.Tipo == TipoDeEventoDaBola.CruzouRede && m[0].Evento.Para == ladoDoAlvo
            && m[1].Evento.Tipo == TipoDeEventoDaBola.Quique && m[1].Evento.Lado == ladoDoAlvo
            && m[2].Evento.Tipo == TipoDeEventoDaBola.Saiu && saidaCerta(m[2].Evento);

        /// <summary>
        /// O juiz: o golpe que um solucionador devolveu vale pelos critérios de antes? Null se vale; senão, o porquê. Confere o
        /// tipo, o lançamento (limites de rapidez), o "vale" da simulação, a robustez às margens e que AlvoX/AlvoY/TempoDeVoo
        /// são o quique da própria simulação.
        /// </summary>
        public static string? PorQueNaoVale(Solucionador qual, Posicao p, Golpe g)
        {
            if (g.Lancamento is not Velocidade v) return "sem Lancamento";
            var (tipo, segundos, eventos, margem, vale, indiceDoQuique) = qual switch
            {
                Solucionador.Chiquita => (TipoDeGolpe.Chiquita, 3f, 2, Margem.DeToque, ValeDaChiquita(p.LadoDoAlvo), 1),
                Solucionador.Contrapared => (TipoDeGolpe.Contrapared, 4f, 3, Margem.DePotencia, ValeDaContrapared(p.LadoDoAlvo), 2),
                Solucionador.SmashPor4 => (TipoDeGolpe.SmashPor4, 2.5f, 3, Margem.DePotencia, ValeDoRemate(p.LadoDoAlvo, SaidaPor4(p.LadoDoAlvo)), 1),
                _ => (TipoDeGolpe.SmashPor3, 2.5f, 3, Margem.DePotencia, ValeDoRemate(p.LadoDoAlvo, SaidaPor3((int)p.Argumento)), 1),
            };
            if (g.Tipo != tipo) return $"tipo {g.Tipo}, esperado {tipo}";
            // A rapidez vem das componentes em float: no limite exato (o remate de 40 m/s) a raiz sai um ulp acima. 1 mm/s de folga.
            float rapidez = MathF.Sqrt(v.Vx * v.Vx + v.Vy * v.Vy + v.Vz * v.Vz);
            if (qual == Solucionador.Contrapared && rapidez > GolpesEspeciais.RapidezMaximaDaContrapared + 1e-3f) return $"contrapared a {rapidez:F1} m/s";
            if (qual is Solucionador.SmashPor3 or Solucionador.SmashPor4 && rapidez > GolpesEspeciais.RapidezMaximaDoRemate + 1e-3f) return $"remate a {rapidez:F1} m/s";
            var m = Simular(p.X, p.Y, p.Z, v, segundos, eventos);
            if (!vale(m)) return "a simulação do lançamento não vale";
            if (!Robusta(p.X, p.Y, p.Z, v, segundos, eventos, margem, vale)) return "não aguenta as margens (robustez)";
            var quique = m[indiceDoQuique];
            if (g.AlvoX != quique.Evento.X || g.AlvoY != quique.Evento.Y || g.TempoDeVoo != quique.T)
                return $"AlvoX/AlvoY/TempoDeVoo ({g.AlvoX}; {g.AlvoY}; {g.TempoDeVoo}) não são o quique simulado ({quique.Evento.X}; {quique.Evento.Y}; {quique.T})";
            return null;
        }

        private static float Rapidez(Velocidade v) => MathF.Sqrt(v.Vx * v.Vx + v.Vy * v.Vy + v.Vz * v.Vz);

        public static Golpe? Chiquita(float x0, float y0, float z0, int ladoDoAlvo, float alvoX)
        {
            alvoX = Util.Limitar(alvoX, -Quadra.MeiaLargura + 0.5f, Quadra.MeiaLargura - 0.5f);
            var vale = ValeDaChiquita(ladoDoAlvo);
            Golpe? melhor = null;
            float menorRapidez = float.PositiveInfinity;
            foreach (float distancia in new[] { 3.0f, 2.5f, 2.0f })
            foreach (float tempo in new[] { 0.6f, 0.7f, 0.8f, 0.9f, 1.0f, 1.1f, 1.2f })
            foreach (float topspin in new[] { 800f, 1500f })
            {
                var efeito = new Efeito(topspin, 0);
                var v = Golpes.Calcular(x0, y0, z0, alvoX, ladoDoAlvo * distancia, tempo, efeito: efeito);
                float rapidez = Rapidez(v);
                if (rapidez >= menorRapidez) continue;
                var m = Simular(x0, y0, z0, v, 3, 2);
                if (!vale(m) || !Robusta(x0, y0, z0, v, 3, 2, Margem.DeToque, vale)) continue;
                menorRapidez = rapidez;
                var quique = m[1];
                melhor = new Golpe(quique.Evento.X, quique.Evento.Y, quique.T, TipoDeGolpe.Chiquita, Efeito: efeito, Lancamento: v);
            }
            return melhor;
        }

        public static Golpe? Contrapared(float x0, float y0, float z0, int ladoDoAlvo, float alvoX)
        {
            int meuLado = -ladoDoAlvo;
            var vale = ValeDaContrapared(ladoDoAlvo);
            float vx = Util.Limitar(alvoX, -Quadra.MeiaLargura + 0.5f, Quadra.MeiaLargura - 0.5f) - x0;
            vx /= 1.3f;
            Golpe? melhor = null;
            float melhorNota = float.PositiveInfinity;
            for (float vy = 6; vy <= 20; vy += 2)
            for (float vz = 4; vz <= 13; vz += 1)
            {
                var v = new Velocidade(vx, meuLado * vy, vz, 0);
                float rapidez = Rapidez(v);
                if (rapidez > GolpesEspeciais.RapidezMaximaDaContrapared) continue;
                var m = Simular(x0, y0, z0, v, 4, 3);
                if (!vale(m)) continue;
                var quique = m[2];
                float nota = MathF.Abs(MathF.Abs(quique.Evento.Y) - 5f) + 0.05f * rapidez;
                if (nota >= melhorNota || !Robusta(x0, y0, z0, v, 4, 3, Margem.DePotencia, vale)) continue;
                melhorNota = nota;
                melhor = new Golpe(quique.Evento.X, quique.Evento.Y, quique.T, TipoDeGolpe.Contrapared, Lancamento: v);
            }
            return melhor;
        }

        public static Golpe? SmashPor4(float x0, float y0, float z0, int ladoDoAlvo, float alvoX)
        {
            float distanciaAoFundo = MathF.Max(1f, Quadra.MeioComprimento + MathF.Abs(y0));
            float preferido = MathF.Atan2(alvoX - x0, distanciaAoFundo);
            var azimutes = new List<float>();
            foreach (float graus in new[] { 0f, -8f, 8f, -16f, 16f, -24f, 24f }) azimutes.Add(preferido + graus * MathF.PI / 180f);
            return Remate(x0, y0, z0, ladoDoAlvo, TipoDeGolpe.SmashPor4, azimutes, SaidaPor4(ladoDoAlvo));
        }

        public static Golpe? SmashPor3(float x0, float y0, float z0, int ladoDoAlvo, int ladoDaSaida)
        {
            int sinal = ladoDaSaida >= 0 ? 1 : -1;
            var azimutes = new List<float>();
            for (float graus = 10; graus <= 60; graus += 5) azimutes.Add(sinal * graus * MathF.PI / 180f);
            return Remate(x0, y0, z0, ladoDoAlvo, TipoDeGolpe.SmashPor3, azimutes, SaidaPor3(ladoDaSaida));
        }

        private static Golpe? Remate(float x0, float y0, float z0, int ladoDoAlvo, TipoDeGolpe tipo, List<float> azimutes, Func<EventoDaBola, bool> saidaCerta)
        {
            var vale = ValeDoRemate(ladoDoAlvo, saidaCerta);
            for (float rapidez = 22; rapidez <= GolpesEspeciais.RapidezMaximaDoRemate; rapidez += 2)
            {
                foreach (float azimute in azimutes)
                for (float graus = -8; graus >= -40; graus -= 2)
                {
                    float elevacao = graus * MathF.PI / 180f;
                    float horizontal = rapidez * MathF.Cos(elevacao);
                    var v = new Velocidade(horizontal * MathF.Sin(azimute), ladoDoAlvo * horizontal * MathF.Cos(azimute), rapidez * MathF.Sin(elevacao), 0);
                    var m = Simular(x0, y0, z0, v, 2.5f, 3);
                    if (!vale(m) || !Robusta(x0, y0, z0, v, 2.5f, 3, Margem.DePotencia, vale)) continue;
                    var quique = m[1];
                    return new Golpe(quique.Evento.X, quique.Evento.Y, quique.T, tipo, Lancamento: v);
                }
            }
            return null;
        }
    }
}
