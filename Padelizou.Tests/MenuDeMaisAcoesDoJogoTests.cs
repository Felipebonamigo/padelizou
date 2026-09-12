using System.IO;
using Xunit;

namespace Padelizou.Tests;

// O MENU ⋯ DA LINHA DE JOGO (10/09/2026).
//
// 🗣️ Felipe, depois de ver a barra de 7 botões quebrar em duas linhas no celular:
// *"tira o ⇄ e o relogio pra um menu ⋯"* e, corrigindo qual par vai: *"relogio e as setas"*.
//
// A barra visível fica com o que se usa no balcão, com o jogo na mão: ▶ começar, ⇄ trocar de
// horário com outro jogo, 📍 mudar de quadra, ✏️ marcar placar. Vão pro menu o relógio
// (definir horário na mão) e as setas ↑↓ (mover uma linha) — as duas do trabalho de ANTES,
// montando a grade em casa.
//
// ⚠️ ESTES TESTES SEGUEM A CADEIA, e não só a presença do nome. A partial do menu é quem
// carrega as setas e o relógio agora: cobrar só `_MenuDoJogo` em `_JogoEmLinha` deixaria passar
// um menu vazio, que é exatamente o defeito que a prévia já teve uma vez (semanas sem nenhum
// botão de horário).
public class MenuDeMaisAcoesDoJogoTests
{
    private static string View(string nome)
    {
        var wwwroot = Path.GetDirectoryName(TestInfra.PastaDasFontesDeVerdade())!;
        return File.ReadAllText(Path.Combine(Path.GetDirectoryName(wwwroot)!, "Views", "Torneios", nome));
    }

    [Fact]
    public void O_jogo_real_e_a_previa_abrem_o_menu()
    {
        Assert.Contains("_MenuDoJogo", View("_JogoEmLinha.cshtml"));
        Assert.Contains("_MenuDoJogo", View("_JogoQueVem.cshtml"));
    }

    [Fact]
    public void O_menu_carrega_as_setas_e_o_definir_horario()
    {
        var menu = View("_MenuDoJogo.cshtml");

        Assert.Contains("_SetasDaOrdem", menu);
        Assert.Contains("data-bs-target=\"#modalDefinirHorario\"", menu);
        // Sem o toggle do Bootstrap o ⋯ é um botão que não abre nada.
        Assert.Contains("data-bs-toggle=\"dropdown\"", menu);
    }

    // ⚠️ O ⇄ FICA NA BARRA, e isso é o pedido, não sobra: trocar dois jogos de horário é a
    // ação do dia de jogo — a chuva chegou, a dupla não veio —, e ela não pode custar dois
    // toques com o clube esperando.
    [Fact]
    public void O_trocar_horario_continua_a_um_toque_e_fora_do_menu()
    {
        Assert.Contains("data-bs-target=\"#modalTrocarHorario\"", View("_JogoEmLinha.cshtml"));
        Assert.Contains("data-bs-target=\"#modalTrocarHorario\"", View("_JogoQueVem.cshtml"));
        Assert.DoesNotContain("#modalTrocarHorario", View("_MenuDoJogo.cshtml"));
    }

    // A outra metade do pedido: se o relógio e as setas continuassem soltos na barra, o menu
    // teria virado um oitavo botão — o contrário do que ele pediu.
    [Fact]
    public void O_relogio_e_as_setas_sairam_da_barra()
    {
        foreach (var tela in new[] { "_JogoEmLinha.cshtml", "_JogoQueVem.cshtml" })
        {
            var fonte = View(tela);
            Assert.DoesNotContain("#modalDefinirHorario", fonte);
            Assert.DoesNotContain("_SetasDaOrdem", fonte);
        }
    }

    // ⚠️ AS SETAS VIRARAM ITEM DE MENU, e item de menu sem rótulo é um ícone órfão: no meio de
    // uma lista vertical, "↑" sozinho não diz para onde nem o quê. O ícone continua, o texto
    // é que passou a existir.
    [Fact]
    public void As_setas_no_menu_dizem_o_que_fazem_por_escrito()
    {
        var setas = View("_SetasDaOrdem.cshtml");

        Assert.Contains("dropdown-item", setas);
        Assert.Contains("Subir uma linha", setas);
        Assert.Contains("Descer uma linha", setas);
    }
}
