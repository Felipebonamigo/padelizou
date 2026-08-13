using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// DUPLICAR TORNEIO: o organizador que faz o mesmo torneio todo mês cria a próxima edição com
// um clique — a ESTRUTURA viaja, o que aconteceu fica.
//
// O teste mais importante daqui é o de reflexão: toda propriedade do Torneio precisa estar em
// UMA das duas listas do DuplicacaoDeTorneio. Campo novo sem lado escolhido quebra o teste —
// que é o contrário de entrar na cópia (ou ficar de fora) em silêncio.
public class DuplicarTorneioTests
{
    // ─────────────────────────── AS DUAS LISTAS ───────────────────────────

    [Fact]
    public void Toda_propriedade_do_Torneio_escolheu_um_lado()
    {
        var escalares = typeof(Torneio).GetProperties()
            .Where(p => p.SetMethod?.IsPublic == true)
            .Where(p => p.PropertyType.IsValueType || p.PropertyType == typeof(string))
            .Select(p => p.Name)
            .ToList();

        var copiadas = DuplicacaoDeTorneio.Copiadas.ToHashSet();
        var naoViajam = DuplicacaoDeTorneio.NaoViajam.ToHashSet();

        // Ninguém nas duas listas ao mesmo tempo.
        Assert.Empty(copiadas.Intersect(naoViajam));

        // E ninguém fora das duas. A mensagem diz QUEM ficou sem lado — é ela que o autor da
        // propriedade nova vai ler.
        var semLado = escalares.Where(n => !copiadas.Contains(n) && !naoViajam.Contains(n)).ToList();
        Assert.True(semLado.Count == 0,
            $"Propriedade nova do Torneio precisa escolher lado na duplicação " +
            $"(DuplicacaoDeTorneio.Copiadas ou NaoViajam): {string.Join(", ", semLado)}");

        // Lista não aponta pra propriedade que não existe (proteção contra rename).
        var nomes = typeof(Torneio).GetProperties().Select(p => p.Name).ToHashSet();
        Assert.All(copiadas.Concat(naoViajam), n => Assert.Contains(n, nomes));
    }

    [Fact]
    public void A_copia_leva_a_estrutura_e_zera_o_que_e_da_edicao()
    {
        var original = new Torneio
        {
            Id = 42,
            Nome = "Copa do Clube",
            Codigo = "ABC12345",
            Status = "Finalizado",
            Formato = "Padrao",
            PrecoInscricao = 79.90m,
            PrecoSegundaInscricao = 49.90m,
            QuantidadeQuadras = 3,
            UsaCheckIn = true,
            UsaVotacaoDeMvp = false,
            Restrito = true,
            ChaveAcesso = "XYZ999",
            LimiteDuplasTotal = 32,
            DataInicio = new DateTime(2026, 7, 1),
            DataFim = new DateTime(2026, 7, 3),
            AprovadoEm = new DateTime(2026, 6, 20),
            AvisoDeMvpEnviadoEm = new DateTime(2026, 7, 4),
            LinkDasFotos = "https://photos.app.goo.gl/abc",
            RecadoAosInscritos = "Levem agasalho",
            MotivoCancelamento = null,
        };

        var novo = DuplicacaoDeTorneio.CopiarConfiguracao(original);

        // A estrutura viaja.
        Assert.Equal(79.90m, novo.PrecoInscricao);
        Assert.Equal(49.90m, novo.PrecoSegundaInscricao);
        Assert.Equal(3, novo.QuantidadeQuadras);
        Assert.True(novo.UsaCheckIn);
        Assert.False(novo.UsaVotacaoDeMvp);   // a escolha do organizador, mesmo desligada
        Assert.True(novo.Restrito);
        Assert.Equal(32, novo.LimiteDuplasTotal);

        // O que é da edição fica.
        Assert.Equal(0, novo.Id);
        Assert.Equal(PortaDaInscricao.Fechada, novo.Status);
        Assert.Null(novo.DataInicio);
        Assert.Null(novo.DataFim);
        Assert.Null(novo.AprovadoEm);          // volta pra fila de aprovação
        Assert.Null(novo.AvisoDeMvpEnviadoEm); // carimbo de aviso NÃO viaja
        Assert.Null(novo.LinkDasFotos);        // as fotos são da edição que aconteceu
        Assert.Null(novo.RecadoAosInscritos);
        Assert.Null(novo.ChaveAcesso);         // provisório; o controller sorteia outra

        // E a série aponta pra raiz.
        Assert.Equal(42, novo.TorneioOrigemId);
    }

    [Fact]
    public void Duplicar_uma_duplicata_herda_a_RAIZ_e_nao_vira_corrente()
    {
        var raiz = new Torneio { Id = 10, Nome = "Copa", Codigo = "A" };
        var edicao2 = new Torneio { Id = 20, Nome = "Copa", Codigo = "B", TorneioOrigemId = 10 };

        Assert.Equal(10, DuplicacaoDeTorneio.RaizDaSerie(raiz));
        Assert.Equal(10, DuplicacaoDeTorneio.RaizDaSerie(edicao2));
        Assert.Equal(10, DuplicacaoDeTorneio.CopiarConfiguracao(edicao2).TorneioOrigemId);
    }

    // ─────────────────────────── O NOME DA EDIÇÃO NOVA ───────────────────────────

    [Fact]
    public void O_nome_original_serve_quando_a_edicao_anterior_ja_acabou()
    {
        var meus = new[] { ("Copa do Clube", (string?)"Finalizado") };
        Assert.Equal("Copa do Clube", DuplicacaoDeTorneio.NomeParaEdicaoNova("Copa do Clube", meus));
    }

    [Fact]
    public void Nome_preso_ganha_numero_ate_achar_um_livre()
    {
        // A original ainda está de pé — "Copa do Clube 2".
        var aindaDePe = new[] { ("Copa do Clube", (string?)"Inscrições Abertas") };
        Assert.Equal("Copa do Clube 2", DuplicacaoDeTorneio.NomeParaEdicaoNova("Copa do Clube", aindaDePe));

        // A "2" também está de pé — "Copa do Clube 3".
        var duasDePe = new[]
        {
            ("Copa do Clube", (string?)"Inscrições Abertas"),
            ("Copa do Clube 2", (string?)"Fase de Grupos"),
        };
        Assert.Equal("Copa do Clube 3", DuplicacaoDeTorneio.NomeParaEdicaoNova("Copa do Clube", duasDePe));
    }

    // ─────────────────────────── O CAMINHO INTEIRO, PELO CONTROLLER ───────────────────────────

    // Um torneio finalizado com tudo que a cópia precisa remapear: limite na categoria,
    // quadras com nome, preferência de quadra e um co-organizador.
    private static (Torneio torneio, Categoria categoria, Jogador organizador, Jogador coOrganizador)
        MontarTorneioCompleto(DbPadelContext ctx)
    {
        var (torneio, categoria, organizador) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Finalizado");
        organizador.IsOrganizadorTorneio = true;   // a porta do formato Padrão
        categoria.LimiteDuplas = 16;
        torneio.QuantidadeQuadras = 2;

        var quadraA = new Quadra { TorneioId = torneio.Id, Nome = "Central" };
        var quadraB = new Quadra { TorneioId = torneio.Id, Nome = "Quadra B" };
        ctx.Quadras.AddRange(quadraA, quadraB);

        var coOrganizador = new Jogador { Nome = "Co-Organizador", Cpf = "99900000098" };
        ctx.Jogadores.Add(coOrganizador);
        ctx.SaveChanges();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador
        {
            TorneioId = torneio.Id, JogadorId = coOrganizador.Id, NivelAcesso = "Organizador",
        });
        ctx.QuadrasDaCategoria.Add(new QuadraDaCategoria
        {
            CategoriaId = categoria.Id, QuadraId = quadraA.Id,
        });
        ctx.SaveChanges();

        return (torneio, categoria, organizador, coOrganizador);
    }

    [Fact]
    public async Task A_edicao_nova_leva_categorias_quadras_preferencias_e_equipe_mas_nenhuma_inscricao()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, organizador, coOrganizador) = MontarTorneioCompleto(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        var resultado = await controller.Duplicar(torneio.Id);

        var redirect = Assert.IsType<RedirectToActionResult>(resultado);
        var novoId = (int)redirect.RouteValues!["id"]!;
        Assert.NotEqual(torneio.Id, novoId);

        var novo = ctx.Torneios.First(t => t.Id == novoId);

        // Identidade própria, série apontando pra origem.
        Assert.Equal(torneio.Id, novo.TorneioOrigemId);
        Assert.NotEqual(torneio.Codigo, novo.Codigo);
        Assert.Equal(PortaDaInscricao.Fechada, novo.Status);
        Assert.Null(novo.DataInicio);
        Assert.Null(novo.AprovadoEm);
        // A edição anterior está finalizada, então o nome fica livre e o original serve.
        Assert.Equal(torneio.Nome, novo.Nome);

        // Categoria copiada com o limite — e SEM as duplas.
        var categoriaNova = Assert.Single(ctx.Categorias.Where(c => c.TorneioId == novoId));
        Assert.Equal(categoria.Nome, categoriaNova.Nome);
        Assert.Equal(16, categoriaNova.LimiteDuplas);
        Assert.Empty(ctx.Duplas.Where(d => d.CategoriaId == categoriaNova.Id));

        // Quadras com os nomes, e a preferência remapeada pros ids NOVOS.
        var quadrasNovas = ctx.Quadras.Where(q => q.TorneioId == novoId).OrderBy(q => q.Id).ToList();
        Assert.Equal(new[] { "Central", "Quadra B" }, quadrasNovas.Select(q => q.Nome));
        var preferencia = Assert.Single(
            ctx.QuadrasDaCategoria.Where(p => p.CategoriaId == categoriaNova.Id));
        Assert.Equal("Central", ctx.Quadras.First(q => q.Id == preferencia.QuadraId).Nome);

        // A equipe viaja: quem duplicou é o CRIADOR (é quem recebe), o resto é organizador.
        var equipe = ctx.TorneioOrganizadores.Where(o => o.TorneioId == novoId).ToList();
        Assert.Equal("Criador", equipe.First(o => o.JogadorId == organizador.Id).NivelAcesso);
        Assert.Equal("Organizador", equipe.First(o => o.JogadorId == coOrganizador.Id).NivelAcesso);
        Assert.Equal(2, equipe.Count);
    }

    [Fact]
    public async Task So_organizador_DESTE_torneio_duplica()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _, _) = MontarTorneioCompleto(ctx);

        // Um jogador de fora — com o perfil de organizador liberado, inclusive: o que trava
        // não é o perfil, é não ser deste torneio.
        var deFora = new Jogador { Nome = "De Fora", Cpf = "99900000097", IsOrganizadorTorneio = true };
        ctx.Jogadores.Add(deFora);
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, deFora.Id);
        var resultado = await controller.Duplicar(torneio.Id);

        var redirect = Assert.IsType<RedirectToActionResult>(resultado);
        Assert.Equal(torneio.Id, redirect.RouteValues!["id"]);
        Assert.NotNull(controller.TempData["Erro"]);
        Assert.Equal(1, ctx.Torneios.Count());
    }

    [Fact]
    public async Task A_porta_do_perfil_vale_na_duplicacao_como_vale_no_Create()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador, _) = MontarTorneioCompleto(ctx);

        // O perfil foi recolhido depois que ele criou o original (aconteceu de verdade com a
        // aprovação pessoa a pessoa): duplicar um Padrão volta a exigir a liberação.
        organizador.IsOrganizadorTorneio = false;
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        var resultado = await controller.Duplicar(torneio.Id);

        Assert.IsType<RedirectToActionResult>(resultado);
        Assert.NotNull(controller.TempData["Erro"]);
        Assert.Equal(1, ctx.Torneios.Count());
    }

    [Fact]
    public async Task Cobrar_pelo_site_sem_conta_conectada_cai_pra_POR_FORA_avisando()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador, _) = MontarTorneioCompleto(ctx);
        torneio.FormaPagamento = FormaDePagamentoDoTorneio.SoPix;
        torneio.PrecoInscricao = 50m;
        await ctx.SaveChangesAsync();

        // Quem duplicou não tem a conta de recebimento — e é ELE que passaria a receber.
        var pagamentos = Substitute.For<IPagamentoInscricaoService>();
        pagamentos.OQueFaltaParaReceber(Arg.Any<Jogador?>()).Returns("Falta conectar a conta.");

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id, pagamentos);
        var resultado = await controller.Duplicar(torneio.Id);

        var redirect = Assert.IsType<RedirectToActionResult>(resultado);
        var novo = ctx.Torneios.First(t => t.Id == (int)redirect.RouteValues!["id"]!);

        // Sem isso, as inscrições entrariam e NENHUMA cobrança nasceria — a armadilha mais
        // cara que o sistema já teve, agora pelo caminho da duplicação.
        Assert.Equal(FormaDePagamentoDoTorneio.Externo, novo.FormaPagamento);
        Assert.Contains("por fora", (string)controller.TempData["Sucesso"]!);
    }

    [Fact]
    public async Task Torneio_restrito_ganha_chave_NOVA_e_nao_a_que_ja_circulou()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador, _) = MontarTorneioCompleto(ctx);
        torneio.Restrito = true;
        torneio.ChaveAcesso = "VELHA1";
        await ctx.SaveChangesAsync();

        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);
        var redirect = Assert.IsType<RedirectToActionResult>(await controller.Duplicar(torneio.Id));
        var novo = ctx.Torneios.First(t => t.Id == (int)redirect.RouteValues!["id"]!);

        Assert.True(novo.Restrito);
        Assert.False(string.IsNullOrWhiteSpace(novo.ChaveAcesso));
        // A chave da edição passada já circulou por grupo de WhatsApp.
        Assert.NotEqual("VELHA1", novo.ChaveAcesso);
    }

    // ─────────────────────────── O RANKING DO CIRCUITO ───────────────────────────

    [Fact]
    public async Task O_ranking_da_serie_SOMA_as_edicoes_e_o_bicampeao_aparece_com_2_titulos()
    {
        using var ctx = TestInfra.NovoContexto();

        // 1ª edição, finalizada, com campeões.
        var (t1, cat1, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Finalizado");
        var duplas1 = ctx.Duplas.Where(d => d.CategoriaId == cat1.Id).OrderBy(d => d.Id).ToList();
        duplas1[0].UltimaFase = "Campeao";
        duplas1[1].UltimaFase = "Final";

        // 2ª edição da série: os MESMOS jogadores, o mesmo pódio.
        var t2 = new Torneio
        {
            Nome = t1.Nome, Codigo = "TST999", Status = "Finalizado",
            DataInicio = new DateTime(2026, 8, 1, 9, 0, 0),
            TorneioOrigemId = t1.Id,
        };
        ctx.Torneios.Add(t2);
        var cat2 = new Categoria { Nome = cat1.Nome, Codigo = cat1.Codigo, Torneio = t2 };
        ctx.Categorias.Add(cat2);
        await ctx.SaveChangesAsync();
        ctx.Duplas.Add(new Dupla
        {
            CategoriaId = cat2.Id, Jogador1Id = duplas1[0].Jogador1Id,
            Jogador2Id = duplas1[0].Jogador2Id, UltimaFase = "Campeao",
        });
        ctx.Duplas.Add(new Dupla
        {
            CategoriaId = cat2.Id, Jogador1Id = duplas1[1].Jogador1Id,
            Jogador2Id = duplas1[1].Jogador2Id, UltimaFase = "Final",
        });
        await ctx.SaveChangesAsync();

        var estatisticas = new EstatisticasService(ctx);
        var solo = await estatisticas.ObterRankingDoTorneioAsync(t1.Id);
        var serie = await estatisticas.ObterRankingDoTorneioAsync(new[] { t1.Id, t2.Id });

        var campeaoSolo = solo.First(l => l.Jogador.Id == duplas1[0].Jogador1Id);
        var campeaoSerie = serie.First(l => l.Jogador.Id == duplas1[0].Jogador1Id);

        // Uma edição: 1 título. O circuito: 2 — e os pontos dobram junto.
        Assert.Equal(1, campeaoSolo.Titulos);
        Assert.Equal(2, campeaoSerie.Titulos);
        Assert.True(campeaoSolo.Pontos > 0);
        Assert.Equal(campeaoSolo.Pontos * 2, campeaoSerie.Pontos);
    }

    // O teste do SELETOR (a série virando opção na tela do ranking) mora em
    // SerieNoSeletorDoRankingTests: ele depende da parte de TELA, que espera a sessão
    // paralela liberar o JogadorsController. Aqui ficam só os testes que não dependem dela —
    // inclusive o de reflexão, que é a proteção central e precisa estar no CI desde já.

    [Fact]
    public async Task Duplicar_a_duplicata_pelo_controller_aponta_pra_MESMA_raiz()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, organizador, _) = MontarTorneioCompleto(ctx);
        var controller = TestInfra.NovoTorneiosController(ctx, organizador.Id);

        var r1 = Assert.IsType<RedirectToActionResult>(await controller.Duplicar(torneio.Id));
        var edicao2Id = (int)r1.RouteValues!["id"]!;

        var r2 = Assert.IsType<RedirectToActionResult>(await controller.Duplicar(edicao2Id));
        var edicao3Id = (int)r2.RouteValues!["id"]!;

        // As duas edições apontam pra raiz — "todas as edições" é uma consulta só.
        Assert.Equal(torneio.Id, ctx.Torneios.First(t => t.Id == edicao2Id).TorneioOrigemId);
        Assert.Equal(torneio.Id, ctx.Torneios.First(t => t.Id == edicao3Id).TorneioOrigemId);

        // E os nomes não colidem: a raiz está finalizada (nome livre pra 2ª edição), mas a 2ª
        // está de pé — a 3ª ganha número.
        Assert.Equal("Torneio de Teste", ctx.Torneios.First(t => t.Id == edicao2Id).Nome);
        Assert.Equal("Torneio de Teste 2", ctx.Torneios.First(t => t.Id == edicao3Id).Nome);
    }
}
