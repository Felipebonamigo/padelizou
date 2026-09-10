using System.Globalization;
using System.Text.RegularExpressions;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — A FAIXA DE BETA OCUPAVA TRÊS LINHAS NO CELULAR.
//
// 🗣️ Deivid Santos, do 2ª Etapa ER PADEL TOUR: *"Diminui um pouco a aba dos bugs"* …
// *"Ta muito longo"*. No print dele a faixa come ~170px antes do nome do torneio: aviso de
// beta e link de feedback empurrando pra baixo justamente o que a pessoa abriu a página pra ver.
//
// A faixa continua: ela é o canal de feedback do site inteiro, e é dela que vêm pedidos como
// este. O que muda é o tamanho — texto curto o bastante pra não virar parágrafo.
public class FaixaDeBetaEnxutaTests
{
    [Fact]
    public void O_aviso_padrao_cabe_em_pouca_tela()
    {
        var padrao = new BetaSettings();

        // 55 caracteres é o que cabe numa linha de celular estreito (~360px) no tamanho da
        // faixa. Acima disso ela vira duas linhas só de aviso, mais uma do link.
        Assert.True(padrao.Texto.Length <= 55,
            $"O aviso de beta tem {padrao.Texto.Length} caracteres: \"{padrao.Texto}\".");
        Assert.False(string.IsNullOrWhiteSpace(padrao.Texto));
    }

    [Fact]
    public void O_canal_de_feedback_continua_na_faixa()
    {
        // Encurtar não pode custar o link: ele é a única porta de reclamação em toda tela do
        // site, e some sem avisar.
        var layout = File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "Views", "Shared", "_Layout.cshtml"));

        Assert.Contains("pdz-link-beta", layout);
        Assert.Contains("asp-action=\"ReportarProblema\"", layout);
    }

    [Fact]
    public void Quem_manda_procurar_o_link_chama_ele_pelo_nome_que_esta_na_tela()
    {
        // 🕳️ O rótulo encurtou na faixa, mas a tela de erro continuava mandando procurar
        // "Sugestão, bug ou crítica" — um nome que não existe mais em lugar nenhum da tela.
        // Quem cai no Ops com um código de referência na mão fica procurando o que não há.
        var layout = File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "Views", "Shared", "_Layout.cshtml"));
        var erro = File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "Views", "Shared", "Error.cshtml"));

        var rotulo = Regex.Match(layout, @"pdz-link-beta""[^>]*>\s*([^<]+?)\s*<i ", RegexOptions.Singleline);
        Assert.True(rotulo.Success, "Não achei o texto do link da faixa de beta.");

        Assert.Contains($"\"{rotulo.Groups[1].Value}\"", erro);
    }

    [Fact]
    public void A_faixa_e_baixa()
    {
        var css = File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "wwwroot", "css", "site.css"));
        var bloco = Regex.Match(css, @"\.pdz-faixa-beta\s*\{([^}]*)\}", RegexOptions.Singleline);
        Assert.True(bloco.Success, "Não achei `.pdz-faixa-beta` no site.css.");

        var padding = Regex.Match(bloco.Groups[1].Value, @"padding:\s*(\d+)px");
        Assert.True(padding.Success, "A faixa de beta precisa dizer o próprio padding vertical.");
        Assert.True(int.Parse(padding.Groups[1].Value, CultureInfo.InvariantCulture) <= 5,
            "O respiro da faixa de beta soma em toda tela do site — 5px em cima e embaixo bastam.");
    }

    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "wwwroot", "css", "site.css")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return dir!.FullName;
    }
}
