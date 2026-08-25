using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Padelizou.Models;
using Padelizou.Services;
using SkiaSharp;
using System.Security.Claims;

namespace Padelizou.Tests;

// O CARD DA NOITE DA PANELINHA — o que a turma manda no grupo depois de jogar.
//
// O que estes testes prendem não é o desenho, é O PÓDIO. E ele tem uma diferença que os
// outros cards não têm: aqui o EMPATE É O CASO NORMAL, não a borda. Com 3 pontos por
// vitória e 1 por derrota, uma noite de quatro jogos empata meia panelinha — um corte nos
// "5 primeiros nomes" partiria empate no meio e publicaria um pódio que mente sobre quem
// ficou em segundo. Por isso o corte é por POSIÇÃO (as três primeiras, com todo mundo que
// empatou dentro delas), e não por quantidade de nomes.
//
// O teste de desenho conta pixels de LETRA pelo mesmo motivo dos outros cards: fonte sem
// glifo desenha o card inteiro, mudo, sem erro nenhum.
public class CartaoDaPanelinhaTests
{
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

    private static FonteDoCartao Fontes() => new(PastaDasFontes());
    private static string WebRoot() => Path.GetDirectoryName(PastaDasFontes())!;

    private static int PixelsBrancos(byte[] png)
    {
        using var imagem = SKBitmap.Decode(png);
        Assert.NotNull(imagem);
        Assert.Equal(CartaoCompartilhavel.Largura, imagem.Width);
        Assert.Equal(CartaoCompartilhavel.Altura, imagem.Height);

        int brancos = 0;
        for (int x = 0; x < imagem.Width; x += 2)
            for (int y = 0; y < imagem.Height; y += 2)
            {
                var c = imagem.GetPixel(x, y);
                if (c.Red > 240 && c.Green > 240 && c.Blue > 240) brancos++;
            }
        return brancos;
    }

    private static DadosDoCardDaPanelinha Noite(
        params (string Nome, int Pontos)[] pontuados) => new()
        {
            Panelinha = "Los Corneteiros",
            Data = new DateTime(2026, 8, 25, 19, 30, 0),
            Clube = "Arena Beira Rio",
            Jogos = 4,
            Podio = CartaoDaPanelinha.Podio(pontuados),
        };

    // ── O pódio ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Ordena_por_pontos_do_maior_pro_menor()
    {
        var podio = CartaoDaPanelinha.Podio(new[] { ("Ana", 4), ("Bruno", 9), ("Carla", 6) });

        Assert.Equal(new[] { "Bruno", "Carla", "Ana" }, podio.Select(l => l.Nome));
        Assert.Equal(new[] { 1, 2, 3 }, podio.Select(l => l.Posicao));
    }

    // ⚠️ O CASO CENTRAL: empate não é exceção nesta pontuação, é rotina.
    [Fact]
    public void Empate_repete_a_posicao_e_pula_a_seguinte()
    {
        var podio = CartaoDaPanelinha.Podio(new[] { ("Ana", 9), ("Bruno", 6), ("Carla", 6), ("Davi", 4) });

        Assert.Equal(new[] { 1, 2, 2 }, podio.Select(l => l.Posicao));
        // Davi está na QUARTA posição, e a quarta não é pódio — mesmo tendo pontuado.
        Assert.DoesNotContain(podio, l => l.Nome == "Davi");
    }

    [Fact]
    public void So_as_tres_primeiras_posicoes_entram()
    {
        var podio = CartaoDaPanelinha.Podio(new[]
        {
            ("Ana", 9), ("Bruno", 7), ("Carla", 5), ("Davi", 3), ("Elis", 1)
        });

        Assert.Equal(3, podio.Count);
        Assert.Equal(new[] { "Ana", "Bruno", "Carla" }, podio.Select(l => l.Nome));
    }

    // Quem não pontuou não é "último": ele não jogou. Zero no card é motivo pra não postar,
    // a mesma régua do card do ano.
    [Fact]
    public void Quem_zerou_nao_entra_no_podio()
    {
        var podio = CartaoDaPanelinha.Podio(new[] { ("Ana", 3), ("Bruno", 0) });

        Assert.Equal(new[] { "Ana" }, podio.Select(l => l.Nome));
    }

    // Panelinha grande com noite igual pra todo mundo: sem teto, o card viraria uma lista de
    // vinte nomes de corpo 12 que ninguém lê no story.
    [Fact]
    public void O_podio_tem_teto_de_linhas()
    {
        var empatados = Enumerable.Range(1, 20).Select(i => ($"Jogador {i}", 3)).ToArray();

        var podio = CartaoDaPanelinha.Podio(empatados);

        Assert.Equal(CartaoDaPanelinha.MaximoDeLinhas, podio.Count);
        Assert.All(podio, l => Assert.Equal(1, l.Posicao));
    }

    [Theory]
    [InlineData(1, "1 jogo na semana")]
    [InlineData(4, "4 jogos na semana")]
    public void Frase_dos_jogos_respeita_o_plural(int jogos, string esperado)
    {
        Assert.Equal(esperado, CartaoDaPanelinha.FraseDosJogos(jogos));
    }

    // ── Quando NÃO vira arte ───────────────────────────────────────────────────────────

    [Fact]
    public void Semana_sem_ninguem_pontuando_nao_vira_arte()
    {
        var vazia = Noite();

        Assert.Empty(vazia.Podio);
        Assert.False(CartaoDaPanelinha.TemOQueMostrar(vazia));
    }

    [Fact]
    public void Semana_sem_jogo_registrado_nao_vira_arte()
    {
        var semJogo = Noite(("Ana", 3));
        semJogo.Jogos = 0;

        Assert.False(CartaoDaPanelinha.TemOQueMostrar(semJogo));
    }

    [Fact]
    public void Uma_noite_com_jogo_e_pontuacao_vira_arte()
    {
        Assert.True(CartaoDaPanelinha.TemOQueMostrar(Noite(("Ana", 3))));
    }

    // ⚠️ ESTE TESTE NASCEU DE OLHAR A ARTE, não o código: a suíte estava verde e o card saía
    // com as três linhas do pódio COLADAS (passo de 40px pra um corpo de 44) e um buraco de
    // 200px embaixo. A primeira versão dividia a faixa pelo TETO de linhas em vez de pela
    // quantidade real — "assim três e oito ocupam a mesma faixa" — e o resultado foi o pior
    // dos dois mundos: apertado em cima, vazio embaixo.
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    public void As_linhas_do_podio_nunca_se_sobrepoem(int quantas)
    {
        var layout = CartaoDaPanelinha.LayoutDoPodio(quantas);

        // Passo menor que o corpo é sobreposição garantida; a folga de 1,15 é o mínimo pra
        // duas linhas de texto não se encostarem.
        Assert.True(layout.Passo >= layout.Corpo * 1.15f,
            $"{quantas} linhas: passo {layout.Passo} pra corpo {layout.Corpo}");
    }

    // Linha sozinha tem passo ZERO, e é o certo: não existe próxima linha pra colidir, e um
    // passo qualquer aqui só empurraria o nome único pra fora do centro da faixa.
    [Fact]
    public void Uma_linha_so_nao_precisa_de_passo()
    {
        var layout = CartaoDaPanelinha.LayoutDoPodio(1);

        Assert.Equal(0, layout.Passo);
        Assert.Equal(
            (CartaoDaPanelinha.TopoDaFaixa + CartaoDaPanelinha.BaseDaFaixa) / 2f,
            layout.PrimeiraLinhaY);
    }

    // E o bloco fica CENTRADO na faixa: sem isso, três nomes ficam grudados no topo com um
    // vão embaixo — que é exatamente como o card saiu na primeira tentativa.
    [Fact]
    public void O_podio_fica_centrado_na_faixa_que_tem()
    {
        var tres = CartaoDaPanelinha.LayoutDoPodio(3);
        var oito = CartaoDaPanelinha.LayoutDoPodio(8);

        Assert.True(tres.PrimeiraLinhaY > oito.PrimeiraLinhaY,
            "com menos linhas o bloco desce, pra ficar no meio da faixa");

        // Nem o de cima escapa da faixa, nem o de baixo passa do rodapé.
        foreach (var layout in new[] { tres, oito })
        {
            var ultima = layout.PrimeiraLinhaY + layout.Passo * (layout.Linhas - 1);
            Assert.True(layout.PrimeiraLinhaY >= CartaoDaPanelinha.TopoDaFaixa);
            Assert.True(ultima <= CartaoDaPanelinha.BaseDaFaixa);
        }
    }

    // ── O desenho ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void O_card_desenha_letra_de_verdade()
    {
        var png = CartaoDaPanelinha.Desenhar(
            Noite(("Ana", 9), ("Bruno", 6), ("Carla", 6)), Fontes(), WebRoot());

        Assert.True(PixelsBrancos(png) > 500);
    }

    // Sem clube o card não pode abrir um buraco no meio nem escrever "null".
    [Fact]
    public void Sem_clube_o_card_continua_inteiro()
    {
        var semClube = Noite(("Ana", 9), ("Bruno", 6));
        semClube.Clube = null;

        var png = CartaoDaPanelinha.Desenhar(semClube, Fontes(), WebRoot());

        Assert.True(PixelsBrancos(png) > 300);
    }
}

// ─────────────────────────────────────────────────────────────────────────────────────────
// A PORTA DO CARD — a parte que não é desenho.
//
// Este é o único card fechado do CartoesController, e por isso é o único que precisa de teste
// de porta: os outros são sobre torneio (evento público) ou sobre alguém mostrando os próprios
// números. Este leva o ranking de um grupo privado, com nome de gente que não escolheu aparecer.
// ─────────────────────────────────────────────────────────────────────────────────────────
public class CardDaPanelinhaNoControllerTests
{
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

    private static Padelizou.Controllers.CartoesController NovoControllerDeCartoes(
        DbPadelContext ctx, int? usuarioLogadoId)
    {
        var fontes = new FonteDoCartao(PastaDasFontes());
        var ambiente = Substitute.For<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        ambiente.WebRootPath.Returns(Path.GetDirectoryName(PastaDasFontes())!);

        var controller = new Padelizou.Controllers.CartoesController(
            ctx, fontes, ambiente, new EstatisticasService(ctx));

        var user = usuarioLogadoId == null
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, usuarioLogadoId.Value.ToString()) }, "Teste"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user },
        };
        return controller;
    }

    // Uma panelinha com um membro, um estranho e um jogo na semana da data devolvida.
    private static async Task<(padelizou.Models.GrupoPrivado Grupo, Jogador Membro, Jogador Estranho, DateTime Data)>
        MontarPanelinhaAsync(DbPadelContext ctx)
    {
        var membro = new Jogador { Nome = "Ana Souza", Cpf = "11111111111" };
        var parceiro = new Jogador { Nome = "Bruno Lima", Cpf = "22222222222" };
        var rival1 = new Jogador { Nome = "Carla Reis", Cpf = "33333333333" };
        var rival2 = new Jogador { Nome = "Davi Alves", Cpf = "44444444444" };
        var estranho = new Jogador { Nome = "Quem Passava", Cpf = "55555555555" };
        ctx.Jogadores.AddRange(membro, parceiro, rival1, rival2, estranho);
        await ctx.SaveChangesAsync();

        var grupo = new padelizou.Models.GrupoPrivado
        {
            Nome = "Los Corneteiros",
            CodigoConvite = "CORNETA",
            AdministradorId = membro.Id,
        };
        ctx.GruposPrivados.Add(grupo);
        await ctx.SaveChangesAsync();

        foreach (var j in new[] { membro, parceiro, rival1, rival2 })
        {
            ctx.JogadoresGrupo.Add(new padelizou.Models.JogadorGrupo { GrupoId = grupo.Id, JogadorId = j.Id });
        }

        var data = new DateTime(2026, 8, 25);
        ctx.JogosSemanais.Add(new JogoSemanal
        {
            GrupoId = grupo.Id,
            DataJogo = data,
            Dupla1Jogador1Id = membro.Id,
            Dupla1Jogador2Id = parceiro.Id,
            Dupla2Jogador1Id = rival1.Id,
            Dupla2Jogador2Id = rival2.Id,
            VencedorLado = 1,
            RegistradoPorId = membro.Id,
        });
        await ctx.SaveChangesAsync();

        return (grupo, membro, estranho, data);
    }

    // ⚠️ O TESTE QUE JUSTIFICA O ARQUIVO. A panelinha é fechada por CodigoConvite; sem esta
    // guarda, qualquer conta logada leria o ranking (e os nomes) de qualquer grupo por
    // `/Cartoes/Panelinha/{id}` — sem convite, sem pista, sem rastro.
    [Fact]
    public async Task Quem_nao_e_da_panelinha_nao_abre_a_pagina_nem_a_imagem()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, _, estranho, data) = await MontarPanelinhaAsync(ctx);

        var controller = NovoControllerDeCartoes(ctx, estranho.Id);

        Assert.IsType<NotFoundResult>(await controller.Panelinha(grupo.Id, data));
        Assert.IsType<NotFoundResult>(await controller.PanelinhaImagem(grupo.Id, data));
    }

    [Fact]
    public async Task Quem_e_da_panelinha_recebe_a_pagina_e_a_imagem()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membro, _, data) = await MontarPanelinhaAsync(ctx);

        var controller = NovoControllerDeCartoes(ctx, membro.Id);

        var pagina = Assert.IsType<ViewResult>(await controller.Panelinha(grupo.Id, data));
        var dados = Assert.IsType<DadosDoCardDaPanelinha>(pagina.Model);
        Assert.Equal("Los Corneteiros", dados.Panelinha);
        Assert.Equal(1, dados.Jogos);
        // Vitória vale 3 e derrota vale 1, então os QUATRO pontuam neste jogo único: os dois
        // vencedores em 1º e os dois perdedores em 3º — não em 2º. Posição de competição: dois
        // empatados no primeiro lugar consomem as duas primeiras posições.
        Assert.Equal(new[] { 1, 1, 3, 3 }, dados.Podio.Select(l => l.Posicao));

        var imagem = Assert.IsType<FileContentResult>(await controller.PanelinhaImagem(grupo.Id, data));
        Assert.Equal("image/png", imagem.ContentType);
        Assert.NotEmpty(imagem.FileContents);
    }

    // ⚠️ O CACHE DESTE CARD NÃO PODE SER `public`. Os outros cards querem cache compartilhado —
    // é dele que a prévia do WhatsApp vive. Este exige login, e `public` autorizaria um proxy
    // ou CDN a guardar a resposta e devolvê-la pra QUEM NÃO É DO GRUPO.
    [Fact]
    public async Task A_imagem_da_panelinha_e_cache_privado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membro, _, data) = await MontarPanelinhaAsync(ctx);

        var controller = NovoControllerDeCartoes(ctx, membro.Id);
        await controller.PanelinhaImagem(grupo.Id, data);

        var cache = controller.Response.Headers.CacheControl.ToString();
        Assert.StartsWith("private", cache);
        Assert.DoesNotContain("public", cache);
    }

    // A janela é de sete dias contados pra trás da data pedida — a mesma da tela da Semana.
    [Fact]
    public async Task Jogo_de_outra_semana_nao_entra_no_card()
    {
        using var ctx = TestInfra.NovoContexto();
        var (grupo, membro, _, data) = await MontarPanelinhaAsync(ctx);

        var controller = NovoControllerDeCartoes(ctx, membro.Id);
        var pagina = Assert.IsType<ViewResult>(await controller.Panelinha(grupo.Id, data.AddDays(21)));
        var dados = Assert.IsType<DadosDoCardDaPanelinha>(pagina.Model);

        Assert.Equal(0, dados.Jogos);
        Assert.Empty(dados.Podio);
        // A PÁGINA abre e explica; a IMAGEM é que não existe.
        Assert.IsType<NotFoundResult>(await controller.PanelinhaImagem(grupo.Id, data.AddDays(21)));
    }
}

// A consulta da semana do card REALMENTE vira SQL? O InMemory do resto da suíte não traduz
// nada (ver TraducaoDeConsultasDoPerfilTests) — e `DataJogo.Date` é exatamente o tipo de
// expressão que passa lisa aqui e é recusada pelo Postgres.
public class TraducaoDaConsultaDoCardDaPanelinhaTests
{
    private static DbPadelContext ContextoPostgres()
    {
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
            .Options;
        return new DbPadelContext(options);
    }

    [Fact]
    public void A_janela_de_sete_dias_do_card_vira_SQL()
    {
        using var ctx = ContextoPostgres();
        var fim = new DateTime(2026, 8, 25);
        var inicio = fim.AddDays(-7);

        // Mesma forma exata de CartoesController.MontarNoiteAsync.
        var consulta = ctx.JogosSemanais
            .AsNoTracking()
            .Where(j => j.GrupoId == 1 && j.DataJogo.Date > inicio && j.DataJogo.Date <= fim);

        Assert.Contains("SELECT", consulta.ToQueryString());
    }

    [Fact]
    public void Os_membros_com_o_jogador_incluido_viram_SQL()
    {
        using var ctx = ContextoPostgres();

        var consulta = ctx.JogadoresGrupo
            .AsNoTracking()
            .Include(jg => jg.Jogador)
            .Where(jg => jg.GrupoId == 1);

        Assert.Contains("SELECT", consulta.ToQueryString());
    }
}
