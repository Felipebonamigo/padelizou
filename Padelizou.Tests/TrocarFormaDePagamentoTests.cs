using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Trocar a forma de recebimento DEPOIS de criar o torneio (08/08/2026).
//
// A escolha era definitiva: quem clicou errado só saía apagando o torneio e criando outro,
// perdendo o link já compartilhado. Agora troca — enquanto ninguém se inscreveu.
//
// O que estes testes seguram é a TRAVA, não a conveniência: com gente dentro, a forma é o que
// o jogador leu antes de entrar e o que fixou o split da cobrança dele.
public class TrocarFormaDePagamentoTests
{
    // A chamada mínima do Editar: só o que a assinatura exige, mais a forma sob teste.
    // Os campos que não interessam aqui vão no valor que não mexe em nada.
    private static Task<IActionResult> Editar(
        Padelizou.Controllers.TorneiosController controlador, int torneioId, string? forma) =>
        controlador.Editar(
            torneioId, "Torneio de Teste", null, null, precoInscricao: 100m, clubeId: 0,
            quantidadeQuadras: 2, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: null, capa: null,
            formaPagamento: forma);

    [Fact]
    public async Task Sem_inscrito_a_forma_de_pagamento_troca()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        ctx.SaveChanges();

        var controlador = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await Editar(controlador, torneio.Id, FormaDePagamentoDoTorneio.Externo);

        Assert.Equal(FormaDePagamentoDoTorneio.Externo,
            ctx.Torneios.Find(torneio.Id)!.FormaPagamento);
    }

    [Fact]
    public async Task Com_inscrito_a_forma_de_pagamento_nao_troca()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2);
        ctx.SaveChanges();

        var controlador = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await Editar(controlador, torneio.Id, FormaDePagamentoDoTorneio.Externo);

        // Continua como nasceu, e o organizador recebe o motivo — recusar calado deixaria
        // ele achando que salvou.
        Assert.Equal(FormaDePagamentoDoTorneio.SoPix,
            ctx.Torneios.Find(torneio.Id)!.FormaPagamento);
        Assert.Equal(FormaDePagamentoDoTorneio.PorqueTravou, controlador.TempData["Erro"]);
    }

    [Fact]
    public async Task Inscrito_do_americano_individual_tambem_trava()
    {
        // ⚠️ O Americano individual grava em InscricoesAmericanas, não em Duplas. Olhar só a
        // primeira tabela destravaria a troca com o torneio cheio de gente — o mesmo buraco
        // que zerou o Ranking Americano de duplas em 07/08.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        var jogador = TestInfra.NovoJogador(1);
        ctx.Jogadores.Add(jogador);
        ctx.SaveChanges();
        ctx.InscricoesAmericanas.Add(new InscricaoAmericana
        {
            CategoriaId = categoria.Id,
            JogadorId = jogador.Id,
        });
        ctx.SaveChanges();

        var controlador = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await Editar(controlador, torneio.Id, FormaDePagamentoDoTorneio.Externo);

        Assert.Equal(FormaDePagamentoDoTorneio.SoPix,
            ctx.Torneios.Find(torneio.Id)!.FormaPagamento);
    }

    [Fact]
    public async Task Time_cadastrado_pelo_organizador_nao_trava_a_troca()
    {
        // Time não paga inscrição pelo sistema — quem o cadastrou foi o próprio organizador.
        // Mesma exceção que TaxaDoTorneioExterno faz na base da taxa.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        ctx.SaveChanges();
        ctx.Duplas.Add(new Dupla { CategoriaId = categoria.Id, NomeTime = "Nata Padel" });
        ctx.SaveChanges();

        var controlador = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await Editar(controlador, torneio.Id, FormaDePagamentoDoTorneio.Externo);

        Assert.Equal(FormaDePagamentoDoTorneio.Externo,
            ctx.Torneios.Find(torneio.Id)!.FormaPagamento);
    }

    [Fact]
    public async Task Forma_desconhecida_e_recusada()
    {
        // POST montado à mão. Uma forma inventada não estoura nada — e é por isso que é
        // perigosa: o torneio viraria "por fora" calado e a taxa cairia no default de 15%.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        ctx.SaveChanges();

        var controlador = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await Editar(controlador, torneio.Id, "PagaComPix?");

        Assert.Equal(FormaDePagamentoDoTorneio.SoPix,
            ctx.Torneios.Find(torneio.Id)!.FormaPagamento);
    }

    [Fact]
    public async Task Trocar_para_pelo_site_sem_conta_conectada_e_recusado()
    {
        // A armadilha cara: o torneio passa a cobrar pelo site, as inscrições entram e
        // NENHUMA cobrança nasce. A tela trava os rádios, mas o servidor precisa recusar.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        torneio.FormaPagamento = FormaDePagamentoDoTorneio.Externo;
        ctx.SaveChanges();

        var pagamentos = Substitute.For<IPagamentoInscricaoService>();
        pagamentos.OQueFaltaParaReceber(Arg.Any<Jogador?>())
            .Returns("Você ainda não disse pra onde quer que o dinheiro das inscrições vá.");

        var controlador = TestInfra.NovoTorneiosController(ctx, organizador.Id, pagamentos);
        await Editar(controlador, torneio.Id, FormaDePagamentoDoTorneio.TodasAsFormas);

        Assert.Equal(FormaDePagamentoDoTorneio.Externo,
            ctx.Torneios.Find(torneio.Id)!.FormaPagamento);
    }

    [Fact]
    public async Task Campo_ausente_mantem_a_forma_gravada()
    {
        // Aba aberta antes deste deploy não manda o campo. Cair no default trocaria como o
        // torneio cobra sem ninguém ter pedido.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        torneio.FormaPagamento = FormaDePagamentoDoTorneio.TodasAsFormas;
        ctx.SaveChanges();

        var controlador = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await Editar(controlador, torneio.Id, forma: null);

        Assert.Equal(FormaDePagamentoDoTorneio.TodasAsFormas,
            ctx.Torneios.Find(torneio.Id)!.FormaPagamento);
    }

    [Fact]
    public void Os_nomes_gravados_no_banco_nao_mudam()
    {
        // Estes três textos estão em toda linha de Torneio.FormaPagamento em produção e na
        // migração TaxaPorFormaDeRecebimento. Renomear a constante é renomear o dado.
        Assert.Equal("Externo", FormaDePagamentoDoTorneio.Externo);
        Assert.Equal("OnlinePix", FormaDePagamentoDoTorneio.SoPix);
        Assert.Equal("OnlineTodas", FormaDePagamentoDoTorneio.TodasAsFormas);

        // E a régua do "o dinheiro passa pelo Padelizou" tem que casar com CobraPeloSite,
        // que agora delega pra cá.
        Assert.True(new Torneio { Nome = "T", Codigo = "T1", FormaPagamento = FormaDePagamentoDoTorneio.SoPix }.CobraPeloSite);
        Assert.True(new Torneio { Nome = "T", Codigo = "T1", FormaPagamento = FormaDePagamentoDoTorneio.TodasAsFormas }.CobraPeloSite);
        Assert.False(new Torneio { Nome = "T", Codigo = "T1", FormaPagamento = FormaDePagamentoDoTorneio.Externo }.CobraPeloSite);
    }
}
