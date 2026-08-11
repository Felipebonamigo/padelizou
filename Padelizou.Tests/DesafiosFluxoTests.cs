using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using padelizou.Models;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// O CAMINHO dos Desafios: a porta em construção, o convite que fecha a dupla, o desafio, o
// placar e o fechamento. As regras puras estão em DesafiosRegrasTests.
//
// ⚠️ O que se guarda aqui é o que teste de unidade não pega: a trava repetida em cada ação, o
// que fica GRAVADO no banco depois de cada POST, e o que morre (ou não) junto.
public class DesafiosFluxoTests
{
    private record Cenario(DbPadelContext Ctx, int[] Jogadores, int ClubeId, int CategoriaId);

    private static Cenario Montar(bool admin = false)
    {
        var ctx = TestInfra.NovoContexto();

        var jogadores = new List<Jogador>();
        for (int i = 1; i <= 4; i++)
        {
            var j = TestInfra.NovoJogador(i);
            // Só o primeiro é admin quando o teste pede: é ele que atravessa a porta fechada.
            if (i == 1 && admin) j.IsAdminGeral = true;
            jogadores.Add(j);
        }
        ctx.Jogadores.AddRange(jogadores);

        var clube = new Clube { Nome = "Golden Point" };
        var categoria = new CategoriaPadrao { Nome = "4ª Categoria", Codigo = "C4M", Tipo = "Masculina" };
        ctx.Clubes.Add(clube);
        ctx.CategoriasPadrao.Add(categoria);
        ctx.SaveChanges();

        return new Cenario(ctx, jogadores.Select(j => j.Id).ToArray(), clube.Id, categoria.Id);
    }

    // Um anúncio já fechado (com parceiro) e no mural.
    private static AnuncioDeDesafio Anunciar(DbPadelContext ctx, int j1, int j2, DateTime valeAte,
        int? categoriaId = null, int? clubeId = null)
    {
        var anuncio = new AnuncioDeDesafio
        {
            Jogador1Id = j1,
            Jogador2Id = j2,
            Status = AnuncioDeDesafio.Publicado,
            ValeAte = valeAte,
        };
        if (categoriaId != null)
            anuncio.Categorias.Add(new AnuncioDesafioCategoria { CategoriaPadraoId = categoriaId.Value });
        if (clubeId != null)
            anuncio.Clubes.Add(new AnuncioDesafioClube { ClubeId = clubeId.Value });

        ctx.AnunciosDeDesafio.Add(anuncio);
        ctx.SaveChanges();
        return anuncio;
    }

    // ── A porta ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Em_construcao_o_mural_nao_responde_pra_jogador_comum()
    {
        // ⚠️ Esconder o item do menu é cortesia. A trava é esta: digitar /Desafios na barra.
        var c = Montar();
        using var _ = c.Ctx;
        var controller = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0], habilitado: false);

        Assert.IsType<NotFoundResult>(await controller.Index(null, null));
        Assert.IsType<NotFoundResult>(await controller.Meus());
        Assert.IsType<NotFoundResult>(await controller.Publicar());
        Assert.IsType<NotFoundResult>(await controller.Responder(1, true));
        Assert.IsType<NotFoundResult>(await controller.LancarPlacar(1, null, null, 6, 4));
    }

    [Fact]
    public async Task Em_construcao_o_admin_entra()
    {
        var c = Montar(admin: true);
        using var _ = c.Ctx;
        var controller = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0], habilitado: false);

        Assert.IsType<ViewResult>(await controller.Index(null, null));
    }

    [Fact]
    public async Task Ligado_o_jogador_comum_entra()
    {
        var c = Montar();
        using var _ = c.Ctx;
        var controller = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0], habilitado: true);

        Assert.IsType<ViewResult>(await controller.Index(null, null));
    }

    // ── O convite do parceiro ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Anuncio_nasce_em_rascunho_e_nao_aparece_no_mural()
    {
        // ⚠️ A regra que protege o parceiro: sem o "sim" dele, ninguém vê o anúncio — e
        // ninguém marca jogo em nome dele.
        var c = Montar();
        using var _ = c.Ctx;
        var controller = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0]);

        await controller.Publicar(new[] { c.CategoriaId }, Array.Empty<int>(), new[] { c.ClubeId },
            "topamos jogar cedo", proximaSemana: false);

        var anuncio = await c.Ctx.AnunciosDeDesafio.SingleAsync();
        Assert.Equal(AnuncioDeDesafio.Rascunho, anuncio.Status);
        Assert.Null(anuncio.Jogador2Id);
        Assert.False(string.IsNullOrWhiteSpace(anuncio.ConviteToken));

        // E o mural de outra pessoa não o enxerga.
        var deOutro = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[2]);
        var vm = Assert.IsType<MuralVM>(Assert.IsType<ViewResult>(await deOutro.Index(null, null)).Model);
        Assert.Empty(vm.Cartoes);
    }

    [Fact]
    public async Task Parceiro_aceitando_o_convite_publica_o_anuncio_e_queima_o_link()
    {
        var c = Montar();
        using var _ = c.Ctx;
        var dono = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0]);
        await dono.Publicar(Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), null, false);

        var token = (await c.Ctx.AnunciosDeDesafio.SingleAsync()).ConviteToken!;

        var parceiro = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[1]);
        await parceiro.AceitarConvite(token);

        var anuncio = await c.Ctx.AnunciosDeDesafio.SingleAsync();
        Assert.Equal(AnuncioDeDesafio.Publicado, anuncio.Status);
        Assert.Equal(c.Jogadores[1], anuncio.Jogador2Id);
        // Link usado não fecha uma segunda dupla.
        Assert.Null(anuncio.ConviteToken);
    }

    [Fact]
    public async Task Quem_publicou_nao_pode_ser_o_proprio_parceiro()
    {
        var c = Montar();
        using var _ = c.Ctx;
        var dono = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0]);
        await dono.Publicar(Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), null, false);

        var token = (await c.Ctx.AnunciosDeDesafio.SingleAsync()).ConviteToken!;
        await dono.AceitarConvite(token);

        var anuncio = await c.Ctx.AnunciosDeDesafio.SingleAsync();
        Assert.Null(anuncio.Jogador2Id);
        Assert.Equal(AnuncioDeDesafio.Rascunho, anuncio.Status);
    }

    [Fact]
    public void Convite_de_anuncio_vencido_nao_vale()
    {
        var anuncio = new AnuncioDeDesafio
        {
            Jogador1Id = 1,
            ConviteToken = "abc",
            Status = AnuncioDeDesafio.Rascunho,
            ValeAte = new DateTime(2026, 8, 16, 23, 59, 59),
        };

        Assert.True(ConviteDoAnuncio.Valido(anuncio, "abc", new DateTime(2026, 8, 14)));
        Assert.False(ConviteDoAnuncio.Valido(anuncio, "abc", new DateTime(2026, 8, 18)));
        Assert.False(ConviteDoAnuncio.Valido(anuncio, "outro", new DateTime(2026, 8, 14)));
    }

    // ── Desafiar ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sem_anuncio_proprio_nao_da_pra_desafiar()
    {
        // A dupla do desafiante sai do anúncio DELE — é o que dá identidade sem inventar um
        // segundo fluxo de escolher parceiro.
        var c = Montar();
        using var _ = c.Ctx;
        var alvo = Anunciar(c.Ctx, c.Jogadores[2], c.Jogadores[3], DateTime.Now.AddDays(3));

        var controller = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0]);
        var resultado = Assert.IsType<RedirectToActionResult>(await controller.Desafiar(alvo.Id));

        Assert.Equal(nameof(DesafiosController.Publicar), resultado.ActionName);
        Assert.Empty(c.Ctx.Desafios);
    }

    [Fact]
    public async Task Desafio_fora_da_categoria_aceita_e_recusado_mesmo_vindo_pelo_POST()
    {
        // ⚠️ A lista veio da tela, e a tela é do navegador de quem envia. A regra é reconferida
        // no servidor — mesma disciplina do preço da quadra.
        var c = Montar();
        using var _ = c.Ctx;
        var outraCategoria = new CategoriaPadrao { Nome = "2ª Categoria", Codigo = "C2M", Tipo = "Masculina" };
        c.Ctx.CategoriasPadrao.Add(outraCategoria);
        c.Ctx.SaveChanges();

        var valeAte = DateTime.Now.AddDays(3);
        Anunciar(c.Ctx, c.Jogadores[0], c.Jogadores[1], valeAte, c.CategoriaId);
        var alvo = Anunciar(c.Ctx, c.Jogadores[2], c.Jogadores[3], valeAte, c.CategoriaId);

        var controller = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0]);
        await controller.Desafiar(alvo.Id, outraCategoria.Id, c.ClubeId,
            DateTime.Now.AddDays(1), FormatoDoDesafio.UmSet, null);

        Assert.Empty(c.Ctx.Desafios);
    }

    [Fact]
    public async Task Desafio_pra_depois_da_semana_anunciada_e_recusado()
    {
        var c = Montar();
        using var _ = c.Ctx;
        var valeAte = DateTime.Now.AddDays(3);
        Anunciar(c.Ctx, c.Jogadores[0], c.Jogadores[1], valeAte);
        var alvo = Anunciar(c.Ctx, c.Jogadores[2], c.Jogadores[3], valeAte);

        var controller = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0]);
        await controller.Desafiar(alvo.Id, c.CategoriaId, c.ClubeId,
            valeAte.AddDays(5), FormatoDoDesafio.UmSet, null);

        Assert.Empty(c.Ctx.Desafios);
    }

    [Fact]
    public async Task Desafio_avisa_a_dupla_desafiada_pelo_WhatsApp_e_ninguem_mais()
    {
        // ⚠️ É o ÚNICO aviso destes que vai pro WhatsApp: pessoal, morre em 48h e pede ação.
        // O resto é app. E não existe broadcast "nova dupla na sua cidade" em lugar nenhum.
        var c = Montar();
        using var _ = c.Ctx;
        var push = Substitute.For<IPushNotificationService>();
        var valeAte = DateTime.Now.AddDays(3);
        Anunciar(c.Ctx, c.Jogadores[0], c.Jogadores[1], valeAte);
        var alvo = Anunciar(c.Ctx, c.Jogadores[2], c.Jogadores[3], valeAte);

        var controller = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0], push: push);
        await controller.Desafiar(alvo.Id, c.CategoriaId, c.ClubeId,
            DateTime.Now.AddDays(1), FormatoDoDesafio.UmSet, null);

        Assert.Single(c.Ctx.Desafios);
        await push.Received(1).EnviarParaJogadorAsync(c.Jogadores[2], Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), AlcanceDoAviso.AppEWhatsApp);
        await push.Received(1).EnviarParaJogadorAsync(c.Jogadores[3], Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), AlcanceDoAviso.AppEWhatsApp);
        await push.DidNotReceive().EnviarParaJogadorAsync(c.Jogadores[0], Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // ── O placar ──────────────────────────────────────────────────────────────────────

    // Um desafio aceito, marcado pra `quando`.
    //
    // ⚠️ O desafio é SEMPRE criado com hora futura, e só depois a linha é empurrada pra
    // `quando`. É de propósito: a regra recusa proposta pra hora que já passou, e forçar isso
    // no POST faria o helper testar a recusa em vez de montar o cenário. Na vida real é o
    // relógio que anda — aqui, o cenário.
    private static async Task<Desafio> DesafioAceitoAsync(Cenario c, DateTime quando)
    {
        var valeAte = DateTime.Now.AddDays(3);
        Anunciar(c.Ctx, c.Jogadores[0], c.Jogadores[1], valeAte);
        var alvo = Anunciar(c.Ctx, c.Jogadores[2], c.Jogadores[3], valeAte);

        var desafiante = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0]);
        await desafiante.Desafiar(alvo.Id, c.CategoriaId, c.ClubeId,
            DateTime.Now.AddDays(1), FormatoDoDesafio.UmSet, null);

        var desafio = await c.Ctx.Desafios.SingleAsync();
        var desafiado = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[2]);
        await desafiado.Responder(desafio.Id, aceitar: true);

        desafio = await c.Ctx.Desafios.SingleAsync();
        desafio.DataHora = quando;
        await c.Ctx.SaveChangesAsync();

        return desafio;
    }

    [Fact]
    public async Task Placar_confirmado_congela_os_pontos_na_linha()
    {
        // ⚠️ Congelar é o que impede que mexer na fórmula em novembro reescreva o ranking de
        // agosto — mesmo padrão do PrecoPorJogoCotado.
        var c = Montar();
        using var _ = c.Ctx;
        var desafio = await DesafioAceitoAsync(c, DateTime.Now.AddMinutes(-30));

        var quemJogou = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0]);
        await quemJogou.LancarPlacar(desafio.Id, null, null, 6, 3);

        var oOutroLado = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[2]);
        await oOutroLado.ResponderPlacar(desafio.Id, confirmar: true, null);

        var fechado = await c.Ctx.Desafios.SingleAsync();
        Assert.Equal(Desafio.Confirmado, fechado.Status);
        // Ninguém tem Padelímetro no cenário: expectativa neutra, vitória de 10 + 1 de presença.
        Assert.Equal(11, fechado.PontosDesafiante);
        Assert.Equal(PontosDoDesafio.Presenca, fechado.PontosDesafiado);
        Assert.Equal(c.Jogadores[2], fechado.ConfirmadoPorId);
    }

    [Fact]
    public async Task Placar_contestado_nao_da_ponto_a_ninguem()
    {
        // Duas duplas discordando é problema humano — o sistema não escolhe lado.
        var c = Montar();
        using var _ = c.Ctx;
        var desafio = await DesafioAceitoAsync(c, DateTime.Now.AddMinutes(-30));

        var quemJogou = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0]);
        await quemJogou.LancarPlacar(desafio.Id, null, null, 6, 3);

        var oOutroLado = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[3]);
        await oOutroLado.ResponderPlacar(desafio.Id, confirmar: false, "foi 6x4 pra gente");

        var contestado = await c.Ctx.Desafios.SingleAsync();
        Assert.Equal(Desafio.EmDisputa, contestado.Status);
        Assert.Null(contestado.PontosDesafiante);
        Assert.Null(contestado.PontosDesafiado);
        Assert.Equal("foi 6x4 pra gente", contestado.MotivoDaDisputa);
    }

    [Fact]
    public async Task Placar_antes_da_hora_marcada_e_recusado()
    {
        var c = Montar();
        using var _ = c.Ctx;
        var desafio = await DesafioAceitoAsync(c, DateTime.Now.AddDays(1));

        var controller = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0]);
        await controller.LancarPlacar(desafio.Id, null, null, 6, 3);

        var intacto = await c.Ctx.Desafios.SingleAsync();
        Assert.Equal(Desafio.Aceito, intacto.Status);
        Assert.Null(intacto.GamesDesafiante);
    }

    [Fact]
    public async Task Estranho_nao_lanca_placar_de_jogo_alheio()
    {
        var c = Montar();
        using var _ = c.Ctx;
        var estranho = new Jogador { Nome = "Penetra", Cpf = "99988877766" };
        c.Ctx.Jogadores.Add(estranho);
        c.Ctx.SaveChanges();

        var desafio = await DesafioAceitoAsync(c, DateTime.Now.AddMinutes(-30));

        var controller = TestInfra.NovoDesafiosController(c.Ctx, estranho.Id);
        await controller.LancarPlacar(desafio.Id, null, null, 6, 0);

        Assert.Equal(Desafio.Aceito, (await c.Ctx.Desafios.SingleAsync()).Status);
    }

    [Fact]
    public async Task Sets_lancados_num_formato_que_nao_tem_sets_sao_ignorados()
    {
        // Um set gravado no "1 set" decidiria o vencedor por um campo que a tela nem mostrou.
        var c = Montar();
        using var _ = c.Ctx;
        var desafio = await DesafioAceitoAsync(c, DateTime.Now.AddMinutes(-30));

        var controller = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0]);
        await controller.LancarPlacar(desafio.Id, setsDesafiante: 0, setsDesafiado: 2,
            gamesDesafiante: 6, gamesDesafiado: 3);

        var lancado = await c.Ctx.Desafios.SingleAsync();
        Assert.Null(lancado.SetsDesafiante);
        Assert.Null(lancado.SetsDesafiado);
        Assert.Equal(1, EstadoDoDesafio.LadoVencedor(lancado));
    }

    // ── O anti-farm, no banco ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Terceiro_confronto_do_mes_contra_a_mesma_dupla_so_paga_a_presenca()
    {
        var c = Montar();
        using var _ = c.Ctx;
        var agora = new DateTime(2026, 8, 20, 21, 0, 0);

        // Dois confrontos já fechados no mês entre as MESMAS duas duplas — o segundo com os
        // lados trocados, que para o anti-farm é o mesmo jogo de novo.
        c.Ctx.Desafios.AddRange(
            Fechado(c, agora.AddDays(-10), invertido: false),
            Fechado(c, agora.AddDays(-3), invertido: true));
        c.Ctx.SaveChanges();

        var terceiro = new Desafio
        {
            DesafianteJogador1Id = c.Jogadores[0], DesafianteJogador2Id = c.Jogadores[1],
            DesafiadoJogador1Id = c.Jogadores[2], DesafiadoJogador2Id = c.Jogadores[3],
            CategoriaPadraoId = c.CategoriaId, ClubeId = c.ClubeId,
            Formato = FormatoDoDesafio.UmSet, Status = Desafio.PlacarLancado,
            DataHora = agora.AddHours(-2), PropostoEm = agora.AddDays(-1),
            GamesDesafiante = 6, GamesDesafiado = 2,
            LancadoPorId = c.Jogadores[0], LancadoEm = agora.AddHours(-1),
        };
        c.Ctx.Desafios.Add(terceiro);
        c.Ctx.SaveChanges();

        var fechamento = TestInfra.FechamentoDeDesafioDe(c.Ctx);
        Assert.Equal(2, await fechamento.ConfrontosAnterioresNoMesAsync(terceiro, agora));

        await fechamento.FecharAsync(terceiro, c.Jogadores[2], agora);

        // Venceu, mas a vitória já não paga: sobra a presença.
        Assert.Equal(PontosDoDesafio.Presenca, terceiro.PontosDesafiante);
        Assert.Equal(PontosDoDesafio.Presenca, terceiro.PontosDesafiado);
    }

    [Fact]
    public async Task Confronto_do_mes_passado_nao_conta_no_desconto()
    {
        var c = Montar();
        using var _ = c.Ctx;
        var agora = new DateTime(2026, 8, 5, 21, 0, 0);

        c.Ctx.Desafios.Add(Fechado(c, new DateTime(2026, 7, 28, 20, 0, 0), invertido: false));
        c.Ctx.SaveChanges();

        var novo = new Desafio
        {
            DesafianteJogador1Id = c.Jogadores[0], DesafianteJogador2Id = c.Jogadores[1],
            DesafiadoJogador1Id = c.Jogadores[2], DesafiadoJogador2Id = c.Jogadores[3],
            CategoriaPadraoId = c.CategoriaId, ClubeId = c.ClubeId,
            Formato = FormatoDoDesafio.UmSet, Status = Desafio.PlacarLancado,
            DataHora = agora.AddHours(-2), PropostoEm = agora.AddDays(-1),
            GamesDesafiante = 6, GamesDesafiado = 2,
        };
        c.Ctx.Desafios.Add(novo);
        c.Ctx.SaveChanges();

        var fechamento = TestInfra.FechamentoDeDesafioDe(c.Ctx);
        Assert.Equal(0, await fechamento.ConfrontosAnterioresNoMesAsync(novo, agora));
    }

    private static Desafio Fechado(Cenario c, DateTime quando, bool invertido) => new()
    {
        DesafianteJogador1Id = invertido ? c.Jogadores[2] : c.Jogadores[0],
        DesafianteJogador2Id = invertido ? c.Jogadores[3] : c.Jogadores[1],
        DesafiadoJogador1Id = invertido ? c.Jogadores[0] : c.Jogadores[2],
        DesafiadoJogador2Id = invertido ? c.Jogadores[1] : c.Jogadores[3],
        CategoriaPadraoId = c.CategoriaId,
        ClubeId = c.ClubeId,
        Formato = FormatoDoDesafio.UmSet,
        Status = Desafio.Confirmado,
        DataHora = quando,
        PropostoEm = quando.AddDays(-1),
        GamesDesafiante = 6,
        GamesDesafiado = 4,
        ConfirmadoEm = quando.AddHours(1),
        PontosDesafiante = 11,
        PontosDesafiado = 1,
    };

    // ── O que NÃO morre junto ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancelar_o_anuncio_nao_desmarca_o_jogo_ja_aceito()
    {
        // ⚠️ A pergunta das 8 inscrições que sumiram: "o que mais morre junto?". Aqui, nada —
        // um jogo aceito é compromisso entre quatro pessoas e sobrevive ao anúncio.
        var c = Montar();
        using var _ = c.Ctx;
        var desafio = await DesafioAceitoAsync(c, DateTime.Now.AddDays(1));

        var meuAnuncio = await c.Ctx.AnunciosDeDesafio
            .FirstAsync(a => a.Jogador1Id == c.Jogadores[0]);

        var controller = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0]);
        await controller.CancelarAnuncio(meuAnuncio.Id);

        Assert.Equal(AnuncioDeDesafio.Cancelado,
            (await c.Ctx.AnunciosDeDesafio.FindAsync(meuAnuncio.Id))!.Status);

        var aindaDePe = await c.Ctx.Desafios.SingleAsync();
        Assert.Equal(Desafio.Aceito, aindaDePe.Status);
        Assert.Equal(desafio.DataHora, aindaDePe.DataHora);
    }

    [Fact]
    public async Task Anuncio_de_outra_pessoa_nao_pode_ser_cancelado()
    {
        var c = Montar();
        using var _ = c.Ctx;
        var alheio = Anunciar(c.Ctx, c.Jogadores[2], c.Jogadores[3], DateTime.Now.AddDays(3));

        var controller = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0]);
        Assert.IsType<ForbidResult>(await controller.CancelarAnuncio(alheio.Id));

        Assert.Equal(AnuncioDeDesafio.Publicado,
            (await c.Ctx.AnunciosDeDesafio.FindAsync(alheio.Id))!.Status);
    }

    // ── O mural ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Filtro_de_categoria_mantem_quem_aceita_qualquer_uma()
    {
        // ⚠️ Lista vazia = "aceita qualquer categoria". Um filtro cru esconderia justamente as
        // duplas mais fáceis de desafiar.
        var c = Montar();
        using var _ = c.Ctx;
        var outra = new CategoriaPadrao { Nome = "2ª Categoria", Codigo = "C2M", Tipo = "Masculina" };
        c.Ctx.CategoriasPadrao.Add(outra);
        c.Ctx.SaveChanges();

        var valeAte = DateTime.Now.AddDays(3);
        Anunciar(c.Ctx, c.Jogadores[0], c.Jogadores[1], valeAte);                      // sem restrição
        Anunciar(c.Ctx, c.Jogadores[2], c.Jogadores[3], valeAte, categoriaId: outra.Id);

        var estranho = new Jogador { Nome = "Visitante", Cpf = "99911122233" };
        c.Ctx.Jogadores.Add(estranho);
        c.Ctx.SaveChanges();

        var controller = TestInfra.NovoDesafiosController(c.Ctx, estranho.Id);
        var vm = Assert.IsType<MuralVM>(
            Assert.IsType<ViewResult>(await controller.Index(c.CategoriaId, null)).Model);

        // Só a dupla sem restrição — a outra só aceita a categoria que não foi filtrada.
        var unico = Assert.Single(vm.Cartoes);
        Assert.Equal(c.Jogadores[0], unico.Anuncio.Jogador1Id);
        Assert.Equal("Qualquer categoria", unico.CategoriasNaTela);
    }

    [Fact]
    public async Task Anuncio_vencido_some_do_mural_sem_ninguem_gravar_nada()
    {
        var c = Montar();
        using var _ = c.Ctx;
        var vencido = Anunciar(c.Ctx, c.Jogadores[2], c.Jogadores[3], DateTime.Now.AddHours(-1));

        var controller = TestInfra.NovoDesafiosController(c.Ctx, c.Jogadores[0]);
        var vm = Assert.IsType<MuralVM>(
            Assert.IsType<ViewResult>(await controller.Index(null, null)).Model);

        Assert.Empty(vm.Cartoes);
        Assert.Equal(AnuncioDeDesafio.Publicado,
            (await c.Ctx.AnunciosDeDesafio.FindAsync(vencido.Id))!.Status);
    }
}
