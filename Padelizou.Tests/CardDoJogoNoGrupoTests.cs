using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// 11/09/2026 — O CARD DO JOGO, DENTRO DO CARD DO GRUPO, GANHOU DESTINO E ESTADO.
//
// 🗣️ Felipe, com o print do Grupo C: *"permita clicar no aovivo e ir para a pagina do aovivo
// aonde esta o jogo"* · *"e o que estiver finalizado deixe um circulo parecido com o do
// aovivo, só que outra cor q mostre q foi finalizado, direfernte do aovivo e do aguardando
// ainda"*.
//
// Duas coisas, e as duas na MESMA linha do card:
//
// 1️⃣ O jogo em quadra vira LINK pro card grande da aba Ao Vivo — que mora na mesma página,
//    noutra aba. Por isso a hash (`#jogo-123`) e não uma URL nova: nada recarrega e o
//    <iframe> da transmissão não reinicia (o motivo de jogos-ao-vivo-atualiza.js existir).
//
// 2️⃣ O jogo FINALIZADO ganha o selo dele. Antes eram dois estados na tela — bolinha vermelha
//    ou uma data —, e a data é a MESMA do jogo que já acabou e do que ainda não começou:
//    quem batia o olho no grupo não sabia se aquele "sex 11/09 19:40" era história ou agenda.
//
// ⚠️ A suíte não renderiza Razor: guarda de ARQUIVO, como em EscudoDoTimeNosJogosTests.
public class CardDoJogoNoGrupoTests
{
    // ── 1. O JOGO EM QUADRA VIRA LINK ──────────────────────────────────────────────────────

    [Fact]
    public void O_card_do_jogo_do_grupo_e_um_link_pra_hash_do_jogo()
    {
        var abertura = AberturaDoCardDoJogo();

        Assert.StartsWith("<a ", abertura);
        Assert.Contains("#jogo-", abertura);
    }

    // SÓ o jogo em quadra: um `href` fixo faria o card do jogo de amanhã prometer um destino
    // que não existe — a âncora `#jogo-123` só nasce no card AO VIVO, e clicar num link que
    // não leva a lugar nenhum é pior que um card que não é link.
    [Fact]
    public void So_o_jogo_em_quadra_ganha_o_href()
    {
        var abertura = AberturaDoCardDoJogo();

        var href = Regex.Match(abertura, @"href=""@\(([^)]*)\)""");
        Assert.True(href.Success, "o href do card do grupo precisa ser condicional");
        Assert.Contains("noAr", href.Groups[1].Value);
    }

    [Fact]
    public void O_card_ao_vivo_carrega_a_ancora_que_o_link_procura()
    {
        var jogos = LerDaWeb("Views", "Torneios", "_JogosDoTorneio.cshtml");

        // A âncora vive no CARD, e não num <span> qualquer: é o card inteiro que precisa
        // entrar na tela quando o navegador rolar até ele.
        var card = Regex.Match(jogos, @"<div class=""pdz-live-card[^""]*""[^>]*>");
        Assert.True(card.Success, "não achei a abertura do .pdz-live-card");
        Assert.Contains(@"id=""jogo-@jogo.Id""", card.Value);
    }

    // O `scrollIntoView` sozinho não rola nada quando o alvo está dentro de aba escondida
    // (`display: none` não tem posição). As duas abas primeiro, o rolar depois.
    [Fact]
    public void A_hash_do_jogo_abre_as_duas_abas_e_so_entao_rola()
    {
        var roteiro = FuncaoDaHashDoJogo();

        int mae = roteiro.IndexOf("#jogosDoTorneio", StringComparison.Ordinal);
        int sub = roteiro.IndexOf("#aovivo", StringComparison.Ordinal);
        int rola = roteiro.IndexOf("scrollIntoView", StringComparison.Ordinal);

        Assert.True(mae >= 0, "a aba mãe (Jogos) precisa ser aberta");
        Assert.True(sub > mae, "a sub-aba Ao Vivo precisa ser aberta depois da aba mãe");
        Assert.True(rola > sub, "rolar até o card só faz sentido depois de as duas abas abrirem");
    }

    // Clicar numa hash com a página JÁ aberta não dispara carregamento nenhum: sem
    // `hashchange` o primeiro clique funcionaria e o segundo (voltar e clicar de novo, ou
    // clicar noutro jogo) não faria nada.
    [Fact]
    public void A_hash_do_jogo_tambem_vale_com_a_pagina_ja_aberta()
    {
        Assert.Contains("hashchange", BlocoDaHashDoJogo());
    }

    // ── 2. O SELO DO JOGO FINALIZADO ───────────────────────────────────────────────────────

    [Fact]
    public void O_jogo_finalizado_do_grupo_mostra_o_selo_dele()
    {
        var linha = LinhaDoQuandoDoCardDoJogo();

        Assert.Contains("pdz-chave-encerrado", linha);
        // Pelo STATUS da partida, e não pelo placar preenchido: um jogo em quadra também tem
        // games, e carimbar "encerrado" nele seria mentira na tela.
        Assert.Contains("acabou", linha);
    }

    [Fact]
    public void O_selo_do_finalizado_tem_cor_propria_diferente_do_ao_vivo()
    {
        var vivo = CorDe(".pdz-chave-aovivo");
        var fim = CorDe(".pdz-chave-encerrado");

        Assert.NotEqual(vivo, fim);
        // E nem a cor de "ainda vai acontecer", que é o texto apagado da linha do quando.
        Assert.NotEqual("var(--pdz-muted)", fim);
    }

    // ── Ajudantes ──────────────────────────────────────────────────────────────────────────

    // A abertura do elemento de UM jogo dentro do card do grupo (`.pdz-grupo-jogo`), e não a
    // da lista que embrulha todos (`.pdz-grupo-jogos`) — daí o espaço depois da classe.
    private static string AberturaDoCardDoJogo()
    {
        var view = LerDaWeb("Views", "Torneios", "Details.cshtml");
        var abertura = Regex.Match(view, @"<\w+ class=""pdz-grupo-jogo [^""]*""[^>]*>");

        Assert.True(abertura.Success, "não achei a abertura do card do jogo do grupo no Details");
        return abertura.Value;
    }

    private static string LinhaDoQuandoDoCardDoJogo()
    {
        var view = LerDaWeb("Views", "Torneios", "Details.cshtml");
        int comeco = view.IndexOf(@"<div class=""pdz-grupo-jogo-quando"">", StringComparison.Ordinal);
        Assert.True(comeco >= 0, "não achei a linha do 'quando' no card do jogo do grupo");

        int fim = view.IndexOf("pdz-grupo-jogo-lado", comeco, StringComparison.Ordinal);
        Assert.True(fim > comeco, "não achei o fim da linha do 'quando'");
        return view[comeco..fim];
    }

    // A ORDEM dos três passos é do corpo da função; QUEM a chama é o bloco inteiro. Dois
    // recortes, e não um: um comentário no bloco pode citar as abas em qualquer ordem sem que
    // isso diga nada sobre o que o navegador executa.
    private static string FuncaoDaHashDoJogo()
    {
        var funcao = Regex.Match(BlocoDaHashDoJogo(), @"function pdzIrProJogoDaHash.*?\n    \}",
                                 RegexOptions.Singleline);

        Assert.True(funcao.Success, "não achei a função pdzIrProJogoDaHash");
        return funcao.Value;
    }

    private static string BlocoDaHashDoJogo()
    {
        var view = LerDaWeb("Views", "Torneios", "Details.cshtml");
        var bloco = Regex.Match(view, @"<script>(?:(?!</script>).)*?pdzIrProJogoDaHash.*?</script>",
                                RegexOptions.Singleline);

        Assert.True(bloco.Success, "não achei o <script> do pdzIrProJogoDaHash no Details");
        return bloco.Value;
    }

    // ⚠️ SELETOR ANCORADO (lição de 11/09/2026): sem o `^` o regex de `.pdz-chave-aovivo`
    // casa também com qualquer regra que termine nele (`.x .pdz-chave-aovivo`), e o teste
    // passa a medir a cor errada.
    private static string CorDe(string seletor)
    {
        var css = LerDaWeb("wwwroot", "css", "site.css");
        var regra = Regex.Match(css, @"^" + Regex.Escape(seletor) + @"\s*\{([^}]*)\}",
                                RegexOptions.Singleline | RegexOptions.Multiline);

        Assert.True(regra.Success, $"não achei a regra {seletor} no site.css");

        var cor = Regex.Match(regra.Groups[1].Value, @"color:\s*([^;]+);");
        Assert.True(cor.Success, $"a regra {seletor} precisa declarar uma cor");
        return cor.Groups[1].Value.Trim();
    }

    private static string LerDaWeb(params string[] caminho)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
                return File.ReadAllText(Path.Combine(dir.FullName, "Padelizou", Path.Combine(caminho)));
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto web a partir de " + AppContext.BaseDirectory);
    }
}
