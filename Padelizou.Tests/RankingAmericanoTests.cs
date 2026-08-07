using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O Ranking Americano só conta o Americano que passou pelas QUATRO condições: contratado,
// pago, com o piso de 8 pessoas e terminado.
//
// Cada uma delas fecha um buraco diferente, e é por isso que cada uma tem teste próprio: são
// as quatro maneiras de alguém entrar no ranking sem ter direito.
public class RankingAmericanoTests
{
    // Monta um Americano fechado: N inscritos, N/2 partidas de uma rodada só, todas
    // finalizadas, com o primeiro jogador somando mais games que os outros.
    private static async Task<Torneio> MontarAmericanoAsync(
        DbPadelContext ctx, int pessoas, bool contratou, bool pagou, string status = "Finalizado")
    {
        var torneio = new Torneio
        {
            Nome = "Americano de Teste",
            Codigo = "AM" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
            Formato = FormatoDoTorneio.Americano,
            Status = status,
            DataInicio = new DateTime(2026, 8, 1),
            PontuaNoRankingAmericano = contratou,
            RankingAmericanoPagoEm = pagou ? new DateTime(2026, 8, 2) : null,
        };
        ctx.Torneios.Add(torneio);
        await ctx.SaveChangesAsync();

        var categoria = new Categoria { TorneioId = torneio.Id, Nome = "Livre", Codigo = "LIV" };
        ctx.Categorias.Add(categoria);
        await ctx.SaveChangesAsync();

        // Reaproveita quem já existe pelo CPF: chamar esta montagem duas vezes tem que
        // devolver AS MESMAS pessoas, senão o teste de "dois Americanos somam" mediria dois
        // grupos de estranhos e passaria sem provar nada.
        var jogadores = new List<Jogador>();
        for (int i = 0; i < pessoas; i++)
        {
            var cpf = $"8880000{i:0000}";
            var j = await ctx.Jogadores.FirstOrDefaultAsync(x => x.Cpf == cpf);
            if (j == null)
            {
                j = new Jogador { Nome = $"Jogador {i + 1:00}", Cpf = cpf };
                ctx.Jogadores.Add(j);
            }
            jogadores.Add(j);
        }
        await ctx.SaveChangesAsync();

        foreach (var j in jogadores)
        {
            ctx.InscricoesAmericanas.Add(new InscricaoAmericana
            {
                CategoriaId = categoria.Id,
                JogadorId = j.Id,
            });
        }
        await ctx.SaveChangesAsync();

        // Uma rodada: pares (0,1) vs (2,3), (4,5) vs (6,7)... O par que leva o jogador 0 faz
        // mais games, então ele termina em 1º.
        for (int i = 0; i + 3 < pessoas; i += 4)
        {
            var d1 = new Dupla { CategoriaId = categoria.Id, Jogador1Id = jogadores[i].Id, Jogador2Id = jogadores[i + 1].Id };
            var d2 = new Dupla { CategoriaId = categoria.Id, Jogador1Id = jogadores[i + 2].Id, Jogador2Id = jogadores[i + 3].Id };
            ctx.Duplas.AddRange(d1, d2);
            await ctx.SaveChangesAsync();

            ctx.Partidas.Add(new Partida
            {
                TorneioId = torneio.Id,
                CategoriaId = categoria.Id,
                Codigo = Guid.NewGuid().ToString("N")[..8],
                Fase = FaseDoAmericano.RodadaUnica(1),
                Status = "Finalizada",
                Dupla1Id = d1.Id,
                Dupla2Id = d2.Id,
                // O primeiro par de cada jogo ganha; o jogo do jogador 0 ganha por mais.
                GamesDupla1 = i == 0 ? 9 : 6,
                GamesDupla2 = i == 0 ? 1 : 5,
            });
        }
        await ctx.SaveChangesAsync();

        return torneio;
    }

    [Fact]
    public async Task Americano_contratado_pago_e_terminado_entra_no_ranking()
    {
        using var ctx = TestInfra.NovoContexto();
        await MontarAmericanoAsync(ctx, pessoas: 8, contratou: true, pagou: true);

        var ranking = await new RankingAmericanoService(ctx).ListarAsync();

        Assert.NotEmpty(ranking);
        // 8 pessoas = peso 1, então o líder leva a tabela crua.
        Assert.Equal(100, ranking[0].Pontos);
        Assert.Equal(1, ranking[0].Vitorias);
        Assert.Equal(1, ranking[0].Americanos);
        Assert.Equal("Americano de Teste", ranking[0].UltimoNome);
    }

    [Fact]
    public async Task Sem_contratar_nao_entra()
    {
        using var ctx = TestInfra.NovoContexto();
        await MontarAmericanoAsync(ctx, pessoas: 8, contratou: false, pagou: true);

        Assert.Empty(await new RankingAmericanoService(ctx).ListarAsync());
    }

    [Fact]
    public async Task Contratado_e_NAO_PAGO_nao_entra()
    {
        // Contratar é intenção. Se marcar a caixinha bastasse, o preço não existiria — e é o
        // preço que impede o ranking de ser fabricado.
        using var ctx = TestInfra.NovoContexto();
        await MontarAmericanoAsync(ctx, pessoas: 8, contratou: true, pagou: false);

        Assert.Empty(await new RankingAmericanoService(ctx).ListarAsync());
    }

    [Fact]
    public async Task Abaixo_do_piso_nao_entra_mesmo_pago()
    {
        using var ctx = TestInfra.NovoContexto();
        await MontarAmericanoAsync(ctx, pessoas: 4, contratou: true, pagou: true);

        Assert.Empty(await new RankingAmericanoService(ctx).ListarAsync());
    }

    [Fact]
    public async Task Americano_em_andamento_nao_entra()
    {
        // A colocação só existe quando o último jogo saiu. Ponto que aparece e some é pior que
        // ponto que demora.
        using var ctx = TestInfra.NovoContexto();
        await MontarAmericanoAsync(ctx, pessoas: 8, contratou: true, pagou: true, status: "Fase de Grupos");

        Assert.Empty(await new RankingAmericanoService(ctx).ListarAsync());
    }

    [Fact]
    public async Task O_peso_do_tamanho_chega_no_ranking()
    {
        // 16 pessoas: o líder leva 200, não 100. É a prova de que o peso não ficou só na
        // função pura — ele atravessa até a tabela que o jogador vê.
        using var ctx = TestInfra.NovoContexto();
        await MontarAmericanoAsync(ctx, pessoas: 16, contratou: true, pagou: true);

        var ranking = await new RankingAmericanoService(ctx).ListarAsync();

        Assert.Equal(200, ranking[0].Pontos);
    }

    [Fact]
    public async Task Dois_americanos_somam_no_mesmo_jogador()
    {
        using var ctx = TestInfra.NovoContexto();
        await MontarAmericanoAsync(ctx, pessoas: 8, contratou: true, pagou: true);

        var ranking = await new RankingAmericanoService(ctx).ListarAsync();
        var lider = ranking[0].Jogador.Id;
        var pontosDeUm = ranking[0].Pontos;

        // O mesmo jogador num segundo Americano: os CPFs se repetem, então ele é a mesma
        // pessoa e os pontos se somam.
        await MontarAmericanoAsync(ctx, pessoas: 8, contratou: true, pagou: true);

        var depois = await new RankingAmericanoService(ctx).ListarAsync();
        var mesmo = depois.First(l => l.Jogador.Id == lider);

        Assert.Equal(pontosDeUm * 2, mesmo.Pontos);
        Assert.Equal(2, mesmo.Americanos);
    }

    [Fact]
    public async Task O_filtro_regional_vale_aqui_tambem()
    {
        using var ctx = TestInfra.NovoContexto();
        await MontarAmericanoAsync(ctx, pessoas: 8, contratou: true, pagou: true);

        var todos = await new RankingAmericanoService(ctx).ListarAsync();
        var soUm = await new RankingAmericanoService(ctx).ListarAsync(
            new HashSet<int> { todos[0].Jogador.Id });

        Assert.Single(soUm);
        Assert.Equal(todos[0].Jogador.Id, soUm[0].Jogador.Id);
    }
}
