using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using NSubstitute;

namespace Padelizou.Tests;

// "PAGUEI SÓ O MEU" — e o sistema marcou a DUPLA como paga.
//
// 🗣️ Lucas Almeida, organizador do NATA PADEL TOUR, pelo WhatsApp (06/10/2026): *"eu me
// inscrevi sozinho... aí eu paguei... fiquei como pago. Quando eu puxei o Greg como minha
// dupla, já ficou marcado como pago a dupla"* — *"tenho absoluta certeza de que paguei só o
// meu"*. Ele estava certo: a comissão daquela inscrição foi de R$ 12,50, que é 10% de R$ 125,
// a conta de UMA pessoa.
//
// 🕳️ Desde 08/08/2026 a inscrição sozinha custa uma pessoa, e quando o parceiro entra o
// `ValorInscricao` sobe pra duas (PrecoDaInscricao.AoEntrarOParceiro). O que ninguém tinha
// feito era olhar pro `Pago`: ele continuava `true` com metade do dinheiro dentro. O próprio
// comentário da régua dizia *"a diferença é acertada com o organizador"* — só que o
// organizador não era avisado em lugar nenhum, e a tela afirmava que estava pago. O parceiro
// saía da lista de cobrança para sempre.
public class PagarSoAMinhaParteTests
{
    private static (DbPadelContext ctx, Torneio torneio, Categoria categoria, Jogador eu) Cenario(
        decimal preco = 125m, decimal? precoSegunda = null)
    {
        var ctx = TestInfra.NovoContexto();

        var eu = new Jogador { Nome = "Lucas Almeida", Cpf = "11144477735" };
        ctx.Jogadores.Add(eu);

        var torneio = new Torneio
        {
            Nome = "NATA PADEL TOUR",
            Codigo = "NATA1",
            Status = "Inscrições Abertas",
            DataInicio = DateTime.Today.AddDays(20),
            PrecoInscricao = preco,
            PrecoSegundaInscricao = precoSegunda,
            PermiteMultiplasCategorias = true,
            FormaPagamento = FormaDePagamentoDoTorneio.TodasAsFormas,
            // O arranjo do caso: a vaga é garantida e o pagamento vem depois.
            PagamentoObrigatorioNaInscricao = false,
        };
        ctx.Torneios.Add(torneio);

        var categoria = new Categoria { Nome = "4ª Masculina", Codigo = "C4M", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        return (ctx, torneio, categoria, eu);
    }

    private static Pagamento PagamentoConfirmado(DbPadelContext ctx, Torneio torneio, Jogador quem,
        int duplaId, decimal valor)
    {
        var pagamento = new Pagamento
        {
            Tipo = "TorneioPagarDepois",
            ReferenciaId = duplaId,
            TorneioId = torneio.Id,
            JogadorId = quem.Id,
            Valor = valor,
            Status = "Confirmado",
        };
        ctx.Pagamentos.Add(pagamento);
        ctx.SaveChanges();
        return pagamento;
    }

    // ── A régua: quanto entrou, quanto falta ────────────────────────────────────────

    [Fact]
    public void Metade_paga_e_inscricao_PARCIAL_e_nao_quitada()
    {
        Assert.False(QuitacaoDaInscricao.Quitada(250m, jaPago: 125m));
        Assert.True(QuitacaoDaInscricao.Parcial(250m, jaPago: 125m));
        Assert.Equal(125m, QuitacaoDaInscricao.Falta(250m, jaPago: 125m));
    }

    [Fact]
    public void Sem_pagamento_nenhum_NAO_e_parcial()
    {
        // A diferença importa: "parcial" é dinheiro que entrou e não fechou. Inscrição sem
        // pagamento nenhum é simplesmente não paga — ou foi marcada na mão pelo organizador,
        // e aí a marcação dele é soberana.
        Assert.False(QuitacaoDaInscricao.Parcial(250m, jaPago: 0m));
        Assert.Equal(250m, QuitacaoDaInscricao.Falta(250m, jaPago: 0m));
    }

    [Fact]
    public void Pagou_tudo_quita_e_nao_falta_nada()
    {
        Assert.True(QuitacaoDaInscricao.Quitada(250m, jaPago: 250m));
        Assert.False(QuitacaoDaInscricao.Parcial(250m, jaPago: 250m));
        Assert.Equal(0m, QuitacaoDaInscricao.Falta(250m, jaPago: 250m));
    }

    [Fact]
    public void Pagou_a_mais_nao_vira_falta_negativa()
    {
        // Acontece com estorno parcial e com ajuste do organizador: o número na tela não pode
        // virar "falta -R$ 30,00".
        Assert.Equal(0m, QuitacaoDaInscricao.Falta(250m, jaPago: 280m));
        Assert.True(QuitacaoDaInscricao.Quitada(250m, jaPago: 280m));
    }

    // ── O que a pessoa escolhe pagar ────────────────────────────────────────────────

    [Fact]
    public void Minha_parte_e_o_preco_de_UMA_pessoa_e_a_dupla_e_o_que_falta()
    {
        var (_, torneio, _, _) = Cenario();

        Assert.Equal(125m, QuitacaoDaInscricao.ValorDaEscolha(
            torneio, QuitacaoDaInscricao.MinhaParte, euRepitoNoTorneio: false, falta: 250m));

        Assert.Equal(250m, QuitacaoDaInscricao.ValorDaEscolha(
            torneio, QuitacaoDaInscricao.ADupla, euRepitoNoTorneio: false, falta: 250m));
    }

    [Fact]
    public void Quem_esta_na_SEGUNDA_categoria_paga_a_parte_dele_com_desconto()
    {
        // O desconto é por PESSOA (Services/PrecoDaInscricao): a minha parte é a MINHA, não
        // metade do total.
        var (_, torneio, _, _) = Cenario(precoSegunda: 90m);

        Assert.Equal(90m, QuitacaoDaInscricao.ValorDaEscolha(
            torneio, QuitacaoDaInscricao.MinhaParte, euRepitoNoTorneio: true, falta: 215m));
    }

    [Fact]
    public void Minha_parte_nunca_passa_do_que_ainda_falta()
    {
        // Metade já entrou: pagar "a minha parte" não pode cobrar mais do que o que resta.
        var (_, torneio, _, _) = Cenario();

        Assert.Equal(40m, QuitacaoDaInscricao.ValorDaEscolha(
            torneio, QuitacaoDaInscricao.MinhaParte, euRepitoNoTorneio: false, falta: 40m));
    }

    [Fact]
    public void Escolha_ausente_ou_desconhecida_cobra_a_INSCRICAO_INTEIRA()
    {
        // Formulário antigo em cache, requisição montada à mão. Errar pra cá deixa a pessoa
        // pagando o que ela já ia pagar antes desta mudança; errar pro outro lado cobraria
        // metade de quem queria quitar, e a dupla ficaria devendo sem ninguém perceber.
        var (_, torneio, _, _) = Cenario();

        Assert.Equal(250m, QuitacaoDaInscricao.ValorDaEscolha(torneio, null, false, falta: 250m));
        Assert.Equal(250m, QuitacaoDaInscricao.ValorDaEscolha(torneio, "xpto", false, falta: 250m));
    }

    // ── O defeito do relato: o parceiro entra e a dupla continua "paga" ─────────────

    [Fact]
    public async Task Parceiro_entra_na_dupla_paga_por_UMA_pessoa_e_ela_deixa_de_estar_quitada()
    {
        var (ctx, torneio, categoria, eu) = Cenario();
        var dupla = new Dupla
        {
            Categoria = categoria,
            Jogador1Id = eu.Id,
            ValorInscricao = PrecoDaInscricao.Total(torneio, new[] { false }),   // 125, sozinho
            Pago = true,
            PagoEm = DateTime.Now,
        };
        ctx.Duplas.Add(dupla);
        await ctx.SaveChangesAsync();
        PagamentoConfirmado(ctx, torneio, eu, dupla.Id, 125m);

        var greg = new Jogador { Nome = "Greg Souza", Cpf = "22255588846" };
        ctx.Jogadores.Add(greg);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoDuplasController(ctx, eu.Id);
        await controller.TrocarParceiro(dupla.Id, greg.Cpf, null);
        Assert.Null(controller.TempData["Erro"]);     // a ação precisa ter ACONTECIDO

        var depois = await ctx.Duplas.FindAsync(dupla.Id);
        Assert.Equal(250m, depois!.ValorInscricao);   // a régua de 08/08 já fazia isto
        Assert.False(depois.Pago);                    // o que faltava: ela não está quitada
        Assert.Null(depois.PagoEm);
    }

    [Fact]
    public async Task Inscricao_marcada_na_MAO_pelo_organizador_continua_paga()
    {
        // Sem pagamento nenhum no gateway, "pago" é a palavra do organizador — ele acertou por
        // fora e sabe o que fez. Desmarcar sozinho seria o sistema contradizendo quem tem a
        // informação que ele não tem.
        var (ctx, torneio, categoria, eu) = Cenario();
        var dupla = new Dupla
        {
            Categoria = categoria,
            Jogador1Id = eu.Id,
            ValorInscricao = PrecoDaInscricao.Total(torneio, new[] { false }),
            Pago = true,
            PagoEm = DateTime.Now,
        };
        ctx.Duplas.Add(dupla);
        var greg = new Jogador { Nome = "Greg Souza", Cpf = "22255588846" };
        ctx.Jogadores.Add(greg);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoDuplasController(ctx, eu.Id);
        await controller.TrocarParceiro(dupla.Id, greg.Cpf, null);

        var depois = await ctx.Duplas.FindAsync(dupla.Id);
        Assert.True(depois!.Pago);
    }

    // ── O webhook: meia inscrição paga NÃO quita a dupla ────────────────────────────

    [Fact]
    public async Task Pagamento_de_METADE_nao_marca_a_dupla_como_paga()
    {
        // É a trava que sustenta o "pagar só o meu": sem ela, a primeira metade quitaria a
        // dupla inteira e o defeito do relato nasceria de novo, agora pela porta da frente.
        var (ctx, torneio, categoria, eu) = Cenario();
        var greg = new Jogador { Nome = "Greg Souza", Cpf = "22255588846" };
        ctx.Jogadores.Add(greg);
        var dupla = new Dupla
        {
            Categoria = categoria,
            Jogador1Id = eu.Id,
            Jogador2Id = greg.Id,
            ValorInscricao = PrecoDaInscricao.Total(torneio, new[] { false, false }),   // 250
        };
        ctx.Duplas.Add(dupla);
        await ctx.SaveChangesAsync();

        await TestInfra.ConfirmarPagamentoDeInscricaoAsync(ctx, torneio, eu, dupla.Id, 125m);

        var depois = await ctx.Duplas.FindAsync(dupla.Id);
        Assert.False(depois!.Pago);

        // A segunda metade fecha a conta.
        await TestInfra.ConfirmarPagamentoDeInscricaoAsync(ctx, torneio, greg, dupla.Id, 125m);

        depois = await ctx.Duplas.FindAsync(dupla.Id);
        Assert.True(depois!.Pago);
        Assert.NotNull(depois.PagoEm);
    }

    // ── A FAIXA: o que a pessoa vê e o que ela pode escolher ────────────────────────

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

    [Fact]
    public async Task A_inscricao_marcada_como_paga_com_METADE_dentro_volta_pra_faixa()
    {
        // O dado que já existe em produção: a dupla do Lucas. `Pago` continua true, mas o
        // dinheiro não cobre — e é o dinheiro que decide quem aparece.
        var (ctx, torneio, categoria, eu) = Cenario();
        var greg = new Jogador { Nome = "Greg Souza", Cpf = "22255588846" };
        ctx.Jogadores.Add(greg);
        var dupla = new Dupla
        {
            Categoria = categoria, Jogador1Id = eu.Id, Jogador2Id = greg.Id,
            ValorInscricao = 250m, Pago = true, PagoEm = DateTime.Now,
        };
        ctx.Duplas.Add(dupla);
        await ctx.SaveChangesAsync();
        PagamentoConfirmado(ctx, torneio, eu, dupla.Id, 125m);

        var linha = Assert.Single(await FaixaAsync(ctx, torneio.Id, eu.Id));

        Assert.Equal(250m, linha.Valor);      // o total continua sendo o do anúncio
        Assert.Equal(125m, linha.JaPago);
        Assert.Equal(125m, linha.Falta);
    }

    [Fact]
    public async Task O_que_o_organizador_marcou_na_MAO_nao_volta_pra_faixa()
    {
        // Sem dinheiro no gateway, "pago" é a palavra dele — acertaram por fora. Cobrar de
        // novo quem já pagou é o pior erro possível desta tela.
        var (ctx, torneio, categoria, eu) = Cenario();
        ctx.Duplas.Add(new Dupla
        {
            Categoria = categoria, Jogador1Id = eu.Id,
            ValorInscricao = 125m, Pago = true, PagoEm = DateTime.Now,
        });
        await ctx.SaveChangesAsync();

        Assert.Empty(await FaixaAsync(ctx, torneio.Id, eu.Id));
    }

    [Fact]
    public async Task Dupla_sem_nada_pago_oferece_as_DUAS_opcoes()
    {
        var (ctx, torneio, categoria, eu) = Cenario();
        var greg = new Jogador { Nome = "Greg Souza", Cpf = "22255588846" };
        ctx.Jogadores.Add(greg);
        ctx.Duplas.Add(new Dupla
        {
            Categoria = categoria, Jogador1Id = eu.Id, Jogador2Id = greg.Id,
            ValorInscricao = 250m,
        });
        await ctx.SaveChangesAsync();

        var linha = Assert.Single(await FaixaAsync(ctx, torneio.Id, eu.Id));

        Assert.True(linha.PodeEscolherMinhaParte);
        Assert.Equal(125m, linha.MinhaParte);
        Assert.Equal(250m, linha.Falta);
    }

    [Fact]
    public async Task Faltando_so_UMA_parte_nao_ha_escolha_a_fazer()
    {
        // Dois botões com o mesmo valor seriam uma escolha falsa.
        var (ctx, torneio, categoria, eu) = Cenario();
        var greg = new Jogador { Nome = "Greg Souza", Cpf = "22255588846" };
        ctx.Jogadores.Add(greg);
        var dupla = new Dupla
        {
            Categoria = categoria, Jogador1Id = eu.Id, Jogador2Id = greg.Id, ValorInscricao = 250m,
        };
        ctx.Duplas.Add(dupla);
        await ctx.SaveChangesAsync();
        PagamentoConfirmado(ctx, torneio, eu, dupla.Id, 125m);

        var linha = Assert.Single(await FaixaAsync(ctx, torneio.Id, eu.Id));

        Assert.False(linha.PodeEscolherMinhaParte);
        Assert.Equal(125m, linha.Falta);
    }

    // ── O SERVIDOR é quem calcula o valor da escolha ────────────────────────────────

    [Fact]
    public async Task Escolher_minha_parte_cobra_SO_a_parte_de_uma_pessoa()
    {
        var (ctx, torneio, categoria, eu) = Cenario();
        var greg = new Jogador { Nome = "Greg Souza", Cpf = "22255588846" };
        ctx.Jogadores.Add(greg);
        var dupla = new Dupla
        {
            Categoria = categoria, Jogador1Id = eu.Id, Jogador2Id = greg.Id, ValorInscricao = 250m,
        };
        ctx.Duplas.Add(dupla);
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = eu.Id });
        await ctx.SaveChangesAsync();

        var pagamentos = Substitute.For<IPagamentoInscricaoService>();
        pagamentos.PodeCobrar(Arg.Any<Torneio>(), Arg.Any<Jogador?>()).Returns(true);
        pagamentos.IniciarCobrancaDeInscricaoAsync(
            Arg.Any<Torneio>(), Arg.Any<Jogador>(), Arg.Any<Jogador>(), Arg.Any<bool>(),
            Arg.Any<int>(), Arg.Any<DadosPagamentoDeInscricao>(), Arg.Any<string?>(), Arg.Any<decimal?>())
            .Returns("https://checkout/x");

        var controller = TestInfra.NovoTorneiosController(ctx, eu.Id, pagamentos);
        await controller.PagarInscricao(torneio.Id, dupla.Id, null, "Pix",
            QuitacaoDaInscricao.MinhaParte);

        await pagamentos.Received(1).IniciarCobrancaDeInscricaoAsync(
            Arg.Any<Torneio>(), Arg.Any<Jogador>(), Arg.Any<Jogador>(), Arg.Any<bool>(),
            Arg.Any<int>(), Arg.Any<DadosPagamentoDeInscricao>(), Arg.Any<string?>(),
            valorAPagar: 125m);
    }

    [Fact]
    public async Task Escolher_a_dupla_cobra_tudo_o_que_falta()
    {
        var (ctx, torneio, categoria, eu) = Cenario();
        var greg = new Jogador { Nome = "Greg Souza", Cpf = "22255588846" };
        ctx.Jogadores.Add(greg);
        var dupla = new Dupla
        {
            Categoria = categoria, Jogador1Id = eu.Id, Jogador2Id = greg.Id, ValorInscricao = 250m,
        };
        ctx.Duplas.Add(dupla);
        await ctx.SaveChangesAsync();
        PagamentoConfirmado(ctx, torneio, eu, dupla.Id, 125m);

        var pagamentos = Substitute.For<IPagamentoInscricaoService>();
        pagamentos.PodeCobrar(Arg.Any<Torneio>(), Arg.Any<Jogador?>()).Returns(true);
        pagamentos.IniciarCobrancaDeInscricaoAsync(
            Arg.Any<Torneio>(), Arg.Any<Jogador>(), Arg.Any<Jogador>(), Arg.Any<bool>(),
            Arg.Any<int>(), Arg.Any<DadosPagamentoDeInscricao>(), Arg.Any<string?>(), Arg.Any<decimal?>())
            .Returns("https://checkout/x");

        var controller = TestInfra.NovoTorneiosController(ctx, greg.Id, pagamentos);
        await controller.PagarInscricao(torneio.Id, dupla.Id, null, "Pix", QuitacaoDaInscricao.ADupla);

        // O que falta, não o total: a metade do Lucas já entrou.
        await pagamentos.Received(1).IniciarCobrancaDeInscricaoAsync(
            Arg.Any<Torneio>(), Arg.Any<Jogador>(), Arg.Any<Jogador>(), Arg.Any<bool>(),
            Arg.Any<int>(), Arg.Any<DadosPagamentoDeInscricao>(), Arg.Any<string?>(),
            valorAPagar: 125m);
    }

    [Fact]
    public async Task Inscricao_quitada_recusa_o_pagamento()
    {
        var (ctx, torneio, categoria, eu) = Cenario();
        var dupla = new Dupla { Categoria = categoria, Jogador1Id = eu.Id, ValorInscricao = 125m };
        ctx.Duplas.Add(dupla);
        await ctx.SaveChangesAsync();
        PagamentoConfirmado(ctx, torneio, eu, dupla.Id, 125m);

        var pagamentos = Substitute.For<IPagamentoInscricaoService>();
        pagamentos.PodeCobrar(Arg.Any<Torneio>(), Arg.Any<Jogador?>()).Returns(true);

        var controller = TestInfra.NovoTorneiosController(ctx, eu.Id, pagamentos);
        await controller.PagarInscricao(torneio.Id, dupla.Id, null, "Pix", QuitacaoDaInscricao.ADupla);

        Assert.Equal("Esta inscrição já está paga.", controller.TempData["Erro"]);
        await pagamentos.DidNotReceive().IniciarCobrancaDeInscricaoAsync(
            Arg.Any<Torneio>(), Arg.Any<Jogador>(), Arg.Any<Jogador>(), Arg.Any<bool>(),
            Arg.Any<int>(), Arg.Any<DadosPagamentoDeInscricao>(), Arg.Any<string?>(), Arg.Any<decimal?>());
    }
}
