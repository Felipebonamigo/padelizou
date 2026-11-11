using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// TROCAR HORÁRIO PELA JANELA NÃO JOGA A TELA PRO TOPO (11/11/2026).
//
// 🗣️ Felipe, com a lista de jogos do Chakra: *"quando eu troco o horário ou coloco pra cima e baixo,
// ele volta lá em cima na tela, como se recarregasse o site"*.
//
// As setas ↑↓ (_SetasDaOrdem) já tinham `data-manter-posicao` e ficavam no lugar — medido no
// navegador, 793px antes e depois. As JANELAS (modais) de trocar horário, definir horário e trocar
// quadra não tinham: medido 2221px antes e 889px depois (o começo da lista). Sem a marca, o
// js/manter-posicao-na-lista.js nem fica sabendo do envio.
public class JanelasDeHorarioFicamNoLugarTests
{
    private static string Lista() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
        "Padelizou", "Views", "Torneios", "_JogosDoTorneio.cshtml"));

    [Theory]
    [InlineData("TrocarHorario")]
    [InlineData("DefinirHorario")]
    [InlineData("TrocarQuadra")]
    public void A_janela_guarda_a_altura_da_tela_ao_enviar(string acao)
    {
        var form = Regex.Match(Lista(), $"<form[^>]*asp-action=\"{acao}\"[^>]*>", RegexOptions.Singleline);
        Assert.True(form.Success, $"não achei o formulário de {acao}");
        Assert.Contains("data-manter-posicao", form.Value);
    }
}
