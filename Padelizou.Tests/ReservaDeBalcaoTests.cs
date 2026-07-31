using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A reserva de balcão: o clube registra quem ligou ou chamou no WhatsApp (proposta aprovada
// pelo Felipe em 30/07/2026). Sem ela o dono era obrigado a manter o caderninho junto com o
// sistema — bloquear escondia o nome e a receita, deixar livre arriscava venda dupla.
public class ReservaDeBalcaoTests
{
    // ---------- A regra pura ----------

    [Fact]
    public void Sem_nome_nao_ha_reserva()
    {
        Assert.NotNull(ReservaDeBalcao.ProblemaCom("", DateTime.Now.AddHours(2), DateTime.Now));
        Assert.NotNull(ReservaDeBalcao.ProblemaCom("   ", DateTime.Now.AddHours(2), DateTime.Now));
        Assert.NotNull(ReservaDeBalcao.ProblemaCom(null, DateTime.Now.AddHours(2), DateTime.Now));
    }

    [Fact]
    public void Horario_que_ja_passou_e_recusado_mas_o_jogo_de_agora_entra()
    {
        var agora = new DateTime(2026, 7, 30, 20, 0, 0);

        Assert.NotNull(ReservaDeBalcao.ProblemaCom("Índio", agora.AddHours(-2), agora));
        // Margem de 1h pra trás: registrar o jogo que está ROLANDO (cliente chegou e pagou
        // na hora) é caso real de balcão.
        Assert.Null(ReservaDeBalcao.ProblemaCom("Índio", agora.AddMinutes(-30), agora));
        Assert.Null(ReservaDeBalcao.ProblemaCom("Índio", agora.AddHours(2), agora));
    }

    [Fact]
    public void O_selo_conta_a_verdade_e_so_a_verdade()
    {
        var pago = new MarcacaoJogo { PagaNoLocal = true, PagoEm = DateTime.Now };
        var pagaLa = new MarcacaoJogo { PagaNoLocal = true };
        var antiga = new MarcacaoJogo();          // de antes do selo existir
        var bloqueio = new MarcacaoJogo { EhBloqueio = true, PagaNoLocal = true };

        Assert.Equal("pago", ReservaDeBalcao.Selo(pago, 80m));
        Assert.Equal("paga lá", ReservaDeBalcao.Selo(pagaLa, 80m));
        // Sem preço não há o que acertar; reserva antiga não ganha selo porque não dá pra
        // saber como foi acertada — chutar mentiria pra um lado ou pro outro.
        Assert.Equal("", ReservaDeBalcao.Selo(pagaLa, null));
        Assert.Equal("", ReservaDeBalcao.Selo(pagaLa, 0m));
        Assert.Equal("", ReservaDeBalcao.Selo(antiga, 80m));
        Assert.Equal("", ReservaDeBalcao.Selo(bloqueio, 80m));
    }

    [Fact]
    public void Na_agenda_o_nome_do_balcao_manda_sobre_o_da_conta()
    {
        var conta = new Jogador { Nome = "Gabriel Reis", Celular = "51911112222" };
        var balcao = new MarcacaoJogo { NomeClienteBalcao = "Índio", CelularClienteBalcao = "51999998888", Jogador = conta };
        var site = new MarcacaoJogo { Jogador = conta };

        // O dono conhece o cliente pelo nome que ele mesmo digitou — não pelo cadastro.
        Assert.Equal("Índio", ReservaDeBalcao.NomeNaAgenda(balcao));
        Assert.Equal("51999998888", ReservaDeBalcao.CelularDeContato(balcao));
        Assert.Equal("Gabriel Reis", ReservaDeBalcao.NomeNaAgenda(site));
        Assert.Equal("51911112222", ReservaDeBalcao.CelularDeContato(site));
    }

    // ---------- O balcão de ponta a ponta ----------

    private static ClubeGestaoController Controller(DbPadelContext ctx, int usuarioId)
    {
        var c = new ClubeGestaoController(ctx, Substitute.For<IPushNotificationService>(),
            Microsoft.Extensions.Options.Options.Create(new Padelizou.Services.BarSettings()),
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

    private static (DbPadelContext ctx, Clube clube, QuadraClube quadra, Jogador dono) Cenario()
    {
        var ctx = TestInfra.NovoContexto();
        var dono = new Jogador { Id = 1, Nome = "Dono do Clube", Cpf = "1" };
        var clube = new Clube { Id = 1, Nome = "Arena Teste", DonoId = 1 };
        var quadra = new QuadraClube { Id = 1, ClubeId = 1, Nome = "Quadra 1", Ativa = true };
        ctx.Jogadores.Add(dono);
        ctx.Clubes.Add(clube);
        ctx.QuadrasClube.Add(quadra);
        ctx.SaveChanges();
        return (ctx, clube, quadra, dono);
    }

    private static DateTime Amanha19h => DateTime.Today.AddDays(1).AddHours(19);

    [Fact]
    public async Task Balcao_marca_com_nome_e_nasce_paga_la()
    {
        var (ctx, _, _, _) = Cenario();

        await Controller(ctx, 1).MarcarBalcao(1, 1, Amanha19h, "Índio", "(51) 99999-8888");

        var m = Assert.Single(ctx.MarcacoesJogo);
        Assert.Equal("Índio", m.NomeClienteBalcao);
        Assert.Equal("51999998888", m.CelularClienteBalcao);   // máscara removida no servidor
        Assert.True(m.PagaNoLocal);
        Assert.Null(m.PagoEm);
        Assert.Equal(1, m.JogadorId);   // sem conta com esse celular → aponta pra quem registrou
    }

    [Fact]
    public async Task Celular_que_bate_com_uma_conta_liga_a_reserva_a_ela()
    {
        var (ctx, _, _, _) = Cenario();
        ctx.Jogadores.Add(new Jogador { Id = 2, Nome = "Gabriel Reis", Cpf = "2", Celular = "51999998888" });
        ctx.SaveChanges();

        await Controller(ctx, 1).MarcarBalcao(1, 1, Amanha19h, "Índio", "51999998888", jaPagou: true);

        var m = Assert.Single(ctx.MarcacoesJogo);
        Assert.Equal(2, m.JogadorId);              // a reserva aparece no "minhas marcações" dele
        Assert.Equal("Índio", m.NomeClienteBalcao); // mas o nome que o dono digitou continua mandando
        Assert.NotNull(m.PagoEm);
    }

    [Fact]
    public async Task Horario_ocupado_e_recusado_sem_criar_nada()
    {
        // A razão de existir do balcão: impedir venda dupla.
        var (ctx, _, _, _) = Cenario();
        await Controller(ctx, 1).MarcarBalcao(1, 1, Amanha19h, "Índio", null);

        await Controller(ctx, 1).MarcarBalcao(1, 1, Amanha19h, "Outro Cliente", null);

        Assert.Single(ctx.MarcacoesJogo);
    }

    [Fact]
    public async Task Quem_nao_gerencia_o_clube_nao_marca_nem_recebe()
    {
        var (ctx, _, _, _) = Cenario();
        ctx.Jogadores.Add(new Jogador { Id = 9, Nome = "Intruso", Cpf = "9" });
        ctx.SaveChanges();

        Assert.IsType<ForbidResult>(await Controller(ctx, 9).MarcarBalcao(1, 1, Amanha19h, "Índio", null));
        Assert.Empty(ctx.MarcacoesJogo);
    }

    [Fact]
    public async Task Receber_carimba_uma_vez_e_nao_desmarca()
    {
        var (ctx, _, _, _) = Cenario();
        await Controller(ctx, 1).MarcarBalcao(1, 1, Amanha19h, "Índio", null);
        var m = ctx.MarcacoesJogo.Single();

        await Controller(ctx, 1).ReceberPagamento(m.Id);
        var carimbo = m.PagoEm;
        Assert.NotNull(carimbo);

        // Repetir não muda o carimbo — e não existe caminho de "desreceber": sumir com
        // receita registrada não pode ser um clique.
        await Controller(ctx, 1).ReceberPagamento(m.Id);
        Assert.Equal(carimbo, m.PagoEm);
    }

    [Fact]
    public async Task Cancelar_pelo_clube_so_vale_pra_reserva_de_balcao()
    {
        var (ctx, _, _, _) = Cenario();
        await Controller(ctx, 1).MarcarBalcao(1, 1, Amanha19h, "Índio", null);
        var balcao = ctx.MarcacoesJogo.Single();

        var doSite = new MarcacaoJogo
        {
            ClubeId = 1, QuadraClubeId = 1, JogadorId = 1,
            DataHora = Amanha19h.AddHours(1), DuracaoMinutos = 60, Status = "Confirmada",
        };
        ctx.MarcacoesJogo.Add(doSite);
        ctx.SaveChanges();

        await Controller(ctx, 1).CancelarBalcao(balcao.Id);
        Assert.Equal("Cancelada", balcao.Status);
        Assert.Equal("Clube", balcao.CanceladaPor);

        // A do site envolve a conta (e às vezes o dinheiro) do jogador — não cai por aqui.
        Assert.IsType<NotFoundResult>(await Controller(ctx, 1).CancelarBalcao(doSite.Id));
        Assert.Equal("Confirmada", doSite.Status);
    }

    [Fact]
    public async Task Horario_cancelado_volta_a_aceitar_reserva()
    {
        var (ctx, _, _, _) = Cenario();
        await Controller(ctx, 1).MarcarBalcao(1, 1, Amanha19h, "Índio", null);
        await Controller(ctx, 1).CancelarBalcao(ctx.MarcacoesJogo.Single().Id);

        await Controller(ctx, 1).MarcarBalcao(1, 1, Amanha19h, "Outro Cliente", null);

        Assert.Equal(2, ctx.MarcacoesJogo.Count());
        Assert.Single(ctx.MarcacoesJogo.Where(m => m.Status == "Confirmada"));
    }
}
