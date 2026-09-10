using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// AS AÇÕES DA PRÓPRIA INSCRIÇÃO, NA TELA DO TORNEIO.
//
// 💥 O DEFEITO QUE ESTE ARQUIVO TRAVA (achado em revisão adversarial, 09/09/2026): quando a
// janela de definir parceiro passou a valer até a dupla entrar em quadra, a cópia do
// ORGANIZADOR foi repontada pra `JanelaDoParceiro` e a cópia do PRÓPRIO JOGADOR ficou presa em
// `Model.Status == "Inscrições Abertas"`.
//
// O resultado era a promessa central da mudança sem caminho: o push que o encerramento das
// inscrições dispara diz *"Sua vaga está garantida... dá pra fechar pela página do torneio"*, o
// Paulo abre a página e não existe botão nenhum na inscrição dele — nem "Convidar por link",
// nem "Definir por CPF", nem "N querem jogar com você". O servidor aceitava; a tela escondia.
// É o mesmo defeito que a mudança veio consertar, virado do avesso.
//
// ⚠️ E O CONSERTO NÃO É TIRAR O STATUS DO `@if`: o MESMO bloco guarda o "Desistir" e a troca de
// impedimento, que continuam presos em "Inscrições Abertas" com razão — os dois mexem na grade
// já montada, e os controllers deles recusam ali. São duas janelas no mesmo bloco.
//
// Teste de FONTE porque é onde este defeito mora: a suíte não renderiza Razor.
public class AcoesDaMinhaInscricaoTests
{
    private static string Tela()
    {
        var caminho = Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml");
        return File.ReadAllText(caminho);
    }

    private static string PastaDoProjeto()
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(pasta, "Padelizou");
            if (Directory.Exists(Path.Combine(tentativa, "Views"))) return tentativa;
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new DirectoryNotFoundException("Não achei a pasta Padelizou/Views a partir do bin.");
    }

    [Fact]
    public void As_acoes_de_parceiro_da_minha_inscricao_leem_a_janela_e_nao_o_status()
    {
        var tela = Tela();

        // A régua da janela precisa estar sendo consultada pro card do próprio inscrito.
        Assert.Contains("podeDefinirNaMinha", tela);

        // E o portão antigo — que prendia TODAS as ações da própria inscrição em "Inscrições
        // Abertas", inclusive definir parceiro — não pode ter voltado.
        Assert.DoesNotContain(
            "meuIdNaPagina != null && Model.Status == \"Inscrições Abertas\"",
            tela);
    }

    [Fact]
    public void Desistir_e_impedimento_continuam_presos_em_inscricoes_abertas()
    {
        // A outra metade da mesma decisão: abrir a janela pro parceiro não pode ter deixado
        // vazar "Desistir" e "trocar impedimento" pra depois do sorteio. Os dois mexem na grade
        // já montada — e os controllers deles recusam ali de qualquer jeito, então a tela
        // estaria oferecendo o que o servidor nega.
        var tela = Tela();

        var desistir = tela.IndexOf("asp-action=\"Desistir\"", StringComparison.Ordinal);
        Assert.True(desistir > 0, "Não achei o formulário de Desistir na tela.");

        var cerca = tela.IndexOf("@if (inscricoesAbertas)", StringComparison.Ordinal);
        Assert.True(cerca > 0 && cerca < desistir,
            "O \"Desistir\" precisa estar dentro da cerca `@if (inscricoesAbertas)` — sem ela, "
            + "abrir a janela do parceiro deixaria desistir e impedimento vazarem pra depois do sorteio.");

        var impedimento = tela.IndexOf("asp-action=\"AlterarImpedimento\"", StringComparison.Ordinal);
        Assert.True(impedimento > cerca, "A troca de impedimento também precisa ficar dentro da cerca.");
    }
}
