using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 09/11/2026 — "PÔR OS 2 JOGOS EM" PRECISA OFERECER A QUINTA À NOITE.
//
// 🗣️ Felipe, com o print do painel "Pagamentos e impedimentos": *"aqui tem que ver os dias que
// tem o torneio, porque esse torneio começa na quinta à noite"*. O seletor só conhecia sexta,
// sábado de manhã e sábado à tarde — a quinta nunca entrou no enum (ConcentracaoDeJogos já
// avisava: "se fizer falta, entra aqui e em JanelaDoTurno").
//
// ⚠️ A COLUNA É `int` (Dupla.ConcentrarJogosEm). O valor novo entra NO FIM do enum: inserido no
// meio, toda dupla com "sábado à tarde" gravado viraria outro turno sem ninguém mexer.
public class ConcentracaoNaQuintaTests
{
    // 03/12/2026 é uma quinta-feira.
    private static Torneio QueComecaNaQuinta() => new() { DataInicio = new DateTime(2026, 12, 3, 19, 0, 0) };
    private static Torneio QueComecaNoSabado() => new() { DataInicio = new DateTime(2026, 12, 5, 8, 0, 0) };

    [Fact]
    public void Torneio_que_comeca_na_quinta_oferece_a_quinta_a_noite_primeiro()
    {
        var opcoes = TurnosDoTorneio.OferecidosParaConcentrar(QueComecaNaQuinta());

        Assert.Equal(new[]
        {
            TurnoDeConcentracao.Nenhuma, TurnoDeConcentracao.QuintaNoite, TurnoDeConcentracao.SextaNoite,
            TurnoDeConcentracao.SabadoManha, TurnoDeConcentracao.SabadoTarde,
        }, opcoes);
    }

    [Fact]
    public void Torneio_que_comeca_no_sabado_nao_oferece_quinta()
    {
        Assert.DoesNotContain(TurnoDeConcentracao.QuintaNoite, TurnosDoTorneio.OferecidosParaConcentrar(QueComecaNoSabado()));
    }

    [Fact]
    public void A_quinta_a_noite_e_o_dia_de_quinta_inteiro()
    {
        // Mesma forma da sexta: nela só existe o turno da noite.
        var janela = ConcentracaoDeJogos.JanelaDoTurno(QueComecaNaQuinta(), TurnoDeConcentracao.QuintaNoite);

        Assert.Equal((new DateTime(2026, 12, 3), new DateTime(2026, 12, 4)), janela);
    }

    [Fact]
    public void Quinta_tem_rotulo_nas_duas_formas()
    {
        Assert.Equal("Quinta à noite", ConcentracaoDeJogos.RotuloCurto(TurnoDeConcentracao.QuintaNoite));
        Assert.Equal("Os 2 jogos na quinta à noite", ConcentracaoDeJogos.Rotulo(TurnoDeConcentracao.QuintaNoite));
    }

    [Fact]
    public void Os_valores_gravados_nao_mudam()
    {
        Assert.Equal(0, (int)TurnoDeConcentracao.Nenhuma);
        Assert.Equal(1, (int)TurnoDeConcentracao.SextaNoite);
        Assert.Equal(2, (int)TurnoDeConcentracao.SabadoManha);
        Assert.Equal(3, (int)TurnoDeConcentracao.SabadoTarde);
        Assert.Equal(4, (int)TurnoDeConcentracao.QuintaNoite);
    }

    [Fact]
    public void Organizador_pode_concentrar_na_quinta_num_torneio_de_quinta()
    {
        var torneio = QueComecaNaQuinta();
        torneio.Status = "Inscrições Abertas";
        var dupla = new Dupla();

        Assert.Null(ConcentracaoDeJogos.MotivoParaOrganizadorNaoConcentrar(dupla, torneio, jaSorteou: false,
            TurnoDeConcentracao.QuintaNoite));
    }
}
