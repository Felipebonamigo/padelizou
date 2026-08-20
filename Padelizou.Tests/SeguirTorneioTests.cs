using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using System.Text.Json;

namespace Padelizou.Tests;

// "Apitouuuu!": quem SEGUE um torneio recebe um aviso a cada inscrição nova nele.
//
// O risco aqui não é o aviso não sair — é ele sair demais, ou sair pela metade:
//   * a inscrição tem DUAS portas (dupla e individual/americano) e ligar só uma deixa metade
//     das inscrições passando calada, sem erro nenhum na tela;
//   * um torneio grande dispara um aviso POR INSCRIÇÃO — 46 duplas viram 46 avisos por
//     seguidor. É rajada, e rajada por e-mail já queimou a cota do Gmail deste projeto.
public class SeguirTorneioTests
{
    private static (DbPadelContext ctx, IPushNotificationService push, AvisoDeInscricaoNoTorneio aviso) Montar()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Torneios.Add(new Torneio { Id = 7, Nome = "Copa da Grade", Codigo = "TEST-APITO" });
        // O apito agora recebe o ID da categoria (é dele que sai o nome e a conta das vagas).
        ctx.Categorias.Add(new Categoria { Id = 701, TorneioId = 7, Nome = "Open Masculino", Codigo = "OM" });
        ctx.Categorias.Add(new Categoria { Id = 702, TorneioId = 7, Nome = "Open", Codigo = "OPN" });
        ctx.SaveChanges();

        var push = Substitute.For<IPushNotificationService>();
        return (ctx, push, new AvisoDeInscricaoNoTorneio(ctx, push));
    }

    private static void Segue(DbPadelContext ctx, params int[] jogadorIds)
    {
        foreach (var id in jogadorIds)
            ctx.SeguidoresTorneio.Add(new SeguidorTorneio { TorneioId = 7, JogadorId = id });
        ctx.SaveChanges();
    }

    [Fact]
    public async Task Quem_segue_o_torneio_recebe_o_apito()
    {
        var (ctx, push, aviso) = Montar();
        Segue(ctx, 100);

        await aviso.NotificarAsync(7, 701, new[] { "Ana", "Bia" }, new[] { 1, 2 }, "/Torneios/Details/7");

        await push.Received(1).EnviarParaJogadorAsync(100, TextoDoApito.Titulo,
            "Dupla Ana e Bia inscrita no torneio Copa da Grade. Na categoria Open Masculino.",
            "/Torneios/Details/7", AlcanceDoAviso.AppSemEmail);
    }

    // ⚠️ E-MAIL NÃO. É rajada e não pede ação nenhuma de quem recebe — exatamente o perfil
    // que fez a cota do Gmail estourar e levar junto 130 e-mails, duas recuperações de senha
    // entre eles. Se alguém trocar o alcance, este teste é o que grita.
    [Fact]
    public async Task O_apito_nunca_sai_por_email()
    {
        var (ctx, push, aviso) = Montar();
        Segue(ctx, 100);

        await aviso.NotificarAsync(7, 702, new[] { "Ana" }, new[] { 1 }, null);

        await push.Received(1).EnviarParaJogadorAsync(
            Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            AlcanceDoAviso.AppSemEmail);
    }

    // Ninguém precisa de notificação pra saber que se inscreveu. Receber isso é o tipo de
    // aviso que ensina a pessoa a ignorar o canal inteiro.
    [Fact]
    public async Task Quem_acabou_de_se_inscrever_nao_recebe_o_proprio_apito()
    {
        var (ctx, push, aviso) = Montar();
        Segue(ctx, 100, 200);

        // 100 é seguidor E acabou de entrar; 200 só segue.
        await aviso.NotificarAsync(7, 702, new[] { "Ana", "Bia" }, new[] { 100, 55 }, null);

        await push.DidNotReceive().EnviarParaJogadorAsync(100, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
        await push.Received(1).EnviarParaJogadorAsync(200, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Fact]
    public async Task Torneio_sem_seguidor_nao_manda_nada()
    {
        var (ctx, push, aviso) = Montar();

        await aviso.NotificarAsync(7, 702, new[] { "Ana" }, new[] { 1 }, null);

        await push.DidNotReceive().EnviarParaJogadorAsync(Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    // Seguir o torneio A não pode fazer chegar aviso do torneio B.
    [Fact]
    public async Task Seguidor_de_outro_torneio_nao_recebe()
    {
        var (ctx, push, aviso) = Montar();
        ctx.SeguidoresTorneio.Add(new SeguidorTorneio { TorneioId = 99, JogadorId = 100 });
        ctx.SaveChanges();

        await aviso.NotificarAsync(7, 702, new[] { "Ana" }, new[] { 1 }, null);

        await push.DidNotReceive().EnviarParaJogadorAsync(Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<AlcanceDoAviso>());
    }

    [Theory]
    // Dois nomes = dupla, com o "Dupla" na frente que o Felipe pediu.
    [InlineData(new[] { "Ana", "Bia" }, "Dupla Ana e Bia inscrita no torneio Copa. Na categoria Open.")]
    // Um nome só = individual. ⚠️ Inclui a inscrição SEM PARCEIRO: chamar de "dupla" quem
    // ainda está sozinho descreveria errado o que apareceu na chave.
    [InlineData(new[] { "Carlos" }, "Carlos inscrito no torneio Copa. Na categoria Open.")]
    public void O_texto_do_apito_muda_entre_dupla_e_individual(string[] nomes, string esperado)
    {
        Assert.Equal(esperado, TextoDoApito.Corpo(nomes, "Copa", "Open"));
    }

    // Torneio de categoria única pode ter nome de categoria vazio; a frase não pode terminar
    // em "Na categoria ." — texto quebrado numa notificação é o que a pessoa lembra do produto.
    [Fact]
    public void Sem_categoria_a_frase_termina_inteira()
    {
        Assert.Equal("Carlos inscrito no torneio Copa.", TextoDoApito.Corpo(new[] { "Carlos" }, "Copa", ""));
    }

    // ── INSCREVEU, SEGUIU ────────────────────────────────────────────────────────────────
    // Desde 20/08/2026 a inscrição segue o torneio sozinha (pedido do Felipe): quem entra
    // na chave é quem quer saber quem mais entra, e quase ninguém achava o botão.

    [Fact]
    public async Task Quem_se_inscreve_passa_a_seguir_o_torneio_sozinho()
    {
        var (ctx, _, aviso) = Montar();

        await aviso.SeguirAsync(7, new[] { 10, 20 });

        var seguidores = ctx.SeguidoresTorneio
            .Where(s => s.TorneioId == 7)
            .Select(s => s.JogadorId)
            .OrderBy(id => id)
            .ToList();
        Assert.Equal(new[] { 10, 20 }, seguidores);
    }

    // Segunda categoria no mesmo torneio, clique repetido, id duplicado na mesma dupla:
    // nada disso pode virar segunda linha nem exceção na chave composta.
    [Fact]
    public async Task Seguir_de_novo_nao_duplica_nem_estoura()
    {
        var (ctx, _, aviso) = Montar();
        Segue(ctx, 10); // já seguia — clicou no botão, ou é a segunda categoria

        await aviso.SeguirAsync(7, new[] { 10, 10, 20 });

        Assert.Equal(2, ctx.SeguidoresTorneio.Count(s => s.TorneioId == 7));
    }

    // O caminho INTEIRO por uma das portas de verdade: o POST de inscrição do americano
    // termina com o jogador seguindo o torneio, sem clicar em nada.
    [Fact]
    public async Task A_inscricao_individual_ja_sai_seguindo_o_torneio()
    {
        var ctx = TestInfra.NovoContexto();
        var torneio = new Torneio
        {
            Nome = "Americano de teste", Codigo = "AMT", Status = "Inscrições Abertas",
            Formato = "Americano", DataInicio = new DateTime(2026, 8, 20, 19, 0, 0),
        };
        ctx.Torneios.Add(torneio);
        var categoria = new Categoria { Nome = "Geral", Codigo = "GER", Torneio = torneio };
        ctx.Categorias.Add(categoria);
        var jogador = new Jogador { Nome = "Fulano Beltrano", Cpf = "52998224725" };
        ctx.Jogadores.Add(jogador);
        ctx.SaveChanges();

        var controller = TestInfra.NovoTorneiosController(ctx, jogador.Id);
        await controller.InscreverIndividual(torneio.Id, categoria.Id, jogador.Nome, jogador.Cpf);

        Assert.True(ctx.SeguidoresTorneio.Any(s => s.TorneioId == torneio.Id && s.JogadorId == jogador.Id),
            "A inscrição individual foi criada mas o jogador NÃO ficou seguindo o torneio.");
    }

    // A TERCEIRA porta: torneio com pagamento obrigatório, onde a inscrição nasce no
    // webhook. Até 20/08/2026 ela ficava fora do apito E do seguir — o torneio que cobrava
    // na entrada era justamente o que nunca apitava.
    [Fact]
    public async Task A_inscricao_paga_que_nasce_no_webhook_segue_e_apita()
    {
        var (ctx, push, _) = Montar();
        ctx.Categorias.Add(new Categoria { Id = 70, TorneioId = 7, Nome = "Open", Codigo = "OP" });
        ctx.Jogadores.AddRange(
            new Jogador { Id = 1, Nome = "Ana", Cpf = "52998224725" },
            new Jogador { Id = 2, Nome = "Bia", Cpf = "11144477735" });
        Segue(ctx, 100); // quem já seguia é quem tem que ouvir o apito

        var pagamento = new Pagamento
        {
            Tipo = "TorneioDupla",
            TorneioId = 7,
            JogadorId = 1,
            Valor = 200m,
            DadosInscricao = JsonSerializer.Serialize(
                new DadosInscricaoTorneio(7, 70, 1, 2, false, false, false, false)),
        };
        ctx.Pagamentos.Add(pagamento);
        ctx.SaveChanges();

        var servico = new PagamentoInscricaoService(
            ctx, Substitute.For<IAsaasService>(), Options.Create(new AsaasSettings()),
            NullLogger<PagamentoInscricaoService>.Instance, push,
            Options.Create(new TaxasExibicao()), Options.Create(new PlanoProfessorSettings()));

        await servico.EfetivarAsync(pagamento);

        Assert.True(ctx.SeguidoresTorneio.Any(s => s.TorneioId == 7 && s.JogadorId == 1),
            "A inscrição paga nasceu no webhook mas o jogador 1 não ficou seguindo o torneio.");
        Assert.True(ctx.SeguidoresTorneio.Any(s => s.TorneioId == 7 && s.JogadorId == 2),
            "A inscrição paga nasceu no webhook mas o jogador 2 não ficou seguindo o torneio.");

        await push.Received(1).EnviarParaJogadorAsync(100, TextoDoApito.Titulo,
            "Dupla Ana e Bia inscrita no torneio Copa da Grade. Na categoria Open.",
            "/Torneios/Details/7", AlcanceDoAviso.AppSemEmail);
    }

    // ⚠️ A GUARDA DA REGRA DUPLICADA, versão do seguir: a regra é uma implementação só
    // (SeguirAsync), mas a CHAMADA precisa existir em cada porta. Sumir uma delas deixa
    // aquela porta fora do seguir automático — e nada na tela denuncia.
    [Fact]
    public void As_TRES_portas_de_inscricao_seguem_o_torneio_sozinhas()
    {
        var dupla = Arquivo(Path.Combine("Controllers", "DuplasController.cs"));
        var individual = Arquivo(Path.Combine("Controllers", "TorneiosController.Inscricoes.cs"));
        var paga = Arquivo(Path.Combine("Services", "PagamentoInscricaoService.cs"));

        Assert.True(dupla.Contains("_avisoDeInscricao.SeguirAsync("),
            "A inscrição em DUPLA parou de seguir o torneio sozinha.");
        Assert.True(individual.Contains("_avisoDeInscricao.SeguirAsync("),
            "A inscrição INDIVIDUAL (americano) parou de seguir o torneio sozinha.");
        Assert.True(paga.Contains("_avisoDeInscricao.SeguirAsync("),
            "A inscrição PAGA (a que nasce no webhook do pagamento obrigatório) parou de "
            + "seguir o torneio sozinha.");
    }

    // ⚠️ A GUARDA DA REGRA DUPLICADA. A inscrição tem TRÊS portas e este projeto já sangrou
    // por consertar só uma delas. Se alguém apagar a chamada de um dos lados, parte das
    // inscrições para de apitar — e nada na tela denuncia. A terceira porta (inscrição paga,
    // que nasce no webhook) ficou muda até 20/08/2026 exatamente por não estar nesta lista.
    [Fact]
    public void As_TRES_portas_de_inscricao_chamam_o_apito()
    {
        var dupla = Arquivo(Path.Combine("Controllers", "DuplasController.cs"));
        var individual = Arquivo(Path.Combine("Controllers", "TorneiosController.Inscricoes.cs"));
        var paga = Arquivo(Path.Combine("Services", "PagamentoInscricaoService.cs"));

        Assert.True(dupla.Contains("_avisoDeInscricao.NotificarAsync("),
            "A inscrição em DUPLA parou de avisar quem segue o torneio.");
        Assert.True(individual.Contains("_avisoDeInscricao.NotificarAsync("),
            "A inscrição INDIVIDUAL (americano) parou de avisar quem segue o torneio — "
            + "metade das inscrições passa calada e nada na tela denuncia.");
        Assert.True(paga.Contains("_avisoDeInscricao.NotificarAsync("),
            "A inscrição PAGA (webhook do pagamento obrigatório) parou de avisar quem segue "
            + "o torneio — o torneio que cobra na entrada volta a ser o que nunca apita.");
    }

    // Seguir GRAVA, e o que grava não pode ser alcançável por GET: link é pré-carregado por
    // navegador e varrido por robô. Mesmo motivo do convite pra panelinha.
    [Fact]
    public void Seguir_e_deixar_de_seguir_sao_POST_e_exigem_carimbo()
    {
        var arquivo = Arquivo(Path.Combine("Controllers", "TorneiosController.Seguir.cs"));

        foreach (var acao in new[] { "SeguirTorneio", "DeixarDeSeguirTorneio" })
        {
            var inicio = arquivo.IndexOf($"IActionResult> {acao}(", StringComparison.Ordinal);
            Assert.True(inicio > 0, $"Não achei a ação {acao}.");

            // Os atributos ficam nas linhas ANTES da assinatura.
            var cabecalho = arquivo[Math.Max(0, inicio - 400)..inicio];
            Assert.True(cabecalho.Contains("[HttpPost]"), $"{acao} deixou de ser [HttpPost].");
            Assert.True(cabecalho.Contains("[ValidateAntiForgeryToken]"), $"{acao} perdeu o carimbo antifalsificação.");
        }
    }

    // ── QUEM VÊ O BOTÃO ──────────────────────────────────────────────────────────────────
    // A tela mostra o botão a partir de ViewBag.EstouInscritoNesteTorneio, e essa conta olha
    // as DUAS portas de inscrição. Se ela olhasse só uma, metade das pessoas abriria a página
    // do próprio torneio sem botão nenhum — e sem erro, que é a assinatura dos defeitos caros
    // deste projeto.

    private static DbPadelContext ComTorneioEInscritos(int inscritoEmDupla, int inscritoNoAmericano)
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Torneios.Add(new Torneio { Id = 7, Nome = "Copa da Grade", Codigo = "TEST-APITO", Formato = "Americano" });
        ctx.Categorias.Add(new Categoria { Id = 70, TorneioId = 7, Nome = "Open", Codigo = "OPEN" });
        ctx.Duplas.Add(new Dupla { Id = 700, CategoriaId = 70, Jogador1Id = inscritoEmDupla });
        ctx.InscricoesAmericanas.Add(new InscricaoAmericana { Id = 701, CategoriaId = 70, JogadorId = inscritoNoAmericano });
        ctx.SaveChanges();
        return ctx;
    }

    [Theory]
    [InlineData(11, true)]   // entrou pela porta da DUPLA
    [InlineData(22, true)]   // entrou pela porta INDIVIDUAL (americano)
    [InlineData(99, false)]  // não está inscrito: sem botão
    public async Task O_botao_de_seguir_aparece_pra_quem_esta_inscrito_por_qualquer_porta(int quemOlha, bool deveVer)
    {
        var ctx = ComTorneioEInscritos(inscritoEmDupla: 11, inscritoNoAmericano: 22);
        var controller = TestInfra.NovoTorneiosController(ctx, quemOlha);

        await controller.Details(7, null, null);

        Assert.Equal(deveVer, controller.ViewBag.EstouInscritoNesteTorneio == true);
    }

    [Fact]
    public async Task O_botao_troca_de_seguir_para_parar_quando_ja_sigo()
    {
        var ctx = ComTorneioEInscritos(inscritoEmDupla: 11, inscritoNoAmericano: 22);
        var controller = TestInfra.NovoTorneiosController(ctx, 11);

        await controller.Details(7, null, null);
        Assert.False(controller.ViewBag.SigoEsteTorneio == true);

        ctx.SeguidoresTorneio.Add(new SeguidorTorneio { TorneioId = 7, JogadorId = 11 });
        ctx.SaveChanges();

        var depois = TestInfra.NovoTorneiosController(ctx, 11);
        await depois.Details(7, null, null);
        Assert.True(depois.ViewBag.SigoEsteTorneio == true);
    }

    private static string Arquivo(string caminhoRelativo) =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), caminhoRelativo));

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
                return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
