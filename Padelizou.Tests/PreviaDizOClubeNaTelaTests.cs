using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// A PRÉVIA TAMBÉM TEM CLUBE, E AS TRÊS TELAS QUE A DESENHAM PRECISAM PEDI-LO (10/09/2026).
//
// 🗣️ Felipe, num print do quadro do 2ª Etapa ER PADEL TOUR: *"quartas de final ta sem clube"*.
// As quartas tiveram a hora digitada na mão, e "hora digitada não traz quadra: o robô escolhe"
// (TorneiosController.DefinirHorario grava `NomeQuadra = null` na reserva). Dali em diante o
// jogo previsto não tinha NADA pra dizer onde é — e o clube ele tem: quando a reserva nasce
// jogo de verdade, ela pula o encaixe e `OrdemDeLiberacao.CarimbarOClube` a carimba com o clube
// do torneio. É esse carimbo que `JogoQueVem.ClubeId` antecipa.
//
// 🗣️ E a régua é do próprio Felipe, 10/09: *"nao precisa ter a quadra definida, mas o clube
// sempre tem q estar definido"*.
//
// ⚠️ SÃO TRÊS TELAS PORQUE O MESMO JOGO PREVISTO APARECE EM TRÊS (o cartão do quadro, a linha da
// aba Jogos e a opção do modal de troca) — é o "nove telas mostrando a mesma etiqueta" do
// cabeçalho de LugarDoJogo, e a que fica pra trás é sempre a que ninguém lembra que existe.
//
// ⚠️ POR QUE TESTE DE FONTE: a suíte não renderiza Razor.
public class PreviaDizOClubeNaTelaTests
{
    [Theory]
    // O cartão da prévia no quadro do mata-mata — o do print.
    [InlineData("Torneios/Details.cshtml", "previsto")]
    // A opção "trocar com qual jogo?" do modal de horário, no mesmo arquivo da lista.
    [InlineData("Torneios/_JogosDoTorneio.cshtml", "previsto")]
    // A linha do jogo previsto na aba Jogos.
    [InlineData("Torneios/_JogoQueVem.cshtml", "Model")]
    public void A_etiqueta_do_jogo_previsto_pede_categoria_E_clube(string view, string jogo)
    {
        var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", view));

        Assert.Matches(new Regex(
            @"LugarDoJogo\.Etiqueta\(\s*ViewData\.Sedes\(\),\s*"
            + Regex.Escape(jogo) + @"\.Quadra,\s*"
            + Regex.Escape(jogo) + @"\.CategoriaId,\s*"
            + Regex.Escape(jogo) + @"\.ClubeId\s*\)"), fonte);
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
