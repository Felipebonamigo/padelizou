using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// No começo não existe clube nem cidade nenhuma cadastrada. Se o cadastro só deixasse
// escolher de uma lista vazia, a pessoa não conseguiria dizer onde joga.
public class CadastroCatalogosTests
{
    [Fact]
    public async Task Cria_o_clube_quando_ainda_nao_existe()
    {
        using var ctx = TestInfra.NovoContexto();

        var clube = await CatalogoLocais.AcharOuCriarClubeAsync(ctx, "Nata Padel");

        Assert.NotNull(clube);
        Assert.Equal("Nata Padel", clube!.Nome);
        Assert.Single(ctx.Clubes);
    }

    [Theory]
    [InlineData("nata padel")]
    [InlineData("NATA PADEL")]
    [InlineData("  Nata Padel  ")]
    public async Task Nao_duplica_clube_por_caixa_ou_espaco(string digitado)
    {
        using var ctx = TestInfra.NovoContexto();
        await CatalogoLocais.AcharOuCriarClubeAsync(ctx, "Nata Padel");

        var segundo = await CatalogoLocais.AcharOuCriarClubeAsync(ctx, digitado);

        Assert.Single(ctx.Clubes);
        Assert.Equal("Nata Padel", segundo!.Nome);
    }

    [Fact]
    public async Task Nome_vazio_nao_cria_nada()
    {
        using var ctx = TestInfra.NovoContexto();

        Assert.Null(await CatalogoLocais.AcharOuCriarClubeAsync(ctx, "   "));
        Assert.Null(await CatalogoLocais.AcharOuCriarClubeAsync(ctx, null));
        Assert.Null(await CatalogoLocais.AcharOuCriarCidadeAsync(ctx, "", "RS"));
        Assert.Empty(ctx.Clubes);
        Assert.Empty(ctx.Cidades);
    }

    [Fact]
    public async Task Cidade_guarda_a_uf_sempre_em_maiusculas()
    {
        using var ctx = TestInfra.NovoContexto();

        var cidade = await CatalogoLocais.AcharOuCriarCidadeAsync(ctx, "Porto Alegre", "rs");

        Assert.Equal("Porto Alegre", cidade!.Nome);
        Assert.Equal("RS", cidade.Estado);
    }

    [Fact]
    public async Task Nao_duplica_cidade_ja_cadastrada()
    {
        using var ctx = TestInfra.NovoContexto();
        await CatalogoLocais.AcharOuCriarCidadeAsync(ctx, "Canoas", "RS");

        await CatalogoLocais.AcharOuCriarCidadeAsync(ctx, "canoas", "rs");

        Assert.Single(ctx.Cidades);
    }

    [Fact]
    public async Task Nome_gigante_e_cortado_no_limite_da_coluna()
    {
        using var ctx = TestInfra.NovoContexto();

        var clube = await CatalogoLocais.AcharOuCriarClubeAsync(ctx, new string('x', 500));

        Assert.Equal(CatalogoLocais.TamanhoMaximoNome, clube!.Nome.Length);
    }
}
