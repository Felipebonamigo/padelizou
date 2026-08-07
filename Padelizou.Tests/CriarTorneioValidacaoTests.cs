using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A criação do torneio recusa o que deixaria o torneio inútil — e recusa ANTES de gravar,
// devolvendo o formulário com o motivo. Achado no ensaio da liberação de 30/07/2026: dava
// pra criar torneio sem categoria nenhuma, e aí ninguém conseguia se inscrever.
public class CriarTorneioValidacaoTests
{
    private static Torneio TorneioValido() => new()
    {
        Nome = "Copa Teste",
        ClubeId = 1,
        Status = "Inscrições Abertas",
        SetsFaseGrupos = 1,
        GamesFaseGrupos = 6,
        RestricaoCategoria = "Livre",
        FormaPagamento = "Externo",
    };

    private static DbPadelContext ContextoComClube()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Id = 1, Nome = "Organizador", Cpf = "1", IsOrganizadorTorneio = true });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Clube Teste" });
        ctx.CategoriasPadrao.Add(new padelizou.Models.CategoriaPadrao
        {
            Id = 3,
            Nome = "3ª Categoria Masculina",
            Codigo = "3CatM",
            Tipo = "Masculina",
        });
        ctx.SaveChanges();
        return ctx;
    }

    [Fact]
    public async Task Torneio_sem_categoria_nenhuma_e_recusado()
    {
        // Sem categoria o formulário de inscrição não tem o que escolher: o organizador
        // compartilharia o link e descobriria pelo primeiro jogador que não conseguiu entrar.
        using var ctx = ContextoComClube();
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        var resultado = await controller.Create(TorneioValido(), Array.Empty<int>(), null, null, null, null);

        var view = Assert.IsType<ViewResult>(resultado);
        Assert.Contains("categoria", (string)view.ViewData["Erro"]!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(ctx.Torneios);
    }

    [Fact]
    public async Task Recusa_acontece_antes_de_gravar_qualquer_coisa()
    {
        // Recusar depois de criar deixaria um torneio órfão no ar a cada tentativa.
        using var ctx = ContextoComClube();
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        await controller.Create(TorneioValido(), null!, null, null, null, null);

        Assert.Empty(ctx.Torneios);
        Assert.Empty(ctx.Categorias);
    }

    [Fact]
    public async Task Com_categoria_o_torneio_nasce_com_a_chave_escolhida()
    {
        using var ctx = ContextoComClube();
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        var torneio = TorneioValido();
        torneio.Restrito = true;

        await controller.Create(torneio, new[] { 3 }, null, null, null, null,
            chaveAcessoEscolhida: "virgili10");

        var criado = Assert.Single(ctx.Torneios);
        Assert.Equal("virgili10", criado.ChaveAcesso);
        Assert.Single(ctx.Categorias);
    }

    [Fact]
    public async Task O_mesmo_organizador_nao_cria_dois_torneios_com_o_mesmo_nome()
    {
        // O caso real de 30/07/2026: "Amigos do Eder" criado duas vezes seguidas.
        using var ctx = ContextoComClube();
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        await controller.Create(TorneioValido(), new[] { 3 }, null, null, null, null);
        Assert.Single(ctx.Torneios);

        var segundo = TorneioValido();
        var resultado = await controller.Create(segundo, new[] { 3 }, null, null, null, null);

        var view = Assert.IsType<ViewResult>(resultado);
        Assert.Contains("já tem um torneio", (string)view.ViewData["Erro"]!);
        Assert.Single(ctx.Torneios);   // continua um só
    }

    [Fact]
    public async Task Nome_repetido_libera_depois_que_o_torneio_anterior_termina()
    {
        using var ctx = ContextoComClube();
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        await controller.Create(TorneioValido(), new[] { 3 }, null, null, null, null);
        var anterior = ctx.Torneios.Single();
        anterior.Status = "Finalizado";
        await ctx.SaveChangesAsync();

        await controller.Create(TorneioValido(), new[] { 3 }, null, null, null, null);

        Assert.Equal(2, ctx.Torneios.Count());
    }

    [Fact]
    public async Task Outro_organizador_pode_usar_o_mesmo_nome()
    {
        // "Copa de Verão" não pertence a ninguém: bloquear no sistema inteiro recusaria o
        // torneio de um clube por causa do nome escolhido por outro.
        using var ctx = ContextoComClube();
        ctx.Jogadores.Add(new Jogador { Id = 2, Nome = "Outro organizador", Cpf = "2", IsOrganizadorTorneio = true });
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1)
            .Create(TorneioValido(), new[] { 3 }, null, null, null, null);
        await TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 2)
            .Create(TorneioValido(), new[] { 3 }, null, null, null, null);

        Assert.Equal(2, ctx.Torneios.Count());
    }

    [Fact]
    public async Task Chave_ruim_recusa_antes_de_criar_o_torneio()
    {
        using var ctx = ContextoComClube();
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        var torneio = TorneioValido();
        torneio.Restrito = true;

        var resultado = await controller.Create(torneio, new[] { 3 }, null, null, null, null,
            chaveAcessoEscolhida: "ab");

        Assert.IsType<ViewResult>(resultado);
        Assert.Empty(ctx.Torneios);
    }
}
