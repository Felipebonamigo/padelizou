using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Mudar a QUADRA de um jogo sem mexer na hora. Até então o organizador só conseguia isso
// trocando o SLOT INTEIRO com outro jogo (TrocaDeHorario), o que arrastava o horário junto —
// e quase nunca era o que ele queria: a quadra 3 molhou, a 1 é a coberta, a final merece a do
// meio.
public class TrocaDeQuadraTests
{
    private static readonly string[] Quadras = { "Quadra 1", "Quadra 2", "Quadra 3" };
    private static readonly DateTime Vinte = new(2026, 8, 5, 20, 0, 0);

    private static Partida Jogo(int id, string codigo, string? quadra, DateTime? horario,
        string status = "Agendada") => new()
    {
        Id = id,
        Codigo = codigo,
        TorneioId = 1,
        Status = status,
        NomeQuadra = quadra,
        HorarioPrevisto = horario,
        Dupla1Id = id * 10,
        Dupla2Id = id * 10 + 1,
    };

    [Fact]
    public void Quadra_livre_no_horario_o_jogo_so_muda_de_lugar()
    {
        var jogo = Jogo(1, "AAA", "Quadra 1", Vinte);
        var outro = Jogo(2, "BBB", "Quadra 2", Vinte);

        Assert.Null(TrocaDeQuadra.MotivoParaNaoMudar(jogo, "Quadra 3", 1, Quadras));

        var ocupante = TrocaDeQuadra.QuemOcupa(jogo, "Quadra 3", new[] { jogo, outro });
        Assert.Null(ocupante);

        TrocaDeQuadra.Mudar(jogo, "Quadra 3", ocupante);

        Assert.Equal("Quadra 3", jogo.NomeQuadra);
        Assert.Equal("Quadra 2", outro.NomeQuadra);      // ninguém mais mexeu
        Assert.Equal(Vinte, jogo.HorarioPrevisto);       // a hora NÃO muda
    }

    [Fact]
    public void Quadra_ocupada_no_mesmo_horario_faz_os_dois_trocarem()
    {
        // Recusar deixaria o organizador no mesmo beco ("então como eu ponho esse jogo na
        // Quadra 2?"). Trocar resolve o pedido e mantém a grade íntegra — nenhuma quadra em
        // dobro, nenhum buraco —, igual à troca de horário.
        var jogo = Jogo(1, "AAA", "Quadra 1", Vinte);
        var ocupada = Jogo(2, "BBB", "Quadra 2", Vinte);

        var ocupante = TrocaDeQuadra.QuemOcupa(jogo, "Quadra 2", new[] { jogo, ocupada });
        Assert.Same(ocupada, ocupante);

        TrocaDeQuadra.Mudar(jogo, "Quadra 2", ocupante);

        Assert.Equal("Quadra 2", jogo.NomeQuadra);
        Assert.Equal("Quadra 1", ocupada.NomeQuadra);
        Assert.Equal(Vinte, jogo.HorarioPrevisto);
        Assert.Equal(Vinte, ocupada.HorarioPrevisto);
    }

    [Fact]
    public void Quadra_ocupada_em_OUTRO_horario_nao_atrapalha()
    {
        // Duas duplas na mesma quadra em horários diferentes é o normal do torneio.
        var jogo = Jogo(1, "AAA", "Quadra 1", Vinte);
        var maisTarde = Jogo(2, "BBB", "Quadra 2", Vinte.AddMinutes(30));

        Assert.Null(TrocaDeQuadra.QuemOcupa(jogo, "Quadra 2", new[] { jogo, maisTarde }));
    }

    [Fact]
    public void Jogo_sem_horario_nao_disputa_quadra_com_ninguem()
    {
        // Torneio por ordem de liberação: o jogo entra quando a Mesa chamar.
        var jogo = Jogo(1, "AAA", null, null);
        var outro = Jogo(2, "BBB", "Quadra 2", null);

        Assert.Null(TrocaDeQuadra.QuemOcupa(jogo, "Quadra 2", new[] { jogo, outro }));
    }

    [Fact]
    public void Jogo_finalizado_tem_quadra_de_historia()
    {
        var jogo = Jogo(1, "AAA", "Quadra 1", Vinte, status: "Finalizada");

        var motivo = TrocaDeQuadra.MotivoParaNaoMudar(jogo, "Quadra 2", 1, Quadras);

        Assert.NotNull(motivo);
        Assert.Contains("história", motivo);
    }

    [Fact]
    public void Jogo_EM_QUADRA_pode_ser_remanejado()
    {
        // Quadra que molha ou luz que apaga acontece com jogo em andamento, e é justamente aí
        // que remanejar importa.
        var jogo = Jogo(1, "AAA", "Quadra 1", Vinte, status: "AoVivo");

        Assert.Null(TrocaDeQuadra.MotivoParaNaoMudar(jogo, "Quadra 2", 1, Quadras));
    }

    [Fact]
    public void Quadra_de_fora_do_torneio_e_recusada()
    {
        var jogo = Jogo(1, "AAA", "Quadra 1", Vinte);

        Assert.NotNull(TrocaDeQuadra.MotivoParaNaoMudar(jogo, "Quadra 9", 1, Quadras));
        Assert.NotNull(TrocaDeQuadra.MotivoParaNaoMudar(jogo, "", 1, Quadras));
        Assert.NotNull(TrocaDeQuadra.MotivoParaNaoMudar(jogo, "Quadra 1", 1, Quadras));   // já está lá
    }

    [Fact]
    public void Jogo_de_outro_torneio_e_recusado()
    {
        var jogo = Jogo(1, "AAA", "Quadra 1", Vinte);

        Assert.NotNull(TrocaDeQuadra.MotivoParaNaoMudar(jogo, "Quadra 2", torneioId: 99, Quadras));
        Assert.NotNull(TrocaDeQuadra.MotivoParaNaoMudar(null, "Quadra 2", 1, Quadras));
    }

    // ---- o caminho completo, pelo controller ----

    [Fact]
    public async Task Pelo_controller_a_troca_grava_e_nunca_deixa_quadra_em_dobro()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        torneio.QuantidadeQuadras = 3;
        ctx.Set<Quadra>().AddRange(Quadras.Select(n => new Quadra { TorneioId = torneio.Id, Nome = n }));
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        var noMesmoHorario = await ctx.Partidas
            .Where(p => p.TorneioId == torneio.Id)
            .OrderBy(p => p.HorarioPrevisto).Take(2).ToListAsync();

        var jogo = noMesmoHorario[0];
        var vizinho = noMesmoHorario[1];
        Assert.Equal(jogo.HorarioPrevisto, vizinho.HorarioPrevisto);

        var quadraDoVizinho = vizinho.NomeQuadra!;
        var quadraDoJogo = jogo.NomeQuadra!;

        await controller.TrocarQuadra(torneio.Id, jogo.Id, quadraDoVizinho);

        var depois = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).ToListAsync();
        var jogoDepois = depois.Single(p => p.Id == jogo.Id);
        var vizinhoDepois = depois.Single(p => p.Id == vizinho.Id);

        Assert.Equal(quadraDoVizinho, jogoDepois.NomeQuadra);
        Assert.Equal(quadraDoJogo, vizinhoDepois.NomeQuadra);
        Assert.Null(controller.TempData["Erro"]);

        // A garantia que não pode cair nunca: ninguém repete quadra no mesmo horário.
        foreach (var noSlot in depois.Where(p => p.HorarioPrevisto != null).GroupBy(p => p.HorarioPrevisto))
        {
            var quadras = noSlot.Select(p => p.NomeQuadra).Where(q => q != null).ToList();
            Assert.Equal(quadras.Count, quadras.Distinct().Count());
        }
    }

    [Fact]
    public async Task Quem_nao_organiza_nao_muda_quadra()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        ctx.Set<Quadra>().AddRange(Quadras.Select(n => new Quadra { TorneioId = torneio.Id, Nome = n }));
        await ctx.SaveChangesAsync();

        var doOrganizador = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doOrganizador.GerarChaves(torneio.Id);

        var jogo = await ctx.Partidas.FirstAsync(p => p.TorneioId == torneio.Id);
        var quadraAntes = jogo.NomeQuadra;

        var intruso = new Jogador { Nome = "Xereta", Cpf = "99900000098" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, intruso.Id);
        var resposta = await controller.TrocarQuadra(torneio.Id, jogo.Id, "Quadra 3");

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(resposta);
        Assert.Equal(quadraAntes, (await ctx.Partidas.FindAsync(jogo.Id))!.NomeQuadra);
    }
}
