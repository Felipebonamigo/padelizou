using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// Dupla que marca os três turnos está dizendo "não podemos jogar em nenhum horário do
// torneio": o chaveamento não tem onde encaixá-la, e isso só aparecia na hora de montar a
// grade. A tela deixa marcar um só, e esta é a segunda tranca — página em cache e POST feito
// à mão não passam pela tela.
public class ImpedimentoUnicoTests
{
    [Fact]
    public void Um_marcado_continua_marcado()
    {
        Assert.Equal((true, false, false), ImpedimentoUnico.Apenas(true, false, false));
        Assert.Equal((false, true, false), ImpedimentoUnico.Apenas(false, true, false));
        Assert.Equal((false, false, true), ImpedimentoUnico.Apenas(false, false, true));
    }

    [Fact]
    public void Nenhum_marcado_continua_nenhum()
        => Assert.Equal((false, false, false), ImpedimentoUnico.Apenas(false, false, false));

    [Theory]
    // Vindo mais de um, fica o primeiro na ordem da tela: sexta, sábado de manhã, sábado à
    // tarde. Recusar a inscrição inteira perderia o cadastro por um detalhe contornável.
    [InlineData(true, true, false, true, false, false)]
    [InlineData(true, false, true, true, false, false)]
    [InlineData(false, true, true, false, true, false)]
    [InlineData(true, true, true, true, false, false)]
    public void Mais_de_um_fica_so_o_primeiro(
        bool sexta, bool manha, bool tarde,
        bool esperaSexta, bool esperaManha, bool esperaTarde)
        => Assert.Equal(
            (esperaSexta, esperaManha, esperaTarde),
            ImpedimentoUnico.Apenas(sexta, manha, tarde));
}
