using Padelizou.Services;

namespace Padelizou.Tests;

// A ordem em que os jogos são DESENHADOS no chaveamento.
//
// Os jogos nascem numerados 1..N na ordem de criação, mas o mata-mata cruza o primeiro com o
// último (ParearVencedores). Desenhados na ordem de criação, os jogos 1 e 2 aparecem colados
// e vão pra lados opostos da chave — o quadro fica certo nos dados e mentiroso no desenho.
public class OrdemDoQuadroTests
{
    [Theory]
    [InlineData(1, new[] { 1 })]
    [InlineData(2, new[] { 1, 2 })]
    [InlineData(4, new[] { 1, 4, 2, 3 })]
    [InlineData(8, new[] { 1, 8, 4, 5, 2, 7, 3, 6 })]
    public void A_ordem_poe_cada_par_de_vizinhos_desembocando_no_mesmo_jogo(int jogos, int[] esperado)
    {
        Assert.Equal(esperado, OrdemDoQuadro.De(jogos));
    }

    [Fact]
    public void Nenhum_jogo_some_nem_aparece_duas_vezes()
    {
        foreach (int jogos in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 12, 16 })
        {
            var ordem = OrdemDoQuadro.De(jogos);

            Assert.Equal(jogos, ordem.Count);
            Assert.Equal(Enumerable.Range(1, jogos), ordem.OrderBy(n => n));
        }
    }

    [Fact]
    public void Os_vizinhos_desenhados_sao_os_que_o_pareamento_junta()
    {
        // A prova da regra: lendo a ordem de dois em dois, cada par tem que ser exatamente
        // um dos confrontos que o ParearVencedores monta pra rodada seguinte.
        const int jogos = 8;
        var ordem = OrdemDoQuadro.De(jogos);

        var paresDesenhados = new List<(int, int)>();
        for (int i = 0; i < ordem.Count; i += 2)
            paresDesenhados.Add((ordem[i], ordem[i + 1]));

        var paresDeVerdade = ChaveamentoMataMata
            .ParearVencedores(Enumerable.Range(1, jogos).ToList())
            .Select(c => (c.Dupla1Id, c.Dupla2Id))
            .ToList();

        Assert.All(paresDesenhados, par =>
            Assert.Contains(paresDeVerdade, real => real == par || real == (par.Item2, par.Item1)));
    }

    [Fact]
    public void Ordenar_devolve_os_proprios_jogos_remexidos()
    {
        var jogos = new[] { "A", "B", "C", "D" };

        Assert.Equal(new[] { "A", "D", "B", "C" }, OrdemDoQuadro.Ordenar(jogos));
    }

    [Fact]
    public void Rodada_vazia_nao_estoura()
    {
        Assert.Empty(OrdemDoQuadro.De(0));
        Assert.Empty(OrdemDoQuadro.Ordenar(Array.Empty<string>()));
    }
}
