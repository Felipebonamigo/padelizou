using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// RECOLHER as chaves: a aresta que faltava, `Fase de Grupos` → `Chaves em Aprovação`.
//
// 🗣️ Felipe, 10/09/2026: *"permita recolocar o torneio em fase fechada, ou já tem isso?"*, com o
// Painel de Controle do 2ª Etapa ER Padel Tour aberto em "FASE DE GRUPOS".
//
// Não tinha. O status só andava pra frente depois da aprovação: `DesfazerSorteio` fecha no
// instante em que se aprova (e APAGA grupos e jogos), `ReabrirInscricoes` recusa assim que
// existe partida, e `AlternarVisibilidade` esconde da listagem mas deixa quem já está inscrito
// vendo a página (é o desenho de VisibilidadeDoTorneio).
//
// ⚠️ O ENCANAMENTO JÁ EXISTIA: `AprovacaoDeChaves.Publicada` é um predicado só, que a agenda/ICS,
// a Home, o push de quadra atrasada e as abas já consultam. Virar o status de volta re-esconde
// tudo sozinho — o que faltava era só a transição.
public class RecolherChavesTests
{
    // ── A TRANSIÇÃO ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Recolher_volta_pra_aprovacao_sem_apagar_grupo_nem_jogo()
    {
        // A diferença inteira pro DesfazerSorteio: lá o sorteio some, aqui ele fica. Recolher é
        // esconder pra conferir, não refazer.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);
        await controller.AprovarChaves(torneio.Id);

        var jogosAntes = await ctx.Partidas.CountAsync(p => p.TorneioId == torneio.Id);
        // Duplas AINDA amarradas ao grupo: é isto que o DesfazerSorteio zera (ele solta a dupla
        // antes de apagar o grupo, por causa da FK). Se sobreviver, o sorteio ficou de pé.
        var emGrupoAntes = await ctx.Duplas.CountAsync(
            d => d.Categoria.TorneioId == torneio.Id && d.GrupoTorneioId != null);
        Assert.True(jogosAntes > 0, "O sorteio precisa ter gerado jogos pro teste valer alguma coisa.");
        Assert.True(emGrupoAntes > 0, "O sorteio precisa ter posto dupla em grupo pro teste valer alguma coisa.");

        await controller.RecolherChaves(torneio.Id);

        Assert.Equal(AprovacaoDeChaves.Pendente, (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
        Assert.Equal(jogosAntes, await ctx.Partidas.CountAsync(p => p.TorneioId == torneio.Id));
        Assert.Equal(emGrupoAntes, await ctx.Duplas.CountAsync(
            d => d.Categoria.TorneioId == torneio.Id && d.GrupoTorneioId != null));
    }

    [Fact]
    public async Task Recolher_esconde_os_jogos_do_jogador_de_novo()
    {
        // O que importa pro jogador não é o nome do status: é o predicado que a agenda, a Home e
        // o push consultam. Se ele não voltar a recusar, "recolher" não escondeu nada.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);
        await controller.AprovarChaves(torneio.Id);

        Assert.True(await PublicadasAsync(ctx, torneio.Id) > 0,
            "Depois de aprovar, os jogos precisam estar visíveis — senão o teste do recolher não prova nada.");

        await controller.RecolherChaves(torneio.Id);

        Assert.Equal(0, await PublicadasAsync(ctx, torneio.Id));
    }

    [Fact]
    public async Task Recolher_recusa_quando_a_bola_ja_rolou()
    {
        // A única recusa do desenho. Esconder um torneio EM ANDAMENTO não é preferência do
        // organizador — é jogador no clube sem ver contra quem joga. A régua é a mesma que o
        // mural usa pra saber quem já entrou em quadra: "Finalizada" OU com horário real.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);
        await controller.AprovarChaves(torneio.Id);

        var jogo = await ctx.Partidas.FirstAsync(p => p.TorneioId == torneio.Id);
        jogo.HorarioInicioReal = new DateTime(2026, 7, 1, 9, 5, 0);
        await ctx.SaveChangesAsync();

        await controller.RecolherChaves(torneio.Id);

        Assert.Equal("Fase de Grupos", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task So_organizador_ou_admin_recolhe()
    {
        // Regra 0 do CLAUDE.md: ação que grava dado precisa da checagem de dono. O gate mecânico
        // dos POSTs cobre o [Authorize]; a checagem de organizador é trabalho deste teste.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var intruso = new Jogador { Nome = "Intruso", Cpf = "99900000088" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var doOrganizador = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doOrganizador.GerarChaves(torneio.Id);
        await doOrganizador.AprovarChaves(torneio.Id);

        var resultado = await TestInfra.NovoTorneiosController(ctx, intruso.Id).RecolherChaves(torneio.Id);

        Assert.IsType<Microsoft.AspNetCore.Mvc.ForbidResult>(resultado);
        Assert.Equal("Fase de Grupos", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
    }

    [Fact]
    public async Task Nao_da_pra_recolher_torneio_que_nao_esta_publicado()
    {
        // Chave ainda esperando aprovação já está escondida: recolher de novo não significa nada,
        // e deixar passar mudaria o status por baixo de quem não pediu.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        await controller.RecolherChaves(torneio.Id);

        Assert.Equal(AprovacaoDeChaves.Pendente, (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    [Fact]
    public async Task Americano_nao_recolhe_porque_nunca_passou_pela_aprovacao()
    {
        // O Americano vai DIRETO pra "Fase de Grupos" (GerarRodadasAmericano) — não existe
        // aprovação dele pra recolher, e `PublicacaoDaChave.SaiPublicaNaHora` é a régua que já
        // diz isso na tela do sorteio.
        //
        // ⚠️ E deixar passar teria custo concreto, não teórico: em "Chaves em Aprovação" o painel
        // mostra o "Desfazer e Sortear de Novo" do formato Padrão, e o `DesfazerSorteio` apaga
        // GruposTorneio e Partidas sem tratar a Dupla EFÊMERA do Americano individual — que é
        // exatamente a diferença que o `DesfazerRodadasAmericano` existe pra respeitar.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        torneio.Formato = "Americano";
        torneio.Status = "Fase de Grupos";
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.RecolherChaves(torneio.Id);

        Assert.Equal("Fase de Grupos", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
        Assert.NotNull(controller.TempData["Erro"]);
    }

    // ── O CARIMBO E A CAIXINHA ───────────────────────────────────────────────────────────

    [Fact]
    public async Task A_primeira_aprovacao_carimba_que_avisou()
    {
        // "Prazo se lê do relógio, mas ENVIO se carimba" — a mesma lição do AvisoDeTorneioNovoEm.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        Assert.Null((await ctx.Torneios.FindAsync(torneio.Id))!.ChavesAvisadasEm);

        await controller.AprovarChaves(torneio.Id);

        Assert.NotNull((await ctx.Torneios.FindAsync(torneio.Id))!.ChavesAvisadasEm);
    }

    [Fact]
    public async Task Recolher_nao_apaga_o_carimbo()
    {
        // É exatamente o que ele tem que lembrar: apagar aqui faria a re-aprovação achar que
        // nunca avisou, e a caixinha nasceria marcada — a segunda rajada que isto existe pra evitar.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);
        await controller.AprovarChaves(torneio.Id);
        var carimbo = (await ctx.Torneios.FindAsync(torneio.Id))!.ChavesAvisadasEm;

        await controller.RecolherChaves(torneio.Id);

        Assert.Equal(carimbo, (await ctx.Torneios.FindAsync(torneio.Id))!.ChavesAvisadasEm);
    }

    [Fact]
    public async Task Reaprovar_sem_marcar_a_caixa_nao_manda_push_de_novo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var push = Substitute.For<IPushNotificationService>();
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id, push: push);
        await controller.GerarChaves(torneio.Id);
        await controller.AprovarChaves(torneio.Id);
        await controller.RecolherChaves(torneio.Id);
        push.ClearReceivedCalls();

        await controller.AprovarChaves(torneio.Id, avisarJogadores: false);

        Assert.Equal("Fase de Grupos", (await ctx.Torneios.FindAsync(torneio.Id))!.Status);
        await push.DidNotReceive().EnviarParaJogadorAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Reaprovar_marcando_a_caixa_manda_push()
    {
        // A saída continua aberta: às vezes os horários mudaram de verdade entre o recolher e o
        // aprovar, e aí avisar de novo é o certo. Quem decide é o organizador.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var push = Substitute.For<IPushNotificationService>();
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id, push: push);
        await controller.GerarChaves(torneio.Id);
        await controller.AprovarChaves(torneio.Id);
        await controller.RecolherChaves(torneio.Id);
        push.ClearReceivedCalls();

        await controller.AprovarChaves(torneio.Id, avisarJogadores: true);

        await push.Received().EnviarParaJogadorAsync(
            Arg.Any<int>(), "Chaves do Torneio de Teste saíram!", Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Sem_dizer_nada_avisa_so_na_primeira_vez()
    {
        // O default do PARÂMETRO, pra quem chamar sem a caixinha (POST feito à mão, formulário
        // velho em aba aberta). Mesmo raciocínio do `refazerHorarios: true` do TrocarDuplasDeGrupo:
        // o default é o comportamento seguro, e aqui o seguro é não repetir a rajada.
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var push = Substitute.For<IPushNotificationService>();
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id, push: push);
        await controller.GerarChaves(torneio.Id);

        await controller.AprovarChaves(torneio.Id);
        await push.Received().EnviarParaJogadorAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());

        await controller.RecolherChaves(torneio.Id);
        push.ClearReceivedCalls();

        await controller.AprovarChaves(torneio.Id);

        await push.DidNotReceive().EnviarParaJogadorAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // ── A TELA ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void O_painel_de_controle_tem_o_botao_de_recolher()
    {
        // Teste de FONTE: a suíte não renderiza Razor. Ele trava o `asp-action` do formulário —
        // sem isto o botão pode sumir num refactor da view e nenhum teste de C# reclamaria.
        var fonte = Details();

        Assert.Contains("asp-action=\"RecolherChaves\"", fonte, StringComparison.Ordinal);
    }

    [Fact]
    public void A_caixinha_de_avisar_nasce_desmarcada_quando_ja_avisou()
    {
        // O default da tela sai do carimbo, e é o oposto do default do HTML: `checked` só quando
        // `ChavesAvisadasEm == null`. Uma caixinha sempre marcada mandaria a segunda rajada no
        // primeiro Enter distraído.
        var fonte = Details();

        Assert.Contains("name=\"avisarJogadores\"", fonte, StringComparison.Ordinal);
        Assert.Contains("Model.ChavesAvisadasEm == null", fonte, StringComparison.Ordinal);
    }

    // ── infra ────────────────────────────────────────────────────────────────────────────

    // Quantos jogos deste torneio o predicado público deixa passar.
    private static async Task<int> PublicadasAsync(DbPadelContext ctx, int torneioId) =>
        await ctx.Partidas
            .Include(p => p.Categoria)
                .ThenInclude(c => c.Torneio)
            .Where(p => p.Categoria.TorneioId == torneioId)
            .Where(AprovacaoDeChaves.Publicada)
            .CountAsync();

    private static string Details() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto Padelizou a partir de " + AppContext.BaseDirectory);
    }
}
