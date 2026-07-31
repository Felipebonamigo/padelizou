using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;

namespace Padelizou.Tests;

// Check-in e financeiro do torneio; ocupação, mensalista e bloqueio do clube.
public class GestaoTorneioClubeTests
{
    private static ClubeGestaoController NovoClubeController(DbPadelContext ctx, int usuarioId)
    {
        var c = new ClubeGestaoController(ctx, Substitute.For<IPushNotificationService>(),
            new Padelizou.Services.ModuloDoBar(ctx, Microsoft.Extensions.Options.Options.Create(new Padelizou.Services.BarSettings())),
            NullLogger<ClubeGestaoController>.Instance);

        c.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.ToString()) }, "Teste")),
            },
        };
        c.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            c.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        return c;
    }

    private static (Clube clube, QuadraClube quadra, Jogador dono) MontarClube(DbPadelContext ctx)
    {
        var dono = new Jogador { Nome = "Dono", Cpf = "1" };
        ctx.Jogadores.Add(dono);
        ctx.SaveChanges();

        var clube = new Clube { Nome = "Arena", Contato = "x", Endereco = "y", DonoId = dono.Id };
        ctx.Clubes.Add(clube);
        ctx.SaveChanges();

        var quadra = new QuadraClube { ClubeId = clube.Id, Nome = "Q1", Ativa = true };
        ctx.QuadrasClube.Add(quadra);
        ctx.SaveChanges();

        return (clube, quadra, dono);
    }

    // ---------- Check-in ----------

    [Fact]
    public async Task Check_in_marca_e_desmarca_a_dupla()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        await controller.MarcarCheckIn(dupla.Id, presente: true);
        Assert.NotNull((await ctx.Duplas.FindAsync(dupla.Id))!.CheckInEm);

        await controller.MarcarCheckIn(dupla.Id, presente: false);
        Assert.Null((await ctx.Duplas.FindAsync(dupla.Id))!.CheckInEm);
    }

    [Fact]
    public async Task Quem_nao_organiza_nao_faz_check_in()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");
        var dupla = await ctx.Duplas.FirstAsync(d => d.CategoriaId == categoria.Id);

        var intruso = new Jogador { Nome = "Intruso", Cpf = "77777777777" };
        ctx.Jogadores.Add(intruso);
        ctx.SaveChanges();

        var controller = TestInfra.NovoTorneiosController(ctx, intruso.Id);
        var resultado = await controller.MarcarCheckIn(dupla.Id, presente: true);

        Assert.IsType<ForbidResult>(resultado);
        Assert.Null((await ctx.Duplas.FindAsync(dupla.Id))!.CheckInEm);
    }

    // ---------- Clube: bloqueio ----------

    [Fact]
    public async Task Bloqueio_ocupa_a_quadra_e_pode_ser_removido()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, quadra, dono) = MontarClube(ctx);
        var controller = NovoClubeController(ctx, dono.Id);

        var quando = DateTime.Today.AddDays(1).AddHours(19);
        await controller.Bloquear(clube.Id, quadra.Id, quando, 60, "Manutenção");

        var bloqueio = await ctx.MarcacoesJogo.SingleAsync();
        Assert.True(bloqueio.EhBloqueio);
        Assert.Equal("Manutenção", bloqueio.MotivoBloqueio);

        await controller.Desbloquear(bloqueio.Id);
        Assert.Empty(ctx.MarcacoesJogo);
    }

    [Fact]
    public async Task Nao_bloqueia_horario_que_ja_tem_reserva()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, quadra, dono) = MontarClube(ctx);
        var quando = DateTime.Today.AddDays(1).AddHours(19);

        ctx.MarcacoesJogo.Add(new MarcacaoJogo
        {
            ClubeId = clube.Id, QuadraClubeId = quadra.Id, JogadorId = dono.Id,
            DataHora = quando, DuracaoMinutos = 60, Status = "Confirmada",
        });
        ctx.SaveChanges();

        var controller = NovoClubeController(ctx, dono.Id);
        await controller.Bloquear(clube.Id, quadra.Id, quando, 60, "Evento");

        // Continua só a reserva original — o bloqueio não entrou por cima.
        Assert.Single(ctx.MarcacoesJogo);
        Assert.False(ctx.MarcacoesJogo.Single().EhBloqueio);
    }

    // ---------- Clube: mensalista ----------

    [Fact]
    public async Task Mensalista_cria_uma_reserva_por_semana_e_pula_conflito()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, quadra, dono) = MontarClube(ctx);
        var aluno = new Jogador { Nome = "Mensalista", Cpf = "2" };
        ctx.Jogadores.Add(aluno);
        ctx.SaveChanges();

        var primeira = DateTime.Today.AddDays(1).AddHours(19);

        // A 3ª semana já está ocupada — deve ser pulada, não sobrescrita.
        ctx.MarcacoesJogo.Add(new MarcacaoJogo
        {
            ClubeId = clube.Id, QuadraClubeId = quadra.Id, JogadorId = dono.Id,
            DataHora = primeira.AddDays(14), DuracaoMinutos = 60, Status = "Confirmada",
        });
        ctx.SaveChanges();

        var controller = NovoClubeController(ctx, dono.Id);
        await controller.CriarMensalista(clube.Id, quadra.Id, aluno.Id, primeira, 60, semanas: 4);

        var doMensalista = await ctx.MarcacoesJogo.Where(m => m.JogadorId == aluno.Id).ToListAsync();
        Assert.Equal(3, doMensalista.Count);                       // 4 pedidas, 1 pulada
        Assert.Single(doMensalista.Select(m => m.MensalidadeId).Distinct()); // mesma série
        Assert.All(doMensalista, m => Assert.NotNull(m.MensalidadeId));
    }

    [Fact]
    public async Task Cancelar_mensalista_so_mexe_no_futuro()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, quadra, dono) = MontarClube(ctx);
        var serie = Guid.NewGuid();

        ctx.MarcacoesJogo.Add(new MarcacaoJogo
        {
            ClubeId = clube.Id, QuadraClubeId = quadra.Id, JogadorId = dono.Id,
            DataHora = DateTime.Now.AddDays(-7), DuracaoMinutos = 60,
            Status = "Confirmada", MensalidadeId = serie,
        });
        ctx.MarcacoesJogo.Add(new MarcacaoJogo
        {
            ClubeId = clube.Id, QuadraClubeId = quadra.Id, JogadorId = dono.Id,
            DataHora = DateTime.Now.AddDays(7), DuracaoMinutos = 60,
            Status = "Confirmada", MensalidadeId = serie,
        });
        ctx.SaveChanges();

        var controller = NovoClubeController(ctx, dono.Id);
        await controller.CancelarMensalista(clube.Id, serie);

        var todas = await ctx.MarcacoesJogo.OrderBy(m => m.DataHora).ToListAsync();
        Assert.Equal("Confirmada", todas[0].Status);   // passado não se apaga
        Assert.Equal("Cancelada", todas[1].Status);
    }

    // ---------- Clube: no-show ----------

    [Fact]
    public async Task No_show_marca_falta_e_respeita_a_decisao_de_cobrar()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, quadra, dono) = MontarClube(ctx);

        var m = new MarcacaoJogo
        {
            ClubeId = clube.Id, QuadraClubeId = quadra.Id, JogadorId = dono.Id,
            DataHora = DateTime.Now.AddHours(-2), DuracaoMinutos = 60, Status = "Confirmada",
        };
        ctx.MarcacoesJogo.Add(m);
        ctx.SaveChanges();

        var controller = NovoClubeController(ctx, dono.Id);
        await controller.RegistrarNoShow(m.Id, compareceu: false, cobrar: true);

        var depois = await ctx.MarcacoesJogo.FindAsync(m.Id);
        Assert.Equal("Faltou", depois!.Status);
        Assert.True(depois.CobrarMesmoAssim);
    }

    // ---------- Clube: ocupação ----------

    [Fact]
    public async Task Mapa_de_ocupacao_conta_so_o_que_o_clube_abriu()
    {
        using var ctx = TestInfra.NovoContexto();
        var (clube, quadra, dono) = MontarClube(ctx);

        // Abre segunda-feira das 19h às 21h (2 slots de 1h).
        ctx.HorariosMarcacaoDisponivel.Add(new HorarioMarcacaoDisponivel
        {
            ClubeId = clube.Id, QuadraClubeId = quadra.Id, DiaSemana = (int)DayOfWeek.Monday,
            HoraInicio = TimeSpan.FromHours(19), HoraFim = TimeSpan.FromHours(21),
            DuracaoMinutos = 60, Ativo = true, Preco = 100,
        });
        ctx.SaveChanges();

        var hoje = DateTime.Today;
        var domingo = hoje.AddDays(-(int)hoje.DayOfWeek);
        var segunda = domingo.AddDays(1);

        ctx.MarcacoesJogo.Add(new MarcacaoJogo
        {
            ClubeId = clube.Id, QuadraClubeId = quadra.Id, JogadorId = dono.Id,
            DataHora = segunda.AddHours(19), DuracaoMinutos = 60, Status = "Confirmada",
        });
        ctx.SaveChanges();

        var controller = NovoClubeController(ctx, dono.Id);
        var vm = (OcupacaoClubeVM)((ViewResult)await controller.Ocupacao(clube.Id, domingo)).Model!;

        // A semana tem 7 dias, mas só a segunda abre: 2 slots no total.
        Assert.Equal(2, vm.TotalSlots);
        Assert.Equal(1, vm.SlotsOcupados);
        Assert.Equal(50, vm.PercentualOcupacao);
        Assert.Equal(100, vm.ReceitaSemana);
    }
}
