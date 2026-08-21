using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using padelizou.Controllers;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// 21/08/2026 — PRESENÇA PRESUMIDA NO JOGO DA SEMANA, pedido do Felipe: "não seja necessário a
// pessoa aceitar, ela já fica como se estivesse no grupo, mas permita a pessoa recusar e sair,
// ai depois disso, somente aceitando".
//
// O que está sendo protegido aqui NÃO é a tela. São três coisas que só aparecem semanas depois:
//
//   • O NÚMERO QUE RESERVA QUADRA. "8 confirmados" deixou de querer dizer "8 disseram sim". Se
//     `Presumido` e `Confirmado` virarem a mesma coisa, a diferença some pra sempre no primeiro
//     dia — e ninguém mais consegue saber quantos de fato responderam.
//   • O PASSADO. Presumir num jogo que já aconteceu é escrever "o grupo inteiro estava lá" num
//     placar de julho. A tela tem botão de "semana anterior", então isso é um clique.
//   • O SILÊNCIO DE QUEM SAIU. Quem apertou "Não vou" não pode voltar pra lista sozinho na
//     semana seguinte... mas TEM que voltar na semana seguinte. É por sessão, não pra sempre.
public class PresencaPresumidaNoJogoDaSemanaTests
{
    private static GruposController Controller(DbPadelContext ctx, int euId)
    {
        var controller = new GruposController(
            ctx, new SessaoGrupoService(ctx),
            Substitute.For<IPushNotificationService>(), NullLogger<GruposController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, euId.ToString()) }, "Teste")),
                },
            },
        };
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext, Substitute.For<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>());
        controller.Url = TestInfra.UrlDeTeste();
        return controller;
    }

    private static async Task<(GrupoPrivado grupo, List<Jogador> membros)> MontarAsync(DbPadelContext ctx)
    {
        var membros = Enumerable.Range(1, 4)
            .Select(i => new Jogador { Nome = $"Jogador {i}", Cpf = $"6660000000{i}", Login = $"pres{i}" })
            .ToList();
        ctx.Jogadores.AddRange(membros);
        await ctx.SaveChangesAsync();

        var grupo = new GrupoPrivado
        {
            Nome = "Terças no OK", CodigoConvite = "PRES01", AdministradorId = membros[0].Id,
            DiaSemanaFixo = 2, HorarioFixo = new TimeSpan(19, 0, 0),
        };
        ctx.GruposPrivados.Add(grupo);
        await ctx.SaveChangesAsync();

        ctx.JogadoresGrupo.AddRange(membros.Select(m =>
            new JogadorGrupo { GrupoId = grupo.Id, JogadorId = m.Id, PontuacaoInterna = 0 }));
        await ctx.SaveChangesAsync();

        return (grupo, membros);
    }

    // ── A régua pura ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Presumido_e_confirmado_contam_como_quem_vai_e_o_resto_nao()
    {
        Assert.True(PresencaNaSessao.EstaNaLista(PresencaNaSessao.Presumido));
        Assert.True(PresencaNaSessao.EstaNaLista(PresencaNaSessao.Confirmado));
        Assert.False(PresencaNaSessao.EstaNaLista(PresencaNaSessao.NaoVai));
        Assert.False(PresencaNaSessao.EstaNaLista(PresencaNaSessao.Pendente));
        Assert.False(PresencaNaSessao.EstaNaLista((string?)null));
    }

    // ⚠️ O PASSADO NÃO SE PRESUME. A tela da Semana tem botão de "semana anterior" e ele manda
    // data arbitrária: sem esta guarda, dois cliques pra trás gravariam "o grupo inteiro estava
    // lá" num jogo de julho que ninguém jogou.
    [Fact]
    public void Jogo_que_ja_passou_nasce_Pendente_e_o_futuro_nasce_Presumido()
    {
        var agora = new DateTime(2026, 8, 21, 12, 0, 0);

        Assert.Equal(PresencaNaSessao.Presumido,
            PresencaNaSessao.StatusDeNascimento(agora.AddHours(1), agora));
        Assert.Equal(PresencaNaSessao.Pendente,
            PresencaNaSessao.StatusDeNascimento(agora.AddMinutes(-1), agora));
        Assert.Equal(PresencaNaSessao.Pendente,
            PresencaNaSessao.StatusDeNascimento(agora.AddDays(-7), agora));
    }

    [Fact]
    public void NaoVai_sem_data_de_resposta_e_o_admin_que_tirou()
    {
        var euAvisei = new ConfirmacaoSessao { Status = PresencaNaSessao.NaoVai, RespondidoEm = DateTime.Now };
        var meTiraram = new ConfirmacaoSessao { Status = PresencaNaSessao.NaoVai, RespondidoEm = null };

        Assert.False(PresencaNaSessao.TiradoPeloAdmin(euAvisei));
        Assert.True(PresencaNaSessao.TiradoPeloAdmin(meTiraram));
    }

    // ── O nascimento da linha ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_panelinha_inteira_nasce_dentro_do_jogo_da_semana()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);

        var sessao = await new SessaoGrupoService(ctx).ObterOuCriarSessaoAsync(grupo, null);

        Assert.Equal(4, sessao.Confirmacoes.Count);
        Assert.All(sessao.Confirmacoes, c => Assert.Equal(PresencaNaSessao.Presumido, c.Status));
        Assert.All(sessao.Confirmacoes, c => Assert.True(PresencaNaSessao.EstaNaLista(c)));

        // ⚠️ Presumido NÃO é resposta: ninguém respondeu nada ainda, e é isso que separa este
        // número do número de quem apertou o botão.
        Assert.All(sessao.Confirmacoes, c => Assert.Null(c.RespondidoEm));
    }

    [Fact]
    public async Task Sessao_de_um_jogo_que_ja_passou_NAO_presume_ninguem()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, _) = await MontarAsync(ctx);

        var semanaPassada = DateTime.Today.AddDays(-7).AddHours(19);
        var sessao = await new SessaoGrupoService(ctx).ObterOuCriarSessaoAsync(grupo, semanaPassada);

        Assert.All(sessao.Confirmacoes, c => Assert.Equal(PresencaNaSessao.Pendente, c.Status));
    }

    // ⚠️ O ITEM MAIS SILENCIOSO DA LEVA. Excluir a conta ANONIMIZA em vez de apagar, e nada no
    // sistema remove a linha de JogadoresGrupo. Sem o filtro, "Jogador removido" ganha linha
    // toda semana, entra na lista da quadra e no sorteio — e não consegue sair, porque a conta
    // não loga mais. HOJE este teste falha sem a correção.
    [Fact]
    public async Task Conta_excluida_NAO_ganha_linha_no_jogo_da_semana()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);

        membros[3].ExcluidoEm = DateTime.Now.AddDays(-1);
        membros[3].Nome = "Jogador removido";
        await ctx.SaveChangesAsync();

        var sessao = await new SessaoGrupoService(ctx).ObterOuCriarSessaoAsync(grupo, null);

        Assert.Equal(3, sessao.Confirmacoes.Count);
        Assert.DoesNotContain(sessao.Confirmacoes, c => c.JogadorId == membros[3].Id);
    }

    // ── Recusar, e voltar ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Quem_recusa_sai_da_lista_e_so_volta_APERTANDO()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);
        var sessao = await new SessaoGrupoService(ctx).ObterOuCriarSessaoAsync(grupo, null);

        await Controller(ctx, membros[1].Id).ConfirmarPresenca(sessao.Id, vou: false, lado: null);

        var minha = await ctx.ConfirmacoesSessao.FirstAsync(c => c.SessaoId == sessao.Id && c.JogadorId == membros[1].Id);
        Assert.Equal(PresencaNaSessao.NaoVai, minha.Status);
        Assert.False(PresencaNaSessao.EstaNaLista(minha));

        // ⚠️ E ELE NÃO VOLTA SOZINHO. Pedir a sessão de novo (é o que toda visita à tela faz)
        // não pode re-presumir quem já disse que não vai — é o "depois disso, somente aceitando".
        var deNovo = await new SessaoGrupoService(ctx).ObterOuCriarSessaoAsync(grupo, null);
        Assert.Equal(PresencaNaSessao.NaoVai,
            deNovo.Confirmacoes.First(c => c.JogadorId == membros[1].Id).Status);

        // Aceitando, volta — e volta como quem RESPONDEU, nunca como presumido.
        await Controller(ctx, membros[1].Id).ConfirmarPresenca(sessao.Id, vou: true, lado: null);
        var voltou = await ctx.ConfirmacoesSessao.FirstAsync(c => c.SessaoId == sessao.Id && c.JogadorId == membros[1].Id);
        Assert.Equal(PresencaNaSessao.Confirmado, voltou.Status);
        Assert.NotNull(voltou.RespondidoEm);
    }

    // ⚠️ A RECUSA VALE POR SESSÃO, e não pra sempre. Panelinha é hábito: quem não pode nesta
    // terça não está se demitindo do grupo. "Saiu, saiu até avisar" seria outro recurso.
    [Fact]
    public async Task Semana_que_vem_quem_recusou_nasce_dentro_de_novo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);

        var servico = new SessaoGrupoService(ctx);
        var estaSemana = await servico.ObterOuCriarSessaoAsync(grupo, null);
        await Controller(ctx, membros[1].Id).ConfirmarPresenca(estaSemana.Id, vou: false, lado: null);

        var semanaQueVem = await servico.ObterOuCriarSessaoAsync(grupo, estaSemana.DataHora.AddDays(7));

        Assert.Equal(PresencaNaSessao.Presumido,
            semanaQueVem.Confirmacoes.First(c => c.JogadorId == membros[1].Id).Status);
    }

    // ── O admin tira alguém ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Admin_tira_da_lista_SEM_carimbar_resposta_no_lugar_da_pessoa()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);
        var sessao = await new SessaoGrupoService(ctx).ObterOuCriarSessaoAsync(grupo, null);

        await Controller(ctx, membros[0].Id).TirarDoJogo(sessao.Id, membros[2].Id);

        var dele = await ctx.ConfirmacoesSessao.FirstAsync(c => c.SessaoId == sessao.Id && c.JogadorId == membros[2].Id);
        Assert.Equal(PresencaNaSessao.NaoVai, dele.Status);

        // ⚠️ O carimbo fica NULO — quem disse que não vai foi o admin, não ela. É esse par que
        // impede a tela de dizer "ela avisou que não ia" sobre quem não avisou nada.
        Assert.Null(dele.RespondidoEm);
        Assert.True(PresencaNaSessao.TiradoPeloAdmin(dele));
    }

    [Fact]
    public async Task Membro_comum_NAO_tira_ninguem_da_lista()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);
        var sessao = await new SessaoGrupoService(ctx).ObterOuCriarSessaoAsync(grupo, null);

        var resultado = await Controller(ctx, membros[1].Id).TirarDoJogo(sessao.Id, membros[2].Id);

        Assert.IsType<ForbidResult>(resultado);
        Assert.Equal(PresencaNaSessao.Presumido,
            (await ctx.ConfirmacoesSessao.FirstAsync(c => c.SessaoId == sessao.Id && c.JogadorId == membros[2].Id)).Status);
    }

    // ── A tela ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_tela_da_semana_separa_quem_confirmou_de_quem_esta_no_automatico()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membros) = await MontarAsync(ctx);
        var sessao = await new SessaoGrupoService(ctx).ObterOuCriarSessaoAsync(grupo, null);

        await Controller(ctx, membros[1].Id).ConfirmarPresenca(sessao.Id, vou: true, lado: null);
        await Controller(ctx, membros[2].Id).ConfirmarPresenca(sessao.Id, vou: false, lado: null);

        var view = Assert.IsType<ViewResult>(
            await Controller(ctx, membros[0].Id).Semana(grupo.Id, sessao.DataHora));

        // 4 membros: 1 confirmou, 1 saiu, 2 continuam no automático → 3 na lista.
        Assert.Equal(3, ((List<ConfirmacaoSessao>)view.ViewData["NaLista"]!).Count);
        Assert.Equal(1, (int)view.ViewData["Confirmaram"]!);
        Assert.Single((List<ConfirmacaoSessao>)view.ViewData["NaoVao"]!);
        Assert.Empty((List<ConfirmacaoSessao>)view.ViewData["FaltaResponder"]!);
    }

    // ── O sorteio ─────────────────────────────────────────────────────────────────────────

    // Ninguém consegue provar na tela que um sorteio saiu errado — por isso ele TEM que dizer
    // quantos da lista nunca responderam. Sem o aviso, o admin sorteia 9 e aparecem 4.
    [Fact]
    public void Sorteio_avisa_quantos_da_lista_nao_responderam()
    {
        var gente = new List<SorteioDeDuplas.Candidato>
        {
            new(1, "Ana", LadoNaQuadra.Direita, Confirmou: true),
            new(2, "Bia", LadoNaQuadra.Esquerda, Confirmou: false),
            new(3, "Caio", LadoNaQuadra.Direita, Confirmou: false),
            new(4, "Duda", LadoNaQuadra.Esquerda, Confirmou: false),
        };

        var r = SorteioDeDuplas.Sortear(gente, new Dictionary<(int, int), int>(), new Random(1));

        Assert.Contains(r.Avisos, a => a.Contains("3 de 4") && a.Contains("automático"));
    }

    [Fact]
    public void Sorteio_com_todo_mundo_confirmado_nao_avisa_nada_de_automatico()
    {
        var gente = new List<SorteioDeDuplas.Candidato>
        {
            new(1, "Ana", LadoNaQuadra.Direita, Confirmou: true),
            new(2, "Bia", LadoNaQuadra.Esquerda, Confirmou: true),
        };

        var r = SorteioDeDuplas.Sortear(gente, new Dictionary<(int, int), int>(), new Random(1));

        Assert.DoesNotContain(r.Avisos, a => a.Contains("automático"));
    }

    // ── O aviso de 24h ────────────────────────────────────────────────────────────────────

    // ⚠️ Ele PAROU de pedir confirmação: pedir a quem já está na lista ensina que ainda falta
    // um ato, que é o contrário do que o sistema passou a fazer.
    [Fact]
    public void O_aviso_de_24h_nao_pede_confirmacao_de_quem_ja_esta_na_lista()
    {
        var quando = new DateTime(2026, 8, 25, 19, 0, 0);

        var paraQuemNaoRespondeu = AvisoDoJogoDaSemana.Corpo(false, "Terças no OK", quando, "Arena X");
        Assert.DoesNotContain("Confirma", paraQuemNaoRespondeu);
        Assert.Contains("avise agora", paraQuemNaoRespondeu);
        Assert.Contains("Terças no OK", paraQuemNaoRespondeu);   // o nome do GRUPO, que faltava
        Assert.Contains("Arena X", paraQuemNaoRespondeu);

        var paraQuemConfirmou = AvisoDoJogoDaSemana.Corpo(true, "Terças no OK", quando, null);
        Assert.Contains("Você confirmou", paraQuemConfirmou);
    }

    // ═══════════════════════════════════════════════════════════════════════════════════════
    // ⚠️ A GUARDA QUE IMPEDE A PRÓXIMA CÓPIA.
    //
    // O modo de falhar desta leva é um leitor esquecido comparando `Status == "Confirmado"` na
    // mão: ele para de contar o presumido e some com gente da lista, CALADO. Revisão humana não
    // pega — a string parece certa. Só a varredura pega.
    // ═══════════════════════════════════════════════════════════════════════════════════════
    [Fact]
    public void Ninguem_compara_o_status_da_presenca_com_a_string_crua()
    {
        var raiz = PastaDoProjeto();

        // Os únicos lugares onde as palavras podem aparecer soltas: a própria régua, e os
        // escritores que traduzem do formulário. Tudo o mais tem que passar por PresencaNaSessao.
        var permitidos = new[]
        {
            Path.Combine("Services", "PresencaNaSessao.cs"),
        };

        // ⚠️ "Pendente" e "Confirmado" também são status de Pagamento e de Desafio — a busca é
        // por comparação com o campo `Status` DE CONFIRMAÇÃO, então o padrão exige o contexto
        // `c.Status ==` / `x.Status ==` junto de uma das palavras da presença.
        var padrao = new Regex(@"\.Status\s*[=!]=\s*""(Presumido|NaoVai)""", RegexOptions.Compiled);

        var suspeitos = new List<string>();
        foreach (var arquivo in Directory.EnumerateFiles(raiz, "*.cs", SearchOption.AllDirectories)
                     .Concat(Directory.EnumerateFiles(raiz, "*.cshtml", SearchOption.AllDirectories)))
        {
            var relativo = Path.GetRelativePath(raiz, arquivo);
            if (relativo.StartsWith("Migrations") || relativo.StartsWith("obj") || relativo.StartsWith("bin")) continue;
            if (permitidos.Any(p => relativo.EndsWith(p, StringComparison.OrdinalIgnoreCase))) continue;

            var texto = File.ReadAllText(arquivo);
            if (padrao.IsMatch(texto)) suspeitos.Add(relativo);
        }

        Assert.True(suspeitos.Count == 0,
            "Comparação de presença com string crua — use PresencaNaSessao: " + string.Join(", ", suspeitos));
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
                return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto web a partir de " + AppContext.BaseDirectory);
    }
}
