using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 12/09/2026 — CLICAR NO NOME NA TABELA DE PALPITEIROS E VER O QUE A PESSOA PALPITOU.
// 🗣️ Felipe, com o print da aba Palpiteiros no celular: *"ai clicar no nome, permita ver os
// resultados q a pessoa colocou mas de um modo que nao quebre a tela"*.
//
// 🕳️ A tabela mostrava o RESULTADO da conta (20 pontos, 41 em aberto) e nada de onde ela vem.
// Quem palpitou junto queria ver o palpite: em que dupla, com que placar, e quanto valeu.
// Existia o caminho contrário — o modal "quem palpitou o quê" abre a partir de UM JOGO e lista
// todo mundo —, e nenhum que partisse da PESSOA.
//
// ⚠️ A RÉGUA AQUI É A MESMA DO RANKING, e é de propósito: a lista chama `PontosDoPalpite.De`
// pela mesma `Conferir` que a apuração usa. Duas contas separadas acabariam discordando — o
// modal dizendo 3 pontos naquele jogo e a tabela somando 2 —, e a tela que explica a conta
// seria justamente a que desmente.
public class PalpitesDoPalpiteiroTests
{
    // ─────────────────────────── O QUE A LISTA DEVOLVE ───────────────────────────

    [Fact]
    public async Task A_lista_diz_EM_QUEM_a_pessoa_apostou_o_PLACAR_dela_e_o_que_DEU()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, partida) = await MontarJogoTerminadoAsync(ctx);
        var duplas = DuplasDa(ctx, categoria);

        var torcedor = await NovoTorcedorAsync(ctx, "Torcedor Certeiro", "55520000001");
        await PalpitarAsync(ctx, partida, torcedor.Id, duplas[0].Id, games1: 6, games2: 4);

        var lista = await RankingDePalpiteiros.DoPalpiteiroNoTorneioAsync(ctx, torneio.Id, torcedor.Id);

        Assert.NotNull(lista);
        Assert.Equal("Torcedor Certeiro", lista!.Jogador);

        var linha = Assert.Single(lista.Linhas);
        Assert.Equal(partida.Id, linha.PartidaId);
        Assert.Equal(categoria.Nome, linha.Categoria);
        Assert.Equal("Final", linha.Fase);
        Assert.Equal("Marcelo / Enio", linha.Escolhida);
        Assert.Equal("Paulo / Andryo", linha.Adversaria);

        // O palpite e o resultado, os dois na MESMA orientação (a dupla escolhida primeiro) —
        // é o que deixa comparar um com o outro sem virar a cabeça.
        Assert.Equal(6, linha.PalpitouEscolhida);
        Assert.Equal(4, linha.PalpitouAdversaria);
        Assert.Equal(6, linha.PlacarEscolhida);
        Assert.Equal(4, linha.PlacarAdversaria);

        Assert.True(linha.Apurado);
        Assert.True(linha.Acertou);
        Assert.Equal(PontosDoPalpite.Cravou, linha.Pontos);
    }

    [Fact]
    public async Task O_placar_de_quem_apostou_na_DUPLA_2_nao_sai_invertido()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, partida) = await MontarJogoTerminadoAsync(ctx);
        var duplas = DuplasDa(ctx, categoria);

        // ⚠️ No BANCO o placar mora na orientação do JOGO (lado 1 = Dupla1), no palpite e no
        // resultado. Quem apostou "6 x 4 pra Dupla 2" gravou 4 x 6 — e a linha tem que ler 6 x 4,
        // que é a ficha que a pessoa tocou. Mesma régua do modal de quem votou.
        var torcedor = await NovoTorcedorAsync(ctx, "Torcedor do Azarao", "55520000002");
        await PalpitarAsync(ctx, partida, torcedor.Id, duplas[1].Id, games1: 4, games2: 6);

        var lista = await RankingDePalpiteiros.DoPalpiteiroNoTorneioAsync(ctx, torneio.Id, torcedor.Id);
        var linha = Assert.Single(lista!.Linhas);

        Assert.Equal("Paulo / Andryo", linha.Escolhida);
        Assert.Equal(6, linha.PalpitouEscolhida);
        Assert.Equal(4, linha.PalpitouAdversaria);

        // O jogo terminou 6 x 4 pra Dupla 1 — do lado de quem ele escolheu, isso é 4 x 6.
        Assert.Equal(4, linha.PlacarEscolhida);
        Assert.Equal(6, linha.PlacarAdversaria);
        Assert.False(linha.Acertou);
        Assert.Equal(0, linha.Pontos);
    }

    [Fact]
    public async Task Palpite_SEM_placar_aparece_sem_placar_inventado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, partida) = await MontarJogoTerminadoAsync(ctx);
        var duplas = DuplasDa(ctx, categoria);

        var torcedor = await NovoTorcedorAsync(ctx, "So Disse Quem Vence", "55520000003");
        await PalpitarAsync(ctx, partida, torcedor.Id, duplas[0].Id);

        var lista = await RankingDePalpiteiros.DoPalpiteiroNoTorneioAsync(ctx, torneio.Id, torcedor.Id);
        var linha = Assert.Single(lista!.Linhas);

        // ⚠️ Nulo, e não "0 x 0": palpitar o placar é opcional desde sempre, e um zero inventado
        // diria que ela chutou um placar que nenhum jogo termina.
        Assert.Null(linha.PalpitouEscolhida);
        Assert.Null(linha.PalpitouAdversaria);
        Assert.False(linha.PalpitouOPlacar);

        // O RESULTADO continua aparecendo — é o que ela quer ver ao abrir a lista.
        Assert.Equal(6, linha.PlacarEscolhida);
        Assert.Equal(4, linha.PlacarAdversaria);
        Assert.Equal(PontosDoPalpite.SoOVencedor, linha.Pontos);
    }

    [Fact]
    public async Task O_jogo_que_ainda_NAO_terminou_entra_como_EM_ABERTO_e_sem_ponto()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, partida) = await MontarJogoTerminadoAsync(ctx);
        var duplas = DuplasDa(ctx, categoria);

        partida.Status = "Agendada";
        partida.VencedorId = null;
        partida.GamesDupla1 = null;
        partida.GamesDupla2 = null;
        await ctx.SaveChangesAsync();

        var torcedor = await NovoTorcedorAsync(ctx, "Palpitou na Vespera", "55520000004");
        await PalpitarAsync(ctx, partida, torcedor.Id, duplas[0].Id, games1: 6, games2: 3);

        var lista = await RankingDePalpiteiros.DoPalpiteiroNoTorneioAsync(ctx, torneio.Id, torcedor.Id);
        var linha = Assert.Single(lista!.Linhas);

        Assert.False(linha.Apurado);
        Assert.Equal(0, linha.Pontos);
        Assert.False(linha.Acertou);
        Assert.Null(linha.PlacarEscolhida);
        Assert.Null(linha.PlacarAdversaria);

        // O palpite dela continua na tela — é justamente o que a coluna "Em aberto" está contando.
        Assert.Equal(6, linha.PalpitouEscolhida);
        Assert.Equal(3, linha.PalpitouAdversaria);
        Assert.Equal(1, lista.EmAberto);
        Assert.Equal(0, lista.Palpites);
    }

    [Fact]
    public async Task O_jogo_em_que_a_PESSOA_ESTAVA_EM_QUADRA_aparece_MARCADO_e_fora_da_conta()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, partida) = await MontarJogoTerminadoAsync(ctx);
        var duplas = DuplasDa(ctx, categoria);

        // O próprio jogador da Dupla 1 cravou o placar do próprio jogo.
        await PalpitarAsync(ctx, partida, duplas[0].Jogador1Id, duplas[0].Id, games1: 6, games2: 4);

        var lista = await RankingDePalpiteiros.DoPalpiteiroNoTorneioAsync(ctx, torneio.Id, duplas[0].Jogador1Id);
        var linha = Assert.Single(lista!.Linhas);

        // ⚠️ APARECE — sumir com a linha faria a pessoa procurar um palpite que ela lembra de ter
        // dado. O que ela NÃO faz é somar ponto: é a mesma exclusão do ranking, e a lista precisa
        // dizer por quê, senão a conta parece quebrada.
        Assert.True(linha.EstavaEmQuadra);
        Assert.False(linha.Conta);
        Assert.Equal(0, linha.Pontos);

        Assert.Equal(0, lista.Pontos);
        Assert.Equal(0, lista.Palpites);
        Assert.Equal(0, lista.EmAberto);
    }

    // ─────────────────────── OS TOTAIS BATEM COM A LINHA DA TABELA ───────────────────────

    [Fact]
    public async Task Os_totais_da_lista_sao_EXATAMENTE_os_da_linha_da_tabela()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, terminado) = await MontarJogoTerminadoAsync(ctx);
        var duplas = DuplasDa(ctx, categoria);

        var agendado = new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id,
            Dupla2Id = duplas[1].Id,
            Status = "Agendada",
            Fase = "Semifinal",
            Codigo = "P2",
        };
        ctx.Partidas.Add(agendado);
        await ctx.SaveChangesAsync();

        var torcedor = await NovoTorcedorAsync(ctx, "Palpiteiro Completo", "55520000005");
        await PalpitarAsync(ctx, terminado, torcedor.Id, duplas[0].Id, games1: 6, games2: 4);
        await PalpitarAsync(ctx, agendado, torcedor.Id, duplas[1].Id, games1: 4, games2: 6);

        var lista = await RankingDePalpiteiros.DoPalpiteiroNoTorneioAsync(ctx, torneio.Id, torcedor.Id);
        var ranking = await RankingDePalpiteiros.DoTorneioAsync(ctx, torneio.Id, olhandoId: null);
        var naTabela = ranking!.Linhas.Single(l => l.JogadorId == torcedor.Id);

        // ⚠️ ESTE É O TESTE QUE IMPORTA. A lista é a explicação da linha da tabela: se os dois
        // números discordarem, é a tela que explica a conta desmentindo a conta.
        Assert.Equal(naTabela.Pontos, lista!.Pontos);
        Assert.Equal(naTabela.Acertos, lista.Acertos);
        Assert.Equal(naTabela.Palpites, lista.Palpites);
        Assert.Equal(naTabela.Cravadas, lista.Cravadas);
        Assert.Equal(naTabela.EmAberto, lista.EmAberto);
    }

    [Fact]
    public async Task Os_jogos_JA_APURADOS_vem_antes_dos_que_esperam_resultado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, terminado) = await MontarJogoTerminadoAsync(ctx);
        var duplas = DuplasDa(ctx, categoria);

        // O agendado é gravado ANTES na ordem de Id? Não: ele nasce depois, e mesmo assim tem que
        // cair no fim — quem abre a lista quer ver primeiro o que já valeu ponto.
        var agendado = new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id,
            Dupla2Id = duplas[1].Id,
            Status = "Agendada",
            Fase = "Semifinal",
            Codigo = "P2",
        };
        ctx.Partidas.Add(agendado);
        await ctx.SaveChangesAsync();

        var torcedor = await NovoTorcedorAsync(ctx, "Palpiteiro Ordenado", "55520000006");
        await PalpitarAsync(ctx, agendado, torcedor.Id, duplas[0].Id);
        await PalpitarAsync(ctx, terminado, torcedor.Id, duplas[0].Id);

        var lista = await RankingDePalpiteiros.DoPalpiteiroNoTorneioAsync(ctx, torneio.Id, torcedor.Id);

        Assert.Equal(new[] { terminado.Id, agendado.Id }, lista!.Linhas.Select(l => l.PartidaId));
    }

    [Fact]
    public async Task Quem_NAO_palpitou_neste_torneio_nao_tem_lista()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, _) = await MontarJogoTerminadoAsync(ctx);

        var estranho = await NovoTorcedorAsync(ctx, "Nunca Palpitou", "55520000007");

        // ⚠️ Nulo, e não uma lista vazia: quem chama responde 404 — a mesma régua da página
        // /Torneios/Palpiteiros, onde tela vazia é link que só sabe decepcionar.
        Assert.Null(await RankingDePalpiteiros.DoPalpiteiroNoTorneioAsync(ctx, torneio.Id, estranho.Id));
        Assert.Null(await RankingDePalpiteiros.DoPalpiteiroNoTorneioAsync(ctx, torneio.Id, jogadorId: 999999));
    }

    [Fact]
    public async Task O_palpite_de_OUTRO_torneio_nao_entra_na_lista_deste()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, partida) = await MontarJogoTerminadoAsync(ctx);
        var duplas = DuplasDa(ctx, categoria);

        var (outro, outraCategoria, outraPartida) = await MontarJogoTerminadoAsync(ctx);
        var outrasDuplas = DuplasDa(ctx, outraCategoria);

        var torcedor = await NovoTorcedorAsync(ctx, "Palpita em Tudo", "55520000008");
        await PalpitarAsync(ctx, partida, torcedor.Id, duplas[0].Id);
        await PalpitarAsync(ctx, outraPartida, torcedor.Id, outrasDuplas[0].Id);

        var lista = await RankingDePalpiteiros.DoPalpiteiroNoTorneioAsync(ctx, torneio.Id, torcedor.Id);

        var linha = Assert.Single(lista!.Linhas);
        Assert.Equal(partida.Id, linha.PartidaId);
        Assert.Equal(torneio.Id, lista.TorneioId);
    }

    // ─────────────────────────── A INFRA DOS TESTES ───────────────────────────

    private static List<Dupla> DuplasDa(DbPadelContext ctx, Categoria categoria) =>
        ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).OrderBy(d => d.Id).ToList();

    // Um jogo que terminou 6 x 4 pra Dupla 1, com gente de nome distinguível: a lista mostra o
    // NOME CURTO da dupla ("Marcelo / Enio"), e "Jogador 01 / Jogador 02" não provaria nada.
    private static async Task<(Torneio torneio, Categoria categoria, Partida partida)>
        MontarJogoTerminadoAsync(DbPadelContext ctx)
    {
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 2, status: "Finalizado");
        var duplas = ctx.Duplas.Where(d => d.CategoriaId == categoria.Id).OrderBy(d => d.Id).ToList();

        var nomes = new[] { "Marcelo Prestes", "Enio Machado", "Paulo Prass", "Andryo Andrade" };
        var jogadores = duplas
            .SelectMany(d => new[] { d.Jogador1Id, d.Jogador2Id!.Value })
            .Select(id => ctx.Jogadores.Find(id)!)
            .ToList();
        for (int i = 0; i < jogadores.Count; i++) jogadores[i].Nome = nomes[i];

        var partida = new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = categoria.Id,
            Dupla1Id = duplas[0].Id,
            Dupla2Id = duplas[1].Id,
            VencedorId = duplas[0].Id,
            GamesDupla1 = 6,
            GamesDupla2 = 4,
            Status = "Finalizada",
            Fase = "Final",
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
}
