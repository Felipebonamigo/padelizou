using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// QUEM JOGA O TORNEIO, NA TABELA DE PALPITEIROS (12/09/2026).
//
// 🗣️ Felipe, com o print da aba Palpiteiros do 2ª Etapa ER PADEL TOUR aberta no computador:
// *"aqui no palpitometro, coloque um filtro, para ver se a pessoa esta jogando o torneio ou
// nao"*.
//
// 🎯 O QUE ISSO RESPONDE: numa tabela de 30+ palpiteiros, "quem está na minha frente" são duas
// perguntas diferentes — quem está jogando (adversário, que também palpita) e quem só assiste.
// O selo responde por linha; os três botões isolam o grupo.
//
// ⚠️ A RÉGUA DE "JOGA" É A MESMA DO `EmQuadraAsync`, e não uma nova: os jogadores das duplas
// NÃO-time do torneio. Num time, o `Jogador1Id` é o ORGANIZADOR que cadastrou (coluna NOT
// NULL) — marcá-lo como jogador viraria a mesma pessoa "jogando" em todo torneio de times que
// ela cadastrou. Duas réguas pra mesma pergunta é como uma delas passa a discordar da outra.
//
// ⚠️ LISTA DE ESPERA FICA DE FORA (escolha do Felipe entre três saídas): quem está na espera
// ainda não joga, e marcá-lo como jogando promete um adversário que pode nunca entrar em
// quadra. Quem entrou SOZINHO, ainda procurando parceiro, conta — ele já entra no sorteio (ver
// Dupla.Jogador2Id e Services/JanelaDoParceiro).
public class QuemJogaOTorneioNoPalpitometroTests
{
    // ─────────────────── 1. A RÉGUA, CONTRA O BANCO ───────────────────

    [Fact]
    public async Task Inscrito_no_torneio_vem_marcado_como_jogando()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, partida) = await MontarJogoAgendadoAsync(ctx);
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();
        var inscrito = duplas[0].Jogador1Id;

        // Ele palpita num jogo que NÃO é dele, senão o palpite não entra na conta.
        await PalpitarAsync(ctx, partida, inscrito, duplas[2].Id);

        var ranking = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, olhandoId: null);

        var linha = Assert.Single(ranking!.Linhas);
        Assert.Equal(inscrito, linha.JogadorId);
        Assert.True(linha.JogaOTorneio);
    }

    [Fact]
    public async Task Torcedor_sem_inscricao_vem_marcado_como_de_fora()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, partida) = await MontarJogoAgendadoAsync(ctx);
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();

        var torcedor = await NovoTorcedorAsync(ctx, "Só Assiste", "55520000001");
        await PalpitarAsync(ctx, partida, torcedor.Id, duplas[2].Id);

        var ranking = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, olhandoId: null);

        var linha = Assert.Single(ranking!.Linhas);
        Assert.False(linha.JogaOTorneio);
    }

    [Fact]
    public async Task Quem_esta_na_LISTA_DE_ESPERA_nao_conta_como_jogando()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, partida) = await MontarJogoAgendadoAsync(ctx);
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();

        var esperando = await NovoTorcedorAsync(ctx, "Na Fila", "55520000002");
        ctx.Duplas.Add(new Dupla
        {
            CategoriaId = categoria.Id,
            Jogador1Id = esperando.Id,
            EmListaDeEspera = true,
        });
        await ctx.SaveChangesAsync();
        await PalpitarAsync(ctx, partida, esperando.Id, duplas[2].Id);

        var ranking = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, olhandoId: null);

        var linha = Assert.Single(ranking!.Linhas);
        Assert.Equal(esperando.Id, linha.JogadorId);

        // ⚠️ A espera não é vaga: prometer "jogando" a quem pode não entrar em quadra é pior
        // que não dizer nada, porque quem lê a tabela decide com base nisso.
        Assert.False(linha.JogaOTorneio);
    }

    [Fact]
    public async Task Em_categoria_de_TIMES_quem_CADASTROU_o_time_nao_vira_jogador()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, partida) = await MontarJogoAgendadoAsync(ctx);
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();

        var quemCadastrou = await NovoTorcedorAsync(ctx, "Dono do Time", "55520000003");
        ctx.Duplas.Add(new Dupla
        {
            CategoriaId = categoria.Id,
            Jogador1Id = quemCadastrou.Id,
            NomeTime = "Time do Bairro",
        });
        await ctx.SaveChangesAsync();
        await PalpitarAsync(ctx, partida, quemCadastrou.Id, duplas[2].Id);

        var ranking = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, olhandoId: null);

        var linha = Assert.Single(ranking!.Linhas);

        // ⚠️ Mesma régua do `EmQuadraAsync`: na linha de TIME o Jogador1 é quem cadastrou, e
        // ele pode nem estar na quadra. Sem esta exceção, o organizador de um time apareceria
        // "jogando" em todo torneio que ele inscreveu um time.
        Assert.False(linha.JogaOTorneio);
    }

    [Fact]
    public async Task Inscrito_SOZINHO_ainda_sem_parceiro_JA_conta_como_jogando()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, partida) = await MontarJogoAgendadoAsync(ctx);
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();

        var sozinho = await NovoTorcedorAsync(ctx, "Procura Parceiro", "55520000004");
        ctx.Duplas.Add(new Dupla { CategoriaId = categoria.Id, Jogador1Id = sozinho.Id });
        await ctx.SaveChangesAsync();
        await PalpitarAsync(ctx, partida, sozinho.Id, duplas[2].Id);

        var ranking = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, olhandoId: null);

        var linha = Assert.Single(ranking!.Linhas);

        // A dupla incompleta OCUPA a vaga e entra no sorteio desde 09/09/2026 — ele joga.
        Assert.True(linha.JogaOTorneio);
    }

    [Fact]
    public async Task O_PARCEIRO_da_dupla_tambem_conta_nao_so_o_Jogador1()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, partida) = await MontarJogoAgendadoAsync(ctx);
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();
        var parceiro = duplas[0].Jogador2Id;

        Assert.NotNull(parceiro);
        await PalpitarAsync(ctx, partida, parceiro!.Value, duplas[2].Id);

        var ranking = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, olhandoId: null);

        var linha = Assert.Single(ranking!.Linhas);
        Assert.True(linha.JogaOTorneio);
    }

    [Fact]
    public async Task Inscricao_em_OUTRO_torneio_nao_marca_ninguem()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, partida) = await MontarJogoAgendadoAsync(ctx);
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();

        // Um torneio à parte, com as duplas dele. Quem joga LÁ palpita AQUI.
        var (_, outraCategoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2);
        var deOutroTorneio = ctx.Duplas.First(d => d.CategoriaId == outraCategoria.Id).Jogador1Id;
        await PalpitarAsync(ctx, partida, deOutroTorneio, duplas[2].Id);

        var ranking = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, olhandoId: null);

        var linha = Assert.Single(ranking!.Linhas);
        Assert.False(linha.JogaOTorneio);
    }

    // ─────────────────── 2. A CONSULTA VIRA SQL DE VERDADE ───────────────────
    //
    // ⚠️ O InMemory do resto da suíte NÃO TRADUZ NADA (CLAUDE.md), e esta consulta atravessa a
    // navegação `Categoria` pra chegar no `TorneioId` — exatamente o tipo de consulta que passa
    // verde aqui e responde 500 na primeira visita. Mesmo padrão de
    // TraducaoDasConsultasDePalpiteTests.
    [Fact]
    public void A_consulta_de_quem_joga_o_torneio_vira_SQL()
    {
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
            .Options;
        using var ctx = new DbPadelContext(options);

        var sql = RankingDePalpiteiros.ConsultaDeQuemJoga(ctx, torneioId: 1).ToQueryString();

        Assert.Contains("SELECT", sql);
    }

    // ─────────────────── 3. O FILTRO SÓ APARECE QUANDO SERVE ───────────────────

    private static PalpiteiroNoRanking Linha(int id, bool joga) =>
        new() { JogadorId = id, Nome = "P" + id, JogaOTorneio = joga };

    private static TabelaDePalpiteirosVM Tabela(int? torneioId, params bool[] jogam) =>
        new(jogam.Select((joga, i) => Linha(i + 1, joga)).ToList(), MeuId: null,
            MostrarCravadas: false, MostrarEmAberto: false, MostrarApuracao: true, TorneioId: torneioId);

    [Fact]
    public void O_filtro_aparece_quando_existem_os_DOIS_grupos()
    {
        Assert.True(Tabela(torneioId: 26, true, false).MostrarQuemJoga);
    }

    [Fact]
    public void Com_TODOS_jogando_o_filtro_some()
    {
        // Três botões em que dois dão a mesma tabela e um dá tabela vazia. E o selo em TODA
        // linha não distingue nada — é tinta em cima de informação que não existe.
        Assert.False(Tabela(torneioId: 26, true, true).MostrarQuemJoga);
    }

    [Fact]
    public void Com_NINGUEM_jogando_o_filtro_some()
    {
        Assert.False(Tabela(torneioId: 26, false, false).MostrarQuemJoga);
    }

    [Fact]
    public void No_HUB_do_ranking_o_filtro_nunca_aparece()
    {
        // ⚠️ Lá a tabela soma VÁRIOS torneios (TorneioId nulo) e "joga o torneio" não tem
        // sujeito — qual torneio? É a mesma pergunta feita ao dado que já decide se o nome
        // abre o modal dos palpites.
        Assert.False(Tabela(torneioId: null, true, false).MostrarQuemJoga);
    }

    // ─────────────────── 4. O QUE A TELA DESENHA ───────────────────

    private static string Tela() => TestInfra.SemComentarios(
        File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "Views", "Shared", "_TabelaDePalpiteiros.cshtml")));

    [Fact]
    public void A_linha_da_tabela_carrega_o_data_joga_que_o_JS_filtra()
    {
        Assert.Contains("data-joga=", Tela());
    }

    [Fact]
    public void Quem_joga_ganha_o_selo_na_linha()
    {
        Assert.Contains("pdz-selo-jogando", Tela());
    }

    [Fact]
    public void O_selo_tem_regra_de_verdade_no_site_css()
    {
        // Classe sem regra é peso morto que a próxima sessão lê como estilo existente.
        var css = File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "wwwroot", "css", "site.css"));

        Assert.Contains(".pdz-selo-jogando", css);
    }

    [Fact]
    public void Os_TRES_botoes_do_filtro_existem()
    {
        var tela = Tela();

        Assert.Contains("data-filtro=\"todos\"", tela);
        Assert.Contains("data-filtro=\"jogando\"", tela);
        Assert.Contains("data-filtro=\"fora\"", tela);
    }

    // 🕳️ VISTO NO CHROMIUM, a 430px, ANTES de publicar (12/09/2026): a contagem dentro dos
    // botões estava em `text-body-secondary`, e o botão ATIVO do `btn-outline-secondary` pinta
    // o fundo de cinza escuro. Resultado: "Todos(7)" virava cinza sobre cinza — o número
    // desaparecia exatamente no botão que está valendo. Nenhum teste da suíte pegaria isso.
    //
    // A correção é a contagem HERDAR a cor do botão, que já muda com o estado. Por isso o
    // guarda é pela AUSÊNCIA: classe de cor fixa aqui volta a brigar com o fundo.
    [Fact]
    public void A_contagem_nos_botoes_do_filtro_nao_fixa_cor()
    {
        var botoes = Regex.Match(Tela(), @"aria-label=""Filtrar palpiteiros"">.*?</div>",
            RegexOptions.Singleline);

        Assert.True(botoes.Success, "não achei o bloco dos botões do filtro");
        Assert.DoesNotContain("text-body-secondary", botoes.Value);
        Assert.DoesNotContain("text-muted", botoes.Value);

        // ⚠️ E NEM OPACIDADE. Foi a primeira correção, e ela também foi MEDIDA no Chromium a
        // 430px: `opacity-75` deixava a contagem em 3,02:1 no botão inativo e 3,43:1 no ativo,
        // contra o mínimo de 4,5:1 do WCAG AA pra texto pequeno. Cor cheia do botão é o que
        // acompanha os dois estados sem pagar contraste.
        Assert.DoesNotContain("opacity-", botoes.Value);
    }

    [Fact]
    public void O_filtro_e_a_tabela_ficam_dentro_do_MESMO_escopo()
    {
        // É por ele que o JS acha as linhas a partir do botão clicado. Sem o escopo, duas
        // tabelas na mesma página (o hub tem abas) se filtrariam uma à outra.
        Assert.Contains("pdz-palpiteiros", Tela());
    }

    [Fact]
    public void O_JS_do_filtro_e_carregado_pela_propria_partial()
    {
        Assert.Contains("filtro-de-palpiteiros.js", Tela());

        Assert.True(File.Exists(Path.Combine(RaizDoRepo(), "Padelizou", "wwwroot", "js", "filtro-de-palpiteiros.js")),
            "não achei o wwwroot/js/filtro-de-palpiteiros.js");
    }

    // ─────────────────── A INFRA ───────────────────

    private static async Task<(Torneio torneio, Categoria categoria, Partida partida)>
        MontarJogoAgendadoAsync(DbPadelContext ctx)
    {
        // ⚠️ QUATRO duplas, e o jogo entre as DUAS ÚLTIMAS. Com duas duplas só, todo inscrito
        // estaria EM QUADRA na única partida — e quem joga a partida não entra na conta dela
        // (regra de sempre do ranking), então a tabela vinha VAZIA e o teste media outra coisa.
        // Foi o vermelho que estes testes deram primeiro, e ele era do cenário, não do código.
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        torneio.AprovadoEm = DateTime.Now;
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();

        var partida = new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Dupla1Id = duplas[2].Id,
            Dupla2Id = duplas[3].Id,
            Status = "Agendada",
            Fase = "Grupos",
            Codigo = "P1",
        };
        ctx.Partidas.Add(partida);
        await ctx.SaveChangesAsync();

        return (torneio, categoria, partida);
    }

    private static async Task<Jogador> NovoTorcedorAsync(DbPadelContext ctx, string nome, string cpf)
    {
        var torcedor = new Jogador { Nome = nome, Cpf = cpf };
        ctx.Jogadores.Add(torcedor);
        await ctx.SaveChangesAsync();
        return torcedor;
    }

    private static async Task PalpitarAsync(DbPadelContext ctx, Partida partida, int jogadorId, int duplaId)
    {
        ctx.PalpitesPartida.Add(new PalpitePartida
        {
            PartidaId = partida.Id,
            JogadorId = jogadorId,
            DuplaEscolhidaId = duplaId,
        });
        await ctx.SaveChangesAsync();
    }

    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a raiz do repositório a partir de " + AppContext.BaseDirectory);
    }
}
