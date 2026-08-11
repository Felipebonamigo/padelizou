using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Vários horários numa marcação só (pedido do Felipe, 30/07): 2h seguidas em slots de 1h,
// ou duas quadras pro grupo grande. Tudo-ou-nada: se um caiu, nada é criado — criar só
// metade entregaria outra coisa que não a escolhida.
public class MarcarVariosTests
{
    private static readonly DateTime Amanha19 = DateTime.Today.AddDays(1).AddHours(19);

    private static MarcarJogoController Controller(DbPadelContext ctx, int usuarioId,
        IHorarioMarcacaoService? slots = null, IPagamentoInscricaoService? pagamentos = null)
    {
        var pag = pagamentos ?? Substitute.For<IPagamentoInscricaoService>();
        // Porta ABERTA nos testes: o módulo está escondido em produção até termos um clube
        // (Services/MarcarJogoSettings), mas o que se exercita aqui é a regra do tudo-ou-nada,
        // não a trava. Com a porta fechada toda ação responderia 404 e o teste passaria vazio.
        var porta = new PortaDoMarcarJogo(ctx,
            Microsoft.Extensions.Options.Options.Create(new MarcarJogoSettings { Habilitado = true }));
        var c = new MarcarJogoController(ctx,
            slots ?? Substitute.For<IHorarioMarcacaoService>(),
            pag, porta);
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

    private static (DbPadelContext ctx, IHorarioMarcacaoService slots) Cenario(params (int Quadra, DateTime Inicio, decimal? Preco)[] disponiveis)
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Id = 5, Nome = "Jogador", Cpf = "5" });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Arena", MarcacaoHorariosAtiva = true });
        ctx.QuadrasClube.AddRange(
            new QuadraClube { Id = 1, ClubeId = 1, Nome = "Quadra 1", Ativa = true },
            new QuadraClube { Id = 2, ClubeId = 1, Nome = "Quadra 2", Ativa = true });
        ctx.SaveChanges();

        var svc = Substitute.For<IHorarioMarcacaoService>();
        svc.ObterSlotsDisponiveisAsync(1, Arg.Any<DateTime>()).Returns(disponiveis
            .Select(d => new SlotMarcacao(d.Quadra, $"Quadra {d.Quadra}", d.Inicio, d.Inicio.AddHours(1), d.Preco))
            .ToList());
        return (ctx, svc);
    }

    // O serviço de verdade (com o gateway dublado), pra exercitar o EfetivarAsync — o
    // mesmo arranjo dos outros testes de efetivação.
    private static PagamentoInscricaoService ServicoDePagamento(DbPadelContext ctx)
    {
        var asaas = Substitute.For<IAsaasService>();
        asaas.Configurado.Returns(true);
        return new PagamentoInscricaoService(
            ctx, asaas, Microsoft.Extensions.Options.Options.Create(new AsaasSettings()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<PagamentoInscricaoService>.Instance,
            Substitute.For<IPushNotificationService>(),
            Microsoft.Extensions.Options.Options.Create(new TaxasExibicao()),
            Microsoft.Extensions.Options.Options.Create(new PlanoProfessorSettings()));
    }

    private static string Slot(int quadra, DateTime inicio) => $"{quadra}|{inicio:yyyy-MM-ddTHH:mm:ss}";

    [Fact]
    public async Task Dois_horarios_seguidos_viram_duas_reservas_paga_la()
    {
        var (ctx, slots) = Cenario((1, Amanha19, 80m), (1, Amanha19.AddHours(1), 80m));

        await Controller(ctx, 5, slots).MarcarVarios(1,
            new[] { Slot(1, Amanha19), Slot(1, Amanha19.AddHours(1)) });

        Assert.Equal(2, ctx.MarcacoesJogo.Count());
        Assert.All(ctx.MarcacoesJogo, m =>
        {
            Assert.Equal(5, m.JogadorId);
            Assert.Equal("Confirmada", m.Status);
            Assert.True(m.PagaNoLocal);   // clube sem cobrança online: acerto no balcão
            Assert.Equal(60, m.DuracaoMinutos);
        });
    }

    [Fact]
    public async Task Se_um_horario_caiu_nada_e_criado()
    {
        // Só o das 19h está disponível; o das 20h acabou de ser tomado.
        var (ctx, slots) = Cenario((1, Amanha19, 80m));

        await Controller(ctx, 5, slots).MarcarVarios(1,
            new[] { Slot(1, Amanha19), Slot(1, Amanha19.AddHours(1)) });

        Assert.Empty(ctx.MarcacoesJogo);
    }

    [Fact]
    public async Task Horarios_de_dias_diferentes_sao_recusados_juntos()
    {
        var (ctx, slots) = Cenario((1, Amanha19, 80m), (1, Amanha19.AddDays(1), 80m));

        await Controller(ctx, 5, slots).MarcarVarios(1,
            new[] { Slot(1, Amanha19), Slot(1, Amanha19.AddDays(1)) });

        Assert.Empty(ctx.MarcacoesJogo);
    }

    [Fact]
    public async Task Com_cobranca_online_vai_pro_checkout_e_nada_nasce_antes_do_webhook()
    {
        var (ctx, slots) = Cenario((1, Amanha19, 80m), (2, Amanha19, 80m));
        var dono = new Jogador { Id = 9, Nome = "Dono", Cpf = "9" };

        var pagamentos = Substitute.For<IPagamentoInscricaoService>();
        pagamentos.ObterDonoDoClubeAsync(1).Returns(dono);
        pagamentos.PodeCobrarQuadra(Arg.Any<decimal?>(), dono).Returns(true);
        pagamentos.IniciarCobrancaQuadrasAsync(Arg.Any<Clube>(), dono, Arg.Any<Jogador>(),
            160m, Arg.Any<DadosMarcacaoJogoVarios>()).Returns("https://checkout/x");

        var resultado = await Controller(ctx, 5, slots, pagamentos).MarcarVarios(1,
            new[] { Slot(1, Amanha19), Slot(2, Amanha19) });

        var redirect = Assert.IsType<RedirectResult>(resultado);
        Assert.Equal("https://checkout/x", redirect.Url);
        Assert.Empty(ctx.MarcacoesJogo);   // igual a torneio e aula: só nasce quando paga

        // E a cobrança foi da SOMA (2 × 80), não de um slot só.
        await pagamentos.Received(1).IniciarCobrancaQuadrasAsync(Arg.Any<Clube>(), dono,
            Arg.Any<Jogador>(), 160m, Arg.Is<DadosMarcacaoJogoVarios>(d => d.Slots.Count == 2));
    }

    [Fact]
    public async Task O_webhook_cria_todas_pagas_ou_nenhuma()
    {
        var (ctx, _) = Cenario();
        var dados = new DadosMarcacaoJogoVarios(1, 5, new List<SlotReservado>
        {
            new(1, Amanha19, 60),
            new(1, Amanha19.AddHours(1), 60),
        });
        var pagamento = new Pagamento
        {
            Id = 77, JogadorId = 5, Tipo = "JogoVarios", Status = "Confirmado",
            DadosInscricao = System.Text.Json.JsonSerializer.Serialize(dados),
        };
        ctx.Pagamentos.Add(pagamento);
        ctx.SaveChanges();

        var svc = ServicoDePagamento(ctx);
        await svc.EfetivarAsync(pagamento);

        Assert.Equal(2, ctx.MarcacoesJogo.Count());
        Assert.All(ctx.MarcacoesJogo, m => Assert.NotNull(m.PagoEm));
        Assert.NotNull(pagamento.ReferenciaId);

        // Reentrega do webhook não duplica (guarda de idempotência pela ReferenciaId).
        await svc.EfetivarAsync(pagamento);
        Assert.Equal(2, ctx.MarcacoesJogo.Count());
    }

    [Fact]
    public async Task Webhook_com_um_horario_tomado_nao_cria_nenhum_e_pede_estorno()
    {
        var (ctx, _) = Cenario();
        // O das 20h foi tomado enquanto a cobrança estava pendente.
        ctx.MarcacoesJogo.Add(new MarcacaoJogo
        {
            ClubeId = 1, QuadraClubeId = 1, JogadorId = 9,
            DataHora = Amanha19.AddHours(1), DuracaoMinutos = 60, Status = "Confirmada",
        });
        var dados = new DadosMarcacaoJogoVarios(1, 5, new List<SlotReservado>
        {
            new(1, Amanha19, 60),
            new(1, Amanha19.AddHours(1), 60),
        });
        var pagamento = new Pagamento
        {
            Id = 78, JogadorId = 5, Tipo = "JogoVarios", Status = "Confirmado",
            DadosInscricao = System.Text.Json.JsonSerializer.Serialize(dados),
        };
        ctx.Pagamentos.Add(pagamento);
        ctx.SaveChanges();

        await ServicoDePagamento(ctx).EfetivarAsync(pagamento);

        Assert.Single(ctx.MarcacoesJogo);   // só a que já existia
        Assert.Equal("AguardandoEstorno", pagamento.Status);
    }
}

