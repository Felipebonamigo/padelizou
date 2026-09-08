using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A ORDEM QUE DEFINE OS CABEÇAS DE CHAVE.
//
// 🗣️ Pedido do Felipe, 07/09/2026: *"quando não tiver ranking, por exemplo esse primeiro, é sem
// ranking, faz totalmente aleatório"*.
//
// 🕳️ O QUE ELE VIU, medido no banco de produção antes desta mudança: dos **114 inscritos** no
// 2ª Etapa ER PADEL TOUR, **0 tinham histórico que conta no ranking**. O motivo é que
// `DuplaContaNoRanking` exclui Americano, e os dois únicos torneios finalizados da produção são
// os dois Americanos das Gurias — nenhum torneio de chave terminou ainda.
//
// Com todo mundo em 0 ponto, o `OrderByDescending` é ESTÁVEL: ele preserva a ordem em que as
// duplas vieram do banco, que é aproximadamente a de inscrição. Os cabeças de chave saíam por
// ordem de chegada — exatamente o sintoma que um comentário do `GerarChaves` diz ter
// consertado quando trocou o campo morto `PontuacaoGlobal` por pontos de verdade. O bug voltou
// pela porta dos dados, não pela do código.
//
// ⚠️ A correção é DESEMPATE ALEATÓRIO, e não um ramo "se não houver ranking, sorteia": além de
// resolver o caso do Felipe como consequência, ela conserta o caso mais sutil de um torneio COM
// ranking, onde duas duplas empatadas em pontos também saíam por ordem de inscrição.
public class SemeaduraDaChaveTests
{
    private static Jogador Jog(int id) => new() { Id = id, Nome = $"Jogador {id}", Cpf = id.ToString() };

    private static Dupla Dup(int id, int j1, int j2) => new()
    {
        Id = id, Codigo = "D" + id,
        Jogador1Id = j1, Jogador1 = Jog(j1),
        Jogador2Id = j2, Jogador2 = Jog(j2),
    };

    // Oito duplas, cada uma com dois jogadores próprios: 1+2, 3+4, 5+6...
    private static List<Dupla> OitoDuplas() =>
        Enumerable.Range(0, 8).Select(i => Dup(i + 1, i * 2 + 1, i * 2 + 2)).ToList();

    private static Dictionary<int, int> SemPontos() => new();

    [Fact]
    public void Sem_ranking_nenhum_a_ordem_NAO_e_a_de_inscricao()
    {
        // O pedido literal. Semente fixa pra prova ser determinística — o que se afirma aqui é
        // que a saída deixou de ser a entrada, não qual embaralhamento saiu.
        var duplas = OitoDuplas();

        var ordenadas = SemeaduraDaChave.Ordenar(duplas, SemPontos(), new Random(42));

        Assert.NotEqual(duplas.Select(d => d.Id), ordenadas.Select(d => d.Id));
    }

    [Fact]
    public void Sem_ranking_nenhuma_dupla_some_nem_duplica()
    {
        // Embaralhar é fácil de fazer perdendo gente. Numa chave, uma dupla a menos é alguém
        // que se inscreveu, pagou e não joga.
        var duplas = OitoDuplas();

        var ordenadas = SemeaduraDaChave.Ordenar(duplas, SemPontos(), new Random(1));

        Assert.Equal(duplas.Count, ordenadas.Count);
        Assert.Equal(duplas.Select(d => d.Id).OrderBy(x => x), ordenadas.Select(d => d.Id).OrderBy(x => x));
    }

    [Fact]
    public void Sementes_diferentes_dao_ordens_diferentes()
    {
        // Prova de que o sorteio é sorteio: sem isto, uma implementação que ignorasse o
        // `Random` e devolvesse sempre a mesma ordem passaria nos testes acima.
        var duplas = OitoDuplas();

        var a = SemeaduraDaChave.Ordenar(duplas, SemPontos(), new Random(1)).Select(d => d.Id);
        var b = SemeaduraDaChave.Ordenar(duplas, SemPontos(), new Random(2)).Select(d => d.Id);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void COM_ranking_quem_tem_mais_ponto_vem_primeiro()
    {
        // A regra que não pode ser perdida no meio da mudança: o sorteio é DESEMPATE, não
        // substituto do ranking.
        var duplas = OitoDuplas();
        var pontos = new Dictionary<int, int> { [5] = 100 };   // jogador 5 está na dupla 3

        var ordenadas = SemeaduraDaChave.Ordenar(duplas, pontos, new Random(7));

        Assert.Equal(3, ordenadas[0].Id);
    }

    [Fact]
    public void Os_pontos_da_dupla_sao_a_SOMA_dos_dois()
    {
        // Uma dupla de dois medianos pode valer mais que uma com um forte e um estreante — é
        // assim que o `GerarChaves` sempre somou, e a régua não muda aqui.
        var duplas = new List<Dupla> { Dup(1, 1, 2), Dup(2, 3, 4) };
        var pontos = new Dictionary<int, int> { [1] = 10, [2] = 10, [3] = 15, [4] = 0 };

        var ordenadas = SemeaduraDaChave.Ordenar(duplas, pontos, new Random(3));

        Assert.Equal(1, ordenadas[0].Id);   // 20 contra 15
    }

    [Fact]
    public void Empate_no_meio_do_ranking_tambem_e_sorteado()
    {
        // ⚠️ O caso que o pedido não citava e que esta mudança conserta junto: num torneio COM
        // ranking, as duplas empatadas saíam na ordem de inscrição. A dupla com ponto continua
        // em cima; as três empatadas embaixo dela é que embaralham.
        var duplas = OitoDuplas();
        var pontos = new Dictionary<int, int> { [1] = 50 };   // dupla 1 é a única com ponto

        var a = SemeaduraDaChave.Ordenar(duplas, pontos, new Random(1));
        var b = SemeaduraDaChave.Ordenar(duplas, pontos, new Random(2));

        Assert.Equal(1, a[0].Id);
        Assert.Equal(1, b[0].Id);
        Assert.NotEqual(a.Skip(1).Select(d => d.Id), b.Skip(1).Select(d => d.Id));
    }

    [Fact]
    public void Jogador_sem_ponto_no_dicionario_vale_zero()
    {
        // `ObterPontosPorJogadorAsync` devolve só quem tem histórico; quem nunca jogou não
        // aparece no dicionário. Estourar aqui derrubaria o sorteio do torneio inteiro.
        var duplas = new List<Dupla> { Dup(1, 1, 2) };

        var ordenadas = SemeaduraDaChave.Ordenar(duplas, new Dictionary<int, int>(), new Random(1));

        Assert.Single(ordenadas);
    }

    [Fact]
    public void Lista_vazia_nao_estoura()
    {
        Assert.Empty(SemeaduraDaChave.Ordenar(new List<Dupla>(), SemPontos(), new Random(1)));
    }
}
