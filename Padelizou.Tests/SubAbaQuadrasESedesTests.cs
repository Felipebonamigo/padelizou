using System.IO;
using Xunit;

namespace Padelizou.Tests;

// A SUB-ABA "QUADRAS E SEDES", dentro de "Pagamentos e impedimentos" (08/09/2026).
//
// 🗣️ Felipe: "temos que pensar nisso, e aonde colocar, por que isso só vai ser sabido quando
// formos gerar as chaves, talvez naquela aba 'pagamentos e impedimentos'".
//
// ⚠️ POR QUE TESTE DE FONTE: a suíte não renderiza Razor (mesmo motivo dos outros arquivos de
// aba). O COMPORTAMENTO tem trava de verdade em SedeExtraPeloOrganizadorTests.
public class SubAbaQuadrasESedesTests
{
    [Fact]
    public void A_sub_aba_existe_dentro_do_painel_de_pagamentos()
    {
        var fonte = Details();

        var painel = fonte.IndexOf("<div class=\"tab-pane fade\" id=\"pagamentos\"", StringComparison.Ordinal);
        Assert.True(painel >= 0, "Não achei o painel da aba Pagamentos.");

        var botao = fonte.IndexOf("id=\"pagamentos-quadras-tab\"", painel, StringComparison.Ordinal);
        Assert.True(botao > painel, "A sub-aba de quadras precisa estar dentro do painel de Pagamentos.");
        Assert.Contains("id=\"pagamentosQuadras\"", fonte);
    }

    [Theory]
    [InlineData("AlterarJanelaDaSede")]
    [InlineData("AlterarTransbordoDaCategoria")]
    [InlineData("AlterarEvitarDoisJogosNaSedeExtra")]
    public void As_tres_acoes_novas_tem_formulario(string acao)
    {
        Assert.Contains($"asp-action=\"{acao}\"", Details());
    }

    // ⚠️ A JANELA USA `datetime-local`, o campo NATIVO — mesma escada do CLAUDE.md que já
    // escolheu `<input type="date">` em vez de biblioteca de calendário.
    [Fact]
    public void A_janela_usa_o_campo_nativo_de_data_e_hora()
    {
        var fonte = Details();

        var form = fonte.IndexOf("asp-action=\"AlterarJanelaDaSede\"", StringComparison.Ordinal);
        Assert.True(form >= 0);

        var fim = fonte.IndexOf("</form>", form, StringComparison.Ordinal);
        Assert.Contains("type=\"datetime-local\"", fonte[form..fim]);
    }

    // "Quantos jogos cabem lá" é CONTA, não campo — decisão tomada com o Felipe. Se algum dia
    // virar `<input>`, este teste cai e a conversa volta.
    [Fact]
    public void Quantos_jogos_cabem_e_mostrado_e_nao_digitado()
    {
        var fonte = Details();

        Assert.Contains("JogosQueCabemNaJanela", fonte);

        var painel = fonte.IndexOf("id=\"pagamentosQuadras\"", StringComparison.Ordinal);
        var fimPainel = fonte.IndexOf("id=\"pagamentosEliminatorias\"", StringComparison.Ordinal);
        var trecho = fimPainel > painel ? fonte[painel..fimPainel] : fonte[painel..];
        Assert.DoesNotContain("name=\"quantosJogos\"", trecho);
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
