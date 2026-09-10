using System.IO;
using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — O "RECALCULAR HORÁRIOS" SAIU DA LISTA DE JOGOS E FOI PRO PAINEL DE CONTROLE.
//
// 🗣️ Felipe, num print da aba Partidas do 2ª Etapa ER PADEL TOUR, com o botão vermelho logo
// acima da lista de 97 jogos: *"mude esse botao recalcular horarios, para o lado desse do
// 'recolher as chaves' se nao alguem pode clicar sem querer ali"*.
//
// 🕳️ Ele morava na tela onde se PASSA O DIA — a lista que se rola pra achar quem joga agora e
// pra apertar o play. Botão que apaga uma noite de trocas na mão não fica no caminho do polegar
// de quem está operando o torneio. O painel do organizador é onde já moram as outras ações que
// mexem no torneio inteiro (Desfazer o sorteio, Recolher as chaves).
//
// ⚠️ QUEM SÓ MARCA PLACAR PERDE O BOTÃO, e isso é a consequência pedida, não um efeito colateral
// esquecido: o painel é de quem organiza (ViewBag.PodeGerenciar). A mesa continua com o "Ajustar
// horários" — o irmão manso, que não joga nada fora — e com o "Conferir a grade". O SERVIDOR não
// mudou: RefazerGrade ainda aceita marcador (PodeOperarODiaDeJogoAsync); o que saiu é a porta.
//
// Teste de FONTE porque a suíte não renderiza Razor — o mesmo recurso dos outros testes de tela.
public class RecalcularHorariosNoPainelTests
{
    private static string View(string nome)
    {
        var wwwroot = Path.GetDirectoryName(TestInfra.PastaDasFontesDeVerdade())!;
        return File.ReadAllText(Path.Combine(Path.GetDirectoryName(wwwroot)!, "Views", "Torneios", nome));
    }

    [Fact]
    public void O_recalcular_horarios_nao_fica_mais_na_lista_de_jogos()
    {
        Assert.DoesNotContain("asp-action=\"RefazerGrade\"", View("_JogosDoTorneio.cshtml"));
    }

    // "para o lado desse do 'recolher as chaves'": dentro do card de status do Painel de
    // Controle, depois do Recolher e antes do bloco da enquete — que é onde aquele card acaba.
    [Fact]
    public void O_recalcular_horarios_mora_no_painel_de_controle_junto_do_recolher_as_chaves()
    {
        var fonte = View("Details.cshtml");

        int painel = fonte.IndexOf("Painel de Controle", System.StringComparison.Ordinal);
        int recolher = fonte.IndexOf("asp-action=\"RecolherChaves\"", System.StringComparison.Ordinal);
        int recalcular = fonte.IndexOf("asp-action=\"RefazerGrade\"", System.StringComparison.Ordinal);
        int fimDoCard = fonte.IndexOf("COMO O TORNEIO FOI AVALIADO", System.StringComparison.Ordinal);

        Assert.True(recalcular > 0, "O <form asp-action=\"RefazerGrade\"> não está em Details.cshtml.");
        Assert.True(recalcular > painel, "O recalcular ficou ANTES do Painel de Controle.");
        Assert.True(recalcular > recolher, "O recalcular ficou antes do Recolher as Chaves.");
        Assert.True(recalcular < fimDoCard, "O recalcular caiu fora do card de status do painel.");
    }

    // Depois de recalcular, a tela que interessa é a dos horários novos — e não o painel de onde
    // se clicou. O `voltarPara` é o que leva pra lá (TorneiosController.VoltarPara: Details com
    // âncora na aba de jogos).
    [Fact]
    public void Depois_de_recalcular_a_tela_volta_pra_lista_de_jogos()
    {
        var fonte = View("Details.cshtml");
        var formulario = fonte[fonte.IndexOf("asp-action=\"RefazerGrade\"", System.StringComparison.Ordinal)..];
        formulario = formulario[..formulario.IndexOf("</form>", System.StringComparison.Ordinal)];

        Assert.Contains("name=\"voltarPara\" value=\"Details\"", formulario);
    }

    // O que FICA com quem opera o dia: os dois botões que não jogam trabalho fora.
    [Fact]
    public void A_mesa_continua_com_o_ajustar_horarios_e_o_conferir_a_grade()
    {
        var fonte = View("_JogosDoTorneio.cshtml");

        Assert.Contains("asp-action=\"AjustarHorarios\"", fonte);
        Assert.Contains("asp-action=\"ConferirGrade\"", fonte);
    }
}
