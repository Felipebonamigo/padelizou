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

    private static string Js(string nome)
    {
        var wwwroot = Path.GetDirectoryName(TestInfra.PastaDasFontesDeVerdade())!;
        return File.ReadAllText(Path.Combine(wwwroot, "js", nome));
    }

    // ⚠️ A CADEIA GANHOU UM ELO EM 10/09/2026: as setas saíram da barra e foram pro menu ⋯
    // (🗣️ *"relogio e as setas"*), então a tela chama `_MenuDoJogo` e é ELE quem chama
    // `_SetasDaOrdem`. O teste segue os dois passos de propósito — cobrar só o menu deixaria
    // passar um menu vazio, que é o mesmo defeito de quando a prévia ficou semanas sem botão
    // de horário nenhum.
    [Fact]
    public void O_jogo_real_agendado_tem_as_setas()
    {
        var fonte = View("_JogoEmLinha.cshtml");

        Assert.Contains("_MenuDoJogo", fonte);
        Assert.Contains("_SetasDaOrdem", View("_MenuDoJogo.cshtml"));
        // Só em jogo agendado: mover quem está em quadra é reescrever o que está acontecendo.
        Assert.Contains("jogo.Status == \"Agendada\"", fonte);
    }

    // A prévia entra na mesma fila — as semifinais e finais de domingo do Er ainda não nasceram.
    [Fact]
    public void A_previa_tambem_tem_as_setas()
    {
        Assert.Contains("_MenuDoJogo", View("_JogoQueVem.cshtml"));
        Assert.Contains("_SetasDaOrdem", View("_MenuDoJogo.cshtml"));
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

        // ⚠️ O `chegadas` faz parte da régua desde 12/09/2026 (a ordem por presença): a tela tem
        // que passar o que SABE de quem chegou, senão ela ordena diferente das setas de novo.
        Assert.Contains("OrdemNoHorario.Ordenar(agendadasList, jogosQueVem,", fonte);
        Assert.Contains("ChegadasNoTorneio", fonte);
        Assert.DoesNotContain(".OrderBy(x => x.Horario ?? DateTime.MaxValue)", fonte);
    }

    // ═══ A TELA NÃO PODE VOLTAR PRO TOPO A CADA CLIQUE (10/09/2026) ═══
    //
    // 🗣️ Felipe, num print da lista do Er rolada até as quartas de domingo: *"quando eu trocar
    // aqui, ele tem q permanecer no mesmo local da tela, esta indo para o inicio"*.
    //
    // 🕳️ Toda ação do organizador é POST → redirect → GET, e a página nova nasce no começo. Numa
    // lista de 97 jogos, arrumar a ordem de sete semifinais custava sete rolagens até achar de
    // novo onde se estava. É a MESMA queixa de 08/08 que criou o js/jogos-abas.js ("ele tem que
    // se manter na tela que eu estou editando") — e a resposta é a mesma peça: sessionStorage.
    [Fact]
    public void As_setas_pedem_pra_tela_ficar_onde_esta()
    {
        Assert.Contains("data-manter-posicao", View("_SetasDaOrdem.cshtml"));
    }

    [Fact]
    public void As_duas_telas_que_mostram_a_lista_carregam_o_script()
    {
        // Details (aba Jogos embutida) e /Torneios/Jogos — as mesmas duas do js/jogos-abas.js.
        Assert.Contains("manter-posicao-na-lista.js", View("Details.cshtml"));
        Assert.Contains("manter-posicao-na-lista.js", View("jogos.cshtml"));
    }

    [Fact]
    public void O_script_guarda_a_posicao_e_sobrevive_a_memoria_proibida()
    {
        var fonte = Js("manter-posicao-na-lista.js");

        Assert.Contains("sessionStorage", fonte);
        Assert.Contains("scrollY", fonte);
        // Navegação privada com cookies bloqueados faz o sessionStorage ESTOURAR no acesso.
        // Sem try/catch, a exceção derruba o script e a lista some de comportamento — a mesma
        // defesa que o js/jogos-abas.js já tem.
        Assert.Contains("catch", fonte);
        // Consumida UMA vez: sem apagar, qualquer visita seguinte àquela página seria arrastada
        // pra uma posição escolhida em outro momento.
        Assert.Contains("removeItem", fonte);
    }
}
