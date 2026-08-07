using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O AMERICANO DE DUPLAS no ranking. Ele estava fora de duas maneiras ao mesmo tempo, e nenhuma
// delas dava erro na tela:
//
//   1. A contagem de pessoas olhava só `InscricaoAmericana` — onde o formato INDIVIDUAL grava.
//      O de duplas grava em `Dupla` (herdou o caminho do Padrão), então ele aparecia com ZERO
//      pessoas: abaixo do piso de 8, o que quer dizer "não pontua e não se cobra".
//   2. A colocação vinha da tabela POR PESSOA. Como os dois parceiros da dupla fixa jogam
//      exatamente as mesmas partidas, as somas empatam e o desempate por Id os separa: a dupla
//      campeã levava 1º (100) e 2º (60) pelo MESMO resultado.
public class RankingAmericanoDeDuplasTests
{
    // Um Americano de Duplas fechado: `duplas` pares, todos contra todos numa rodada, com a
    // primeira dupla somando mais games que as outras.
    private static async Task<(Torneio Torneio, List<Dupla> Duplas)> MontarAsync(
        DbPadelContext ctx, int duplas, bool contratou = true, bool pagou = true)
    {
        var torneio = new Torneio
        {
            Nome = "Americano de Duplas de Teste",
            Codigo = "AD" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
            Formato = FormatoDoTorneio.AmericanoDeDuplas,
            Status = "Finalizado",
            DataInicio = new DateTime(2026, 8, 1),
            PontuaNoRankingAmericano = contratou,
            RankingAmericanoPagoEm = pagou ? new DateTime(2026, 8, 2) : null,
        };
        ctx.Torneios.Add(torneio);
        await ctx.SaveChangesAsync();

        var categoria = new Categoria { TorneioId = torneio.Id, Nome = "Livre", Codigo = "LIV" };
        ctx.Categorias.Add(categoria);
        await ctx.SaveChangesAsync();

        var criadas = new List<Dupla>();
        for (int i = 0; i < duplas; i++)
        {
            var a = new Jogador { Nome = $"Dupla {i + 1} — A", Cpf = $"777{i:0000}001" };
            var b = new Jogador { Nome = $"Dupla {i + 1} — B", Cpf = $"777{i:0000}002" };
            ctx.Jogadores.AddRange(a, b);
            await ctx.SaveChangesAsync();

            var dupla = new Dupla { CategoriaId = categoria.Id, Jogador1Id = a.Id, Jogador2Id = b.Id };
            ctx.Duplas.Add(dupla);
            await ctx.SaveChangesAsync();
            criadas.Add(dupla);
        }

        // Todas contra todas. A dupla 0 vence por 9x1; as outras se enfrentam em 6x5, então ela
        // termina isolada na liderança em games.
        for (int i = 0; i < criadas.Count; i++)
        {
            for (int j = i + 1; j < criadas.Count; j++)
            {
                ctx.Partidas.Add(new Partida
                {
                    TorneioId = torneio.Id,
                    CategoriaId = categoria.Id,
                    Codigo = Guid.NewGuid().ToString("N")[..8],
                    Fase = FaseDoAmericano.RodadaUnica(1),
                    Status = "Finalizada",
                    Dupla1Id = criadas[i].Id,
                    Dupla2Id = criadas[j].Id,
                    GamesDupla1 = i == 0 ? 9 : 6,
                    GamesDupla2 = i == 0 ? 1 : 5,
                });
            }
        }
        await ctx.SaveChangesAsync();

        return (torneio, criadas);
    }

    [Fact]
    public async Task Os_DOIS_da_dupla_campea_levam_os_MESMOS_pontos()
    {
        // O coração do defeito: mesmo resultado, mesma quadra, mesmos jogos — e um saía com
        // 100 e o outro com 60 porque a tabela individual precisou desempatar por Id.
        using var ctx = TestInfra.NovoContexto();
        var (_, duplas) = await MontarAsync(ctx, duplas: 4);   // 4 duplas = 8 pessoas = o piso

        var ranking = (await new RankingAmericanoService(ctx).ListarAsync()).Duplas;

        var campea = duplas[0];
        var um = ranking.Single(l => l.Jogador.Id == campea.Jogador1Id);
        var outro = ranking.Single(l => l.Jogador.Id == campea.Jogador2Id);

        Assert.Equal(um.Pontos, outro.Pontos);
        Assert.Equal(100, um.Pontos);      // 8 pessoas = peso 1, campeão = 100
        Assert.Equal(1, um.Vitorias);
        Assert.Equal(1, outro.Vitorias);   // o título é dos dois, como no mata-mata
    }

    [Fact]
    public async Task O_americano_de_duplas_ENTRA_no_ranking()
    {
        // Antes ele não entrava: a contagem de pessoas não sabia procurar em `Dupla`, devolvia
        // zero, e zero fica abaixo do piso.
        using var ctx = TestInfra.NovoContexto();
        await MontarAsync(ctx, duplas: 4);

        var ranking = await new RankingAmericanoService(ctx).ListarAsync();

        Assert.Equal(8, ranking.Duplas.Count);
        Assert.Empty(ranking.Individual);
    }

    [Fact]
    public async Task Abaixo_do_piso_de_8_pessoas_o_de_duplas_tambem_nao_pontua()
    {
        // 3 duplas = 6 pessoas. O piso é por PESSOA nos dois formatos — senão bastaria jogar
        // em duplas pra driblar a regra que existe justamente contra resultado combinado.
        using var ctx = TestInfra.NovoContexto();
        await MontarAsync(ctx, duplas: 3);

        Assert.Empty((await new RankingAmericanoService(ctx).ListarAsync()).Duplas);
    }

    [Fact]
    public async Task Os_dois_formatos_NAO_se_misturam_na_mesma_lista()
    {
        // É o pedido do Felipe virado teste: quem joga os dois aparece nas duas listas, com
        // pontos independentes — e nenhuma das duas vaza pra outra.
        using var ctx = TestInfra.NovoContexto();
        await MontarAsync(ctx, duplas: 4);

        var ranking = await new RankingAmericanoService(ctx).ListarAsync();

        Assert.NotEmpty(ranking.Duplas);
        Assert.Empty(ranking.Individual);
        Assert.All(ranking.Duplas, l => Assert.True(l.Pontos > 0));
    }

    [Fact]
    public async Task Contratado_mas_NAO_pago_fica_de_fora_igual_ao_individual()
    {
        using var ctx = TestInfra.NovoContexto();
        await MontarAsync(ctx, duplas: 4, contratou: true, pagou: false);

        Assert.Empty((await new RankingAmericanoService(ctx).ListarAsync()).Duplas);
    }
}
