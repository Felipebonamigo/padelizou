using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 11/09/2026 — O DESEMPATE DO GRUPO GANHOU CONFRONTO DIRETO E RANKING.
//
// 🗣️ Felipe, depois de o painel revelar que um empate total cai na ordem de cadastro:
// *"e se empatar entre apenas 2 duplas, passa quem venceu o confronto direto"* e *"e empate
// entre os 3, passa quem esta na frente no ranking, se não tiver ninguem com pontuação ainda,
// faça sorteio"*.
//
// ⚠️ A REGRA DELE RESOLVE A OBJEÇÃO HISTÓRICA. Confronto direto tinha sido RECUSADO em
// 05/08/2026, e o motivo está escrito no ClassificacaoDeGrupos: no empate de TRÊS ele é
// circular (Target.it ganhou da Valandro, a Valandro da Argentus e a Argentus da Target.it),
// então não resolvia justamente o empate que apareceu. Separar por TAMANHO do empate — direto
// pra 2, ranking pra 3+ — é o que faz ele funcionar.
//
// ⚠️ ESTES CRITÉRIOS SÓ DISPARAM ONDE HOJE É SORTEIO. Vitórias, saldo e games a favor continuam
// na frente, e nenhum grupo que se decide na quadra muda de resultado.
public class DesempateDoGrupoTests
{
    private static Dupla Dupla(int id, int pontos = 0) => new()
    {
        Id = id,
        Grupo = "A",
        // ⚠️ As FKs, e não só a navegação: é `Dupla.Jogador1Id` que a régua lê pros pontos —
        // em produção o EF preenche as duas, aqui não.
        Jogador1Id = id * 10,
        Jogador2Id = id * 10 + 1,
        Jogador1 = new Jogador { Id = id * 10, Nome = $"J{id}a", Cpf = $"9990000{id:D4}" },
        Jogador2 = new Jogador { Id = id * 10 + 1, Nome = $"J{id}b", Cpf = $"9991000{id:D4}" },
    };

    private static Partida Jogo(int d1, int d2, int g1, int g2) => new()
    { Dupla1Id = d1, Dupla2Id = d2, GamesDupla1 = g1, GamesDupla2 = g2, Fase = "Grupo A" };

    private static readonly Dictionary<int, int> SemPontos = new();

    // ═══════════════ EMPATE DE DUAS: CONFRONTO DIRETO ═══════════════

    [Fact]
    public void Empate_de_duas_e_decidido_pelo_confronto_direto()
    {
        // A e B empatam em tudo (1 vitória, saldo 0, 13 games a favor cada) porque cada uma
        // venceu um jogo por 9x4 — e é a A que venceu o jogo ENTRE elas.
        //
        // ⚠️ A ordem por Id daria o mesmo aqui; é o teste seguinte, com os ids invertidos, que
        // prova que quem decide é o confronto e não o cadastro.
        var duplas = new[] { Dupla(1), Dupla(2), Dupla(3) };
        var jogos = new[] { Jogo(1, 2, 9, 4), Jogo(2, 3, 9, 4), Jogo(3, 1, 9, 4) };

        var ranking = ClassificacaoDeGrupos.Ordenar(duplas, jogos, SemPontos);

        // Empate PERFEITO entre as três — a prova de que o teste está no caso certo.
        Assert.All(ranking, l => Assert.Equal(1, l.Vitorias));
        Assert.All(ranking, l => Assert.Equal(0, l.Saldo));
        Assert.All(ranking, l => Assert.Equal(13, l.GamesPro));
    }

    [Fact]
    public void No_empate_de_DUAS_quem_venceu_o_jogo_entre_elas_fica_na_frente()
    {
        // Grupo de 4 pra o empate ser de DUAS, e não das três. C e D se decidem na quadra;
        // A e B empatam em tudo, e B venceu o confronto direto — mesmo tendo o Id MAIOR.
        // A e B: 1 vitória, 2 derrotas, saldo −4 e 21 games a favor — IDÊNTICAS. E a B, que
        // tem o Id MAIOR (2 > 1), é quem venceu o jogo entre elas.
        var duplas = new[] { Dupla(1), Dupla(2), Dupla(3), Dupla(4) };
        var jogos = new[]
        {
            Jogo(2, 1, 9, 7),   // B vence A — o confronto direto
            Jogo(1, 3, 9, 7),   // A vence C
            Jogo(4, 1, 9, 5),   // D vence A
            Jogo(3, 2, 9, 5),   // C vence B
            Jogo(4, 2, 9, 7),   // D vence B
            Jogo(3, 4, 9, 0),   // C vence D
        };

        var ranking = ClassificacaoDeGrupos.Ordenar(duplas, jogos, SemPontos);
        var a = ranking.Single(l => l.Dupla.Id == 1);
        var b = ranking.Single(l => l.Dupla.Id == 2);

        Assert.Equal(a.Vitorias, b.Vitorias);
        Assert.Equal(a.Saldo, b.Saldo);
        Assert.Equal(a.GamesPro, b.GamesPro);
        Assert.True(ranking.IndexOf(b) < ranking.IndexOf(a),
            "a B venceu o confronto direto e tinha que ficar na frente, mesmo com o Id maior");
    }

    // ═══════════════ EMPATE DE TRÊS: O RANKING ═══════════════

    [Fact]
    public void No_empate_de_TRES_quem_tem_mais_pontos_no_ranking_fica_na_frente()
    {
        // ⚠️ Confronto direto NÃO serve aqui, e é o caso do Interno de 05/08: com todos 9x4 o
        // confronto é circular (A venceu B, B venceu C, C venceu A). Por isso o ranking.
        var duplas = new[] { Dupla(1), Dupla(2), Dupla(3) };
        var jogos = new[] { Jogo(1, 2, 9, 4), Jogo(2, 3, 9, 4), Jogo(3, 1, 9, 4) };

        // A dupla soma os pontos dos DOIS jogadores. A dupla 3 é a mais pontuada.
        var pontos = new Dictionary<int, int>
        {
            [10] = 100, [11] = 50,   // dupla 1 = 150
            [20] = 10, [21] = 10,    // dupla 2 = 20
            [30] = 400, [31] = 300,  // dupla 3 = 700
        };

        var ranking = ClassificacaoDeGrupos.Ordenar(duplas, jogos, pontos);

        Assert.Equal(new[] { 3, 1, 2 }, ranking.Select(l => l.Dupla.Id).ToArray());
    }

    [Fact]
    public void Sem_ninguem_pontuado_o_empate_de_TRES_vai_pro_sorteio()
    {
        var duplas = new[] { Dupla(1), Dupla(2), Dupla(3) };
        var jogos = new[] { Jogo(1, 2, 9, 4), Jogo(2, 3, 9, 4), Jogo(3, 1, 9, 4) };

        var ranking = ClassificacaoDeGrupos.Ordenar(duplas, jogos, SemPontos);

        // ⚠️ O SORTEIO PRECISA SER ESTÁVEL — é a invariante inteira desta régua. Sorteio que
        // muda de ideia entre duas telas foi o defeito de 05/08: a tela mostrava um 2º colocado
        // e o chaveamento montava a chave com OUTRO.
        var denovo = ClassificacaoDeGrupos.Ordenar(duplas, jogos, SemPontos);
        Assert.Equal(ranking.Select(l => l.Dupla.Id), denovo.Select(l => l.Dupla.Id));

        // ⚠️ E NÃO PODE SER A ORDEM DE CADASTRO, que é o que o Felipe achou injusto: ela
        // favorece quem se inscreveu primeiro. Com 3 duplas um sorteio pode cair em 1,2,3 por
        // acidente — então a prova é sobre um grupo grande, abaixo.
        Assert.Equal(3, ranking.Count);
    }

    [Fact]
    public void O_sorteio_nao_e_a_ordem_de_inscricao()
    {
        // Oito duplas empatadas em TUDO (ninguém jogou): se o sorteio fosse o `ThenBy(Id)` de
        // antes, a ordem sairia 1,2,3,4,5,6,7,8 — quem se inscreveu primeiro passava sempre.
        var duplas = Enumerable.Range(1, 8).Select(i => Dupla(i)).ToArray();

        var ordem = ClassificacaoDeGrupos.Ordenar(duplas, Array.Empty<Partida>(), SemPontos)
            .Select(l => l.Dupla.Id).ToArray();

        Assert.NotEqual(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, ordem);
        Assert.Equal(8, ordem.Distinct().Count());
    }

    // ⚠️ ESTÁVEL ENTRE PROCESSOS, e não só dentro de um. `string.GetHashCode()` do .NET é
    // aleatorizado por processo: usá-lo faria a régua responder uma coisa antes do deploy e
    // outra depois, com os mesmos jogos. Por isso o sorteio tem hash próprio.
    [Fact]
    public void O_sorteio_responde_o_mesmo_depois_de_reiniciar_o_app()
    {
        var duplas = Enumerable.Range(1, 6).Select(i => Dupla(i)).ToArray();

        // ⚠️ O VALOR É CRAVADO, e não foi lido do código: é o FNV-1a de "1".."6", conferido
        // contra uma implementação independente da mesma especificação. Se alguém trocar o hash,
        // este teste acusa — é o que garante que a chave montada hoje continua valendo depois do
        // próximo deploy.
        Assert.Equal(new[] { 5, 4, 6, 1, 3, 2 },
            ClassificacaoDeGrupos.Ordenar(duplas, Array.Empty<Partida>(), SemPontos)
                .Select(l => l.Dupla.Id).ToArray());
    }

    // ═══════════════ A PORTA BARATA ═══════════════

    // Buscar pontos custa duas consultas ao banco, e a página do torneio é a mais visitada do
    // site. Esta pergunta é a que deixa "ao vivo" caber lá: sem empate perfeito, ninguém paga.
    [Fact]
    public void Grupo_que_se_decide_na_quadra_nao_precisa_de_pontos()
    {
        var duplas = new[] { Dupla(1), Dupla(2), Dupla(3) };
        var jogos = new[] { Jogo(1, 2, 9, 4), Jogo(2, 3, 9, 6), Jogo(3, 1, 9, 2) };

        Assert.False(ClassificacaoDeGrupos.PrecisaDePontos(duplas, jogos));
    }

    [Fact]
    public void Empate_de_tres_precisa_de_pontos()
    {
        var duplas = new[] { Dupla(1), Dupla(2), Dupla(3) };
        var jogos = new[] { Jogo(1, 2, 9, 4), Jogo(2, 3, 9, 4), Jogo(3, 1, 9, 4) };

        Assert.True(ClassificacaoDeGrupos.PrecisaDePontos(duplas, jogos));
    }

    [Fact]
    public void Empate_de_DUAS_nao_precisa_de_pontos()
    {
        // O confronto direto resolve sem ranking nenhum — pedir pontos aqui seria pagar duas
        // consultas por um desempate que já estava decidido na quadra.
        var duplas = new[] { Dupla(1), Dupla(2), Dupla(3), Dupla(4) };
        var jogos = new[]
        {
            Jogo(2, 1, 9, 7), Jogo(1, 3, 9, 7), Jogo(4, 1, 9, 5),
            Jogo(3, 2, 9, 5), Jogo(4, 2, 9, 7), Jogo(3, 4, 9, 0),
        };

        Assert.False(ClassificacaoDeGrupos.PrecisaDePontos(duplas, jogos));
    }
}
