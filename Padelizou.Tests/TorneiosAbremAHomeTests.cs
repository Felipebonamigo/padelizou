using Xunit;

namespace Padelizou.Tests;

// 02/09/2026 — OS DOIS PRÓXIMOS TORNEIOS ABREM A HOME DO VISITANTE, pedido do Felipe num print
// da primeira tela: "logo que abre, a primeira coisa a aparecer tem q ser os 2 proximos
// torneios para as pessoas se inscreverem".
//
// Antes, quem chegava no site via primeiro o hero, depois "O que dá pra fazer aqui" (seis
// cards de navegação) e só ENTÃO "Inscrições abertas" — a única seção da página em que dá pra
// gastar dinheiro. A conversão estava atrás do mapa.
//
// ⚠️ SÓ NA TELA DE VISITANTE, de propósito. Na versão logada o topo é a agenda da pessoa
// (próximo jogo, compromissos, "Seus torneios") — empurrar isso pra baixo por uma vitrine
// trocaria informação pessoal e datada por publicidade. Quem já entrou tem o que fazer; quem
// acabou de chegar, não.
//
// ⚠️ POR QUE TESTE DE FONTE: a ORDEM das seções numa view não é observável por teste de
// comportamento — o `HomeVM` é o mesmo objeto independente de onde o Razor imprime cada bloco,
// e a suíte não renderiza Razor. Um `Take(2)` trocado por `Take(6)`, ou a seção voltando pro
// fim da página, passariam verdes na suíte inteira. A alternativa era não travar nada.
//
// O que os dois próximos SÃO já tem trava de comportamento em
// OrdemDosTorneiosTests.Inscricoes_abertas_vem_do_mais_proximo_pro_mais_distante: `Abertos`
// chega ordenado do mais próximo pro mais distante, então "os 2 primeiros" são os 2 próximos.
public class TorneiosAbremAHomeTests
{
    [Fact]
    public void A_vitrine_de_torneios_vem_ANTES_do_mapa_da_plataforma()
    {
        // O coração do pedido. Se alguém mover o bloco de volta pra baixo, o visitante volta a
        // ver seis cards de navegação antes do primeiro torneio.
        var fonte = Home();

        var torneios = fonte.IndexOf(MarcaDaVitrineDoVisitante, StringComparison.Ordinal);
        Assert.True(torneios >= 0,
            $"Não achei a vitrine de torneios do visitante (marca: \"{MarcaDaVitrineDoVisitante}\") na Home. "
            + "Ela foi renomeada ou removida, e esta trava parou de olhar pra ela.");

        var mapa = fonte.IndexOf("O que dá pra fazer aqui", StringComparison.Ordinal);
        Assert.True(mapa >= 0, "Não achei a seção \"O que dá pra fazer aqui\" na Home.");

        Assert.True(torneios < mapa,
            "A vitrine de torneios do visitante precisa vir ANTES de \"O que dá pra fazer aqui\" — "
            + "é o pedido inteiro: quem chega vê torneio antes de menu.");
    }

    [Fact]
    public void A_vitrine_do_visitante_mostra_DOIS_torneios()
    {
        // "os 2 proximos", não os seis. Dois cabem lado a lado no celular sem virar rolagem.
        var bloco = BlocoDaVitrineDoVisitante();

        Assert.Contains("Take(2)", bloco);
    }

    [Fact]
    public void A_vitrine_do_visitante_leva_pro_resto_dos_torneios()
    {
        // Mostrar 2 de 6 sem saída esconderia os outros quatro — o "Ver todos" é o que faz o
        // corte ser destaque em vez de amputação.
        var bloco = BlocoDaVitrineDoVisitante();

        Assert.Contains("asp-controller=\"Torneios\"", bloco);
        Assert.Contains("asp-action=\"Index\"", bloco);
    }

    [Fact]
    public void A_secao_de_baixo_nao_repete_os_torneios_pro_visitante()
    {
        // A vitrine antiga continua existindo pro LOGADO, no lugar de sempre. Sem o gate de
        // `logado`, o visitante veria os mesmos torneios duas vezes na mesma página.
        var fonte = Home();

        // O gate da vitrine de baixo exige `logado`. Sem ele a condição seria só
        // `Model.Abertos.Any()`, e o visitante veria os mesmos torneios duas vezes.
        Assert.Contains("@if (logado && Model.Abertos.Any())", fonte);

        // E a de cima é a imagem espelhada: só pra quem NÃO está logado.
        Assert.Contains("@if (!logado && Model.Abertos.Any())", fonte);
    }

    // A marca do bloco novo. É um comentário Razor plantado de propósito: o texto visível
    // ("Inscrições abertas") aparece nas DUAS vitrines, então buscar por ele não distinguiria
    // uma da outra — e o teste de ordem compararia a posição errada. Foi exatamente esse tipo
    // de âncora genérica que já custou uma rodada nas travas de fonte deste projeto.
    private const string MarcaDaVitrineDoVisitante = "VITRINE DO VISITANTE";

    private static string BlocoDaVitrineDoVisitante()
    {
        var fonte = Home();

        var inicio = fonte.IndexOf(MarcaDaVitrineDoVisitante, StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Não achei a vitrine de torneios do visitante na Home.");

        // Até o fim do bloco: a seção seguinte é o mapa da plataforma.
        var fim = fonte.IndexOf("O mapa da plataforma", inicio, StringComparison.Ordinal);
        Assert.True(fim > inicio, "Não achei o fim da vitrine do visitante (o mapa da plataforma vem depois dela).");

        return fonte[inicio..fim];
    }

    private static string Home() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Home", "Index.cshtml"));

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
