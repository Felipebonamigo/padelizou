using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;

namespace Padelizou.Tests;

// Categoria desligada (CategoriaPadrao.Ativa = false) é categoria tirada de circulação: some
// das telas de escolha e não entra em torneio novo, mas continua no banco — torneio antigo,
// preferência de jogador e aviso guardam o Id dela, e apagar a linha deixaria tudo isso órfão.
//
// Nasceu em 07/08/2026, quando as duas "Iniciantes" saíram do catálogo.
public class CategoriaDesligadaTests
{
    private const int AtivaId = 3;
    private const int DesligadaId = 8;

    private static Torneio TorneioValido() => new()
    {
        Nome = "Copa Teste",
        ClubeId = 1,
        Status = "Inscrições Abertas",
        SetsFaseGrupos = 1,
        GamesFaseGrupos = 6,
        RestricaoCategoria = "Livre",
        FormaPagamento = "Externo",
    };

    private static DbPadelContext ContextoComCatalogo()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Id = 1, Nome = "Organizador", Cpf = "1", IsOrganizadorTorneio = true });
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Clube Teste" });
        ctx.CategoriasPadrao.AddRange(
            new padelizou.Models.CategoriaPadrao
            {
                Id = AtivaId,
                Nome = "3ª Categoria Masculina",
                Codigo = "3CatM",
                Tipo = "Masculina",
            },
            new padelizou.Models.CategoriaPadrao
            {
                Id = DesligadaId,
                Nome = "Categoria Iniciantes Masculina",
                Codigo = "ICatM",
                Tipo = "Masculina",
                Ativa = false,
            });
        ctx.SaveChanges();
        return ctx;
    }

    [Fact]
    public void Categoria_do_catalogo_nasce_ativa()
    {
        // Desligar é escolha; o catálogo semeado no start conta com isto pra não nascer mudo.
        Assert.True(new padelizou.Models.CategoriaPadrao
        {
            Nome = "3ª Categoria Masculina",
            Codigo = "3CatM",
            Tipo = "Masculina",
        }.Ativa);
    }

    [Fact]
    public async Task A_tela_de_criar_torneio_nao_oferece_a_desligada()
    {
        using var ctx = ContextoComCatalogo();
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        var view = Assert.IsType<ViewResult>(await controller.Create());

        var catalogo = Assert.IsType<List<padelizou.Models.CategoriaPadrao>>(view.ViewData["CatalogoCategorias"]);
        Assert.Contains(catalogo, c => c.Id == AtivaId);
        Assert.DoesNotContain(catalogo, c => c.Id == DesligadaId);
    }

    [Fact]
    public async Task Desligada_nao_entra_no_torneio_nem_por_POST_feito_a_mao()
    {
        // A tela não oferece, mas o formulário se monta à mão — e sem esta guarda a categoria
        // voltava pro ar por um atalho, com gente se inscrevendo nela.
        using var ctx = ContextoComCatalogo();
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: 1);

        await controller.Create(TorneioValido(), new[] { AtivaId, DesligadaId }, null, null, null, null);

        var categoria = Assert.Single(ctx.Categorias);
        Assert.Equal("3ª Categoria Masculina", categoria.Nome);
    }

    [Fact]
    public async Task Adicionar_categoria_no_torneio_ja_criado_tambem_recusa_a_desligada()
    {
        // A segunda porta pra mesma coisa: a aba "Gerenciar Torneio" põe categoria depois de
        // o torneio estar no ar. Corrigir só a criação deixaria o caminho de trás aberto.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0, status: "Inscrições Abertas");
        ctx.CategoriasPadrao.Add(new padelizou.Models.CategoriaPadrao
        {
            Id = DesligadaId,
            Nome = "Categoria Iniciantes Feminina",
            Codigo = "ICatF",
            Tipo = "Feminina",
            Ativa = false,
        });
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await controller.AdicionarCategoria(torneio.Id, DesligadaId, limiteDuplas: null);

        Assert.NotNull(controller.TempData["Erro"]);
        Assert.Single(ctx.Categorias);   // só a que o torneio já tinha
    }
}
