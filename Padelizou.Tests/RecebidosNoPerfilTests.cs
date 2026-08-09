using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// O CARD "O QUE DIZEM DE VOCÊ", no Meu Painel.
//
// ⚠️ A diferença pro perfil público é a pergunta que cada tela responde: lá o elogio é
// agregado por TIPO ("3× Boa Bandeja"), que é o que interessa a quem chega de fora e quer
// saber como a pessoa joga. Agregado, o nome de QUEM elogiou desaparece — e era isso que não
// existia em tela nenhuma do sistema.
public class RecebidosNoPerfilTests
{
    private static DbPadelContext Cenario()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.AddRange(
            new Jogador { Id = 1, Nome = "Dono do Painel", Cpf = "1" },
            new Jogador { Id = 2, Nome = "MARIA APARECIDA DOS SANTOS", Apelido = "Cida", Cpf = "2" },
            new Jogador { Id = 3, Nome = "Outro Jogador", Cpf = "3" });
        ctx.SaveChanges();
        return ctx;
    }

    private static async Task<RecebidosNoPerfilVM> PainelAsync(DbPadelContext ctx, int jogadorId)
    {
        var controller = TestInfra.NovoAuthController(ctx, jogadorId);
        Assert.IsType<ViewResult>(await controller.Perfil());
        return Assert.IsType<RecebidosNoPerfilVM>(controller.ViewBag.Recebidos);
    }

    [Fact]
    public async Task O_elogio_vem_com_o_nome_de_quem_deu()
    {
        var ctx = Cenario();
        ctx.Elogios.Add(new Elogio { DeJogadorId = 2, ParaJogadorId = 1, Tipo = "BoaBandeja" });
        ctx.SaveChanges();

        var recebidos = await PainelAsync(ctx, 1);

        var elogio = Assert.Single(recebidos.Elogios);
        Assert.Equal("Boa Bandeja", elogio.Titulo);
        // Nome bonito e com apelido, como o resto do site mostra gente.
        Assert.Equal("Maria Santos (Cida)", elogio.DeQuem);
        Assert.Equal(2, elogio.DeJogadorId);
    }

    [Fact]
    public async Task O_comentario_vem_com_autor_e_texto()
    {
        var ctx = Cenario();
        ctx.ComentariosPerfil.Add(new ComentarioPerfil { AutorId = 2, PerfilId = 1, Texto = "Parceiraço" });
        ctx.SaveChanges();

        var comentario = Assert.Single((await PainelAsync(ctx, 1)).Comentarios);
        Assert.Equal("Parceiraço", comentario.Texto);
        Assert.Equal("Maria Santos (Cida)", comentario.DeQuem);
    }

    [Fact]
    public async Task So_aparece_o_que_e_MEU()
    {
        // Elogio que a pessoa DEU não entra: o card responde "o que dizem de você".
        var ctx = Cenario();
        ctx.Elogios.AddRange(
            new Elogio { DeJogadorId = 1, ParaJogadorId = 3, Tipo = "Garra" },
            new Elogio { DeJogadorId = 3, ParaJogadorId = 2, Tipo = "Garra" });
        ctx.SaveChanges();

        var recebidos = await PainelAsync(ctx, 1);

        Assert.Empty(recebidos.Elogios);
        Assert.Equal(0, recebidos.TotalElogios);
        Assert.False(recebidos.TemAlgo);
    }

    [Fact]
    public async Task O_card_mostra_os_ultimos_e_o_TOTAL_conta_todos()
    {
        // "e mais 3 elogio(s)" só sai certo se o total vier do banco, não do tamanho da lista
        // que a tela recebeu.
        var ctx = Cenario();
        for (var i = 0; i < 8; i++)
        {
            ctx.Jogadores.Add(new Jogador { Id = 100 + i, Nome = $"Fulano {i}", Cpf = $"9{i}" });
            ctx.Elogios.Add(new Elogio
            {
                DeJogadorId = 100 + i,
                ParaJogadorId = 1,
                Tipo = "Garra",
                CriadoEm = new DateTime(2026, 8, 1).AddHours(i),
            });
        }
        ctx.SaveChanges();

        var recebidos = await PainelAsync(ctx, 1);

        Assert.Equal(5, recebidos.Elogios.Count);
        Assert.Equal(8, recebidos.TotalElogios);
        // Mais recente primeiro: o elogio de hoje é o que a pessoa entrou pra ver.
        Assert.Equal(107, recebidos.Elogios.First().DeJogadorId);
    }

    [Fact]
    public async Task Codigo_de_elogio_fora_do_catalogo_nao_aparece_NEM_conta()
    {
        // Um código que saiu do catálogo apareceria como "TipoQueNaoExisteMais" na tela.
        // ⚠️ E ficar de fora só do desenho não basta: contando no total, a tela prometeria
        // "e mais 1 elogio" que ela nunca vai mostrar. O que não aparece não conta.
        var ctx = Cenario();
        ctx.Elogios.AddRange(
            new Elogio { DeJogadorId = 2, ParaJogadorId = 1, Tipo = "TipoQueNaoExisteMais" },
            new Elogio { DeJogadorId = 3, ParaJogadorId = 1, Tipo = "Garra" });
        ctx.SaveChanges();

        var recebidos = await PainelAsync(ctx, 1);

        Assert.Equal("Garra", Assert.Single(recebidos.Elogios).Titulo);
        Assert.Equal(1, recebidos.TotalElogios);
    }

    [Fact]
    public async Task Painel_sem_nada_recebido_nao_estoura()
    {
        var recebidos = await PainelAsync(Cenario(), 1);

        Assert.False(recebidos.TemAlgo);
        Assert.Empty(recebidos.Comentarios);
    }
}
