using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// "QUEM NÃO PAGAR PERDE A VAGA" — a caixinha que NÃO FAZIA NADA (Felipe, 10/08/2026).
//
// ⚠️ `Torneio.ExcluirSeNaoPagar` existia em dois lugares: o modelo e a tela de criação. Nenhuma
// linha a lia. No NATA PADEL TOUR ela estava LIGADA, e o organizador seguiu o torneio inteiro
// achando que o sistema ia limpar sozinho no fechamento.
//
// ⚠️ E o conserto NÃO foi passar a excluir sozinho: apagar inscrição de gente real por conta
// própria é o desastre que já aconteceu aqui uma vez. O sistema PERGUNTA.
public class DecisaoSobreQuemNaoPagouTests
{
    private static Torneio Torneio(bool caixinha = true, DateTime? fecha = null,
        DateTime? prazo = null, decimal preco = 125m, string status = "Inscrições Abertas") => new()
    {
        Nome = "NATA PADEL TOUR", Codigo = "NATA1",
        ExcluirSeNaoPagar = caixinha,
        PrecoInscricao = preco,
        PrevisaoEncerramentoInscricoes = fecha ?? new DateTime(2026, 10, 5),
        PrazoPagamento = prazo,
        Status = status,
    };

    // ── Quando perguntar ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_pergunta_e_do_dia_seguinte_ao_fechamento()
    {
        // No dia 5 ainda dá pra pagar: o dia é inteiro de quem deve.
        Assert.False(DecisaoSobreQuemNaoPagou.HoraDePerguntar(
            Torneio(), new DateTime(2026, 10, 5, 20, 0, 0), null));

        Assert.True(DecisaoSobreQuemNaoPagou.HoraDePerguntar(
            Torneio(), new DateTime(2026, 10, 6, 9, 0, 0), null));
    }

    [Fact]
    public void Prazo_de_pagamento_MAIS_LONGE_que_o_fechamento_adia_a_pergunta()
    {
        // ⚠️ O caso que faria alguém perder a vaga dentro do prazo prometido: inscrições fecham
        // dia 5, mas o organizador aceita pagamento até o dia 10. Perguntar no dia 6 ofereceria
        // remover quem ainda tem quatro dias — e essa pessoa pagaria no dia 8.
        var torneio = Torneio(fecha: new DateTime(2026, 10, 5), prazo: new DateTime(2026, 10, 10));

        Assert.False(DecisaoSobreQuemNaoPagou.HoraDePerguntar(torneio, new DateTime(2026, 10, 6, 9, 0, 0), null));
        Assert.True(DecisaoSobreQuemNaoPagou.HoraDePerguntar(torneio, new DateTime(2026, 10, 11, 9, 0, 0), null));
    }

    [Fact]
    public void Prazo_de_pagamento_MAIS_CURTO_nao_antecipa_a_pergunta()
    {
        // O contrário: o fechamento é que manda, porque até lá ainda entra gente.
        var torneio = Torneio(fecha: new DateTime(2026, 10, 5), prazo: new DateTime(2026, 10, 1));

        Assert.False(DecisaoSobreQuemNaoPagou.HoraDePerguntar(torneio, new DateTime(2026, 10, 3, 9, 0, 0), null));
        Assert.True(DecisaoSobreQuemNaoPagou.HoraDePerguntar(torneio, new DateTime(2026, 10, 6, 9, 0, 0), null));
    }

    [Fact]
    public void Sem_a_caixinha_o_organizador_nao_e_incomodado()
    {
        // Ele já disse que acerta por fora. Perguntar mesmo assim contradiz a própria escolha.
        Assert.False(DecisaoSobreQuemNaoPagou.HoraDePerguntar(
            Torneio(caixinha: false), new DateTime(2026, 10, 6, 9, 0, 0), null));
    }

    [Fact]
    public void A_pergunta_e_feita_UMA_vez_so()
    {
        // ⚠️ O varredor passa de hora em hora: sem a marca, seriam doze avisos no primeiro dia.
        Assert.False(DecisaoSobreQuemNaoPagou.HoraDePerguntar(
            Torneio(), new DateTime(2026, 10, 6, 9, 0, 0), jaPerguntouEm: new DateTime(2026, 10, 6, 9, 0, 0)));
    }

    [Fact]
    public void Torneio_de_graca_ou_cancelado_fica_de_fora()
    {
        Assert.False(DecisaoSobreQuemNaoPagou.HoraDePerguntar(
            Torneio(preco: 0m), new DateTime(2026, 10, 6, 9, 0, 0), null));

        var cancelado = Torneio();
        cancelado.CanceladoEm = new DateTime(2026, 9, 1);
        Assert.False(DecisaoSobreQuemNaoPagou.HoraDePerguntar(cancelado, new DateTime(2026, 10, 6, 9, 0, 0), null));
    }

    [Fact]
    public void Sem_data_de_fechamento_nao_ha_quando_perguntar()
    {
        var semData = Torneio();
        semData.PrevisaoEncerramentoInscricoes = null;

        Assert.Null(DecisaoSobreQuemNaoPagou.Quando(semData));
        Assert.False(DecisaoSobreQuemNaoPagou.HoraDePerguntar(semData, new DateTime(2026, 10, 6, 9, 0, 0), null));
    }

    // ── Se ainda dá pra remover ──────────────────────────────────────────────────────────────

    [Fact]
    public void Depois_de_encerradas_as_inscricoes_nao_se_remove_mais()
    {
        // O chaveamento pode já ter usado essas duplas, e tirá-las deixaria jogo órfão. A tela
        // precisa DIZER isso, em vez de deixar o botão falhar calado.
        Assert.True(DecisaoSobreQuemNaoPagou.PodeRemoverAgora(Torneio()));
        Assert.False(DecisaoSobreQuemNaoPagou.PodeRemoverAgora(Torneio(status: "Chaves em Sorteio")));
    }

    // ── A varredura ──────────────────────────────────────────────────────────────────────────

    private static (Torneio torneio, Categoria categoria) TorneioComDevedor(DbPadelContext ctx, int qtdDuplas)
    {
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas, status: "Inscrições Abertas");
        torneio.PrecoInscricao = 125m;
        torneio.ExcluirSeNaoPagar = true;
        torneio.PrevisaoEncerramentoInscricoes = new DateTime(2026, 10, 5);
        ctx.SaveChanges();
        return (torneio, categoria);
    }

    [Fact]
    public async Task O_organizador_e_avisado_e_a_marca_fica_gravada()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _) = TorneioComDevedor(ctx, qtdDuplas: 2);
        var push = Substitute.For<IPushNotificationService>();

        var perguntados = await PerguntaSobreNaoPagosBackgroundService.VarrerAsync(
            ctx, push, new DateTime(2026, 10, 6, 9, 0, 0));

        Assert.Equal(1, perguntados);
        Assert.NotNull(ctx.Torneios.Find(torneio.Id)!.PerguntaDeNaoPagosEm);
        await push.ReceivedWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!);
    }

    [Fact]
    public async Task A_segunda_varredura_nao_pergunta_de_novo()
    {
        using var ctx = TestInfra.NovoContexto();
        TorneioComDevedor(ctx, qtdDuplas: 2);
        var push = Substitute.For<IPushNotificationService>();

        await PerguntaSobreNaoPagosBackgroundService.VarrerAsync(ctx, push, new DateTime(2026, 10, 6, 9, 0, 0));
        push.ClearReceivedCalls();

        var segunda = await PerguntaSobreNaoPagosBackgroundService.VarrerAsync(
            ctx, push, new DateTime(2026, 10, 6, 10, 0, 0));

        Assert.Equal(0, segunda);
        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!);
    }

    [Fact]
    public async Task Torneio_com_todo_mundo_pago_nao_gera_pergunta_sem_assunto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria) = TorneioComDevedor(ctx, qtdDuplas: 2);
        foreach (var d in ctx.Duplas.Where(d => d.CategoriaId == categoria.Id)) d.Pago = true;
        ctx.SaveChanges();
        var push = Substitute.For<IPushNotificationService>();

        var perguntados = await PerguntaSobreNaoPagosBackgroundService.VarrerAsync(
            ctx, push, new DateTime(2026, 10, 6, 9, 0, 0));

        Assert.Equal(0, perguntados);
        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!);

        // ⚠️ Mas a marca FICA: senão a varredura voltaria a este torneio de hora em hora, pra
        // sempre, só pra redescobrir que não há o que perguntar.
        Assert.NotNull(ctx.Torneios.Find(torneio.Id)!.PerguntaDeNaoPagosEm);
    }

    [Fact]
    public async Task De_madrugada_ninguem_decide_sobre_a_vaga_dos_outros()
    {
        using var ctx = TestInfra.NovoContexto();
        TorneioComDevedor(ctx, qtdDuplas: 2);
        var push = Substitute.For<IPushNotificationService>();

        Assert.Equal(0, await PerguntaSobreNaoPagosBackgroundService.VarrerAsync(
            ctx, push, new DateTime(2026, 10, 6, 4, 0, 0)));
    }

    [Fact]
    public async Task Antes_do_fechamento_a_varredura_nao_pergunta_nada()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _) = TorneioComDevedor(ctx, qtdDuplas: 2);
        var push = Substitute.For<IPushNotificationService>();

        Assert.Equal(0, await PerguntaSobreNaoPagosBackgroundService.VarrerAsync(
            ctx, push, new DateTime(2026, 10, 1, 9, 0, 0)));

        // E sem marca: a pergunta ainda está por vir.
        Assert.Null(ctx.Torneios.Find(torneio.Id)!.PerguntaDeNaoPagosEm);
    }
}
