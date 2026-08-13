using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Padelizou.Controllers;
using Padelizou.Middleware;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A porta que o Ranking Brasil usa pra ler os nossos torneios (GET /api/ranking/torneios).
//
// É a única coisa deste sistema que um terceiro lê por conta própria, e é isso que muda o que
// precisa estar preso aqui. Dois riscos, e nenhum deles quebra nada quando acontece:
//
//  1. SAIR O QUE NÃO DEVIA. Um torneio oculto, um esperando aprovação — ou, o pior de todos,
//     nome de atleta. Nada disso dá erro: vira conteúdo publicado no site de outra empresa.
//  2. NÃO SAIR O QUE DEVIA. Torneio sem clube cadastrado, sem foto, sem data. A lista responde
//     200 com uma linha a menos e ninguém liga a ausência a nada.
public class ApiDeTorneiosDoParceiroTests
{
    // Do tamanho de uma chave de verdade (`openssl rand -hex 32`): abaixo do mínimo a
    // integração se considera desligada, e todo teste daqui responderia 503.
    private const string ChaveBoa = "9f2c7b41ae6d40c8b5e3129af07d6c48";

    // ── O cenário ──────────────────────────────────────────────────────────────────────────

    private static DbPadelContext Cenario(Action<Torneio>? ajustar = null, bool comClube = true)
    {
        var ctx = TestInfra.NovoContexto();

        var cidade = new Cidade { Nome = "gravataí", Estado = "RS" };
        ctx.Cidades.Add(cidade);
        ctx.SaveChanges();

        var clube = new Clube
        {
            Nome = "NATA Padel",
            Endereco = "Av. Dorival Cândido de Oliveira, 100",
            CidadeId = cidade.Id,
        };
        ctx.Clubes.Add(clube);
        ctx.SaveChanges();

        var torneio = new Torneio
        {
            Nome = "NATA PADEL TOUR",
            Codigo = "NATA26",
            Status = PortaDaInscricao.Aberta,
            ValidarPeloRankingRs = true,
            AprovadoEm = new DateTime(2026, 8, 1),
            DataInicio = new DateTime(2026, 8, 22),
            DataFim = new DateTime(2026, 8, 24),
            LocalTorneio = "Quadras cobertas",
            ImagemCapa = "/uploads/capas/nata.webp",
            PrecoInscricao = 90m,
            ClubeId = comClube ? clube.Id : 0,
        };
        ajustar?.Invoke(torneio);
        ctx.Torneios.Add(torneio);
        ctx.SaveChanges();

        ctx.Categorias.AddRange(
            new Categoria
            {
                TorneioId = torneio.Id,
                Nome = "4ª Categoria Masculina",
                Codigo = "4CatM",
                RankingRsCategoriaId = 108,
            },
            // Sem de-para de propósito: a 7ª não existe no catálogo do ranking, e é o caso que
            // eles precisam conseguir distinguir do "esqueceram de configurar".
            new Categoria
            {
                TorneioId = torneio.Id,
                Nome = "7ª Categoria Masculina",
                Codigo = "7CatM",
            });
        ctx.SaveChanges();

        return ctx;
    }

    private static TorneiosDoRankingApiController Api(DbPadelContext ctx,
        string chaveConfigurada = ChaveBoa, string? chaveEnviada = ChaveBoa)
    {
        var controller = new TorneiosDoRankingApiController(
            ctx,
            Options.Create(new RankingRsSettings { ChaveDoParceiro = chaveConfigurada }),
            NullLogger<TorneiosDoRankingApiController>.Instance);

        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("padelizou.com.br");
        if (chaveEnviada != null) http.Request.Headers["x-api-key"] = chaveEnviada;

        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }

    private static async Task<TorneiosParaOParceiroDoRanking.Resposta> ListarAsync(DbPadelContext ctx)
    {
        var resultado = Assert.IsType<OkObjectResult>(await Api(ctx).Torneios());
        return Assert.IsType<TorneiosParaOParceiroDoRanking.Resposta>(resultado.Value);
    }

    // ── A chave ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Sem_a_chave_certa_nao_sai_torneio_nenhum()
    {
        var ctx = Cenario();

        Assert.IsType<UnauthorizedObjectResult>(await Api(ctx, chaveEnviada: null).Torneios());
        Assert.IsType<UnauthorizedObjectResult>(await Api(ctx, chaveEnviada: "").Torneios());
        Assert.IsType<UnauthorizedObjectResult>(await Api(ctx, chaveEnviada: "chave-errada").Torneios());
    }

    // Uma chave curta protege tão pouco quanto nenhuma, e o jeito de isso passar despercebido é
    // a API responder normalmente. Aqui ela se comporta como desligada.
    [Fact]
    public async Task Chave_curta_demais_conta_como_integracao_desligada()
    {
        var ctx = Cenario();
        var curta = "padelizou123";

        var resultado = Assert.IsType<ObjectResult>(await Api(ctx, chaveConfigurada: curta, chaveEnviada: curta).Torneios());

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, resultado.StatusCode);
    }

    // ⚠️ 503 e não 401: quando a chave nem foi configurada aqui, o problema é NOSSO. Com um 401
    // o parceiro passaria a tarde conferindo a chave que recebeu.
    [Fact]
    public async Task Sem_chave_configurada_a_resposta_diz_que_o_problema_e_daqui()
    {
        var ctx = Cenario();

        var resultado = Assert.IsType<ObjectResult>(await Api(ctx, chaveConfigurada: "").Torneios());

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, resultado.StatusCode);
    }

    // A comparação é de tempo fixo (CryptographicOperations), o que é fácil de quebrar sem
    // querer numa refatoração — este teste segura o comportamento visível dela.
    [Fact]
    public void A_chave_confere_so_quando_e_exatamente_a_mesma()
    {
        var settings = new RankingRsSettings { ChaveDoParceiro = ChaveBoa };

        Assert.True(settings.ChaveDoParceiroConfere(ChaveBoa));
        // Espaço em volta acontece em toda variável de systemd copiada e colada.
        Assert.True(settings.ChaveDoParceiroConfere($"  {ChaveBoa} "));
        Assert.False(settings.ChaveDoParceiroConfere(ChaveBoa + "x"));
        Assert.False(settings.ChaveDoParceiroConfere(ChaveBoa.ToUpperInvariant()));
        Assert.False(settings.ChaveDoParceiroConfere(null));
    }

    // ── Quem entra na lista ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task O_torneio_aberto_que_aderiu_ao_ranking_aparece()
    {
        var lista = await ListarAsync(Cenario());

        var torneio = Assert.Single(lista.Torneios);
        Assert.Equal("NATA PADEL TOUR", torneio.Nome);
        Assert.Equal(1, lista.Quantidade);
    }

    // A caixa "conferir as inscrições no Ranking" é o que faz o torneio ser DELES. Sem ela, o
    // torneio é nosso e mais nada — e é a mesma coluna pela qual a gente paga o R$ 1 por
    // inscrito, então a lista e a conta enxergam o mesmo conjunto.
    [Fact]
    public async Task Torneio_que_nao_escolheu_o_ranking_fica_de_fora()
    {
        var lista = await ListarAsync(Cenario(t => t.ValidarPeloRankingRs = false));

        Assert.Empty(lista.Torneios);
    }

    [Theory]
    // Fechado pra sorteio, em andamento e finalizado: nenhum deles é "torneio aberto".
    [InlineData(PortaDaInscricao.Fechada)]
    [InlineData("Fase de Grupos")]
    [InlineData("Finalizado")]
    [InlineData("Cancelado")]
    public async Task So_entra_torneio_com_inscricao_aberta(string status)
    {
        var lista = await ListarAsync(Cenario(t => t.Status = status));

        Assert.Empty(lista.Torneios);
    }

    // Aprovação é a trava que impede alguém de encher o sistema de torneio inventado. Ela vale
    // pra vitrine — e a vitrine agora inclui o site do parceiro, que publica o que a gente
    // mandar sem ter como desconfiar.
    [Fact]
    public async Task Torneio_esperando_aprovacao_nao_estreia_no_site_do_parceiro()
    {
        var lista = await ListarAsync(Cenario(t => t.AprovadoEm = null));

        Assert.Empty(lista.Torneios);
    }

    [Fact]
    public async Task Torneio_oculto_pelo_organizador_continua_oculto_la_fora()
    {
        var lista = await ListarAsync(Cenario(t => t.Oculto = true));

        Assert.Empty(lista.Torneios);
    }

    // Restrito NÃO some da lista: ele aparece na nossa vitrine (o que a chave tranca é a
    // inscrição, não a página), e sumir aqui criaria duas listas diferentes de "torneios
    // abertos". Mas vai MARCADO — senão eles anunciariam como aberto a todos um evento que
    // pede chave de acesso, e quem descobre isso é o atleta, no fim do formulário.
    [Fact]
    public async Task Torneio_restrito_aparece_dizendo_que_e_restrito()
    {
        var lista = await ListarAsync(Cenario(t => { t.Restrito = true; t.ChaveAcesso = "SEGREDO"; }));

        Assert.True(Assert.Single(lista.Torneios).InscricaoRestrita);
        // ⚠️ E a chave de acesso NÃO vai junto — ela é a fechadura do torneio.
        Assert.DoesNotContain("SEGREDO", JsonSerializer.Serialize(lista));
    }

    // ── O que vai dentro de cada linha ─────────────────────────────────────────────────────

    [Fact]
    public async Task A_foto_e_o_link_saem_como_endereco_completo()
    {
        var torneio = Assert.Single((await ListarAsync(Cenario())).Torneios);

        Assert.Equal("https://padelizou.com.br/uploads/capas/nata.webp", torneio.Foto);
        Assert.Equal($"https://padelizou.com.br/Torneios/Details/{torneio.Id}", torneio.Link);
    }

    // Torneio sem capa é o caso comum de quem acabou de criar. `null` é a resposta honesta —
    // string vazia viraria `<img src="">` na tela deles.
    [Fact]
    public async Task Torneio_sem_capa_manda_foto_nula_em_vez_de_vazia()
    {
        var lista = await ListarAsync(Cenario(t => t.ImagemCapa = null));

        Assert.Null(Assert.Single(lista.Torneios).Foto);
    }

    // Capa antiga podia ter sido gravada como URL inteira. Concatenar produziria
    // "https://padelizou.com.br/https://..." — um link quebrado, e só no site do parceiro.
    [Fact]
    public void Capa_ja_gravada_como_endereco_completo_nao_e_concatenada()
    {
        Assert.Equal("https://cdn.exemplo.com/capa.webp",
            TorneiosParaOParceiroDoRanking.Absoluto("https://padelizou.com.br", "https://cdn.exemplo.com/capa.webp"));
    }

    [Fact]
    public async Task O_clube_vai_com_cidade_escrita_como_gente()
    {
        var clube = Assert.Single((await ListarAsync(Cenario())).Torneios).Clube;

        Assert.NotNull(clube);
        Assert.Equal("NATA Padel", clube!.Nome);
        // O catálogo de produção tem cidade em minúsculo; o que sai daqui vira título na tela
        // deles.
        Assert.Equal("Gravataí", clube.Cidade);
        Assert.Equal("RS", clube.Estado);
    }

    // ⚠️ Include em navegação obrigatória vira INNER JOIN, e o torneio sem clube sumiria da
    // lista sem erro nenhum. É a armadilha que este teste existe pra trancar.
    [Fact]
    public async Task Torneio_sem_clube_cadastrado_continua_na_lista()
    {
        var lista = await ListarAsync(Cenario(comClube: false));

        var torneio = Assert.Single(lista.Torneios);
        Assert.Null(torneio.Clube);
    }

    // O campo mais útil da resposta pra eles: o de-para, pra casarem a categoria sem adivinhar
    // pelo nome que o organizador digitou.
    [Fact]
    public async Task As_categorias_dizem_qual_e_a_categoria_do_ranking()
    {
        var torneio = Assert.Single((await ListarAsync(Cenario())).Torneios);

        var quarta = Assert.Single(torneio.Categorias, c => c.Nome.StartsWith("4ª"));
        Assert.Equal(108, quarta.RankingId);
        Assert.Equal("4ª Masculina", quarta.RankingNome);

        // A 7ª não existe no ranking deles: nulo é a resposta certa, e é diferente de "zero".
        var setima = Assert.Single(torneio.Categorias, c => c.Nome.StartsWith("7ª"));
        Assert.Null(setima.RankingId);
        Assert.Null(setima.RankingNome);
    }

    [Fact]
    public async Task As_datas_saem_sem_hora_e_sem_fuso()
    {
        var torneio = Assert.Single((await ListarAsync(Cenario())).Torneios);

        Assert.Equal("2026-08-22", torneio.DataInicio);
        Assert.Equal("2026-08-24", torneio.DataFim);
    }

    // ── A promessa que foi feita a eles ────────────────────────────────────────────────────
    //
    // 🚨 O TESTE MAIS IMPORTANTE DESTE ARQUIVO. O combinado foi "nenhum dado dos atletas", e o
    // jeito de essa promessa ser quebrada não é alguém decidir quebrá-la: é um campo novo que
    // arrasta uma navegação junto, ou um `Include` posto pra resolver outra coisa. Nada disso
    // dá erro — vira nome de gente publicado no site de outra empresa.
    //
    // Por isso a conferência é sobre o CORPO INTEIRO serializado, e não sobre os campos que a
    // gente lembrou de listar.
    [Fact]
    public async Task Nenhum_dado_de_atleta_sai_no_corpo_da_resposta()
    {
        var ctx = Cenario();

        var torneio = ctx.Torneios.First();
        var categoria = ctx.Categorias.First(c => c.TorneioId == torneio.Id);

        var ana = new Jogador { Nome = "Ana Krüger Sobrinho", Cpf = "12345678901", Email = "ana@exemplo.com" };
        var bruno = new Jogador { Nome = "Bruno Zappelini", Cpf = "10987654321", Email = "bruno@exemplo.com" };
        ctx.Jogadores.AddRange(ana, bruno);
        ctx.SaveChanges();

        ctx.Duplas.Add(new Dupla { CategoriaId = categoria.Id, Jogador1Id = ana.Id, Jogador2Id = bruno.Id });
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = torneio.Id, JogadorId = ana.Id });
        ctx.SaveChanges();

        var corpo = JsonSerializer.Serialize(await ListarAsync(ctx));

        Assert.Contains("NATA PADEL TOUR", corpo);   // a lista não veio vazia por acidente
        Assert.DoesNotContain("Ana Krüger", corpo);
        Assert.DoesNotContain("Zappelini", corpo);
        Assert.DoesNotContain("12345678901", corpo);
        Assert.DoesNotContain("exemplo.com", corpo);
    }

    // Nem a CONTAGEM de inscritos. "Só um totalzinho" é como um escopo combinado vira outro sem
    // ninguém decidir nada — e quantas duplas um torneio tem é informação do organizador.
    [Fact]
    public void A_linha_do_torneio_nao_tem_campo_de_inscritos()
    {
        var campos = typeof(TorneiosParaOParceiroDoRanking.TorneioPublicado)
            .GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain(campos, nome =>
            nome.Contains("Inscrito", StringComparison.OrdinalIgnoreCase)
            || nome.Contains("Dupla", StringComparison.OrdinalIgnoreCase)
            || nome.Contains("Jogador", StringComparison.OrdinalIgnoreCase)
            || nome.Contains("Atleta", StringComparison.OrdinalIgnoreCase));
    }

    // ── A porta continua aberta quando o site fecha ────────────────────────────────────────
    //
    // O portão de acesso antecipado responde 302 pra uma tela de senha. Quem chama aqui é um
    // servidor: ele não tem cookie, não tem conta e não tem onde digitar — do lado deles isso
    // apareceria como "o Padelizou mandou HTML no lugar do JSON".
    [Fact]
    public void O_portao_de_acesso_antecipado_nao_fecha_a_porta_do_parceiro()
    {
        Assert.True(AcessoAntecipadoMiddleware.EhCaminhoLiberado("/api/ranking/torneios"));

        // E o que foi liberado é SÓ esta família, não "/api" inteiro: a próxima API que este
        // projeto ganhar não pode nascer fora do portão de graça.
        Assert.False(AcessoAntecipadoMiddleware.EhCaminhoLiberado("/api/outra-coisa"));
    }
}
