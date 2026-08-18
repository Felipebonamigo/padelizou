using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;
using padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// O botão "Paguei": o registro do repasse ao parceiro comercial.
//
// ⚠️ Ele NÃO movimenta dinheiro — o Pix é feito no banco, à mão. Estes testes cuidam da conta
// que sobra: quanto já saiu, quanto falta, e o que ainda não pode sair.
public class RepasseAoParceiroTests
{
    // ── A conta do acerto ─────────────────────────────────────────────────────────────────

    [Fact]
    public void O_saldo_e_o_que_ganhou_menos_o_que_ja_recebeu()
    {
        var acerto = new ComissaoDoParceiro.Acerto(TotalGanho: 300m, JaRepassado: 100m, DoMesCorrente: 0m);

        Assert.Equal(200m, acerto.Saldo);
        Assert.Equal(200m, acerto.PagavelAgora);
        Assert.Equal(0m, acerto.Adiantado);
    }

    [Fact]
    public void O_mes_corrente_NAO_entra_no_a_pagar()
    {
        // ⚠️ A regra que evita o repasse que ninguém explica depois: o mês corrente ainda pode
        // crescer — ou ENCOLHER, se entrar um estorno antes do fim do mês. Pagar adiantado
        // cria um crédito que só aparece dois meses depois.
        var acerto = new ComissaoDoParceiro.Acerto(TotalGanho: 300m, JaRepassado: 0m, DoMesCorrente: 120m);

        Assert.Equal(300m, acerto.Saldo);
        Assert.Equal(180m, acerto.PagavelAgora);
    }

    [Fact]
    public void Quem_recebeu_a_mais_aparece_como_adiantado_e_nunca_como_saldo_negativo()
    {
        // Acontece de verdade: o Pix sai e depois um estorno derruba a comissão que o
        // justificava. Um número negativo na tela faria o Felipe achar que ainda deve.
        var acerto = new ComissaoDoParceiro.Acerto(TotalGanho: 100m, JaRepassado: 150m, DoMesCorrente: 0m);

        Assert.Equal(-50m, acerto.Saldo);
        Assert.Equal(0m, acerto.PagavelAgora);
        Assert.Equal(50m, acerto.Adiantado);
    }

    [Fact]
    public void Pagou_tudo_que_fechou_e_o_a_pagar_zera_mesmo_com_o_mes_correndo()
    {
        var acerto = new ComissaoDoParceiro.Acerto(TotalGanho: 250m, JaRepassado: 180m, DoMesCorrente: 70m);

        Assert.Equal(0m, acerto.PagavelAgora);
        Assert.Equal(70m, acerto.Saldo);
    }

    // ── A gravação ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Registrar_o_repasse_derruba_o_saldo_na_mesma_hora()
    {
        using var ctx = await CenarioAsync();
        var felipe = ctx.Jogadores.Single(j => j.IsAdminRaiz);
        var ana = ctx.Jogadores.Single(j => j.Nome == "Ana Vendedora");

        var antes = DoParceiro(await Controlador(ctx, felipe.Id).Comissoes(null), ana.Id);
        Assert.Equal(20m, antes.Acerto.TotalGanho);
        Assert.Equal(20m, antes.Acerto.PagavelAgora);

        await Controlador(ctx, felipe.Id)
            .RegistrarRepasse(ana.Id, valor: 20m, pagoEm: new DateTime(2026, 8, 11), observacao: "Pix");

        var depois = DoParceiro(await Controlador(ctx, felipe.Id).Comissoes(null), ana.Id);
        Assert.Equal(20m, depois.Acerto.JaRepassado);
        Assert.Equal(0m, depois.Acerto.PagavelAgora);
        Assert.Equal("Pix", Assert.Single(depois.Repasses).Observacao);
    }

    [Fact]
    public async Task Valor_com_centavos_e_gravado_com_centavos()
    {
        // Este projeto já leu 79.90 como 7990 uma vez. Repasse é dinheiro de terceiro: testar
        // com centavos aqui não é zelo, é o mínimo.
        using var ctx = await CenarioAsync();
        var felipe = ctx.Jogadores.Single(j => j.IsAdminRaiz);
        var ana = ctx.Jogadores.Single(j => j.Nome == "Ana Vendedora");

        await Controlador(ctx, felipe.Id).RegistrarRepasse(ana.Id, 12.34m, null, null);

        Assert.Equal(12.34m, ctx.RepassesAoParceiro.Single().Valor);
    }

    [Fact]
    public async Task Data_no_futuro_e_recusada()
    {
        // Ano trocado é o erro comum, e uma data futura some do fechamento do mês sem ninguém
        // entender por quê.
        using var ctx = await CenarioAsync();
        var felipe = ctx.Jogadores.Single(j => j.IsAdminRaiz);
        var ana = ctx.Jogadores.Single(j => j.Nome == "Ana Vendedora");

        await Controlador(ctx, felipe.Id).RegistrarRepasse(ana.Id, 20m, DateTime.Now.AddDays(1), null);

        Assert.Empty(ctx.RepassesAoParceiro);
    }

    [Fact]
    public async Task Valor_zero_ou_negativo_e_recusado()
    {
        using var ctx = await CenarioAsync();
        var felipe = ctx.Jogadores.Single(j => j.IsAdminRaiz);
        var ana = ctx.Jogadores.Single(j => j.Nome == "Ana Vendedora");

        await Controlador(ctx, felipe.Id).RegistrarRepasse(ana.Id, 0m, null, null);
        await Controlador(ctx, felipe.Id).RegistrarRepasse(ana.Id, -50m, null, null);

        Assert.Empty(ctx.RepassesAoParceiro);
    }

    [Fact]
    public async Task Apagar_o_lancamento_devolve_o_valor_ao_saldo()
    {
        using var ctx = await CenarioAsync();
        var felipe = ctx.Jogadores.Single(j => j.IsAdminRaiz);
        var ana = ctx.Jogadores.Single(j => j.Nome == "Ana Vendedora");

        await Controlador(ctx, felipe.Id).RegistrarRepasse(ana.Id, 20m, null, null);
        var repasse = ctx.RepassesAoParceiro.Single();

        await Controlador(ctx, felipe.Id).ExcluirRepasse(repasse.Id);

        Assert.Empty(ctx.RepassesAoParceiro);
        Assert.Equal(20m, DoParceiro(await Controlador(ctx, felipe.Id).Comissoes(null), ana.Id).Acerto.PagavelAgora);
    }

    // ── Quem pode ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task O_parceiro_NAO_pode_registrar_o_proprio_repasse()
    {
        // 🔒 Senão ele zera a própria dívida sozinho. Escrever é de admin.
        using var ctx = await CenarioAsync();
        var ana = ctx.Jogadores.Single(j => j.Nome == "Ana Vendedora");

        var resposta = await Controlador(ctx, ana.Id).RegistrarRepasse(ana.Id, 30m, null, null);

        Assert.IsType<ForbidResult>(resposta);
        Assert.Empty(ctx.RepassesAoParceiro);
    }

    [Fact]
    public async Task O_assistente_do_sistema_NAO_ve_nem_registra()
    {
        // ⚠️ MUDOU EM 18/08/2026. Antes o assistente ABRIA esta tela (a trava dele era o
        // verbo: passava no GET, barrado no POST). Agora dinheiro é só do raiz, e a carteira
        // dos parceiros é a primeira coisa que sai da vista de quem não é dono da conta.
        using var ctx = await CenarioAsync();
        var foka = new Jogador { Nome = "Foka", Cpf = "99900000077", IsAssistente = true };
        ctx.Jogadores.Add(foka);
        await ctx.SaveChangesAsync();
        var ana = ctx.Jogadores.Single(j => j.Nome == "Ana Vendedora");

        Assert.IsType<RedirectToActionResult>(await Controlador(ctx, foka.Id).Comissoes(null));

        var resposta = await Controlador(ctx, foka.Id).RegistrarRepasse(ana.Id, 30m, null, null);
        Assert.IsType<ForbidResult>(resposta);
        Assert.Empty(ctx.RepassesAoParceiro);
    }

    [Fact]
    public async Task O_parceiro_ve_os_proprios_repasses_e_nenhum_do_colega()
    {
        using var ctx = await CenarioAsync();
        var felipe = ctx.Jogadores.Single(j => j.IsAdminRaiz);
        var ana = ctx.Jogadores.Single(j => j.Nome == "Ana Vendedora");
        var bruno = ctx.Jogadores.Single(j => j.Nome == "Bruno Vendedor");

        await Controlador(ctx, felipe.Id).RegistrarRepasse(ana.Id, 10m, null, "da Ana");
        await Controlador(ctx, felipe.Id).RegistrarRepasse(bruno.Id, 999m, null, "do Bruno");

        var doLadoDaAna = Acertos(await Controlador(ctx, ana.Id).Comissoes(parceiroId: bruno.Id));

        var unico = Assert.Single(doLadoDaAna);
        Assert.Equal(ana.Id, unico.Parceiro.Id);
        Assert.Equal("da Ana", Assert.Single(unico.Repasses).Observacao);
    }

    // ── Cenário ───────────────────────────────────────────────────────────────────────────

    private static async Task<DbPadelContext> CenarioAsync()
    {
        var ctx = TestInfra.NovoContexto();

        var felipe = new Jogador { Nome = "Felipe", Cpf = "99900000001", IsAdminRaiz = true };
        var ana = new Jogador { Nome = "Ana Vendedora", Cpf = "99900000011", IsParceiroComercial = true };
        var bruno = new Jogador { Nome = "Bruno Vendedor", Cpf = "99900000012", IsParceiroComercial = true };
        var clienteDaAna = new Jogador { Nome = "Grupo da Ana", Cpf = "99900000021" };
        var clienteDoBruno = new Jogador { Nome = "Grupo do Bruno", Cpf = "99900000022" };
        ctx.Jogadores.AddRange(felipe, ana, bruno, clienteDaAna, clienteDoBruno);
        await ctx.SaveChangesAsync();

        ctx.LeadsComerciais.AddRange(
            Fechado(ana.Id, clienteDaAna.Id, "Grupo da Ana", "51988880001"),
            Fechado(bruno.Id, clienteDoBruno.Id, "Grupo do Bruno", "51988880002"));

        // Um pagamento antigo pra cada — fora do mês corrente, então tudo já é pagável.
        ctx.Pagamentos.AddRange(
            Confirmado(clienteDaAna.Id, torneioId: 1, comissao: 100m),
            Confirmado(clienteDoBruno.Id, torneioId: 2, comissao: 100m));

        await ctx.SaveChangesAsync();
        return ctx;
    }

    private static LeadComercial Fechado(int parceiroId, int clienteId, string contato, string telefone) =>
        new()
        {
            ParceiroId = parceiroId, ClienteId = clienteId, Contato = contato, Telefone = telefone,
            Tipo = "Torneio", Status = LeadsComerciais.Ganho,
            RegistradoEm = new DateTime(2026, 1, 5), FechadoEm = new DateTime(2026, 2, 1),
        };

    // Um ano atrás: nunca cai no mês corrente, seja qual for o dia em que o teste rodar.
    private static Pagamento Confirmado(int recebedorId, int torneioId, decimal comissao) =>
        new()
        {
            Tipo = "TorneioDupla", TorneioId = torneioId, JogadorId = 999, RecebedorId = recebedorId,
            Valor = 1000m, Comissao = comissao, Status = "Confirmado",
            ConfirmadoEm = DateTime.Now.AddMonths(-11),
        };

    private static List<AcertoDoParceiroVM> Acertos(IActionResult resposta) =>
        (List<AcertoDoParceiroVM>)Assert.IsType<ViewResult>(resposta).ViewData["Acertos"]!;

    // O cenário tem DOIS parceiros de propósito — pegar "o único" esconderia justamente o
    // vazamento de um pro outro.
    private static AcertoDoParceiroVM DoParceiro(IActionResult resposta, int parceiroId) =>
        Acertos(resposta).Single(a => a.Parceiro.Id == parceiroId);

    private static AdminController Controlador(DbPadelContext ctx, int usuarioLogadoId)
    {
        var controller = new AdminController(
            ctx,
            Substitute.For<IPushNotificationService>(),
            new ConfigurationBuilder().Build(),
            Options.Create(new RegistroResultadosSettings()));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.ToString()) }, "Teste")),
            },
        };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        return controller;
    }
}
