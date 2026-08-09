using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A VARREDURA do lembrete de inscrição não paga — a ligação, não a régua.
//
// A régua está em LembreteDeInscricaoNaoPagaTests. Aqui se exercita o percurso inteiro: achar os
// torneios certos, achar as inscrições devendo, avisar as pessoas certas e gravar o marco pra não
// repetir. É onde moram os erros que régua nenhuma pega — filtro esquecido, aviso pro organizador
// de uma categoria de times, marco não gravado.
public class VarreduraDeInscricaoNaoPagaTests
{
    // 7 dias antes do fechamento, dez da manhã: o primeiro marco, em hora civilizada.
    private static readonly DateTime Fechamento = new(2026, 9, 21);
    private static readonly DateTime SeteDiasAntes = new(2026, 9, 14, 10, 0, 0);

    private static (Torneio torneio, Categoria categoria) TorneioQueCobra(DbPadelContext ctx, int qtdDuplas)
    {
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas, status: "Inscrições Abertas");
        torneio.PrecoInscricao = 125m;
        torneio.FormaPagamento = FormaDePagamentoDoTorneio.TodasAsFormas;
        torneio.PagamentoObrigatorioNaInscricao = false;
        torneio.PrevisaoEncerramentoInscricoes = Fechamento;
        ctx.SaveChanges();
        return (torneio, categoria);
    }

    [Fact]
    public async Task Avisa_OS_DOIS_da_dupla_e_grava_o_marco()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, categoria) = TorneioQueCobra(ctx, qtdDuplas: 1);
        var push = Substitute.For<IPushNotificationService>();

        var lembradas = await LembreteInscricaoNaoPagaBackgroundService.VarrerAsync(ctx, push, SeteDiasAntes);

        var dupla = ctx.Duplas.Single(d => d.CategoriaId == categoria.Id);
        Assert.Equal(1, lembradas);
        Assert.Equal(7, dupla.UltimoLembreteDePagamento);

        // Uma inscrição, DUAS pessoas avisadas.
        await push.Received(2).EnviarParaJogadorAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task A_segunda_varredura_do_dia_nao_avisa_de_novo()
    {
        // ⚠️ O varredor roda de hora em hora, e agora também na SUBIDA do processo: sem o marco
        // gravado, um deploy no meio da tarde reenviaria tudo.
        using var ctx = TestInfra.NovoContexto();
        TorneioQueCobra(ctx, qtdDuplas: 1);
        var push = Substitute.For<IPushNotificationService>();

        await LembreteInscricaoNaoPagaBackgroundService.VarrerAsync(ctx, push, SeteDiasAntes);
        push.ClearReceivedCalls();

        var segunda = await LembreteInscricaoNaoPagaBackgroundService.VarrerAsync(ctx, push, SeteDiasAntes.AddHours(1));

        Assert.Equal(0, segunda);
        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!);
    }

    [Fact]
    public async Task Quem_ja_pagou_nao_e_cobrado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, categoria) = TorneioQueCobra(ctx, qtdDuplas: 1);
        var dupla = ctx.Duplas.Single(d => d.CategoriaId == categoria.Id);
        dupla.Pago = true;
        ctx.SaveChanges();
        var push = Substitute.For<IPushNotificationService>();

        var lembradas = await LembreteInscricaoNaoPagaBackgroundService.VarrerAsync(ctx, push, SeteDiasAntes);

        Assert.Equal(0, lembradas);
        await push.DidNotReceiveWithAnyArgs().EnviarParaJogadorAsync(default, default!, default!);
    }

    [Fact]
    public async Task Quem_esta_na_lista_de_espera_nao_deve_nada_ainda()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, categoria) = TorneioQueCobra(ctx, qtdDuplas: 1);
        var dupla = ctx.Duplas.Single(d => d.CategoriaId == categoria.Id);
        dupla.EmListaDeEspera = true;
        ctx.SaveChanges();
        var push = Substitute.For<IPushNotificationService>();

        Assert.Equal(0, await LembreteInscricaoNaoPagaBackgroundService.VarrerAsync(ctx, push, SeteDiasAntes));
    }

    [Fact]
    public async Task Categoria_de_TIMES_nao_cobra_o_organizador()
    {
        // ⚠️ Numa linha de time o Jogador1Id aponta pro ORGANIZADOR que cadastrou. Sem o filtro,
        // ele levaria um "você não pagou" por cada time do torneio — dívida que não é dele.
        using var ctx = TestInfra.NovoContexto();
        var (_, categoria) = TorneioQueCobra(ctx, qtdDuplas: 0);
        var organizador = ctx.Jogadores.First(j => j.Nome == "Organizador");
        ctx.Duplas.Add(new Dupla
        {
            CategoriaId = categoria.Id,
            Jogador1Id = organizador.Id,
            NomeTime = "Chakra Padel",
            UltimaFase = "",
        });
        ctx.SaveChanges();
        var push = Substitute.For<IPushNotificationService>();

        Assert.Equal(0, await LembreteInscricaoNaoPagaBackgroundService.VarrerAsync(ctx, push, SeteDiasAntes));
    }

    [Fact]
    public async Task Dupla_sem_parceiro_avisa_uma_pessoa_so()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, categoria) = TorneioQueCobra(ctx, qtdDuplas: 1);
        var dupla = ctx.Duplas.Single(d => d.CategoriaId == categoria.Id);
        dupla.Jogador2Id = null;
        ctx.SaveChanges();
        var push = Substitute.For<IPushNotificationService>();

        await LembreteInscricaoNaoPagaBackgroundService.VarrerAsync(ctx, push, SeteDiasAntes);

        await push.Received(1).EnviarParaJogadorAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task De_madrugada_nao_sai_nada()
    {
        using var ctx = TestInfra.NovoContexto();
        TorneioQueCobra(ctx, qtdDuplas: 1);
        var push = Substitute.For<IPushNotificationService>();

        Assert.Equal(0, await LembreteInscricaoNaoPagaBackgroundService.VarrerAsync(
            ctx, push, SeteDiasAntes.Date.AddHours(4)));
    }

    [Fact]
    public async Task Torneio_que_exige_pagamento_na_inscricao_fica_de_fora()
    {
        // Lá a inscrição nasce paga: uma linha não paga ali é resquício, não dívida.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _) = TorneioQueCobra(ctx, qtdDuplas: 1);
        torneio.PagamentoObrigatorioNaInscricao = true;
        ctx.SaveChanges();
        var push = Substitute.For<IPushNotificationService>();

        Assert.Equal(0, await LembreteInscricaoNaoPagaBackgroundService.VarrerAsync(ctx, push, SeteDiasAntes));
    }

    [Fact]
    public async Task O_aviso_leva_pra_tela_do_torneio_e_nao_pra_uma_fatura()
    {
        // ⚠️ Quem paga depois pode não ter cobrança nenhuma aberta — é o caso NORMAL aqui. Um
        // link de fatura seria nulo justamente pra quem mais precisa do aviso.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _) = TorneioQueCobra(ctx, qtdDuplas: 1);
        var push = Substitute.For<IPushNotificationService>();

        await LembreteInscricaoNaoPagaBackgroundService.VarrerAsync(ctx, push, SeteDiasAntes);

        await push.Received().EnviarParaJogadorAsync(
            Arg.Any<int>(), Arg.Any<string>(),
            Arg.Is<string>(c => c.Contains("125") && c.Contains("21/09")),
            $"/Torneios/Details/{torneio.Id}", Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task O_americano_tambem_e_lembrado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, categoria) = TorneioQueCobra(ctx, qtdDuplas: 0);
        var jogador = ctx.Jogadores.First(j => j.Nome == "Organizador");
        ctx.InscricoesAmericanas.Add(new InscricaoAmericana
        {
            CategoriaId = categoria.Id,
            JogadorId = jogador.Id,
            ValorInscricao = 125m,
        });
        ctx.SaveChanges();
        var push = Substitute.For<IPushNotificationService>();

        var lembradas = await LembreteInscricaoNaoPagaBackgroundService.VarrerAsync(ctx, push, SeteDiasAntes);

        Assert.Equal(1, lembradas);
        Assert.Equal(7, ctx.InscricoesAmericanas.Single().UltimoLembreteDePagamento);
    }
}
