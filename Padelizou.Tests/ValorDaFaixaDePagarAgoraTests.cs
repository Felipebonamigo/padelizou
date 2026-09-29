using NSubstitute;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// A FAIXA "SUA INSCRIÇÃO ESTÁ SEM PAGAMENTO" DIZ UM NÚMERO. ELE É O QUE VAI SER COBRADO?
//
// 🗣️ Relato de um jogador (29/09/2026), num torneio de R$ 125 por pessoa em que ele estava em
// duas categorias: *"talvez fosse interessante dizer aqui as infos sobre esse valor... eu
// imagino que esses 250 é para as duas categorias"*. A pergunta dele era sobre TEXTO — e a
// suposição estava errada: R$ 250 é UMA categoria, com duas pessoas.
//
// 🕳️ O que ela desenterrou é dinheiro. A faixa RECALCULAVA o valor (`Torneio.ValorCobrado`,
// preço × 2 fixo) enquanto o checkout cobra o `ValorInscricao` GRAVADO na inscrição
// (`PagamentoInscricaoService.ValorJaCombinadoAsync`). É exatamente o que
// Services/PrecoDaInscricao avisa em letras maiúsculas: *"quem recalcula não tem como saber
// quem entrou pela segunda vez — e passa a mentir sem avisar"*. A faixa tinha ficado de fora
// daquela varredura de 08/08/2026.
public class ValorDaFaixaDePagarAgoraTests
{
    private static (DbPadelContext ctx, Torneio torneio, Categoria categoria, Jogador eu) Cenario(
        decimal preco = 125m, decimal? precoSegunda = null)
    {
        var ctx = TestInfra.NovoContexto();

        var eu = new Jogador { Nome = "Felipe Bonamigo", Cpf = "11122233344" };
        ctx.Jogadores.Add(eu);

        var torneio = new Torneio
        {
            Nome = "Torneio de Teste",
            Codigo = "TST999",
            Status = "Inscrições Abertas",
            DataInicio = DateTime.Today.AddDays(10),
            PrecoInscricao = preco,
            PrecoSegundaInscricao = precoSegunda,
            PermiteMultiplasCategorias = true,
            FormaPagamento = FormaDePagamentoDoTorneio.SoPix,
        };
        ctx.Torneios.Add(torneio);

        var categoria = new Categoria { Nome = "4ª Masculina", Codigo = "C4M", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        return (ctx, torneio, categoria, eu);
    }

    private static Dupla Inscrever(DbPadelContext ctx, Torneio torneio, Categoria categoria,
        Jogador eu, Jogador? parceiro, IEnumerable<bool> quemPaga)
    {
        var dupla = new Dupla
        {
            Categoria = categoria,
            Jogador1Id = eu.Id,
            Jogador2Id = parceiro?.Id,
            // Como a inscrição de verdade grava (DuplasController): é este número que o
            // checkout cobra, e o único que sabe quem pagou o preço de segunda.
            ValorInscricao = PrecoDaInscricao.Total(torneio, quemPaga),
        };
        ctx.Duplas.Add(dupla);
        ctx.SaveChanges();
        return dupla;
    }

    // O que a faixa mostra pra esta pessoa, com o torneio cobrando pelo site.
    private static async Task<List<InscricaoNaoPagaVM>> FaixaAsync(
        DbPadelContext ctx, int torneioId, int jogadorId)
    {
        var pagamentos = Substitute.For<IPagamentoInscricaoService>();
        pagamentos.PodeCobrar(Arg.Any<Torneio>(), Arg.Any<Jogador?>()).Returns(true);

        var controller = TestInfra.NovoTorneiosController(ctx, jogadorId, pagamentos);
        await controller.Details(torneioId, null, null);

        return controller.ViewBag.MinhasInscricoesNaoPagas as List<InscricaoNaoPagaVM>
               ?? new List<InscricaoNaoPagaVM>();
    }

    // ── O VALOR: o gravado, nunca o recalculado ──────────────────────────────────────

    [Fact]
    public async Task Quem_se_inscreveu_SEM_PARCEIRO_ve_o_que_vai_pagar_e_nao_o_dobro()
    {
        // A inscrição sozinha custa UMA pessoa desde 08/08/2026 (Felipe: cobrar duas "seria
        // cobrar por alguém que não foi definido"). A faixa mostrava R$ 250 e o checkout
        // cobrava R$ 125.
        var (ctx, torneio, categoria, eu) = Cenario();
        var dupla = Inscrever(ctx, torneio, categoria, eu, null, new[] { false });

        Assert.Equal(125m, dupla.ValorInscricao);

        var faixa = await FaixaAsync(ctx, torneio.Id, eu.Id);

        Assert.Equal(dupla.ValorInscricao, Assert.Single(faixa).Valor);
    }

    [Fact]
    public async Task Quem_esta_na_SEGUNDA_categoria_ve_o_valor_COM_o_desconto()
    {
        // O preço de segunda inscrição é por PESSOA: dois que já estão no torneio pagam
        // 2 × 90, não 2 × 125.
        var (ctx, torneio, categoria, eu) = Cenario(precoSegunda: 90m);
        var parceiro = new Jogador { Nome = "Andrey Souza", Cpf = "55566677788" };
        ctx.Jogadores.Add(parceiro);
        ctx.SaveChanges();

        var dupla = Inscrever(ctx, torneio, categoria, eu, parceiro, new[] { true, true });

        Assert.Equal(180m, dupla.ValorInscricao);

        var faixa = await FaixaAsync(ctx, torneio.Id, eu.Id);

        Assert.Equal(dupla.ValorInscricao, Assert.Single(faixa).Valor);
    }

    [Fact]
    public async Task Dupla_completa_no_preco_cheio_continua_batendo()
    {
        // O caso comum — e a regressão que o conserto precisa manter de pé.
        var (ctx, torneio, categoria, eu) = Cenario();
        var parceiro = new Jogador { Nome = "Andrey Souza", Cpf = "55566677788" };
        ctx.Jogadores.Add(parceiro);
        ctx.SaveChanges();

        var dupla = Inscrever(ctx, torneio, categoria, eu, parceiro, new[] { false, false });

        Assert.Equal(250m, dupla.ValorInscricao);

        var faixa = await FaixaAsync(ctx, torneio.Id, eu.Id);

        Assert.Equal(250m, Assert.Single(faixa).Valor);
    }

    // ── AS DUAS CATEGORIAS: a faixa mostrava UMA e escondia a outra ──────────────────

    [Fact]
    public async Task Quem_esta_em_DUAS_categorias_ve_as_DUAS_cobrancas()
    {
        // O caso do relato: 5ª com parceiro, 4ª sozinho. A consulta era um FirstOrDefault sem
        // ordenação — pegava uma qualquer e a outra não aparecia em lugar nenhum da faixa,
        // nem no botão. Quem pagasse ali continuaria devendo, sem nada na tela dizendo isso.
        var (ctx, torneio, quarta, eu) = Cenario();
        var quinta = new Categoria { Nome = "5ª Masculina", Codigo = "C5M", Torneio = torneio };
        ctx.Categorias.Add(quinta);
        var parceiro = new Jogador { Nome = "Andrey Souza", Cpf = "55566677788" };
        ctx.Jogadores.Add(parceiro);
        ctx.SaveChanges();

        Inscrever(ctx, torneio, quinta, eu, parceiro, new[] { false, false });   // 250
        Inscrever(ctx, torneio, quarta, eu, null, new[] { true });               // 125, 2ª categoria

        var faixa = await FaixaAsync(ctx, torneio.Id, eu.Id);

        Assert.Equal(2, faixa.Count);
        Assert.Contains(faixa, i => i.Categoria == "5ª Masculina" && i.Valor == 250m);
        Assert.Contains(faixa, i => i.Categoria == "4ª Masculina" && i.Valor == 125m);
    }

    [Fact]
    public async Task Cada_linha_diz_a_categoria_e_quem_esta_na_inscricao()
    {
        // O pedido do jogador, na letra: "dizer aqui as infos sobre esse valor". Sem a
        // categoria e sem quem está na inscrição, o total é um número solto — foi assim que
        // ele leu R$ 250 como "as duas categorias".
        var (ctx, torneio, categoria, eu) = Cenario();
        var parceiro = new Jogador { Nome = "Andrey Souza", Cpf = "55566677788" };
        ctx.Jogadores.Add(parceiro);
        ctx.SaveChanges();

        Inscrever(ctx, torneio, categoria, eu, parceiro, new[] { false, false });

        var linha = Assert.Single(await FaixaAsync(ctx, torneio.Id, eu.Id));

        Assert.Equal("4ª Masculina", linha.Categoria);
        Assert.Contains("Felipe", linha.Quem);
        Assert.Contains("Andrey", linha.Quem);
    }

    [Fact]
    public async Task Inscricao_ja_paga_nao_aparece()
    {
        var (ctx, torneio, categoria, eu) = Cenario();
        var dupla = Inscrever(ctx, torneio, categoria, eu, null, new[] { false });
        dupla.Pago = true;
        ctx.SaveChanges();

        Assert.Empty(await FaixaAsync(ctx, torneio.Id, eu.Id));
    }

    // ── A CONTA: só se afirma quando ela BATE com o valor gravado ────────────────────

    [Fact]
    public void A_conta_e_afirmada_quando_reproduz_o_valor_gravado()
    {
        var torneio = new Torneio { Nome = "T", Codigo = "T", PrecoInscricao = 125m };

        var conta = ContaDaInscricao.Frase(torneio, pessoas: 2, impedimentos: 0, valorGravado: 250m);

        Assert.NotNull(conta);
        Assert.Contains("2", conta!);
        Assert.Contains("125", conta);
    }

    [Fact]
    public void Com_desconto_de_segunda_categoria_a_conta_NAO_finge_ser_preco_cheio()
    {
        // 2 × R$ 125 não explica R$ 180. Afirmar a decomposição aqui seria a faixa mentindo
        // de novo, agora por escrito.
        var torneio = new Torneio
        {
            Nome = "T", Codigo = "T", PrecoInscricao = 125m,
            PrecoSegundaInscricao = 90m, PermiteMultiplasCategorias = true,
        };

        var conta = ContaDaInscricao.Frase(torneio, pessoas: 2, impedimentos: 0, valorGravado: 180m);

        Assert.NotNull(conta);
        Assert.DoesNotContain("125", conta!);
        Assert.Contains("2ª categoria", conta);
    }

    [Fact]
    public void Valor_que_nenhuma_conta_conhecida_explica_nao_ganha_frase_inventada()
    {
        // Inscrição mexida na mão, estorno parcial (AjustarValorDaInscricaoAsync), preço que
        // o organizador mudou no meio do caminho. Calar é a resposta honesta: o valor continua
        // na tela, só não vem acompanhado de uma conta que não fecha.
        var torneio = new Torneio { Nome = "T", Codigo = "T", PrecoInscricao = 125m };

        Assert.Null(ContaDaInscricao.Frase(torneio, pessoas: 2, impedimentos: 0, valorGravado: 200m));
    }

    [Fact]
    public void O_impedimento_entra_na_conta_porque_entra_no_valor()
    {
        // A taxa de impedimento é da INSCRIÇÃO, não do atleta — mas está dentro do total, e
        // uma conta que a ignora não fecha com o número ao lado.
        var torneio = new Torneio
        {
            Nome = "T", Codigo = "T", PrecoInscricao = 125m, TaxaPorImpedimento = 20m,
        };

        var conta = ContaDaInscricao.Frase(torneio, pessoas: 2, impedimentos: 1, valorGravado: 270m);

        Assert.NotNull(conta);
        Assert.Contains("impedimento", conta!);
    }

    // ── O QUE SÓ EXISTE NA VIEW ──────────────────────────────────────────────────────
    //
    // A suíte não renderiza Razor. Estes dois leem a FONTE, que é o único jeito de travar um
    // defeito que mora no HTML — e o de baixo é silencioso do pior jeito: a pessoa escolhe
    // cartão e o formulário manda Pix.

    [Fact]
    public void Cada_inscricao_da_faixa_tem_o_SEU_sufixo_na_escolha_da_forma()
    {
        // Com a faixa repetindo o parcial (uma por categoria), ids iguais no mesmo documento
        // fazem o `for=` do label da SEGUNDA casar com o rádio da PRIMEIRA. O sufixo por
        // inscrição é o que separa os dois grupos.
        Assert.Contains("ViewData[\"SufixoDaForma\"] =", Details());
        Assert.Contains("$\"insc{inscricao.DuplaId ?? inscricao.InscricaoAmericanaId}\"", Details());

        var parcial = File.ReadAllText(Path.Combine(
            PastaDoProjeto(), "Views", "Shared", "_EscolhaFormaPagamento.cshtml"));

        Assert.Contains("sufixoDaForma", parcial);
        // O id NÃO pode mais sair do torneio: numa página com duas inscrições ele se repete.
        Assert.DoesNotContain("id=\"forma_@(escolha)_@Model.Id\"", parcial);
    }

    [Fact]
    public void A_faixa_desenha_TODAS_as_inscricoes_e_nao_uma_so()
    {
        var fonte = Details();

        Assert.Contains("MinhasInscricoesNaoPagas", fonte);
        Assert.Contains("foreach (var inscricao in minhasNaoPagas)", fonte);
        // Os ViewBags singulares saíram de cena — quem voltasse a usá-los traria de volta a
        // faixa que mostra uma inscrição e esconde a outra.
        Assert.DoesNotContain("MinhaInscricaoNaoPagaDuplaId", fonte);
        Assert.DoesNotContain("MinhaInscricaoNaoPagaValor", fonte);
        // E a tela diz, com todas as letras, que são cobranças separadas.
        Assert.Contains("São cobranças separadas, uma por categoria.", fonte);
    }

    private static string Details() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

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
