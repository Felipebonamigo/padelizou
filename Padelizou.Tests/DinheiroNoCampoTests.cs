using System.Globalization;
using Padelizou.Services;

namespace Padelizou.Tests;

// A OUTRA METADE do bug de dinheiro. O DinheiroModelBinder resolveu a ENTRADA (o navegador
// manda "79.90" e o pt-BR lia 7990); isto é a SAÍDA.
//
// ⚠️ `value="150,00"` num <input type="number"> é valor inválido, e o navegador não reclama:
// mostra o campo VAZIO. Com a cultura pt-BR do app, escrever `@Model.PrecoInscricao` direto no
// HTML produz exatamente isso — foi assim que o preço da inscrição sumia toda vez que o
// organizador abria "Editar Dados do Torneio", e o mesmo valia pros preços do professor.
//
// Os testes rodam com a cultura pt-BR fixada de propósito: em máquina com cultura invariante
// eles passariam sem provar nada, que é como esse defeito chegou à produção.
public class DinheiroNoCampoTests
{
    private static T ComCulturaBrasileira<T>(Func<T> acao)
    {
        var antes = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
        try { return acao(); }
        finally { CultureInfo.CurrentCulture = antes; }
    }

    // O valor vem como TEXTO e é convertido aqui: `[InlineData(79.90)]` passaria por `double`
    // no caminho e chegaria como 79,9 — o teste mediria um número que não é o do banco.
    [Theory]
    [InlineData("150", "150")]
    [InlineData("0", "0")]
    [InlineData("79.90", "79.90")]      // o caso que o pt-BR estragava: centavos
    [InlineData("1234.56", "1234.56")]  // e sem separador de milhar: type=number não aceita
    public void Escreve_sempre_com_PONTO_mesmo_em_pt_BR(string valor, string esperado)
    {
        var quanto = decimal.Parse(valor, CultureInfo.InvariantCulture);

        Assert.Equal(esperado, ComCulturaBrasileira(() => DinheiroNoCampo.Valor(quanto)));
    }

    [Fact]
    public void O_que_o_pt_BR_geraria_sozinho_e_justamente_o_que_o_navegador_recusa()
    {
        // Guarda o porquê: se um dia alguém "simplificar" de volta pro ToString() direto,
        // este teste mostra a vírgula aparecendo.
        var comoEra = ComCulturaBrasileira(() => 79.90m.ToString());
        Assert.Contains(",", comoEra);

        var comoE = ComCulturaBrasileira(() => DinheiroNoCampo.Valor(79.90m));
        Assert.DoesNotContain(",", comoE);
    }

    [Fact]
    public void Valor_opcional_vazio_continua_VAZIO_e_nao_vira_zero()
    {
        // Campo opcional em branco não pode aparecer como "0": o professor confirmaria um
        // preço que nunca digitou, e aula de graça é diferente de aula sem preço definido.
        Assert.Equal("", DinheiroNoCampo.Valor((decimal?)null));
        Assert.Equal("0", DinheiroNoCampo.Valor((decimal?)0m));
    }

    [Fact]
    public void O_que_sai_daqui_volta_igual_pelo_binder_de_entrada()
    {
        // As duas metades têm que fechar o ciclo: o que a tela escreve, o servidor relê igual.
        foreach (var valor in new[] { 0m, 9m, 79.90m, 150m, 1234.56m })
        {
            var noHtml = ComCulturaBrasileira(() => DinheiroNoCampo.Valor(valor));
            Assert.Equal(valor, DinheiroModelBinder.Ler(noHtml));
        }
    }
}
