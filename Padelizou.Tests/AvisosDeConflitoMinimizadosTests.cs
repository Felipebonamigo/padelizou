using Xunit;

namespace Padelizou.Tests;

// OS AVISOS DE CONFLITO DA ABA JOGOS NASCEM FECHADOS (22/11/2026).
// 🗣️ Felipe, com o print da aba Jogos tomada pelos avisos: *"isso aqui polui muito a tela, coloque
// minimizado, para o organizador abrir se quiser"*. Fica à vista só a linha do título (com a
// contagem); a lista abre no toque — <details> do próprio HTML, sem JS.
public class AvisosDeConflitoMinimizadosTests
{
    private static string View(string nome) => TestInfra.SemComentarios(
        File.ReadAllText(Path.Combine(Raiz(), "Padelizou", "Views", "Torneios", nome)));

    [Theory]
    [InlineData("_JogosDoTorneio.cshtml", "conflitos de jogador em dois jogos ao mesmo tempo")]
    [InlineData("_EliminatoriasNoMesmoHorario.cshtml", "casos de jogador que pode cair em duas eliminatórias")]
    public void O_aviso_mostra_so_o_titulo_e_a_lista_abre_no_toque(string arquivo, string titulo)
    {
        var view = View(arquivo);
        var posTitulo = view.IndexOf(titulo, StringComparison.Ordinal);
        Assert.True(posTitulo > 0, "título do aviso não encontrado");

        // O título mora num <summary> de um <details>, e a lista vem depois dele, dentro do mesmo.
        var details = view.LastIndexOf("<details", posTitulo, StringComparison.Ordinal);
        var summary = view.LastIndexOf("<summary", posTitulo, StringComparison.Ordinal);
        Assert.True(details >= 0 && summary > details, "o título não está no <summary> de um <details>");
        var fim = view.IndexOf("</details>", posTitulo, StringComparison.Ordinal);
        var lista = view.IndexOf("<ul", posTitulo, StringComparison.Ordinal);
        Assert.True(lista > 0 && fim > lista, "a lista não está dentro do <details>");

        // Fechado por padrão: sem o atributo `open`.
        var abertura = view.Substring(details, view.IndexOf('>', details) - details);
        Assert.DoesNotContain("open", abertura);
    }

    private static string Raiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".github", "workflows"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Não achei a raiz do repositório.");
    }
}
