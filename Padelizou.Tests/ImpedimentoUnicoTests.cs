using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// Dupla que marca todos os turnos está dizendo "não podemos jogar em nenhum horário do
// torneio": o chaveamento não tem onde encaixá-la, e isso só aparecia na hora de montar a
// grade. A tela deixa marcar um só, e esta é a segunda tranca — página em cache e POST feito
// à mão não passam pela tela.
public class ImpedimentoUnicoTests
{
    [Fact]
    public void Um_marcado_continua_marcado()
    {
        Assert.Equal((true, false, false, false), ImpedimentoUnico.Apenas(true, false, false, false));
        Assert.Equal((false, true, false, false), ImpedimentoUnico.Apenas(false, true, false, false));
        Assert.Equal((false, false, true, false), ImpedimentoUnico.Apenas(false, false, true, false));
        Assert.Equal((false, false, false, true), ImpedimentoUnico.Apenas(false, false, false, true));
    }

    [Fact]
    public void Nenhum_marcado_continua_nenhum()
        => Assert.Equal((false, false, false, false), ImpedimentoUnico.Apenas(false, false, false, false));

    [Theory]
    // Vindo mais de um, fica o primeiro na ordem da tela — que é a ordem do relógio: quinta,
    // sexta, sábado de manhã, sábado à tarde. Recusar a inscrição inteira perderia o
    // cadastro por um detalhe contornável.
    [InlineData(true, true, false, false, true, false, false, false)]
    [InlineData(false, true, true, false, false, true, false, false)]
    [InlineData(false, true, false, true, false, true, false, false)]
    [InlineData(false, false, true, true, false, false, true, false)]
    [InlineData(true, true, true, true, true, false, false, false)]
    public void Mais_de_um_fica_so_o_primeiro(
        bool quinta, bool sexta, bool manha, bool tarde,
        bool esperaQuinta, bool esperaSexta, bool esperaManha, bool esperaTarde)
        => Assert.Equal(
            (esperaQuinta, esperaSexta, esperaManha, esperaTarde),
            ImpedimentoUnico.Apenas(quinta, sexta, manha, tarde));

    // A quinta entra na frente da sexta: quem marcou os dois quis dizer "a abertura", e o
    // primeiro dia é o que o organizador precisa saber pra montar a grade.
    [Fact]
    public void Quinta_ganha_da_sexta_quando_os_dois_vem_marcados()
        => Assert.Equal((true, false, false, false), ImpedimentoUnico.Apenas(true, true, false, false));
}
