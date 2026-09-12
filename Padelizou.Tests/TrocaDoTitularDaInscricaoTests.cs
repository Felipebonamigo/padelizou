using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace Padelizou.Tests;

// TIRAR O TITULAR (Jogador1) DA INSCRIÇÃO, E NÃO SÓ O SEGUNDO NOME.
//
// 🗣️ Felipe, 12/09/2026, no card de uma dupla paga: *"precisamos mudar o alexandre nesse caso
// de agora"* — o Alexandre é o `Jogador1`, e `TrocarParceiro` escrevia UMA linha só,
// `dupla.Jogador2Id = novo.Id`. Trocar o titular não tinha caminho nenhum: sobrava remover a
// inscrição e refazê-la, perdendo vaga na chave, lugar na grade e o pagamento já marcado — a
// mesma perda que a janela larga do organizador (10/09) tinha acabado de eliminar pro OUTRO
// dos dois nomes.
//
// ⚠️ NENHUMA RÉGUA DE AUTORIZAÇÃO NOVA, e isso é decisão, não economia. 🗣️ Felipe: *"antes de
// fechar as chaves organizador e os dois da dupla, depois que fechar, só o organizador"* — que
// é LETRA POR LETRA o par que `TrocarParceiro` já aplicava: o jogador preso em
// "Inscrições Abertas", o organizador andando pela grade (`MotivoParaOrganizadorNaoTrocar`).
// Tirar o titular entra na janela que já existe; uma terceira régua seria uma quarta cópia pra
// dessincronizar (ver a Mesa de Controle em 31/07).
//
// ⚠️ QUEM FICA VIRA O TITULAR. Sair do Alexandre não promove o nome NOVO ao lugar dele: o
// Tiago sobe pra `Jogador1` e o novo entra como `Jogador2`. Assim `Jogador1` continua querendo
// dizer alguma coisa — o mais antigo dos dois na inscrição — em vez de virar mera posição, e a
// escrita daqui pra baixo segue sendo a troca do segundo nome de sempre.
public class TrocaDoTitularDaInscricaoTests
{
    // Alexandre é o titular (Jogador1); Tiago é o parceiro (Jogador2). Fulano é quem entra.
    private static async Task<(DbPadelContext ctx, Torneio torneio, Categoria cat, Jogador organizador,
        Jogador alexandre, Jogador tiago, Jogador fulano, Dupla dupla)> MontarAsync(string status)
    {
        var ctx = TestInfra.NovoContexto();

        var torneio = new Torneio { Nome = "Er Padel Open", Codigo = "ER1", Status = status };
        ctx.Torneios.Add(torneio);
        await ctx.SaveChangesAsync();

        var cat = new Categoria { Nome = "3ª Masculina", Codigo = "C3M", TorneioId = torneio.Id };
        ctx.Categorias.Add(cat);

        var organizador = new Jogador { Nome = "Organizador", Cpf = "11144477735" };
        var alexandre = new Jogador { Nome = "Alexandre Lima", Cpf = "22255588846" };
        var tiago = new Jogador { Nome = "Tiago Prezzi", Cpf = "33366699957" };
        var fulano = new Jogador { Nome = "Arthur Prass", Cpf = "44477788827" };
        ctx.Jogadores.AddRange(organizador, alexandre, tiago, fulano);
        await ctx.SaveChangesAsync();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador
        {
            TorneioId = torneio.Id, JogadorId = organizador.Id, NivelAcesso = "Criador",
        });

        var dupla = new Dupla
        {
            CategoriaId = cat.Id, Jogador1Id = alexandre.Id, Jogador2Id = tiago.Id,
            Codigo = "DAL", Pago = true,
        };
        ctx.Duplas.Add(dupla);
        await ctx.SaveChangesAsync();

        return (ctx, torneio, cat, organizador, alexandre, tiago, fulano, dupla);
    }

    private static DuplasController Controller(DbPadelContext ctx, int usuarioLogadoId) =>
        Controller(ctx, usuarioLogadoId, out _);

    private static DuplasController Controller(DbPadelContext ctx, int usuarioLogadoId,
        out IPushNotificationService push)
    {
        push = Substitute.For<IPushNotificationService>();
        var controller = new DuplasController(
            ctx, new EstatisticasService(ctx),
            push,
            Substitute.For<IPagamentoInscricaoService>(),
            new ValidacaoPeloRankingRs(ctx, Substitute.For<IRankingRsService>(),
                NullLogger<ValidacaoPeloRankingRs>.Instance),
            new AvisoDeInscricaoNoTorneio(ctx, Substitute.For<IPushNotificationService>()),
            NullLogger<DuplasController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.ToString()) }, "Teste")),
                },
            },
        };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        controller.Url = TestInfra.UrlDeTeste();
        return controller;
    }

    // ── O CASO DO FELIPE ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task O_organizador_tira_o_titular_e_quem_fica_sobe_pro_lugar_dele()
    {
        var (ctx, _t, _c, organizador, alexandre, tiago, fulano, dupla) =
            await MontarAsync("Chaves em Sorteio");
        using var _ = ctx;

        var controller = Controller(ctx, organizador.Id);
        await controller.TrocarParceiro(dupla.Id, fulano.Cpf, null, saiId: alexandre.Id);

        var depois = await ctx.Duplas.AsNoTracking().FirstAsync(d => d.Id == dupla.Id);
        Assert.Null(controller.TempData["Erro"]);
        Assert.Equal(tiago.Id, depois.Jogador1Id);
        Assert.Equal(fulano.Id, depois.Jogador2Id);
    }

    [Fact]
    public async Task O_titular_que_saiu_nao_fica_em_lugar_nenhum_da_inscricao()
    {
        // O defeito que este teste trava é o mais caro de todos: tirar o Alexandre de um campo
        // e deixá-lo no outro. Ranking, Padelímetro e estatística leem os DOIS (Jogador1Id ou
        // Jogador2Id) — sobrar em qualquer um dos dois é continuar inscrito, calado.
        var (ctx, _t, _c, organizador, alexandre, _tiago, fulano, dupla) =
            await MontarAsync("Chaves em Sorteio");
        using var _ = ctx;

        await Controller(ctx, organizador.Id).TrocarParceiro(dupla.Id, fulano.Cpf, null, saiId: alexandre.Id);

        var depois = await ctx.Duplas.AsNoTracking().FirstAsync(d => d.Id == dupla.Id);
        Assert.NotEqual(alexandre.Id, depois.Jogador1Id);
        Assert.NotEqual(alexandre.Id, depois.Jogador2Id);
    }

    [Fact]
    public async Task Tirar_o_titular_nao_mexe_no_dinheiro_da_inscricao()
    {
        // Continua sendo uma inscrição de DUAS pessoas, antes e depois: o valor congelado não
        // muda e o "Pago" segue de pé. Trocar não é vender uma vaga.
        var (ctx, _t, _c, organizador, alexandre, tiago, fulano, dupla) =
            await MontarAsync("Chaves em Sorteio");
        using var _ = ctx;
        dupla.ValorInscricao = 240m;
        await ctx.SaveChangesAsync();

        await Controller(ctx, organizador.Id).TrocarParceiro(dupla.Id, fulano.Cpf, null, saiId: alexandre.Id);

        var depois = await ctx.Duplas.AsNoTracking().FirstAsync(d => d.Id == dupla.Id);
        // A saída do TITULAR aconteceu de verdade — senão o resto deste teste mediria o
        // dinheiro de uma troca do segundo nome, que já tem teste próprio e passaria de graça.
        Assert.Equal(tiago.Id, depois.Jogador1Id);
        Assert.Equal(fulano.Id, depois.Jogador2Id);
        Assert.Equal(240m, depois.ValorInscricao);
        Assert.True(depois.Pago);
    }

    [Fact]
    public async Task O_aviso_de_quem_saiu_nomeia_quem_FICOU_e_nao_quem_saiu()
    {
        // O aviso é montado com `dupla.Jogador1.Nome`, e o que o deixa certo é a ORDEM: o push
        // sai depois do `SaveChangesAsync`, quando o ChangeTracker já trocou a navegação junto
        // com a FK. Puxar `AvisarTrocaDeParceiroAsync` pra antes do save — ou destacar a dupla —
        // faz o aviso chegar ao Alexandre dizendo "Alexandre Lima trocou de parceiro", ele
        // lendo que ele mesmo se tirou. Falha calada: o banco fica certo, só o texto mente, e
        // nenhum dos testes de composição acima olha pra isso.
        var (ctx, _t, _c, organizador, alexandre, tiago, fulano, dupla) =
            await MontarAsync("Chaves em Sorteio");
        using var _ = ctx;

        await Controller(ctx, organizador.Id, out var push)
            .TrocarParceiro(dupla.Id, fulano.Cpf, null, saiId: alexandre.Id);

        await push.Received(1).EnviarParaJogadorAsync(
            alexandre.Id,
            Arg.Any<string>(),
            Arg.Is<string>(corpo => corpo != null
                && corpo.Contains(tiago.Nome) && !corpo.Contains(alexandre.Nome)),
            Arg.Any<string?>(),
            Arg.Any<AlcanceDoAviso>());
    }

    // ── A JANELA: A MESMA DE TROCAR O SEGUNDO NOME ────────────────────────────────────────

    [Fact]
    public async Task Com_as_inscricoes_abertas_o_proprio_parceiro_tira_o_titular()
    {
        // 🗣️ "antes de fechar as chaves organizador e os dois da dupla". O Tiago é o Jogador2 e
        // está tirando quem se inscreveu — e pode, enquanto as inscrições estão abertas.
        var (ctx, _t, _c, _org, alexandre, tiago, fulano, dupla) =
            await MontarAsync("Inscrições Abertas");
        using var _ = ctx;

        var controller = Controller(ctx, tiago.Id);
        await controller.TrocarParceiro(dupla.Id, fulano.Cpf, null, saiId: alexandre.Id);

        var depois = await ctx.Duplas.AsNoTracking().FirstAsync(d => d.Id == dupla.Id);
        Assert.Null(controller.TempData["Erro"]);
        Assert.Equal(tiago.Id, depois.Jogador1Id);
        Assert.Equal(fulano.Id, depois.Jogador2Id);
    }

    [Fact]
    public async Task Depois_do_sorteio_o_jogador_nao_tira_mais_o_titular()
    {
        // 🗣️ "depois que fechar, só o organizador". A régua do jogador é a de sempre — e é ela
        // que impede o parceiro de desfazer, sozinho, uma dupla que já está na chave.
        var (ctx, _t, _c, _org, alexandre, tiago, fulano, dupla) =
            await MontarAsync("Chaves em Sorteio");
        using var _ = ctx;

        var controller = Controller(ctx, tiago.Id);
        await controller.TrocarParceiro(dupla.Id, fulano.Cpf, null, saiId: alexandre.Id);

        var depois = await ctx.Duplas.AsNoTracking().FirstAsync(d => d.Id == dupla.Id);
        Assert.NotNull(controller.TempData["Erro"]);
        Assert.Equal(alexandre.Id, depois.Jogador1Id);
        Assert.Equal(tiago.Id, depois.Jogador2Id);
    }

    [Fact]
    public async Task Depois_que_a_bola_rolou_nem_o_organizador_tira_o_titular()
    {
        // O mesmo teto que protege o histórico no outro nome: `Partida` não guarda quem jogou,
        // lê a composição ATUAL da dupla. Tirar o titular depois do jogo daria ao novo os games
        // que o Alexandre jogou.
        var (ctx, _t, cat, organizador, alexandre, tiago, fulano, dupla) =
            await MontarAsync("Fase de Grupos");
        using var _ = ctx;

        var adversaria = new Dupla { CategoriaId = cat.Id, Jogador1Id = organizador.Id, Codigo = "DADV" };
        ctx.Duplas.Add(adversaria);
        await ctx.SaveChangesAsync();

        ctx.Partidas.Add(new Partida
        {
            CategoriaId = cat.Id, Dupla1Id = dupla.Id, Dupla2Id = adversaria.Id,
            Codigo = "P1", Status = "Finalizada",
        });
        await ctx.SaveChangesAsync();

        var controller = Controller(ctx, organizador.Id);
        await controller.TrocarParceiro(dupla.Id, fulano.Cpf, null, saiId: alexandre.Id);

        var depois = await ctx.Duplas.AsNoTracking().FirstAsync(d => d.Id == dupla.Id);
        Assert.NotNull(controller.TempData["Erro"]);
        Assert.Equal(alexandre.Id, depois.Jogador1Id);
        Assert.Equal(tiago.Id, depois.Jogador2Id);
    }

    [Fact]
    public async Task Quem_nao_e_da_dupla_nem_organiza_continua_sem_entrar()
    {
        // Regra 0: a porta nova não pode ter afrouxado o gate. O `fulano` é estranho à dupla —
        // e é exatamente quem teria interesse em se pendurar nela tirando o titular.
        var (ctx, _t, _c, _org, alexandre, tiago, fulano, dupla) =
            await MontarAsync("Inscrições Abertas");
        using var _ = ctx;

        var resultado = await Controller(ctx, fulano.Id)
            .TrocarParceiro(dupla.Id, fulano.Cpf, null, saiId: alexandre.Id);

        Assert.IsType<ForbidResult>(resultado);
        var depois = await ctx.Duplas.AsNoTracking().FirstAsync(d => d.Id == dupla.Id);
        Assert.Equal(alexandre.Id, depois.Jogador1Id);
        Assert.Equal(tiago.Id, depois.Jogador2Id);
    }

    // ── AS RECUSAS DO PRÓPRIO `saiId` ─────────────────────────────────────────────────────

    [Fact]
    public async Task Quem_sai_precisa_estar_nesta_inscricao()
    {
        // `saiId` vem do formulário: é entrada de fora, e um id que não é de nenhum dos dois
        // não pode cair no caminho do "então é o segundo nome" — isso trocaria um nome que
        // ninguém pediu pra trocar.
        var (ctx, _t, _c, organizador, alexandre, tiago, fulano, dupla) =
            await MontarAsync("Chaves em Sorteio");
        using var _ = ctx;

        var controller = Controller(ctx, organizador.Id);
        await controller.TrocarParceiro(dupla.Id, fulano.Cpf, null, saiId: organizador.Id);

        var depois = await ctx.Duplas.AsNoTracking().FirstAsync(d => d.Id == dupla.Id);
        Assert.NotNull(controller.TempData["Erro"]);
        Assert.Equal(alexandre.Id, depois.Jogador1Id);
        Assert.Equal(tiago.Id, depois.Jogador2Id);
    }

    [Fact]
    public async Task O_novo_parceiro_nao_pode_ser_quem_fica()
    {
        // Sem esta recusa a inscrição ficaria com o Tiago nos DOIS campos — uma dupla de uma
        // pessoa só, passando por completa em toda consulta que lê `Completa`.
        var (ctx, _t, _c, organizador, alexandre, tiago, _fulano, dupla) =
            await MontarAsync("Chaves em Sorteio");
        using var _ = ctx;

        var controller = Controller(ctx, organizador.Id);
        await controller.TrocarParceiro(dupla.Id, tiago.Cpf, null, saiId: alexandre.Id);

        var depois = await ctx.Duplas.AsNoTracking().FirstAsync(d => d.Id == dupla.Id);
        Assert.NotNull(controller.TempData["Erro"]);
        Assert.Equal(alexandre.Id, depois.Jogador1Id);
        Assert.Equal(tiago.Id, depois.Jogador2Id);
    }

    [Fact]
    public async Task Na_inscricao_sozinha_nao_ha_titular_pra_tirar()
    {
        // Ali a pergunta é outra — DEFINIR o que falta — e tirar o único nome deixaria a
        // inscrição sem ninguém. `Jogador1Id` é NOT NULL: o caminho nem existe.
        var (ctx, _t, _c, organizador, alexandre, _tiago, fulano, dupla) =
            await MontarAsync("Inscrições Abertas");
        using var _ = ctx;
        dupla.Jogador2Id = null;
        await ctx.SaveChangesAsync();

        var controller = Controller(ctx, organizador.Id);
        await controller.TrocarParceiro(dupla.Id, fulano.Cpf, null, saiId: alexandre.Id);

        var depois = await ctx.Duplas.AsNoTracking().FirstAsync(d => d.Id == dupla.Id);
        Assert.NotNull(controller.TempData["Erro"]);
        Assert.Equal(alexandre.Id, depois.Jogador1Id);
        Assert.Null(depois.Jogador2Id);
    }

    [Fact]
    public async Task Sem_saiId_a_troca_continua_sendo_a_do_segundo_nome()
    {
        // A regressão que importa: todo formulário que já existia posta SEM `saiId`, e o
        // comportamento deles não pode ter mudado uma vírgula.
        var (ctx, _t, _c, organizador, alexandre, _tiago, fulano, dupla) =
            await MontarAsync("Chaves em Sorteio");
        using var _ = ctx;

        var controller = Controller(ctx, organizador.Id);
        await controller.TrocarParceiro(dupla.Id, fulano.Cpf, null);

        var depois = await ctx.Duplas.AsNoTracking().FirstAsync(d => d.Id == dupla.Id);
        Assert.Null(controller.TempData["Erro"]);
        Assert.Equal(alexandre.Id, depois.Jogador1Id);
        Assert.Equal(fulano.Id, depois.Jogador2Id);
    }

    [Fact]
    public async Task Apontar_saiId_para_o_segundo_nome_e_a_troca_de_sempre()
    {
        var (ctx, _t, _c, organizador, alexandre, tiago, fulano, dupla) =
            await MontarAsync("Chaves em Sorteio");
        using var _ = ctx;

        var controller = Controller(ctx, organizador.Id);
        await controller.TrocarParceiro(dupla.Id, fulano.Cpf, null, saiId: tiago.Id);

        var depois = await ctx.Duplas.AsNoTracking().FirstAsync(d => d.Id == dupla.Id);
        Assert.Null(controller.TempData["Erro"]);
        Assert.Equal(alexandre.Id, depois.Jogador1Id);
        Assert.Equal(fulano.Id, depois.Jogador2Id);
    }

    // ── A TELA ────────────────────────────────────────────────────────────────────────────
    //
    // Teste de FONTE, mesmo motivo de TrocaDeParceiroPeloOrganizadorTests: a suíte não renderiza
    // Razor, e servidor que aceita com caminho escondido é a mudança que não existe pra quem usa.

    private static string Tela()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou");
            if (Directory.Exists(Path.Combine(tentativa, "Views")))
                return File.ReadAllText(Path.Combine(tentativa, "Views", "Torneios", "Details.cshtml"));
            pasta = Directory.GetParent(pasta)?.FullName;
        }

        throw new DirectoryNotFoundException("Não achei a pasta Padelizou/Views a partir do bin.");
    }

    private static string Formulario(string idDoCollapse)
    {
        var tela = Tela();
        var inicio = tela.IndexOf(idDoCollapse, StringComparison.Ordinal);
        Assert.True(inicio > 0, $"Não achei o formulário de parceiro em {idDoCollapse}.");

        var fim = tela.IndexOf("</form>", inicio, StringComparison.Ordinal);
        Assert.True(fim > inicio, $"O formulário de {idDoCollapse} não fecha.");

        return tela[inicio..fim];
    }

    [Fact]
    public void O_formulario_do_organizador_deixa_escolher_quem_sai()
    {
        Assert.Contains("name=\"saiId\"", Formulario("id=\"trocaParceiro-"));
    }

    [Fact]
    public void O_formulario_da_propria_inscricao_deixa_escolher_quem_sai()
    {
        // A outra metade da resposta do Felipe: antes das chaves, os DOIS da dupla. Se o
        // seletor só existisse no painel do organizador, "os dois" não teria caminho.
        Assert.Contains("name=\"saiId\"", Formulario("id=\"meuParceiro-"));
    }
}
