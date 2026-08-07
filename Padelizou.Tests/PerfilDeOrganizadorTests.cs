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
    public void Jogador_comum_nao_cria_torneio_OFICIAL()
    {
        Assert.False(PermissaoDeOrganizador.PodeCriarTorneio(
            new Jogador { Id = 1, Nome = "Comum", Cpf = "1" }, FormatoDoTorneio.Padrao));
    }

    [Theory]
    [InlineData(FormatoDoTorneio.Americano)]
    [InlineData(FormatoDoTorneio.AmericanoDeDuplas)]
    public void Jogador_comum_CRIA_americano(string formato)
    {
        // O Americano é o rodízio de sábado: gente conhecida, criado na sexta à noite. Exigir
        // liberação poria o Felipe no meio do combinado de um grupo de amigos.
        //
        // E é seguro porque criar não avisa mais ninguém — o "novo torneio aberto" saiu da
        // criação e foi pra aprovação. Um Americano inventado fica no link de quem criou.
        Assert.True(PermissaoDeOrganizador.PodeCriarTorneio(
            new Jogador { Id = 1, Nome = "Comum", Cpf = "1" }, formato));
    }

    [Fact]
    public void Quem_tem_o_perfil_cria_o_oficial()
    {
        Assert.True(PermissaoDeOrganizador.PodeCriarTorneio(
            new Jogador { Id = 1, Nome = "Organizador", Cpf = "1", IsOrganizadorTorneio = true },
            FormatoDoTorneio.Padrao));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Admin_cria_o_oficial_sem_precisar_do_perfil(bool raiz, bool geral)
    {
        // É ele quem socorre organizador travado no dia do jogo. Depender do perfil que ele
        // mesmo distribui seria um nó que só se desata pelo banco.
        Assert.True(PermissaoDeOrganizador.PodeCriarOficial(
            new Jogador { Id = 1, Nome = "Admin", Cpf = "1", IsAdminRaiz = raiz, IsAdminGeral = geral }));
    }

    [Theory]
    [InlineData(FormatoDoTorneio.Padrao)]
    [InlineData(FormatoDoTorneio.Americano)]
    public void Ninguem_logado_nao_cria_nada(string formato)
    {
        // Nem o Americano: livre é pra quem TEM conta. Sem conta não há a quem responsabilizar
        // pelo que for criado.
        Assert.False(PermissaoDeOrganizador.PodeCriarTorneio(null, formato));
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
    public async Task Sem_perfil_a_tela_ABRE_e_diz_que_o_oficial_esta_trancado()
    {
        // A tela não recusa mais na porta: mandar embora quem só queria criar um Americano
        // seria fechar a porta de entrada do app na cara de quem chegou.
        using var ctx = ContextoCom(new Jogador { Id = 1, Nome = "Comum", Cpf = "1" });

        var view = Assert.IsType<ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1).Create());

        Assert.Equal(false, view.ViewData["PodeCriarOficial"]);
    }

    [Fact]
    public async Task Sem_perfil_o_OFICIAL_por_POST_feito_a_mao_e_recusado()
    {
        // A tela desabilita o formato — mas quem quer lotar o sistema de torneio é exatamente
        // quem não usa o formulário.
        using var ctx = ContextoCom(new Jogador { Id = 1, Nome = "Comum", Cpf = "1" });

        var resultado = await TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1)
            .Create(TorneioValido(), new[] { 3 }, null, null, null, null);

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Empty(ctx.Torneios);
    }

    [Fact]
    public async Task Sem_perfil_o_AMERICANO_e_criado_normalmente()
    {
        using var ctx = ContextoCom(new Jogador { Id = 1, Nome = "Comum", Cpf = "1" });

        var americano = TorneioValido();
        americano.Formato = FormatoDoTorneio.Americano;

        await TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1)
            .Create(americano, new[] { 3 }, null, null, null, null);

        var criado = Assert.Single(ctx.Torneios);
        // ...e ele também espera aprovação pra aparecer. É essa trava que torna a liberdade
        // de criar segura: sem ela, Americano livre seria porta aberta pra spam.
        Assert.Null(criado.AprovadoEm);
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
