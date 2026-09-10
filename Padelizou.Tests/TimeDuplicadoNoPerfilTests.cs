using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;

namespace Padelizou.Tests;

// A caixa "sou dono do time" no /Auth/EditarPerfil — o TERCEIRO caminho que cria um Time.
//
// Os outros dois já recusam nome repetido: o AdminController.CriarTime devolve erro, e o
// AuthController.DefinirTimeAsync acha por nome sem diferenciar maiúscula e ENTRA no que
// existe. Este aqui criava direto, sem olhar — e foi por ele que a base de produção ganhou
// "ER Padel" (21 jogadores, 318 pontos) e "Er padel" (2 jogadores, 25 pontos) como times
// diferentes, com o pódio de times do torneio partido entre as duas linhas.
//
// O comentário do CriarTime já dizia o estrago: "com dois SINDAQUA na base, cada pessoa cai
// num dos dois pelo acaso da consulta — e metade do time fica pendurada no cadastro errado,
// em silêncio". A regra existia; um dos três caminhos não a aplicava.
public class TimeDuplicadoNoPerfilTests
{
    private static async Task<(DbPadelContext ctx, Jogador jogador)> CenarioAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var jogador = new Jogador { Id = 1, Nome = "Camila Lopes", Cpf = "1", Email = "camila@x.com" };
        ctx.Jogadores.Add(jogador);
        ctx.Times.Add(new Time { Id = 10, Nome = "ER Padel" });
        await ctx.SaveChangesAsync();
        return (ctx, jogador);
    }

    private static Task<IActionResult> SalvarComoDonoAsync(DbPadelContext ctx, int jogadorId, string nomeTime) =>
        TestInfra.NovoAuthController(ctx, jogadorId).EditarPerfil(
            "Camila Lopes", "camila@x.com", null, cidade: null, estado: null, isProfessor: false, foto: null,
            ehDonoTime: true, nomeTime: nomeTime);

    [Fact]
    public async Task Dono_com_nome_que_ja_existe_entra_no_time_em_vez_de_criar_um_segundo()
    {
        // O caso exato de produção: "ER Padel" já existe e alguém digita "Er padel".
        var (ctx, jogador) = await CenarioAsync();
        using var _ = ctx;

        await SalvarComoDonoAsync(ctx, jogador.Id, "Er padel");

        Assert.Single(ctx.Times);
        Assert.Equal("ER Padel", ctx.Times.Single().Nome);
        Assert.Equal(10, (await ctx.Jogadores.FindAsync(jogador.Id))!.TimeId);
    }

    [Fact]
    public async Task Entrar_por_esse_caminho_num_time_que_ja_existe_nao_da_administracao()
    {
        // Mesma razão do DefinirTimeAsync: digitar o nome de um time que já existe não pode
        // dar a alguém o comando dele — os times importados do ranking viram alvo fácil.
        var (ctx, jogador) = await CenarioAsync();
        using var _ = ctx;

        await SalvarComoDonoAsync(ctx, jogador.Id, "Er padel");

        Assert.Empty(ctx.TimeAdministradores);
    }

    [Fact]
    public async Task Renomear_o_proprio_time_para_o_nome_de_outro_funde_os_dois()
    {
        // O agravante do mesmo trecho: com os dois times já na base, a dona do "Er padel"
        // corrigindo o nome pra "ER Padel" passaria a ter DOIS times com o nome idêntico —
        // pior que o problema que ela quis arrumar. Agora os dois viram um só, pela régua do
        // FusaoDeTimes (sobrevive o de mais jogadores).
        var (ctx, jogador) = await CenarioAsync();
        using var _ = ctx;
        ctx.Jogadores.AddRange(
            new Jogador { Id = 2, Nome = "Rafael", Cpf = "2", TimeId = 10 },
            new Jogador { Id = 3, Nome = "Bruno", Cpf = "3", TimeId = 10 });
        ctx.Times.Add(new Time { Id = 11, Nome = "Er padel" });
        ctx.TimeAdministradores.Add(new TimeAdministrador
        {
            TimeId = 11,
            JogadorId = jogador.Id,
            ConcedidoPorId = jogador.Id,
            ConcedidoEm = DateTime.Now,
        });
        jogador.TimeId = 11;
        await ctx.SaveChangesAsync();

        await SalvarComoDonoAsync(ctx, jogador.Id, "ER Padel");

        Assert.Single(ctx.Times);
        Assert.Equal("ER Padel", ctx.Times.Single().Nome);
        Assert.Equal(10, (await ctx.Jogadores.FindAsync(jogador.Id))!.TimeId);
        // A fusão não promove: ela administrava 1 pessoa, e não passa a mandar em 3.
        Assert.Empty(ctx.TimeAdministradores);
    }

    [Fact]
    public async Task Dono_com_nome_novo_continua_criando_o_time_e_administrando()
    {
        // A trava não pode custar o caminho feliz: nome que não existe segue criando o time,
        // e quem criou continua sendo o primeiro administrador dele.
        var (ctx, jogador) = await CenarioAsync();
        using var _ = ctx;

        await SalvarComoDonoAsync(ctx, jogador.Id, "Los Corneteiros");

        var criado = ctx.Times.Single(t => t.Nome == "Los Corneteiros");
        Assert.Equal(criado.Id, (await ctx.Jogadores.FindAsync(jogador.Id))!.TimeId);
        Assert.Single(ctx.TimeAdministradores.Where(a => a.TimeId == criado.Id && a.JogadorId == jogador.Id));
    }
}
