using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O AMERICANO SAI DO RANKING OFICIAL (decisão do Felipe, 07/08/2026).
//
// Era uma porta escancarada, e não teórica: o rodízio de sábado pontuava igual à final de uma
// 3ª Categoria. Três amigos criam um Americano, lançam os placares que quiserem e fabricam
// ranking sem enfrentar ninguém de fora.
//
// E o estrago não era um campeão a mais: no Americano cada RODADA cria uma dupla nova, então
// um rodízio de 12 pessoas despejava dezenas de linhas de "participou" no ranking de uma vez.
//
// O Americano ganha ranking PRÓPRIO, separado e pago pelo organizador que quiser (RANKING.md).
public class AmericanoNaoPontuaNoOficialTests
{
    [Theory]
    [InlineData(FormatoDoTorneio.Padrao, true)]
    [InlineData(FormatoDoTorneio.Americano, false)]
    [InlineData(FormatoDoTorneio.AmericanoDeDuplas, false)]
    public void A_regra_pura_separa_por_formato(string formato, bool deveContar)
    {
        var torneio = new Torneio { Nome = "x", Codigo = "x", Formato = formato };

        Assert.Equal(deveContar, EstatisticasService.ContaNoRanking(torneio));
    }

    [Fact]
    public void As_duas_exclusoes_convivem()
    {
        // Restrito já estava fora desde 31/07. Uma regra nova não pode ressuscitar a antiga:
        // Americano restrito continua fora por dois motivos, e Padrão restrito por um.
        var restritoPadrao = new Torneio { Nome = "x", Codigo = "x", Restrito = true };
        var americanoAberto = new Torneio { Nome = "x", Codigo = "x", Formato = FormatoDoTorneio.Americano };

        Assert.False(EstatisticasService.ContaNoRanking(restritoPadrao));
        Assert.False(EstatisticasService.ContaNoRanking(americanoAberto));
    }

    [Fact]
    public void Dupla_sem_o_torneio_carregado_continua_contando()
    {
        // Consulta sem Include devolve o torneio nulo. Sumir do ranking por causa de um
        // Include esquecido seria pior do que qualquer uma das duas exclusões.
        Assert.True(EstatisticasService.ContaNoRanking(null));
    }

    [Fact]
    public async Task O_ranking_por_categoria_ignora_o_americano_inteiro()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 3, status: "Finalizado");
        torneio.Formato = FormatoDoTorneio.Americano;
        await ctx.SaveChangesAsync();

        var ranking = await new EstatisticasService(ctx).ObterRankingPorCategoriaAsync();

        Assert.Empty(ranking);
    }

    [Fact]
    public async Task O_mesmo_cenario_no_formato_padrao_PONTUA()
    {
        // O par do teste acima: sem ele, "ranking vazio" poderia ser qualquer outra coisa
        // quebrada na montagem, e o teste passaria sem provar nada.
        using var ctx = TestInfra.NovoContexto();
        TestInfra.MontarTorneio(ctx, qtdDuplas: 3, status: "Finalizado");
        await ctx.SaveChangesAsync();

        var ranking = await new EstatisticasService(ctx).ObterRankingPorCategoriaAsync();

        Assert.NotEmpty(ranking);
    }

    [Fact]
    public async Task O_padelimetro_tambem_ignora_partida_de_americano()
    {
        // O Padelímetro lê a MESMA regra (PadelimetroService.Conta chama ContaNoRanking), e é
        // de propósito: nível e ranking não podem discordar sobre o que é torneio de verdade.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Finalizado");
        torneio.Formato = FormatoDoTorneio.Americano;
        await ctx.SaveChangesAsync();

        var duplas = await ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToListAsync();
        categoria.Torneio = torneio;

        var partida = new Partida
        {
            Status = "Finalizada",
            GamesDupla1 = 6,
            GamesDupla2 = 2,
        };

        Assert.False(PadelimetroService.Conta(partida, categoria, duplas[0], duplas[1]));
    }
}
