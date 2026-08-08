using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// "Só confirmo a inscrição depois de pago" virou editável (08/08/2026).
//
// A chave só existia na tela de CRIAÇÃO e o Editar nunca a lia. ⚠️ Desligada, NENHUMA
// cobrança é gerada em lugar nenhum: os dois únicos caminhos que criam cobrança de torneio
// (DuplasController.Create e TorneiosController.InscreverAmericano) passam por ela, e não
// existe botão de "pagar depois".
//
// Descoberto no NATA PADEL TOUR: ele nasceu "por fora" (chave desligada), virou "pelo site" —
// e ficou cobrando pelo site SÓ NO NOME. O Felipe se inscreveu e não apareceu pagamento.
public class PagamentoObrigatorioEditavelTests
{
    private static Task<IActionResult> Editar(
        Padelizou.Controllers.TorneiosController controlador, int torneioId, bool? exigePagar) =>
        controlador.Editar(
            torneioId, "Torneio de Teste", null, null, precoInscricao: 120m, clubeId: 0,
            quantidadeQuadras: 2, nomesQuadras: null,
            permiteImpedimentos: false, permiteImpedimentoSextaNoite: false,
            permiteImpedimentoSabadoManha: false, permiteImpedimentoSabadoTarde: false,
            restricaoCategoria: null, capa: null,
            pagamentoObrigatorio: exigePagar);

    [Fact]
    public async Task Ligar_a_chave_passa_a_exigir_pagamento()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        torneio.FormaPagamento = FormaDePagamentoDoTorneio.TodasAsFormas;
        torneio.PagamentoObrigatorioNaInscricao = false;
        ctx.SaveChanges();

        var controlador = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await Editar(controlador, torneio.Id, exigePagar: true);

        Assert.True(ctx.Torneios.Find(torneio.Id)!.PagamentoObrigatorioNaInscricao);
    }

    [Fact]
    public async Task Desligar_a_chave_tambem_funciona()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        torneio.PagamentoObrigatorioNaInscricao = true;
        ctx.SaveChanges();

        var controlador = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await Editar(controlador, torneio.Id, exigePagar: false);

        Assert.False(ctx.Torneios.Find(torneio.Id)!.PagamentoObrigatorioNaInscricao);
    }

    [Fact]
    public async Task Campo_ausente_mantem_o_que_estava_gravado()
    {
        // Aba aberta antes deste deploy não manda o campo — e torneio "por fora" nem desenha
        // a chave. Cair no default trocaria como o torneio cobra sem ninguém ter pedido.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        torneio.PagamentoObrigatorioNaInscricao = false;
        ctx.SaveChanges();

        var controlador = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        await Editar(controlador, torneio.Id, exigePagar: null);

        Assert.False(ctx.Torneios.Find(torneio.Id)!.PagamentoObrigatorioNaInscricao);
    }

    [Fact]
    public async Task Quem_nao_organiza_nao_mexe_na_chave()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 0);
        torneio.PagamentoObrigatorioNaInscricao = false;
        var estranho = new Jogador { Nome = "Estranho", Cpf = "11144477735" };
        ctx.Jogadores.Add(estranho);
        ctx.SaveChanges();

        var controlador = TestInfra.NovoTorneiosController(ctx, estranho.Id);
        await Editar(controlador, torneio.Id, exigePagar: true);

        Assert.False(ctx.Torneios.Find(torneio.Id)!.PagamentoObrigatorioNaInscricao);
    }

    [Fact]
    public void Torneio_novo_ja_nasce_exigindo_pagamento()
    {
        // O padrão do modelo não mudou: quem cria hoje continua nascendo com a chave ligada.
        Assert.True(new Torneio { Nome = "T", Codigo = "T1" }.PagamentoObrigatorioNaInscricao);
    }
}
