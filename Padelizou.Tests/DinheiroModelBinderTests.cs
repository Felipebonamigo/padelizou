using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O bug que originou isto: a cultura do app é pt-BR, onde "." é separador de milhar, e todo
// campo de dinheiro da tela é <input type="number"> — que manda "79.90" mesmo mostrando
// "79,90" pro usuário. O binder padrão lia 7990. Uma inscrição de R$ 79,90 virava R$ 7.990,00
// sem erro nenhum, e só não estourou antes porque os preços em uso eram redondos.
public class DinheiroModelBinderTests
{
    [Theory]
    // O que o <input type="number"> manda de verdade: ponto decimal, sem milhar.
    [InlineData("79.90", 79.90)]
    [InlineData("150.00", 150)]
    [InlineData("0.01", 0.01)]
    [InlineData("1200.00", 1200)]
    [InlineData("150", 150)]
    [InlineData("0", 0)]
    public void O_que_o_navegador_manda_e_lido_certo(string texto, double esperado)
        => Assert.Equal((decimal)esperado, DinheiroModelBinder.Ler(texto));

    [Theory]
    // Digitado à mão, no formato brasileiro.
    [InlineData("79,90", 79.90)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("R$ 89,90", 89.90)]
    [InlineData(" 45,50 ", 45.50)]
    // Formato americano com milhar: o último separador manda.
    [InlineData("1,234.56", 1234.56)]
    public void Tambem_le_o_que_a_pessoa_digita(string texto, double esperado)
        => Assert.Equal((decimal)esperado, DinheiroModelBinder.Ler(texto));

    [Fact]
    public void Um_ponto_sozinho_e_sempre_decimal()
    {
        // "1.500" vira 1,5 e não 1500: o <input type="number"> nunca manda separador de
        // milhar, então um ponto sozinho só pode ser a vírgula decimal. Escolha deliberada —
        // o caso que importa é o preço com centavos, não o de quatro dígitos digitado torto.
        Assert.Equal(1.5m, DinheiroModelBinder.Ler("1.500"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("--")]
    public void Vazio_e_lixo_nao_viram_numero(string? texto)
        => Assert.Null(DinheiroModelBinder.Ler(texto));

    [Fact]
    public void Negativo_continua_negativo()
    {
        // A regra do que é aceitável (min=0 no campo, validação no controller) não é daqui:
        // o binder só traduz texto em número. Engolir o sinal aqui esconderia o problema.
        Assert.Equal(-10m, DinheiroModelBinder.Ler("-10"));
    }
}
