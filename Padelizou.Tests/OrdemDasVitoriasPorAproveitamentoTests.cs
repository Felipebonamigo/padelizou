using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 14/09/2026 — MESMAS VITÓRIAS: QUEM PRECISOU DE MENOS JOGOS VEM NA FRENTE.
//
// 🗣️ Felipe, com o print da aba Vitórias: *"deveria estar como segunda opção quem tem maior
// aproveitamento — vitorias > aproveitamento > jogos"*.
//
// 🕳️ A ordem era `Vitórias ↓` e depois `Jogos ↓` — o INVERSO. Com 5 vitórias, quem fez em 6
// jogos (83%) aparecia ACIMA de quem fez em 5 (100%), bem ao lado de uma coluna "Aproveit."
// que mostrava os dois números desmentindo a ordem da tabela.
//
// 🔑 COM AS VITÓRIAS IGUAIS, "MAIOR APROVEITAMENTO" É EXATAMENTE "MENOS JOGOS": aproveitamento
// é V/J, e com V fixo ele só cresce quando J diminui. Por isso o critério do meio não precisa
// de conta nova nem de número quebrado — é `ThenBy(Jogos)`, comparação de inteiro, sem
// arredondamento pra discordar do que a coluna mostra. E é por isso também que o terceiro
// critério ("jogos") não desempata nada que os dois primeiros já não tenham desempatado.
//
// ⚠️ O teste mede o que a TELA recebe (`hub.VitoriasJogadores`), não uma função interna: a
// ordem estava escrita em quatro lugares, e cobrar o resultado é o que impede uma cópia de
// ficar pra trás.
public class OrdemDasVitoriasPorAproveitamentoTests
{
    // Duas duplas com as MESMAS 2 vitórias e aproveitamentos diferentes — a mesma forma do
    // print do Felipe (5/6 contra 5/5), no menor cenário que a reproduz:
    //   Perfeita → 2 vitórias em 2 jogos = 100%
    //   Rodada   → 2 vitórias em 3 jogos = 67%
    private static (DbPadelContext ctx, EstatisticasService svc) Cenario()
    {
        var ctx = TestInfra.NovoContexto();

        Jogador Novo(string nome, string cpf) => new() { Nome = nome, Cpf = cpf };

        var ana = Novo("Ana Perfeita", "77700000001");
        var alice = Novo("Alice Perfeita", "77700000002");
        var dora = Novo("Dora Rodada", "77700000003");
        var dani = Novo("Dani Rodada", "77700000004");
        var bia = Novo("Bia Saco", "77700000005");
        var bruna = Novo("Bruna Saco", "77700000006");
        var cida = Novo("Cida Saco", "77700000007");
        var clara = Novo("Clara Saco", "77700000008");
        var eva = Novo("Eva Carrasca", "77700000009");
        var elis = Novo("Elis Carrasca", "77700000010");
        ctx.Jogadores.AddRange(ana, alice, dora, dani, bia, bruna, cida, clara, eva, elis);

        var torneio = new Torneio
        {
            Nome = "Copa do Aproveitamento",
            Codigo = "CPA001",
            Status = "Finalizado",
            DataInicio = new DateTime(2026, 6, 1),
        };
        var cat = new Categoria { Nome = "3ª Categoria Feminina", Codigo = "C3F", Torneio = torneio };
        ctx.AddRange(torneio, cat);

        var perfeita = new Dupla { Categoria = cat, Jogador1 = ana, Jogador2 = alice };
        var rodada = new Dupla { Categoria = cat, Jogador1 = dora, Jogador2 = dani };
        var saco1 = new Dupla { Categoria = cat, Jogador1 = bia, Jogador2 = bruna };
        var saco2 = new Dupla { Categoria = cat, Jogador1 = cida, Jogador2 = clara };
        var carrasca = new Dupla { Categoria = cat, Jogador1 = eva, Jogador2 = elis };
        ctx.Duplas.AddRange(perfeita, rodada, saco1, saco2, carrasca);
        ctx.SaveChanges();

        int n = 0;
        void Jogo(Dupla d1, Dupla d2, Dupla vencedora) => ctx.Partidas.Add(new Partida
        {
            TorneioId = torneio.Id,
            CategoriaId = cat.Id,
            Dupla1Id = d1.Id,
            Dupla2Id = d2.Id,
            Fase = "Grupos",
            Status = "Finalizada",
            Codigo = $"APR{++n:000}",
            VencedorId = vencedora.Id,
            GamesDupla1 = vencedora.Id == d1.Id ? 6 : 2,
            GamesDupla2 = vencedora.Id == d1.Id ? 2 : 6,
        });

        Jogo(perfeita, saco1, perfeita);     // Perfeita 1/1
        Jogo(perfeita, saco2, perfeita);     // Perfeita 2/2 → 100%
        Jogo(rodada, saco1, rodada);         // Rodada   1/1
        Jogo(rodada, saco2, rodada);         // Rodada   2/2
        Jogo(rodada, carrasca, carrasca);    // Rodada   2/3 → 67%
        ctx.SaveChanges();

        return (ctx, new EstatisticasService(ctx));
    }

    [Fact]
    public async Task Com_as_mesmas_vitorias_o_MAIOR_APROVEITAMENTO_vem_primeiro()
    {
        var (ctx, svc) = Cenario();
        using var _ = ctx;

        var hub = await svc.ObterRankingHubAsync();

        var ordem = hub.VitoriasJogadores.Select(l => l.Jogador.Nome).ToList();
        int perfeita = ordem.FindIndex(n => n.StartsWith("Ana"));
        int rodada = ordem.FindIndex(n => n.StartsWith("Dora"));

        // As duas têm 2 vitórias; o que as separa é o aproveitamento.
        Assert.Equal(2, hub.VitoriasJogadores.Single(l => l.Jogador.Nome.StartsWith("Ana")).Vitorias);
        Assert.Equal(2, hub.VitoriasJogadores.Single(l => l.Jogador.Nome.StartsWith("Dora")).Vitorias);
        Assert.Equal(2, hub.VitoriasJogadores.Single(l => l.Jogador.Nome.StartsWith("Ana")).Jogos);
        Assert.Equal(3, hub.VitoriasJogadores.Single(l => l.Jogador.Nome.StartsWith("Dora")).Jogos);

        Assert.True(perfeita < rodada,
            $"100% (2 em 2) tem que vir antes de 67% (2 em 3). Ordem recebida: {string.Join(" · ", ordem)}");
    }

    [Fact]
    public async Task A_mesma_regua_vale_na_tabela_de_DUPLAS()
    {
        // "São os mesmos jogos vistos de três jeitos", diz a própria tela. Duas ordens
        // diferentes pro mesmo par de números seria a tabela discordando de si mesma.
        var (ctx, svc) = Cenario();
        using var _ = ctx;

        var hub = await svc.ObterRankingHubAsync();

        var ordem = hub.VitoriasDuplas.Select(d => d.Jogador1.Nome).ToList();
        Assert.True(ordem.FindIndex(n => n.StartsWith("Ana")) < ordem.FindIndex(n => n.StartsWith("Dora")),
            $"Dupla de 100% antes da de 67%. Ordem recebida: {string.Join(" · ", ordem)}");
    }

    [Fact]
    public async Task E_vale_tambem_nas_tabelas_POR_CATEGORIA()
    {
        // As de categoria são outras duas cópias da mesma ordenação. Sem isto, a aba geral
        // ficaria certa e a de categoria continuaria mostrando o inverso, lado a lado.
        var (ctx, svc) = Cenario();
        using var _ = ctx;

        var hub = await svc.ObterRankingHubAsync();

        var jogadores = Assert.Single(hub.VitoriasJogadoresPorCategoria).Jogadores
            .Select(l => l.Jogador.Nome).ToList();
        Assert.True(jogadores.FindIndex(n => n.StartsWith("Ana")) < jogadores.FindIndex(n => n.StartsWith("Dora")),
            $"Jogadores por categoria. Ordem recebida: {string.Join(" · ", jogadores)}");

        var duplas = Assert.Single(hub.VitoriasDuplasPorCategoria).Duplas
            .Select(d => d.Jogador1.Nome).ToList();
        Assert.True(duplas.FindIndex(n => n.StartsWith("Ana")) < duplas.FindIndex(n => n.StartsWith("Dora")),
            $"Duplas por categoria. Ordem recebida: {string.Join(" · ", duplas)}");
    }

    [Fact]
    public async Task Vitorias_continuam_mandando_mais_que_aproveitamento()
    {
        // ⚠️ A régua é vitórias PRIMEIRO. Sem esta asserção, "ordenar por aproveitamento"
        // poderia virar aproveitamento em cima — e aí a carrasca, com 1 vitória em 1 jogo
        // (100%), passaria na frente de quem ganhou 2. Quem joga mais não pode ser punido.
        var (ctx, svc) = Cenario();
        using var _ = ctx;

        var hub = await svc.ObterRankingHubAsync();

        var ordem = hub.VitoriasJogadores.Select(l => l.Jogador.Nome).ToList();
        var eva = hub.VitoriasJogadores.Single(l => l.Jogador.Nome.StartsWith("Eva"));
        Assert.Equal(1, eva.Vitorias);
        Assert.Equal(1, eva.Jogos);   // 100% de aproveitamento

        Assert.True(ordem.FindIndex(n => n.StartsWith("Dora")) < ordem.FindIndex(n => n.StartsWith("Eva")),
            $"2 vitórias (67%) vêm antes de 1 vitória (100%). Ordem recebida: {string.Join(" · ", ordem)}");
    }
}
