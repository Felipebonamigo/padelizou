using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.Services;
using Achado = Padelizou.Services.AuditoriaDaGrade.Achado;

namespace Padelizou.Tests;

// O FILTRO DO "CONFERIR A GRADE" (19/11/2026). 🗣️ Felipe, com 21 pontos na tela da Wallau:
// *"Crie um filtro aqui para exibir ou ocultar os tipos de pontos — 0 jogos de descanso, 1 jogo
// de descanso, jogo retardatário, e os demais"*.
public class FiltroDaConferenciaTests
{
    private static Achado Seguidos(int gravidade) => new(AuditoriaDaGrade.JogosSeguidos, "x", null, gravidade);

    [Fact]
    public void Jogos_seguidos_se_dividem_pelo_descanso_que_sobrou()
    {
        Assert.Equal("Jogos seguidos · 0 de descanso", FiltroDaConferencia.Tipo(Seguidos(2)));
        Assert.Equal("Jogos seguidos · 1 de descanso", FiltroDaConferencia.Tipo(Seguidos(1)));
        Assert.Equal(AuditoriaDaGrade.Retardatario,
            FiltroDaConferencia.Tipo(new Achado(AuditoriaDaGrade.Retardatario, "x", null)));
    }

    [Fact]
    public void Ocultar_tira_so_os_tipos_escolhidos()
    {
        var achados = new[] { Seguidos(2), Seguidos(1), new Achado(AuditoriaDaGrade.Retardatario, "r", null) };

        var visiveis = FiltroDaConferencia.Visiveis(achados, new[] { "Jogos seguidos · 0 de descanso", AuditoriaDaGrade.Retardatario });

        Assert.Equal(new[] { Seguidos(1) }, visiveis);
    }

    [Fact]
    public void Sem_filtro_mostra_tudo_e_os_tipos_saem_com_a_contagem()
    {
        var achados = new[] { Seguidos(2), Seguidos(2), Seguidos(1) };

        Assert.Equal(3, FiltroDaConferencia.Visiveis(achados, null).Count);
        var tipos = FiltroDaConferencia.Tipos(achados);
        Assert.Equal(("Jogos seguidos · 0 de descanso", 2), tipos[0]);
        Assert.Equal(("Jogos seguidos · 1 de descanso", 1), tipos[1]);
    }

    // O endereço leva o que está oculto: dá pra recarregar e mandar pro colega do mesmo jeito.
    [Fact]
    public void O_link_de_cada_tipo_liga_e_desliga_so_ele()
    {
        var ocultos = new[] { AuditoriaDaGrade.Retardatario };

        Assert.Equal(new[] { AuditoriaDaGrade.Retardatario, "Jogos seguidos · 0 de descanso" },
            FiltroDaConferencia.Alternar(ocultos, "Jogos seguidos · 0 de descanso"));
        Assert.Empty(FiltroDaConferencia.Alternar(ocultos, AuditoriaDaGrade.Retardatario));
    }

    [Fact]
    public async Task A_tela_recebe_o_que_esta_oculto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2);

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        Assert.IsType<ViewResult>(await controller.ConferirGrade(torneio.Id, new[] { AuditoriaDaGrade.Retardatario }));

        Assert.Equal(new[] { AuditoriaDaGrade.Retardatario }, controller.ViewBag.Ocultos as string[]);
    }
}
