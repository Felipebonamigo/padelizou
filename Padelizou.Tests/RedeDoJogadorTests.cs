using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// QUEM TE SEGUE E QUEM VOCÊ SEGUE.
//
// Seguir existe desde o começo do sistema — é o que faz alguém receber aviso dos torneios e
// dos mata-matas de quem acompanha — e nunca teve NENHUMA tela: dava pra apertar o botão no
// perfil de alguém e nunca mais saber quem estava dos dois lados.
public class RedeDoJogadorTests
{
    private static DbPadelContext Cenario()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.AddRange(
            new Jogador { Id = 1, Nome = "Dono da Rede", Cpf = "1" },
            new Jogador { Id = 2, Nome = "Quem Me Segue", Cpf = "2" },
            new Jogador { Id = 3, Nome = "Quem Eu Sigo", Cpf = "3" },
            new Jogador { Id = 4, Nome = "Reservado", Cpf = "4", PerfilPrivado = true });

        // 2 me segue; eu sigo 3. Ninguém aqui é recíproco de propósito — é o caso que separa
        // "seguir de volta" de "vocês se seguem".
        ctx.SeguidoresJogador.AddRange(
            new SeguidorJogador { SeguidorId = 2, SeguidoId = 1 },
            new SeguidorJogador { SeguidorId = 1, SeguidoId = 3 });
        ctx.SaveChanges();
        return ctx;
    }

    private static async Task<RedeDoJogadorVM> RedeAsync(DbPadelContext ctx, int deQuem, int? logadoComo, string? aba = null)
    {
        var view = Assert.IsType<ViewResult>(
            await TestInfra.NovoJogadoresController(ctx, logadoComo).Rede(deQuem, aba));
        return Assert.IsType<RedeDoJogadorVM>(view.Model);
    }

    [Fact]
    public async Task As_duas_listas_saem_separadas()
    {
        var rede = await RedeAsync(Cenario(), deQuem: 1, logadoComo: 1);

        Assert.Equal(new[] { 2 }, rede.Seguidores.Select(p => p.Id));
        Assert.Equal(new[] { 3 }, rede.Seguindo.Select(p => p.Id));
        Assert.True(rede.SouEu);
    }

    [Fact]
    public async Task Quem_me_segue_e_eu_nao_sigo_de_volta_fica_marcado()
    {
        // É o que decide se a linha ganha o botão "Seguir de volta" ou o selo "vocês se
        // seguem" — oferecer o botão pra quem eu já sigo seria um clique que não faz nada.
        var ctx = Cenario();

        var rede = await RedeAsync(ctx, deQuem: 1, logadoComo: 1);
        Assert.False(rede.Seguidores.Single().EuSigo);

        ctx.SeguidoresJogador.Add(new SeguidorJogador { SeguidorId = 1, SeguidoId = 2 });
        ctx.SaveChanges();

        var depois = await RedeAsync(ctx, deQuem: 1, logadoComo: 1);
        Assert.True(depois.Seguidores.Single().EuSigo);
    }

    [Fact]
    public async Task A_rede_dos_OUTROS_da_pra_ver_mas_sem_ser_dono()
    {
        // Ver é público (o perfil e o histórico já são). O que muda é que não aparece botão:
        // seguir de volta é ação sobre uma relação que não é minha.
        var rede = await RedeAsync(Cenario(), deQuem: 1, logadoComo: 3);

        Assert.False(rede.SouEu);
        Assert.Single(rede.Seguidores);
    }

    [Fact]
    public async Task Visitante_deslogado_ve_a_lista_e_nao_segue_ninguem()
    {
        // Sem isto, um `int.Parse` num claim inexistente derrubaria a página pra quem chegou
        // pelo link compartilhado.
        var rede = await RedeAsync(Cenario(), deQuem: 1, logadoComo: null);

        Assert.Single(rede.Seguidores);
        Assert.All(rede.Seguidores, p => Assert.False(p.EuSigo));
    }

    [Fact]
    public async Task Perfil_privado_nao_expoe_a_rede_pros_outros()
    {
        // ⚠️ A rede seria uma porta lateral pro que o perfil privado acabou de fechar.
        var resultado = await TestInfra.NovoJogadoresController(Cenario(), 1).Rede(4, null);

        var redirect = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Perfil", redirect.ActionName);
    }

    [Fact]
    public async Task O_DONO_do_perfil_privado_ve_a_propria_rede()
    {
        var view = Assert.IsType<ViewResult>(
            await TestInfra.NovoJogadoresController(Cenario(), 4).Rede(4, null));

        Assert.True(Assert.IsType<RedeDoJogadorVM>(view.Model).SouEu);
    }

    [Fact]
    public async Task A_aba_de_abertura_vem_do_link()
    {
        // O card "você segue N" tem que cair na lista de quem eu sigo, não na de seguidores.
        var ctx = Cenario();

        Assert.True((await RedeAsync(ctx, 1, 1, "seguindo")).AbrirEmSeguindo);
        Assert.False((await RedeAsync(ctx, 1, 1, null)).AbrirEmSeguindo);
    }

    [Fact]
    public async Task Seguir_pela_tela_da_rede_volta_pra_rede()
    {
        // Sem isso, "seguir de volta" jogava a pessoa no perfil de quem ela acabou de seguir
        // e ela perdia o lugar na lista que estava percorrendo.
        var resultado = await TestInfra.NovoJogadoresController(Cenario(), 1)
            .Seguir(2, voltarParaRede: 1);

        var redirect = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal("Rede", redirect.ActionName);
        Assert.Equal(1, redirect.RouteValues!["id"]);
    }

    [Fact]
    public async Task Jogador_que_nao_existe_da_nao_encontrado()
    {
        Assert.IsType<NotFoundResult>(await TestInfra.NovoJogadoresController(Cenario(), 1).Rede(999, null));
    }
}
