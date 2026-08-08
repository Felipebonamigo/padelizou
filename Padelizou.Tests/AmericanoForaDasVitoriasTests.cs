using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O Americano já estava fora do ranking POR CATEGORIA e do de Times — mas continuava contando
// em Vitórias (jogadores), Vitórias (duplas) e Invencibilidade, que saem de outra lista de
// partidas. Régua duplicada: corrigiram uma cópia e a outra seguiu contando.
//
// O estrago era visível em produção em 08/08/2026: o "Americano das Gurias" (10 jogadoras, 13
// jogos numa noite) ocupava sozinho as 8 primeiras posições do ranking geral de vitórias.
//
// A regra do Felipe: o Americano conta SÓ no Ranking Americano.
public class AmericanoForaDasVitoriasTests
{
    // Um torneio com 1 categoria, 2 duplas e 1 jogo finalizado — o suficiente pra aparecer
    // (ou não) nas três seções.
    private static async Task<Jogador> MontarTorneioComUmJogoAsync(
        DbPadelContext ctx, string formato, string nome, string cpfBase)
    {
        var torneio = new Torneio
        {
            Nome = nome,
            Codigo = nome.Replace(" ", "")[..Math.Min(8, nome.Replace(" ", "").Length)],
            Formato = formato,
            Status = "Finalizado",
            DataInicio = new DateTime(2026, 8, 1),
        };
        ctx.Torneios.Add(torneio);
        await ctx.SaveChangesAsync();

        var categoria = new Categoria { TorneioId = torneio.Id, Nome = "Livre", Codigo = "LIV" };
        ctx.Categorias.Add(categoria);
        await ctx.SaveChangesAsync();

        var jogadores = new List<Jogador>();
        for (int i = 0; i < 4; i++)
        {
            var j = new Jogador { Nome = $"{nome} — Jogador {i + 1}", Cpf = $"{cpfBase}{i:00}" };
            ctx.Jogadores.Add(j);
            jogadores.Add(j);
        }
        await ctx.SaveChangesAsync();

        var vencedora = new Dupla { CategoriaId = categoria.Id, Jogador1Id = jogadores[0].Id, Jogador2Id = jogadores[1].Id };
        var perdedora = new Dupla { CategoriaId = categoria.Id, Jogador1Id = jogadores[2].Id, Jogador2Id = jogadores[3].Id };
        ctx.Duplas.AddRange(vencedora, perdedora);
        await ctx.SaveChangesAsync();

        ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Codigo = Guid.NewGuid().ToString("N")[..8],
            Fase = FormatoDoTorneio.EhAmericano(formato) ? FaseDoAmericano.RodadaUnica(1) : "Final",
            Status = "Finalizada",
            Dupla1Id = vencedora.Id,
            Dupla2Id = perdedora.Id,
            GamesDupla1 = 6,
            GamesDupla2 = 2,
            VencedorId = vencedora.Id,
            HorarioFimReal = new DateTime(2026, 8, 1, 20, 0, 0),
        });
        await ctx.SaveChangesAsync();

        return jogadores[0];   // quem venceu
    }

    [Theory]
    [InlineData(FormatoDoTorneio.Americano)]
    [InlineData(FormatoDoTorneio.AmericanoDeDuplas)]
    public async Task Vencer_um_AMERICANO_nao_entra_em_vitorias_nem_invencibilidade(string formato)
    {
        using var ctx = TestInfra.NovoContexto();
        var campea = await MontarTorneioComUmJogoAsync(ctx, formato, "Rodizio de Sabado", "7710");

        var hub = await new EstatisticasService(ctx).ObterRankingHubAsync(null, null, null);

        Assert.DoesNotContain(hub.VitoriasJogadores, l => l.Jogador.Id == campea.Id);
        Assert.Empty(hub.VitoriasDuplas);
        Assert.DoesNotContain(hub.InvictosJogadores, l => l.Jogador.Id == campea.Id);
        Assert.Empty(hub.InvictasDuplas);
    }

    [Fact]
    public async Task Vencer_um_OFICIAL_continua_entrando_nas_tres()
    {
        // A regressão que importa: o filtro não pode levar o torneio de chave junto.
        using var ctx = TestInfra.NovoContexto();
        var campeao = await MontarTorneioComUmJogoAsync(ctx, FormatoDoTorneio.Padrao, "Copa de Chave", "7720");

        var hub = await new EstatisticasService(ctx).ObterRankingHubAsync(null, null, null);

        Assert.Contains(hub.VitoriasJogadores, l => l.Jogador.Id == campeao.Id && l.Vitorias == 1);
        Assert.NotEmpty(hub.VitoriasDuplas);
        Assert.Contains(hub.InvictosJogadores, l => l.Jogador.Id == campeao.Id);
    }

    [Fact]
    public async Task Torneio_RESTRITO_tambem_fica_de_fora_das_tres()
    {
        // Mesma régua, outro motivo (evento fechado). Estava furada pelo mesmo buraco.
        using var ctx = TestInfra.NovoContexto();
        var campeao = await MontarTorneioComUmJogoAsync(ctx, FormatoDoTorneio.Padrao, "Interno do Clube", "7730");

        var torneio = await ctx.Torneios.FirstAsync(t => t.Nome == "Interno do Clube");
        torneio.Restrito = true;
        await ctx.SaveChangesAsync();

        var hub = await new EstatisticasService(ctx).ObterRankingHubAsync(null, null, null);

        Assert.DoesNotContain(hub.VitoriasJogadores, l => l.Jogador.Id == campeao.Id);
        Assert.Empty(hub.VitoriasDuplas);
    }

    [Fact]
    public async Task O_Americano_nao_apaga_o_Oficial_de_quem_joga_os_dois()
    {
        // Quem joga rodízio E torneio de chave aparece no ranking oficial pela campanha do
        // OFICIAL — com o número dele, não inflado pelo rodízio.
        using var ctx = TestInfra.NovoContexto();
        var campeao = await MontarTorneioComUmJogoAsync(ctx, FormatoDoTorneio.Padrao, "Copa de Chave", "7740");

        // A MESMA pessoa vence também um Americano.
        var americano = new Torneio
        {
            Nome = "Rodizio", Codigo = "ROD", Formato = FormatoDoTorneio.Americano,
            Status = "Finalizado", DataInicio = new DateTime(2026, 8, 2),
        };
        ctx.Torneios.Add(americano);
        await ctx.SaveChangesAsync();
        var cat = new Categoria { TorneioId = americano.Id, Nome = "Livre", Codigo = "LIV" };
        ctx.Categorias.Add(cat);
        await ctx.SaveChangesAsync();

        var outro = new Jogador { Nome = "Parceiro de rodizio", Cpf = "774099" };
        ctx.Jogadores.Add(outro);
        await ctx.SaveChangesAsync();

        var d1 = new Dupla { CategoriaId = cat.Id, Jogador1Id = campeao.Id, Jogador2Id = outro.Id };
        var d2 = new Dupla { CategoriaId = cat.Id, Jogador1Id = outro.Id, Jogador2Id = campeao.Id };
        ctx.Duplas.AddRange(d1, d2);
        await ctx.SaveChangesAsync();
        ctx.Partidas.Add(new Partida
        {
            TorneioId = americano.Id, CategoriaId = cat.Id,
            Codigo = Guid.NewGuid().ToString("N")[..8],
            Fase = FaseDoAmericano.RodadaUnica(1), Status = "Finalizada",
            Dupla1Id = d1.Id, Dupla2Id = d2.Id, GamesDupla1 = 6, GamesDupla2 = 1,
            VencedorId = d1.Id, HorarioFimReal = new DateTime(2026, 8, 2, 20, 0, 0),
        });
        await ctx.SaveChangesAsync();

        var hub = await new EstatisticasService(ctx).ObterRankingHubAsync(null, null, null);

        var linha = hub.VitoriasJogadores.Single(l => l.Jogador.Id == campeao.Id);
        Assert.Equal(1, linha.Vitorias);   // a do Oficial, e só ela
        Assert.Equal(1, linha.Jogos);
    }
}
