using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 31/08/2026 — a aba de inscritos abre em "Todos" pra quem NÃO está inscrito.
//
// A escolha de qual categoria abrir já existia e tinha razão escrita no controller ("abre
// direto na categoria em que o usuário logado já está inscrito"). O que muda é só o OUTRO
// caso: quem não está inscrito caía na PRIMEIRA categoria do torneio — uma escolha sem
// motivo nenhum, que mostrava 2 duplas de 9 categorias pra quem quer ver o torneio.
public class AbaDeInscritosAbreEmTodosTests
{
    private static async Task<ViewResult> AbrirAsync(DbPadelContext ctx, int torneioId, int jogadorId)
    {
        var resultado = await TestInfra.NovoTorneiosController(ctx, jogadorId).Details(torneioId, null, null);
        return Assert.IsType<ViewResult>(resultado);
    }

    [Fact]
    public async Task Quem_nao_esta_inscrito_abre_em_Todos()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");

        var visitante = TestInfra.NovoJogador(90);
        ctx.Jogadores.Add(visitante);
        await ctx.SaveChangesAsync();

        var view = await AbrirAsync(ctx, torneio.Id, visitante.Id);

        Assert.Equal(TodosOsInscritos.TodasAsCategorias, view.ViewData["CategoriaSelecionadaId"]);
    }

    [Fact]
    public async Task Quem_ESTA_inscrito_continua_abrindo_na_categoria_dele()
    {
        // Regressão da decisão que já existia: ela não podia ser atropelada pelo "Todos".
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 1, status: "Inscrições Abertas");
        var inscrito = ctx.Duplas.First(d => d.CategoriaId == categoria.Id).Jogador1Id;

        var view = await AbrirAsync(ctx, torneio.Id, inscrito);

        Assert.Equal(categoria.Id, view.ViewData["CategoriaSelecionadaId"]);
    }
}

// A LIGAÇÃO COM A TELA — teste de FONTE, pelo motivo de sempre aqui: a suíte não renderiza
// Razor, então uma view que voltasse a ordenar na mão (ou perdesse a opção "Todos") não
// quebraria teste nenhum de comportamento.
public class AbaDeInscritosTemAOpcaoTodosTests
{
    private static string Fonte() => File.ReadAllText(
        Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

    // Do seletor de categoria da aba de inscritos até o fim dele.
    private static string BlocoDoSeletor()
    {
        var fonte = Fonte();
        var inicio = fonte.IndexOf("id=\"seletorCategoriaInscritos\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Não achei o seletor de categoria da aba de inscritos — o id mudou e esta trava parou de olhar pra ele.");

        var fim = fonte.IndexOf("</select>", inicio, StringComparison.Ordinal);
        Assert.True(fim > inicio, "Não achei o fim do seletor de categoria da aba de inscritos.");

        return fonte[inicio..fim];
    }

    [Fact]
    public void Todos_e_a_PRIMEIRA_opcao_do_seletor()
    {
        // "deixe na primeira" foi o pedido, e é o que faz a opção ser achada: uma lista de
        // nove categorias com "Todos" no fim é uma opção que ninguém rola pra ver.
        var bloco = BlocoDoSeletor();

        var todos = bloco.IndexOf("Todos", StringComparison.Ordinal);
        var primeiraCategoria = bloco.IndexOf("foreach", StringComparison.Ordinal);

        Assert.True(todos >= 0, "A opção \"Todos\" sumiu do seletor da aba de inscritos.");
        Assert.True(todos < primeiraCategoria, "A opção \"Todos\" precisa vir ANTES das categorias no seletor.");
    }

    [Fact]
    public void A_lista_de_Todos_sai_do_servico_e_nao_de_um_OrderBy_solto_na_view()
    {
        Assert.Contains("TodosOsInscritos.MaisRecentesPrimeiro", Fonte(), StringComparison.Ordinal);
    }

    [Fact]
    public void O_painel_de_Todos_existe_e_e_o_que_o_seletor_liga_e_desliga()
    {
        // O script mostra/esconde por `data-categoria-id`; sem o painel com o id do "Todos",
        // escolher a opção esconderia tudo e a aba ficaria em branco.
        Assert.Contains("data-categoria-id=\"@Padelizou.Services.TodosOsInscritos.TodasAsCategorias\"",
            Fonte(), StringComparison.Ordinal);
    }

    [Fact]
    public void Cada_card_de_Todos_diz_de_que_categoria_e_pro_selo_de_historico()
    {
        // ⚠️ O chip do jogador desenha o selo de histórico da categoria que estiver em
        // `ViewData["CategoriaAtual"]`. Numa lista misturada, sem reescrever isso card a
        // card, todo mundo herdaria a categoria de quem veio antes — e o selo mentiria
        // calado, que é o pior jeito de errar.
        var fonte = Fonte();
        var inicio = fonte.IndexOf("TodosOsInscritos.MaisRecentesPrimeiro", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "O painel de \"Todos\" não está montando a lista pelo serviço.");

        var fim = fonte.IndexOf("@foreach (var cat in Model.Categorias)", inicio, StringComparison.Ordinal);
        Assert.True(fim > inicio, "Não achei onde termina o painel de \"Todos\" (o laço das categorias vem logo depois dele).");

        Assert.Contains("ViewData[\"CategoriaAtual\"]", fonte[inicio..fim], StringComparison.Ordinal);
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
