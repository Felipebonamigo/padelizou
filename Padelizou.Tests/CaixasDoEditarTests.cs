using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — NO GERENCIAR › EDITAR, QUATRO CAIXAS SÓ CONSEGUIAM DESLIGAR.
//
// Achado no ensaio do torneio do Er numa app de verdade: "Permitir o mesmo jogador em mais de
// uma categoria" marcada, salvo, e `PermiteMultiplasCategorias` gravado FALSE — as quatro
// inscrições da Mista A recusadas com "cada jogador só pode disputar uma categoria".
//
// 🕳️ O `<input type="hidden" value="false">` vinha ANTES do checkbox de mesmo nome. O POST leva
// `false,true` e o binder de `bool`/`bool?` fica com o PRIMEIRO valor. A ordem certa (checkbox
// primeiro, hidden depois — a que o `asp-for` gera) já estava escrita e explicada na mesma tela,
// nas caixas do MVP e da quinta à noite; estas quatro nasceram invertidas.
//
// ⚠️ POR QUE TESTE DE FONTE: a suíte não renderiza Razor nem passa pelo model binder — teste de
// controller chama a ação direto com o `bool` já decidido, e por isso nunca veria isto. O que
// existe só na VIEW, a view trava.
public class CaixasDoEditarTests
{
    // Toda caixa do formulário de edição que tem um `hidden` de mesmo nome pra mandar "false"
    // quando desmarcada. Quem criar uma caixa nova nesse desenho entra aqui.
    [Theory]
    [InlineData("permiteMultiplasCategorias")]
    [InlineData("excluirSeNaoPagar")]
    [InlineData("pontuaNoRankingAmericano")]
    [InlineData("desempateAmericano")]
    [InlineData("usaVotacaoDeMvp")]
    [InlineData("permiteImpedimentoQuintaNoite")]
    public void O_hidden_false_vem_DEPOIS_do_checkbox_de_mesmo_nome(string nome)
    {
        var fonte = Details();
        var inputs = Regex.Matches(fonte, "<input\\b[^>]*>", RegexOptions.Singleline)
            .Select(m => (Texto: m.Value, Posicao: m.Index))
            .Where(i => i.Texto.Contains($"name=\"{nome}\"", StringComparison.Ordinal))
            .ToList();

        var checkbox = inputs.Where(i => i.Texto.Contains("type=\"checkbox\"", StringComparison.Ordinal)).ToList();
        var hidden = inputs.Where(i => i.Texto.Contains("type=\"hidden\"", StringComparison.Ordinal)).ToList();
        Assert.True(checkbox.Count == 1, $"Esperava UM checkbox name=\"{nome}\" no Details.cshtml, achei {checkbox.Count}.");
        Assert.True(hidden.Count == 1, $"Esperava UM hidden name=\"{nome}\" no Details.cshtml, achei {hidden.Count}.");

        Assert.True(checkbox[0].Posicao < hidden[0].Posicao,
            $"O hidden value=\"false\" de \"{nome}\" vem ANTES do checkbox: marcado, o POST leva false,true e grava false.");
    }

    private static string Details() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

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
