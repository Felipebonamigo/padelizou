using Padelizou.Services;
using Lado = Padelizou.Services.ProximasFasesDaChave.Lado;
using JogoQueVem = Padelizou.Services.ProximasFasesDaChave.JogoQueVem;

namespace Padelizou.Tests;

// "MOSTRE SÓ OS MEUS JOGOS", INCLUSIVE OS QUE AINDA NÃO EXISTEM.
//
// Pedido do Felipe depois do Interno: num torneio de 86 jogos o jogador rola a lista inteira
// pra achar os dele. Filtrar o que existe é trivial; o que ele quer ver é "se eu ganhar este,
// jogo qual, a que horas" — e esses jogos não estão no banco, são projeção.
//
// A projeção descreve os lados por PROCEDÊNCIA ("Vencedor Quartas de Final 1"), então a regra
// é seguir a corrente: meu jogo entrega um vencedor, o jogo que cita esse vencedor é meu, e
// ele por sua vez entrega o próximo. Quem PERDEU não entrega nada — é o que faz a corrente
// parar em vez de prometer a final pra quem já caiu.
public class MeusJogosTests
{
    private const string Cat = "5ª Categoria Masculina";

    private static JogoQueVem Projetado(string fase, int numero, Lado lado1, Lado lado2) =>
        new(Cat, fase, numero, null, lado1, lado2);

    private static Lado VencedorDe(string fase, int numero) =>
        new($"Vencedor {fase} {numero}", fase, numero);

    // Quartas 1..4 reais; semifinais e final projetadas.
    private static List<JogoQueVem> ChaveDeOito() => new()
    {
        Projetado("Semifinal", 1, VencedorDe("Quartas de Final", 1), VencedorDe("Quartas de Final", 4)),
        Projetado("Semifinal", 2, VencedorDe("Quartas de Final", 2), VencedorDe("Quartas de Final", 3)),
        Projetado("Final", 1, VencedorDe("Semifinal", 1), VencedorDe("Semifinal", 2)),
    };

    private static List<MeusJogos.JogoReal> Quartas(int minha, bool perdi = false) =>
        Enumerable.Range(1, 4)
            .Select(n => new MeusJogos.JogoReal(Cat, "Quartas de Final", n, SouEu: n == minha, Perdi: n == minha && perdi))
            .ToList();

    [Fact]
    public void Sigo_a_corrente_ate_a_final()
    {
        // Jogo as Quartas 1 → posso cair na Semifinal 1 e, dali, na Final.
        var meus = MeusJogos.Filtrar(ChaveDeOito(), Quartas(minha: 1), [], []);

        Assert.Equal(["Semifinal 1", "Final"], meus.Select(j => j.FaseNumerada));
    }

    [Fact]
    public void A_semifinal_do_outro_lado_da_chave_nao_e_minha()
    {
        // Quem joga as Quartas 2 cai na Semifinal 2 — a 1 é do outro lado do quadro, e só
        // encontraria essa pessoa na final.
        var meus = MeusJogos.Filtrar(ChaveDeOito(), Quartas(minha: 2), [], []);

        Assert.Equal(["Semifinal 2", "Final"], meus.Select(j => j.FaseNumerada));
    }

    // ⚠️ O TESTE QUE IMPORTA. Sem cortar a corrente em quem perdeu, o eliminado abriria a
    // tela e veria a final marcada como "seu próximo jogo".
    [Fact]
    public void Quem_perdeu_nao_tem_mais_jogo_nenhum()
    {
        var meus = MeusJogos.Filtrar(ChaveDeOito(), Quartas(minha: 1, perdi: true), [], []);

        Assert.Empty(meus);
    }

    [Fact]
    public void Quem_nao_esta_na_chave_nao_ve_projecao_nenhuma()
    {
        // Nenhuma das quartas é dele (o torcedor, ou quem caiu ainda nos grupos).
        var reais = Enumerable.Range(1, 4)
            .Select(n => new MeusJogos.JogoReal(Cat, "Quartas de Final", n, SouEu: false, Perdi: false))
            .ToList();

        Assert.Empty(MeusJogos.Filtrar(ChaveDeOito(), reais, [], []));
    }

    // Quem folgou a primeira rodada aparece na projeção pelo NOME — é o único lado sem jogo
    // de origem que pode ser dele. Sem isto, o bye some da própria chave.
    [Fact]
    public void Quem_pegou_bye_se_reconhece_pelo_nome()
    {
        var projetados = new List<JogoQueVem>
        {
            Projetado("Semifinal", 1, VencedorDe("Primeira Rodada", 1), new Lado("Ana / Bruno")),
            Projetado("Semifinal", 2, VencedorDe("Primeira Rodada", 2), new Lado("Carla / Diego")),
            Projetado("Final", 1, VencedorDe("Semifinal", 1), VencedorDe("Semifinal", 2)),
        };

        var reais = new List<MeusJogos.JogoReal>
        {
            new(Cat, "Primeira Rodada", 1, SouEu: false, Perdi: false),
            new(Cat, "Primeira Rodada", 2, SouEu: false, Perdi: false),
        };

        var meus = MeusJogos.Filtrar(projetados, reais, ["Ana / Bruno"], []);

        Assert.Equal(["Semifinal 1", "Final"], meus.Select(j => j.FaseNumerada));
    }

    // Na fase de grupos a projeção fala em "1º do Grupo A" e ninguém sabe quem vai ser o 1º.
    // Recortar por procedência não daria nada, e a tela mostraria "você não tem mais jogos"
    // pra quem ainda tem o torneio inteiro pela frente.
    [Fact]
    public void Categoria_ainda_nos_grupos_mostra_a_chave_inteira()
    {
        var projetados = new List<JogoQueVem>
        {
            Projetado("Semifinal", 1, new Lado("1º do Grupo A"), new Lado("2º do Grupo B")),
            Projetado("Semifinal", 2, new Lado("1º do Grupo B"), new Lado("2º do Grupo A")),
            Projetado("Final", 1, VencedorDe("Semifinal", 1), VencedorDe("Semifinal", 2)),
        };

        var meus = MeusJogos.Filtrar(projetados, [], [], [Cat]);

        Assert.Equal(3, meus.Count);
    }

    // A chave de OUTRA categoria nunca é dele, mesmo que os números das fases coincidam —
    // e eles coincidem sempre: toda categoria tem uma "Semifinal 1".
    [Fact]
    public void Chave_de_outra_categoria_fica_de_fora()
    {
        var projetados = ChaveDeOito()
            .Append(new JogoQueVem("6ª Categoria Feminina", "Semifinal", 1, null,
                VencedorDe("Quartas de Final", 1), VencedorDe("Quartas de Final", 4)))
            .ToList();

        var meus = MeusJogos.Filtrar(projetados, Quartas(minha: 1), [], []);

        Assert.All(meus, j => Assert.Equal(Cat, j.Categoria));
    }
}
