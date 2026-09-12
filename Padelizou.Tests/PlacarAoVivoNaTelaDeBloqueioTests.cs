using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// "Seguir este jogo" (16/08/2026, pedido do Felipe: "algo parecido com o placar que o Google
// mostra na tela de bloqueio"). Quem segue um jogo AO VIVO recebe UMA notificação por jogo que
// se ATUALIZA sozinha a cada game — não uma nova pra cada um.
//
// ⚠️ O gancho vive em TRÊS lugares (Mesa de Controle offline-first, o lote da lista AO VIVO e
// o Finalizar) porque são as três portas por onde um placar muda — mesma lição da "regra
// duplicada": a segunda cópia é sempre a que ninguém lembra de atualizar. Estes testes cobrem
// as três, não só a mais óbvia.
public class PlacarAoVivoNaTelaDeBloqueioTests
{
    private static async Task<(DbPadelContext ctx, Torneio torneio, Partida aoVivo, Jogador org, Jogador fa)>
        ComUmJogoAoVivoESeguidorAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        await ctx.SaveChangesAsync();

        var doTorneio = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doTorneio.GerarChaves(torneio.Id);

        var jogo = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).OrderBy(p => p.Id).FirstAsync();
        var partidas = TestInfra.NovoPartidasController(ctx, org.Id);
        await partidas.ColocarNoAr(jogo.Id);

        var fa = new Jogador { Nome = "Torcedor", Cpf = "77788899901" };
        ctx.Jogadores.Add(fa);
        await ctx.SaveChangesAsync();

        ctx.Add(new SeguidorDePartida { JogadorId = fa.Id, PartidaId = jogo.Id });
        await ctx.SaveChangesAsync();

        return (ctx, torneio, jogo, org, fa);
    }

    // ===================== O SERVIÇO (Services/AvisoDePlacarAoVivo) =====================

    [Fact]
    public async Task Avisa_cada_seguidor_com_a_MESMA_tag_do_jogo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        await ctx.SaveChangesAsync();

        var doTorneio = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doTorneio.GerarChaves(torneio.Id);
        var jogo = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).OrderBy(p => p.Id).FirstAsync();
        jogo.GamesDupla1 = 4;
        jogo.GamesDupla2 = 3;
        await ctx.SaveChangesAsync();

        var a = new Jogador { Nome = "Ana", Cpf = "77788899902" };
        var b = new Jogador { Nome = "Beto", Cpf = "77788899903" };
        ctx.Jogadores.AddRange(a, b);
        await ctx.SaveChangesAsync();
        ctx.AddRange(
            new SeguidorDePartida { JogadorId = a.Id, PartidaId = jogo.Id },
            new SeguidorDePartida { JogadorId = b.Id, PartidaId = jogo.Id });
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        var servico = new AvisoDePlacarAoVivo(ctx, push);

        await servico.AvisarSeguidoresAsync(jogo.Id, "/Torneios/Details/1");

        var tag = $"partida-{jogo.Id}";
        await push.Received(1).EnviarPlacarAoVivoAsync(a.Id,
            Arg.Is<string>(t => t != null && t.Contains("4") && t.Contains("3")),
            Arg.Any<string>(), "/Torneios/Details/1", tag, Arg.Any<string>());
        await push.Received(1).EnviarPlacarAoVivoAsync(b.Id, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), tag, Arg.Any<string>());
    }

    // Jogo sem seguidor nenhum — o caso comum — não pode custar a consulta cara (a partida com
    // as duas duplas carregadas). O teste prova o efeito observável: zero push.
    [Fact]
    public async Task Jogo_sem_seguidor_nao_manda_push_nenhum()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        await ctx.SaveChangesAsync();
        var doTorneio = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doTorneio.GerarChaves(torneio.Id);
        var jogo = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).OrderBy(p => p.Id).FirstAsync();

        var push = Substitute.For<IPushNotificationService>();
        await new AvisoDePlacarAoVivo(ctx, push).AvisarSeguidoresAsync(jogo.Id, "/x");

        await push.DidNotReceiveWithAnyArgs().EnviarPlacarAoVivoAsync(
            default, default!, default!, default!, default!, default!);
    }

    // O jogo acabou: manda o placar FINAL e não deixa a linha pra trás — não há mais "ao
    // vivo" pra essa pessoa acompanhar, e uma linha esquecida nunca mais dispara nada.
    [Fact]
    public async Task Ao_terminar_manda_o_placar_final_e_para_de_seguir()
    {
        var (ctx, _, jogo, _, fa) = await ComUmJogoAoVivoESeguidorAsync();
        using var _ = ctx;
        jogo.Status = "Finalizada";
        jogo.GamesDupla1 = 9;
        jogo.GamesDupla2 = 5;
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        await new AvisoDePlacarAoVivo(ctx, push).AvisarFimEPararDeSeguirAsync(jogo.Id, "/x");

        // ⚠️ O "encerrado" MUDOU DE LINHA em 12/09/2026, e é de propósito: o título passou a ser
        // o PLACAR (ver a seção abaixo), porque é ele que o Android mostra na notificação
        // recolhida. O estado do jogo desceu pro corpo, que é a segunda linha.
        await push.Received(1).EnviarPlacarAoVivoAsync(fa.Id,
            Arg.Is<string>(t => t != null && t.Contains("9") && t.Contains("5")),
            Arg.Is<string>(c => c != null && c.Contains("encerrado")),
            "/x", $"partida-{jogo.Id}", Arg.Any<string>());

        Assert.False(await ctx.Set<SeguidorDePartida>().AnyAsync(s => s.PartidaId == jogo.Id));
    }

    // ===================== O PLACAR NA LINHA DE FORA (12/09/2026) =====================
    //
    // 🗣️ Felipe, com o jogo ao vivo na mão e a notificação chegando: *"as notificações estao
    // acontecendo, mas eu queria algo tipo esses prints"* — a bolha do app do Google na tela
    // inicial e o placar na Dynamic Island. Nenhum dos dois é alcançável por um site (a bolha é
    // exclusiva do app do Google; a Dynamic Island é Live Activity, que exige app nativo iOS —
    // ver STATUS.md de 16/08). O que dá pra fazer numa notificação da web é ISTO: o placar na
    // linha que o Android mostra RECOLHIDA.
    //
    // Até aqui o título era "Placar ao vivo" — três palavras que não dizem nada de relance — e o
    // placar ficava no corpo. Quem olha o celular de longe lia o rótulo, nunca o jogo.
    [Fact]
    public async Task O_titulo_traz_o_placar_pra_ler_sem_abrir_nada()
    {
        var (ctx, _, jogo, _, fa) = await ComUmJogoAoVivoESeguidorAsync();
        using var _ = ctx;
        jogo.GamesDupla1 = 4;
        jogo.GamesDupla2 = 2;
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        await new AvisoDePlacarAoVivo(ctx, push).AvisarSeguidoresAsync(jogo.Id, "/x");

        await push.Received(1).EnviarPlacarAoVivoAsync(fa.Id,
            Arg.Is<string>(t => t != null && t.Contains("4") && t.Contains("×") && t.Contains("2")),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // E o corpo passa a ser o CONTEXTO: em que pé está o jogo e de que categoria/fase ele é.
    // Sem isso a segunda linha da notificação repetiria o placar que já está no título.
    [Fact]
    public async Task O_corpo_diz_que_o_jogo_esta_ao_vivo()
    {
        var (ctx, _, jogo, _, fa) = await ComUmJogoAoVivoESeguidorAsync();
        using var _ = ctx;

        var push = Substitute.For<IPushNotificationService>();
        await new AvisoDePlacarAoVivo(ctx, push).AvisarSeguidoresAsync(jogo.Id, "/x");

        await push.Received(1).EnviarPlacarAoVivoAsync(fa.Id, Arg.Any<string>(),
            Arg.Is<string>(c => c != null && c.Contains("Ao vivo")),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // ===================== O CARD DENTRO DA NOTIFICAÇÃO =====================

    // O push carrega o endereço do PNG (a `image` do showNotification): puxando a notificação
    // pra baixo no Android, aparece o placar desenhado em vez de duas linhas de texto.
    [Fact]
    public async Task O_push_leva_o_card_do_placar_como_imagem()
    {
        var (ctx, _, jogo, _, fa) = await ComUmJogoAoVivoESeguidorAsync();
        using var _ = ctx;

        var push = Substitute.For<IPushNotificationService>();
        await new AvisoDePlacarAoVivo(ctx, push).AvisarSeguidoresAsync(jogo.Id, "/x");

        await push.Received(1).EnviarPlacarAoVivoAsync(fa.Id, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Is<string>(i => i != null && i.Contains($"/Torneios/CartaoDoPlacarAoVivo/{jogo.Id}")));
    }

    // ⚠️ E O ENDEREÇO MUDA A CADA GAME. Sem isso o `Cache-Control` do card (uma hora, como todo
    // card daqui) devolveria a imagem do placar ANTERIOR — a notificação diria 5×2 no título e
    // mostraria 4×2 no desenho. O trecho depois do `?` não é entrada de nada: o desenho sai do
    // banco; ele existe só pra separar uma versão do card da outra no cache.
    [Fact]
    public async Task O_endereco_do_card_muda_quando_o_placar_muda()
    {
        var (ctx, _, jogo, _, fa) = await ComUmJogoAoVivoESeguidorAsync();
        using var _ = ctx;

        var push = Substitute.For<IPushNotificationService>();
        var servico = new AvisoDePlacarAoVivo(ctx, push);

        jogo.GamesDupla1 = 4;
        jogo.GamesDupla2 = 2;
        await ctx.SaveChangesAsync();
        await servico.AvisarSeguidoresAsync(jogo.Id, "/x");

        jogo.GamesDupla1 = 5;
        await ctx.SaveChangesAsync();
        await servico.AvisarSeguidoresAsync(jogo.Id, "/x");

        var enderecos = push.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == nameof(IPushNotificationService.EnviarPlacarAoVivoAsync))
            .Select(c => (string?)c.GetArguments()[5])
            .ToList();

        Assert.Equal(2, enderecos.Count);
        Assert.NotEqual(enderecos[0], enderecos[1]);
    }

    // ===================== O PNG =====================

    [Fact]
    public async Task O_endereco_do_card_entrega_um_png()
    {
        var (ctx, torneio, jogo, org, _) = await ComUmJogoAoVivoESeguidorAsync();
        using var _ = ctx;
        jogo.GamesDupla1 = 4;
        jogo.GamesDupla2 = 2;
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        var resposta = await controller.CartaoDoPlacarAoVivo(jogo.Id, new FonteDoCartao(PastaDasFontes()));

        var arquivo = Assert.IsType<Microsoft.AspNetCore.Mvc.FileContentResult>(resposta);
        Assert.Equal("image/png", arquivo.ContentType);
        Assert.True(arquivo.FileContents.Length > 1000);
    }

    // ⚠️ TORNEIO OCULTO NÃO ENTREGA O CARD, e nem pro organizador (que ENXERGA o torneio):
    // a resposta é `Cache-Control: public` — é ela que faz o mesmo desenho servir os N
    // seguidores sem redesenhar —, e resposta pública que muda conforme quem pede é como um
    // cache no caminho entrega o card de um torneio escondido pra quem não devia ver.
    [Fact]
    public async Task Torneio_oculto_nao_entrega_o_card()
    {
        var (ctx, torneio, jogo, org, _) = await ComUmJogoAoVivoESeguidorAsync();
        using var _ = ctx;
        torneio.Oculto = true;
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        var resposta = await controller.CartaoDoPlacarAoVivo(jogo.Id, new FonteDoCartao(PastaDasFontes()));

        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(resposta);
    }

    [Fact]
    public async Task Jogo_que_nao_existe_nao_entrega_card()
    {
        var (ctx, _, _, org, _) = await ComUmJogoAoVivoESeguidorAsync();
        using var _ = ctx;

        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        var resposta = await controller.CartaoDoPlacarAoVivo(999999, new FonteDoCartao(PastaDasFontes()));

        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(resposta);
    }

    // O nome da dupla no DESENHO é o mesmo do TEXTO da notificação — uma régua só
    // (`AvisoDePlacarAoVivo.NomeDaDupla`). Duas cópias divergiriam no primeiro ajuste, e o
    // estrago seria a notificação dizendo um nome no título e outro na imagem logo abaixo.
    [Fact]
    public async Task O_nome_da_dupla_no_card_e_o_mesmo_do_texto()
    {
        var (ctx, _, jogo, org, fa) = await ComUmJogoAoVivoESeguidorAsync();
        using var _ = ctx;

        var push = Substitute.For<IPushNotificationService>();
        await new AvisoDePlacarAoVivo(ctx, push).AvisarSeguidoresAsync(jogo.Id, "/x");

        var titulo = (string)push.ReceivedCalls()
            .First(c => c.GetMethodInfo().Name == nameof(IPushNotificationService.EnviarPlacarAoVivoAsync))
            .GetArguments()[1]!;

        var comAsDuplas = await ctx.Partidas
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador1)
            .Include(p => p.Dupla1).ThenInclude(d => d.Jogador2)
            .FirstAsync(p => p.Id == jogo.Id);

        Assert.Contains(AvisoDePlacarAoVivo.NomeDaDupla(comAsDuplas.Dupla1), titulo);
    }

    // O COMBINADO DA IMAGEM EXISTE DOS DOIS LADOS: o C# manda `image` no payload e o sw.js
    // entrega essa chave pro showNotification. Quebrando de um lado só, a notificação volta a
    // ser duas linhas de texto — e o defeito aparece no celular dos outros, nunca aqui. É o
    // mesmo gate da sonda muda (VarreduraDeFantasmasTests).
    [Fact]
    public void O_combinado_do_card_existe_dos_dois_lados()
    {
        var servico = ArquivoDoApp(Path.Combine("Services", "PushNotificationService.cs"));
        var sw = ArquivoDoApp(Path.Combine("wwwroot", "sw.js"));

        Assert.Contains("image = imagem", servico);
        Assert.True(sw.Contains("data.image"),
            "O sw.js não passa mais a imagem pro showNotification — a notificação do placar "
            + "volta a ser só texto, sem nada quebrar em teste nenhum do servidor.");
    }

    private static string ArquivoDoApp(string caminhoRelativo)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
                return File.ReadAllText(Path.Combine(dir.FullName, "Padelizou", caminhoRelativo));
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("pasta do app não encontrada a partir do bin.");
    }

    private static string PastaDasFontes()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou", "wwwroot", "fonts");
            if (Directory.Exists(tentativa)) return tentativa;
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new DirectoryNotFoundException("wwwroot/fonts não encontrado a partir do bin.");
    }

    // ===================== AS TRÊS PORTAS QUE MUDAM O PLACAR =====================

    [Fact]
    public async Task Mesa_de_Controle_avisa_os_seguidores_ao_sincronizar()
    {
        var (ctx, _, jogo, org, fa) = await ComUmJogoAoVivoESeguidorAsync();
        using var _ = ctx;

        var push = Substitute.For<IPushNotificationService>();
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id, push: push);

        await controller.SincronizarPlacar(jogo.Id, games1: 3, games2: 2, sets1: 0, sets2: 0,
            marcadoEm: DateTimeOffset.Now.ToUnixTimeMilliseconds());

        await push.Received(1).EnviarPlacarAoVivoAsync(fa.Id, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), $"partida-{jogo.Id}", Arg.Any<string>());
    }

    [Fact]
    public async Task Lote_da_lista_ao_vivo_avisa_so_o_jogo_que_MUDOU()
    {
        var ctx = TestInfra.NovoContexto();
        using var _ = ctx;
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 8);
        torneio.QuantidadeQuadras = 5;
        await ctx.SaveChangesAsync();

        var doTorneio = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doTorneio.GerarChaves(torneio.Id);
        var partidas = TestInfra.NovoPartidasController(ctx, org.Id);
        var aoVivo = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id)
            .OrderBy(p => p.Id).Take(2).ToListAsync();
        foreach (var jogo in aoVivo) await partidas.ColocarNoAr(jogo.Id);

        var fa = new Jogador { Nome = "Torcedor", Cpf = "77788899904" };
        ctx.Jogadores.Add(fa);
        await ctx.SaveChangesAsync();
        // Só segue o SEGUNDO jogo do lote.
        ctx.Add(new SeguidorDePartida { JogadorId = fa.Id, PartidaId = aoVivo[1].Id });
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id, push: push);

        await controller.SalvarPlacaresAoVivo(torneio.Id, aoVivo.Select(p => p.Id).ToArray(),
            new[] { 4, 4 }, new[] { 1, 1 });

        // Um aviso só, pro jogo que ele segue — não dois, e não pro jogo que ninguém segue.
        await push.Received(1).EnviarPlacarAoVivoAsync(fa.Id, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), $"partida-{aoVivo[1].Id}", Arg.Any<string>());
    }

    [Fact]
    public async Task Finalizar_manda_o_placar_final_e_encerra_o_seguir()
    {
        var (ctx, _, jogo, org, fa) = await ComUmJogoAoVivoESeguidorAsync();
        using var _ = ctx;
        jogo.GamesDupla1 = 9;
        jogo.GamesDupla2 = 4;
        await ctx.SaveChangesAsync();

        var push = Substitute.For<IPushNotificationService>();
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id, push: push);

        await controller.FinalizarPartida(jogo.Id);

        await push.Received(1).EnviarPlacarAoVivoAsync(fa.Id, Arg.Any<string>(),
            Arg.Is<string>(c => c != null && c.Contains("encerrado")), Arg.Any<string>(),
            $"partida-{jogo.Id}", Arg.Any<string>());
        Assert.False(await ctx.Set<SeguidorDePartida>().AnyAsync(s => s.PartidaId == jogo.Id));
    }

    // ===================== SEGUIR / PARAR DE SEGUIR =====================

    [Fact]
    public async Task Seguir_um_jogo_ao_vivo_cria_a_linha()
    {
        var ctx = TestInfra.NovoContexto();
        using var _ = ctx;
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        await ctx.SaveChangesAsync();
        var doTorneio = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doTorneio.GerarChaves(torneio.Id);
        var jogo = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).OrderBy(p => p.Id).FirstAsync();
        var partidas = TestInfra.NovoPartidasController(ctx, org.Id);
        await partidas.ColocarNoAr(jogo.Id);

        var fa = new Jogador { Nome = "Torcedor", Cpf = "77788899905" };
        ctx.Jogadores.Add(fa);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, fa.Id);
        var resposta = await controller.SeguirPartidaAoVivo(jogo.Id);

        var json = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(resposta);
        Assert.Contains("\"seguindo\":true", System.Text.Json.JsonSerializer.Serialize(json.Value));
        Assert.True(await ctx.Set<SeguidorDePartida>().AnyAsync(s => s.JogadorId == fa.Id && s.PartidaId == jogo.Id));
    }

    // Clicar duas vezes (duplo toque, duas abas) não pode virar duas linhas mandando o mesmo
    // aviso duas vezes — o índice único no banco é a segunda trava; esta é a primeira.
    [Fact]
    public async Task Seguir_duas_vezes_nao_duplica_a_linha()
    {
        var (ctx, _, jogo, _, fa) = await ComUmJogoAoVivoESeguidorAsync();
        using var _ = ctx;

        var controller = TestInfra.NovoTorneiosController(ctx, fa.Id);
        await controller.SeguirPartidaAoVivo(jogo.Id);
        await controller.SeguirPartidaAoVivo(jogo.Id);

        Assert.Equal(1, await ctx.Set<SeguidorDePartida>().CountAsync(s => s.JogadorId == fa.Id && s.PartidaId == jogo.Id));
    }

    // Jogo agendado ou já finalizado: seguir não faz sentido (não há placar mudando), e a
    // requisição costuma ser uma tela velha em cache.
    [Fact]
    public async Task Nao_da_pra_seguir_jogo_que_nao_esta_ao_vivo()
    {
        var ctx = TestInfra.NovoContexto();
        using var _ = ctx;
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        await ctx.SaveChangesAsync();
        var doTorneio = TestInfra.NovoTorneiosController(ctx, org.Id);
        await doTorneio.GerarChaves(torneio.Id);
        var agendado = await ctx.Partidas.Where(p => p.TorneioId == torneio.Id).OrderBy(p => p.Id).FirstAsync();

        var fa = new Jogador { Nome = "Torcedor", Cpf = "77788899906" };
        ctx.Jogadores.Add(fa);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, fa.Id);
        var resposta = await controller.SeguirPartidaAoVivo(agendado.Id);

        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(resposta);
        Assert.False(await ctx.Set<SeguidorDePartida>().AnyAsync(s => s.PartidaId == agendado.Id));
    }

    [Fact]
    public async Task Parar_de_seguir_apaga_a_linha()
    {
        var (ctx, _, jogo, _, fa) = await ComUmJogoAoVivoESeguidorAsync();
        using var _ = ctx;

        var controller = TestInfra.NovoTorneiosController(ctx, fa.Id);
        var resposta = await controller.PararDeSeguirPartidaAoVivo(jogo.Id);

        var json = Assert.IsType<Microsoft.AspNetCore.Mvc.JsonResult>(resposta);
        Assert.Contains("\"seguindo\":false", System.Text.Json.JsonSerializer.Serialize(json.Value));
        Assert.False(await ctx.Set<SeguidorDePartida>().AnyAsync(s => s.JogadorId == fa.Id && s.PartidaId == jogo.Id));
    }

    // ===================== O PUSH-ONLY NÃO ENTRA NA CAIXA NEM MANDA E-MAIL =====================
    //
    // Mesmo motivo do AvisoNaoSeguraOCliqueTests: um jogo de 9 games não pode virar 9 linhas
    // na Caixa de Avisos nem 9 e-mails — é acompanhamento em tempo real, não recado.
    [Fact]
    public async Task ApenasPush_nao_entra_na_caixa_de_avisos_nem_manda_email()
    {
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador
        {
            Id = 7, Nome = "Fulano", Cpf = "77788899907", Login = "fulano7",
            Email = "fulano7@exemplo.com", NotificarEmail = true,
        });
        await ctx.SaveChangesAsync();

        var email = Substitute.For<IEmailService>();
        var fila = new FilaDeAvisos(NullLogger<FilaDeAvisos>.Instance);
        var servico = new PushNotificationService(ctx,
            Options.Create(new VapidSettings
            {
                Subject = "mailto:teste@padelizou.com.br",
                PublicKey = "chave-publica-de-teste",
                PrivateKey = "chave-privada-de-teste",
            }),
            Substitute.For<IWhatsAppService>(),
            new FilaDeWhatsApp(NullLogger<FilaDeWhatsApp>.Instance),
            fila, email,
            Options.Create(new SiteSettings { Url = "https://padelizou.com.br" }),
            PorteiroDeTeste.Saida(),
            new SilencioDeAvisos(),
            NullLogger<PushNotificationService>.Instance);

        await servico.EnviarPlacarAoVivoAsync(7, "Placar ao vivo", "4 x 3", "/x", "partida-1",
            "/Torneios/CartaoDoPlacarAoVivo/1?p=4-3");

        Assert.True(fila.TentarLer(out var aviso));
        Assert.True(aviso!.ApenasPush);
        Assert.Equal("partida-1", aviso.Tag);

        await servico.EntregarAgoraAsync(aviso);

        Assert.Empty(ctx.AvisosDoJogador);
        await email.DidNotReceiveWithAnyArgs().EnviarAsync(default!, default!, default!, default!);
    }
}
