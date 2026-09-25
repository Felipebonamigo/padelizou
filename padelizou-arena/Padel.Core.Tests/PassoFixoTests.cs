namespace Padel.Core.Tests;

/// <summary>
/// O passo fixo das sessões online (host e cliente): o delta da física do Godot vira passos de 1/120 s. Achado da
/// revisão do Godot: o acumulador somava o delta (double) e tirava Protocolo.Passo (float, 4,3e-10 s maior), a folga
/// de 1e-6 s se esgotava no quadro 2301 (~19 s), esse quadro não dava passo nenhum e o aperto dele (ação, lob — que só
/// valem um quadro) se perdia: no host, o golpe do jogador 0; no cliente, a entrada nem era mandada.
/// </summary>
public class PassoFixoTests
{
    private const double UmQuadro = 1.0 / 120;   // o delta que o Godot entrega a 120 Hz (physics_ticks_per_second)

    private static readonly Entrada ApertouAcao = new(0, 0, AcaoPressionada: true, AcaoSegurada: true);
    private static readonly Entrada ApertouLob = new(0, 0, AcaoPressionada: false, AcaoSegurada: false, LobPressionada: true);

    [Fact]
    public void Dez_mil_quadros_de_1_120_s_dao_exatamente_um_passo_cada()
    {
        var passo = new PassoFixo(120);
        var fora = new List<string>();
        for (int quadro = 1; quadro <= 10_000; quadro++)
        {
            int passos = passo.Quadro(UmQuadro, Entrada.Vazia);
            if (passos != 1) fora.Add($"quadro {quadro}: {passos} passo(s)");
        }
        Assert.Empty(fora);
    }

    [Fact]
    public void Aperto_de_acao_num_quadro_sem_passo_vale_no_passo_seguinte()
    {
        var passo = new PassoFixo(120);
        Assert.Equal(0, passo.Quadro(UmQuadro / 2, ApertouAcao));
        Assert.Equal(1, passo.Quadro(UmQuadro / 2, Entrada.Vazia));
        Assert.True(passo.EntradaDoPasso(0).AcaoPressionada, "o aperto do quadro sem passo se perdeu");
        // E vale uma vez só: o passo seguinte já não leva o aperto.
        Assert.Equal(0, passo.Quadro(UmQuadro / 2, Entrada.Vazia));
        Assert.Equal(1, passo.Quadro(UmQuadro / 2, Entrada.Vazia));
        Assert.False(passo.EntradaDoPasso(0).AcaoPressionada, "o aperto valeu em dois passos");
    }

    [Fact]
    public void Aperto_de_lob_num_quadro_sem_passo_tambem_nao_se_perde()
    {
        var passo = new PassoFixo(120);
        Assert.Equal(0, passo.Quadro(UmQuadro / 3, ApertouLob));
        Assert.Equal(0, passo.Quadro(UmQuadro / 3, Entrada.Vazia));
        Assert.Equal(1, passo.Quadro(UmQuadro / 3, Entrada.Vazia));
        Assert.True(passo.EntradaDoPasso(0).LobPressionada, "o lob do quadro sem passo se perdeu");
    }

    [Fact]
    public void Direcao_e_botao_segurado_sao_os_do_quadro_mais_novo()
    {
        var passo = new PassoFixo(120);
        Assert.Equal(0, passo.Quadro(UmQuadro / 2, new Entrada(1, 0, AcaoPressionada: true, AcaoSegurada: true)));
        Assert.Equal(1, passo.Quadro(UmQuadro / 2, new Entrada(-1, 0.5f, AcaoPressionada: false, AcaoSegurada: false)));
        var e = passo.EntradaDoPasso(0);
        Assert.Equal(-1, e.Dx);
        Assert.Equal(0.5f, e.Dy);
        Assert.False(e.AcaoSegurada);
        Assert.True(e.AcaoPressionada);
    }

    [Fact]
    public void Quadro_com_varios_passos_so_o_primeiro_leva_o_aperto()
    {
        var passo = new PassoFixo(120);
        var entrada = new Entrada(0.5f, -1, AcaoPressionada: true, AcaoSegurada: true, LobPressionada: true);
        Assert.Equal(3, passo.Quadro(3 * UmQuadro, entrada));
        Assert.Equal(entrada, passo.EntradaDoPasso(0));
        var seguinte = entrada with { AcaoPressionada = false, LobPressionada = false };
        Assert.Equal(seguinte, passo.EntradaDoPasso(1));
        Assert.Equal(seguinte, passo.EntradaDoPasso(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => passo.EntradaDoPasso(3));
    }

    [Theory]
    [InlineData(60, 6000, 12_000)]    // física a 60 Hz: 2 passos por quadro
    [InlineData(90, 9000, 12_000)]    // 90 Hz: 4 passos a cada 3 quadros, sem sobrar nem faltar no fim
    [InlineData(144, 14_400, 12_000)] // 144 Hz: 5 a cada 6
    [InlineData(240, 24_000, 12_000)] // 240 Hz: metade dos quadros sem passo
    public void Cem_segundos_em_qualquer_taxa_de_quadros_dao_doze_mil_passos(int quadrosPorSegundo, int quadros, long esperado)
    {
        var passo = new PassoFixo(120);
        long total = 0;
        for (int i = 0; i < quadros; i++) total += passo.Quadro(1.0 / quadrosPorSegundo, Entrada.Vazia);
        Assert.Equal(esperado, total);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-UmQuadro)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Delta_zero_negativo_ou_invalido_nao_da_passo_e_guarda_o_aperto(double delta)
    {
        var passo = new PassoFixo(120);
        Assert.Equal(0, passo.Quadro(delta, ApertouAcao));
        Assert.Equal(1, passo.Quadro(UmQuadro, Entrada.Vazia));
        Assert.True(passo.EntradaDoPasso(0).AcaoPressionada);
    }

    [Fact]
    public void Delta_absurdo_nao_trava_o_jogo_da_no_maximo_um_segundo_de_passos()
    {
        var passo = new PassoFixo(120);
        Assert.Equal(120, passo.Quadro(1e12, ApertouAcao));
        Assert.True(passo.EntradaDoPasso(0).AcaoPressionada);
        Assert.Equal(1, passo.Quadro(UmQuadro, Entrada.Vazia));   // e o relógio segue inteiro depois
    }

    [Fact]
    public void Antes_do_primeiro_quadro_nao_ha_passo_pra_pedir_entrada()
    {
        var passo = new PassoFixo(120);
        Assert.Throws<ArgumentOutOfRangeException>(() => passo.EntradaDoPasso(0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-120)]
    public void Taxa_de_passos_tem_que_ser_positiva(int passosPorSegundo) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PassoFixo(passosPorSegundo));
}
