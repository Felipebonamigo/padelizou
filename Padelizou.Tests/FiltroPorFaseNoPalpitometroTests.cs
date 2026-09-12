using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O PALPITÔMETRO POR FASE (12/09/2026).
//
// 🗣️ Felipe: *"também no palpitometro, colocar um filtro 'por chaves' 'Por mata mata' 'Apenas
// finais'"*. Perguntado, confirmou que "por chaves" é a FASE DE GRUPOS (não por categoria) e
// que cada recorte é um RANKING PRÓPRIO, com 1º, 2º, 3º e pódio dele.
//
// ⚠️ ESTE FILTRO NÃO É O IRMÃO DELE. O "jogando / de fora" esconde linha no navegador; este
// MUDA OS NÚMEROS — pontos, acertos e aproveitamento de "só o mata-mata" são outra apuração.
// Por isso ele recarrega a página com `fasePalpiteiros=` e a conta é refeita no servidor.
// Esconder linha aqui mostraria o ponto do torneio inteiro debaixo do rótulo de uma fase.
//
// ⚠️ A RÉGUA DO GRUPO É A QUE JÁ EXISTIA (`FasesTorneio.EhFaseDeGrupos`), e ela conhece as DUAS
// formas gravadas no banco: os seeds antigos gravaram "Fase de Grupos" e o `GerarChaves` atual
// grava "Grupo A"/"Grupo B". Uma régua nova aqui deixaria o torneio antigo fora do recorte —
// caladinho, porque a tabela continuaria desenhando.
public class FiltroPorFaseNoPalpitometroTests
{
    // ─────────────────── 1. A RÉGUA DE CADA RECORTE ───────────────────

    private static bool Pega(string recorte, string fase) =>
        FaseDoPalpitometro.Filtro(torneioId: 1, recorte).Compile()(
            new Partida { TorneioId = 1, Fase = fase });

    [Fact]
    public void GRUPOS_pega_as_DUAS_formas_gravadas_no_banco()
    {
        Assert.True(Pega(FaseDoPalpitometro.Grupos, "Fase de Grupos"));   // seed antigo
        Assert.True(Pega(FaseDoPalpitometro.Grupos, "Grupo A"));          // GerarChaves atual
        Assert.True(Pega(FaseDoPalpitometro.Grupos, "Grupo D"));

        Assert.False(Pega(FaseDoPalpitometro.Grupos, "Semifinal"));
        Assert.False(Pega(FaseDoPalpitometro.Grupos, "Final"));
    }

    [Fact]
    public void MATA_MATA_e_tudo_que_NAO_e_grupo()
    {
        Assert.True(Pega(FaseDoPalpitometro.MataMata, "Oitavas de Final"));
        Assert.True(Pega(FaseDoPalpitometro.MataMata, "Quartas de Final"));
        Assert.True(Pega(FaseDoPalpitometro.MataMata, "Semifinal"));
        Assert.True(Pega(FaseDoPalpitometro.MataMata, ChaveamentoMataMata.PrimeiraRodada));

        // ⚠️ A FINAL ESTÁ DENTRO DO MATA-MATA, e é de propósito: ela é a última fase da chave.
        // "Apenas finais" é um recorte MAIS ESTREITO, não um irmão que a exclui daqui.
        Assert.True(Pega(FaseDoPalpitometro.MataMata, "Final"));

        Assert.False(Pega(FaseDoPalpitometro.MataMata, "Fase de Grupos"));
        Assert.False(Pega(FaseDoPalpitometro.MataMata, "Grupo B"));
    }

    [Fact]
    public void APENAS_FINAIS_pega_so_a_Final()
    {
        Assert.True(Pega(FaseDoPalpitometro.Finais, "Final"));

        Assert.False(Pega(FaseDoPalpitometro.Finais, "Semifinal"));
        Assert.False(Pega(FaseDoPalpitometro.Finais, "Grupo A"));
    }

    [Fact]
    public void TUDO_pega_qualquer_fase_e_e_o_padrao()
    {
        Assert.True(Pega(FaseDoPalpitometro.Tudo, "Grupo A"));
        Assert.True(Pega(FaseDoPalpitometro.Tudo, "Final"));

        // ⚠️ O recorte vem da QUERY STRING, ou seja, de fora: `?fasePalpiteiros=xpto` não pode
        // virar tabela vazia nem exceção — cai no padrão, que é o torneio inteiro.
        Assert.Equal(FaseDoPalpitometro.Tudo, FaseDoPalpitometro.Normalizar("xpto"));
        Assert.Equal(FaseDoPalpitometro.Tudo, FaseDoPalpitometro.Normalizar(null));
        Assert.Equal(FaseDoPalpitometro.Tudo, FaseDoPalpitometro.Normalizar(""));
        Assert.Equal(FaseDoPalpitometro.Grupos, FaseDoPalpitometro.Normalizar("grupos"));
    }

    [Fact]
    public void O_recorte_NAO_atravessa_torneio()
    {
        // A fase é a mesma; o torneio não. Sem o TorneioId no filtro, o recorte somaria o
        // "Grupo A" de todos os torneios do site.
        Assert.False(FaseDoPalpitometro.Filtro(torneioId: 1, FaseDoPalpitometro.Grupos).Compile()(
            new Partida { TorneioId = 2, Fase = "Grupo A" }));
    }

    // ─────────────────── 2. A CONTA MUDA, CONTRA O BANCO ───────────────────

    [Fact]
    public async Task O_recorte_de_GRUPOS_soma_so_os_jogos_de_grupo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, grupo, final) = await MontarDoisJogosAsync(ctx);

        // Cravou nos dois (3 pontos em cada, 6 no torneio inteiro).
        var torcedor = await NovoTorcedorAsync(ctx, "Cravou Tudo", "55530000001");
        await PalpitarAsync(ctx, grupo, torcedor.Id, grupo.Dupla1Id, 6, 4);
        await PalpitarAsync(ctx, final, torcedor.Id, final.Dupla1Id, 6, 4);

        var tudo = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, null);
        var grupos = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, null, FaseDoPalpitometro.Grupos);

        Assert.Equal(6, Assert.Single(tudo!.Linhas).Pontos);
        Assert.Equal(2, tudo.JogosApurados);

        Assert.Equal(3, Assert.Single(grupos!.Linhas).Pontos);
        Assert.Equal(1, grupos.JogosApurados);
        Assert.Equal(1, Assert.Single(grupos.Linhas).Palpites);
    }

    [Fact]
    public async Task Cada_recorte_tem_RANKING_PROPRIO_com_posicao_recalculada()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, grupo, final) = await MontarDoisJogosAsync(ctx);

        // ⚠️ OS NÚMEROS SÃO ESCOLHIDOS PRA A ORDEM INVERTER — foi o vermelho que este teste deu
        // primeiro, e era do teste: eu tinha empatado os dois em 4, e empate não demonstra
        // inversão nenhuma. O "Rei dos Grupos" crava o grupo (3) e acerta só o vencedor da
        // final (1) = 4; o "Rei da Final" erra o grupo (0) e crava a final (3) = 3.
        var reiDosGrupos = await NovoTorcedorAsync(ctx, "Rei dos Grupos", "55530000002");
        await PalpitarAsync(ctx, grupo, reiDosGrupos.Id, grupo.Dupla1Id, 6, 4);      // cravou:   3
        await PalpitarAsync(ctx, final, reiDosGrupos.Id, final.Dupla1Id, 6, 0);      // vencedor: 1

        var reiDaFinal = await NovoTorcedorAsync(ctx, "Rei da Final", "55530000003");
        await PalpitarAsync(ctx, grupo, reiDaFinal.Id, grupo.Dupla2Id, 4, 6);        // errou:  0
        await PalpitarAsync(ctx, final, reiDaFinal.Id, final.Dupla1Id, 6, 4);        // cravou: 3

        var tudo = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, null);
        var finais = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, null, FaseDoPalpitometro.Finais);

        // ⚠️ COMPARA POR ID, não por nome: o nome na tela passa pelo `NomeBonito` (que desde
        // 12/09/2026 mostra primeiro e último, sem os do meio), e "Rei dos Grupos" chega como
        // "Rei Grupos". Este teste é sobre ORDEM — travar a formatação do nome aqui o quebraria
        // no dia em que aquela régua mudasse, por um motivo que não tem nada a ver com fase.
        // No torneio inteiro o Rei dos Grupos está na FRENTE, com 4 contra 3.
        Assert.Equal(new[] { reiDosGrupos.Id, reiDaFinal.Id }, tudo!.Linhas.Select(l => l.JogadorId));
        Assert.Equal(new[] { 4, 3 }, tudo.Linhas.Select(l => l.Pontos));

        // 🎯 E NA FINAL A ORDEM VIRA: o 2º do torneio é o 1º daqui, 3 contra 1.
        Assert.Equal(new[] { reiDaFinal.Id, reiDosGrupos.Id }, finais!.Linhas.Select(l => l.JogadorId));
        Assert.Equal(new[] { 1, 2 }, finais.Linhas.Select(l => l.Posicao));
        Assert.Equal(new[] { 3, 1 }, finais.Linhas.Select(l => l.Pontos));
    }

    [Fact]
    public async Task O_que_esta_EM_ABERTO_tambem_respeita_o_recorte()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, grupo, final) = await MontarDoisJogosAsync(ctx);

        // A final volta a ser jogo agendado: o palpite nela fica em aberto.
        final.Status = "Agendada";
        final.VencedorId = null;
        final.GamesDupla1 = null;
        final.GamesDupla2 = null;
        await ctx.SaveChangesAsync();

        var torcedor = await NovoTorcedorAsync(ctx, "Palpita nos Dois", "55530000004");
        await PalpitarAsync(ctx, grupo, torcedor.Id, grupo.Dupla1Id, 6, 4);
        await PalpitarAsync(ctx, final, torcedor.Id, final.Dupla1Id, 6, 4);

        var grupos = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, null, FaseDoPalpitometro.Grupos);
        var finais = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, null, FaseDoPalpitometro.Finais);

        // No recorte dos grupos o palpite da final não aparece nem como pendente.
        Assert.Equal(0, Assert.Single(grupos!.Linhas).EmAberto);
        Assert.Equal(3, Assert.Single(grupos.Linhas).Pontos);

        // E no recorte da final só existe o pendente — a fase ainda não pontuou ninguém.
        Assert.Equal(1, Assert.Single(finais!.Linhas).EmAberto);
        Assert.True(finais.ModoParticipacao);
    }

    // ─────────────────── 3. OS BOTÕES SAEM DO DADO ───────────────────

    [Fact]
    public async Task Torneio_que_so_teve_GRUPOS_nao_oferece_mata_mata_nem_finais()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, grupo, final) = await MontarDoisJogosAsync(ctx);

        // Apaga a final: sobra um torneio que não saiu da fase de grupos.
        ctx.Partidas.Remove(final);
        await ctx.SaveChangesAsync();

        var ranking = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, null);

        Assert.Equal(new[] { FaseDoPalpitometro.Tudo, FaseDoPalpitometro.Grupos }, ranking!.RecortesComJogo);

        // ⚠️ E AÍ O FILTRO NÃO APARECE: "Tudo" e "Grupos" dariam a MESMA tabela, e dois botões
        // pra uma resposta só é tela pedindo escolha que não existe.
        Assert.False(ranking.MostrarFiltroDeFase);
    }

    [Fact]
    public async Task Torneio_com_grupos_e_final_oferece_os_tres_recortes()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _, _) = await MontarDoisJogosAsync(ctx);

        var ranking = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, null);

        Assert.Equal(
            new[] { FaseDoPalpitometro.Tudo, FaseDoPalpitometro.Grupos, FaseDoPalpitometro.MataMata, FaseDoPalpitometro.Finais },
            ranking!.RecortesComJogo);
        Assert.True(ranking.MostrarFiltroDeFase);
    }

    [Fact]
    public async Task O_recorte_escolhido_volta_no_modelo_pra_tela_marcar_o_botao()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _, _) = await MontarDoisJogosAsync(ctx);

        var ranking = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, null, FaseDoPalpitometro.MataMata);

        Assert.Equal(FaseDoPalpitometro.MataMata, ranking!.Recorte);
    }

    // ─────────────────── 4. AS CONSULTAS VIRAM SQL ───────────────────
    //
    // ⚠️ O InMemory da suíte não traduz nada (CLAUDE.md), e estes predicados usam `StartsWith`
    // — é o caminho clássico de passar verde aqui e responder 500 no Postgres.
    [Theory]
    [InlineData(FaseDoPalpitometro.Tudo)]
    [InlineData(FaseDoPalpitometro.Grupos)]
    [InlineData(FaseDoPalpitometro.MataMata)]
    [InlineData(FaseDoPalpitometro.Finais)]
    public void O_filtro_de_cada_recorte_vira_SQL(string recorte)
    {
        var options = new DbContextOptionsBuilder<DbPadelContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=nao_existe;Username=x;Password=x")
            .Options;
        using var ctx = new DbPadelContext(options);

        var filtro = FaseDoPalpitometro.Filtro(torneioId: 1, recorte);

        Assert.Contains("SELECT", RankingDePalpiteiros.ConsultaDePartidas(ctx, filtro).ToQueryString());
        Assert.Contains("SELECT", RankingDePalpiteiros.ConsultaDePartidasEmAberto(ctx, filtro).ToQueryString());
    }

    // ─────────────────── 5. O QUE A TELA DESENHA ───────────────────

    private static string Tela() => TestInfra.SemComentarios(
        File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "Views", "Shared", "_RankingDePalpiteiros.cshtml")));

    [Fact]
    public void Os_botoes_do_recorte_sao_LINK_e_nao_botao_de_JS()
    {
        // Recarregam a página com o recorte — a conta é do servidor. Um `onclick` aqui
        // prometeria filtro instantâneo e entregaria número do torneio inteiro.
        var tela = Tela();

        Assert.Contains("fasePalpiteiros", tela);
        Assert.Contains("pdz-filtro-de-fase", tela);
    }

    // 🕳️ VISTO NA TELA, no Chromium, antes de publicar: a frase que avisa do recorte usava o
    // RÓTULO DO BOTÃO e saía *"Só apenas finais — ..."*. Rótulo de botão e texto corrido são
    // duas escritas diferentes da mesma coisa, e o botão precisa do "Apenas" pra se distinguir
    // do "Mata-mata" ao lado.
    [Fact]
    public void A_frase_do_recorte_nao_repete_o_ROTULO_do_botao()
    {
        Assert.Equal("as finais", FaseDoPalpitometro.NaFrase(FaseDoPalpitometro.Finais));
        Assert.Equal("os jogos de grupo", FaseDoPalpitometro.NaFrase(FaseDoPalpitometro.Grupos));
        Assert.Equal("os jogos do mata-mata", FaseDoPalpitometro.NaFrase(FaseDoPalpitometro.MataMata));

        // E a tela usa ESTA, não o rótulo.
        Assert.Contains("NaFrase", Tela());
        Assert.DoesNotContain("Rotulo(Model.Recorte)", Tela());
    }

    [Fact]
    public void A_ANCORA_da_aba_entra_no_link_quando_a_pagina_manda()
    {
        // Sem ela, filtrar dentro da página do torneio jogaria o leitor de volta pro topo,
        // na primeira aba — longe da tabela que ele estava lendo.
        Assert.Contains("AncoraDosPalpiteiros", Tela());
    }

    [Fact]
    public void A_aba_do_torneio_declara_a_ancora()
    {
        var details = TestInfra.SemComentarios(
            File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "Views", "Torneios", "Details.cshtml")));

        Assert.Contains("AncoraDosPalpiteiros", details);
    }

    [Fact]
    public void As_DUAS_acoes_aceitam_o_recorte()
    {
        var fonte = File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "Controllers", "TorneiosController.cs"));
        var palpiteiros = File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "Controllers", "TorneiosController.Palpiteiros.cs"));

        // A página cheia e a aba usam o MESMO nome de parâmetro: é o que deixa a partial
        // trocar uma chave só, sem saber em qual das duas telas ela está.
        Assert.Contains("fasePalpiteiros", palpiteiros);
        Assert.Contains("fasePalpiteiros", fonte);
    }

    // ─────────────────── A INFRA ───────────────────

    // Um torneio com DOIS jogos terminados 6 x 4: um de grupo e a final.
    private static async Task<(Torneio torneio, Categoria categoria, Partida grupo, Partida final)>
        MontarDoisJogosAsync(DbPadelContext ctx)
    {
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 4);
        torneio.AprovadoEm = DateTime.Now;
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).ToList();

        var grupo = Jogo(torneio, categoria, duplas[0], duplas[1], "Grupo A", "P1");
        var final = Jogo(torneio, categoria, duplas[2], duplas[3], "Final", "P2");
        ctx.Partidas.AddRange(grupo, final);
        await ctx.SaveChangesAsync();

        return (torneio, categoria, grupo, final);
    }

    private static Partida Jogo(Torneio torneio, Categoria categoria, Dupla d1, Dupla d2,
        string fase, string codigo) => new()
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Dupla1Id = d1.Id,
            Dupla2Id = d2.Id,
            VencedorId = d1.Id,
            GamesDupla1 = 6,
            GamesDupla2 = 4,
            Status = "Finalizada",
            Fase = fase,
            Codigo = codigo,
        };

    private static async Task<Jogador> NovoTorcedorAsync(DbPadelContext ctx, string nome, string cpf)
    {
        var torcedor = new Jogador { Nome = nome, Cpf = cpf };
        ctx.Jogadores.Add(torcedor);
        await ctx.SaveChangesAsync();
        return torcedor;
    }

    private static async Task PalpitarAsync(DbPadelContext ctx, Partida partida, int jogadorId, int duplaId,
        int? games1 = null, int? games2 = null)
    {
        ctx.PalpitesPartida.Add(new PalpitePartida
        {
            PartidaId = partida.Id,
            JogadorId = jogadorId,
            DuplaEscolhidaId = duplaId,
            GamesDupla1 = games1,
            GamesDupla2 = games2,
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
