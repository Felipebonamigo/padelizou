using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;
using padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O painel admin virou 5 grupos com abas (pedido do Felipe, 11/08: "tem muita coisa, tá ruim de
// me achar"). A lista de grupos é a fonte única do cartão em /Admin e da barra de abas dentro de
// cada tela — e é justamente isso que estes testes protegem: uma action renomeada deixa o cartão
// e a aba apontando pro vazio, e o defeito é MUDO (link que dá 404 só aparece pra quem clica).
public class PainelAdminAgrupadoTests
{
    [Fact]
    public void Toda_aba_aponta_pra_uma_tela_que_existe()
    {
        var acoes = typeof(AdminController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var orfas = PainelAdminAgrupado.Grupos
            .SelectMany(g => g.Abas)
            .Where(a => !acoes.Contains(a.Action))
            .Select(a => a.Action)
            .ToList();

        Assert.Empty(orfas);
    }

    [Fact]
    public void Nenhuma_tela_esta_em_dois_grupos()
    {
        // `DoAction` devolve o PRIMEIRO grupo que contém a action. Com a mesma tela em dois
        // grupos, a barra de abas dela mudaria conforme a ordem da lista — e a aba ativa
        // apareceria num grupo e sumiria no outro.
        var repetidas = PainelAdminAgrupado.Grupos
            .SelectMany(g => g.Abas)
            .GroupBy(a => a.Action, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(repetidas);
    }

    [Fact]
    public void Grupo_de_uma_aba_so_nao_e_grupo()
    {
        // A barra some quando sobra uma aba — um "grupo" de um item viraria um cartão que
        // promete abas e abre numa tela sem nenhuma.
        Assert.All(PainelAdminAgrupado.Grupos, g => Assert.True(g.Abas.Count > 1, g.Titulo));
    }

    [Fact]
    public void Cada_tela_do_painel_encontra_o_proprio_grupo()
    {
        foreach (var grupo in PainelAdminAgrupado.Grupos)
        {
            foreach (var aba in grupo.Abas)
            {
                Assert.Equal(grupo.Titulo, PainelAdminAgrupado.DoAction(aba.Action)?.Titulo);
            }
        }
    }

    [Fact]
    public void Tela_de_fora_dos_grupos_nao_ganha_barra_de_abas()
    {
        Assert.Null(PainelAdminAgrupado.DoAction("Erros"));
        Assert.Null(PainelAdminAgrupado.DoAction(null));
    }

    // ⚠️ Achado ao conferir a aba "Torneios para aprovar" no navegador: a tela devolvia 500.
    // Nada no cadastro impede DOIS "Criador" no mesmo torneio, e o `ToDictionaryAsync` assumia
    // um só — um torneio duplicado derrubava a FILA INTEIRA, e nenhum torneio podia ser
    // aprovado. No banco local já havia três assim.
    [Fact]
    public async Task A_fila_de_aprovacao_aguenta_torneio_com_dois_criadores()
    {
        using var ctx = TestInfra.NovoContexto();

        var raiz = new Jogador { Nome = "Felipe", Cpf = "99900000001", IsAdminRaiz = true };
        var criador = new Jogador { Nome = "Organizador Um", Cpf = "99900000002" };
        var socio = new Jogador { Nome = "Organizador Dois", Cpf = "99900000003" };
        ctx.Jogadores.AddRange(raiz, criador, socio);
        ctx.Torneios.Add(new Torneio { Id = 1, Nome = "Aberto de Verão", Codigo = "AV1", Status = "Inscrições Abertas" });
        await ctx.SaveChangesAsync();

        ctx.TorneioOrganizadores.AddRange(
            new TorneioOrganizador { TorneioId = 1, JogadorId = criador.Id, NivelAcesso = "Criador" },
            new TorneioOrganizador { TorneioId = 1, JogadorId = socio.Id, NivelAcesso = "Criador" });
        await ctx.SaveChangesAsync();

        var resposta = await Controlador(ctx, raiz.Id).TorneiosParaAprovar();

        var view = Assert.IsType<ViewResult>(resposta);
        var criadores = (Dictionary<int, Jogador>)view.ViewData["Criadores"]!;
        Assert.Equal(criador.Id, criadores[1].Id);   // o primeiro por JogadorId
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
        return controller;
    }
}
