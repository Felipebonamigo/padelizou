using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Controllers;
using Xunit;

namespace Padelizou.Tests;

// CURTIR UM COMENTÁRIO NO PERFIL — pedido do Felipe (07/09/2026): "Permita as pessoas
// curtirem comentário no perfil também".
//
// Mesma forma do Elogio (Curtir/Descurtir são duas ações, como DarElogio/RemoverElogio e
// SeguirTorneio/DeixarDeSeguirTorneio — não um toggle só), e a mesma trava de "um por pessoa":
// em produção a chave composta (ComentarioId, JogadorId) no banco não deixa clicar duas vezes
// e empilhar duas curtidas (mesma régua do Elogio — ver Models/DbPadelContext).
//
// ⚠️ FALSIFICADO E CORRIGIDO: o EF InMemory desta suíte NÃO aplica índice único — testado
// removendo a checagem em C# (`jaCurti`) e a suíte deixou duas linhas entrarem, silenciosa.
// `Curtir_duas_vezes_nao_empilha`, abaixo, trava o GUARDA EM C#, que é a defesa que esta
// suíte consegue exercitar de verdade; o índice do banco continua existindo como a segunda
// camada que só um teste contra Postgres provaria.
public class CurtirComentarioTests
{
    private static DbPadelContext Cenario()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.AddRange(
            new Jogador { Id = 1, Nome = "Dono do Perfil", Cpf = "11144477735" },
            new Jogador { Id = 2, Nome = "Quem Comentou", Cpf = "22233344456" },
            new Jogador { Id = 3, Nome = "Quem Curte", Cpf = "33322211187" },
            new Jogador { Id = 4, Nome = "Outra Pessoa", Cpf = "44455566678" });
        ctx.ComentariosPerfil.Add(new ComentarioPerfil { Id = 50, AutorId = 2, PerfilId = 1, Texto = "Jogador excelente" });
        ctx.SaveChanges();
        return ctx;
    }

    private static JogadoresController Como(DbPadelContext ctx, int jogadorId) =>
        TestInfra.NovoJogadoresController(ctx, jogadorId);

    [Fact]
    public async Task Curtir_grava_a_curtida()
    {
        var ctx = Cenario();

        await Como(ctx, 3).CurtirComentario(50);

        var curtida = Assert.Single(ctx.CurtidasDoComentario);
        Assert.Equal(50, curtida.ComentarioId);
        Assert.Equal(3, curtida.JogadorId);
    }

    [Fact]
    public async Task Curtir_duas_vezes_nao_empilha()
    {
        var ctx = Cenario();

        await Como(ctx, 3).CurtirComentario(50);
        await Como(ctx, 3).CurtirComentario(50);

        Assert.Single(ctx.CurtidasDoComentario);
    }

    [Fact]
    public async Task Duas_pessoas_diferentes_curtem_o_mesmo_comentario()
    {
        var ctx = Cenario();

        await Como(ctx, 3).CurtirComentario(50);
        await Como(ctx, 4).CurtirComentario(50);

        Assert.Equal(2, await ctx.CurtidasDoComentario.CountAsync(c => c.ComentarioId == 50));
    }

    [Fact]
    public async Task Descurtir_apaga_a_curtida()
    {
        var ctx = Cenario();
        await Como(ctx, 3).CurtirComentario(50);

        await Como(ctx, 3).DescurtirComentario(50);

        Assert.Empty(ctx.CurtidasDoComentario);
    }

    [Fact]
    public async Task Descurtir_sem_ter_curtido_nao_estoura()
    {
        var ctx = Cenario();

        var resultado = await Como(ctx, 3).DescurtirComentario(50);

        Assert.Empty(ctx.CurtidasDoComentario);
        Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToActionResult>(resultado);
    }

    // Mesma régua do elogio: não curte o próprio.
    [Fact]
    public async Task Autor_nao_curte_o_proprio_comentario()
    {
        var ctx = Cenario();

        await Como(ctx, 2).CurtirComentario(50);

        Assert.Empty(ctx.CurtidasDoComentario);
    }

    // O DONO DO PERFIL pode curtir um comentário que outra pessoa deixou sobre ele — a régua
    // bloqueia só o AUTOR do comentário, não o dono do perfil onde ele está.
    [Fact]
    public async Task Dono_do_perfil_pode_curtir_comentario_de_outra_pessoa()
    {
        var ctx = Cenario();

        await Como(ctx, 1).CurtirComentario(50);

        Assert.Single(ctx.CurtidasDoComentario);
    }

    [Fact]
    public async Task Curtir_comentario_que_nao_existe_nao_estoura()
    {
        var ctx = Cenario();

        var resultado = await Como(ctx, 3).CurtirComentario(999);

        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(resultado);
        Assert.Empty(ctx.CurtidasDoComentario);
    }

    // Apagar o comentário apaga as curtidas dele — sem isso a linha de CurtidaDoComentario
    // ficaria órfã apontando pra um comentário que não existe mais.
    [Fact]
    public async Task Apagar_o_comentario_apaga_as_curtidas()
    {
        var ctx = Cenario();
        await Como(ctx, 3).CurtirComentario(50);
        await Como(ctx, 4).CurtirComentario(50);

        var comentario = await ctx.ComentariosPerfil.FindAsync(50);
        ctx.ComentariosPerfil.Remove(comentario!);
        await ctx.SaveChangesAsync();

        Assert.Empty(ctx.CurtidasDoComentario);
    }
}
