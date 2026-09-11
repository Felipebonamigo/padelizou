using Xunit;

namespace Padelizou.Tests;

// 11/09/2026 — O MODAL NO APP INSTALADO, e o X que não dava pra tocar. 🗣️ Felipe, num print do
// iPhone com o modal aberto: *"bug, o X de fechar, fica em cima da bateria e nao conseguimos
// fechar. E permita minimizar pela 'dupla' apostada tambem"*.
//
// 🕳️ O `_Layout` pede `viewport-fit=cover` — instalado, o app usa a tela INTEIRA, inclusive
// atrás do relógio e da bateria. O `site.css` já paga essa conta pro corpo da página
// (`env(safe-area-inset-*)`), mas o `.modal` é `position: fixed` e NÃO herda nada disso: com a
// lista longa o diálogo encosta no topo do aparelho e o botão de fechar vai parar debaixo da
// barra de status, onde o toque não chega.
public class ModalDeVotantesNoAppInstaladoTests
{
    [Fact]
    public void O_modal_respeita_a_AREA_SEGURA_do_aparelho()
    {
        var css = Ler("wwwroot", "css", "site.css");

        var regra = css.IndexOf(".modal {", StringComparison.Ordinal);
        Assert.True(regra >= 0, "Não achei a regra do `.modal` no site.css.");

        var bloco = css[regra..(css.IndexOf('}', regra) + 1)];
        Assert.Contains("safe-area-inset-top", bloco);

        // ⚠️ O RODAPÉ TAMBÉM: no iPhone sem botão, a barra de gestos come a última linha da
        // lista — e é justamente onde ficam os que não palpitaram placar.
        Assert.Contains("safe-area-inset-bottom", bloco);
    }

    [Fact]
    public void Cada_DUPLA_do_modal_recolhe_sozinha()
    {
        // 🗣️ *"permita minimizar pela 'dupla' apostada tambem"*: com 15 nomes de um lado, ver a
        // outra dupla exige rolar a lista inteira. O cabeçalho de cada lado vira o botão que
        // dobra aquele lado.
        var modal = Ler("Views", "Torneios", "_ModalVerVotos.cshtml");

        Assert.Contains("alternarVotantes(", modal);

        // ⚠️ É um `<button>`, e não um `<p>` com onclick: recolher é ação, e ação sem botão não
        // chega pra quem navega por teclado nem pro leitor de tela.
        Assert.Contains("<button", modal);

        var js = Ler("wwwroot", "js", "palpitrometro.js");
        Assert.Contains("function alternarVotantes", js);

        // A seta que diz se aquele lado está aberto ou fechado — sem ela o cabeçalho é um botão
        // que não parece botão, e ninguém descobre que dá pra dobrar. Ela mora na VIEW (o ícone)
        // e no CSS (a rotação), não no JS: quem guarda o estado é o `aria-expanded`, e é o
        // seletor de atributo que vira a seta — um segundo estado no JS começaria a divergir.
        Assert.Contains("pdz-votantes-seta", modal);

        var css = Ler("wwwroot", "css", "site.css");
        Assert.Contains("aria-expanded=\"false\"] .pdz-votantes-seta", css);
    }

    private static string Ler(params string[] caminho) =>
        File.ReadAllText(Path.Combine(new[] { PastaDoProjeto() }.Concat(caminho).ToArray()));

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
