using System.Text.RegularExpressions;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A CHAVE QUE O ORGANIZADOR ESCOLHEU TEM QUE CABER NO CAMPO DE QUEM SE INSCREVE (20/09/2026).
//
// 🐛 O CASO: Felipe cadastrou `CORNETA0310` (11 caracteres) no "Los Corneteiros | Seletiva
// QTimes" e, na inscrição, o campo parava de aceitar letra em `CORNET`. 🗣️ *"na hora da
// inscrição tem limite de 6 caracteres"*. Ninguém conseguia entrar no torneio.
//
// 🕳️ A ORIGEM É UMA REGRA QUE MUDOU DE UM LADO SÓ. A chave nasceu SORTEADA com 6 caracteres
// (`ChaveDeAcessoDoTorneio.Sortear`), e o campo da inscrição foi escrito com esse 6 literal.
// Depois o organizador ganhou o direito de ESCOLHER a chave, "de 4 a 20" — e o outro lado do
// fluxo ficou onde estava. O servidor nunca cortou nada: ele só compara. O `maxlength` era o
// obstáculo inteiro.
//
// ⚠️ POR ISSO O TESTE OLHA A CONSTANTE, E NÃO O NÚMERO 20: amarrar no literal reproduziria
// exatamente o defeito que ele existe pra travar — alguém muda `TamanhoMaximo` e os campos
// ficam pra trás de novo, calados, e o sintoma só aparece pra quem tenta se inscrever.
public class ChaveDeAcessoCabeNaInscricaoTests
{
    [Fact]
    public void Todo_campo_de_chave_da_inscricao_aceita_a_chave_mais_longa_que_o_organizador_pode_criar()
    {
        var html = File.ReadAllText(Path.Combine(
            RaizDoRepo(), "Padelizou", "Views", "Torneios", "Details.cshtml"));

        var campos = Regex.Matches(html, @"<input[^>]*name=""chaveAcesso""[^>]*>")
            .Select(m => m.Value)
            .ToList();

        // Sem isto, apagar os campos (ou renomeá-los) deixaria o teste verde varrendo o vazio.
        // São DOIS hoje: a inscrição de dupla e a do americano.
        Assert.True(campos.Count >= 2, $"Esperava ao menos 2 campos de chave na inscrição; achei {campos.Count}.");

        foreach (var campo in campos)
        {
            var limite = Regex.Match(campo, @"maxlength=""([^""]*)""");
            Assert.True(limite.Success, $"Campo de chave sem maxlength — some com o limite ou ponha o certo:\n{campo}");
            var valor = limite.Groups[1].Value;

            // Duas grafias servem, e só duas. A que vale de verdade é a PRIMEIRA: apontar pra
            // constante não fica pra trás quando ela mudar. Um número solto ainda passa, desde
            // que caiba — mas é justamente o jeito que já falhou uma vez.
            if (valor.Contains("ChaveDeAcessoDoTorneio.TamanhoMaximo", StringComparison.Ordinal)) continue;

            Assert.True(int.TryParse(valor, out var numero),
                $"maxlength não é número nem aponta pra ChaveDeAcessoDoTorneio.TamanhoMaximo:\n{campo}");
            Assert.True(numero >= ChaveDeAcessoDoTorneio.TamanhoMaximo,
                $"O campo corta antes da chave mais longa que o organizador pode criar "
                + $"({ChaveDeAcessoDoTorneio.TamanhoMaximo}):\n{campo}");
        }
    }

    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "Padelizou.csproj")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return dir!.FullName;
    }
}
