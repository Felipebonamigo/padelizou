using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — O HORÁRIO DO JOGO PRECISA SER LIDO DE RELANCE.
//
// 🗣️ Deivid Santos, do 2ª Etapa ER PADEL TOUR: *"Os horários tão um pouco pequenos"* — com o
// print aproximado da linha do jogo.
//
// A hora é o dado que a pessoa procura numa lista de 97 jogos, e ela estava do mesmo tamanho
// do texto comum (1rem), com a data menor ainda que a etiqueta de categoria ao lado. Quem
// varre a lista no celular, no clube, de pé, lê a hora antes de qualquer outra coisa.
//
// ⚠️ CSS não quebra build nem teste de comportamento: sem esta trava, uma limpeza devolve os
// tamanhos antigos em silêncio, e o defeito só aparece pra quem abrir a tela no celular.
public class HorarioDoJogoLegivelTests
{
    [Fact]
    public void A_hora_e_maior_que_o_texto_comum_da_linha()
    {
        var hora = TamanhoEmRem(@"\.pdz-jl-quando\s+strong\s*\{[^}]*font-size:\s*([\d.]+)rem");

        Assert.True(hora >= 1.1, $"A hora do jogo está em {hora}rem — pequena demais pra lista do celular.");
    }

    [Fact]
    public void A_data_tambem_cresce_junto()
    {
        var data = TamanhoEmRem(@"\.pdz-jl-quando\s+span\s*\{[^}]*font-size:\s*([\d.]+)rem");

        // Ela vinha em .72rem — MENOR que a etiqueta cinza da quadra ao lado (.68rem era o
        // texto; a etiqueta tem fundo e peso 700, então lia maior). Aumentar só a hora deixaria
        // o dia da semana, que nasceu neste mesmo pedido, ilegível do lado dela.
        Assert.True(data >= 0.8, $"A data do jogo está em {data}rem — pequena demais pra lista do celular.");
    }

    private static double TamanhoEmRem(string padrao)
    {
        var achado = Regex.Match(Css(), padrao, RegexOptions.Singleline);
        Assert.True(achado.Success, $"Não achei o `font-size` de `{padrao}` no site.css.");
        return double.Parse(achado.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    private static string Css() =>
        File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "wwwroot", "css", "site.css"));

    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "wwwroot", "css", "site.css")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return dir!.FullName;
    }
}
