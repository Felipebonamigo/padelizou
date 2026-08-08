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

    // ── Partida de desempate ──────────────────────────────────────────────────────────
    // Regra do organizador: empatou em games na liderança, os dois escolhem um parceiro e
    // sai um jogo só pra decidir o título.

    [Fact]
    public void Com_dois_empatados_e_tudo_jogado_o_desempate_pode_ser_criado()
    {
        Assert.Null(TabelaDoAmericano.ProblemaParaDesempatar(
            torneioPermite: true, rodadasPendentes: 0, quantosEmpatados: 2));
    }

    [Fact]
    public void Torneio_que_nao_previu_desempate_nao_cria_partida()
    {
        var problema = TabelaDoAmericano.ProblemaParaDesempatar(false, 0, 2);

        Assert.NotNull(problema);
        Assert.Contains("não previu", problema);
    }

    [Fact]
    public void Nao_desempata_com_jogo_pendente()
    {
        // Criar antes de acabar congelaria uma liderança que ainda vai mudar.
        var problema = TabelaDoAmericano.ProblemaParaDesempatar(true, rodadasPendentes: 3, quantosEmpatados: 2);

        Assert.NotNull(problema);
        Assert.Contains("3", problema);
    }

    [Fact]
    public void Sem_empate_nao_ha_o_que_desempatar()
    {
        Assert.NotNull(TabelaDoAmericano.ProblemaParaDesempatar(true, 0, 1));
        Assert.NotNull(TabelaDoAmericano.ProblemaParaDesempatar(true, 0, 0));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(7)]
    public void Com_mais_de_dois_empatados_uma_partida_so_nao_decide(int quantos)
    {
        // Inventar um chaveiro aqui seria decidir por uma regra que o organizador não deu.
        var problema = TabelaDoAmericano.ProblemaParaDesempatar(true, 0, quantos);

        Assert.NotNull(problema);
        Assert.Contains("organizador", problema);
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

    // ── EMPATE EM GAMES: confronto direto, e sorteio ESTÁVEL quando ele também empata ──────
    // (Felipe, 08/08/2026) Antes o critério era o Id do jogador: estável, mas não esportivo —
    // passava pro grupo final quem tinha se inscrito primeiro.

    [Fact]
    public void Empate_em_games_e_desfeito_pelo_confronto_direto()
    {
        var (a, b, c, d) = (Novo("Ana"), Novo("Bia"), Novo("Caio"), Novo("Duda"));
        var (e, f) = (Novo("Eva"), Novo("Fabi"));

        // ⚠️ São SEIS jogadoras porque o confronto direto só conta o que aconteceu com as duas
        // em lados OPOSTOS: num Americano de quatro elas acabam parceiras, e o jogo em que
        // jogam juntas dá os mesmos games às duas — não separa ninguém.
        var partidas = new[]
        {
            Jogo(a, c, 5, b, d, 2),   // Ana 5 CONTRA a Bia; Bia 2 contra a Ana
            Jogo(b, c, 4, a, d, 3),   // Bia 4 CONTRA a Ana; Ana 3 contra a Bia
            Jogo(a, e, 0, c, f, 7),   // Ana sem a Bia na frente: não entra no confronto delas
            Jogo(b, e, 2, d, f, 5),   // Bia sem a Ana na frente: idem
        };

        var tabela = TabelaDoAmericano.Montar(partidas);
        var linhaA = tabela.Single(l => l.Jogador == a);
        var linhaB = tabela.Single(l => l.Jogador == b);

        // Empatadas no total: Ana 5+3+0 = 8, Bia 2+4+2 = 8.
        Assert.Equal(8, linhaA.TotalGames);
        Assert.Equal(8, linhaB.TotalGames);

        // No confronto direto a Ana fez 5+3 = 8 contra a Bia, e a Bia 2+4 = 6 contra a Ana.
        Assert.True(tabela.IndexOf(linhaA) < tabela.IndexOf(linhaB));
        Assert.Equal(TabelaDoAmericano.PorConfrontoDireto, linhaA.Desempate);
    }

    [Fact]
    public void Confronto_direto_tambem_empatado_cai_no_sorteio_e_o_sorteio_NAO_muda_no_F5()
    {
        // O caso que o Felipe levantou: como no Americano cada par se enfrenta um número PAR
        // de vezes, dá empate no confronto direto também.
        var (a, b, c, d) = (Novo("Ana"), Novo("Bia"), Novo("Caio"), Novo("Duda"));

        var partidas = new[]
        {
            Jogo(a, c, 4, b, d, 4),
            Jogo(b, c, 4, a, d, 4),
        };

        var tabela = TabelaDoAmericano.Montar(partidas);
        var ordemPrimeira = tabela.Select(l => l.Jogador.Id).ToList();

        Assert.Contains(tabela, l => l.Desempate == TabelaDoAmericano.PorSorteio);

        // ⚠️ O QUE MAIS IMPORTA: montar de novo devolve a MESMA ordem. Um Random de verdade
        // faria a classificação mudar a cada F5, e o robô que monta o grupo final enxergaria
        // uma tabela enquanto a tela mostrava outra.
        for (int vez = 0; vez < 5; vez++)
        {
            Assert.Equal(ordemPrimeira, TabelaDoAmericano.Montar(partidas).Select(l => l.Jogador.Id).ToList());
        }

        // E a ordem das partidas na consulta não pode mexer no resultado: cada tela monta a
        // dela de um jeito, e foi assim que a mesma tabela já respondeu colocações diferentes.
        Assert.Equal(ordemPrimeira, TabelaDoAmericano.Montar(partidas.Reverse()).Select(l => l.Jogador.Id).ToList());
    }

    [Fact]
    public void Quem_nao_empatou_nao_ganha_rotulo_de_desempate()
    {
        // O rótulo é o que a tela mostra; carimbá-lo em quem venceu limpo explicaria um
        // critério que não foi usado.
        //
        // ⚠️ Precisa de DUAS partidas: numa só, as duas parceiras levam os mesmos games e
        // empatam sempre — no Americano o empate em games é o caso comum, não a exceção.
        var (a, b, c, d) = (Novo("Ana"), Novo("Bia"), Novo("Caio"), Novo("Duda"));

        var tabela = TabelaDoAmericano.Montar(new[]
        {
            Jogo(a, b, 6, c, d, 1),   // Ana 6, Bia 6, Caio 1, Duda 1
            Jogo(a, c, 5, b, d, 2),   // Ana 11, Caio 6, Bia 8, Duda 3
        });

        Assert.Equal(new[] { 11, 8, 6, 3 }, tabela.Select(l => l.TotalGames));
        Assert.All(tabela, l => Assert.Null(l.Desempate));
    }
}
