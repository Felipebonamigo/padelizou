using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// "COPIAR CONFIGURAÇÕES DO ÚLTIMO TORNEIO" na tela de CRIAR (pedido do Felipe, 12/08/2026).
//
// É primo do "criar a próxima edição", e a diferença é de intenção:
//   · próxima edição  → dentro do torneio, cria na hora, ENTRA NA SÉRIE (ranking do circuito);
//   · copiar config   → na tela de criar, só PRÉ-PREENCHE, e NÃO herda série nem nome.
//
// O teste que mais importa aqui é o da porta: `?copiarDe=` de torneio alheio não pode
// devolver preço, chave Pix e limites de quem não é o dono.
public class CopiarConfiguracaoNaCriacaoTests
{
    // ─────────────────────────── A REGRA PURA ───────────────────────────

    [Fact]
    public void Copiar_para_o_formulario_NAO_herda_serie_nem_nome()
    {
        var original = new Torneio
        {
            Id = 7,
            Nome = "Copa de Agosto",
            Codigo = "AGO1",
            PrecoInscricao = 120m,
            QuantidadeQuadras = 3,
            UsaCheckIn = true,
        };

        var paraOFormulario = DuplicacaoDeTorneio.ParaOFormularioDeCriacao(original);

        // A configuração vem…
        Assert.Equal(120m, paraOFormulario.PrecoInscricao);
        Assert.Equal(3, paraOFormulario.QuantidadeQuadras);
        Assert.True(paraOFormulario.UsaCheckIn);

        // …mas a identidade, não. Sem isso, "Copa de Agosto" seria publicada de novo em
        // setembro com o mesmo nome, e o circuito somaria dois eventos diferentes.
        Assert.Equal("", paraOFormulario.Nome);
        Assert.Null(paraOFormulario.TorneioOrigemId);

        // E o "criar a próxima edição" continua fazendo o contrário — são os dois caminhos.
        Assert.Equal(7, DuplicacaoDeTorneio.CopiarConfiguracao(original).TorneioOrigemId);
        Assert.Equal("Copa de Agosto", DuplicacaoDeTorneio.CopiarConfiguracao(original).Nome);
    }

    // ─────────────────────────── O CAMINHO INTEIRO ───────────────────────────

    // Um torneio configurado de verdade: categorias do catálogo com limite, quadras com nome,
    // e a estrutura de dinheiro/formato preenchida.
    private static (DbPadelContext ctx, Torneio modelo, Jogador dono) Cenario()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, dono) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Finalizado");
        dono.IsOrganizadorTorneio = true;

        var doBanco = ctx.Torneios.First(t => t.Id == torneio.Id);
        doBanco.PrecoInscricao = 180m;
        doBanco.QuantidadeQuadras = 2;
        doBanco.UsaCheckIn = true;
        doBanco.TempoPrevistoPartidaMinutos = 40;

        // A categoria do torneio guarda o Codigo do catálogo — é por ele que a tela volta ao
        // checkbox certo. O catálogo do teste precisa ter esse código.
        ctx.CategoriasPadrao.Add(new padelizou.Models.CategoriaPadrao
        {
            Nome = categoria.Nome,
            Codigo = categoria.Codigo,
            Tipo = "Masculina",
            Ativa = true,
        });
        ctx.Categorias.First(c => c.Id == categoria.Id).LimiteDuplas = 12;

        ctx.Quadras.AddRange(
            new Quadra { TorneioId = torneio.Id, Nome = "Central" },
            new Quadra { TorneioId = torneio.Id, Nome = "Quadra 2" });
        ctx.SaveChanges();

        return (ctx, doBanco, dono);
    }

    [Fact]
    public async Task O_formulario_volta_preenchido_com_categorias_limites_e_quadras()
    {
        var (ctx, modelo, dono) = Cenario();
        using var _ = ctx;
        var controller = TestInfra.NovoTorneiosController(ctx, dono.Id);

        var view = Assert.IsType<ViewResult>(await controller.Create(copiarDe: modelo.Id));
        var preenchido = Assert.IsType<Torneio>(view.Model);

        // A configuração escalar.
        Assert.Equal(180m, preenchido.PrecoInscricao);
        Assert.Equal(2, preenchido.QuantidadeQuadras);
        Assert.True(preenchido.UsaCheckIn);
        Assert.Equal(40, preenchido.TempoPrevistoPartidaMinutos);
        Assert.Equal("", preenchido.Nome);

        // A categoria marcada é a do CATÁLOGO (casada pelo código), com o limite junto.
        var doCatalogo = ctx.CategoriasPadrao.First();
        var marcadas = Assert.IsType<int[]>(view.ViewData["CategoriasSelecionadas"]);
        Assert.Equal(new[] { doCatalogo.Id }, marcadas);

        var limites = Assert.IsType<Dictionary<int, int>>(view.ViewData["LimitesCopiados"]);
        Assert.Equal(12, limites[doCatalogo.Id]);

        // As quadras, na ordem em que nasceram — é a ordem que a preferência de quadra usa.
        var quadras = Assert.IsType<List<string>>(view.ViewData["NomesDeQuadraCopiados"]);
        Assert.Equal(new[] { "Central", "Quadra 2" }, quadras);

        // E a tela sabe dizer de onde veio.
        Assert.Equal(modelo.Nome, view.ViewData["CopiadoDe"]);
    }

    [Fact]
    public async Task Copiar_torneio_que_cobrava_PELO_SITE_sem_conta_conectada_cai_pra_POR_FORA()
    {
        var (ctx, modelo, dono) = Cenario();
        using var _ = ctx;
        ctx.Torneios.First(t => t.Id == modelo.Id).FormaPagamento = FormaDePagamentoDoTorneio.SoPix;
        await ctx.SaveChangesAsync();

        // Quem copia não tem conta de recebimento — o padrão do dublê é justamente esse.
        var controller = TestInfra.NovoTorneiosController(ctx, dono.Id);
        var view = Assert.IsType<ViewResult>(await controller.Create(copiarDe: modelo.Id));
        var copia = Assert.IsType<Torneio>(view.Model);

        // ⚠️ RECUSA ETERNA se isto não existisse: a tela desabilita os rádios "pelo site", e
        // rádio desabilitado não é enviado — o POST chegaria sem forma, o binder cairia no
        // valor do modelo ("pelo site" de novo) e a recusa se repetiria pra sempre.
        Assert.Equal(FormaDePagamentoDoTorneio.Externo, copia.FormaPagamento);
        Assert.Equal(true, view.ViewData["PagamentoVirouExterno"]);
    }

    [Fact]
    public async Task Torneio_de_OUTRA_pessoa_nao_e_copiado()
    {
        var (ctx, modelo, _) = Cenario();
        using var _ctx = ctx;

        // Um organizador liberado, mas que não é deste torneio: `?copiarDe=` na barra de
        // endereços não pode devolver preço, chave Pix e limites de quem não é o dono.
        var deFora = new Jogador { Nome = "De Fora", Cpf = "44400000001", IsOrganizadorTorneio = true };
        ctx.Jogadores.Add(deFora);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, deFora.Id);
        var view = Assert.IsType<ViewResult>(await controller.Create(copiarDe: modelo.Id));
        var formulario = Assert.IsType<Torneio>(view.Model);

        // Voltou o formulário EM BRANCO (o padrão da tela: 150 de sugestão), não o do outro.
        Assert.Equal(150m, formulario.PrecoInscricao);
        Assert.Equal(0, formulario.QuantidadeQuadras);
        Assert.Null(view.ViewData["CopiadoDe"]);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Sem_copiarDe_a_tela_de_criar_segue_como_sempre()
    {
        var (ctx, _, dono) = Cenario();
        using var _c = ctx;
        var controller = TestInfra.NovoTorneiosController(ctx, dono.Id);

        var view = Assert.IsType<ViewResult>(await controller.Create(copiarDe: null));
        var formulario = Assert.IsType<Torneio>(view.Model);

        Assert.Equal(150m, formulario.PrecoInscricao);
        Assert.Null(view.ViewData["CopiadoDe"]);

        // E a lista do atalho existe pra quem já organizou — é ela que faz o bloco aparecer.
        // ⚠️ O TIPO faz parte do teste: com tipo anônimo (internal) a view não enxerga a
        // lista, o `as` devolve nulo e o bloco some da tela sem erro nenhum. Foi assim que
        // este atalho nasceu invisível — pego olhando a tela, não pelos testes.
        var lista = Assert.IsType<List<Padelizou.ViewModels.TorneioParaCopiarVM>>(
            view.ViewData["TorneiosParaCopiar"]);
        Assert.NotEmpty(lista);
        Assert.All(lista, t => Assert.False(string.IsNullOrWhiteSpace(t.Nome)));
    }
}
