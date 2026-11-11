using Microsoft.EntityFrameworkCore;
using NSubstitute;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 11/11/2026 — PAGO / NÃO PAGO E COBRAR POR JOGADOR, COM "DUPLA PAGOU" PRA QUEM PAGA JUNTO.
//
// 🗣️ Felipe, com o print da aba de inscritos: *"temos que pôr o pago/não pago e cobrar para
// jogador individualmente também e a opção a dupla pagou, para otimizar"* · *"porque
// normalmente a cobrança é individual"* · *"o valor da inscrição é colocado em todo torneio"*.
//
// Até aqui o "Pago" era UM campo por dupla, e o "Cobrar" mandava a inscrição INTEIRA pro
// primeiro jogador. A marca manual mora por JOGADOR (não por posição 1/2): trocar parceiro não
// pode levar o "pago" de um pra quem entrou no lugar.
public class PagamentoPorJogadorTests
{
    // ── A régua pura ─────────────────────────────────────────────────────────────────────

    private static Dupla Dupla(bool pago = false) =>
        new() { Id = 10, Jogador1Id = 1, Jogador2Id = 2, Pago = pago };

    [Fact]
    public void Dupla_paga_vale_pros_dois()
    {
        var d = Dupla(pago: true);
        Assert.True(PagamentoPorJogador.Pagou(d, 1, [], []));
        Assert.True(PagamentoPorJogador.Pagou(d, 2, [], []));
    }

    [Fact]
    public void Marca_do_organizador_vale_so_pra_quem_foi_marcado()
    {
        var d = Dupla();
        var manuais = new[] { new PagamentoManualDoJogador { DuplaId = 10, JogadorId = 1 } };
        Assert.True(PagamentoPorJogador.Pagou(d, 1, manuais, []));
        Assert.False(PagamentoPorJogador.Pagou(d, 2, manuais, []));
    }

    [Fact]
    public void Quem_pagou_a_parte_dele_pelo_app_aparece_pago()
    {
        var d = Dupla();
        var confirmados = new[] { new Pagamento { JogadorId = 2, Status = "Confirmado", ReferenciaId = 10 } };
        Assert.False(PagamentoPorJogador.Pagou(d, 1, [], confirmados));
        Assert.True(PagamentoPorJogador.Pagou(d, 2, [], confirmados));
    }

    [Fact]
    public void A_parte_de_cada_um_vem_do_valor_do_torneio_e_soma_o_total()
    {
        // Inscrição por pessoa 100; segunda inscrição no torneio 80. O 2 já jogava outra
        // categoria. Impedimento com taxa de 20 na dupla: metade pra cada.
        var torneio = new Torneio { PrecoInscricao = 100m, PrecoSegundaInscricao = 80m };
        var d = Dupla();
        d.ValorInscricao = 200m;

        var (j1, j2) = PagamentoPorJogador.Partes(torneio, d, jogador1JaNoTorneio: false, jogador2JaNoTorneio: true);

        Assert.Equal(110m, j1);
        Assert.Equal(90m, j2);
    }

    [Fact]
    public void Inscricao_antiga_sem_valor_gravado_usa_o_preco_do_torneio()
    {
        var torneio = new Torneio { PrecoInscricao = 100m };
        var (j1, j2) = PagamentoPorJogador.Partes(torneio, Dupla(), false, false);
        Assert.Equal(100m, j1);
        Assert.Equal(100m, j2);
    }

    [Fact]
    public void Centavo_impar_vai_pro_primeiro_e_a_soma_fecha()
    {
        var torneio = new Torneio { PrecoInscricao = 100m };
        var d = Dupla();
        d.ValorInscricao = 200.01m;
        var (j1, j2) = PagamentoPorJogador.Partes(torneio, d, false, false);
        Assert.Equal(200.01m, j1 + j2);
        Assert.Equal(100.01m, j1);
    }

    [Fact]
    public void Sem_parceiro_a_parte_do_unico_e_o_total()
    {
        var torneio = new Torneio { PrecoInscricao = 100m };
        var d = new Dupla { Jogador1Id = 1, ValorInscricao = 100m };
        Assert.Equal((100m, 0m), PagamentoPorJogador.Partes(torneio, d, false, false));
    }

    [Fact]
    public void O_retrato_da_tela_sabe_quem_pagou_e_quanto_cada_um_deve()
    {
        // O jogador 2 já estava na dupla 10 (inscrita antes): na dupla 20 ele paga o preço de
        // segunda inscrição. Na 10, os dois pagam o cheio.
        var torneio = new Torneio { PrecoInscricao = 100m, PrecoSegundaInscricao = 80m };
        var primeira = new Dupla { Id = 10, Jogador1Id = 1, Jogador2Id = 2, ValorInscricao = 200m };
        var segunda = new Dupla { Id = 20, Jogador1Id = 3, Jogador2Id = 2, ValorInscricao = 180m };
        var manuais = new[] { new PagamentoManualDoJogador { DuplaId = 20, JogadorId = 3 } };

        var retrato = PagamentoPorJogador.Retrato(torneio, new[] { primeira, segunda }, manuais, []);

        Assert.Equal(new PagamentoPorJogador.Situacao(false, false, 100m, 100m), retrato[10]);
        Assert.Equal(new PagamentoPorJogador.Situacao(true, false, 100m, 80m), retrato[20]);
    }

    // ── O botão do organizador ───────────────────────────────────────────────────────────

    private static (int torneioId, int duplaId, int organizadorId) Montar(DbPadelContext ctx, bool comParceiro = true)
    {
        var organizador = new Jogador { Nome = "Organizador", Cpf = "11144477735" };
        var j1 = new Jogador { Nome = "Felipe Borges", Cpf = "22255588846" };
        var j2 = new Jogador { Nome = "Alexandre Costa", Cpf = "52998224725" };
        ctx.Jogadores.AddRange(organizador, j1, j2);
        ctx.SaveChanges();
        var torneio = new Torneio { Nome = "THE LAST DANCE", Codigo = "TLD", Status = "Inscrições Abertas", PrecoInscricao = 100m };
        ctx.Torneios.Add(torneio);
        ctx.SaveChanges();
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = organizador.Id });
        var categoria = new Categoria { Nome = "4ª Categoria Masculina", Codigo = "C4M", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();
        var dupla = new Dupla { CategoriaId = categoria.Id, Jogador1Id = j1.Id, Jogador2Id = comParceiro ? j2.Id : null };
        ctx.Duplas.Add(dupla);
        ctx.SaveChanges();
        return (torneio.Id, dupla.Id, organizador.Id);
    }

    [Fact]
    public async Task Marcar_um_jogador_nao_quita_a_dupla()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, duplaId, org) = Montar(ctx);
        var dupla = await ctx.Duplas.FindAsync(duplaId);

        await TestInfra.NovoTorneiosController(ctx, org).AlternarPagamentoJogador(duplaId, dupla!.Jogador1Id);

        Assert.False(dupla.Pago);
        Assert.Single(ctx.PagamentosManuaisDeJogador.Where(p => p.DuplaId == duplaId && p.JogadorId == dupla.Jogador1Id));
    }

    [Fact]
    public async Task Marcar_os_dois_quita_a_dupla()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, duplaId, org) = Montar(ctx);
        var dupla = (await ctx.Duplas.FindAsync(duplaId))!;
        var controller = TestInfra.NovoTorneiosController(ctx, org);

        await controller.AlternarPagamentoJogador(duplaId, dupla.Jogador1Id);
        await controller.AlternarPagamentoJogador(duplaId, dupla.Jogador2Id!.Value);

        Assert.True(dupla.Pago);
        Assert.NotNull(dupla.PagoEm);
    }

    [Fact]
    public async Task Inscricao_sem_parceiro_quita_com_o_unico_jogador()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, duplaId, org) = Montar(ctx, comParceiro: false);
        var dupla = (await ctx.Duplas.FindAsync(duplaId))!;

        await TestInfra.NovoTorneiosController(ctx, org).AlternarPagamentoJogador(duplaId, dupla.Jogador1Id);

        Assert.True(dupla.Pago);
    }

    [Fact]
    public async Task Desmarcar_um_da_dupla_paga_mantem_o_outro_pago()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, duplaId, org) = Montar(ctx);
        var dupla = (await ctx.Duplas.FindAsync(duplaId))!;
        dupla.Pago = true;   // "Dupla pagou"
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org).AlternarPagamentoJogador(duplaId, dupla.Jogador2Id!.Value);

        Assert.False(dupla.Pago);
        var manuais = await ctx.PagamentosManuaisDeJogador.Where(p => p.DuplaId == duplaId).ToListAsync();
        Assert.True(PagamentoPorJogador.Pagou(dupla, dupla.Jogador1Id, manuais, []));
        Assert.False(PagamentoPorJogador.Pagou(dupla, dupla.Jogador2Id.Value, manuais, []));
    }

    [Fact]
    public async Task Quem_pagou_pelo_app_nao_e_desmarcado_na_mao()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneioId, duplaId, org) = Montar(ctx);
        var dupla = (await ctx.Duplas.FindAsync(duplaId))!;
        ctx.Pagamentos.Add(new Pagamento
        {
            Tipo = "TorneioPagarDepois", TorneioId = torneioId, JogadorId = dupla.Jogador1Id,
            Valor = 100m, Status = "Confirmado", ReferenciaId = duplaId,
        });
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org);
        await controller.AlternarPagamentoJogador(duplaId, dupla.Jogador1Id);

        Assert.Empty(ctx.PagamentosManuaisDeJogador);
        Assert.Contains("pelo app", controller.TempData["Erro"]?.ToString());
    }

    [Fact]
    public async Task Marcar_um_jogador_cancela_so_a_fatura_aberta_dele()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneioId, duplaId, org) = Montar(ctx);
        var dupla = (await ctx.Duplas.FindAsync(duplaId))!;
        var pagamentos = Substitute.For<IPagamentoInscricaoService>();

        await TestInfra.NovoTorneiosController(ctx, org, pagamentos).AlternarPagamentoJogador(duplaId, dupla.Jogador2Id!.Value);

        await pagamentos.Received(1).CancelarCobrancasAbertasDaInscricaoAsync(torneioId, duplaId, null, dupla.Jogador2Id.Value);
    }

    [Fact]
    public async Task Desmarcar_a_dupla_inteira_limpa_as_marcas_individuais()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, duplaId, org) = Montar(ctx);
        var dupla = (await ctx.Duplas.FindAsync(duplaId))!;
        var controller = TestInfra.NovoTorneiosController(ctx, org);
        await controller.AlternarPagamentoJogador(duplaId, dupla.Jogador1Id);
        await controller.AlternarPagamentoJogador(duplaId, dupla.Jogador2Id!.Value);
        Assert.True(dupla.Pago);

        await controller.AlternarPagamentoDupla(duplaId);   // "Marcar não pago" da dupla

        Assert.False(dupla.Pago);
        Assert.Empty(ctx.PagamentosManuaisDeJogador);
    }

    [Fact]
    public async Task Quem_nao_e_organizador_nao_mexe()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, duplaId, _) = Montar(ctx);
        var dupla = (await ctx.Duplas.FindAsync(duplaId))!;

        var resultado = await TestInfra.NovoTorneiosController(ctx, dupla.Jogador1Id).AlternarPagamentoJogador(duplaId, dupla.Jogador1Id);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(resultado);
        Assert.Empty(ctx.PagamentosManuaisDeJogador);
    }

    [Fact]
    public async Task Jogador_que_nao_e_da_dupla_e_recusado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (_, duplaId, org) = Montar(ctx);

        var resultado = await TestInfra.NovoTorneiosController(ctx, org).AlternarPagamentoJogador(duplaId, 999);

        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(resultado);
        Assert.Empty(ctx.PagamentosManuaisDeJogador);
    }
}
