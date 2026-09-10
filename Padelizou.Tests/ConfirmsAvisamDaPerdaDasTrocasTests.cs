using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — TODO BOTÃO QUE RECALCULA A GRADE AVISA QUE AS TROCAS NA MÃO SE PERDEM.
//
// O Felipe passou uma noite trocando horário na mão no Er e pediu o aviso no "Recalcular
// horários" (PR #123). O ensaio do torneio numa app de verdade mostrou que dois outros botões
// fazem a MESMA coisa por baixo (`RecalcularAGradeAsync` apaga as reservas e desfaz as trocas)
// e não diziam: "Trocar as duas de grupo" e "Desfazer o sorteio". Perder uma noite de ajustes
// por um confirm que só falou dos confrontos é o defeito que o #123 existiu pra evitar.
//
// Teste de FONTE porque a suíte não renderiza Razor; o texto só existe na view.
public class ConfirmsAvisamDaPerdaDasTrocasTests
{
    [Theory]
    [InlineData("Torneios/Details.cshtml", "DesfazerSorteio")]
    [InlineData("Torneios/Details.cshtml", "TrocarDuplasDeGrupo")]
    [InlineData("Torneios/Details.cshtml", "RefazerGrade")]
    public void O_confirm_diz_que_as_trocas_na_mao_e_as_reservas_se_perdem(string view, string acao)
    {
        var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", view));
        var form = Regex.Match(fonte, $"<form[^>]*asp-action=\"{acao}\"[^>]*>", RegexOptions.Singleline);
        Assert.True(form.Success, $"Não achei o <form asp-action=\"{acao}\"> em {view}.");

        var confirm = Regex.Match(form.Value, "data-confirmar=\"([^\"]*)\"");
        Assert.True(confirm.Success, $"O form de {acao} não tem data-confirmar.");
        Assert.Contains("troca", confirm.Groups[1].Value, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reserva", confirm.Groups[1].Value, StringComparison.OrdinalIgnoreCase);
    }

    private static string PastaDoProjeto()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou");
            if (File.Exists(Path.Combine(tentativa, "Padelizou.csproj"))) return tentativa;
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new DirectoryNotFoundException("Padelizou.csproj não encontrado a partir do bin.");
    }
}
