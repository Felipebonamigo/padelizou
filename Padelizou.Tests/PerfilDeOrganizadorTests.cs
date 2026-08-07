using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// DUAS TRAVAS, DUAS PERGUNTAS (decisões do Felipe, 07/08/2026):
//   1. PERFIL — quem pode criar torneio. Liberado pessoa a pessoa, no painel.
//   2. APROVAÇÃO — qual torneio APARECE. Todo torneio, sempre.
//
// Nasceram do medo certo: *"tenho medo que qualquer pessoa chegue, crie torneio e lote de
// torneios"*. E o estrago não seria uma lista suja — cada torneio dispara aviso pra base
// inteira, então torneio inventado é spam no celular de todo mundo.
public class PerfilDeOrganizadorTests
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

    private static DbPadelContext ContextoCom(Jogador quemCria)
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(quemCria);
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

    // ── A regra pura ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Jogador_comum_nao_cria_torneio()
    {
        Assert.False(PermissaoDeOrganizador.PodeCriarTorneio(
            new Jogador { Id = 1, Nome = "Comum", Cpf = "1" }));
    }

    [Fact]
    public void Quem_tem_o_perfil_cria()
    {
        Assert.True(PermissaoDeOrganizador.PodeCriarTorneio(
            new Jogador { Id = 1, Nome = "Organizador", Cpf = "1", IsOrganizadorTorneio = true }));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Admin_cria_sem_precisar_do_perfil(bool raiz, bool geral)
    {
        // É ele quem socorre organizador travado no dia do jogo. Depender do perfil que ele
        // mesmo distribui seria um nó que só se desata pelo banco.
        Assert.True(PermissaoDeOrganizador.PodeCriarTorneio(
            new Jogador { Id = 1, Nome = "Admin", Cpf = "1", IsAdminRaiz = raiz, IsAdminGeral = geral }));
    }

    [Fact]
    public void Ninguem_logado_nao_cria()
    {
        Assert.False(PermissaoDeOrganizador.PodeCriarTorneio(null));
    }

    // ── A vitrine ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Torneio_sem_aprovacao_fica_fora_da_vitrine()
    {
        var novo = new Torneio { Nome = "x", Codigo = "x" };

        Assert.False(PermissaoDeOrganizador.ApareceNaVitrine(novo));
    }

    [Fact]
    public void Aprovado_aparece()
    {
        var aprovado = new Torneio { Nome = "x", Codigo = "x", AprovadoEm = DateTime.Now };

        Assert.True(PermissaoDeOrganizador.ApareceNaVitrine(aprovado));
    }

    [Fact]
    public void Aprovado_mas_OCULTO_continua_fora()
    {
        // As duas regras convivem: aprovar não desfaz a escolha do organizador de não
        // aparecer. Sem isto, aprovar um torneio restrito o jogaria na vitrine.
        var aprovadoOculto = new Torneio
        {
            Nome = "x", Codigo = "x", AprovadoEm = DateTime.Now, Oculto = true,
        };

        Assert.False(PermissaoDeOrganizador.ApareceNaVitrine(aprovadoOculto));
    }

    // ── A porta, no servidor ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sem_perfil_a_tela_de_criar_nem_abre()
    {
        using var ctx = ContextoCom(new Jogador { Id = 1, Nome = "Comum", Cpf = "1" });

        var resultado = await TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1).Create();

        var redir = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Index", redir.ActionName);
    }

    [Fact]
    public async Task Sem_perfil_o_POST_feito_a_mao_tambem_e_recusado()
    {
        // A tela some do menu e recusa na porta — mas quem quer lotar o sistema de torneio é
        // exatamente quem não usa o formulário.
        using var ctx = ContextoCom(new Jogador { Id = 1, Nome = "Comum", Cpf = "1" });

        var resultado = await TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1)
            .Create(TorneioValido(), new[] { 3 }, null, null, null, null);

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Empty(ctx.Torneios);
    }

    [Fact]
    public async Task Com_perfil_o_torneio_nasce_ESPERANDO_aprovacao()
    {
        using var ctx = ContextoCom(new Jogador
        {
            Id = 1, Nome = "Organizador", Cpf = "1", IsOrganizadorTorneio = true,
        });

        await TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1)
            .Create(TorneioValido(), new[] { 3 }, null, null, null, null);

        var criado = Assert.Single(ctx.Torneios);
        Assert.Null(criado.AprovadoEm);
        Assert.False(PermissaoDeOrganizador.ApareceNaVitrine(criado));
    }

    [Fact]
    public async Task Criar_torneio_NAO_avisa_mais_a_base_inteira()
    {
        // O aviso "novo torneio aberto" mudou de lugar: sai da APROVAÇÃO. Avisar na criação
        // entregaria à base justamente o torneio que ninguém olhou ainda.
        //
        // Quem recebe push aqui são os ADMINS (o torneio entrou na fila deles) — nunca os 71
        // jogadores com "quero saber de torneio novo".
        using var ctx = ContextoCom(new Jogador
        {
            Id = 1, Nome = "Organizador", Cpf = "1", IsOrganizadorTorneio = true,
        });
        ctx.Jogadores.Add(new Jogador
        {
            Id = 2, Nome = "Jogador Comum", Cpf = "2", NotificarTorneiosAbertos = true,
        });
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1, push: push);

        await controller.Create(TorneioValido(), new[] { 3 }, null, null, null, null);

        await push.DidNotReceive().EnviarParaJogadorAsync(
            2, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }
}
