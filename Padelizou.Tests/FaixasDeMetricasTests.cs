using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O painel de métricas só sabia somar por semana. Semana é boa pra tendência e péssima pra
// "o que aconteceu hoje, depois que mandei o link no grupo?" e "como está o ano?".
public class FaixasDeMetricasTests
{
    // Uma quarta-feira, pra os testes de semana não passarem por acidente de o dia
    // escolhido já ser segunda.
    private static readonly DateTime QuartaFeira = new(2026, 8, 5, 14, 30, 0);

    [Theory]
    [InlineData("dia", "dia")]
    [InlineData("semana", "semana")]
    [InlineData("mes", "mes")]
    [InlineData("DIA", "dia")]
    [InlineData("  Mes  ", "mes")]
    public void Aceita_os_tres_agrupamentos_sem_ligar_pra_caixa(string entrada, string esperado) =>
        Assert.Equal(esperado, FaixasDeMetricas.Normalizar(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ano")]
    [InlineData("'; drop table")]
    public void Qualquer_outra_coisa_cai_na_semana(string? entrada)
    {
        // Link velho (sem o parâmetro), endereço digitado à mão ou lixo não podem virar erro
        // numa tela de leitura — caem no que a tela sempre mostrou.
        Assert.Equal(FaixasDeMetricas.Semana, FaixasDeMetricas.Normalizar(entrada));
    }

    // ---- Onde cada fatia começa ----

    [Fact]
    public void A_fatia_do_dia_começa_na_meia_noite_daquele_dia()
    {
        var inicio = FaixasDeMetricas.InicioDaFaixa("dia", QuartaFeira);

        Assert.Equal(new DateTime(2026, 8, 5), inicio);
        Assert.Equal(TimeSpan.Zero, inicio.TimeOfDay);
    }

    [Fact]
    public void A_semana_começa_na_SEGUNDA_e_nao_no_domingo()
    {
        // Domingo é o dia 0 do .NET, mas a semana de quem organiza torneio começa na
        // segunda: o fim de semana é o EVENTO, não o começo dela.
        Assert.Equal(new DateTime(2026, 8, 3), FaixasDeMetricas.InicioDaFaixa("semana", QuartaFeira));
        Assert.Equal(DayOfWeek.Monday, FaixasDeMetricas.InicioDaFaixa("semana", QuartaFeira).DayOfWeek);
    }

    [Fact]
    public void Domingo_pertence_a_semana_que_começou_na_segunda_anterior()
    {
        // O caso que quebra quem usa `AddDays(-(int)DayOfWeek)`: domingo viraria o começo de
        // uma semana nova, e o torneio de sábado-e-domingo apareceria partido em duas linhas.
        var domingo = new DateTime(2026, 8, 9, 20, 0, 0);

        Assert.Equal(new DateTime(2026, 8, 3), FaixasDeMetricas.InicioDaFaixa("semana", domingo));
    }

    [Fact]
    public void A_fatia_do_mes_começa_no_dia_1() =>
        Assert.Equal(new DateTime(2026, 8, 1), FaixasDeMetricas.InicioDaFaixa("mes", QuartaFeira));

    // ---- Onde cada fatia termina ----

    [Fact]
    public void O_fim_do_mes_acompanha_o_tamanho_do_mes()
    {
        // Somar 30 dias fixos deixaria fevereiro sobrando dois dias dentro de março.
        Assert.Equal(new DateTime(2026, 3, 1), FaixasDeMetricas.ProximaFaixa("mes", new DateTime(2026, 2, 1)));
        Assert.Equal(new DateTime(2026, 2, 1), FaixasDeMetricas.ProximaFaixa("mes", new DateTime(2026, 1, 1)));
        Assert.Equal(new DateTime(2027, 1, 1), FaixasDeMetricas.ProximaFaixa("mes", new DateTime(2026, 12, 1)));
    }

    [Fact]
    public void O_fim_da_semana_e_sete_dias_depois() =>
        Assert.Equal(new DateTime(2026, 8, 10), FaixasDeMetricas.ProximaFaixa("semana", new DateTime(2026, 8, 3)));

    [Fact]
    public void O_fim_do_dia_e_o_dia_seguinte() =>
        Assert.Equal(new DateTime(2026, 8, 6), FaixasDeMetricas.ProximaFaixa("dia", new DateTime(2026, 8, 5)));

    // ---- A série ----

    [Theory]
    [InlineData("dia", 14)]
    [InlineData("semana", 8)]
    [InlineData("mes", 12)]
    public void Cada_agrupamento_mostra_a_sua_quantidade_de_linhas(string agrupamento, int esperado)
    {
        var serie = FaixasDeMetricas.Series(agrupamento, QuartaFeira);

        Assert.Equal(esperado, serie.Count);
        Assert.Equal(esperado, FaixasDeMetricas.Quantidade(agrupamento));
    }

    [Fact]
    public void A_serie_vem_da_mais_antiga_pra_mais_recente_e_termina_no_periodo_de_agora()
    {
        var serie = FaixasDeMetricas.Series("dia", QuartaFeira);

        Assert.Equal(serie.OrderBy(d => d).ToList(), serie);
        // A última fatia é a de HOJE: o que aconteceu às 14h30 tem que aparecer.
        Assert.Equal(new DateTime(2026, 8, 5), serie[^1]);
        Assert.Equal(new DateTime(2026, 7, 23), serie[0]);
    }

    [Fact]
    public void A_serie_mensal_atravessa_a_virada_do_ano()
    {
        var serie = FaixasDeMetricas.Series("mes", new DateTime(2027, 1, 15));

        Assert.Equal(new DateTime(2027, 1, 1), serie[^1]);
        Assert.Equal(new DateTime(2026, 2, 1), serie[0]);
    }

    [Fact]
    public void As_fatias_se_encaixam_sem_buraco_e_sem_sobreposicao()
    {
        // Um buraco perderia cadastro; uma sobreposição contaria o mesmo pagamento duas
        // vezes — e é o valor arrecadado que sai dessa conta.
        foreach (var agrupamento in new[] { "dia", "semana", "mes" })
        {
            var serie = FaixasDeMetricas.Series(agrupamento, QuartaFeira);
            for (var i = 0; i < serie.Count - 1; i++)
            {
                Assert.Equal(serie[i + 1], FaixasDeMetricas.ProximaFaixa(agrupamento, serie[i]));
            }
        }
    }

    [Fact]
    public void A_busca_no_banco_começa_na_fatia_mais_antiga()
    {
        foreach (var agrupamento in new[] { "dia", "semana", "mes" })
        {
            Assert.Equal(
                FaixasDeMetricas.Series(agrupamento, QuartaFeira)[0],
                FaixasDeMetricas.InicioDaBusca(agrupamento, QuartaFeira));
        }
    }

    // ---- Como aparece na tela ----

    [Fact]
    public void O_rotulo_do_dia_traz_o_dia_da_semana()
    {
        // Sábado de torneio não é terça-feira: sem o dia da semana, a linha não conta nada.
        var rotulo = FaixasDeMetricas.Rotulo("dia", new DateTime(2026, 8, 5));

        Assert.Contains("05/08", rotulo);
        Assert.Contains("qua", rotulo);
    }

    [Fact]
    public void O_rotulo_da_semana_mostra_o_intervalo_fechado() =>
        Assert.Equal("03/08 – 09/08", FaixasDeMetricas.Rotulo("semana", new DateTime(2026, 8, 3)));

    [Fact]
    public void O_rotulo_do_mes_traz_o_nome_e_o_ano() =>
        Assert.Equal("agosto/2026", FaixasDeMetricas.Rotulo("mes", new DateTime(2026, 8, 1)));

    [Fact]
    public void O_titulo_e_o_subtitulo_acompanham_o_agrupamento()
    {
        // A tela dizia "semana a semana" fixo — mentiria assim que mostrasse dias.
        Assert.Contains("14 dias", FaixasDeMetricas.Titulo("dia"));
        Assert.Contains("8 semanas", FaixasDeMetricas.Titulo("semana"));
        Assert.Contains("12 meses", FaixasDeMetricas.Titulo("mes"));

        Assert.Contains("dia a dia", FaixasDeMetricas.Subtitulo("dia"));
        Assert.Contains("semana a semana", FaixasDeMetricas.Subtitulo("semana"));
        Assert.Contains("mês a mês", FaixasDeMetricas.Subtitulo("mes"));
    }
}
