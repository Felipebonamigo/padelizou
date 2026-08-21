using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Tests;

// Achado da análise de sistema de 21/08/2026: quando um W.O. TARDIO (sobre um jogo já
// finalizado) troca o vencedor de uma fase que NÃO é a final, a fase seguinte da categoria —
// que nasceu do vencedor errado — ficava de pé, com a dupla errada, em silêncio.
// PartidasController.ControlePlacar já tinha esse cuidado ao corrigir PLACAR (refazer a fase
// seguinte se ainda não começou, avisar se já começou); RegistrarWo não tinha o equivalente.
public class WoTardioEmFaseNaoFinalTests
{
    private static (Torneio torneio, Categoria categoria, Jogador organizador,
        Dupla a, Dupla b, Dupla c, Partida semifinal) Montar(DbPadelContext ctx)
    {
        var organizador = new Jogador { Nome = "Organizador", Cpf = "99900000099" };
        ctx.Jogadores.Add(organizador);

        var torneio = new Torneio { Nome = "Torneio de Teste", Codigo = "WOT1", Status = "Fase de Grupos" };
        ctx.Torneios.Add(torneio);
        var categoria = new Categoria { Nome = "3ª Categoria Masculina", Codigo = "C3M", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });

        Dupla NovaDupla(int seed) => new()
        {
            Categoria = categoria,
            Jogador1 = TestInfra.NovoJogador(seed * 2 - 1),
            Jogador2 = TestInfra.NovoJogador(seed * 2),
        };
        var a = NovaDupla(1);
        var b = NovaDupla(2);
        var c = NovaDupla(3);   // já venceu a outra semifinal, espera a final
        ctx.Duplas.AddRange(a, b, c);
        ctx.SaveChanges();

        // A venceu B 6x2 — resultado que o W.O. tardio vai desmentir.
        var semifinal = new Partida
        {
            CategoriaId = categoria.Id, TorneioId = torneio.Id, Codigo = "SF1", Fase = "Semifinal",
            Dupla1Id = a.Id, Dupla2Id = b.Id, Status = "Finalizada",
            GamesDupla1 = 6, GamesDupla2 = 2, SetsDupla1 = 1, SetsDupla2 = 0, VencedorId = a.Id,
        };
        ctx.Partidas.Add(semifinal);
        ctx.SaveChanges();

        return (torneio, categoria, organizador, a, b, c, semifinal);
    }

    [Fact]
    public async Task Fase_seguinte_ainda_nao_comecou_e_e_refeita_sozinha()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, categoria, org, a, _, c, semifinal) = Montar(ctx);

        var final = new Partida
        {
            CategoriaId = categoria.Id, TorneioId = semifinal.TorneioId, Codigo = "F1", Fase = "Final",
            Dupla1Id = a.Id, Dupla2Id = c.Id, Status = "Agendada",
        };
        ctx.Partidas.Add(final);
        ctx.SaveChanges();

        var controller = TestInfra.NovoPartidasController(ctx, org.Id);

        // O organizador descobre que A não compareceu de verdade — o 6x2 nunca aconteceu.
        await controller.RegistrarWo(semifinal.Id, a.Id);

        // A fase seguinte nasceu do vencedor errado (A) e ainda não começou: some sozinha.
        Assert.False(await ctx.Partidas.AnyAsync(p => p.Id == final.Id));

        var semifinalDepois = await ctx.Partidas.FindAsync(semifinal.Id);
        Assert.NotEqual(a.Id, semifinalDepois!.VencedorId);
    }

    [Fact]
    public async Task Fase_seguinte_ja_comecou_e_nao_e_mexida_so_avisada()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, categoria, org, a, _, c, semifinal) = Montar(ctx);

        var final = new Partida
        {
            CategoriaId = categoria.Id, TorneioId = semifinal.TorneioId, Codigo = "F1", Fase = "Final",
            Dupla1Id = a.Id, Dupla2Id = c.Id, Status = "AoVivo",   // já entrou em quadra
        };
        ctx.Partidas.Add(final);
        ctx.SaveChanges();

        var controller = TestInfra.NovoPartidasController(ctx, org.Id);
        await controller.RegistrarWo(semifinal.Id, a.Id);

        // Jogo em quadra não é apagado por baixo de quem está jogando.
        Assert.True(await ctx.Partidas.AnyAsync(p => p.Id == final.Id));
    }

    [Fact]
    public async Task Sem_fase_seguinte_nenhuma_o_wo_tardio_funciona_normalmente()
    {
        // Regressão simples: categoria cuja semifinal É a última fase gerada até agora (a
        // final ainda nem foi criada) não pode quebrar por falta do que remover.
        using var ctx = TestInfra.NovoContexto();
        var (_, _, org, a, b, _, semifinal) = Montar(ctx);

        var controller = TestInfra.NovoPartidasController(ctx, org.Id);
        var resultado = await controller.RegistrarWo(semifinal.Id, a.Id);

        var depois = await ctx.Partidas.FindAsync(semifinal.Id);
        Assert.Equal(b.Id, depois!.VencedorId);
    }
}
