using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Xunit;

namespace Padelizou.Tests;

// O admin do Padelizou precisa entrar em qualquer torneio pra socorrer organizador travado:
// no dia do torneio, com as quadras ocupadas, o problema é sempre urgente — e "me adiciona
// como organizador" depende justamente de quem não está conseguindo mexer no sistema. Antes o
// único caminho era mexer no banco na mão.
//
// O que estes testes seguram é o outro lado: continuar recusando quem NÃO é nem organizador
// nem admin. Uma trava que passou a deixar todo mundo entrar seria pior que trava nenhuma,
// porque ninguém repara.
public class AdminMandaEmQualquerTorneioTests
{
    private static Jogador Adicionar(DbPadelContext ctx, string nome, string cpf,
        bool adminRaiz = false, bool adminGeral = false)
    {
        var j = new Jogador { Nome = nome, Cpf = cpf, IsAdminRaiz = adminRaiz, IsAdminGeral = adminGeral };
        ctx.Jogadores.Add(j);
        ctx.SaveChanges();
        return j;
    }

    // Encerrar inscrições é uma ação de organizador como qualquer outra: passa pela mesma
    // porta (EhOrganizadorAsync), então serve de sonda pra ela.
    private static async Task<IActionResult> TentarEncerrarAsync(DbPadelContext ctx, int torneioId, int quemTenta) =>
        await TestInfra.NovoTorneiosController(ctx, quemTenta).EncerrarInscricoes(torneioId);

    [Fact]
    public async Task Admin_raiz_encerra_inscricoes_de_torneio_que_nao_e_dele()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");
        var admin = Adicionar(ctx, "Felipe", "02061197043", adminRaiz: true);

        var resultado = await TentarEncerrarAsync(ctx, torneio.Id, admin.Id);

        Assert.IsNotType<ForbidResult>(resultado);
        Assert.Equal("Chaves em Sorteio", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    [Fact]
    public async Task Admin_geral_tambem_manda()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");
        var admin = Adicionar(ctx, "Admin nomeado", "11144477735", adminGeral: true);

        Assert.IsNotType<ForbidResult>(await TentarEncerrarAsync(ctx, torneio.Id, admin.Id));
    }

    [Fact]
    public async Task O_organizador_de_verdade_continua_mandando()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");

        Assert.IsNotType<ForbidResult>(await TentarEncerrarAsync(ctx, torneio.Id, organizador.Id));
    }

    [Fact]
    public async Task Jogador_comum_continua_recusado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");
        var estranho = Adicionar(ctx, "Estranho", "22255588896");

        Assert.IsType<ForbidResult>(await TentarEncerrarAsync(ctx, torneio.Id, estranho.Id));
        Assert.Equal("Inscrições Abertas", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    [Fact]
    public async Task Visitante_sem_conta_continua_recusado()
    {
        // Deslogado chega como id 0, e id 0 não pode casar com jogador nenhum.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Inscrições Abertas");

        Assert.IsType<ForbidResult>(await TentarEncerrarAsync(ctx, torneio.Id, 0));
    }
}
