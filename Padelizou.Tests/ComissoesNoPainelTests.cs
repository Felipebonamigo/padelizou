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

// A tela /Admin/Comissoes vista pelos dois lados.
//
// ⚠️ ESTE ARQUIVO EXISTE POR CAUSA DE UMA LINHA SÓ: o parceiro comercial não escolhe o filtro,
// ele É o filtro. Dois parceiros disputam os mesmos contatos, e quem o outro trouxe e quanto
// vai receber é informação de concorrente. Um `parceiroId ?? quem.Id` no controller pareceria
// idêntico e deixaria `?parceiroId=3` ler a carteira do colega — sem erro, sem log, sem nada.
public class ComissoesNoPainelTests
{
    private static async Task<(DbPadelContext ctx, Jogador ana, Jogador bruno)> CenarioComDoisParceirosAsync()
    {
        var ctx = TestInfra.NovoContexto();

        var ana = new Jogador { Nome = "Ana Vendedora", Cpf = "99900000011", IsParceiroComercial = true };
        var bruno = new Jogador { Nome = "Bruno Vendedor", Cpf = "99900000012", IsParceiroComercial = true };
        var clienteDaAna = new Jogador { Nome = "Grupo da Ana", Cpf = "99900000021" };
        var clienteDoBruno = new Jogador { Nome = "Grupo do Bruno", Cpf = "99900000022" };
        ctx.Jogadores.AddRange(ana, bruno, clienteDaAna, clienteDoBruno);
        await ctx.SaveChangesAsync();

        ctx.LeadsComerciais.AddRange(
            new LeadComercial
            {
                ParceiroId = ana.Id, Contato = "Grupo da Ana", Telefone = "51984567890",
                Tipo = "Torneio", Status = LeadsComerciais.Ganho, ClienteId = clienteDaAna.Id,
                RegistradoEm = new DateTime(2026, 1, 5), FechadoEm = new DateTime(2026, 2, 1),
            },
            new LeadComercial
            {
                ParceiroId = bruno.Id, Contato = "Grupo do Bruno", Telefone = "51984567891",
                Tipo = "Torneio", Status = LeadsComerciais.Ganho, ClienteId = clienteDoBruno.Id,
                RegistradoEm = new DateTime(2026, 1, 6), FechadoEm = new DateTime(2026, 2, 2),
            });

        ctx.Pagamentos.AddRange(
            new Pagamento
            {
                Tipo = "TorneioDupla", TorneioId = 1, JogadorId = 999, RecebedorId = clienteDaAna.Id,
                Valor = 1000m, Comissao = 100m, Status = "Confirmado",
                ConfirmadoEm = new DateTime(2026, 3, 10),
            },
            new Pagamento
            {
                Tipo = "TorneioDupla", TorneioId = 2, JogadorId = 999, RecebedorId = clienteDoBruno.Id,
                Valor = 2000m, Comissao = 200m, Status = "Confirmado",
                ConfirmadoEm = new DateTime(2026, 3, 11),
            });

        await ctx.SaveChangesAsync();
        return (ctx, ana, bruno);
    }

    private static List<ComissaoDeClienteVM> Linhas(IActionResult resposta) =>
        (List<ComissaoDeClienteVM>)Assert.IsType<ViewResult>(resposta).Model!;

    [Fact]
    public async Task O_parceiro_ve_apenas_os_clientes_que_ELE_trouxe()
    {
        var (ctx, ana, _) = await CenarioComDoisParceirosAsync();
        using var _ctx = ctx;

        var linhas = Linhas(await Controlador(ctx, ana.Id).Comissoes(parceiroId: null));

        var linha = Assert.Single(linhas);
        Assert.Equal("Grupo da Ana", linha.Lead.Contato);
        Assert.Equal(30m, linha.Conta.Total);   // 30% de R$ 100 na 1ª edição
    }

    [Fact]
    public async Task Parceiro_que_pede_o_id_do_COLEGA_na_url_continua_vendo_so_o_dele()
    {
        // 🔒 A trava. Se um dia isto falhar, um parceiro lê a carteira do outro digitando um
        // número na barra de endereço.
        var (ctx, ana, bruno) = await CenarioComDoisParceirosAsync();
        using var _ctx = ctx;

        var linhas = Linhas(await Controlador(ctx, ana.Id).Comissoes(parceiroId: bruno.Id));

        var linha = Assert.Single(linhas);
        Assert.Equal("Grupo da Ana", linha.Lead.Contato);
    }

    [Fact]
    public async Task O_parceiro_nao_recebe_a_lista_de_nomes_dos_outros_parceiros()
    {
        // Montar o seletor sempre entregaria os nomes dos concorrentes dentro do HTML dele,
        // mesmo sem nenhum link pra clicar.
        var (ctx, ana, _) = await CenarioComDoisParceirosAsync();
        using var _ctx = ctx;

        var controller = Controlador(ctx, ana.Id);
        await controller.Comissoes(parceiroId: null);

        Assert.Empty((List<Jogador>)controller.ViewBag.Parceiros);
        Assert.True((bool)controller.ViewBag.SoVeAsProprias);
    }

    [Fact]
    public async Task O_admin_ve_todos_os_parceiros_e_pode_filtrar_por_um()
    {
        var (ctx, ana, _) = await CenarioComDoisParceirosAsync();
        using var _ctx = ctx;

        var felipe = new Jogador { Nome = "Felipe", Cpf = "99900000001", IsAdminRaiz = true };
        ctx.Jogadores.Add(felipe);
        await ctx.SaveChangesAsync();

        var todos = Linhas(await Controlador(ctx, felipe.Id).Comissoes(parceiroId: null));
        Assert.Equal(2, todos.Count);

        var soDaAna = Linhas(await Controlador(ctx, felipe.Id).Comissoes(parceiroId: ana.Id));
        Assert.Equal("Grupo da Ana", Assert.Single(soDaAna).Lead.Contato);
    }

    [Fact]
    public async Task Quem_nao_e_parceiro_nem_admin_nao_entra()
    {
        var (ctx, _, _) = await CenarioComDoisParceirosAsync();
        using var _ctx = ctx;

        var qualquerUm = new Jogador { Nome = "Jogador comum", Cpf = "99900000099" };
        ctx.Jogadores.Add(qualquerUm);
        await ctx.SaveChangesAsync();

        var resposta = await Controlador(ctx, qualquerUm.Id).Comissoes(parceiroId: null);

        var redirect = Assert.IsType<RedirectToActionResult>(resposta);
        Assert.Equal("Perfil", redirect.ActionName);
    }

    [Fact]
    public async Task Lead_aberto_nao_vira_linha_de_comissao()
    {
        // Só contato FECHADO paga. Um lead aberto é uma conversa, não uma venda.
        var (ctx, ana, _) = await CenarioComDoisParceirosAsync();
        using var _ctx = ctx;

        foreach (var lead in ctx.LeadsComerciais)
            lead.Status = LeadsComerciais.Aberto;
        await ctx.SaveChangesAsync();

        Assert.Empty(Linhas(await Controlador(ctx, ana.Id).Comissoes(parceiroId: null)));
    }

    [Fact]
    public void A_flag_do_parceiro_comercial_nao_da_poder_nenhum_alem_da_tela_dele()
    {
        // ⚠️ A regra de ouro do PoderesNoSistema: flag nova NUNCA entra na credencial de
        // escrita nem na de leitura ampla. Errar pra menos é "ele não vê a tela"; pra mais é
        // gente de fora dentro do painel.
        var parceiro = new Jogador { Nome = "Ana", IsParceiroComercial = true };

        Assert.False(PoderesNoSistema.PodeEditarTudo(parceiro));
        Assert.False(PoderesNoSistema.PodeOlharTudo(parceiro));
        Assert.False(PoderesNoSistema.PodeVerRelatorioDoRanking(parceiro));
        Assert.True(PoderesNoSistema.PodeVerComissoesDeParceiro(parceiro));
        Assert.True(PoderesNoSistema.SoVeAsPropriasComissoes(parceiro));
    }

    [Fact]
    public void Admin_que_tambem_e_parceiro_continua_vendo_todo_mundo()
    {
        // A flag SOMA, não substitui — mesma disciplina do assistente e do parceiro do Ranking.
        // Sem isto, o Felipe se indicando viraria alguém que só vê a própria carteira.
        var felipeParceiro = new Jogador { Nome = "Felipe", IsAdminRaiz = true, IsParceiroComercial = true };

        Assert.True(PoderesNoSistema.PodeVerComissoesDeParceiro(felipeParceiro));
        Assert.False(PoderesNoSistema.SoVeAsPropriasComissoes(felipeParceiro));
    }

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
