using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using padelizou.Controllers;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// VÁRIOS JOGOS NUM LANÇAMENTO SÓ — pedido do Felipe (20/08/2026).
//
// "quando elas jogam mais de um jogo, tem como anotar aqui?" Não tinha: a tela gravava UM jogo
// e voltava pro ranking, então o segundo set custava refazer data, os quatro nomes e o
// vencedor. E veio a segunda metade do pedido logo depois: "no dia podem ter vários jogos e
// até com as duplas diferentes" — ou seja, cada jogo escolhe os SEUS quatro. O que é do DIA
// (data e local) continua sendo perguntado uma vez só.
//
// ⚠️ O QUE ESTE ARQUIVO MAIS PROTEGE É A ATOMICIDADE. A recusa desta tela é TempData +
// RedirectToAction, isto é, formulário novo em branco. Se o jogo 1 fosse gravado e o jogo 2
// recusado, a pessoa voltaria sem saber o que entrou — e relançaria os dois, duplicando o
// primeiro no ranking. Ou entram todos, ou nenhum.
public class VariosJogosNumLancamentoTests
{
    private static GruposController Controller(DbPadelContext ctx, int euId)
    {
        var controller = new GruposController(
            ctx, new SessaoGrupoService(ctx),
            Substitute.For<IPushNotificationService>(), NullLogger<GruposController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, euId.ToString()) }, "Teste")),
                },
            },
        };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        controller.Url = TestInfra.UrlDeTeste();
        return controller;
    }

    // Seis: quatro pro primeiro jogo e folga pra trocar as duplas no segundo.
    private static async Task<(GrupoPrivado grupo, List<Jogador> membros)> MontarAsync(DbPadelContext ctx)
    {
        var membros = Enumerable.Range(1, 6)
            .Select(i => new Jogador { Nome = $"Jogador {i}", Cpf = $"7770000000{i}", Login = $"jog{i}" })
            .ToList();
        ctx.Jogadores.AddRange(membros);

        var clube = new Clube { Nome = "Wallau" };
        ctx.Clubes.Add(clube);
        await ctx.SaveChangesAsync();

        var grupo = new GrupoPrivado
        {
            Nome = "Sub 90", CodigoConvite = "SUB090", AdministradorId = membros[0].Id, ClubeId = clube.Id,
        };
        ctx.GruposPrivados.Add(grupo);
        await ctx.SaveChangesAsync();

        ctx.JogadoresGrupo.AddRange(membros.Select(m =>
            new JogadorGrupo { GrupoId = grupo.Id, JogadorId = m.Id, PontuacaoInterna = 0 }));
        await ctx.SaveChangesAsync();

        return (grupo, membros);
    }

    private static ResultadoLancado Jogo(
        Jogador a, Jogador b, Jogador c, Jogador d, int vencedor, int? games1 = null, int? games2 = null) =>
        new()
        {
            Dupla1Jogador1Id = a.Id, Dupla1Jogador2Id = b.Id,
            Dupla2Jogador1Id = c.Id, Dupla2Jogador2Id = d.Id,
            VencedorLado = vencedor, GamesDupla1 = games1, GamesDupla2 = games2,
        };

    [Fact]
    public async Task Dois_jogos_num_POST_viram_duas_linhas()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, m) = await MontarAsync(ctx);

        await Controller(ctx, m[0].Id).RegistrarJogo(
            grupo.Id, DateTime.Today, m[0].Id, m[1].Id, m[2].Id, m[3].Id, 1, 6, 4, null,
            maisResultados: new List<ResultadoLancado> { Jogo(m[0], m[1], m[2], m[3], 2, 3, 6) });

        var jogos = await ctx.JogosSemanais.OrderBy(j => j.Id).ToListAsync();
        Assert.Equal(2, jogos.Count);
        Assert.Equal(ResultadoDoJogoSemanal.Dupla1, jogos[0].VencedorLado);
        Assert.Equal(ResultadoDoJogoSemanal.Dupla2, jogos[1].VencedorLado);
    }

    // A segunda metade do pedido: as duplas podem mudar de um jogo pro outro.
    [Fact]
    public async Task O_segundo_jogo_pode_ter_outras_duplas()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, m) = await MontarAsync(ctx);

        await Controller(ctx, m[0].Id).RegistrarJogo(
            grupo.Id, DateTime.Today, m[0].Id, m[1].Id, m[2].Id, m[3].Id, 1, null, null, null,
            // Mesma gente, pares trocados — e o jogador 5 entrando no lugar do 4.
            maisResultados: new List<ResultadoLancado> { Jogo(m[0], m[2], m[1], m[4], 1) });

        var jogos = await ctx.JogosSemanais.OrderBy(j => j.Id).ToListAsync();
        Assert.Equal(new int?[] { m[0].Id, m[1].Id, m[2].Id, m[3].Id },
            new[] { jogos[0].Dupla1Jogador1Id, jogos[0].Dupla1Jogador2Id, jogos[0].Dupla2Jogador1Id, jogos[0].Dupla2Jogador2Id });
        Assert.Equal(new int?[] { m[0].Id, m[2].Id, m[1].Id, m[4].Id },
            new[] { jogos[1].Dupla1Jogador1Id, jogos[1].Dupla1Jogador2Id, jogos[1].Dupla2Jogador1Id, jogos[1].Dupla2Jogador2Id });
    }

    [Fact]
    public async Task Data_e_local_valem_pro_lancamento_inteiro()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, m) = await MontarAsync(ctx);
        var quinta = new DateTime(2026, 8, 20);

        await Controller(ctx, m[0].Id).RegistrarJogo(
            grupo.Id, quinta, m[0].Id, m[1].Id, m[2].Id, m[3].Id, 1, null, null, null,
            maisResultados: new List<ResultadoLancado> { Jogo(m[0], m[2], m[1], m[3], 2) });

        var jogos = await ctx.JogosSemanais.ToListAsync();
        Assert.All(jogos, j => Assert.Equal(quinta, j.DataJogo));
        Assert.All(jogos, j => Assert.Equal(grupo.ClubeId, j.ClubeId));
    }

    // ===================== OU ENTRAM TODOS, OU NENHUM =====================

    [Fact]
    public async Task Jogo_2_sem_vencedor_nao_deixa_o_jogo_1_gravado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, m) = await MontarAsync(ctx);

        var controller = Controller(ctx, m[0].Id);
        await controller.RegistrarJogo(
            grupo.Id, DateTime.Today, m[0].Id, m[1].Id, m[2].Id, m[3].Id, 1, null, null, null,
            maisResultados: new List<ResultadoLancado> { Jogo(m[0], m[2], m[1], m[3], vencedor: 9) });

        Assert.Empty(await ctx.JogosSemanais.ToListAsync());
        // E a recusa diz QUAL jogo: com dois na tela, "marque quem venceu" sozinho não ajuda.
        Assert.StartsWith("Jogo 2:", controller.TempData["Erro"] as string ?? "");
    }

    [Fact]
    public async Task Placar_do_jogo_2_que_contradiz_o_vencedor_derruba_o_lancamento_todo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, m) = await MontarAsync(ctx);

        var controller = Controller(ctx, m[0].Id);
        await controller.RegistrarJogo(
            grupo.Id, DateTime.Today, m[0].Id, m[1].Id, m[2].Id, m[3].Id, 1, 6, 4, null,
            // Marcou a dupla 1 e digitou 3 x 6: um dos dois cliques foi errado.
            maisResultados: new List<ResultadoLancado> { Jogo(m[0], m[1], m[2], m[3], 1, 3, 6) });

        Assert.Empty(await ctx.JogosSemanais.ToListAsync());
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Quem_nao_e_da_panelinha_no_jogo_2_derruba_o_lancamento_todo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, m) = await MontarAsync(ctx);
        var deFora = new Jogador { Nome = "De Fora", Cpf = "77700000099", Login = "fora" };
        ctx.Jogadores.Add(deFora);
        await ctx.SaveChangesAsync();

        var controller = Controller(ctx, m[0].Id);
        await controller.RegistrarJogo(
            grupo.Id, DateTime.Today, m[0].Id, m[1].Id, m[2].Id, m[3].Id, 1, null, null, null,
            maisResultados: new List<ResultadoLancado> { Jogo(m[0], m[1], m[2], deFora, 1) });

        Assert.Empty(await ctx.JogosSemanais.ToListAsync());
        Assert.NotNull(controller.TempData["Erro"]);
    }

    // ===================== O TETO =====================

    [Fact]
    public async Task Acima_do_teto_o_lancamento_inteiro_e_recusado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, m) = await MontarAsync(ctx);

        // O primeiro solto + o teto na lista = teto + 1.
        var extras = Enumerable.Range(0, ResultadoDoJogoSemanal.MaximoPorLancamento)
            .Select(_ => Jogo(m[0], m[1], m[2], m[3], 1))
            .ToList();

        var controller = Controller(ctx, m[0].Id);
        await controller.RegistrarJogo(
            grupo.Id, DateTime.Today, m[0].Id, m[1].Id, m[2].Id, m[3].Id, 1, null, null, null,
            maisResultados: extras);

        Assert.Empty(await ctx.JogosSemanais.ToListAsync());
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task No_teto_exato_passa()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, m) = await MontarAsync(ctx);

        var extras = Enumerable.Range(0, ResultadoDoJogoSemanal.MaximoPorLancamento - 1)
            .Select(_ => Jogo(m[0], m[1], m[2], m[3], 1))
            .ToList();

        await Controller(ctx, m[0].Id).RegistrarJogo(
            grupo.Id, DateTime.Today, m[0].Id, m[1].Id, m[2].Id, m[3].Id, 1, null, null, null,
            maisResultados: extras);

        Assert.Equal(ResultadoDoJogoSemanal.MaximoPorLancamento, await ctx.JogosSemanais.CountAsync());
    }

    // ===================== O CONVIDADO É POR JOGO =====================

    // ⚠️ O teto de convidados vale POR PARTIDA, e é por isso que ele é conferido dentro do
    // laço. Uma noite de vários jogos pode ter um convidado em cada um sem que nenhum deles
    // vire uma partida de anônimos — somar a noite inteira barraria gente por nada.
    [Fact]
    public async Task Um_convidado_em_cada_jogo_continua_valendo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, m) = await MontarAsync(ctx);

        await Controller(ctx, m[0].Id).RegistrarJogo(
            grupo.Id, DateTime.Today, m[0].Id, m[1].Id, m[2].Id, ConvidadoNoJogo.NoFio, 1, null, null, null,
            maisResultados: new List<ResultadoLancado>
            {
                new()
                {
                    Dupla1Jogador1Id = m[0].Id, Dupla1Jogador2Id = m[2].Id,
                    Dupla2Jogador1Id = m[1].Id, Dupla2Jogador2Id = ConvidadoNoJogo.NoFio,
                    VencedorLado = 2,
                },
            });

        var jogos = await ctx.JogosSemanais.OrderBy(j => j.Id).ToListAsync();
        Assert.Equal(2, jogos.Count);
        // A vaga do convidado vira NULO no banco — sem nome, o sistema não sabe se é o mesmo primo.
        Assert.All(jogos, j => Assert.Null(j.Dupla2Jogador2Id));
    }

    // Um jogo só continua se comportando como sempre: mensagem no singular e uma linha.
    [Fact]
    public async Task Sem_jogos_extras_nada_muda()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, m) = await MontarAsync(ctx);

        var controller = Controller(ctx, m[0].Id);
        await controller.RegistrarJogo(
            grupo.Id, DateTime.Today, m[0].Id, m[1].Id, m[2].Id, m[3].Id, 1, 6, 4, null);

        Assert.Single(await ctx.JogosSemanais.ToListAsync());
        Assert.Equal("Jogo registrado! Ranking atualizado.", controller.TempData["Sucesso"]);
    }
}
