using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;

namespace Padelizou.Tests;

// DUAS TELAS FINALIZAM PARTIDA, E A CHAVE TEM QUE SAIR IGUAL PELAS DUAS.
//
// O organizador encerra um jogo por dois caminhos:
//   • Mesa de Controle / botão do card       → TorneiosController.FinalizarPartida
//   • Controle de Placar em tela cheia       → PartidasController.ControlePlacar
//
// Até 06/08/2026 cada controller tinha a PRÓPRIA cópia do robô que cria a fase seguinte. Os
// confrontos batiam (o pareamento sempre saiu de ChaveamentoMataMata), mas a cópia do
// PartidasController agendava na mão: `HorarioPrevisto = DateTime.Now.AddHours(2)` pra todos
// os jogos da rodada, sem quadra e sem olhar quem já estava marcado. Resultado: a rodada
// inteira nascia no mesmo minuto, "quadra a definir", num dia que não era o do torneio — e
// isso dependia SÓ de por qual tela o placar tinha sido lançado.
//
// Estes testes rodam o torneio pelo caminho da TELA CHEIA de propósito: é o que ninguém
// testava, e é o que quebrou no Interno de 05/08/2026.
public class UmRoboSoDeChaveamentoTests
{
    // 12 duplas, 2 quadras: 4 grupos de 3 → 8 classificados → Quartas com 4 jogos.
    // Com 2 quadras, 4 jogos NÃO cabem no mesmo horário — que é justamente o que a cópia
    // antiga fazia.
    private static (Torneio torneio, Categoria categoria, Jogador org) MontarComQuadras(
        DbPadelContext ctx, int qtdDuplas = 12, int quadras = 2)
    {
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas);

        torneio.QuantidadeQuadras = quadras;
        torneio.TempoPrevistoPartidaMinutos = 30;
        for (int i = 1; i <= quadras; i++)
            ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = $"Quadra {i}" });
        ctx.SaveChanges();

        return (torneio, categoria, org);
    }

    // Finaliza pelo Controle de Placar em tela cheia — o caminho do PartidasController.
    private static async Task FinalizarPelaTelaCheiaAsync(
        DbPadelContext ctx, int organizadorId, IEnumerable<Partida> jogos)
    {
        foreach (var jogo in jogos)
        {
            // Controller novo por jogo: em produção cada POST é uma requisição nova, e um
            // controller reaproveitado esconderia dependência de estado que lá não existe.
            var controller = TestInfra.NovoPartidasController(ctx, organizadorId);
            // A quadra vai como está: a tela cheia manda o campo de volta preenchido, e
            // reescrevê-lo com outro nome seria o TESTE mudando a grade que ele confere.
            await controller.ControlePlacar(jogo.Id, "Finalizada", 9, 3, jogo.NomeQuadra, null);
        }
    }

    [Fact]
    public async Task Fim_dos_grupos_pela_tela_cheia_agenda_o_mata_mata_na_grade()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = MontarComQuadras(ctx);
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var grupos = await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync();
        Assert.All(grupos, p => Assert.NotNull(p.HorarioPrevisto));
        var diaDoTorneio = grupos.Max(p => p.HorarioPrevisto!.Value);

        await FinalizarPelaTelaCheiaAsync(ctx, org.Id, grupos);

        var quartas = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Quartas de Final")
            .ToListAsync();

        Assert.Equal(4, quartas.Count);

        // ⚠️ O CORAÇÃO DO TESTE. Com o robô antigo deste controller todas as quatro nasciam
        // com o MESMO `DateTime.Now.AddHours(2)`: mesmo minuto, sem quadra, e num dia que
        // não é o do torneio.
        Assert.All(quartas, p => Assert.NotNull(p.HorarioPrevisto));
        Assert.All(quartas, p => Assert.False(string.IsNullOrEmpty(p.NomeQuadra)));
        Assert.All(quartas, p => Assert.True(p.HorarioPrevisto! > diaDoTorneio,
            $"a fase seguinte abriu em {p.HorarioPrevisto:dd/MM HH:mm}, antes do fim dos grupos ({diaDoTorneio:dd/MM HH:mm})"));

        // 4 jogos em 2 quadras não cabem num horário só.
        Assert.All(quartas.GroupBy(p => p.HorarioPrevisto),
            leva => Assert.True(leva.Count() <= torneio.QuantidadeQuadras,
                $"{leva.Count()} jogos às {leva.Key:HH:mm} num torneio de {torneio.QuantidadeQuadras} quadras"));

        // Duas quadras no mesmo horário nunca são a mesma quadra.
        Assert.All(quartas.GroupBy(p => p.HorarioPrevisto),
            leva => Assert.Equal(leva.Count(), leva.Select(p => p.NomeQuadra).Distinct().Count()));
    }

    [Fact]
    public async Task Avanco_de_fase_pela_tela_cheia_tambem_entra_na_grade()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = MontarComQuadras(ctx);
        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);

        var grupos = await ctx.Partidas.Where(p => p.CategoriaId == categoria.Id).ToListAsync();
        await FinalizarPelaTelaCheiaAsync(ctx, org.Id, grupos);

        var quartas = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Quartas de Final").ToListAsync();
        var fimDasQuartas = quartas.Max(p => p.HorarioPrevisto!.Value);

        await FinalizarPelaTelaCheiaAsync(ctx, org.Id, quartas);

        var semis = await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id && p.Fase == "Semifinal").ToListAsync();

        Assert.Equal(2, semis.Count);
        Assert.All(semis, p => Assert.NotNull(p.HorarioPrevisto));
        Assert.All(semis, p => Assert.False(string.IsNullOrEmpty(p.NomeQuadra)));

        // Uma fase nunca abre no horário da fase que a decide — quem joga a semifinal é quem
        // acabou de sair da quadra das quartas.
        Assert.All(semis, p => Assert.True(p.HorarioPrevisto! > fimDasQuartas,
            $"semifinal às {p.HorarioPrevisto:dd/MM HH:mm} com as quartas terminando {fimDasQuartas:dd/MM HH:mm}"));
    }

    // A prova direta: o MESMO torneio, sorteado igual, rodado por uma tela e pela outra, tem
    // que terminar com a mesma chave e a mesma grade. Enquanto havia duas cópias do robô, o
    // resultado do torneio dependia de qual tela o organizador tinha aberto.
    [Fact]
    public async Task As_duas_telas_produzem_a_mesma_chave_e_a_mesma_grade()
    {
        var pelaTelaCheia = await RodarAteOFimAsync(porTelaCheia: true);
        var pelaMesa = await RodarAteOFimAsync(porTelaCheia: false);

        // Comparado como texto de uma vez só: colection-diff do xUnit corta em "···" e
        // esconde justamente a linha que difere.
        // Comparado como texto de uma vez só: o diff de coleção do xUnit corta em "···" e
        // esconde justamente a linha que difere.
        Assert.Equal(string.Join("\n", pelaMesa), string.Join("\n", pelaTelaCheia));
    }

    // Devolve a chave inteira como texto comparável: fase, hora, quadra. Ordenado por
    // conteúdo porque o Id das partidas criadas pelos robôs não precisa coincidir.
    private static async Task<List<string>> RodarAteOFimAsync(bool porTelaCheia)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = MontarComQuadras(ctx);
        var torneios = TestInfra.NovoTorneiosController(ctx, org.Id);
        await torneios.GerarChaves(torneio.Id);

        // Sorteio é aleatório: pra comparar as duas telas o CONFRONTO tem que ser o mesmo,
        // então os grupos saem do mesmo lugar e só o caminho de finalizar muda. O que este
        // teste compara é a GRADE que cada caminho produz.
        for (int volta = 0; volta < 6; volta++)
        {
            var pendentes = await ctx.Partidas
                .Where(p => p.CategoriaId == categoria.Id && p.Status != "Finalizada")
                .OrderBy(p => p.Id)
                .ToListAsync();
            if (pendentes.Count == 0) break;

            if (porTelaCheia)
                await FinalizarPelaTelaCheiaAsync(ctx, org.Id, pendentes);
            else
                foreach (var jogo in pendentes)
                    await TestInfra.FinalizarComPlacarAsync(ctx, torneios, jogo, 9, 3);
        }

        return await ctx.Partidas
            .Where(p => p.CategoriaId == categoria.Id)
            .Select(p => p.Fase + " | " + p.HorarioPrevisto + " | " + (p.NomeQuadra ?? "sem quadra"))
            .OrderBy(linha => linha)
            .ToListAsync();
    }
}
