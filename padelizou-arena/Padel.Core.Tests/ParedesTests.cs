namespace Padel.Core.Tests;

/// <summary>
/// As paredes como na regra (FIP, simplificada): fundo de vidro até 3 m e grade até 4 m; lateral com o canto igual ao
/// fundo, o degrau (vidro até 2 m, grade até 3 m), o meio de grade até 3 m e as quatro portas. O vidro devolve, a grade
/// mata a bola e é irregular — mas determinística: o mesmo ponto de contato dá o mesmo rebote em qualquer máquina.
/// </summary>
public class ParedesTests
{
    private const float Rapidez = 10f;
    private const float DistanciaDaParede = 1f;

    /// <summary>
    /// Bola sem efeito lançada reto contra a parede (perpendicular a ela), de 1 m de distância, a 10 m/s, subindo só o
    /// bastante pra chegar ao ponto (aoLongo, z) no alto da parábola: no contato a velocidade é quase toda normal.
    /// </summary>
    private static Bola LancarContra(QualParede parede, int sinal, float aoLongo, float z)
    {
        float t = DistanciaDaParede / Rapidez;
        float z0 = z - 0.5f * Bola.G * t * t, vz = Bola.G * t;
        var b = new Bola();
        if (parede == QualParede.Lateral)
        {
            b.Posicionar(sinal * (Quadra.MeiaLargura - DistanciaDaParede), aoLongo, z0);
            b.Lancar(new Velocidade(sinal * Rapidez, 0, vz, 0));
        }
        else
        {
            b.Posicionar(aoLongo, sinal * (Quadra.MeioComprimento - DistanciaDaParede), z0);
            b.Lancar(new Velocidade(0, sinal * Rapidez, vz, 0));
        }
        return b;
    }

    /// <summary>O primeiro contato com parede (Parede ou Saiu), com a rapidez logo antes e logo depois do sub-passo em que aconteceu.</summary>
    private readonly record struct Contato(EventoDaBola Evento, float RapidezAntes, float RapidezDepois, float Vx, float Vy, float Vz);

    private static Contato PrimeiroContato(Bola b)
    {
        var eventos = new List<EventoDaBola>();
        for (int i = 0; i < 480 && b.EmJogo && !b.Parada; i++)
        {
            float antes = b.Rapidez;
            b.Avancar(Bola.PassoMaximo, eventos);
            foreach (var e in eventos)
                if (e.Tipo is TipoDeEventoDaBola.Parede or TipoDeEventoDaBola.Saiu)
                    return new Contato(e, antes, b.Rapidez, b.Vx, b.Vy, b.Vz);
            eventos.Clear();
        }
        throw new Xunit.Sdk.XunitException("a bola não chegou à parede");
    }

    private static List<EventoDaBola> Voo(float x, float y, float z, float vx, float vy, float vz, float segundos = 3)
    {
        var b = new Bola();
        b.Posicionar(x, y, z);
        b.Lancar(new Velocidade(vx, vy, vz, 0));
        var eventos = new List<EventoDaBola>();
        for (int i = 0; i < segundos * 240 && b.EmJogo && !b.Parada; i++) b.Avancar(Bola.PassoMaximo, eventos);
        return eventos;
    }

    [Theory]
    // Fundo (|y| = 10): vidro até 3 m, grade de 3 a 4 m, acima disso sai.
    [InlineData(QualParede.Fundo, 1, 2.0f, 1.5f, TipoDeEventoDaBola.Parede, Superficie.Vidro, false)]
    [InlineData(QualParede.Fundo, -1, -3.0f, 3.5f, TipoDeEventoDaBola.Parede, Superficie.Grade, false)]
    [InlineData(QualParede.Fundo, 1, 0.5f, 4.3f, TipoDeEventoDaBola.Saiu, Superficie.Aberto, false)]
    // Lateral no canto (8 ≤ |y| ≤ 10): igual ao fundo.
    [InlineData(QualParede.Lateral, 1, 9.0f, 2.5f, TipoDeEventoDaBola.Parede, Superficie.Vidro, false)]
    [InlineData(QualParede.Lateral, -1, -9.0f, 3.5f, TipoDeEventoDaBola.Parede, Superficie.Grade, false)]
    [InlineData(QualParede.Lateral, 1, -9.0f, 4.3f, TipoDeEventoDaBola.Saiu, Superficie.Aberto, false)]
    // Lateral no degrau (6 ≤ |y| < 8): vidro até 2 m, grade de 2 a 3 m, acima disso sai.
    [InlineData(QualParede.Lateral, -1, 7.0f, 1.5f, TipoDeEventoDaBola.Parede, Superficie.Vidro, false)]
    [InlineData(QualParede.Lateral, 1, -7.0f, 2.5f, TipoDeEventoDaBola.Parede, Superficie.Grade, false)]
    [InlineData(QualParede.Lateral, -1, 6.5f, 3.5f, TipoDeEventoDaBola.Saiu, Superficie.Aberto, false)]
    // Lateral no meio (|y| < 6): grade até 3 m, acima disso sai.
    [InlineData(QualParede.Lateral, 1, 3.0f, 1.0f, TipoDeEventoDaBola.Parede, Superficie.Grade, false)]
    [InlineData(QualParede.Lateral, -1, -4.0f, 2.5f, TipoDeEventoDaBola.Parede, Superficie.Grade, false)]
    [InlineData(QualParede.Lateral, 1, 0.2f, 1.0f, TipoDeEventoDaBola.Parede, Superficie.Grade, false)]   // entre as duas portas, junto ao poste
    [InlineData(QualParede.Lateral, -1, 2.0f, 3.5f, TipoDeEventoDaBola.Saiu, Superficie.Aberto, false)]
    // Portas (0,45 ≤ |y| ≤ 1,25, do chão até 2 m): a bola sai por elas; acima da porta é grade; acima de 3 m sai por cima.
    [InlineData(QualParede.Lateral, 1, 0.85f, 1.0f, TipoDeEventoDaBola.Saiu, Superficie.Aberto, true)]
    [InlineData(QualParede.Lateral, -1, -0.85f, 0.5f, TipoDeEventoDaBola.Saiu, Superficie.Aberto, true)]
    [InlineData(QualParede.Lateral, 1, -1.1f, 1.7f, TipoDeEventoDaBola.Saiu, Superficie.Aberto, true)]
    [InlineData(QualParede.Lateral, -1, 1.0f, 2.5f, TipoDeEventoDaBola.Parede, Superficie.Grade, false)]
    [InlineData(QualParede.Lateral, 1, 0.85f, 3.5f, TipoDeEventoDaBola.Saiu, Superficie.Aberto, false)]
    public void Cada_trecho_da_parede_da_a_superficie_e_o_evento_certos(QualParede parede, int sinal, float aoLongo, float z, TipoDeEventoDaBola tipo, Superficie superficie, bool pelaPorta)
    {
        var b = LancarContra(parede, sinal, aoLongo, z);
        var e = PrimeiroContato(b).Evento;
        Assert.Equal(tipo, e.Tipo);
        Assert.Equal(parede, e.Parede);
        Assert.Equal(superficie, e.Superficie);
        Assert.Equal(pelaPorta, e.PelaPorta);
        Assert.Equal(tipo == TipoDeEventoDaBola.Parede, b.EmJogo);
        Assert.Equal(Quadra.LadoDe(parede == QualParede.Lateral ? aoLongo : sinal * Quadra.MeioComprimento), e.Lado);
    }

    [Theory]
    [InlineData(QualParede.Fundo, 0f, 3.0f, Superficie.Vidro)]      // o limite é do vidro
    [InlineData(QualParede.Fundo, 4.9f, 3.01f, Superficie.Grade)]
    [InlineData(QualParede.Fundo, -4.9f, 4.01f, Superficie.Aberto)]
    [InlineData(QualParede.Lateral, 8.0f, 2.9f, Superficie.Vidro)]   // |y| = 8 já é canto
    [InlineData(QualParede.Lateral, -9.5f, 3.9f, Superficie.Grade)]
    [InlineData(QualParede.Lateral, 10.0f, 4.01f, Superficie.Aberto)]
    [InlineData(QualParede.Lateral, 7.99f, 2.01f, Superficie.Grade)]  // degrau
    [InlineData(QualParede.Lateral, -6.0f, 1.9f, Superficie.Vidro)]   // |y| = 6 já é degrau
    [InlineData(QualParede.Lateral, 6.5f, 3.01f, Superficie.Aberto)]
    [InlineData(QualParede.Lateral, 5.99f, 0.1f, Superficie.Grade)]   // meio
    [InlineData(QualParede.Lateral, -3.0f, 3.0f, Superficie.Grade)]
    [InlineData(QualParede.Lateral, 3.0f, 3.01f, Superficie.Aberto)]
    [InlineData(QualParede.Lateral, 0.85f, 0.0f, Superficie.Aberto)]  // porta
    [InlineData(QualParede.Lateral, -1.2f, 1.99f, Superficie.Aberto)]
    [InlineData(QualParede.Lateral, 0.5f, 2.0f, Superficie.Grade)]    // acima da porta
    [InlineData(QualParede.Lateral, 0.4f, 1.0f, Superficie.Grade)]    // entre as portas
    [InlineData(QualParede.Lateral, -1.3f, 1.0f, Superficie.Grade)]   // depois da porta
    public void A_superficie_de_cada_ponto_da_parede_segue_a_regra(QualParede parede, float aoLongo, float z, Superficie esperada)
    {
        Assert.Equal(esperada, Quadra.SuperficieDaParede(parede, aoLongo, z));
    }

    [Fact]
    public void As_quatro_portas_deixam_a_bola_sair_e_ela_para_no_plano_da_parede()
    {
        foreach (int sinalX in new[] { 1, -1 })
        foreach (int sinalY in new[] { 1, -1 })
        {
            var b = LancarContra(QualParede.Lateral, sinalX, sinalY * 0.85f, 1.0f);
            var e = PrimeiroContato(b).Evento;
            Assert.Equal(TipoDeEventoDaBola.Saiu, e.Tipo);
            Assert.True(e.PelaPorta, $"porta ({sinalX}, {sinalY}): devia sair pela porta");
            Assert.Equal(Superficie.Aberto, e.Superficie);
            Assert.Equal(sinalY, e.Lado);
            Assert.False(b.EmJogo);
            // A bola para onde cruzou a parede: a simulação confere |x| ≤ 5,01 a cada quadro, inclusive depois de sair.
            Assert.Equal(sinalX * Quadra.MeiaLargura, e.X);
            Assert.True(MathF.Abs(b.X) <= Quadra.MeiaLargura, $"a bola ficou fora da quadra: x = {b.X}");
        }

        // Bola rasteira (rolando no chão) também sai pela porta.
        var rasteira = new Bola();
        rasteira.Posicionar(3.5f, -0.9f, 0);
        rasteira.Lancar(new Velocidade(6, 0, 0, 0));
        var saiu = PrimeiroContato(rasteira).Evento;
        Assert.Equal(TipoDeEventoDaBola.Saiu, saiu.Tipo);
        Assert.True(saiu.PelaPorta, "a bola rasteira devia sair pela porta");
    }

    [Fact]
    public void A_grade_devolve_menos_energia_que_o_vidro_na_media_de_varios_impactos()
    {
        float Energia(QualParede parede, int sinal, float aoLongo, float z, Superficie esperada)
        {
            var c = PrimeiroContato(LancarContra(parede, sinal, aoLongo, z));
            Assert.Equal(TipoDeEventoDaBola.Parede, c.Evento.Tipo);
            Assert.Equal(esperada, c.Evento.Superficie);
            return c.RapidezDepois * c.RapidezDepois / (c.RapidezAntes * c.RapidezAntes);
        }
        var vidro = new List<float>();
        var grade = new List<float>();
        for (int i = 0; i < 16; i++)
        {
            int sinal = i % 2 == 0 ? 1 : -1;
            int metade = i % 4 < 2 ? 1 : -1;
            vidro.Add(Energia(QualParede.Fundo, sinal, -4.5f + 0.6f * i, 0.5f + 0.15f * i, Superficie.Vidro));
            vidro.Add(Energia(QualParede.Lateral, sinal, metade * (8.1f + 0.1f * i), 0.5f + 0.15f * i, Superficie.Vidro));   // canto
            vidro.Add(Energia(QualParede.Lateral, sinal, metade * (6.1f + 0.1f * i), 0.3f + 0.1f * i, Superficie.Vidro));    // degrau
            grade.Add(Energia(QualParede.Fundo, sinal, -4.5f + 0.6f * i, 3.2f + 0.04f * i, Superficie.Grade));
            grade.Add(Energia(QualParede.Lateral, sinal, metade * (1.5f + 0.25f * i), 0.3f + 0.15f * i, Superficie.Grade));  // meio
            grade.Add(Energia(QualParede.Lateral, sinal, metade * (6.1f + 0.1f * i), 2.1f + 0.05f * i, Superficie.Grade));   // degrau
        }
        float mediaDoVidro = vidro.Average(), mediaDaGrade = grade.Average();
        Assert.True(mediaDaGrade < mediaDoVidro * 0.5f, $"a grade devia devolver bem menos energia: grade {mediaDaGrade:F3} x vidro {mediaDoVidro:F3}");
    }

    [Fact]
    public void A_grade_desvia_a_saida_ate_15_graus_e_restitui_entre_0_30_e_0_50_conforme_o_ponto()
    {
        var desvios = new List<float>();
        var restituicoes = new List<float>();
        for (int i = 0; i < 24; i++)
        {
            int sinal = i % 2 == 0 ? 1 : -1;
            float y = (i % 4 < 2 ? 1 : -1) * (1.6f + 0.17f * i);   // meio da lateral, longe das portas
            float z = 0.4f + 0.1f * i;
            var c = PrimeiroContato(LancarContra(QualParede.Lateral, sinal, y, z));
            Assert.Equal(Superficie.Grade, c.Evento.Superficie);
            // Lançada reto, sem efeito e no alto da parábola: sem a irregularidade, a bola voltaria reto pela normal (−sinal, 0, 0).
            float cosseno = -sinal * c.Vx / c.RapidezDepois;
            desvios.Add(MathF.Acos(Math.Clamp(cosseno, -1f, 1f)) * 180f / MathF.PI);
            restituicoes.Add(c.RapidezDepois / c.RapidezAntes);
        }
        Assert.All(desvios, d => Assert.InRange(d, 0f, 15f + 1f));             // 1° de folga: a bola chega quase, não exatamente, reta
        Assert.All(restituicoes, e => Assert.InRange(e, 0.30f - 0.01f, 0.50f + 0.01f));
        // Irregular de verdade: o ponto muda o rebote...
        Assert.True(desvios.Max() - desvios.Min() > 5f, $"desvios quase iguais: {desvios.Min():F1}° a {desvios.Max():F1}°");
        Assert.True(restituicoes.Max() - restituicoes.Min() > 0.08f, $"restituições quase iguais: {restituicoes.Min():F3} a {restituicoes.Max():F3}");
        // ... mas na média é a grade de sempre (restituição 0,40).
        Assert.InRange(restituicoes.Average(), 0.35f, 0.45f);
    }

    [Fact]
    public void A_grade_e_deterministica_o_mesmo_lancamento_da_o_mesmo_rebote_exato()
    {
        Bola Jogar()
        {
            var b = LancarContra(QualParede.Lateral, 1, 3.3f, 1.7f);
            b.Avancar(0.5f, new List<EventoDaBola>());
            return b;
        }
        var a = Jogar();
        var b = Jogar();
        Assert.Equal((a.X, a.Y, a.Z, a.Vx, a.Vy, a.Vz, a.Wx, a.Wy, a.Wz), (b.X, b.Y, b.Z, b.Vx, b.Vy, b.Vz, b.Wx, b.Wy, b.Wz));

        // A IA lê a bola por um clone: a previsão tem que ser exatamente o que a bola real faz.
        var real = LancarContra(QualParede.Fundo, -1, 1.3f, 3.4f);
        var previsao = real.Clonar();
        real.Avancar(0.5f, new List<EventoDaBola>());
        previsao.Avancar(0.5f, new List<EventoDaBola>());
        Assert.Equal((real.X, real.Y, real.Z, real.Vx, real.Vy, real.Vz), (previsao.X, previsao.Y, previsao.Z, previsao.Vx, previsao.Vy, previsao.Vz));
    }

    [Fact]
    public void A_grade_nunca_joga_a_bola_pra_dentro_dela_nem_de_raspao()
    {
        // Bolas quase paralelas à lateral (5° de ataque, 10 m/s): o desvio da grade (até 15°) é maior que o ângulo de saída
        // (~2°) e pode apontar pra parede; a bola tem que sair dela e não voltar a tocá-la logo em seguida.
        // Saem de 0,2 m da lateral, correm ~2,3 m rumo à rede e batem no meio da grade, com 1,6 ≤ |y| ≤ 3,2 — longe da
        // porta (|y| ≤ 1,25) e do degrau (|y| = 6).
        float angulo = 5f * MathF.PI / 180f;
        for (int i = 0; i < 40; i++)
        {
            int sinal = i % 2 == 0 ? 1 : -1;
            int metade = i % 4 < 2 ? 1 : -1;
            var b = new Bola();
            b.Posicionar(sinal * 4.8f, metade * (3.9f + 0.04f * i), 0.5f + 0.05f * i);
            b.Lancar(new Velocidade(sinal * 10 * MathF.Sin(angulo), -metade * 10 * MathF.Cos(angulo), 2f, 0));
            var eventos = new List<EventoDaBola>();
            for (int passo = 0; passo < 240 && !eventos.Any(e => e.Tipo is TipoDeEventoDaBola.Parede or TipoDeEventoDaBola.Saiu); passo++)
            {
                eventos.Clear();
                b.Avancar(Bola.PassoMaximo, eventos);
            }
            var contato = Assert.Single(eventos, e => e.Tipo is TipoDeEventoDaBola.Parede or TipoDeEventoDaBola.Saiu);
            Assert.Equal(TipoDeEventoDaBola.Parede, contato.Tipo);
            Assert.Equal(QualParede.Lateral, contato.Parede);
            Assert.Equal(Superficie.Grade, contato.Superficie);
            Assert.True(-sinal * b.Vx > 0, $"lançamento {i}: depois da grade a bola devia sair da parede, vx = {b.Vx}");
            // Nem de raspão: nos 0,1 s seguintes a bola não toca parede nenhuma.
            for (int passo = 0; passo < 24; passo++)
            {
                eventos.Clear();
                b.Avancar(Bola.PassoMaximo, eventos);
                Assert.DoesNotContain(eventos, e => e.Tipo is TipoDeEventoDaBola.Parede or TipoDeEventoDaBola.Saiu);
            }
        }
    }

    [Fact]
    public void Os_paineis_cobrem_cada_parede_sem_sobreposicao_e_sem_buraco_alem_das_portas()
    {
        // Área de cada parede pelas medidas da regra: fundo 10 m × 4 m; lateral: dois cantos de 2 m × 4 m, dois degraus de
        // 2 m × 3 m e o meio de 12 m × 3 m, menos as duas portas de 0,80 m × 2 m.
        var esperada = new Dictionary<(QualParede, int), float>
        {
            [(QualParede.Fundo, 1)] = 40f,
            [(QualParede.Fundo, -1)] = 40f,
            [(QualParede.Lateral, 1)] = 2 * 2 * 4 + 2 * 2 * 3 + 12 * 3 - 2 * 0.8f * 2,
            [(QualParede.Lateral, -1)] = 2 * 2 * 4 + 2 * 2 * 3 + 12 * 3 - 2 * 0.8f * 2,
        };
        var paineis = Quadra.Paineis;
        Assert.Equal(esperada.Keys.OrderBy(k => k), paineis.Select(p => (p.Parede, p.Sinal)).Distinct().OrderBy(k => k));
        foreach (var ((parede, sinal), area) in esperada)
        {
            var daParede = paineis.Where(p => p.Parede == parede && p.Sinal == sinal).ToList();
            float extensao = parede == QualParede.Lateral ? Quadra.MeioComprimento : Quadra.MeiaLargura;
            foreach (var p in daParede)
            {
                Assert.True(p.Inicio < p.Fim && p.ZMin < p.ZMax, $"painel vazio ou invertido: {p}");
                Assert.True(p.Inicio >= -extensao && p.Fim <= extensao && p.ZMin >= 0 && p.ZMax <= Quadra.AlturaDaParede, $"painel fora da parede: {p}");
                Assert.True(p.Superficie is Superficie.Vidro or Superficie.Grade, $"painel sem material: {p}");
            }
            for (int i = 0; i < daParede.Count; i++)
                for (int j = i + 1; j < daParede.Count; j++)
                {
                    var (a, b) = (daParede[i], daParede[j]);
                    float sobreposicao = MathF.Max(0, MathF.Min(a.Fim, b.Fim) - MathF.Max(a.Inicio, b.Inicio))
                                       * MathF.Max(0, MathF.Min(a.ZMax, b.ZMax) - MathF.Max(a.ZMin, b.ZMin));
                    Assert.True(sobreposicao < 1e-4f, $"painéis sobrepostos: {a} e {b}");
                }
            float soma = daParede.Sum(p => (p.Fim - p.Inicio) * (p.ZMax - p.ZMin));
            Assert.InRange(soma, area - 1e-3f, area + 1e-3f);
        }
    }

    [Fact]
    public void Os_paineis_sao_exatamente_o_que_a_fisica_usa()
    {
        // Amostra cada parede num gradeado de 5 cm (fora das emendas) e confere que painel e física concordam ponto a ponto.
        foreach (var parede in new[] { QualParede.Fundo, QualParede.Lateral })
        foreach (int sinal in new[] { 1, -1 })
        {
            float extensao = parede == QualParede.Lateral ? Quadra.MeioComprimento : Quadra.MeiaLargura;
            var daParede = Quadra.Paineis.Where(p => p.Parede == parede && p.Sinal == sinal).ToList();
            for (int i = 0; -extensao + 0.025f + i * 0.05f < extensao; i++)
            for (int k = 0; 0.025f + k * 0.05f < 4.5f; k++)
            {
                float a = -extensao + 0.025f + i * 0.05f, z = 0.025f + k * 0.05f;
                var fisica = Quadra.SuperficieDaParede(parede, a, z);
                var cobrem = daParede.Where(p => a > p.Inicio && a < p.Fim && z > p.ZMin && z < p.ZMax).ToList();
                if (fisica == Superficie.Aberto)
                    Assert.True(cobrem.Count == 0, $"{parede} {sinal} ({a:F3}, {z:F3}): aberto na física, mas coberto por {cobrem.Count} painel(éis)");
                else
                    Assert.True(cobrem.Count == 1 && cobrem[0].Superficie == fisica, $"{parede} {sinal} ({a:F3}, {z:F3}): física diz {fisica}, painéis: {string.Join(", ", cobrem)}");
            }
        }
    }

    [Fact]
    public void O_arbitro_da_o_ponto_a_quem_bateu_se_a_bola_quicou_do_outro_lado_e_saiu_pela_porta()
    {
        // O time 0 (lado +1) bate de perto da rede, curto e cruzado: a bola cruza, quica do lado -1 e escapa pela porta de x = +5.
        var eventos = Voo(1, 0.6f, 2.0f, 8, -3, -3);
        int quique = eventos.FindIndex(e => e.Tipo == TipoDeEventoDaBola.Quique);
        int saiu = eventos.FindIndex(e => e.Tipo == TipoDeEventoDaBola.Saiu);
        Assert.True(quique >= 0 && saiu > quique, $"a bola devia quicar e depois sair: {string.Join(" → ", eventos.Select(e => e.Tipo))}");
        Assert.Equal(-1, eventos[quique].Lado);
        Assert.True(eventos[saiu].PelaPorta, "devia sair pela porta");
        Assert.DoesNotContain(eventos.Take(saiu), e => e.Tipo == TipoDeEventoDaBola.Parede);

        var arbitro = new Arbitro();
        arbitro.RegistrarGolpe(0);
        Assert.Equal(Decisao.Ponto(0, Motivo.Fora), Arbitrar(arbitro, eventos));
    }

    [Fact]
    public void O_arbitro_da_o_ponto_contra_quem_bateu_se_a_bola_saiu_pela_porta_sem_quicar()
    {
        // Cruzou a rede e saiu pela porta do outro lado sem tocar o chão.
        var cruzada = Voo(2, 2, 1.2f, 6, -5.6f, 2);
        Assert.Contains(cruzada, e => e.Tipo == TipoDeEventoDaBola.CruzouRede && e.Para == -1);
        var saiu = Assert.Single(cruzada, e => e.Tipo == TipoDeEventoDaBola.Saiu);
        Assert.True(saiu.PelaPorta, "devia sair pela porta");
        Assert.Equal(-1, saiu.Lado);
        Assert.DoesNotContain(cruzada, e => e.Tipo is TipoDeEventoDaBola.Quique or TipoDeEventoDaBola.Parede);
        var arbitro = new Arbitro();
        arbitro.RegistrarGolpe(0);
        Assert.Equal(Decisao.Ponto(1, Motivo.Fora), Arbitrar(arbitro, cruzada));

        // Nem cruzou: saiu pela porta do próprio lado.
        var propria = Voo(3.5f, 2, 1, 6, -4, 1);
        var saiuDoProprioLado = Assert.Single(propria, e => e.Tipo == TipoDeEventoDaBola.Saiu);
        Assert.True(saiuDoProprioLado.PelaPorta, "devia sair pela porta do próprio lado");
        Assert.Equal(1, saiuDoProprioLado.Lado);
        Assert.DoesNotContain(propria, e => e.Tipo is TipoDeEventoDaBola.CruzouRede or TipoDeEventoDaBola.Quique or TipoDeEventoDaBola.Parede);
        var outro = new Arbitro();
        outro.RegistrarGolpe(0);
        Assert.Equal(Decisao.Ponto(1, Motivo.Fora), Arbitrar(outro, propria));
    }

    private static Decisao? Arbitrar(Arbitro arbitro, List<EventoDaBola> eventos)
    {
        foreach (var e in eventos)
            if (arbitro.Processar(e) is Decisao d) return d;
        return null;
    }
}
