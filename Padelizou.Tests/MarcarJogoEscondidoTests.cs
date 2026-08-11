using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;
using padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O Marcar Jogo está pronto e ESCONDIDO até termos um clube (pedido do Felipe, 11/08). Sumir
// do menu é cortesia — o que fecha a porta é a trava do controller, e é ela que estes testes
// seguram: sem eles, um refactor que remova o `PortaAbertaAsync` de uma ação deixa o CI verde
// e o módulo escondido volta a atender por uma porta que ninguém lembrou de fechar.
public class MarcarJogoEscondidoTests
{
    private const int Jogador = 5;
    private const int Admin = 9;

    private static DbPadelContext Cenario()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Id = Jogador, Nome = "Jogador", Cpf = "5" });
        ctx.Jogadores.Add(new Jogador { Id = Admin, Nome = "Felipe", Cpf = "9", IsAdminGeral = true });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Arena", MarcacaoHorariosAtiva = true });
        ctx.SaveChanges();
        return ctx;
    }

    private static MarcarJogoController Controller(DbPadelContext ctx, int usuarioId, bool habilitado)
    {
        var porta = new PortaDoMarcarJogo(ctx, Options.Create(new MarcarJogoSettings { Habilitado = habilitado }));
        var c = new MarcarJogoController(ctx,
            Substitute.For<IHorarioMarcacaoService>(),
            Substitute.For<IPagamentoInscricaoService>(),
            porta);
        c.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) }, "Teste")),
            },
        };
        return c;
    }

    [Fact]
    public async Task Escondido_a_vitrine_de_clubes_nao_responde_pro_jogador()
    {
        using var ctx = Cenario();

        var resposta = await Controller(ctx, Jogador, habilitado: false).Index();

        Assert.IsType<NotFoundResult>(resposta);
    }

    [Fact]
    public async Task Escondido_a_agenda_do_clube_nao_responde_pro_jogador()
    {
        using var ctx = Cenario();

        var resposta = await Controller(ctx, Jogador, habilitado: false).Clube(1, null);

        Assert.IsType<NotFoundResult>(resposta);
    }

    // A marcação em si, não só as telas: sem esta, dava pra reservar quadra postando o
    // formulário direto, com o módulo fechado.
    [Fact]
    public async Task Escondido_ninguem_marca_quadra_pelo_post()
    {
        using var ctx = Cenario();

        var resposta = await Controller(ctx, Jogador, habilitado: false)
            .Marcar(1, 1, DateTime.Today.AddDays(1).AddHours(19), 60);

        Assert.IsType<NotFoundResult>(resposta);
        Assert.Empty(ctx.MarcacoesJogo);
    }

    [Fact]
    public async Task Admin_do_padelizou_enxerga_o_modulo_escondido()
    {
        using var ctx = Cenario();

        var resposta = await Controller(ctx, Admin, habilitado: false).Index();

        Assert.IsType<ViewResult>(resposta);
    }

    [Fact]
    public async Task Habilitado_a_vitrine_volta_pra_todo_mundo()
    {
        using var ctx = Cenario();

        var resposta = await Controller(ctx, Jogador, habilitado: true).Index();

        Assert.IsType<ViewResult>(resposta);
    }

    // ⚠️ A trava é só na ENTRADA. Quem já tem reserva precisa continuar vendo e CANCELANDO —
    // fechar isso junto prenderia a pessoa numa quadra que ela não consegue mais desmarcar, e
    // o no-show sobraria pro clube.
    [Fact]
    public async Task Escondido_quem_ja_tem_reserva_ainda_ve_e_cancela()
    {
        using var ctx = Cenario();
        ctx.QuadrasClube.Add(new QuadraClube { Id = 1, ClubeId = 1, Nome = "Quadra 1", Ativa = true });
        ctx.MarcacoesJogo.Add(new MarcacaoJogo
        {
            Id = 1,
            ClubeId = 1,
            QuadraClubeId = 1,
            JogadorId = Jogador,
            DataHora = DateTime.Today.AddDays(1).AddHours(19),
            DuracaoMinutos = 60,
            Status = "Confirmada",
        });
        ctx.SaveChanges();

        var lista = await Controller(ctx, Jogador, habilitado: false).MinhasMarcacoes();
        Assert.IsType<ViewResult>(lista);

        await Controller(ctx, Jogador, habilitado: false).Cancelar(1);
        Assert.Equal("Cancelada", ctx.MarcacoesJogo.Single().Status);
    }
}
