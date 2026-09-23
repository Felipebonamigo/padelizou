using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using padelizou.Models;
using Padelizou.Controllers;
using padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// O pacote "nós registramos os resultados" voltou a ser cobrado POR JOGO em 23/09/2026
// (R$ 12), no lugar dos 10% das inscrições. 🗣️ Felipe: *"mude o sistema, para que seja 12
// reais por jogo, no lugar de 10% para marcarmos os placares"*.
//
// ⚠️ O QUE ESTE ARQUIVO SEGURA NÃO É O NÚMERO — é o que o número passou a exigir. Enquanto o
// preço era percentual, a CONTAGEM DE JOGOS só alimentava o custo estimado num painel interno,
// e errar ali era uma linha torta pra quem responde. Agora ela É o preço: contar zero cobra o
// mínimo, contar demais cobra a mais. Os dois defeitos que isso desenterrou estão aqui.
public class PrecoPorJogoDoRegistroTests
{
    private static AdminController NovoAdminController(DbPadelContext ctx, int usuarioLogadoId)
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
        controller.TempData = new TempDataDictionary(
            controller.HttpContext, Substitute.For<ITempDataProvider>());
        return controller;
    }

    // Torneio que ACEITA pedido: com data longe o bastante pra montar equipe.
    private static (Torneio torneio, Categoria categoria, Jogador organizador) TorneioQuePodePedir(
        DbPadelContext ctx, int qtdDuplas, string formato = FormatoDoTorneio.Padrao)
    {
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas);
        torneio.DataInicio = DateTime.Today.AddDays(30);
        torneio.Formato = formato;
        torneio.PrecoInscricao = 150m;
        ctx.SaveChanges();
        return (torneio, categoria, organizador);
    }

    [Fact]
    public async Task Pedido_novo_congela_o_preco_POR_JOGO_e_nao_o_percentual()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TorneioQuePodePedir(ctx, 12);
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        await controller.SolicitarRegistroResultados(torneio.Id, "meu telefone é 51 9...");

        var pedido = await ctx.SolicitacoesRegistroResultados.SingleAsync();
        Assert.Null(pedido.PercentualCotado);          // a régua percentual saiu de cena
        Assert.Equal(12m, pedido.PrecoPorJogoCotado);
        Assert.Equal(500m, pedido.ValorMinimoCotado);
        Assert.Equal(19, pedido.JogosPrevistos);       // 12 duplas = 19 jogos
    }

    [Fact]
    public async Task Pedido_de_americano_individual_nao_nasce_com_ZERO_jogo()
    {
        // O Americano individual inscreve PESSOA, não dupla, e a contagem olhava só
        // `Categoria.Duplas`. O pedido nascia com zero jogo — que pelo percentual era só uma
        // linha vazia no painel, e por jogo é o mínimo cobrado num torneio de 60 partidas.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TorneioQuePodePedir(ctx, 0, FormatoDoTorneio.Americano);

        for (int i = 0; i < 16; i++)
        {
            var jogador = TestInfra.NovoJogador(500 + i);
            ctx.Jogadores.Add(jogador);
            ctx.InscricoesAmericanas.Add(new InscricaoAmericana { Categoria = categoria, Jogador = jogador });
        }
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await controller.SolicitarRegistroResultados(torneio.Id, null);

        var pedido = await ctx.SolicitacoesRegistroResultados.SingleAsync();
        Assert.Equal(60, pedido.JogosPrevistos);       // 16 pessoas, cada um com cada um
    }

    [Fact]
    public async Task Chave_direta_nao_infla_os_jogos_do_pedido()
    {
        // Chave direta é mata-mata puro: 8 duplas = 7 jogos. Pela régua dos grupos daria 10,
        // e a diferença agora é dinheiro cobrado a mais.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TorneioQuePodePedir(ctx, 8);
        categoria.ChaveDireta = true;
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await controller.SolicitarRegistroResultados(torneio.Id, null);

        var pedido = await ctx.SolicitacoesRegistroResultados.SingleAsync();
        Assert.Equal(7, pedido.JogosPrevistos);
    }

    [Fact]
    public async Task O_painel_do_admin_conta_os_jogos_AO_VIVO_e_nao_os_do_dia_do_pedido()
    {
        // ⚠️ O DEFEITO QUE A MUDANÇA DE PREÇO DESENTERROU. O pedido é feito com no mínimo 7
        // dias de antecedência, ou seja, com as inscrições ABERTAS — quase sempre vazias. O
        // número de jogos congelava ali, e o valor combina-se depois, na resposta. Pelo
        // percentual isso não aparecia (a base de pessoas já era contada ao vivo); por jogo,
        // cotar pelo número do dia do pedido cobraria o mínimo de todo mundo.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TorneioQuePodePedir(ctx, 0);
        ctx.Jogadores.Add(new Jogador { Id = 777, Nome = "Raiz", Cpf = "777", IsAdminRaiz = true });
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await controller.SolicitarRegistroResultados(torneio.Id, null);
        Assert.Null((await ctx.SolicitacoesRegistroResultados.SingleAsync()).JogosPrevistos);

        // O torneio enche DEPOIS do pedido — é o caso normal, não o excepcional.
        for (int i = 0; i < 16; i++)
        {
            var j1 = TestInfra.NovoJogador(900 + i * 2);
            var j2 = TestInfra.NovoJogador(901 + i * 2);
            ctx.Jogadores.AddRange(j1, j2);
            ctx.Duplas.Add(new Dupla { Categoria = categoria, Jogador1 = j1, Jogador2 = j2 });
        }
        await ctx.SaveChangesAsync();

        var admin = NovoAdminController(ctx, 777);
        await admin.RegistroResultados();

        var aoVivo = Assert.IsType<Dictionary<int, int>>(admin.ViewData["JogosAoVivo"]);
        Assert.Equal(21, aoVivo[torneio.Id]);          // 16 duplas = 21 jogos
    }

    [Fact]
    public async Task Dupla_na_lista_de_espera_nao_entra_na_conta()
    {
        // Quem está na espera não entra no sorteio (Services/ForaDoSorteio), logo não gera
        // jogo — e jogo que não existe não se cobra.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TorneioQuePodePedir(ctx, 12);
        var naEspera = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).Take(4).ToListAsync();
        foreach (var d in naEspera) d.EmListaDeEspera = true;
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await controller.SolicitarRegistroResultados(torneio.Id, null);

        var pedido = await ctx.SolicitacoesRegistroResultados.SingleAsync();
        Assert.Equal(10, pedido.JogosPrevistos);       // 8 duplas de verdade, não 12
    }
}
