using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Tests;

// 28/09/2026 — A CAMPANHA DO TIME NO TORNEIO.
//
// 🗣️ Felipe, com o print de um concorrente ("time acompanhado: 8 duplas, 22 jogos, 7—15,
// games 123/163, saldo −40"): *"Interessante esse resumo por times no torneio — não podemos
// fazer igual, mas podemos fazer algo parecido"*. A aba Times do torneio só dizia os PONTOS
// de cada time; o que o time fez em quadra (vitórias, derrotas, games) não estava em lugar
// nenhum, e quem quer saber "como está o meu time" somava de cabeça, jogo a jogo.
//
// A campanha do time é a SOMA DAS CAMPANHAS DAS DUPLAS dele — a dupla é do time se qualquer
// um dos dois joga por ele. Duas consequências que os testes travam de propósito, porque são
// as que um "conserto" futuro mudaria sem perceber:
//   • dupla mista (um de cada time) conta pros DOIS times — a mesma régua dos pontos;
//   • confronto interno (duas duplas do mesmo time) é uma vitória E uma derrota do time.
public class CampanhaDoTimeNoTorneioTests
{
    // ── A conta, sem banco ────────────────────────────────────────────────────────────

    private static Time T(int id, string nome) => new() { Id = id, Nome = nome };

    private static Jogador J(int id, Time? time) =>
        new() { Id = id, Nome = $"Jogador {id}", Time = time, TimeId = time?.Id };

    private static Dupla D(int id, Jogador a, Jogador? b) =>
        new() { Id = id, Jogador1 = a, Jogador1Id = a.Id, Jogador2 = b, Jogador2Id = b?.Id };

    private static Partida P(int id, Dupla a, Dupla b, string status,
        int? games1 = null, int? games2 = null, DateTime? horario = null) =>
        new()
        {
            Id = id, Codigo = $"J{id}", Fase = "Fase de Grupos",
            Dupla1 = a, Dupla1Id = a.Id, Dupla2 = b, Dupla2Id = b.Id,
            Status = status, GamesDupla1 = games1, GamesDupla2 = games2, HorarioPrevisto = horario,
        };

    [Fact]
    public void Soma_jogos_vitorias_derrotas_e_games_das_duplas_do_time()
    {
        var nata = T(1, "Nata");
        var rival = T(2, "Rival");
        var a = D(10, J(1, nata), J(2, nata));
        var b = D(11, J(3, nata), J(4, nata));
        var c = D(12, J(5, rival), J(6, rival));
        var d = D(13, J(7, rival), J(8, rival));

        var campanhas = CampanhaDosTimes.Montar(new[] { a, b, c, d }, new[]
        {
            P(100, a, c, "Finalizada", 6, 3),
            P(101, b, d, "Finalizada", 2, 6),
            P(102, d, a, "Finalizada", 4, 6),
        });

        var n = campanhas[nata.Id];
        Assert.Equal("Nata", n.Time);
        Assert.Equal(2, n.Duplas);            // dupla com os dois do time conta UMA vez
        Assert.Equal(3, n.Jogos);
        Assert.Equal(2, n.Vitorias);
        Assert.Equal(1, n.Derrotas);
        Assert.Equal(14, n.GamesPro);
        Assert.Equal(13, n.GamesContra);
        Assert.Equal(1, n.Saldo);
        Assert.Equal(67, n.Aproveitamento);

        var r = campanhas[rival.Id];
        Assert.Equal(1, r.Vitorias);
        Assert.Equal(2, r.Derrotas);
        Assert.Equal(-1, r.Saldo);
    }

    [Fact]
    public void Dupla_mista_conta_pros_dois_times()
    {
        var nata = T(1, "Nata");
        var rival = T(2, "Rival");
        var mista = D(10, J(1, nata), J(2, rival));
        var semTime = D(11, J(3, null), J(4, null));

        var campanhas = CampanhaDosTimes.Montar(new[] { mista, semTime },
            new[] { P(100, mista, semTime, "Finalizada", 6, 0) });

        Assert.Equal(1, campanhas[nata.Id].Vitorias);
        Assert.Equal(1, campanhas[rival.Id].Vitorias);
        // Quem não joga por time nenhum não vira uma linha "sem time".
        Assert.Equal(2, campanhas.Count);
    }

    [Fact]
    public void Confronto_interno_e_uma_vitoria_e_uma_derrota_do_time()
    {
        var nata = T(1, "Nata");
        var a = D(10, J(1, nata), J(2, nata));
        var b = D(11, J(3, nata), J(4, nata));

        var n = CampanhaDosTimes.Montar(new[] { a, b },
            new[] { P(100, a, b, "Finalizada", 6, 2) })[nata.Id];

        Assert.Equal(2, n.Jogos);
        Assert.Equal(1, n.Vitorias);
        Assert.Equal(1, n.Derrotas);
        Assert.Equal(0, n.Saldo);
    }

    [Fact]
    public void Dupla_time_lista_de_espera_e_quem_esta_sem_parceiro_nao_contam()
    {
        var nata = T(1, "Nata");
        var valendo = D(10, J(1, nata), J(2, nata));
        var semParceiro = D(11, J(3, nata), null);
        var naEspera = D(12, J(4, nata), J(5, nata));
        naEspera.EmListaDeEspera = true;
        // A dupla-TIME tem o ORGANIZADOR como Jogador1 — contá-la poria a campanha do time
        // inteiro na conta do time do organizador (mesma régua dos pontos).
        var duplaTime = D(13, J(6, nata), null);
        duplaTime.NomeTime = "Nata Padel";

        var n = CampanhaDosTimes.Montar(new[] { valendo, semParceiro, naEspera, duplaTime },
            Array.Empty<Partida>())[nata.Id];

        Assert.Equal(1, n.Duplas);
    }

    [Fact]
    public void Jogo_que_nao_acabou_fica_fora_da_conta_e_vira_o_proximo_jogo()
    {
        var nata = T(1, "Nata");
        var rival = T(2, "Rival");
        var a = D(10, J(1, nata), J(2, nata));
        var b = D(11, J(3, nata), J(4, nata));
        var c = D(12, J(5, rival), J(6, rival));

        var cedo = P(100, a, c, "Agendada", horario: new DateTime(2026, 9, 28, 9, 0, 0));
        var tarde = P(101, b, c, "Agendada", horario: new DateTime(2026, 9, 28, 15, 0, 0));
        // Placar parcial de jogo em quadra NÃO é resultado.
        var emQuadra = P(102, b, c, "AoVivo", 5, 1, new DateTime(2026, 9, 28, 16, 0, 0));

        var n = CampanhaDosTimes.Montar(new[] { a, b, c }, new[] { tarde, cedo, emQuadra })[nata.Id];

        Assert.Equal(0, n.Jogos);
        Assert.Equal(0, n.GamesPro);
        Assert.Null(n.Aproveitamento);
        Assert.Equal(2, n.DuplasComJogoMarcado);
        // O jogo EM QUADRA vem antes de qualquer agendado, mesmo marcado pra mais tarde.
        Assert.Same(emQuadra, n.ProximoJogo);
    }

    [Fact]
    public void Sem_jogo_em_quadra_o_proximo_e_o_mais_cedo_e_sem_horario_vai_pro_fim()
    {
        var nata = T(1, "Nata");
        var a = D(10, J(1, nata), J(2, nata));
        var c = D(12, J(5, null), J(6, null));

        var semHora = P(100, a, c, "Agendada");
        var tarde = P(101, a, c, "Agendada", horario: new DateTime(2026, 9, 28, 15, 0, 0));
        var cedo = P(102, a, c, "Agendada", horario: new DateTime(2026, 9, 28, 9, 0, 0));

        var n = CampanhaDosTimes.Montar(new[] { a, c }, new[] { semHora, tarde, cedo })[nata.Id];

        Assert.Same(cedo, n.ProximoJogo);
        Assert.Equal(1, n.DuplasComJogoMarcado);
    }

    [Fact]
    public void A_tabela_ordena_por_pontos_depois_vitorias_depois_saldo()
    {
        var campanhas = new Dictionary<int, CampanhaDosTimes.Campanha>
        {
            [1] = new(1, "Alfa", null, 2, 4, 1, 3, 10, 20, 0, null),
            [2] = new(2, "Beta", null, 2, 4, 3, 1, 20, 10, 0, null),
            [3] = new(3, "Gama", null, 2, 4, 3, 1, 22, 10, 0, null),
        };

        var comPontos = CampanhaDosTimes.Tabela(campanhas, new[]
        {
            new PontosTimeTorneioVM { TimeId = 1, Pontos = 90 },
            new PontosTimeTorneioVM { TimeId = 2, Pontos = 50 },
            new PontosTimeTorneioVM { TimeId = 3, Pontos = 50 },
        });
        Assert.Equal(new[] { "Alfa", "Gama", "Beta" }, comPontos.Select(l => l.Campanha.Time));
        Assert.Equal(90, comPontos[0].Pontos);

        // Torneio que não pontua (restrito, interno de time): a campanha continua valendo, e a
        // linha diz "sem pontos" em vez de inventar um zero.
        var semPontos = CampanhaDosTimes.Tabela(campanhas, Array.Empty<PontosTimeTorneioVM>());
        Assert.Equal(new[] { "Gama", "Beta", "Alfa" }, semPontos.Select(l => l.Campanha.Time));
        Assert.All(semPontos, l => Assert.Null(l.Pontos));
    }

    // ── A página do torneio ───────────────────────────────────────────────────────────

    private static async Task<(DbPadelContext Ctx, Torneio Torneio, Time Nata, int OrgId, int JogadorDaNata)>
        ComAsChavesSorteadasAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        torneio.QuantidadeQuadras = 2;
        torneio.TempoPrevistoPartidaMinutos = 30;
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Q1" });
        ctx.Quadras.Add(new Quadra { TorneioId = torneio.Id, Nome = "Q2" });

        var nata = new Time { Nome = "Nata" };
        ctx.Times.Add(nata);
        await ctx.SaveChangesAsync();

        var primeira = await ctx.Duplas.Include(d => d.Jogador1).Include(d => d.Jogador2)
            .Where(d => d.CategoriaId == categoria.Id).OrderBy(d => d.Id).FirstAsync();
        primeira.Jogador1.TimeId = nata.Id;
        primeira.Jogador2!.TimeId = nata.Id;
        await ctx.SaveChangesAsync();

        await TestInfra.NovoTorneiosController(ctx, org.Id).GerarChaves(torneio.Id);
        return (ctx, torneio, nata, org.Id, primeira.Jogador1Id);
    }

    private static async Task<(IReadOnlyDictionary<int, CampanhaDosTimes.Campanha> Campanhas, object? MeuTime)>
        NaTelaAsync(DbPadelContext ctx, int torneioId, int quemOlha)
    {
        var view = (ViewResult)await TestInfra.NovoTorneiosController(ctx, quemOlha).Details(torneioId, null, null);
        var campanhas = view.ViewData["CampanhaDosTimes"] as IReadOnlyDictionary<int, CampanhaDosTimes.Campanha>;
        Assert.NotNull(campanhas);
        return (campanhas, view.ViewData["MeuTimeId"]);
    }

    [Fact]
    public async Task A_pagina_do_torneio_entrega_a_campanha_e_diz_qual_e_o_time_de_quem_olha()
    {
        var (ctx, torneio, nata, orgId, jogadorDaNata) = await ComAsChavesSorteadasAsync();
        await TestInfra.NovoTorneiosController(ctx, orgId).AprovarChaves(torneio.Id);

        var duplaDaNata = await ctx.Duplas.Where(d => d.Jogador1Id == jogadorDaNata).Select(d => d.Id).SingleAsync();
        var jogosDaNata = await ctx.Partidas
            .Where(p => p.TorneioId == torneio.Id && (p.Dupla1Id == duplaDaNata || p.Dupla2Id == duplaDaNata))
            .OrderBy(p => p.Id).ToListAsync();
        Assert.True(jogosDaNata.Count >= 2, "O cenário precisa de ao menos dois jogos da Nata.");

        var jogado = jogosDaNata[0];
        bool nataEhADupla1 = jogado.Dupla1Id == duplaDaNata;
        await TestInfra.FinalizarComPlacarAsync(ctx, TestInfra.NovoTorneiosController(ctx, orgId), jogado,
            nataEhADupla1 ? 6 : 2, nataEhADupla1 ? 2 : 6);

        var (campanhas, meuTime) = await NaTelaAsync(ctx, torneio.Id, jogadorDaNata);

        Assert.Equal(nata.Id, meuTime);
        var n = campanhas[nata.Id];
        Assert.Equal(1, n.Duplas);
        Assert.Equal(1, n.Jogos);
        Assert.Equal(1, n.Vitorias);
        Assert.Equal(4, n.Saldo);
        Assert.NotNull(n.ProximoJogo);
        Assert.NotEqual(jogado.Id, n.ProximoJogo.Id);
    }

    [Fact]
    public async Task Quem_nao_tem_time_nao_ganha_card_de_time()
    {
        var (ctx, torneio, _, orgId, _) = await ComAsChavesSorteadasAsync();
        await TestInfra.NovoTorneiosController(ctx, orgId).AprovarChaves(torneio.Id);

        var (_, meuTime) = await NaTelaAsync(ctx, torneio.Id, orgId);

        Assert.Null(meuTime);
    }

    // ⚠️ O PORTÃO DA CHAVE NÃO APROVADA (09/09/2026) vale aqui também: o "próximo jogo" do time
    // é um jogo da grade — mostrá-lo antes da aprovação seria a grade vazando pela aba Times.
    [Fact]
    public async Task Chave_nao_aprovada_nao_vaza_o_proximo_jogo_pra_quem_nao_organiza()
    {
        var (ctx, torneio, nata, orgId, jogadorDaNata) = await ComAsChavesSorteadasAsync();
        Assert.Equal(AprovacaoDeChaves.Pendente, (await ctx.Torneios.FindAsync(torneio.Id))!.Status);

        var (doJogador, _) = await NaTelaAsync(ctx, torneio.Id, jogadorDaNata);
        Assert.Null(doJogador[nata.Id].ProximoJogo);
        Assert.Equal(0, doJogador[nata.Id].DuplasComJogoMarcado);

        // O organizador enxerga a grade — prova de que o teste acima não passa por vazio.
        var (doOrganizador, _) = await NaTelaAsync(ctx, torneio.Id, orgId);
        Assert.NotNull(doOrganizador[nata.Id].ProximoJogo);
    }

    [Fact]
    public void A_aba_times_desenha_o_card_do_meu_time_e_o_de_cada_linha()
    {
        var fonte = TestInfra.SemComentarios(File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml")));
        var inicio = fonte.IndexOf("id=\"timesTorneio\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Não achei a aba Times.");
        var fim = fonte.IndexOf("id=\"palpiteiros\"", inicio, StringComparison.Ordinal);
        var aba = fonte[inicio..(fim > inicio ? fim : fonte.Length)];

        Assert.Contains("ViewBag.MeuTimeId", aba);
        Assert.Contains("CampanhaDosTimes.Tabela(", aba);
        // Duas vezes: o card do meu time, lá em cima, e o de cada linha da tabela.
        Assert.Equal(2, aba.Split("<partial name=\"_CampanhaDoTime\"").Length - 1);
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto web a partir de " + AppContext.BaseDirectory);
    }
}
