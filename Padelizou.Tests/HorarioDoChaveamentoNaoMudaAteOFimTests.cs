using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O HORÁRIO DO CHAVEAMENTO NÃO MUDA ATÉ O FIM DO TORNEIO (11/11/2026).
//
// 🗣️ Felipe: *"os jogos têm que seguir o horário do chaveamento, sem alteração depois, a menos que
// eu faça alguma alteração na mão — tivemos esse problema com o primeiro torneio, que quando foi
// passando de fase, ele foi mudando os horários do mata-mata"*.
//
// A aprovação grava a grade prevista como reserva (OHorarioDoSorteioEPromessaGravadaTests) e o robô
// cria cada eliminatória na reserva (OHorarioDoSorteioEPromessaTests — a semifinal). Aqui o torneio
// INTEIRO, duas categorias, do primeiro jogo de grupo até a última final: cada eliminatória que nasce
// tem que nascer exatamente no horário que estava prometido no dia da aprovação — e nenhum jogo de
// grupo pode ter mudado de horário no caminho.
public class HorarioDoChaveamentoNaoMudaAteOFimTests
{

    [Theory]
    [InlineData(10, 8, 2)]
    [InlineData(12, 12, 2)]
    [InlineData(9, 6, 1)]
    [InlineData(16, 7, 3)]
    [InlineData(6, 11, 2)]
    public async Task Do_primeiro_jogo_a_ultima_final_todo_horario_e_o_da_aprovacao(
        int duplasDaPrimeira, int duplasDaSegunda, int quadras)
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, primeira, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: duplasDaPrimeira);
        torneio.QuantidadeQuadras = quadras;
        torneio.TempoPrevistoPartidaMinutos = 50;
        for (int q = 1; q <= quadras; q++)
            ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = $"Quadra {q}" });
        var segunda = new Categoria { Nome = "4ª Categoria Masculina", Codigo = "CAT4M", Torneio = torneio };
        ctx.Categorias.Add(segunda);
        for (int i = 0; i < duplasDaSegunda; i++)
        {
            var j1 = TestInfra.NovoJogador(300 + i * 2);
            var j2 = TestInfra.NovoJogador(301 + i * 2);
            ctx.Jogadores.AddRange(j1, j2);
            ctx.Duplas.Add(new Dupla { Categoria = segunda, Jogador1 = j1, Jogador2 = j2 });
        }
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id, sortearMesmoSemCaber: true);
        await TestInfra.NovoTorneiosController(ctx, org.Id).AprovarChaves(torneio.Id, avisarJogadores: false);

        // O que ficou combinado no dia da aprovação.
        var gruposNaAprovacao = await ctx.Partidas.AsNoTracking().Where(p => p.TorneioId == torneio.Id)
            .ToDictionaryAsync(p => p.Id, p => p.HorarioPrevisto);
        var prometido = (await ctx.ReservasDeHorario.AsNoTracking()
                .Where(r => r.Categoria.TorneioId == torneio.Id).ToListAsync())
            .ToDictionary(r => (r.CategoriaId, r.Fase, r.Numero), r => r.Horario);
        Assert.NotEmpty(prometido);

        // Joga o torneio inteiro, sempre o próximo jogo pelo horário, até não sobrar nenhum.
        for (int volta = 0; volta < 200; volta++)
        {
            var proximo = await ctx.Partidas
                .Where(p => p.TorneioId == torneio.Id && p.Status == "Agendada")
                .OrderBy(p => p.HorarioPrevisto).ThenBy(p => p.Id)
                .FirstOrDefaultAsync();
            if (proximo == null) break;
            await TestInfra.FinalizarComPlacarAsync(ctx, TestInfra.NovoTorneiosController(ctx, org.Id), proximo, 9, volta % 8);
        }

        ctx.ChangeTracker.Clear();
        var todos = await ctx.Partidas.AsNoTracking().Where(p => p.TorneioId == torneio.Id).ToListAsync();
        Assert.All(todos, p => Assert.Equal("Finalizada", p.Status));

        // Nenhum jogo de grupo mudou de horário.
        foreach (var (id, horario) in gruposNaAprovacao)
            Assert.Equal(horario, todos.Single(p => p.Id == id).HorarioPrevisto);

        // Cada eliminatória nasceu no horário prometido na aprovação.
        var numero = ReservasDeHorario.NumeroNaFase(todos);
        var eliminatorias = todos.Where(p => !FasesTorneio.EhFaseDeGrupos(p.Fase)).ToList();
        Assert.NotEmpty(eliminatorias);
        foreach (var jogo in eliminatorias)
        {
            var chave = (jogo.CategoriaId, jogo.Fase, numero[jogo.Id]);
            Assert.True(prometido.TryGetValue(chave, out var naAprovacao),
                $"{jogo.Fase} {numero[jogo.Id]} (categoria {jogo.CategoriaId}) não tinha horário prometido");
            Assert.True(naAprovacao == jogo.HorarioPrevisto,
                $"{jogo.Fase} {numero[jogo.Id]} (categoria {jogo.CategoriaId}): prometido {naAprovacao:ddd HH:mm}, nasceu {jogo.HorarioPrevisto:ddd HH:mm}");
        }
    }
}
