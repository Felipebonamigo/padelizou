using Padelizou.Services;

namespace Padelizou.Tests;

// "há 3 min", "ontem", "12/08". Era uma função local dentro da tela de Notificações; virou
// classe quando a segunda tela precisou do mesmo texto.
public class TempoRelativoTests
{
    private static readonly DateTime Agora = new(2026, 8, 10, 15, 0, 0);

    [Theory]
    [InlineData(0, "agora")]
    [InlineData(3, "há 3 min")]
    [InlineData(59, "há 59 min")]
    [InlineData(60, "há 1h")]
    [InlineData(60 * 5, "há 5h")]
    public void Curto_conta_minutos_e_horas(int minutosAtras, string esperado) =>
        Assert.Equal(esperado, TempoRelativo.Curto(Agora.AddMinutes(-minutosAtras), Agora));

    [Fact]
    public void Curto_vira_ontem_e_depois_dias()
    {
        Assert.Equal("ontem", TempoRelativo.Curto(Agora.AddHours(-30), Agora));
        Assert.Equal("há 3 dias", TempoRelativo.Curto(Agora.AddDays(-3), Agora));
    }

    [Fact]
    public void Depois_de_uma_semana_o_Curto_mostra_a_data()
    {
        // Passada uma semana, "há 23 dias" já não ajuda ninguém a se localizar.
        Assert.Equal("18/07", TempoRelativo.Curto(new DateTime(2026, 7, 18), Agora));
    }

    [Fact]
    public void Relogio_adiantado_nao_vira_tempo_negativo()
    {
        // Relógio de quem escreveu e relógio do servidor discordam por segundos, e "há -1 min"
        // é o tipo de coisa que faz a pessoa desconfiar do resto da tela.
        Assert.Equal("agora", TempoRelativo.Curto(Agora.AddSeconds(30), Agora));
    }

    [Theory]
    [InlineData(0, "desde hoje")]
    [InlineData(1, "desde ontem")]
    [InlineData(10, "há 10 dias")]
    public void Desde_fala_de_vinculo_que_continua(int diasAtras, string esperado) =>
        Assert.Equal(esperado, TempoRelativo.Desde(Agora.AddDays(-diasAtras), Agora));

    [Fact]
    public void Vinculo_antigo_vira_mes_e_ano()
    {
        // Ninguém guarda o dia em que ganhou um seguidor.
        Assert.Equal("desde 03/2026", TempoRelativo.Desde(new DateTime(2026, 3, 14), Agora));
    }
}
