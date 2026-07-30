using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using padelizou.Controllers;

namespace Padelizou.Tests;

// Denúncia de comentário de perfil: qualquer pessoa logada sinaliza, e a decisão é de
// admin em /Admin/Denuncias — apagar ou manter. Antes disso a moderação dependia de o
// autor, o dono do perfil ou um admin verem o comentário por conta própria: com o portão
// aberto ao público, texto ofensivo ficaria no ar até alguém do time passar por ali.
public class DenunciaComentarioTests
{
    private static (DbPadelContext ctx, ComentarioPerfil comentario) CenarioComComentario()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.AddRange(
            new Jogador { Id = 1, Nome = "Autor", Cpf = "1" },
            new Jogador { Id = 2, Nome = "Dono do Perfil", Cpf = "2" },
            new Jogador { Id = 3, Nome = "Terceiro", Cpf = "3" },
            new Jogador { Id = 9, Nome = "Admin", Cpf = "9", IsAdminGeral = true });
        var comentario = new ComentarioPerfil { Id = 1, AutorId = 1, PerfilId = 2, Texto = "texto suspeito" };
        ctx.ComentariosPerfil.Add(comentario);
        ctx.SaveChanges();
        return (ctx, comentario);
    }

    private static JogadoresController Jogadores(DbPadelContext ctx, int usuarioLogadoId)
    {
        var controller = new JogadoresController(ctx, new EstatisticasService(ctx));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.ToString()) }, "Teste")),
            },
        };
        controller.TempData = new TempDataDictionary(controller.HttpContext, Substitute.For<ITempDataProvider>());
        return controller;
    }

    private static AdminController Admin(DbPadelContext ctx, int usuarioLogadoId)
    {
        var controller = new AdminController(
            ctx,
            Substitute.For<IPushNotificationService>(),
            Substitute.For<Microsoft.Extensions.Configuration.IConfiguration>(),
            Microsoft.Extensions.Options.Options.Create(new RegistroResultadosSettings()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.ToString()) }, "Teste")),
            },
        };
        controller.TempData = new TempDataDictionary(controller.HttpContext, Substitute.For<ITempDataProvider>());
        return controller;
    }

    [Fact]
    public async Task Denunciar_carimba_o_comentario_e_guarda_quem_avisou()
    {
        var (ctx, comentario) = CenarioComComentario();

        await Jogadores(ctx, usuarioLogadoId: 3).DenunciarComentario(comentario.Id);

        Assert.NotNull(comentario.DenunciadoEm);
        Assert.Equal(3, comentario.DenunciadoPorId);
    }

    [Fact]
    public async Task Autor_nao_denuncia_o_proprio_comentario()
    {
        // Ele pode apagá-lo direto — denúncia do próprio texto só encheria a fila.
        var (ctx, comentario) = CenarioComComentario();

        await Jogadores(ctx, usuarioLogadoId: 1).DenunciarComentario(comentario.Id);

        Assert.Null(comentario.DenunciadoEm);
    }

    [Fact]
    public async Task Segunda_denuncia_nao_sobrescreve_a_primeira()
    {
        // A fila ordena pela denúncia mais antiga; re-carimbar empurraria o comentário
        // pro fim da fila a cada nova denúncia — quanto pior o texto, mais ele esperaria.
        var (ctx, comentario) = CenarioComComentario();

        await Jogadores(ctx, usuarioLogadoId: 3).DenunciarComentario(comentario.Id);
        var primeiroCarimbo = comentario.DenunciadoEm;

        await Jogadores(ctx, usuarioLogadoId: 2).DenunciarComentario(comentario.Id);

        Assert.Equal(primeiroCarimbo, comentario.DenunciadoEm);
        Assert.Equal(3, comentario.DenunciadoPorId);
    }

    [Fact]
    public async Task A_fila_do_admin_mostra_so_os_denunciados_na_ordem_de_chegada()
    {
        var (ctx, comentario) = CenarioComComentario();
        var maisAntigo = new ComentarioPerfil
        {
            Id = 2,
            AutorId = 1,
            PerfilId = 3,
            Texto = "denunciado antes",
            DenunciadoEm = DateTime.Now.AddHours(-2),
            DenunciadoPorId = 2,
        };
        ctx.ComentariosPerfil.Add(maisAntigo);
        comentario.DenunciadoEm = DateTime.Now;
        comentario.DenunciadoPorId = 3;
        ctx.SaveChanges();

        var resultado = (ViewResult)await Admin(ctx, usuarioLogadoId: 9).Denuncias();
        var fila = (List<ComentarioPerfil>)resultado.Model!;

        Assert.Equal(new[] { 2, 1 }, fila.Select(c => c.Id));
    }

    [Fact]
    public async Task Manter_limpa_o_carimbo_e_tira_da_fila_sem_apagar_o_texto()
    {
        var (ctx, comentario) = CenarioComComentario();
        comentario.DenunciadoEm = DateTime.Now;
        comentario.DenunciadoPorId = 3;
        ctx.SaveChanges();

        await Admin(ctx, usuarioLogadoId: 9).ManterComentarioDenunciado(comentario.Id);

        Assert.Null(comentario.DenunciadoEm);
        Assert.Null(comentario.DenunciadoPorId);
        Assert.Single(ctx.ComentariosPerfil);
    }

    [Fact]
    public async Task Apagar_remove_o_comentario_de_vez()
    {
        var (ctx, comentario) = CenarioComComentario();
        comentario.DenunciadoEm = DateTime.Now;
        ctx.SaveChanges();

        await Admin(ctx, usuarioLogadoId: 9).ApagarComentarioDenunciado(comentario.Id);

        Assert.Empty(ctx.ComentariosPerfil);
    }

    [Fact]
    public async Task Quem_nao_e_admin_nao_abre_a_fila_nem_decide_nada()
    {
        var (ctx, comentario) = CenarioComComentario();
        comentario.DenunciadoEm = DateTime.Now;
        ctx.SaveChanges();

        // O jogador 3 é gente comum: a fila redireciona e as decisões recusam.
        Assert.IsType<RedirectToActionResult>(await Admin(ctx, usuarioLogadoId: 3).Denuncias());
        Assert.IsType<ForbidResult>(await Admin(ctx, usuarioLogadoId: 3).ApagarComentarioDenunciado(comentario.Id));
        Assert.IsType<ForbidResult>(await Admin(ctx, usuarioLogadoId: 3).ManterComentarioDenunciado(comentario.Id));
        Assert.Single(ctx.ComentariosPerfil);
        Assert.NotNull(comentario.DenunciadoEm);
    }
}
