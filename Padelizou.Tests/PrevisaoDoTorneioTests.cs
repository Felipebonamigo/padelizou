using Padelizou.Services;

namespace Padelizou.Tests;

// "Cabe?" é a pergunta que o organizador faz antes de anunciar o torneio, e que nenhum campo
// da tela responde sozinho. Estes testes prendem a previsão ao que o sorteio REALMENTE faz —
// se as duas contas divergirem, a tela promete uma coisa e a grade entrega outra.
public class PrevisaoDoTorneioTests
{
    [Theory]
    [InlineData(2, 1, 1)]    // 2 duplas: chave direta
    [InlineData(6, 2, 6)]    // 2 grupos de 3
    [InlineData(9, 3, 9)]
    [InlineData(12, 4, 12)]
    [InlineData(13, 5, 11)]  // resto 1: 2 grupos de 2 (1 jogo cada) + 3 de 3 (3 jogos cada)
    [InlineData(14, 5, 13)]  // resto 2: 1 grupo de 2 + 4 de 3
    [InlineData(16, 6, 14)]  // resto 1: 2 grupos de 2 + 4 de 3
    [InlineData(24, 8, 24)]
    public void Fase_de_grupos_espelha_a_regra_do_sorteio(int duplas, int grupos, int jogos)
    {
        var previsto = PrevisaoDoTorneio.FaseDeGrupos(duplas);

        Assert.Equal(grupos, previsto.Grupos);
        Assert.Equal(jogos, previsto.Jogos);
    }

    [Theory]
    [InlineData(1, 1)]    // 1 grupo: final direta
    [InlineData(2, 3)]    // quadro de 4: 2 semis + final
    [InlineData(3, 3)]    // 6 classificados, quadro de 4
    [InlineData(5, 7)]    // 10 classificados, quadro de 8: 4 quartas + 2 semis + final
    [InlineData(6, 7)]
    [InlineData(8, 15)]   // 16 classificados, quadro de 16
    public void Mata_mata_tem_um_jogo_a_menos_que_o_quadro(int grupos, int jogos)
    {
        Assert.Equal(jogos, PrevisaoDoTorneio.MataMata(grupos));
    }

    [Fact]
    public void Sem_duplas_nao_ha_torneio()
    {
        Assert.Equal((0, 0), PrevisaoDoTorneio.FaseDeGrupos(0));
        Assert.Equal((0, 0), PrevisaoDoTorneio.FaseDeGrupos(1));
        Assert.Equal(0, PrevisaoDoTorneio.MataMata(0));
        Assert.Equal(0, PrevisaoDoTorneio.TotalDeJogos(1));
    }

    // O caso que o Felipe descreveu: sexta 18h, sábado e domingo 8h, teto 23h50.
    [Fact]
    public void Torneio_de_fim_de_semana_termina_no_domingo()
    {
        var sexta18h = new DateTime(2026, 8, 21, 18, 0, 0);
        int jogos = PrevisaoDoTorneio.TotalDeJogos(24);   // 8 grupos: 24 + 15 = 39

        Assert.Equal(39, jogos);

        var ultimo = PrevisaoDoTorneio.UltimoJogo(
            sexta18h, new TimeSpan(23, 50, 0), new TimeSpan(8, 0, 0),
            quadras: 3, duracaoMinutos: 50, totalDeJogos: jogos);

        // Sexta: 8 rodadas x 3 quadras = 24 jogos. Sábado abre 8h e leva os 15 restantes
        // em 5 rodadas — 8h às 11h20.
        Assert.Equal(new DateTime(2026, 8, 22, 11, 20, 0), ultimo);
        Assert.Equal(2, PrevisaoDoTorneio.DiasOcupados(sexta18h, ultimo!.Value));
    }

    [Fact]
    public void Uma_quadra_so_estica_o_torneio_por_mais_dias()
    {
        var sexta18h = new DateTime(2026, 8, 21, 18, 0, 0);
        int jogos = PrevisaoDoTorneio.TotalDeJogos(24);

        var comTres = PrevisaoDoTorneio.UltimoJogo(sexta18h, new TimeSpan(23, 50, 0), new TimeSpan(8, 0, 0), 3, 50, jogos);
        var comUma = PrevisaoDoTorneio.UltimoJogo(sexta18h, new TimeSpan(23, 50, 0), new TimeSpan(8, 0, 0), 1, 50, jogos);

        // É o que o organizador precisa ver ANTES de anunciar: a mesma grade, com 1 quadra
        // em vez de 3, deixa de caber no fim de semana.
        Assert.True(comUma > comTres);
        Assert.Equal(2, PrevisaoDoTorneio.DiasOcupados(sexta18h, comTres!.Value));
        Assert.True(PrevisaoDoTorneio.DiasOcupados(sexta18h, comUma!.Value) >= 3);
    }

    [Fact]
    public void A_previsao_bate_com_a_grade_de_verdade()
    {
        var sexta18h = new DateTime(2026, 8, 21, 18, 0, 0);
        int jogos = PrevisaoDoTorneio.TotalDeJogos(16);

        var previsto = PrevisaoDoTorneio.UltimoJogo(
            sexta18h, new TimeSpan(23, 50, 0), new TimeSpan(8, 0, 0), 2, 50, jogos);

        var grade = GradeDeJogos.Horarios(sexta18h, new TimeSpan(23, 50, 0), 2, 50, jogos,
            aberturaDiasSeguintes: new TimeSpan(8, 0, 0)).ToList();

        Assert.Equal(grade.Last(), previsto);
        Assert.Equal(jogos, grade.Count);
    }

    [Fact]
    public void Dias_ocupados_conta_o_dia_de_abertura()
    {
        var sexta = new DateTime(2026, 8, 21, 18, 0, 0);

        Assert.Equal(1, PrevisaoDoTorneio.DiasOcupados(sexta, sexta.AddHours(3)));
        Assert.Equal(3, PrevisaoDoTorneio.DiasOcupados(sexta, new DateTime(2026, 8, 23, 15, 0, 0)));
    }
}
