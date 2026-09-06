using Xunit;

namespace Padelizou.Tests;

// 06/09/2026 — O BOTÃO "DESFAZER RODADAS" NA PÁGINA DO TORNEIO AMERICANO.
//
// ⚠️ POR QUE TESTE DE FONTE: a suíte não renderiza Razor. `DesfazerRodadasAmericanoTests`
// trava a REGRA (o `ViewBag.PodeDesfazerRodadasAmericano` e a ação `DesfazerRodadasAmericano`
// em si) com testes de comportamento de verdade. O que só existe na VIEW — o botão, o texto
// de confirmação, o gate — não tem como um teste de comportamento alcançar, e é isso que este
// arquivo trava.
public class DesfazerRodadasNaPaginaDoTorneioTests
{
    [Fact]
    public void O_botao_existe_e_e_gateado_pelo_viewbag_certo()
    {
        var bloco = BlocoDoBotao();

        Assert.Contains("asp-action=\"DesfazerRodadasAmericano\"", bloco);
        Assert.Contains("@if (ViewBag.PodeDesfazerRodadasAmericano == true)", bloco);
    }

    // Ação sem volta (apaga partida) — mesmo tom e mesmo mecanismo de confirmação do
    // DesfazerSorteio (o par da mesma ideia no formato Padrão): sem isto um clique errado
    // apagaria rodadas sorteadas sem perguntar nada.
    [Fact]
    public void O_botao_pede_confirmacao_antes_de_desfazer()
    {
        var bloco = BlocoDoBotao();

        Assert.Contains("data-confirmar-titulo=\"Desfazer as rodadas?\"", bloco);
        Assert.Contains("data-confirmar-tom=\"perigo\"", bloco);
        Assert.Contains("data-confirmar-ok=\"Desfazer\"", bloco);
    }

    // O método é POST — GET nesta rota apagaria dados de quem só clicou num link por engano
    // ou seguiu um link salvo (Regra 0 do CLAUDE.md: gravação de dado é sempre POST).
    [Fact]
    public void O_botao_manda_por_post()
    {
        var bloco = BlocoDoBotao();

        Assert.Contains("method=\"post\"", bloco);
    }

    private static string BlocoDoBotao()
    {
        var fonte = Details();

        var inicio = fonte.IndexOf("Desfazer as rodadas do Americano", StringComparison.Ordinal);
        Assert.True(inicio >= 0,
            "Não achei o bloco do botão \"Desfazer Rodadas\" na página do torneio (comentário "
            + "\"Desfazer as rodadas do Americano\"). Ele foi renomeado ou removido, e esta "
            + "trava parou de olhar pra ele.");

        var fim = fonte.IndexOf("Desfazer Rodadas", inicio, StringComparison.Ordinal);
        Assert.True(fim > inicio, "Não achei o texto do botão (\"Desfazer Rodadas\") depois do comentário.");

        return fonte[inicio..(fim + "Desfazer Rodadas".Length)];
    }

    private static string Details() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

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
