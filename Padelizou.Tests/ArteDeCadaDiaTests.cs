using Padelizou.Services;

namespace Padelizou.Tests;

// 11/11/2026 — "AQUI TÁ GERANDO A MESMA IMAGEM DE DIAS DIFERENTES".
//
// 🗣️ Felipe, com o print do Compartilhar os jogos do THE LAST DANCE: a arte de "sex 13/11"
// mostrava os jogos de "QUI 12/11".
//
// 🕳️ A tela pedia a imagem com `parte = arte.Parte` — a parte DENTRO do dia (1 em todo dia que
// cabe numa arte só) —, e o endpoint lê `parte` como a POSIÇÃO na lista inteira de artes. Todo
// dia pedia "a arte nº 1". O número do link agora sai de um lugar só, junto com a leitura dele.
public class ArteDeCadaDiaTests
{
    private static JogoDaLista Jogo(DateTime quando) =>
        new(quando, "4ª Masculina", "Grupo A", null, "A / B", "C / D", "A/B", "C/D", false);

    private static List<ArteDeJogos> TresDias() => CartaoDosJogos.Dividir(new[]
    {
        Jogo(new DateTime(2026, 11, 12, 18, 0, 0)),
        Jogo(new DateTime(2026, 11, 13, 18, 0, 0)),
        Jogo(new DateTime(2026, 11, 14, 9, 0, 0)),
    });

    [Fact]
    public void Cada_dia_tem_o_seu_numero_no_link()
    {
        var artes = TresDias();
        var numeros = artes.Select(a => CartaoDosJogos.NumeroNoLink(artes, a)).ToArray();
        Assert.Equal(new[] { 1, 2, 3 }, numeros);
    }

    [Fact]
    public void O_numero_do_link_volta_pra_mesma_arte()
    {
        var artes = TresDias();
        foreach (var arte in artes)
            Assert.Same(arte, CartaoDosJogos.DoLink(artes, CartaoDosJogos.NumeroNoLink(artes, arte)));
        Assert.Null(CartaoDosJogos.DoLink(artes, 4));
        Assert.Null(CartaoDosJogos.DoLink(artes, 0));
    }

    [Fact]
    public void A_tela_monta_o_link_pelo_numero_da_lista_e_nao_pela_parte_do_dia()
    {
        var tela = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "Padelizou", "Views", "Torneios", "CompartilharJogos.cshtml"));
        Assert.DoesNotContain("[\"parte\"] = arte.Parte", tela);
        Assert.Contains("CartaoDosJogos.NumeroNoLink(", tela);
    }

    // ── "Alterei um horário e não alterou aqui na tela" (mesmo dia, mesmo print) ──────────
    // A imagem vai com `Cache-Control: public, max-age=3600` (EntregaDeCard) e a URL não mudava
    // quando a grade mudava: o navegador (e quem mais guardasse) seguia servindo a arte velha
    // por até uma hora. A versão do CONTEÚDO entra no link — mudou um jogo, muda a URL.

    private static GradeDesenhavel Grade(params JogoDaLista[] jogos) =>
        new("THE LAST DANCE", null, "Chakra Padel", CartaoDosJogos.Dividir(jogos));

    [Fact]
    public void Horario_mudado_muda_a_versao_da_arte()
    {
        var antes = Grade(Jogo(new DateTime(2026, 11, 12, 18, 0, 0)));
        var depois = Grade(Jogo(new DateTime(2026, 11, 12, 18, 50, 0)));

        Assert.NotEqual(CartaoDosJogos.Versao(antes, antes.Artes[0]), CartaoDosJogos.Versao(depois, depois.Artes[0]));
        Assert.NotEqual(CartaoDosJogos.Versao(antes, null), CartaoDosJogos.Versao(depois, null));
    }

    [Fact]
    public void Nada_mudou_a_versao_e_a_mesma()
    {
        var a = Grade(Jogo(new DateTime(2026, 11, 12, 18, 0, 0)));
        var b = Grade(Jogo(new DateTime(2026, 11, 12, 18, 0, 0)));
        Assert.Equal(CartaoDosJogos.Versao(a, a.Artes[0]), CartaoDosJogos.Versao(b, b.Artes[0]));
    }

    [Fact]
    public void Os_links_da_tela_levam_a_versao()
    {
        var tela = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
            "Padelizou", "Views", "Torneios", "CompartilharJogos.cshtml"));
        // A arte de cada dia e a imagem única.
        Assert.True(System.Text.RegularExpressions.Regex.Matches(tela, @"\[""v""\] = Padelizou\.Services\.CartaoDosJogos\.Versao\(").Count >= 2);
    }
}
