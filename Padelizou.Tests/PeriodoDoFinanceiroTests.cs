using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O PERÍODO DO FINANCEIRO DO PROFESSOR, E O MÊS QUE JÁ FECHOU.
//
// 🗣️ Pedido do Felipe, 02/09/2026: *"uma aba de conferir o mês passado no financeiro"*.
//
// 🕳️ O que faltava não era um filtro, era um CONCEITO. Os quatro períodos de hoje — semana,
// mês, ano, sempre — são todos ABERTOS: o controller filtrava `a.DataHora >= de`, sem fim
// nenhum. "Mês passado" é o primeiro intervalo com as duas pontas, e sem elas ele mostraria
// "de 1º de agosto até hoje", que inclui setembro inteiro e não responde a pergunta.
//
// ⚠️ A régua saiu do `switch` de dentro do controller pra cá porque agora ela tem dois valores
// pra acertar em vez de um, e porque é ela que decide o rótulo que a tela escreve. Errar o fim
// do intervalo é errar todo número de dinheiro da página de uma vez.
public class PeriodoDoFinanceiroTests
{
    // Uma quarta-feira. Escolhida de propósito no meio do mês e da semana: começo ou fim
    // esconderiam erro de borda nos dois cálculos.
    private static readonly DateTime Hoje = new(2026, 9, 2);

    [Fact]
    public void Mes_passado_comeca_no_dia_1_e_TERMINA_no_ultimo_dia_dele()
    {
        var faixa = PeriodoDoFinanceiro.Intervalo("mespassado", Hoje);

        Assert.Equal(new DateTime(2026, 8, 1), faixa.De);
        Assert.Equal(new DateTime(2026, 9, 1), faixa.Ate);
        Assert.Contains("agosto", faixa.Rotulo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Em_janeiro_o_mes_passado_e_dezembro_do_ano_anterior()
    {
        // A virada de ano é onde `mes - 1` quebra sozinho.
        var faixa = PeriodoDoFinanceiro.Intervalo("mespassado", new DateTime(2027, 1, 15));

        Assert.Equal(new DateTime(2026, 12, 1), faixa.De);
        Assert.Equal(new DateTime(2027, 1, 1), faixa.Ate);
    }

    [Fact]
    public void O_fim_e_EXCLUSIVO_pra_nao_perder_a_aula_da_meia_noite()
    {
        // Data de aula tem hora. Com fim inclusivo em "31/08", a aula das 20h do dia 31 ficaria
        // de fora — e o professor veria um mês fechado com uma aula a menos do que deu.
        var faixa = PeriodoDoFinanceiro.Intervalo("mespassado", Hoje);
        var ultimaAulaDeAgosto = new DateTime(2026, 8, 31, 20, 0, 0);

        Assert.True(ultimaAulaDeAgosto >= faixa.De && ultimaAulaDeAgosto < faixa.Ate);
    }

    [Fact]
    public void A_aula_de_setembro_NAO_entra_no_mes_passado()
    {
        var faixa = PeriodoDoFinanceiro.Intervalo("mespassado", Hoje);
        var aulaDeHoje = new DateTime(2026, 9, 2, 9, 0, 0);

        Assert.False(aulaDeHoje < faixa.Ate && aulaDeHoje >= faixa.De);
    }

    [Theory]
    [InlineData("semana")]
    [InlineData("mes")]
    [InlineData("ano")]
    [InlineData("sempre")]
    public void Os_periodos_que_ja_existiam_continuam_ABERTOS(string periodo)
    {
        // ⚠️ Regressão da régua antiga: eles somam "até hoje", e todo card, a lista de quem
        // deve e a tabela por local dependem disso. Fechar um deles por engano mudaria número
        // de dinheiro em tela que ninguém pediu pra mudar.
        var faixa = PeriodoDoFinanceiro.Intervalo(periodo, Hoje);

        Assert.Null(faixa.Ate);
    }

    [Fact]
    public void Semana_comeca_na_SEGUNDA()
    {
        // A mesma régua do card de semanas do mês (Services/SemanasDoMes) — duas definições de
        // semana na mesma tela seriam dois números pro mesmo dia.
        var faixa = PeriodoDoFinanceiro.Intervalo("semana", Hoje);

        Assert.Equal(new DateTime(2026, 8, 31), faixa.De);
        Assert.Equal(DayOfWeek.Monday, faixa.De.DayOfWeek);
    }

    [Fact]
    public void Mes_e_ano_comecam_onde_sempre_comecaram()
    {
        Assert.Equal(new DateTime(2026, 9, 1), PeriodoDoFinanceiro.Intervalo("mes", Hoje).De);
        Assert.Equal(new DateTime(2026, 1, 1), PeriodoDoFinanceiro.Intervalo("ano", Hoje).De);
        Assert.Equal(DateTime.MinValue, PeriodoDoFinanceiro.Intervalo("sempre", Hoje).De);
    }

    [Fact]
    public void Periodo_desconhecido_cai_no_mes_corrente()
    {
        // O padrão de sempre. Vale pra URL escrita à mão e pro link velho no favorito.
        var faixa = PeriodoDoFinanceiro.Intervalo("qualquer-coisa", Hoje);

        Assert.Equal(new DateTime(2026, 9, 1), faixa.De);
        Assert.Null(faixa.Ate);
    }

    [Fact]
    public void A_aula_entra_no_periodo_por_UMA_regra_so()
    {
        // O controller filtrava com `>= de` solto. Com dois períodos de forma diferente na
        // mesma tela, a comparação tem que morar num lugar só — senão o card do topo e a
        // lista de devedores acabam discordando sobre a mesma aula.
        var fechado = PeriodoDoFinanceiro.Intervalo("mespassado", Hoje);
        var aberto = PeriodoDoFinanceiro.Intervalo("mes", Hoje);

        Assert.True(fechado.Contem(new DateTime(2026, 8, 15)));
        Assert.False(fechado.Contem(new DateTime(2026, 9, 2)));
        Assert.False(fechado.Contem(new DateTime(2026, 7, 31)));

        Assert.True(aberto.Contem(new DateTime(2026, 9, 2)));
        Assert.False(aberto.Contem(new DateTime(2026, 8, 31)));
    }
}
