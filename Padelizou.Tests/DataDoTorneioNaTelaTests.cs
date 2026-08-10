using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// QUANDO O TORNEIO É — a régua que morava só no card da listagem.
//
// A página do torneio dizia local, preço, quando as inscrições fecham e quando sai a chave, e
// **não dizia em que dia o torneio acontece**. Descoberto em 09/08/2026 adiando o NATA PADEL
// TOUR de 26–27/09 pra 10–11/10: quem abrisse o link direto não teria como saber a data nova.
public class DataDoTorneioNaTelaTests
{
    private static Torneio Torneio(DateTime? inicio, DateTime? fim = null) => new()
    {
        Nome = "NATA PADEL TOUR", Codigo = "NATA1",
        DataInicio = inicio, DataFim = fim,
    };

    [Fact]
    public void Torneio_de_dois_dias_mostra_o_INTERVALO()
    {
        // Só o primeiro dia esconderia metade do evento de quem está decidindo se consegue ir.
        var frase = DataDoTorneioNaTela.Frase(
            Torneio(new DateTime(2026, 10, 10), new DateTime(2026, 10, 11)));

        Assert.Equal("10/10 a 11/10/2026", frase);
    }

    [Fact]
    public void Torneio_de_um_dia_so_mostra_uma_data()
    {
        Assert.Equal("12/11/2026", DataDoTorneioNaTela.Frase(
            Torneio(new DateTime(2026, 11, 12), new DateTime(2026, 11, 12))));
    }

    [Fact]
    public void Data_de_fim_igual_a_de_inicio_nao_vira_intervalo()
    {
        // ⚠️ A comparação é por DATA, não por instante: fim gravado com hora (23:59) no mesmo
        // dia do início viraria "10/10 a 10/10/2026", que não diz nada e parece defeito.
        var frase = DataDoTorneioNaTela.Frase(Torneio(
            new DateTime(2026, 10, 10, 9, 0, 0),
            new DateTime(2026, 10, 10, 23, 50, 0)));

        Assert.Equal("10/10/2026", frase);
    }

    [Fact]
    public void Sem_data_de_fim_mostra_so_o_comeco()
    {
        Assert.Equal("10/10/2026", DataDoTorneioNaTela.Frase(Torneio(new DateTime(2026, 10, 10))));
    }

    [Fact]
    public void Torneio_sem_data_diz_que_esta_a_definir()
    {
        Assert.Equal("Data a definir", DataDoTorneioNaTela.Frase(Torneio(null)));
        Assert.False(DataDoTorneioNaTela.TemData(Torneio(null)));
    }

    [Fact]
    public void Quem_tem_data_marcada_e_reconhecido()
    {
        // `TemData` existe pro cabeçalho do torneio, que esconde a linha inteira quando não há
        // data — lá "a definir" é ruído. No card ela é informação, e por isso a decisão de
        // mostrar é da tela.
        Assert.True(DataDoTorneioNaTela.TemData(Torneio(new DateTime(2026, 10, 10))));
    }

    [Fact]
    public void O_ano_aparece_UMA_vez_so()
    {
        // Repetir o ano nas duas pontas é ruído: o que muda entre elas quase sempre é só o dia.
        var frase = DataDoTorneioNaTela.Frase(
            Torneio(new DateTime(2026, 10, 10), new DateTime(2026, 10, 11)));

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(frase, "2026"));
    }
}
