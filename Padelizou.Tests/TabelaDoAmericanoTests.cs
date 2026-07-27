using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// No Americano vence quem somar mais GAMES, não quem ganhou mais jogos: o parceiro muda a
// cada rodada, então a conta é por jogador e cada um leva os games que a SUA dupla fez.
public class TabelaDoAmericanoTests
{
    private static int _proximoId = 1;

    private static Jogador Novo(string nome) => new() { Id = _proximoId++, Nome = nome, Cpf = $"9990000{_proximoId:D4}" };

    private static Partida Jogo(Jogador a1, Jogador a2, int games1, Jogador b1, Jogador b2, int games2) =>
        new()
        {
            Codigo = "X", Status = "Finalizada", Fase = "Americano Rodada 1",
            Dupla1 = new Dupla { Jogador1 = a1, Jogador2 = a2 },
            Dupla2 = new Dupla { Jogador1 = b1, Jogador2 = b2 },
            GamesDupla1 = games1, GamesDupla2 = games2
        };

    [Fact]
    public void Cada_jogador_leva_os_games_da_propria_dupla()
    {
        var (a, b, c, d) = (Novo("Ana"), Novo("Bia"), Novo("Caio"), Novo("Duda"));

        var tabela = TabelaDoAmericano.Montar(new[] { Jogo(a, b, 9, c, d, 4) });

        Assert.Equal(9, tabela.Single(l => l.Jogador == a).TotalGames);
        Assert.Equal(9, tabela.Single(l => l.Jogador == b).TotalGames);
        Assert.Equal(4, tabela.Single(l => l.Jogador == c).TotalGames);
        Assert.Equal(4, tabela.Single(l => l.Jogador == d).TotalGames);
    }

    [Fact]
    public void Os_games_somam_ao_longo_das_rodadas_com_parceiros_diferentes()
    {
        var (a, b, c, d) = (Novo("Ana"), Novo("Bia"), Novo("Caio"), Novo("Duda"));

        var tabela = TabelaDoAmericano.Montar(new[]
        {
            Jogo(a, b, 9, c, d, 3),   // Ana +9, Bia +9, Caio +3, Duda +3
            Jogo(a, c, 5, b, d, 7),   // Ana +5, Caio +5, Bia +7, Duda +7
        });

        Assert.Equal(14, tabela.Single(l => l.Jogador == a).TotalGames);   // 9 + 5
        Assert.Equal(16, tabela.Single(l => l.Jogador == b).TotalGames);   // 9 + 7
        Assert.Equal(8, tabela.Single(l => l.Jogador == c).TotalGames);    // 3 + 5
        Assert.Equal(10, tabela.Single(l => l.Jogador == d).TotalGames);   // 3 + 7

        // Bia lidera com 16 mesmo tendo trocado de parceiro no meio.
        Assert.Equal(b, tabela[0].Jogador);
        Assert.All(tabela, l => Assert.Equal(2, l.Jogos));
    }

    [Fact]
    public void Quem_somou_mais_games_lidera_mesmo_perdendo_mais_jogos()
    {
        // A graça da regra: dá pra liderar perdendo, desde que perca apertado e ganhe folgado.
        var (a, b, c, d) = (Novo("Ana"), Novo("Bia"), Novo("Caio"), Novo("Duda"));
        var (e, f, g, h) = (Novo("Edu"), Novo("Fê"), Novo("Gui"), Novo("Hugo"));

        var tabela = TabelaDoAmericano.Montar(new[]
        {
            Jogo(a, b, 8, c, d, 9),   // Ana PERDE apertado, leva 8
            Jogo(a, c, 8, e, f, 9),   // PERDE de novo, leva 8   => 16
            Jogo(a, d, 9, g, h, 0),   // ganha folgado           => 25
        });

        // Ana perdeu 2 dos 3 jogos e mesmo assim lidera.
        Assert.Equal(a, tabela[0].Jogador);
        Assert.Equal(25, tabela[0].TotalGames);
        Assert.True(tabela[1].TotalGames < 25);
    }

    [Fact]
    public void Empate_na_lideranca_e_sinalizado()
    {
        var (a, b, c, d) = (Novo("Ana"), Novo("Bia"), Novo("Caio"), Novo("Duda"));

        // Ana e Bia jogaram juntas os dois jogos: somam igual.
        var tabela = TabelaDoAmericano.Montar(new[]
        {
            Jogo(a, b, 9, c, d, 4),
            Jogo(a, b, 6, c, d, 9),
        });

        var empatados = TabelaDoAmericano.EmpatadosNaLideranca(tabela);

        Assert.Equal(2, empatados.Count);
        Assert.Contains(a, empatados);
        Assert.Contains(b, empatados);
        Assert.All(tabela.Where(l => l.Jogador == a || l.Jogador == b), l => Assert.True(l.Empatado));
        Assert.All(tabela.Where(l => l.Jogador == c || l.Jogador == d), l => Assert.False(l.Empatado));
    }

    [Fact]
    public void Lider_isolado_nao_gera_desempate()
    {
        var (a, b, c, d) = (Novo("Ana"), Novo("Bia"), Novo("Caio"), Novo("Duda"));

        var tabela = TabelaDoAmericano.Montar(new[] { Jogo(a, b, 9, c, d, 4) });

        // Ana e Bia empatam entre si na liderança neste caso — jogaram juntas.
        // Aqui o que importa é o oposto: quando há um líder só, a lista sai vazia.
        var soUmLider = TabelaDoAmericano.Montar(new[]
        {
            Jogo(a, b, 9, c, d, 4),
            Jogo(a, c, 9, b, d, 2),   // Ana 18, Bia 11, Caio 13, Duda 6
        });

        Assert.Equal(a, soUmLider[0].Jogador);
        Assert.Empty(TabelaDoAmericano.EmpatadosNaLideranca(soUmLider));
        Assert.All(soUmLider, l => Assert.False(l.Empatado));
    }

    [Fact]
    public void Sem_jogo_finalizado_a_tabela_sai_vazia()
    {
        var tabela = TabelaDoAmericano.Montar(Array.Empty<Partida>());

        Assert.Empty(tabela);
        Assert.Empty(TabelaDoAmericano.EmpatadosNaLideranca(tabela));
    }

    [Fact]
    public void Dupla_sem_parceiro_nao_derruba_a_tabela()
    {
        // Não deveria acontecer no Americano, mas um dado torto não pode quebrar a tela no
        // meio do torneio — foi assim que a página do torneio caiu em produção (27/07).
        var (a, c, d) = (Novo("Ana"), Novo("Caio"), Novo("Duda"));
        var partida = new Partida
        {
            Codigo = "X", Status = "Finalizada", Fase = "Americano Rodada 1",
            Dupla1 = new Dupla { Jogador1 = a, Jogador2 = null },
            Dupla2 = new Dupla { Jogador1 = c, Jogador2 = d },
            GamesDupla1 = 9, GamesDupla2 = 4
        };

        var tabela = TabelaDoAmericano.Montar(new[] { partida });

        Assert.Equal(3, tabela.Count);
        Assert.Equal(9, tabela.Single(l => l.Jogador == a).TotalGames);
    }
}
