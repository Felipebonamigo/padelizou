using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Valer ponto no Ranking Americano é CONTRATADO pelo organizador (R$ 5 por pessoa inscrita),
// e só existe no Americano. No formato Padrão a caixa nem aparece — e um POST montado à mão
// não pode gravar um torneio de chave cobrando por um ranking em que ele nunca entra.
public class RankingAmericanoContratadoTests
{
    private static Torneio TorneioValido(string formato) => new()
    {
        Nome = "Copa Teste",
        ClubeId = 1,
        Status = "Inscrições Abertas",
        Formato = formato,
        SetsFaseGrupos = 1,
        GamesFaseGrupos = 6,
        RestricaoCategoria = "Livre",
        FormaPagamento = "Externo",
        PontuaNoRankingAmericano = true,
    };

    private static DbPadelContext ContextoComClube()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Id = 1, Nome = "Organizador", Cpf = "1" });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Clube Teste" });
        ctx.CategoriasPadrao.Add(new padelizou.Models.CategoriaPadrao
        {
            Id = 3,
            Nome = "3ª Categoria Masculina",
            Codigo = "3CatM",
            Tipo = "Masculina",
        });
        ctx.SaveChanges();
        return ctx;
    }

    [Fact]
    public void Torneio_novo_nao_pontua_ate_alguem_contratar()
    {
        Assert.False(new Torneio { Nome = "x", Codigo = "x" }.PontuaNoRankingAmericano);
    }

    [Fact]
    public async Task No_americano_a_escolha_do_organizador_e_gravada()
    {
        using var ctx = ContextoComClube();
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        await controller.Create(TorneioValido(FormatoDoTorneio.Americano),
            new[] { 3 }, null, null, null, null);

        var criado = Assert.Single(ctx.Torneios);
        Assert.True(criado.PontuaNoRankingAmericano);
    }

    [Fact]
    public async Task No_formato_padrao_a_marca_e_ignorada_em_vez_de_recusar()
    {
        // Recusar seria errado: não é erro do organizador, é campo que não se aplica ao
        // formato que ele escolheu. O torneio nasce — sem a marca.
        using var ctx = ContextoComClube();
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        var resultado = await controller.Create(TorneioValido(FormatoDoTorneio.Padrao),
            new[] { 3 }, null, null, null, null);

        Assert.IsType<RedirectToActionResult>(resultado);
        var criado = Assert.Single(ctx.Torneios);
        Assert.False(criado.PontuaNoRankingAmericano);
    }

    [Fact]
    public async Task Formato_inventado_continua_recusado()
    {
        using var ctx = ContextoComClube();
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        var resultado = await controller.Create(TorneioValido("Maluco"),
            new[] { 3 }, null, null, null, null);

        Assert.IsType<ViewResult>(resultado);
        Assert.Empty(ctx.Torneios);
    }

    [Fact]
    public void Contratar_nao_muda_nada_no_ranking_OFICIAL()
    {
        // A trava do oficial é o FORMATO, não a contratação. Sem isto, pagar viraria um jeito
        // de comprar ponto no ranking de torneio — que é o oposto do que este campo faz.
        var americanoPago = new Torneio
        {
            Nome = "x",
            Codigo = "x",
            Formato = FormatoDoTorneio.Americano,
            PontuaNoRankingAmericano = true,
        };

        Assert.False(EstatisticasService.ContaNoRanking(americanoPago));
    }
}
