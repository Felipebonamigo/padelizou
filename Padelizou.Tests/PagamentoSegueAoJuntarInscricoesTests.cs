using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;
using Xunit;

namespace Padelizou.Tests;

// QUEM JÁ PAGOU CONTINUA PAGO QUANDO DUAS INSCRIÇÕES VIRAM UMA.
//
// 🗣️ Felipe, 19/08/2026: *"se ele já pagou, tem q se manter como pago"*. Juntar apaga a
// inscrição sozinha — e, até aqui, apagava junto o "pago" dela: quem tinha pagado a própria
// inscrição virava DEVEDOR ao fechar dupla, entrava na lista de inadimplentes do organizador,
// levava lembrete de pagamento e seria cobrado de novo na quadra por um dinheiro que já
// estava na conta.
//
// Vale nos TRÊS caminhos que juntam: o aceite (mural e convite por link), a troca de parceiro
// e a inscrição nova.
public class PagamentoSegueAoJuntarInscricoesTests
{
    private static DuplasController Controller(
        DbPadelContext ctx, int usuarioLogadoId, IPagamentoInscricaoService? pagamentos = null)
    {
        var controller = new DuplasController(
            ctx,
            new EstatisticasService(ctx),
            Substitute.For<IPushNotificationService>(),
            pagamentos ?? Substitute.For<IPagamentoInscricaoService>(),
            new ValidacaoPeloRankingRs(ctx, Substitute.For<IRankingRsService>(),
                NullLogger<ValidacaoPeloRankingRs>.Instance),
            new AvisoDeInscricaoNoTorneio(ctx, Substitute.For<IPushNotificationService>()),
            NullLogger<DuplasController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.ToString()) }, "Teste")),
            },
        };
        controller.TempData = new TempDataDictionary(controller.HttpContext, Substitute.For<ITempDataProvider>());
        controller.Url = TestInfra.UrlDeTeste();
        return controller;
    }

    // O dono inscrito sozinho, o candidato inscrito sozinho E JÁ PAGO, e o chamado feito.
    private static (DbPadelContext ctx, Torneio torneio, Dupla doDono, Dupla doCandidato, Jogador dono, Jogador candidato)
        Cenario(bool candidatoPagou = true, bool donoPagou = false)
    {
        var ctx = TestInfra.NovoContexto();

        // CPFs com dígito verificador de verdade: a troca de parceiro e a inscrição nova
        // recusam CPF inventado (ver Services/Documentos.CpfEhValido).
        var dono = new Jogador { Nome = "Gabriel Dono", Cpf = "11144477735" };
        var candidato = new Jogador { Nome = "Everson Candidato", Cpf = "22255588846" };
        ctx.Jogadores.AddRange(dono, candidato);

        var torneio = new Torneio
        {
            Nome = "The Last Dance", Codigo = "TLD1", Status = "Inscrições Abertas", PrecoInscricao = 100m,
        };
        ctx.Torneios.Add(torneio);
        var categoria = new Categoria { Nome = "5ª Categoria Masculina", Codigo = "C5M", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        var doDono = new Dupla
        {
            CategoriaId = categoria.Id, Jogador1Id = dono.Id, Jogador2Id = null,
            ValorInscricao = 100m, Pago = donoPagou,
            PagoEm = donoPagou ? new DateTime(2026, 8, 10, 9, 0, 0) : null,
        };
        var doCandidato = new Dupla
        {
            CategoriaId = categoria.Id, Jogador1Id = candidato.Id, Jogador2Id = null,
            ValorInscricao = 100m, Pago = candidatoPagou,
            PagoEm = candidatoPagou ? new DateTime(2026, 8, 12, 15, 30, 0) : null,
        };
        ctx.Duplas.AddRange(doDono, doCandidato);
        ctx.SaveChanges();

        ctx.ChamadosDoMural.Add(new ChamadoDoMural
        {
            DuplaId = doDono.Id, CandidatoId = candidato.Id, CriadoEm = DateTime.Now.AddHours(-1),
        });
        ctx.SaveChanges();

        return (ctx, torneio, doDono, doCandidato, dono, candidato);
    }

    // ═══════════════════ A REGRA PURA ═══════════════════

    [Fact]
    public void A_inscricao_que_fica_herda_o_pago_da_que_sai()
    {
        var fica = new Dupla { Pago = false };
        var sai = new Dupla { Pago = true, PagoEm = new DateTime(2026, 8, 12, 15, 30, 0) };

        JuntarInscricoes.LevarOPagamento(fica, new[] { sai });

        Assert.True(fica.Pago);
        // A data é a do dinheiro que entrou — não a de hoje. Carimbar "agora" diria que o
        // pagamento aconteceu no momento em que as inscrições foram juntadas.
        Assert.Equal(new DateTime(2026, 8, 12, 15, 30, 0), fica.PagoEm);
    }

    // ⚠️ NUNCA REBAIXA: inscrição paga não volta a dever porque a outra não estava paga.
    [Fact]
    public void Quem_ja_estava_pago_nao_deixa_de_estar()
    {
        var pagoEm = new DateTime(2026, 8, 10, 9, 0, 0);
        var fica = new Dupla { Pago = true, PagoEm = pagoEm };

        JuntarInscricoes.LevarOPagamento(fica, new[] { new Dupla { Pago = false } });

        Assert.True(fica.Pago);
        Assert.Equal(pagoEm, fica.PagoEm);
    }

    [Fact]
    public void Sem_ninguem_pago_nada_vira_pago()
    {
        var fica = new Dupla { Pago = false };

        JuntarInscricoes.LevarOPagamento(fica, new[] { new Dupla { Pago = false } });

        Assert.False(fica.Pago);
        Assert.Null(fica.PagoEm);
    }

    // Entre duas pagas, vale a data do dinheiro que entrou PRIMEIRO.
    [Fact]
    public void Entre_duas_pagas_fica_a_data_mais_antiga()
    {
        var fica = new Dupla { Pago = false };
        var antiga = new Dupla { Pago = true, PagoEm = new DateTime(2026, 8, 1, 8, 0, 0) };
        var recente = new Dupla { Pago = true, PagoEm = new DateTime(2026, 8, 15, 8, 0, 0) };

        JuntarInscricoes.LevarOPagamento(fica, new[] { recente, antiga });

        Assert.Equal(new DateTime(2026, 8, 1, 8, 0, 0), fica.PagoEm);
    }

    // ═══════════════════ O ACEITE DO MURAL ═══════════════════

    [Fact]
    public async Task Aceitar_quem_ja_tinha_pago_mantem_a_dupla_como_paga()
    {
        var (ctx, _t, doDono, doCandidato, dono, candidato) = Cenario();
        using var _ = ctx;

        var controller = Controller(ctx, dono.Id);
        await controller.AceitarChamado(doDono.Id, candidato.Id);

        var dupla = await ctx.Duplas.FindAsync(doDono.Id);
        Assert.Equal(candidato.Id, dupla!.Jogador2Id);
        Assert.True(dupla.Pago, "a inscrição que ele já tinha pago virou dupla não paga");
        Assert.Equal(new DateTime(2026, 8, 12, 15, 30, 0), dupla.PagoEm);

        // A inscrição sozinha dele saiu mesmo — o pago veio da que foi absorvida.
        Assert.Null(await ctx.Duplas.FindAsync(doCandidato.Id));

        // E quem aceitou fica sabendo: o organizador não vai cobrar de novo.
        Assert.Contains("paga", (string)controller.TempData["Sucesso"]!);
    }

    // Ninguém pagou: nada de "pago" nascendo do nada.
    [Fact]
    public async Task Aceitar_quem_nao_pagou_nao_marca_a_dupla_como_paga()
    {
        var (ctx, _t, doDono, _dc, dono, candidato) = Cenario(candidatoPagou: false);
        using var _ = ctx;

        await Controller(ctx, dono.Id).AceitarChamado(doDono.Id, candidato.Id);

        Assert.False((await ctx.Duplas.FindAsync(doDono.Id))!.Pago);
    }

    // A inscrição de quem aceita já estava paga e a do outro não: continua paga.
    [Fact]
    public async Task Aceitar_nao_rebaixa_a_inscricao_que_ja_estava_paga()
    {
        var (ctx, _t, doDono, _dc, dono, candidato) = Cenario(candidatoPagou: false, donoPagou: true);
        using var _ = ctx;

        await Controller(ctx, dono.Id).AceitarChamado(doDono.Id, candidato.Id);

        var dupla = await ctx.Duplas.FindAsync(doDono.Id);
        Assert.True(dupla!.Pago);
        Assert.Equal(new DateTime(2026, 8, 10, 9, 0, 0), dupla.PagoEm);
    }

    // ⚠️ A COBRANÇA DE "PAGAR DEPOIS" PASSA A APONTAR PRA INSCRIÇÃO QUE FICOU. Sem isto, o
    // pagamento que confirmasse depois do aceite procuraria uma linha apagada: o dinheiro
    // entraria e ninguém ficaria marcado como pago (o serviço só registra no log).
    [Fact]
    public async Task A_cobranca_pendente_da_inscricao_que_saiu_passa_a_apontar_pra_que_ficou()
    {
        var (ctx, torneio, doDono, doCandidato, dono, candidato) = Cenario(candidatoPagou: false);
        using var _ = ctx;

        ctx.Pagamentos.Add(new Pagamento
        {
            Tipo = "TorneioPagarDepois",
            TorneioId = torneio.Id,
            JogadorId = candidato.Id,
            Valor = 100m,
            Status = "Pendente",
            DadosInscricao = JsonSerializer.Serialize(
                new DadosPagamentoDeInscricao(torneio.Id, doCandidato.Id, null)),
        });
        await ctx.SaveChangesAsync();

        await Controller(ctx, dono.Id).AceitarChamado(doDono.Id, candidato.Id);

        var pagamento = await ctx.Pagamentos.FirstAsync();
        var dados = JsonSerializer.Deserialize<DadosPagamentoDeInscricao>(pagamento.DadosInscricao!);
        Assert.Equal(doDono.Id, dados!.DuplaId);
    }

    // ⚠️ O tipo "TorneioDupla" NÃO é repontado, e é de propósito: lá o estorno APAGA a
    // inscrição apontada, e repontar faria o estorno pedido por um derrubar do torneio a dupla
    // inteira — inclusive quem não pediu nada e pode ter pago.
    [Fact]
    public async Task A_cobranca_do_tipo_TorneioDupla_nao_e_repontada()
    {
        var (ctx, torneio, doDono, doCandidato, dono, candidato) = Cenario();
        using var _ = ctx;

        ctx.Pagamentos.Add(new Pagamento
        {
            Tipo = "TorneioDupla",
            TorneioId = torneio.Id,
            JogadorId = candidato.Id,
            Valor = 100m,
            Status = "Confirmado",
            ReferenciaId = doCandidato.Id,
        });
        await ctx.SaveChangesAsync();

        await Controller(ctx, dono.Id).AceitarChamado(doDono.Id, candidato.Id);

        Assert.Equal(doCandidato.Id, (await ctx.Pagamentos.FirstAsync()).ReferenciaId);
    }

    // ═══════════════════ TROCAR PARCEIRO ═══════════════════

    [Fact]
    public async Task Trocar_parceiro_juntando_a_inscricao_paga_dele_mantem_a_dupla_paga()
    {
        var (ctx, _t, doDono, doCandidato, dono, candidato) = Cenario();
        using var _ = ctx;

        await Controller(ctx, dono.Id).TrocarParceiro(
            doDono.Id, candidato.Cpf, null, juntarComInscricaoSolo: true);

        var dupla = await ctx.Duplas.FindAsync(doDono.Id);
        Assert.Equal(candidato.Id, dupla!.Jogador2Id);
        Assert.True(dupla.Pago);
        Assert.Null(await ctx.Duplas.FindAsync(doCandidato.Id));
    }

    // ═══════════════════ INSCRIÇÃO NOVA ═══════════════════

    // Alguém inscreve a dupla e junta a inscrição sozinha JÁ PAGA do parceiro: a inscrição
    // nova nasce paga. Sem isso, o caminho mais comum de juntar (a tela de inscrição) seria
    // justamente o que apaga o pagamento.
    [Fact]
    public async Task Inscricao_nova_que_junta_uma_inscricao_paga_nasce_paga()
    {
        var (ctx, torneio, doDono, doCandidato, dono, candidato) = Cenario();
        using var _ = ctx;

        // O dono desiste da própria linha: aqui quem inscreve a dupla é ele, do zero, e a
        // única inscrição a juntar é a sozinha (paga) do parceiro.
        ctx.Duplas.Remove(doDono);
        await ctx.SaveChangesAsync();

        var categoriaId = doCandidato.CategoriaId;
        var pagamentos = Substitute.For<IPagamentoInscricaoService>();

        await Controller(ctx, dono.Id, pagamentos).Create(
            torneio.Id, categoriaId,
            dono.Nome, dono.Cpf, null, null, null,
            candidato.Nome, candidato.Cpf, null, null, null,
            false, false, false, false,
            juntarComInscricaoSolo: true);

        var dupla = await ctx.Duplas.SingleAsync();
        Assert.Equal(dono.Id, dupla.Jogador1Id);
        Assert.Equal(candidato.Id, dupla.Jogador2Id);
        Assert.True(dupla.Pago, "a inscrição sozinha dele estava paga e a dupla nasceu devendo");

        // E não abre checkout pra cobrar de novo o que já entrou.
        await pagamentos.DidNotReceive().IniciarCobrancaDeInscricaoAsync(
            Arg.Any<Torneio>(), Arg.Any<Jogador>(), Arg.Any<Jogador>(), Arg.Any<bool>(),
            Arg.Any<int>(), Arg.Any<DadosPagamentoDeInscricao>(), Arg.Any<string?>());
    }
}
