using Padelizou.Models;
using Xunit;

namespace Padelizou.Tests;

// Quando o turno de QUINTA é oferecido. Ele não vale pra todo torneio: num fim de semana
// comum seria oferecer um dia que não existe.
public class TurnoDeQuintaTests
{
    private static Torneio Em(DayOfWeek dia)
    {
        // 09/08/2026 é um domingo — andar até o dia pedido dá uma data real de cada tipo.
        var domingo = new DateTime(2026, 8, 9);
        return new Torneio { Nome = "T", DataInicio = domingo.AddDays((int)dia) };
    }

    [Fact]
    public void Torneio_que_comeca_na_quinta_oferece_o_turno()
        => Assert.True(Em(DayOfWeek.Thursday).QuintaEhDiaDoTorneio);

    [Theory]
    [InlineData(DayOfWeek.Friday)]
    [InlineData(DayOfWeek.Saturday)]
    [InlineData(DayOfWeek.Wednesday)]
    public void Torneio_que_comeca_em_outro_dia_nao_oferece(DayOfWeek dia)
        => Assert.False(Em(dia).QuintaEhDiaDoTorneio);

    // Torneio sem data marcada ainda não é quinta-feira nenhuma.
    [Fact]
    public void Sem_data_nao_oferece()
        => Assert.False(new Torneio { Nome = "T" }.QuintaEhDiaDoTorneio);

    // A data mudou depois que alguém já marcou (e pagou) o impedimento de quinta: o turno
    // continua aparecendo, senão o organizador perderia de vista uma restrição que existe.
    [Fact]
    public void Ja_ligado_continua_aparecendo_mesmo_com_a_data_mudada()
    {
        var torneio = Em(DayOfWeek.Saturday);
        torneio.PermiteImpedimentoQuintaNoite = true;

        Assert.True(torneio.QuintaEhDiaDoTorneio);
    }
}
