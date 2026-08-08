using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A CLASSIFICAÇÃO DO AMERICANO É POR GRUPO — e é a mesma nas três telas que a mostram.
//
// O defeito que originou este arquivo: a sub-aba "Classificação" dentro de Jogos somava a
// CATEGORIA INTEIRA. Num Americano de 10 pessoas (2 grupos de 5) ela ordenava os dez por uma
// soma que nunca aconteceu — ninguém do grupo A enfrentou ninguém do B — enquanto a página
// avulsa, do mesmo torneio, mostrava as duas tabelas certas. Duas classificações, uma tela.
public class ClassificacaoDoAmericanoTests
{
    // Uma partida pronta: dois jogadores de cada lado, com placar.
    private static Partida Jogo(string fase, Jogador a1, Jogador a2, int games1, Jogador b1, Jogador b2, int games2) =>
        new()
        {
            Fase = fase,
            Status = "Finalizada",
            GamesDupla1 = games1,
            GamesDupla2 = games2,
            Dupla1 = new Dupla { Jogador1 = a1, Jogador2 = a2 },
            Dupla2 = new Dupla { Jogador1 = b1, Jogador2 = b2 },
        };

    private static Jogador P(int i) => new() { Id = i, Nome = $"Jogador {i:00}", Cpf = $"999000000{i:00}" };

    [Fact]
    public void Cada_grupo_tem_a_SUA_tabela_e_ninguem_aparece_no_grupo_do_outro()
    {
        // O cenário real: 10 inscritas, 2 grupos de 5, passam 2 de cada.
        var a = Enumerable.Range(1, 5).Select(P).ToArray();
        var b = Enumerable.Range(6, 5).Select(P).ToArray();

        var partidas = new[]
        {
            Jogo(FaseDoAmericano.RodadaDeGrupo("A", 1), a[0], a[1], 6, a[2], a[3], 3),
            Jogo(FaseDoAmericano.RodadaDeGrupo("A", 2), a[0], a[2], 6, a[1], a[4], 4),
            Jogo(FaseDoAmericano.RodadaDeGrupo("B", 1), b[0], b[1], 6, b[2], b[3], 2),
        };

        var tabelas = ClassificacaoDoAmericano.Montar(partidas, passamPorGrupo: 2);

        Assert.Equal(2, tabelas.Count);
        Assert.Equal("Grupo A", tabelas[0].Titulo);
        Assert.Equal("Grupo B", tabelas[1].Titulo);

        // Ninguém do B na tabela do A, e vice-versa.
        Assert.All(tabelas[0].Linhas, l => Assert.InRange(l.Jogador.Id, 1, 5));
        Assert.All(tabelas[1].Linhas, l => Assert.InRange(l.Jogador.Id, 6, 10));

        // O corte que a tela pinta de verde vem do dado, nunca de um número cravado na view.
        Assert.Equal(2, tabelas[0].PassamDaqui);
        Assert.Equal(2, tabelas[1].PassamDaqui);
    }

    [Fact]
    public void Somar_a_categoria_inteira_daria_outra_ordem___que_e_o_bug_que_isto_evita()
    {
        // O par do teste acima: prova que a diferença EXISTE, senão "por grupo" passaria
        // verde mesmo se alguém voltasse a somar tudo junto.
        var a = Enumerable.Range(1, 4).Select(P).ToArray();
        var b = Enumerable.Range(5, 4).Select(P).ToArray();

        var partidas = new[]
        {
            // No grupo A os placares são apertados; no B, largos. Somando tudo, o líder geral
            // sai do B — mas dentro do A o líder é outro, e é ele que passa.
            Jogo(FaseDoAmericano.RodadaDeGrupo("A", 1), a[0], a[1], 6, a[2], a[3], 5),
            Jogo(FaseDoAmericano.RodadaDeGrupo("B", 1), b[0], b[1], 9, b[2], b[3], 0),
        };

        var porGrupo = ClassificacaoDoAmericano.Montar(partidas, passamPorGrupo: 2);
        var tudoJunto = TabelaDoAmericano.Montar(partidas);

        // Tabela única: o topo é do grupo B (9 games).
        Assert.Equal(b[0].Id, tudoJunto[0].Jogador.Id);

        // Por grupo: o A tem o SEU líder, com 6 — que a tabela única esconderia em 3º.
        Assert.Equal(a[0].Id, porGrupo[0].Linhas[0].Jogador.Id);
        Assert.Equal(6, porGrupo[0].Linhas[0].TotalGames);
    }

    [Fact]
    public void Sem_divisao_a_tabela_e_uma_so_e_NAO_tem_corte()
    {
        // Grupo único: ninguém se "classifica", todos já estão no torneio que decide. Pintar
        // dois de verde aqui diria a duas pessoas que elas passaram pra uma fase inexistente.
        var p = Enumerable.Range(1, 4).Select(P).ToArray();
        var partidas = new[] { Jogo(FaseDoAmericano.RodadaUnica(1), p[0], p[1], 6, p[2], p[3], 3) };

        var tabelas = ClassificacaoDoAmericano.Montar(partidas, passamPorGrupo: 0);

        var unica = Assert.Single(tabelas);
        Assert.Null(unica.Titulo);
        Assert.Null(unica.Grupo);
        Assert.Equal(0, unica.PassamDaqui);
        Assert.False(ClassificacaoDoAmericano.TemDivisaoEmGrupos(tabelas));
    }

    [Fact]
    public void O_grupo_final_vem_por_ultimo_e_e_quem_decide_o_titulo()
    {
        var a = Enumerable.Range(1, 4).Select(P).ToArray();
        var partidas = new[]
        {
            Jogo(FaseDoAmericano.RodadaDeGrupo("A", 1), a[0], a[1], 6, a[2], a[3], 3),
            Jogo(FaseDoAmericano.RodadaDoGrupoFinal(1), a[0], a[2], 6, a[1], a[3], 1),
        };

        var tabelas = ClassificacaoDoAmericano.Montar(partidas, passamPorGrupo: 2);

        Assert.Equal("Grupo final", tabelas[^1].Titulo);
        Assert.True(tabelas[^1].EhGrupoFinal);
        Assert.Same(tabelas[^1], ClassificacaoDoAmericano.QueDecideOTitulo(tabelas));
    }

    [Fact]
    public void Com_grupos_e_SEM_grupo_final_ninguem_decide_o_titulo_ainda()
    {
        // ⚠️ É o que impede a tela de oferecer "montar o desempate" no meio da fase de grupos:
        // empate no topo do grupo A não é empate de título — os dois passam do mesmo jeito.
        var a = Enumerable.Range(1, 4).Select(P).ToArray();
        var b = Enumerable.Range(5, 4).Select(P).ToArray();

        var partidas = new[]
        {
            Jogo(FaseDoAmericano.RodadaDeGrupo("A", 1), a[0], a[1], 6, a[2], a[3], 6),
            Jogo(FaseDoAmericano.RodadaDeGrupo("B", 1), b[0], b[1], 6, b[2], b[3], 3),
        };

        var tabelas = ClassificacaoDoAmericano.Montar(partidas, passamPorGrupo: 2);

        Assert.Null(ClassificacaoDoAmericano.QueDecideOTitulo(tabelas));
    }

    [Fact]
    public void Partida_que_nao_terminou_e_partida_que_nao_e_do_americano_ficam_de_fora()
    {
        // A montagem recebe o torneio inteiro de propósito — quem filtra é ela, e não cada
        // uma das três telas, senão elas discordam sobre o que entra na conta.
        var p = Enumerable.Range(1, 4).Select(P).ToArray();

        var emAndamento = Jogo(FaseDoAmericano.RodadaUnica(2), p[0], p[2], 4, p[1], p[3], 4);
        emAndamento.Status = "AoVivo";

        var partidas = new[]
        {
            Jogo(FaseDoAmericano.RodadaUnica(1), p[0], p[1], 6, p[2], p[3], 3),
            emAndamento,
            Jogo("Semifinal", p[0], p[1], 6, p[2], p[3], 0),   // outro formato na mesma consulta
        };

        var tabelas = ClassificacaoDoAmericano.Montar(partidas, passamPorGrupo: 0);

        var unica = Assert.Single(tabelas);
        // Só a primeira partida contou: 6 games pro líder, e 1 jogo para cada um.
        Assert.Equal(6, unica.Linhas[0].TotalGames);
        Assert.All(unica.Linhas, l => Assert.Equal(1, l.Jogos));
    }
}
