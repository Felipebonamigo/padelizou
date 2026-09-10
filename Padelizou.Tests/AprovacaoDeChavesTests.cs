using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A tela de aprovação das chaves: pedido do Felipe, 22/08/2026 — "colocar uma tela antes de
// ... dizer como liberar das chaves. Somente administrador, organizador, e eu teremos acesso".
//
// O sorteio (GerarChaves) continua gravando tudo igual; o que muda é que ele para em
// "Chaves em Aprovação" em vez de ir direto pra "Fase de Grupos" — e ninguém fora de quem
// organiza/administra enxerga nada até alguém aprovar (AprovarChaves).
public class AprovacaoDeChavesTests
{
    [Fact]
    public async Task Sortear_nao_avisa_ninguem_ainda()
    {
        // O aviso "as chaves saíram" tem que esperar a aprovação — avisar no sorteio
        // entregaria a notícia antes de qualquer aprovação existir.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var push = Substitute.For<IPushNotificationService>();

        await TestInfra.NovoTorneiosController(ctx, org.Id, push: push).GerarChaves(torneio.Id);

        await push.DidNotReceive().EnviarParaJogadorAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Aprovar_libera_o_torneio_e_avisa_os_jogadores()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var push = Substitute.For<IPushNotificationService>();
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id, push: push);
        await controller.GerarChaves(torneio.Id);

        await controller.AprovarChaves(torneio.Id);

        Assert.Equal("Fase de Grupos", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
        await push.Received().EnviarParaJogadorAsync(
            Arg.Any<int>(), "Chaves do Torneio de Teste saíram!", Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task So_organizador_ou_admin_aprova()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var intruso = new Jogador { Nome = "Intruso", Cpf = "99900000088" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var resultado = await TestInfra.NovoTorneiosController(ctx, intruso.Id).AprovarChaves(torneio.Id);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(resultado);
        Assert.Equal(AprovacaoDeChaves.Pendente, (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    [Fact]
    public async Task Nao_da_pra_aprovar_torneio_que_nao_esta_esperando_aprovacao()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6); // ainda "Chaves em Sorteio"

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.AprovarChaves(torneio.Id);

        Assert.Equal("Chaves em Sorteio", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Desfazer_apaga_grupos_e_jogos_e_volta_pro_sorteio()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        await controller.DesfazerSorteio(torneio.Id);

        Assert.Equal("Chaves em Sorteio", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
        Assert.Empty(await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync());
        Assert.Empty(await ctx.Set<GrupoTorneio>().Where(g => g.CategoriaId == categoria.Id).ToListAsync());

        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToListAsync();
        Assert.All(duplas, d => Assert.Null(d.GrupoTorneioId));
        Assert.All(duplas, d => Assert.Null(d.Grupo));
    }

    [Fact]
    public async Task Desfazer_e_sortear_de_novo_funciona()
    {
        // A prova de que desfazer devolve o torneio pra um estado que sorteia de novo sem
        // sobra nenhuma do sorteio anterior.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);
        await controller.DesfazerSorteio(torneio.Id);

        await controller.GerarChaves(torneio.Id);

        Assert.Equal(AprovacaoDeChaves.Pendente, (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
        Assert.Equal(6, await ctx.Partidas.CountAsync(p => p.CategoriaId == categoria.Id));
    }

    [Fact]
    public async Task So_organizador_ou_admin_desfaz()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var intruso = new Jogador { Nome = "Intruso", Cpf = "99900000077" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var resultado = await TestInfra.NovoTorneiosController(ctx, intruso.Id).DesfazerSorteio(torneio.Id);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(resultado);
        Assert.Equal(AprovacaoDeChaves.Pendente, (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    [Fact]
    public async Task Nao_da_pra_desfazer_depois_de_aprovado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);
        await controller.AprovarChaves(torneio.Id);

        await controller.DesfazerSorteio(torneio.Id);

        Assert.Equal("Fase de Grupos", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
        Assert.Equal(6, await ctx.Partidas.CountAsync(p => p.CategoriaId == categoria.Id));
    }

    [Fact]
    public async Task Nao_da_pra_desfazer_com_jogo_ja_comecado()
    {
        // Não devia acontecer (só organizador/admin chega no Jogos durante a espera), mas se
        // um placar já foi lançado, desfazer apagaria o jogo em vez de recusar.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        var umJogo = await ctx.Partidas.FirstAsync(p => p.CategoriaId == categoria.Id);
        umJogo.Status = "AoVivo";
        await ctx.SaveChangesAsync();

        await controller.DesfazerSorteio(torneio.Id);

        Assert.Equal(AprovacaoDeChaves.Pendente, (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
        Assert.Equal(6, await ctx.Partidas.CountAsync(p => p.CategoriaId == categoria.Id));
    }

    [Fact]
    public async Task Jogos_fica_escondido_de_quem_nao_pode_aprovar_enquanto_pendente()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var qualquerUm = new Jogador { Nome = "Qualquer Um", Cpf = "99900000066" };
        ctx.Jogadores.Add(qualquerUm);
        await ctx.SaveChangesAsync();
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var controller = TestInfra.NovoTorneiosController(ctx, qualquerUm.Id);
        var resultado = await controller.Jogos(torneio.Id, null, null);

        var redirect = Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToActionResult>(resultado);
        Assert.Equal("Details", redirect.ActionName);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    // ⚠️ 🗣️ Felipe, 09/09/2026, num print de PRODUÇÃO em navegação ANÔNIMA: *"outro erro grave
    // apareceu as partidas agendadas sem ter aprovado as chaves, cuide isso e nao deixe q nada
    // vaze sem ser publicado"*.
    //
    // 🕳️ O PORTÃO EXISTIA NA PORTA ERRADA. A ação `Jogos` (a página dedicada) conferia a aprovação
    // desde 22/08 — e é o que os dois testes acima medem. Mas a MESMA lista de jogos é embutida na
    // aba "Jogos" do `Details`, e lá a única condição era `Status != "Inscrições Abertas"`.
    // "Chaves em Aprovação" passa nessa condição. Ou seja: o portão da frente estava trancado e a
    // porta dos fundos, aberta pra qualquer visitante — inclusive deslogado.
    //
    // ⚠️ A LIÇÃO: uma régua de visibilidade escrita numa AÇÃO protege aquela ação, não o DADO. Aqui
    // ela passou a morar no método que carrega o dado (`CarregarViewBagJogosAsync`), que é por onde
    // as duas telas passam — a única forma de as duas não divergirem de novo.
    [Fact]
    public async Task Details_nao_mostra_jogo_nenhum_enquanto_as_chaves_esperam_aprovacao()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        Assert.Equal(AprovacaoDeChaves.Pendente, (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
        Assert.True(await ctx.Partidas.AnyAsync(p => p.TorneioId == torneio.Id), "o sorteio não gerou jogo");

        // DESLOGADO — o caso do print: navegação anônima em padelizou.com.br.
        var anonimo = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 0);
        var resultado = await anonimo.Details(torneio.Id, null, null);

        var view = Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(resultado);

        // ⚠️ TODAS as listas, e não só a de agendadas: "não exiba nada até publicar a chave"
        // (Felipe). Ao Vivo, Finalizadas e a PRÉVIA das próximas fases contam o mesmo torneio.
        foreach (var chave in new[] { "Agendadas", "AoVivo", "Finalizadas", "JogosQueVem" })
        {
            var lista = view.ViewData[chave] as System.Collections.IEnumerable;
            Assert.True(lista == null || !lista.Cast<object>().Any(),
                $"a aba Jogos do Details entregou \"{chave}\" com as chaves ainda esperando aprovação");
        }
    }

    [Fact]
    public async Task Details_mostra_os_jogos_pro_organizador_enquanto_pendente()
    {
        // A contrapartida: quem organiza precisa ver pra poder conferir e aprovar. Esconder dele
        // transformaria a aprovação num carimbo às cegas.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        var view = Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(
            await controller.Details(torneio.Id, null, null));

        var agendadas = view.ViewData["Agendadas"] as System.Collections.IEnumerable;
        Assert.True(agendadas != null && agendadas.Cast<object>().Any(),
            "o organizador precisa ver a grade pra decidir se aprova");
    }

    [Fact]
    public async Task Depois_de_aprovado_o_jogo_aparece_pra_quem_nao_esta_logado()
    {
        // E o fecho do ciclo: aprovar é o que solta. Sem este teste, "esconder sempre" passaria.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var doOrganizador = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doOrganizador.GerarChaves(torneio.Id);
        await doOrganizador.AprovarChaves(torneio.Id);

        var view = Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(
            await TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 0).Details(torneio.Id, null, null));

        var agendadas = view.ViewData["Agendadas"] as System.Collections.IEnumerable;
        Assert.True(agendadas != null && agendadas.Cast<object>().Any(),
            "depois de aprovada, a chave é pública");
    }

    [Fact]
    public async Task Jogos_continua_visivel_pro_organizador_enquanto_pendente()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        var resultado = await controller.Jogos(torneio.Id, null, null);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(resultado);
    }
}
