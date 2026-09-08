using System.IO;
using Xunit;

namespace Padelizou.Tests;

// 08/09/2026 — A ABA VIRA "PAGAMENTOS E IMPEDIMENTOS", E GANHA UMA SUB-ABA.
//
// 🗣️ Felipe: "cria uma outra aba dentro dessa de pagamentos (mude o nome para Pagamentos e
// impedimentos) - colocar por categoria, se vai ter jogos de eliminatórias no sabado a noite
// ainda ou não", e "permita também criar uma opção, lá nos impedimentos, de 'colocar os 2
// jogos na sexta' [...] apenas para os organizadores e adm do sistema".
//
// ⚠️ POR QUE TESTE DE FONTE: a suíte não renderiza Razor (mesmo motivo escrito em
// AbaPagamentosNaPaginaDoTorneioTests). O COMPORTAMENTO das duas coisas tem trava de verdade
// em ConcentracaoPeloOrganizadorTests e EliminatoriaDaCategoriaPeloOrganizadorTests; o que só
// existe na VIEW — o nome da aba, a sub-aba, as três opções novas no select — é o que este
// arquivo cobre.
public class AbaPagamentosEImpedimentosTests
{
    [Fact]
    public void A_aba_se_chama_Pagamentos_e_impedimentos()
    {
        var fonte = Details();

        var botao = fonte.IndexOf("id=\"pagamentos-tab\"", StringComparison.Ordinal);
        Assert.True(botao >= 0, "Não achei o botão da aba.");

        // O texto do botão vem logo depois do ícone, dentro do mesmo <button>.
        var fim = fonte.IndexOf("</button>", botao, StringComparison.Ordinal);
        Assert.Contains("Pagamentos e impedimentos", fonte[botao..fim]);
    }

    [Fact]
    public void As_duas_sub_abas_existem_dentro_do_painel()
    {
        var fonte = Details();

        Assert.Contains("id=\"pagamentos-lista-tab\"", fonte);
        Assert.Contains("id=\"pagamentos-eliminatorias-tab\"", fonte);
        Assert.Contains("id=\"pagamentosEliminatorias\"", fonte);
    }

    // ⚠️ Ancorado no bloco da aba: a sub-aba de eliminatórias precisa estar DENTRO do painel
    // `id="pagamentos"`, não solta em qualquer canto da página. Sem esse recorte, o teste
    // passaria com a sub-aba pendurada na aba errada.
    [Fact]
    public void A_sub_aba_de_eliminatorias_mora_dentro_do_painel_de_pagamentos()
    {
        var fonte = Details();

        var painel = fonte.IndexOf("<div class=\"tab-pane fade\" id=\"pagamentos\"", StringComparison.Ordinal);
        Assert.True(painel >= 0, "Não achei o painel da aba Pagamentos.");

        var subAba = fonte.IndexOf("id=\"pagamentos-eliminatorias-tab\"", painel, StringComparison.Ordinal);
        Assert.True(subAba > painel, "A sub-aba de eliminatórias precisa estar dentro do painel de Pagamentos.");
    }

    [Fact]
    public void A_sub_aba_de_eliminatorias_posta_pra_acao_do_organizador()
    {
        var fonte = Details();

        Assert.Contains("asp-action=\"AlterarEliminatoriaNoSabado\"", fonte);
    }

    // As três opções novas do Felipe, no MESMO select do impedimento — é uma escolha só, e um
    // segundo dropdown convidaria a marcar impedimento e concentração ao mesmo tempo.
    [Theory]
    [InlineData("SoSextaNoite")]
    [InlineData("SoSabadoManha")]
    [InlineData("SoSabadoTarde")]
    public void O_select_do_impedimento_oferece_a_concentracao(string turno)
    {
        var fonte = Details();

        Assert.Contains($"TurnoDoImpedimento.{turno}", fonte);
    }

    // ⚠️ As opções novas NÃO podem aparecer no formulário do JOGADOR (o de `AlterarImpedimento`,
    // lá em cima na página) — o Felipe pediu "apenas para os organizadores e adm do sistema".
    // O servidor recusa de verdade (ConcentracaoPeloOrganizadorTests); isto é a tela não
    // oferecer o que ele não vai aceitar.
    [Fact]
    public void O_formulario_do_jogador_nao_oferece_a_concentracao()
    {
        var fonte = Details();

        var doJogador = fonte.IndexOf("asp-action=\"AlterarImpedimento\"", StringComparison.Ordinal);
        Assert.True(doJogador >= 0, "Não achei o formulário de impedimento do jogador.");

        var fimDoFormulario = fonte.IndexOf("</form>", doJogador, StringComparison.Ordinal);
        Assert.DoesNotContain("TurnoDoImpedimento.So", fonte[doJogador..fimDoFormulario]);
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
        throw new DirectoryNotFoundException("Não achei a pasta do projeto Padelizou.");
    }
}
