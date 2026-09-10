using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — O CARD DO PIX RECOLHE PRA QUEM JÁ PAGOU.
//
// 🗣️ Emerson Pisoni, do 2ª Etapa ER PADEL TOUR: *"Se o cara já pagou, daria pra tirar info do
// pagamento, ocupa muito espaço"*. No celular dele o bloco do Pix — valor, chave, aviso e
// botão do WhatsApp — empurrava as abas (Inscritos, Jogos, Chaves e Grupos) pra fora da tela.
//
// ⚠️ RECOLHE, NÃO SOME. A tela continua trazendo a chave a um toque: a última palavra sobre
// quem pagou é do organizador virando o `Pago` na mão (muita inscrição é paga em dinheiro na
// quadra), e uma marcação errada dele não pode deixar o jogador sem caminho pra pagar.
//
// ⚠️ E "já paguei" é TODAS as minhas inscrições deste torneio pagas — com duas categorias, uma
// paga e outra não, o card fica aberto. Quem não tem inscrição nenhuma também vê aberto: é
// justamente quem ainda vai pagar.
public class PixRecolhidoParaQuemJaPagouTests
{
    [Fact]
    public async Task Quem_nao_tem_inscricao_nenhuma_ve_o_card_aberto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        var visitante = new Jogador { Nome = "Visitante", Cpf = "11100000011" };
        ctx.Jogadores.Add(visitante);
        await ctx.SaveChangesAsync();

        Assert.False(await PixDoOrganizador.JaPagouTudoAsync(ctx, await ComoATelaCarregaAsync(ctx, torneio.Id), visitante.Id));
    }

    [Fact]
    public async Task Inscricao_sem_pagamento_mantem_o_card_aberto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        var eu = Inscrever(ctx, categoria, pago: false);
        await ctx.SaveChangesAsync();

        Assert.False(await PixDoOrganizador.JaPagouTudoAsync(ctx, await ComoATelaCarregaAsync(ctx, torneio.Id), eu.Id));
    }

    [Fact]
    public async Task Inscricao_paga_recolhe_o_card()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        var eu = Inscrever(ctx, categoria, pago: true);
        await ctx.SaveChangesAsync();

        Assert.True(await PixDoOrganizador.JaPagouTudoAsync(ctx, await ComoATelaCarregaAsync(ctx, torneio.Id), eu.Id));
    }

    [Fact]
    public async Task Vale_pra_quem_entrou_como_PARCEIRO_da_dupla()
    {
        // O `Jogador2Id` pagou a mesma inscrição — o card tem que recolher pros dois.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        var parceiro = new Jogador { Nome = "Parceiro", Cpf = "22200000022" };
        ctx.Jogadores.Add(parceiro);
        var eu = Inscrever(ctx, categoria, pago: true, parceiro: parceiro);
        await ctx.SaveChangesAsync();

        Assert.True(await PixDoOrganizador.JaPagouTudoAsync(ctx, await ComoATelaCarregaAsync(ctx, torneio.Id), parceiro.Id));
        Assert.True(await PixDoOrganizador.JaPagouTudoAsync(ctx, await ComoATelaCarregaAsync(ctx, torneio.Id), eu.Id));
    }

    [Fact]
    public async Task Duas_categorias_com_uma_em_aberto_mantem_o_card_aberto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        var segunda = new Categoria { Nome = "3ª Categoria Masculina", Codigo = "CAT3M", Torneio = torneio };
        ctx.Categorias.Add(segunda);

        var eu = Inscrever(ctx, categoria, pago: true);
        ctx.Duplas.Add(new Dupla { Categoria = segunda, Jogador1 = eu, Pago = false });
        await ctx.SaveChangesAsync();

        Assert.False(await PixDoOrganizador.JaPagouTudoAsync(ctx, await ComoATelaCarregaAsync(ctx, torneio.Id), eu.Id));
    }

    [Fact]
    public async Task Americano_conta_igual()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        var eu = new Jogador { Nome = "Eu", Cpf = "33300000033" };
        ctx.Jogadores.Add(eu);
        var inscricao = new InscricaoAmericana { Categoria = categoria, Jogador = eu, Pago = false };
        ctx.InscricoesAmericanas.Add(inscricao);
        await ctx.SaveChangesAsync();

        Assert.False(await PixDoOrganizador.JaPagouTudoAsync(ctx, await ComoATelaCarregaAsync(ctx, torneio.Id), eu.Id));

        inscricao.Pago = true;
        await ctx.SaveChangesAsync();

        Assert.True(await PixDoOrganizador.JaPagouTudoAsync(ctx, await ComoATelaCarregaAsync(ctx, torneio.Id), eu.Id));
    }

    [Fact]
    public async Task O_TIME_do_organizador_nao_conta_como_inscricao_dele()
    {
        // Todo time é uma `Dupla` com o organizador no `Jogador1Id` (ver Dupla.EhTime). Sem
        // esta exclusão, um time não pago recolheria... ao contrário: manteria o card aberto
        // pro organizador pra sempre, por uma linha que não é inscrição de ninguém.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        ctx.Duplas.Add(new Dupla
        {
            Categoria = categoria,
            Jogador1 = organizador,
            NomeTime = "Time da Casa",
            Pago = false,
        });
        var euPaguei = Inscrever(ctx, categoria, pago: true, quem: organizador);
        await ctx.SaveChangesAsync();

        Assert.True(await PixDoOrganizador.JaPagouTudoAsync(ctx, await ComoATelaCarregaAsync(ctx, torneio.Id), euPaguei.Id));
    }

    [Fact]
    public void A_tela_recolhe_em_vez_de_apagar_o_card()
    {
        var fonte = Details();

        // A TAG de verdade, e não a palavra: a primeira versão deste teste achava o
        // "<details>" escrito no comentário logo acima do card e passava com o card apagado.
        Assert.Contains("<details class=\"alert alert-success border-0 pdz-pix\"", fonte);
        Assert.Contains("</details>", fonte);

        // E ele nasce ABERTO pra quem ainda deve — atributo condicional, nulo some no Razor.
        Assert.Contains("open=\"@(pixRecolhido ? null : \"open\")\"", fonte);
        Assert.Contains("ViewBag.JaPagueiNesteTorneio", fonte);
    }

    [Fact]
    public async Task No_Americano_individual_as_duplas_de_RODADA_nao_contam_como_inscricao()
    {
        // 🕳️ NO AMERICANO INDIVIDUAL, `Dupla` NAO E INSCRICAO. Cada rodada sorteada grava um par
        // por confronto (TorneiosController.Americano.GerarRodadasAmericano) e o desempate grava
        // mais dois (CriarDesempateAmericano) — todos com `Pago` false, porque ninguem paga um
        // par de rodada. A inscricao ali e a InscricaoAmericana.
        //
        // Contar essas linhas deixa o card ABERTO PRA SEMPRE pra quem ja pagou: e o pedido do
        // Emerson morrendo em silencio justamente no formato que mais gera essas linhas.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        torneio.Formato = FormatoDoTorneio.Americano;

        var eu = new Jogador { Nome = "Eu", Cpf = "55500000055" };
        var parceiroDaRodada = new Jogador { Nome = "Parceiro da rodada", Cpf = "66600000066" };
        ctx.Jogadores.AddRange(eu, parceiroDaRodada);
        ctx.InscricoesAmericanas.Add(new InscricaoAmericana { Categoria = categoria, Jogador = eu, Pago = true });

        // Duas rodadas sorteadas: dois pares meus, nenhum deles uma inscricao.
        ctx.Duplas.Add(new Dupla { Categoria = categoria, Jogador1 = eu, Jogador2 = parceiroDaRodada });
        ctx.Duplas.Add(new Dupla { Categoria = categoria, Jogador1 = parceiroDaRodada, Jogador2 = eu });
        await ctx.SaveChangesAsync();

        Assert.True(await PixDoOrganizador.JaPagouTudoAsync(ctx, await ComoATelaCarregaAsync(ctx, torneio.Id), eu.Id));
    }

    [Fact]
    public async Task No_Americano_DE_DUPLAS_a_dupla_continua_sendo_a_inscricao()
    {
        // A contraprova do teste de cima: no AmericanoDuplas o par e FIXO — ele E a inscricao, e
        // o sorteio nao cria par nenhum. Se a regra excluisse a familia inteira do Americano,
        // este formato pararia de enxergar a unica inscricao que existe.
        //
        // ⚠️ A INSCRICAO AQUI E PAGA, DE PROPOSITO. A primeira versao deste teste usava uma
        // dupla NAO paga e cobrava `False` — e passava igual com o defeito que dizia travar:
        // excluindo a familia inteira, a lista fica VAZIA, `Count > 0` da falso e o metodo
        // devolve `False` pelo motivo errado. Falso verde. Com a dupla paga, so ha um jeito de
        // dar `True`: enxergar a dupla.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        torneio.Formato = FormatoDoTorneio.AmericanoDeDuplas;
        var eu = Inscrever(ctx, categoria, pago: true);
        await ctx.SaveChangesAsync();

        Assert.True(await PixDoOrganizador.JaPagouTudoAsync(ctx, await ComoATelaCarregaAsync(ctx, torneio.Id), eu.Id));
    }

    [Fact]
    public void A_TELA_pergunta_ao_servico_quem_ja_pagou()
    {
        // 🕳️ Nada ligava o servico a tela: apagar as duas linhas do controller deixava a suite
        // inteira verde, `ViewBag.JaPagueiNesteTorneio` chegava nulo, e o card nunca recolhia —
        // o pedido do Emerson morrendo em silencio com 10 testes verdes em cima dele.
        var controller = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Controllers", "TorneiosController.cs"));

        var chamada = controller.IndexOf("PixDoOrganizador.JaPagouTudoAsync", StringComparison.Ordinal);
        Assert.True(chamada >= 0, "O controller precisa perguntar ao PixDoOrganizador quem ja pagou.");
        Assert.Contains("ViewBag.JaPagueiNesteTorneio", controller);

        // E a resposta so e calculada quando o card existe — o `if (PixDoOrganizador.Aparece(...))`
        // vem antes. Sem isso, duas consultas a mais em TODA abertura da pagina mais pesada do site.
        var portao = controller.LastIndexOf("PixDoOrganizador.Aparece(torneio)", chamada, StringComparison.Ordinal);
        Assert.True(portao >= 0 && chamada - portao < 900,
            "A pergunta precisa ficar DENTRO do `if (PixDoOrganizador.Aparece(torneio))`.");
    }

    // Do mesmo jeito que a acao Details carrega: Categorias -> Duplas ja vem na consulta que abre
    // a pagina, e e de la que a regra le (nenhuma consulta nova na pagina mais pesada do site).
    private static async Task<Torneio> ComoATelaCarregaAsync(DbPadelContext ctx, int torneioId) =>
        await ctx.Torneios
            .Include(t => t.Categorias)
                .ThenInclude(c => c.Duplas)
            .FirstAsync(t => t.Id == torneioId);

    private static Jogador Inscrever(Padelizou.Models.DbPadelContext ctx, Categoria categoria,
        bool pago, Jogador? parceiro = null, Jogador? quem = null)
    {
        var eu = quem ?? new Jogador { Nome = "Eu", Cpf = "44400000044" };
        if (quem == null) ctx.Jogadores.Add(eu);

        ctx.Duplas.Add(new Dupla { Categoria = categoria, Jogador1 = eu, Jogador2 = parceiro, Pago = pago });
        return eu;
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
