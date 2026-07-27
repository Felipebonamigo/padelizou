using Padelizou.Services;

namespace Padelizou.Tests;

// O feedback do site nasce invisível e a nota de 0 a 10 é lida como NPS, não como média.
public class RegrasDeFeedbackTests
{
    [Theory]
    [InlineData(null, "Escreva o que você achou.")]
    [InlineData("", "Escreva o que você achou.")]
    [InlineData("   ", "Escreva o que você achou.")]
    [InlineData("ok", "Escreva um pouco mais pra gente entender.")]
    public void Texto_vazio_ou_curto_demais_e_recusado(string? texto, string esperado)
    {
        Assert.Equal(esperado, RegrasDeFeedback.ProblemaCom(10, texto));
    }

    [Fact]
    public void Texto_gigante_e_recusado()
    {
        var enorme = new string('a', RegrasDeFeedback.TamanhoMaximo + 1);

        Assert.NotNull(RegrasDeFeedback.ProblemaCom(8, enorme));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Nota_fora_da_escala_e_recusada(int nota)
    {
        Assert.Equal("A nota vai de 0 a 10.", RegrasDeFeedback.ProblemaCom(nota, "Muito bom o sistema"));
    }

    [Fact]
    public void Nota_e_opcional_mas_o_texto_nao()
    {
        // Tem gente que só quer escrever, sem dar nota.
        Assert.Null(RegrasDeFeedback.ProblemaCom(null, "Faltou um botão de voltar na tela de jogos"));
        Assert.NotNull(RegrasDeFeedback.ProblemaCom(9, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void As_pontas_da_escala_valem(int nota)
    {
        Assert.Null(RegrasDeFeedback.ProblemaCom(nota, "Tenho uma opinião"));
    }

    [Theory]
    [InlineData(10, "Promotor")]
    [InlineData(9, "Promotor")]
    [InlineData(8, "Neutro")]
    [InlineData(7, "Neutro")]
    [InlineData(6, "Detrator")]
    [InlineData(0, "Detrator")]
    [InlineData(null, "Sem nota")]
    public void Classificacao_segue_a_escala_do_NPS(int? nota, string esperado)
    {
        Assert.Equal(esperado, RegrasDeFeedback.Classificacao(nota));
    }

    [Fact]
    public void Nps_e_promotores_menos_detratores()
    {
        // 2 promotores, 1 neutro, 1 detrator em 4 = (50% - 25%) = 25.
        Assert.Equal(25, RegrasDeFeedback.Nps(new int?[] { 10, 9, 7, 3 }));
    }

    [Fact]
    public void Nps_vai_de_menos_cem_a_cem()
    {
        Assert.Equal(100, RegrasDeFeedback.Nps(new int?[] { 9, 10, 10 }));
        Assert.Equal(-100, RegrasDeFeedback.Nps(new int?[] { 0, 5, 6 }));
        Assert.Equal(0, RegrasDeFeedback.Nps(new int?[] { 8, 7 }));
    }

    [Fact]
    public void Quem_nao_deu_nota_fica_de_fora_da_conta()
    {
        // Contar "sem nota" como zero rebaixaria o resultado sem ninguém ter reclamado.
        Assert.Equal(100, RegrasDeFeedback.Nps(new int?[] { 10, null, null }));
        Assert.Null(RegrasDeFeedback.Nps(new int?[] { null, null }));
        Assert.Null(RegrasDeFeedback.Nps(Array.Empty<int?>()));
    }

    // Média esconde justamente o que interessa: dez notas 8 e dez notas 4 dão a mesma média
    // que vinte notas 6, e são situações bem diferentes. O NPS separa as três.
    [Fact]
    public void Nps_separa_o_que_a_media_esconderia()
    {
        var polarizado = Enumerable.Repeat<int?>(8, 10).Concat(Enumerable.Repeat<int?>(4, 10));
        var morno = Enumerable.Repeat<int?>(6, 20);

        Assert.Equal(-50, RegrasDeFeedback.Nps(polarizado));   // 10 neutros, 10 detratores
        Assert.Equal(-100, RegrasDeFeedback.Nps(morno));       // todo mundo detrator
    }
}
