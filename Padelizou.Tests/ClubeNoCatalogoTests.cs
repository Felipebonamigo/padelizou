using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using padelizou.Controllers;

namespace Padelizou.Tests;

// "Clube selecionável": o interruptor que decide se um clube aparece pra quem vai ESCOLHER
// um local. Nasceu porque o clube vem do que o jogador digitou no cadastro, então a base
// junta os nomes certos com "Batata", "Rogérinho." e sobra de teste — e isso aparecia em
// toda tela de escolha.
//
// O que estes testes guardam são as duas maneiras de o interruptor sair errado:
// (1) desligar um clube e ele continuar aparecendo em ALGUMA tela — o motivo de a régua ser
//     um método só, e não uma condição copiada em onze consultas;
// (2) desligar um clube e isso DESFAZER alguma coisa. Não desfaz: some da escolha e pronto.
public class ClubeNoCatalogoTests
{
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

    private static DbPadelContext Cenario()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.AddRange(
            new Jogador { Id = 9, Nome = "Admin do sistema", Cpf = "9", IsAdminGeral = true },
            new Jogador { Id = 1, Nome = "Jogador comum", Cpf = "1" });
        ctx.Cidades.AddRange(
            new Cidade { Id = 1, Nome = "Porto Alegre", Estado = "RS" },
            new Cidade { Id = 2, Nome = "Gravataí", Estado = "RS" });
        ctx.Clubes.AddRange(
            new Clube { Id = 1, Nome = "Nata Padel", CidadeId = 1 },
            new Clube { Id = 2, Nome = "Arena Central", CidadeId = 2 },
            new Clube { Id = 3, Nome = "Clube Teste CSRF 1785325726351" },   // a sobra de teste
            new Clube { Id = 4, Nome = "Sem cidade nenhuma" });
        ctx.SaveChanges();
        return ctx;
    }

    // ── O passivo ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Clube_novo_nasce_no_catalogo()
    {
        // ⚠️ Esta é a metade em C# da mesma garantia. A outra metade é o `defaultValue: true`
        // escrito à mão na migration ClubeSelecionavel: o padrão daqui só vale pra objeto NOVO,
        // e sem o da migration todos os clubes que já existiam sumiriam das listas no deploy.
        Assert.True(new Clube().Selecionavel);
    }

    [Fact]
    public async Task Clube_criado_pelo_jogador_no_cadastro_ja_aparece_pros_outros()
    {
        // Decisão do Felipe: nada de fila de aprovação. Quem cadastra vê o clube na hora, e o
        // que for lixo é desligado depois.
        using var ctx = Cenario();

        var novo = await CatalogoLocais.AcharOuCriarClubeAsync(ctx, "Arena Nova", "Canoas", "RS");

        Assert.NotNull(novo);
        Assert.True(novo!.Selecionavel);
        Assert.Contains(await ctx.Clubes.ParaEscolher().ToListAsync(), c => c.Id == novo.Id);
    }

    // ── A régua ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Clube_desligado_some_da_lista_de_escolha()
    {
        using var ctx = Cenario();
        await Admin(ctx, usuarioLogadoId: 9).AlternarClubeSelecionavel(clubeId: 3);

        var paraEscolher = await ctx.Clubes.ParaEscolher().ToListAsync();

        Assert.DoesNotContain(paraEscolher, c => c.Id == 3);
        Assert.Equal(3, paraEscolher.Count);
    }

    [Fact]
    public async Task Desligar_nao_apaga_o_clube_nem_desfaz_vinculo()
    {
        // A promessa que a tela faz por escrito: some da escolha, e nada mais muda.
        using var ctx = Cenario();
        ctx.JogadorClubes.Add(new JogadorClube { JogadorId = 1, ClubeId = 3 });
        await ctx.SaveChangesAsync();

        await Admin(ctx, usuarioLogadoId: 9).AlternarClubeSelecionavel(clubeId: 3);

        Assert.NotNull(await ctx.Clubes.FindAsync(3));
        Assert.True(await ctx.JogadorClubes.AnyAsync(jc => jc.JogadorId == 1 && jc.ClubeId == 3));
    }

    [Fact]
    public async Task O_interruptor_volta_atras()
    {
        using var ctx = Cenario();

        await Admin(ctx, usuarioLogadoId: 9).AlternarClubeSelecionavel(clubeId: 3);
        Assert.False((await ctx.Clubes.FindAsync(3))!.Selecionavel);

        await Admin(ctx, usuarioLogadoId: 9).AlternarClubeSelecionavel(clubeId: 3);
        Assert.True((await ctx.Clubes.FindAsync(3))!.Selecionavel);
    }

    [Fact]
    public async Task A_tela_de_gestao_continua_vendo_o_clube_desligado()
    {
        // Se /Admin/Clubes filtrasse, o botão de religar sumiria junto com o clube — e não
        // haveria caminho de volta pela tela.
        using var ctx = Cenario();
        await Admin(ctx, usuarioLogadoId: 9).AlternarClubeSelecionavel(clubeId: 3);

        var resposta = await Admin(ctx, usuarioLogadoId: 9).Clubes() as ViewResult;
        var listados = Assert.IsType<List<Clube>>(resposta!.Model);

        Assert.Contains(listados, c => c.Id == 3);
        Assert.Equal(4, listados.Count);
    }

    [Fact]
    public async Task Nome_repetido_continua_recusado_mesmo_com_o_outro_desligado()
    {
        // Duplicado é duplicado mesmo fora do catálogo: o cadastro casa clube POR NOME, e dois
        // "Nata Padel" fariam cada pessoa cair num dos dois pelo acaso da consulta — inclusive
        // no desligado, que ninguém está olhando.
        using var ctx = Cenario();
        await Admin(ctx, usuarioLogadoId: 9).AlternarClubeSelecionavel(clubeId: 1);

        await Admin(ctx, usuarioLogadoId: 9).RenomearClube(clubeId: 2, nome: "nata padel");

        Assert.Equal("Arena Central", (await ctx.Clubes.FindAsync(2))!.Nome);
    }

    [Fact]
    public async Task Quem_nao_e_admin_nao_mexe_no_catalogo()
    {
        using var ctx = Cenario();

        var resposta = await Admin(ctx, usuarioLogadoId: 1).AlternarClubeSelecionavel(clubeId: 3);

        Assert.IsType<ForbidResult>(resposta);
        Assert.True((await ctx.Clubes.FindAsync(3))!.Selecionavel);
    }

    [Fact]
    public async Task Clube_inexistente_nao_quebra_a_tela()
    {
        using var ctx = Cenario();
        Assert.IsType<NotFoundResult>(await Admin(ctx, usuarioLogadoId: 9).AlternarClubeSelecionavel(clubeId: 9999));
    }

    // ── Cidade: rótulo e agrupamento ──────────────────────────────────────────────────

    [Fact]
    public void O_rotulo_traz_a_cidade_e_a_uf()
    {
        var clube = new Clube { Nome = "Arena Central", Cidade = new Cidade { Nome = "Gravataí", Estado = "RS" } };

        Assert.Equal("Arena Central · Gravataí/RS", CatalogoLocais.RotuloComCidade(clube));
    }

    [Fact]
    public void Clube_sem_cidade_fica_so_com_o_nome()
    {
        // O passivo é grande: clube que nasceu antes de a cidade ser perguntada não pode
        // virar "Fulano · " na tela.
        Assert.Equal("Batata", CatalogoLocais.RotuloComCidade(new Clube { Nome = "Batata" }));
    }

    [Fact]
    public void Cidade_sem_uf_nao_deixa_barra_solta()
    {
        var clube = new Clube { Nome = "Arena X", Cidade = new Cidade { Nome = "Canoas" } };

        Assert.Equal("Arena X · Canoas", CatalogoLocais.RotuloComCidade(clube));
    }

    [Fact]
    public async Task Sem_cidade_e_um_grupo_de_verdade_e_vai_por_ultimo()
    {
        // Escondê-lo seria pior: é onde está a maior parte do passivo, e é justamente ele que
        // precisa ser varrido.
        using var ctx = Cenario();

        var grupos = CatalogoLocais.AgrupadosPorCidade(await ctx.Clubes.ParaEscolher().ToListAsync()).ToList();

        Assert.Equal(CatalogoLocais.SemCidade, grupos.Last().Key);
        Assert.Equal(2, grupos.Last().Count());        // "Clube Teste CSRF..." e "Sem cidade nenhuma"
        Assert.Equal(new[] { "Gravataí/RS", "Porto Alegre/RS" }, grupos.Take(2).Select(g => g.Key));
    }

    [Fact]
    public async Task O_agrupamento_nao_perde_nem_repete_clube()
    {
        using var ctx = Cenario();
        var lista = await ctx.Clubes.ParaEscolher().ToListAsync();

        var agrupados = CatalogoLocais.AgrupadosPorCidade(lista).SelectMany(g => g).ToList();

        Assert.Equal(lista.Count, agrupados.Count);
        Assert.Equal(lista.Select(c => c.Id).OrderBy(id => id), agrupados.Select(c => c.Id).OrderBy(id => id));
    }
}
