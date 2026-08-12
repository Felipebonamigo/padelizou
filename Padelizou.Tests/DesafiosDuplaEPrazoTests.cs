using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using padelizou.Models;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// As duas mudanças de 12/08/2026 (pedido do Felipe):
//
//  1. "DEIXAR ATÉ ALGUÉM ACEITAR" — o anúncio pode não ter prazo. Quem o tira do mural é o
//     aceite de um desafio, não o relógio.
//  2. ESCOLHER O PARCEIRO SEM PEDIR LICENÇA — a dupla nasce fechada e no mural; o parceiro é
//     avisado e pode sair.
//
// ⚠️ A segunda inverte a regra que a fase 1 defendia ("o anúncio precisa dos dois"). O que
// segura a nova é o par AVISO + SAÍDA: sem o aviso chegando, "poder sair" não existe na prática.
// Estes testes guardam esse par.
public class DesafiosDuplaEPrazoTests
{
    private record Cenario(DbPadelContext Ctx, int[] Jogadores, int ClubeId, int CategoriaId,
        IPushNotificationService Push);

    private static Cenario Montar()
    {
        var ctx = TestInfra.NovoContexto();

        var jogadores = new List<Jogador>();
        for (int i = 1; i <= 4; i++) jogadores.Add(TestInfra.NovoJogador(i));
        ctx.Jogadores.AddRange(jogadores);

        var clube = new Clube { Nome = "Golden Point" };
        var categoria = new CategoriaPadrao { Nome = "4ª Categoria", Codigo = "C4M", Tipo = "Masculina" };
        ctx.Clubes.Add(clube);
        ctx.CategoriasPadrao.Add(categoria);
        ctx.SaveChanges();

        return new Cenario(ctx, jogadores.Select(j => j.Id).ToArray(), clube.Id, categoria.Id,
            Substitute.For<IPushNotificationService>());
    }

    // ── 1. O anúncio sem prazo ────────────────────────────────────────────────────────

    [Fact]
    public void Anuncio_sem_prazo_nao_vence_nunca()
    {
        var anuncio = new AnuncioDeDesafio
        {
            Jogador1Id = 1, Jogador2Id = 2,
            Status = AnuncioDeDesafio.Publicado,
            ValeAte = null,
        };

        Assert.True(SemanaDoDesafio.SemPrazo(anuncio));
        Assert.False(SemanaDoDesafio.Vencido(anuncio, new DateTime(2027, 1, 1)));
        Assert.True(SemanaDoDesafio.NoMural(anuncio, new DateTime(2027, 1, 1)));
        Assert.Equal("até alguém aceitar", SemanaDoDesafio.RotuloDoPrazo(anuncio.ValeAte));
    }

    [Fact]
    public void Sem_prazo_dos_dois_lados_o_jogo_ainda_tem_teto()
    {
        // ⚠️ "Aceito desafio quando aparecer" não é "aceito jogar em abril". Sem teto, o campo
        // de data aceitaria qualquer futuro — e um jogo marcado pra daqui a oito meses fica
        // ocupando "Meus desafios" até lá.
        var agora = new DateTime(2026, 8, 12, 10, 0, 0);
        var a = new AnuncioDeDesafio { ValeAte = null };
        var b = new AnuncioDeDesafio { ValeAte = null };

        Assert.Equal(agora + SemanaDoDesafio.TetoSemPrazo, SemanaDoDesafio.LimiteParaJogar(a, b, agora));
    }

    [Fact]
    public void Com_um_lado_datado_manda_a_data_dele_e_nao_o_teto()
    {
        var agora = new DateTime(2026, 8, 12, 10, 0, 0);
        var domingo = new DateTime(2026, 8, 16, 23, 59, 59);

        var semPrazo = new AnuncioDeDesafio { ValeAte = null };
        var daSemana = new AnuncioDeDesafio { ValeAte = domingo };

        Assert.Equal(domingo, SemanaDoDesafio.LimiteParaJogar(semPrazo, daSemana, agora));
        Assert.Equal(domingo, SemanaDoDesafio.LimiteParaJogar(daSemana, semPrazo, agora));
    }

    [Fact]
    public async Task Aceitar_um_desafio_tira_do_mural_o_anuncio_sem_prazo_e_so_o_do_desafiado()
    {
        var c = Montar();
        using var _ = c.Ctx;

        // Os dois anunciam SEM prazo.
        var meu = await PublicarComParceiroAsync(c, c.Jogadores[0], c.Jogadores[1], PrazoDoAnuncio.AteAlguemAceitar);
        var alvo = await PublicarComParceiroAsync(c, c.Jogadores[2], c.Jogadores[3], PrazoDoAnuncio.AteAlguemAceitar);

        var desafiante = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0], push: c.Push);
        await desafiante.Desafiar(alvo.Id, c.CategoriaId, c.ClubeId,
            DateTime.Now.AddDays(2), FormatoDoDesafio.UmSet, null);

        var desafio = await c.Ctx.Desafios.SingleAsync();
        var desafiado = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[2], push: c.Push);
        await desafiado.Responder(desafio.Id, aceitar: true);

        // Quem aceitou achou jogo: o anúncio dele fecha.
        Assert.Equal(AnuncioDeDesafio.Fechado,
            (await c.Ctx.AnunciosDeDesafio.FindAsync(alvo.Id))!.Status);

        // O de quem DESAFIOU continua: a condição que ele escreveu ("até alguém aceitar o meu")
        // não aconteceu.
        Assert.Equal(AnuncioDeDesafio.Publicado,
            (await c.Ctx.AnunciosDeDesafio.FindAsync(meu.Id))!.Status);
    }

    [Fact]
    public async Task Anuncio_com_data_nao_fecha_ao_aceitar_porque_a_dupla_quis_a_semana_toda()
    {
        var c = Montar();
        using var _ = c.Ctx;

        await PublicarComParceiroAsync(c, c.Jogadores[0], c.Jogadores[1], PrazoDoAnuncio.EstaSemana);
        var alvo = await PublicarComParceiroAsync(c, c.Jogadores[2], c.Jogadores[3], PrazoDoAnuncio.EstaSemana);

        var desafiante = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0], push: c.Push);
        await desafiante.Desafiar(alvo.Id, c.CategoriaId, c.ClubeId,
            DateTime.Now.AddHours(6), FormatoDoDesafio.UmSet, null);

        var desafio = await c.Ctx.Desafios.SingleAsync();
        await TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[2], push: c.Push)
            .Responder(desafio.Id, aceitar: true);

        Assert.Equal(AnuncioDeDesafio.Publicado,
            (await c.Ctx.AnunciosDeDesafio.FindAsync(alvo.Id))!.Status);
    }

    [Fact]
    public async Task Prazo_desconhecido_cai_na_semana_atual_em_vez_de_virar_anuncio_eterno()
    {
        // Formulário antigo em cache ou dedo no teclado não pode gerar um anúncio que nunca sai
        // do mural — o padrão de menor surpresa é o que vence sozinho no domingo.
        var c = Montar();
        using var _ = c.Ctx;

        var controller = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0], push: c.Push);
        await controller.Publicar(Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(),
            null, "qualquer-coisa", parceiroId: null);

        var anuncio = await c.Ctx.AnunciosDeDesafio.SingleAsync();
        Assert.NotNull(anuncio.ValeAte);
        Assert.Equal(SemanaDoDesafio.FimDaSemanaDe(DateTime.Now).Date, anuncio.ValeAte!.Value.Date);
    }

    // ── 2. O parceiro escolhido sem pedir licença ─────────────────────────────────────

    private static async Task<AnuncioDeDesafio> PublicarComParceiroAsync(
        Cenario c, int quem, int parceiro, string prazo)
    {
        var controller = TestInfra.NovoDesafiosController(c.Ctx, quem, push: c.Push);
        await controller.Publicar(Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(),
            null, prazo, parceiroId: parceiro);

        return await c.Ctx.AnunciosDeDesafio.OrderByDescending(a => a.Id).FirstAsync();
    }

    [Fact]
    public async Task Com_parceiro_escolhido_o_desafio_ja_nasce_no_mural()
    {
        var c = Montar();
        using var _ = c.Ctx;

        var anuncio = await PublicarComParceiroAsync(c, c.Jogadores[0], c.Jogadores[1], PrazoDoAnuncio.EstaSemana);

        Assert.Equal(AnuncioDeDesafio.Publicado, anuncio.Status);
        Assert.Equal(c.Jogadores[1], anuncio.Jogador2Id);
        // Ninguém pediu licença: fica registrado que ele ainda não se manifestou.
        Assert.Null(anuncio.ParceiroConfirmouEm);
        // E não há link pendente — a dupla já está fechada.
        Assert.Null(anuncio.ConviteToken);
        Assert.True(SemanaDoDesafio.NoMural(anuncio, DateTime.Now));
    }

    [Fact]
    public async Task O_parceiro_e_avisado_na_hora_e_pelo_WhatsApp()
    {
        // ⚠️ É ESTE AVISO QUE SUSTENTA A REGRA. O anúncio é público e diz onde a pessoa vai
        // estar; descobrir isso por acaso, dias depois, seria a mesma coisa que não poder sair.
        var c = Montar();
        using var _ = c.Ctx;

        await PublicarComParceiroAsync(c, c.Jogadores[0], c.Jogadores[1], PrazoDoAnuncio.EstaSemana);

        await c.Push.Received(1).EnviarParaJogadorAsync(c.Jogadores[1],
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), AlcanceDoAviso.AppEWhatsApp);
    }

    [Fact]
    public async Task Quem_desligou_os_convites_de_jogo_nao_pode_ser_escolhido()
    {
        // O mesmo interruptor que já filtra o convite do Marcar Jogo e do Grupo. Quem o desligou
        // fechou essa porta — e escolher essa pessoa direto seria entrar por ela assim mesmo.
        var c = Montar();
        using var _ = c.Ctx;

        var fechado = await c.Ctx.Jogadores.FindAsync(c.Jogadores[1]);
        fechado!.AceitaConvitesJogo = false;
        await c.Ctx.SaveChangesAsync();

        var controller = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0], push: c.Push);
        var resultado = await controller.Publicar(Array.Empty<int>(), Array.Empty<int>(),
            Array.Empty<int>(), null, PrazoDoAnuncio.EstaSemana, parceiroId: c.Jogadores[1]);

        Assert.Empty(c.Ctx.AnunciosDeDesafio);
        Assert.Equal(nameof(DesafiosController.Publicar),
            Assert.IsType<RedirectToActionResult>(resultado).ActionName);

        // E ele nem aparece na lista de escolha.
        var form = Assert.IsType<PublicarAnuncioVM>(
            Assert.IsType<ViewResult>(await controller.Publicar()).Model);
        Assert.DoesNotContain(form.ParceirosPossiveis, p => p.Id == c.Jogadores[1]);
    }

    [Fact]
    public async Task Ninguem_faz_dupla_consigo_mesmo()
    {
        var c = Montar();
        using var _ = c.Ctx;

        var controller = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0], push: c.Push);
        await controller.Publicar(Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(),
            null, PrazoDoAnuncio.EstaSemana, parceiroId: c.Jogadores[0]);

        Assert.Empty(c.Ctx.AnunciosDeDesafio);
    }

    [Fact]
    public async Task O_parceiro_incluido_sai_da_dupla_e_o_anuncio_some_do_mural()
    {
        // A outra metade da regra: se ele pode ser posto sem pedir licença, tem que poder sair
        // sozinho — e quem publicou precisa saber que saiu.
        var c = Montar();
        using var _ = c.Ctx;

        var anuncio = await PublicarComParceiroAsync(c, c.Jogadores[0], c.Jogadores[1], PrazoDoAnuncio.EstaSemana);

        var parceiro = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[1], push: c.Push);
        await parceiro.CancelarAnuncio(anuncio.Id);

        var depois = await c.Ctx.AnunciosDeDesafio.FindAsync(anuncio.Id);
        Assert.Equal(AnuncioDeDesafio.Cancelado, depois!.Status);
        Assert.False(SemanaDoDesafio.NoMural(depois, DateTime.Now));

        // Quem publicou é avisado — senão ficaria esperando desafio de um anúncio que não existe.
        await c.Push.Received(1).EnviarParaJogadorAsync(c.Jogadores[0],
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), AlcanceDoAviso.SoApp);
    }

    [Fact]
    public async Task O_parceiro_incluido_pode_confirmar_e_a_cobranca_para()
    {
        var c = Montar();
        using var _ = c.Ctx;

        var anuncio = await PublicarComParceiroAsync(c, c.Jogadores[0], c.Jogadores[1], PrazoDoAnuncio.EstaSemana);

        var parceiro = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[1], push: c.Push);
        await parceiro.ConfirmarParceria(anuncio.Id);

        Assert.NotNull((await c.Ctx.AnunciosDeDesafio.FindAsync(anuncio.Id))!.ParceiroConfirmouEm);
    }

    [Fact]
    public async Task Estranho_nao_confirma_dupla_alheia_e_nem_confirma_duas_vezes()
    {
        var c = Montar();
        using var _ = c.Ctx;

        var anuncio = await PublicarComParceiroAsync(c, c.Jogadores[0], c.Jogadores[1], PrazoDoAnuncio.EstaSemana);

        // Nem quem criou, nem um estranho.
        Assert.IsType<ForbidResult>(await TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0], push: c.Push)
            .ConfirmarParceria(anuncio.Id));
        Assert.IsType<ForbidResult>(await TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[2], push: c.Push)
            .ConfirmarParceria(anuncio.Id));

        var parceiro = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[1], push: c.Push);
        await parceiro.ConfirmarParceria(anuncio.Id);
        Assert.IsType<ForbidResult>(await parceiro.ConfirmarParceria(anuncio.Id));
    }

    [Fact]
    public async Task Sem_parceiro_escolhido_o_caminho_do_link_continua_igual()
    {
        // A regra nova não pode ter matado a antiga: quem não sabe com quem vai jogar ainda
        // publica e manda o link.
        var c = Montar();
        using var _ = c.Ctx;

        var controller = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0], push: c.Push);
        await controller.Publicar(Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(),
            null, PrazoDoAnuncio.EstaSemana, parceiroId: null);

        var anuncio = await c.Ctx.AnunciosDeDesafio.SingleAsync();
        Assert.Equal(AnuncioDeDesafio.Rascunho, anuncio.Status);
        Assert.Null(anuncio.Jogador2Id);
        Assert.False(string.IsNullOrWhiteSpace(anuncio.ConviteToken));

        // E quem aceita pelo link entra como CONFIRMADO — diferente de quem foi escolhido.
        await TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[1], push: c.Push)
            .AceitarConvite(anuncio.ConviteToken!);

        var depois = await c.Ctx.AnunciosDeDesafio.SingleAsync();
        Assert.Equal(AnuncioDeDesafio.Publicado, depois.Status);
        Assert.NotNull(depois.ParceiroConfirmouEm);
    }

    [Fact]
    public async Task Anuncio_fechado_nao_aceita_mais_convite_pelo_link()
    {
        var c = Montar();
        using var _ = c.Ctx;

        var anuncio = new AnuncioDeDesafio
        {
            Jogador1Id = c.Jogadores[0],
            ConviteToken = "abc",
            Status = AnuncioDeDesafio.Fechado,
            ValeAte = null,
        };

        Assert.False(ConviteDoAnuncio.Valido(anuncio, "abc", DateTime.Now));
        Assert.Contains("já marcou", ConviteDoAnuncio.MotivoDeNaoValer(anuncio, DateTime.Now));
        await Task.CompletedTask;
    }
}
