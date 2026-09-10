using System.IO;
using Xunit;

namespace Padelizou.Tests;

// AS SETAS ↑↓ EXISTEM NA TELA, E A LISTA SAI DA MESMA RÉGUA DELAS (10/09/2026).
//
// 🗣️ *"tem q trocar tambem para q eu possa colocar a ordem que eu quiser"*. O servidor pode estar
// certíssimo e o organizador continuar sem conseguir arrumar o domingo, porque o botão não está
// lá — foi assim que a prévia passou semanas sem nenhum botão de horário. Estes testes leem a
// PRÓPRIA view, que é o recurso que o projeto já usa pro que Razor não deixa testar de outro jeito.
public class SetasDaOrdemNaTelaTests
{
    private static string View(string nome)
    {
        var wwwroot = Path.GetDirectoryName(TestInfra.PastaDasFontesDeVerdade())!;
        return File.ReadAllText(Path.Combine(Path.GetDirectoryName(wwwroot)!, "Views", "Torneios", nome));
    }

    [Fact]
    public void O_jogo_real_agendado_tem_as_setas()
    {
        var fonte = View("_JogoEmLinha.cshtml");

        Assert.Contains("_SetasDaOrdem", fonte);
        // Só em jogo agendado: mover quem está em quadra é reescrever o que está acontecendo.
        Assert.Contains("jogo.Status == \"Agendada\"", fonte);
    }

    // A prévia entra na mesma fila — as semifinais e finais de domingo do Er ainda não nasceram.
    [Fact]
    public void A_previa_tambem_tem_as_setas()
    {
        Assert.Contains("_SetasDaOrdem", View("_JogoQueVem.cshtml"));
    }

    [Fact]
    public void As_setas_mandam_o_jogo_a_direcao_e_o_torneio()
    {
        var fonte = View("_SetasDaOrdem.cshtml");

        Assert.Contains("asp-action=\"MoverNoHorario\"", fonte);
        Assert.Contains("name=\"direcao\" value=\"cima\"", fonte);
        Assert.Contains("name=\"direcao\" value=\"baixo\"", fonte);
        Assert.Contains("name=\"jogo\"", fonte);
        Assert.Contains("name=\"id\"", fonte);
        // POST de organizador leva token — a ação recusa sem ele (ValidateAntiForgeryToken).
        Assert.Contains("@Html.AntiForgeryToken()", fonte);
    }

    // ⚠️ A LISTA E AS SETAS PRECISAM CONCORDAR. Se a tela ordenasse por conta própria, ↑ moveria
    // o jogo pra um lugar diferente do que ela mostrou — e o organizador clicaria de novo achando
    // que não funcionou. A régua é uma só: Services/OrdemNoHorario.
    [Fact]
    public void A_lista_da_aba_Jogos_ordena_pela_mesma_regua_das_setas()
    {
        var fonte = View("_JogosDoTorneio.cshtml");

        Assert.Contains("OrdemNoHorario.Ordenar(agendadasList, jogosQueVem)", fonte);
        Assert.DoesNotContain(".OrderBy(x => x.Horario ?? DateTime.MaxValue)", fonte);
    }
}
