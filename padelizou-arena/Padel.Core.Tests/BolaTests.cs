namespace Padel.Core.Tests;

public class BolaTests
{
    private static List<EventoDaBola> Simular(Bola b, float segundos, float passo = 1f / 120f)
    {
        var eventos = new List<EventoDaBola>();
        for (float t = 0; t < segundos && b.EmJogo && !b.Parada; t += passo) b.Avancar(passo, eventos);
        return eventos;
    }

    [Fact]
    public void Bola_solta_do_alto_quica_perde_energia_e_acaba_parada()
    {
        var b = new Bola();
        b.Posicionar(1, 4, 2);
        b.Lancar(new Velocidade(0, 0, 0, 0));
        var eventos = Simular(b, 6);
        var quiques = eventos.Where(e => e.Tipo == TipoDeEventoDaBola.Quique).ToList();
        Assert.True(quiques.Count >= 3, $"esperava vários quiques, veio {quiques.Count}");
        Assert.Equal(1, quiques[0].Lado);
        Assert.Equal(0, b.Z);
        Assert.True(b.Parada);
        Assert.True(float.IsFinite(b.X) && float.IsFinite(b.Y));
    }

    [Fact]
    public void A_parede_lateral_devolve_a_bola_pra_dentro()
    {
        var b = new Bola();
        b.Posicionar(4, 5, 1);
        b.Lancar(new Velocidade(10, 0, 3, 0));
        var eventos = Simular(b, 1);
        var parede = eventos.First(e => e.Tipo == TipoDeEventoDaBola.Parede);
        Assert.Equal(QualParede.Lateral, parede.Parede);
        Assert.True(MathF.Abs(b.X) < 5, $"bola fora da quadra: x={b.X}");
    }

    [Fact]
    public void A_parede_de_fundo_devolve_e_a_bola_alta_demais_sai()
    {
        var baixa = new Bola();
        baixa.Posicionar(0, -9, 1);
        baixa.Lancar(new Velocidade(0, -8, 2, 0));
        Assert.Contains(Simular(baixa, 0.5f), e => e.Tipo == TipoDeEventoDaBola.Parede && e.Parede == QualParede.Fundo);
        Assert.True(baixa.EmJogo);

        var alta = new Bola();
        alta.Posicionar(0, -9, 3.9f);
        alta.Lancar(new Velocidade(0, -8, 6, 0));
        Assert.Contains(Simular(alta, 0.5f), e => e.Tipo == TipoDeEventoDaBola.Saiu);
        Assert.False(alta.EmJogo);
    }

    [Fact]
    public void Bola_baixa_bate_na_rede_e_volta_bola_alta_cruza()
    {
        var baixa = new Bola();
        baixa.Posicionar(0, 3, 0.5f);
        baixa.Lancar(new Velocidade(0, -10, 1, 0));
        Assert.Contains(Simular(baixa, 0.6f), e => e.Tipo == TipoDeEventoDaBola.Rede);
        Assert.True(baixa.Y > 0, "depois da rede a bola devia ficar do lado de onde veio");

        var alta = new Bola();
        alta.Posicionar(0, 3, 1.5f);
        alta.Lancar(new Velocidade(0, -10, 3, 0));
        var cruzou = Simular(alta, 0.6f).First(e => e.Tipo == TipoDeEventoDaBola.CruzouRede);
        Assert.Equal(-1, cruzou.Para);
        Assert.True(cruzou.Z > Quadra.AlturaDaRede);
    }

    [Theory]
    [InlineData(2, 7, 1, -2, -6, 0.9f)]
    [InlineData(-3, -8, 0.8f, 3, 6.5f, 1.0f)]
    [InlineData(0, 2, 0.6f, 0, -7, 0.5f)]    // curto e baixo: precisa subir o arco
    [InlineData(1, 4, 1, -1, -8, 1.7f)]       // lob
    public void Calcular_golpe_leva_a_bola_por_cima_da_rede_ate_perto_do_alvo(float x0, float y0, float z0, float ax, float ay, float tempo)
    {
        var v = Golpes.Calcular(x0, y0, z0, ax, ay, tempo);
        var b = new Bola();
        b.Posicionar(x0, y0, z0);
        b.Lancar(v);
        var eventos = Simular(b, 3);
        int indiceDoQuique = eventos.FindIndex(e => e.Tipo == TipoDeEventoDaBola.Quique);
        Assert.True(indiceDoQuique >= 0, "não quicou");
        var ateOQuique = eventos.Take(indiceDoQuique).ToList();   // depois do quique a bola volta pelas paredes; não interessa
        Assert.Contains(ateOQuique, e => e.Tipo == TipoDeEventoDaBola.CruzouRede);
        Assert.DoesNotContain(ateOQuique, e => e.Tipo == TipoDeEventoDaBola.Rede);
        var quique = eventos[indiceDoQuique];
        float erro = Util.Distancia(quique.X, quique.Y, ax, ay);
        Assert.True(erro < 0.6f, $"caiu a {erro:F2} m do alvo ({quique.X:F2}, {quique.Y:F2})");
    }

    [Fact]
    public void Calcular_golpe_com_ignorar_rede_deixa_a_bola_baixa()
    {
        var v = Golpes.Calcular(0, 6, 0.9f, 0, -0.3f, 0.45f, ignorarRede: true);
        var b = new Bola();
        b.Posicionar(0, 6, 0.9f);
        b.Lancar(v);
        Assert.Contains(Simular(b, 2), e => e.Tipo == TipoDeEventoDaBola.Rede);
    }

    [Fact]
    public void O_aleatorio_com_semente_e_reproduzivel_e_fica_em_0_1()
    {
        var a = new Aleatorio(42);
        var b = new Aleatorio(42);
        for (int i = 0; i < 1000; i++)
        {
            float x = a.Proximo();
            Assert.Equal(x, b.Proximo());
            Assert.InRange(x, 0f, 0.99999999f);
        }
        Assert.NotEqual(new Aleatorio(1).Proximo(), new Aleatorio(2).Proximo());
    }
}
