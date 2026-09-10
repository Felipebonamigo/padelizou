using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — A BOLINHA VERMELHA PULSANTE SÓ COM JOGO EM QUADRA.
//
// 🗣️ Felipe: *"deixaria piscando vermelho só quando houvesse jogo ao vivo"* — com o print da
// aba dizendo *"● Ao Vivo (0)"*, bolinha piscando ao lado de um contador zerado.
//
// O vermelho pulsante é o sinal de "tem jogo acontecendo AGORA". Aceso o tempo todo, ele
// deixa de ser sinal: quem varre a tela aprende a ignorá-lo, e no dia em que houver jogo de
// verdade ele não chama mais ninguém.
//
// ⚠️ POR QUE TESTE DE FONTE: a suíte não renderiza Razor, e a bolinha é markup puro.
public class PontoVermelhoSoComJogoAoVivoTests
{
    [Fact]
    public void A_bolinha_da_aba_Ao_Vivo_e_gateada_pela_contagem()
    {
        var fonte = ListaDeJogos();

        var bolinha = fonte.IndexOf("pdz-tab-dot", StringComparison.Ordinal);
        Assert.True(bolinha >= 0, "Não achei a bolinha do Ao Vivo (.pdz-tab-dot).");

        // O `@if` que a acende vem ANTES dela, na mesma aba. `aoVivoList` é a lista que o
        // contador da própria aba imprime — a mesma fonte, e não uma segunda contagem que
        // possa discordar do número entre parênteses.
        var gate = fonte.LastIndexOf("@if (aoVivoList.Count > 0)", bolinha, StringComparison.Ordinal);
        Assert.True(gate >= 0, "A bolinha vermelha precisa estar atrás de um `@if (aoVivoList.Count > 0)`.");

        // ⚠️ E o `@if` precisa FECHAR depois dela: entre o `{` do gate e a bolinha não pode haver
        // um `}` — senão o `if` estaria gateando outra coisa e a bolinha ficaria solta de novo.
        // A versão anterior media DISTÂNCIA em caracteres (< 400, quando a real é 65), que é um
        // número mágico: passava com o `if` fechado no meio, e quebraria num comentário a mais.
        var corpo = fonte[gate..bolinha];
        Assert.DoesNotContain("}", corpo);
    }

    private static string ListaDeJogos() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "_JogosDoTorneio.cshtml"));

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
