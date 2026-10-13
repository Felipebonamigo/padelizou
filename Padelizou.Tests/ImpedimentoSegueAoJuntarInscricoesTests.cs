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

// O IMPEDIMENTO DE QUEM SE INSCREVEU SOZINHO NÃO SOME QUANDO A INSCRIÇÃO É JUNTADA.
//
// 🗣️ Felipe, 13/10/2026: *"deixe para a pessoa q colocou impedimento, salvo lá, o impedimento q
// ela colocou"* — e, escolhida a opção (a), *"lembrando que o impedimento é um por dupla"*.
//
// Juntar apaga a inscrição sozinha e mantém a outra. Até aqui o impedimento ia junto com a
// linha apagada: a pessoa marcava "Sábado de manhã", fechava dupla, e a dupla aparecia sem
// impedimento nenhum — exatamente o "não está ficando salvo" do Andrey no NATA.
//
// A régua: a inscrição que fica HERDA o impedimento da que sai quando não tem um; a taxa entra
// no valor como se tivesse sido marcado desde o começo; e quando as duas tinham (um por dupla),
// fica o da que fica e quem perdeu o seu é avisado.
//
// Vale nos TRÊS caminhos que juntam: o aceite (mural e convite), a troca de parceiro e a
// inscrição nova — a mesma lista do PagamentoSegueAoJuntarInscricoesTests.
public class ImpedimentoSegueAoJuntarInscricoesTests
{
    private const decimal Preco = 100m;
    private const decimal Taxa = 20m;

    private static Torneio TorneioComTaxa() => new()
    {
        Nome = "NATA PADEL TOUR", Codigo = "NATA", Status = "Inscrições Abertas",
        PrecoInscricao = Preco, TaxaPorImpedimento = Taxa,
    };

    // ═══════════════════ A REGRA PURA ═══════════════════

    [Fact]
    public void A_que_fica_sem_impedimento_herda_o_da_que_sai_com_a_taxa_no_valor()
    {
        var torneio = TorneioComTaxa();
        var fica = new Dupla { Jogador1Id = 1, ValorInscricao = 200m };
        var sai = new Dupla { Jogador1Id = 2, ImpedimentoSabadoManha = true, ValorInscricao = 120m };

        var resultado = JuntarInscricoes.LevarOImpedimento(fica, new[] { sai }, torneio);

        Assert.Equal(TurnoDoImpedimento.SabadoManha, AlteracaoDeImpedimento.TurnoAtual(fica));
        Assert.Equal(200m + Taxa, fica.ValorInscricao);
        Assert.True(resultado.Veio);
        Assert.Empty(resultado.FicaramDeFora);
    }

    // Quem marcou continua sendo quem marcou: se foi o organizador, a tela segue dizendo
    // "posto pelo organizador"; se veio da inscrição (nulo), segue "pedido pelo jogador".
    [Fact]
    public void A_autoria_vem_junto()
    {
        var torneio = TorneioComTaxa();
        var quando = new DateTime(2026, 10, 9, 18, 30, 0);
        var fica = new Dupla { Jogador1Id = 1, ValorInscricao = 200m };
        var sai = new Dupla
        {
            Jogador1Id = 2, ImpedimentoSabadoTarde = true, ValorInscricao = 120m,
            ImpedimentoAlteradoPorId = 99, ImpedimentoAlteradoEm = quando,
        };

        JuntarInscricoes.LevarOImpedimento(fica, new[] { sai }, torneio);

        Assert.Equal(99, fica.ImpedimentoAlteradoPorId);
        Assert.Equal(quando, fica.ImpedimentoAlteradoEm);
    }

    [Fact]
    public void Autoria_nula_continua_nula()
    {
        var torneio = TorneioComTaxa();
        var fica = new Dupla
        {
            Jogador1Id = 1, ValorInscricao = 200m,
            // Lixo de propósito: a autoria que vale é a do impedimento que chegou.
            ImpedimentoAlteradoPorId = 7, ImpedimentoAlteradoEm = new DateTime(2026, 10, 1),
        };
        var sai = new Dupla { Jogador1Id = 2, ImpedimentoSabadoTarde = true };

        JuntarInscricoes.LevarOImpedimento(fica, new[] { sai }, torneio);

        Assert.Null(fica.ImpedimentoAlteradoPorId);
        Assert.Null(fica.ImpedimentoAlteradoEm);
    }

    // UM POR DUPLA: as duas tinham, turnos diferentes. Fica o da que fica — valor intacto — e o
    // dono da que saiu entra na lista de quem precisa ser avisado.
    [Fact]
    public void As_duas_com_impedimento_fica_o_da_que_fica_e_o_outro_e_avisado()
    {
        var torneio = TorneioComTaxa();
        var fica = new Dupla { Jogador1Id = 1, ImpedimentoSabadoTarde = true, ValorInscricao = 220m };
        var sai = new Dupla { Jogador1Id = 2, ImpedimentoSabadoManha = true, ValorInscricao = 120m };

        var resultado = JuntarInscricoes.LevarOImpedimento(fica, new[] { sai }, torneio);

        Assert.Equal(TurnoDoImpedimento.SabadoTarde, AlteracaoDeImpedimento.TurnoAtual(fica));
        Assert.False(fica.ImpedimentoSabadoManha);
        Assert.Equal(220m, fica.ValorInscricao);
        Assert.False(resultado.Veio);
        var deFora = Assert.Single(resultado.FicaramDeFora);
        Assert.Equal(2, deFora.JogadorId);
        Assert.Equal(TurnoDoImpedimento.SabadoManha, deFora.Turno);
    }

    // O mesmo turno dos dois lados não é conflito: ninguém perdeu nada, e a taxa não dobra.
    [Fact]
    public void O_mesmo_turno_nos_dois_lados_nao_dobra_a_taxa_nem_avisa()
    {
        var torneio = TorneioComTaxa();
        var fica = new Dupla { Jogador1Id = 1, ImpedimentoSabadoManha = true, ValorInscricao = 220m };
        var sai = new Dupla { Jogador1Id = 2, ImpedimentoSabadoManha = true, ValorInscricao = 120m };

        var resultado = JuntarInscricoes.LevarOImpedimento(fica, new[] { sai }, torneio);

        Assert.Equal(220m, fica.ValorInscricao);
        Assert.False(resultado.Veio);
        Assert.Empty(resultado.FicaramDeFora);
    }

    // Duas sozinhas absorvidas com turnos diferentes: entra UMA, a outra é avisada.
    [Fact]
    public void Nunca_entram_dois_impedimentos_na_mesma_dupla()
    {
        var torneio = TorneioComTaxa();
        var fica = new Dupla { Jogador1Id = 1, ValorInscricao = 200m };
        var primeira = new Dupla { Id = 10, Jogador1Id = 2, ImpedimentoSabadoManha = true };
        var segunda = new Dupla { Id = 11, Jogador1Id = 3, ImpedimentoSabadoTarde = true };

        var resultado = JuntarInscricoes.LevarOImpedimento(fica, new[] { segunda, primeira }, torneio);

        var marcados = (fica.ImpedimentoQuintaNoite ? 1 : 0) + (fica.ImpedimentoSextaNoite ? 1 : 0)
                       + (fica.ImpedimentoSabadoManha ? 1 : 0) + (fica.ImpedimentoSabadoTarde ? 1 : 0);
        Assert.Equal(1, marcados);
        Assert.Equal(200m + Taxa, fica.ValorInscricao);
        // A inscrição mais antiga (menor Id) é a que chegou primeiro — é a dela que entra.
        Assert.Equal(TurnoDoImpedimento.SabadoManha, AlteracaoDeImpedimento.TurnoAtual(fica));
        Assert.Equal(3, Assert.Single(resultado.FicaramDeFora).JogadorId);
    }

    // Turno que o torneio NÃO TEM (o "Sexta à noite" num torneio de sábado, de antes da trava de
    // 07/10) não é herdado: cobraria taxa por uma janela que o sorteio ignora.
    [Fact]
    public void Turno_que_o_torneio_nao_tem_nao_e_herdado()
    {
        var torneio = TorneioComTaxa();
        torneio.PermiteImpedimentoSextaNoite = false;
        var fica = new Dupla { Jogador1Id = 1, ValorInscricao = 200m };
        var sai = new Dupla { Jogador1Id = 2, ImpedimentoSextaNoite = true };

        var resultado = JuntarInscricoes.LevarOImpedimento(fica, new[] { sai }, torneio);

        Assert.Equal(TurnoDoImpedimento.Nenhum, AlteracaoDeImpedimento.TurnoAtual(fica));
        Assert.Equal(200m, fica.ValorInscricao);
        Assert.False(resultado.Veio);
        Assert.Empty(resultado.FicaramDeFora);
    }

    // Inscrição anterior à coluna de valor: nulo continua nulo (mesma régua do Aplicar).
    [Fact]
    public void Valor_nulo_continua_nulo()
    {
        var torneio = TorneioComTaxa();
        var fica = new Dupla { Jogador1Id = 1, ValorInscricao = null };
        var sai = new Dupla { Jogador1Id = 2, ImpedimentoSabadoManha = true };

        JuntarInscricoes.LevarOImpedimento(fica, new[] { sai }, torneio);

        Assert.True(fica.ImpedimentoSabadoManha);
        Assert.Null(fica.ValorInscricao);
    }

    // ═══════════════════ OS TRÊS CAMINHOS ═══════════════════

    private static DuplasController Controller(DbPadelContext ctx, int usuarioLogadoId, IPushNotificationService push)
    {
        var controller = new DuplasController(
            ctx,
            new EstatisticasService(ctx),
            push,
            Substitute.For<IPagamentoInscricaoService>(),
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

    // O dono inscrito sozinho, o candidato inscrito sozinho COM "Sábado de manhã", e o chamado
    // feito. Os valores gravados são os que a inscrição de verdade grava: uma pessoa + a taxa.
    private static (DbPadelContext ctx, Torneio torneio, Dupla doDono, Dupla doCandidato, Jogador dono, Jogador candidato)
        Cenario(TurnoDoImpedimento doDonoTurno = TurnoDoImpedimento.Nenhum)
    {
        var ctx = TestInfra.NovoContexto();

        var dono = new Jogador { Nome = "Lucas Dono", Cpf = "11144477735" };
        var candidato = new Jogador { Nome = "Andrey Candidato", Cpf = "22255588846" };
        ctx.Jogadores.AddRange(dono, candidato);

        var torneio = TorneioComTaxa();
        ctx.Torneios.Add(torneio);
        var categoria = new Categoria { Nome = "5ª Categoria Masculina", Codigo = "C5M", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        ctx.SaveChanges();

        var doDono = new Dupla
        {
            CategoriaId = categoria.Id, Jogador1Id = dono.Id,
            ValorInscricao = Preco + (doDonoTurno == TurnoDoImpedimento.Nenhum ? 0m : Taxa),
            ImpedimentoSabadoTarde = doDonoTurno == TurnoDoImpedimento.SabadoTarde,
        };
        var doCandidato = new Dupla
        {
            CategoriaId = categoria.Id, Jogador1Id = candidato.Id,
            ValorInscricao = Preco + Taxa, ImpedimentoSabadoManha = true,
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

    private static Task AvisoDeQueNaoEntrou(IPushNotificationService push, int jogadorId) =>
        push.Received(1).EnviarParaJogadorAsync(jogadorId, Arg.Any<string>(),
            Arg.Is<string>(c => c != null && c.Contains("Sábado de manhã") && c.Contains("Sábado à tarde")),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());

    // ── O aceite do mural ──

    [Fact]
    public async Task Aceitar_quem_tinha_impedimento_traz_o_impedimento_pra_dupla()
    {
        var (ctx, _t, doDono, doCandidato, dono, candidato) = Cenario();
        using var _ = ctx;

        await Controller(ctx, dono.Id, Substitute.For<IPushNotificationService>())
            .AceitarChamado(doDono.Id, candidato.Id);

        var dupla = await ctx.Duplas.FindAsync(doDono.Id);
        Assert.Equal(candidato.Id, dupla!.Jogador2Id);
        Assert.Null(await ctx.Duplas.FindAsync(doCandidato.Id));
        Assert.True(dupla.ImpedimentoSabadoManha, "o impedimento do Andrey sumiu ao fechar a dupla");
        // Duas pessoas + a taxa: o mesmo número que a dupla teria se tivesse nascido assim.
        Assert.Equal(2 * Preco + Taxa, dupla.ValorInscricao);
    }

    [Fact]
    public async Task Aceitar_com_impedimento_dos_dois_lados_mantem_o_do_dono_e_avisa_o_outro()
    {
        var (ctx, _t, doDono, _dc, dono, candidato) = Cenario(TurnoDoImpedimento.SabadoTarde);
        using var _ = ctx;
        var push = Substitute.For<IPushNotificationService>();

        await Controller(ctx, dono.Id, push).AceitarChamado(doDono.Id, candidato.Id);

        var dupla = await ctx.Duplas.FindAsync(doDono.Id);
        Assert.True(dupla!.ImpedimentoSabadoTarde);
        Assert.False(dupla.ImpedimentoSabadoManha);
        Assert.Equal(2 * Preco + Taxa, dupla.ValorInscricao);
        await AvisoDeQueNaoEntrou(push, candidato.Id);
    }

    // ── Trocar parceiro ──

    [Fact]
    public async Task Trocar_parceiro_juntando_a_inscricao_dele_traz_o_impedimento()
    {
        var (ctx, _t, doDono, _dc, dono, candidato) = Cenario();
        using var _ = ctx;

        await Controller(ctx, dono.Id, Substitute.For<IPushNotificationService>()).TrocarParceiro(
            doDono.Id, candidato.Cpf, null, juntarComInscricaoSolo: true);

        var dupla = await ctx.Duplas.FindAsync(doDono.Id);
        Assert.Equal(candidato.Id, dupla!.Jogador2Id);
        Assert.True(dupla.ImpedimentoSabadoManha);
        Assert.Equal(2 * Preco + Taxa, dupla.ValorInscricao);
    }

    [Fact]
    public async Task Trocar_parceiro_com_impedimento_dos_dois_lados_avisa_quem_perdeu_o_seu()
    {
        var (ctx, _t, doDono, _dc, dono, candidato) = Cenario(TurnoDoImpedimento.SabadoTarde);
        using var _ = ctx;
        var push = Substitute.For<IPushNotificationService>();

        await Controller(ctx, dono.Id, push).TrocarParceiro(
            doDono.Id, candidato.Cpf, null, juntarComInscricaoSolo: true);

        var dupla = await ctx.Duplas.FindAsync(doDono.Id);
        Assert.True(dupla!.ImpedimentoSabadoTarde);
        Assert.False(dupla.ImpedimentoSabadoManha);
        await AvisoDeQueNaoEntrou(push, candidato.Id);
    }

    // ⚠️ A DUPLA JÁ COMPLETA E PAGA que troca o parceiro por quem trazia impedimento passa a
    // dever a taxa: ela não pagou por essa janela. Sem isto ficaria "paga" devendo R$ 20 — o
    // mesmo defeito que o Lucas relatou em 06/10 com o parceiro (DeixaDeEstarQuitada).
    [Fact]
    public async Task Dupla_paga_que_herda_impedimento_deixa_de_estar_quitada()
    {
        var (ctx, torneio, doDono, _dc, dono, candidato) = Cenario();
        using var _ = ctx;

        var antigo = new Jogador { Nome = "Parceiro Antigo", Cpf = "52998224725" };
        ctx.Jogadores.Add(antigo);
        await ctx.SaveChangesAsync();
        doDono.Jogador2Id = antigo.Id;
        doDono.ValorInscricao = 2 * Preco;
        doDono.Pago = true;
        doDono.PagoEm = new DateTime(2026, 10, 1, 10, 0, 0);
        ctx.Pagamentos.Add(new Pagamento
        {
            Tipo = "TorneioPagarDepois", TorneioId = torneio.Id, JogadorId = dono.Id,
            Valor = 2 * Preco, Status = "Confirmado", ReferenciaId = doDono.Id,
        });
        await ctx.SaveChangesAsync();

        await Controller(ctx, dono.Id, Substitute.For<IPushNotificationService>()).TrocarParceiro(
            doDono.Id, candidato.Cpf, null, juntarComInscricaoSolo: true);

        var dupla = await ctx.Duplas.FindAsync(doDono.Id);
        Assert.Equal(candidato.Id, dupla!.Jogador2Id);
        Assert.True(dupla.ImpedimentoSabadoManha);
        Assert.Equal(2 * Preco + Taxa, dupla.ValorInscricao);
        Assert.False(dupla.Pago, "a dupla herdou a janela de R$ 20 e seguiu marcada como paga");
    }

    // ── Inscrição nova ──

    [Fact]
    public async Task Inscricao_nova_sem_impedimento_que_junta_a_dele_herda_o_impedimento()
    {
        var (ctx, torneio, doDono, doCandidato, dono, candidato) = Cenario();
        using var _ = ctx;
        ctx.Duplas.Remove(doDono);
        await ctx.SaveChangesAsync();

        await Controller(ctx, dono.Id, Substitute.For<IPushNotificationService>()).Create(
            torneio.Id, doCandidato.CategoriaId,
            dono.Nome, dono.Cpf, null, null, null,
            candidato.Nome, candidato.Cpf, null, null, null,
            false, false, false, false,
            juntarComInscricaoSolo: true);

        var dupla = await ctx.Duplas.SingleAsync();
        Assert.Equal(candidato.Id, dupla.Jogador2Id);
        Assert.True(dupla.ImpedimentoSabadoManha, "a inscrição nova apagou o impedimento da sozinha");
        Assert.Equal(2 * Preco + Taxa, dupla.ValorInscricao);
    }

    [Fact]
    public async Task Inscricao_nova_com_outro_impedimento_fica_com_o_do_formulario_e_avisa()
    {
        var (ctx, torneio, doDono, doCandidato, dono, candidato) = Cenario();
        using var _ = ctx;
        ctx.Duplas.Remove(doDono);
        await ctx.SaveChangesAsync();
        var push = Substitute.For<IPushNotificationService>();

        await Controller(ctx, dono.Id, push).Create(
            torneio.Id, doCandidato.CategoriaId,
            dono.Nome, dono.Cpf, null, null, null,
            candidato.Nome, candidato.Cpf, null, null, null,
            false, false, false, impSabadoTarde: true,
            juntarComInscricaoSolo: true);

        var dupla = await ctx.Duplas.SingleAsync();
        Assert.True(dupla.ImpedimentoSabadoTarde);
        Assert.False(dupla.ImpedimentoSabadoManha);
        Assert.Equal(2 * Preco + Taxa, dupla.ValorInscricao);
        await AvisoDeQueNaoEntrou(push, candidato.Id);
    }
}
