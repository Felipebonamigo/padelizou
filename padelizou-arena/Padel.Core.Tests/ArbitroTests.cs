namespace Padel.Core.Tests;

public class ArbitroTests
{
    // Time 0 joga no lado +1; time 1 no lado -1.
    private static Arbitro RallyDoTime0() { var a = new Arbitro(); a.RegistrarGolpe(0); return a; }
    private static EventoDaBola Quique(float x, float y) => new(TipoDeEventoDaBola.Quique, x, y, 0, Quadra.LadoDe(y));
    private static EventoDaBola Parede(int lado, QualParede qual = QualParede.Fundo) => new(TipoDeEventoDaBola.Parede, 0, lado * 9, 1, lado, qual);
    private static EventoDaBola Cruzou(int para) => new(TipoDeEventoDaBola.CruzouRede, 0, 0, 1.5f, para);
    private static EventoDaBola Rede() => new(TipoDeEventoDaBola.Rede, 0, 0, 0.5f, 1);
    private static EventoDaBola Saiu(int lado) => new(TipoDeEventoDaBola.Saiu, 0, lado * 10, 4.5f, lado);

    [Fact]
    public void Dois_quiques_do_lado_de_quem_recebe_ponto_de_quem_bateu()
    {
        var a = RallyDoTime0();
        Assert.Null(a.Processar(Cruzou(-1)));
        Assert.Null(a.Processar(Quique(0, -5)));
        Assert.Null(a.Processar(Parede(-1)));   // depois do quique, pode
        Assert.Equal(Decisao.Ponto(0, Motivo.DoisQuiques), a.Processar(Quique(0, -7)));
    }

    [Fact]
    public void Parede_do_outro_lado_antes_de_quicar_ponto_de_quem_recebia()
    {
        var a = RallyDoTime0();
        a.Processar(Cruzou(-1));
        Assert.Equal(Decisao.Ponto(1, Motivo.ParedeSemQuicar), a.Processar(Parede(-1)));
    }

    [Fact]
    public void Bater_na_propria_parede_antes_de_cruzar_e_permitido_no_rally()
    {
        var a = RallyDoTime0();
        Assert.Null(a.Processar(Parede(1)));
        Assert.Null(a.Processar(Cruzou(-1)));
        Assert.Null(a.Processar(Quique(0, -5)));
    }

    [Fact]
    public void Bola_na_rede_que_cai_do_proprio_lado_ponto_contra_com_motivo_rede()
    {
        var a = RallyDoTime0();
        Assert.Null(a.Processar(Rede()));
        Assert.Equal(Decisao.Ponto(1, Motivo.Rede), a.Processar(Quique(0, 0.5f)));
    }

    [Fact]
    public void Bola_que_quica_do_proprio_lado_sem_tocar_a_rede_nao_passou()
    {
        var a = RallyDoTime0();
        Assert.Equal(Decisao.Ponto(1, Motivo.NaoPassou), a.Processar(Quique(0, 3)));
    }

    [Fact]
    public void Bola_que_sai_por_cima_contra_quem_bateu_se_nao_quicou_a_favor_se_ja_tinha_quicado()
    {
        var semQuique = RallyDoTime0();
        semQuique.Processar(Cruzou(-1));
        Assert.Equal(Decisao.Ponto(1, Motivo.Fora), semQuique.Processar(Saiu(-1)));

        var comQuique = RallyDoTime0();
        comQuique.Processar(Cruzou(-1));
        comQuique.Processar(Quique(0, -5));
        Assert.Equal(Decisao.Ponto(0, Motivo.Fora), comQuique.Processar(Saiu(-1)));
    }

    [Fact]
    public void Bola_que_quica_e_volta_pela_rede_sem_ninguem_tocar_ponto_de_quem_bateu()
    {
        var a = RallyDoTime0();
        a.Processar(Cruzou(-1));
        a.Processar(Quique(0, -8));
        a.Processar(Parede(-1));
        Assert.Equal(Decisao.Ponto(0, Motivo.VoltouPeloVidro), a.Processar(Cruzou(1)));
    }

    [Fact]
    public void O_mesmo_time_nao_bate_duas_vezes_e_quem_recebe_o_saque_deixa_quicar()
    {
        var a = RallyDoTime0();
        Assert.False(a.PodeGolpear(0));
        Assert.True(a.PodeGolpear(1));

        var s = new Arbitro();
        s.IniciarSaque(0, Quadra.CaixaDeSaque(-1, direita: true));
        Assert.False(s.PodeGolpear(1));
        s.Processar(Cruzou(-1));
        s.Processar(Quique(-2.5f, -3.5f));
        Assert.True(s.PodeGolpear(1));
    }

    [Fact]
    public void Saque_fora_da_caixa_e_falta_a_segunda_e_ponto_e_ponto_novo_zera()
    {
        var caixa = Quadra.CaixaDeSaque(-1, direita: true);   // lado -1, direita de quem está lá: x < 0
        var s = new Arbitro();
        s.IniciarSaque(0, caixa);
        s.Processar(Cruzou(-1));
        Assert.Equal(Decisao.Falta(Motivo.ForaDaCaixa), s.Processar(Quique(2, -3)));
        Assert.Equal(1, s.Faltas);
        s.IniciarSaque(0, caixa);
        s.Processar(Cruzou(-1));
        Assert.Equal(Decisao.Ponto(1, Motivo.DuplaFalta), s.Processar(Quique(-2, -8)));
        s.NovoPonto();
        Assert.Equal(0, s.Faltas);
    }

    [Fact]
    public void Saque_na_caixa_depois_de_tocar_a_rede_e_let_e_saque_limpo_segue()
    {
        var caixa = Quadra.CaixaDeSaque(-1, direita: true);
        var s = new Arbitro();
        s.IniciarSaque(0, caixa);
        s.Processar(Rede());
        Assert.Equal(Decisao.Let(), s.Processar(Quique(-2, -3)));
        Assert.Equal(0, s.Faltas);

        var limpo = new Arbitro();
        limpo.IniciarSaque(0, caixa);
        limpo.Processar(Cruzou(-1));
        Assert.Null(limpo.Processar(Quique(-2, -3)));
        Assert.Null(limpo.Processar(Parede(-1, QualParede.Lateral)));
    }

    [Fact]
    public void No_saque_rede_que_cai_do_proprio_lado_ou_propria_parede_e_falta_nao_ponto()
    {
        var caixa = Quadra.CaixaDeSaque(-1, direita: true);
        var s = new Arbitro();
        s.IniciarSaque(0, caixa);
        s.Processar(Rede());
        Assert.Equal(Decisao.Falta(Motivo.Rede), s.Processar(Quique(0, 0.3f)));
        s.IniciarSaque(0, caixa);
        Assert.Equal(Decisao.Ponto(1, Motivo.DuplaFalta), s.Processar(Parede(1)));
    }

    [Fact]
    public void A_caixa_de_saque_diagonal_fica_do_lado_certo()
    {
        var caixa = Quadra.CaixaDeSaque(-1, direita: true);
        Assert.Equal(new Caixa(-5, 0, -6.95f, 0), caixa);
        Assert.True(caixa.Contem(-2.5f, -3.5f));
        Assert.False(caixa.Contem(2.5f, -3.5f));
        Assert.False(caixa.Contem(-2.5f, -7.5f));
        Assert.Equal(new Caixa(-5, 0, 0, 6.95f), Quadra.CaixaDeSaque(1, direita: false));
    }
}
