using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Tests;

// O MARCADOR do torneio (20/08/2026): a pessoa da mesa no dia de jogo. O organizador adiciona
// em "gerenciar torneio", e ela ganha SÓ a operação do dia — Mesa de Controle, placar,
// largada, W.O., check-in e o remendo da grade. Inscrições, dinheiro, sorteio e configuração
// continuam sendo de organizador.
//
// A decisão que estes testes protegem é a tabela PRÓPRIA (TorneioMarcador, não um nível em
// TorneioOrganizador): toda checagem de organizador pergunta "existe linha em
// TorneioOrganizadores?" sem olhar nível, e um marcador lá dentro viraria organizador em tudo
// de uma vez. Fail-closed: o marcador só entra onde uma checagem o incluiu por escrito.
public class MarcadorDoTorneioTests
{
    private static async Task<(DbPadelContext ctx, Torneio torneio, Partida jogo, Jogador marcador, Jogador organizador)>
        MontarAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        // O marcador NÃO organiza nada e NÃO é admin — é o amigo/contratado que vai cuidar
        // da mesa neste torneio.
        var marcador = new Jogador { Nome = "Marcador da Mesa", Cpf = "99900000096" };
        ctx.Jogadores.Add(marcador);
        await ctx.SaveChangesAsync();
        ctx.TorneioMarcadores.Add(new TorneioMarcador { TorneioId = torneio.Id, JogadorId = marcador.Id });
        await ctx.SaveChangesAsync();

        var jogo = await ctx.Partidas.FirstAsync(p => p.TorneioId == torneio.Id);
        return (ctx, torneio, jogo, marcador, org);
    }

    // ── O que o marcador PODE ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Marcador_consegue_colocar_o_jogo_no_ar()
    {
        var (ctx, _, jogo, marcador, _) = await MontarAsync();
        using var _1 = ctx;

        var controller = TestInfra.NovoPartidasController(ctx, marcador.Id);
        var resposta = await controller.ColocarNoAr(jogo.Id);

        Assert.IsNotType<ForbidResult>(resposta);
        Assert.Equal("AoVivo", (await ctx.Partidas.FindAsync(jogo.Id))!.Status);
    }

    [Fact]
    public async Task Marcador_consegue_salvar_o_placar()
    {
        var (ctx, _, jogo, marcador, _) = await MontarAsync();
        using var _1 = ctx;

        var controller = TestInfra.NovoPartidasController(ctx, marcador.Id);
        var resposta = await controller.ControlePlacar(jogo.Id, "AoVivo", 4, 2, null, null);

        Assert.IsNotType<ForbidResult>(resposta);
        var salvo = await ctx.Partidas.FindAsync(jogo.Id);
        Assert.Equal(4, salvo!.GamesDupla1);
        Assert.Equal(2, salvo.GamesDupla2);
    }

    [Fact]
    public async Task Marcador_consegue_abrir_a_Mesa_de_Controle()
    {
        var (ctx, torneio, _, marcador, _) = await MontarAsync();
        using var _1 = ctx;

        var controller = TestInfra.NovoTorneiosController(ctx, marcador.Id);
        var resposta = await controller.MesaControle(torneio.Id);

        Assert.IsNotType<ForbidResult>(resposta);
        Assert.IsType<ViewResult>(resposta);
    }

    // ── O que o marcador NÃO pode — cada caso é uma porta que não pode abrir junto ────

    [Fact]
    public async Task Marcador_nao_sorteia_as_chaves()
    {
        // Torneio próprio, AINDA por sortear: no já sorteado o GerarChaves devolve NotFound
        // pelo status antes de olhar quem chama, e o teste não provaria a porta certa.
        var ctx = TestInfra.NovoContexto();
        using var _1 = ctx;
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);

        var marcador = new Jogador { Nome = "Marcador da Mesa", Cpf = "99900000096" };
        ctx.Jogadores.Add(marcador);
        await ctx.SaveChangesAsync();
        ctx.TorneioMarcadores.Add(new TorneioMarcador { TorneioId = torneio.Id, JogadorId = marcador.Id });
        await ctx.SaveChangesAsync();

        // Refazer grade é remendo do dia (marcador pode); SORTEAR muda o torneio.
        var controller = TestInfra.NovoTorneiosController(ctx, marcador.Id);
        var resposta = await controller.GerarChaves(torneio.Id);

        Assert.IsType<ForbidResult>(resposta);
    }

    [Fact]
    public async Task Marcador_nao_ve_o_financeiro()
    {
        var (ctx, torneio, _, marcador, _) = await MontarAsync();
        using var _1 = ctx;

        var controller = TestInfra.NovoTorneiosController(ctx, marcador.Id);
        var resposta = await controller.Financeiro(torneio.Id);

        Assert.IsType<ForbidResult>(resposta);
    }

    [Fact]
    public async Task Marcador_nao_adiciona_gente_nem_como_marcador()
    {
        var (ctx, torneio, _, marcador, _) = await MontarAsync();
        using var _1 = ctx;

        var outro = new Jogador { Nome = "Amigo do Marcador", Cpf = "99900000095" };
        ctx.Jogadores.Add(outro);
        await ctx.SaveChangesAsync();

        // Sem isto, o marcador repassaria a chave da mesa sozinho — e quem responde pelo
        // torneio é o organizador.
        var controller = TestInfra.NovoTorneiosController(ctx, marcador.Id);
        Assert.IsType<ForbidResult>(await controller.AdicionarMarcador(torneio.Id, outro.Id));
        Assert.IsType<ForbidResult>(await controller.AdicionarOrganizador(torneio.Id, outro.Id));
    }

    // ── O ciclo de adicionar e remover ────────────────────────────────────────────────

    [Fact]
    public async Task Organizador_adiciona_e_remove_o_marcador_e_o_acesso_morre_junto()
    {
        var (ctx, torneio, jogo, _, org) = await MontarAsync();
        using var _1 = ctx;

        var novo = new Jogador { Nome = "Mesário do Sábado", Cpf = "99900000094" };
        ctx.Jogadores.Add(novo);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.AdicionarMarcador(torneio.Id, novo.Id);
        Assert.True(await ctx.TorneioMarcadores.AnyAsync(m => m.TorneioId == torneio.Id && m.JogadorId == novo.Id));

        // Marcador é papel de UM evento: removeu, a porta fecha na hora.
        await controller.RemoverMarcador(torneio.Id, novo.Id);
        Assert.False(await ctx.TorneioMarcadores.AnyAsync(m => m.TorneioId == torneio.Id && m.JogadorId == novo.Id));

        var partidas = TestInfra.NovoPartidasController(ctx, novo.Id);
        Assert.IsType<ForbidResult>(await partidas.ColocarNoAr(jogo.Id));
    }

    [Fact]
    public async Task Organizador_nao_vira_marcador_ele_ja_pode_tudo_isso()
    {
        var (ctx, torneio, _, _, org) = await MontarAsync();
        using var _1 = ctx;

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.AdicionarMarcador(torneio.Id, org.Id);

        Assert.False(await ctx.TorneioMarcadores.AnyAsync(m => m.TorneioId == torneio.Id && m.JogadorId == org.Id));
    }

    [Fact]
    public async Task Clique_duplo_no_adicionar_nao_duplica_nem_estoura()
    {
        var (ctx, torneio, _, marcador, org) = await MontarAsync();
        using var _1 = ctx;

        // O marcador da montagem já existe — adicionar de novo tem que ser um não-fazer,
        // não uma exceção de chave duplicada no meio do clique.
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.AdicionarMarcador(torneio.Id, marcador.Id);

        Assert.Equal(1, await ctx.TorneioMarcadores
            .CountAsync(m => m.TorneioId == torneio.Id && m.JogadorId == marcador.Id));
    }

    [Fact]
    public async Task Quem_nao_e_marcador_continua_barrado()
    {
        var (ctx, _, jogo, _, _) = await MontarAsync();
        using var _1 = ctx;

        // A prova de que a porta nova não afrouxou a antiga: jogador comum segue Forbid.
        var comum = new Jogador { Nome = "Torcedor", Cpf = "99900000093" };
        ctx.Jogadores.Add(comum);
        await ctx.SaveChangesAsync();

        var partidas = TestInfra.NovoPartidasController(ctx, comum.Id);
        Assert.IsType<ForbidResult>(await partidas.ColocarNoAr(jogo.Id));
    }
}
