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

    // ⚠️ 09/09/2026: AS TRÊS OPÇÕES SAÍRAM DO SELECT DO IMPEDIMENTO e ganharam formulário
    // próprio. Enquanto dividiam o mesmo `<select>`, gravar uma apagava a outra — a reclamação
    // que originou o conserto ("não consegue pôr os 2 jogos no sábado de manhã"). São de donos
    // diferentes: o impedimento é do JOGADOR, a concentração é do ORGANIZADOR por cima.
    [Theory]
    [InlineData("SextaNoite")]
    [InlineData("SabadoManha")]
    [InlineData("SabadoTarde")]
    public void O_formulario_de_concentracao_oferece_os_tres_turnos(string turno)
    {
        var fonte = Details();

        var form = fonte.IndexOf("asp-action=\"AlterarConcentracaoOrganizador\"", StringComparison.Ordinal);
        Assert.True(form >= 0, "Não achei o formulário de concentração.");

        var fim = fonte.IndexOf("</form>", form, StringComparison.Ordinal);
        Assert.Contains($"TurnoDeConcentracao.{turno}", fonte[form..fim]);
    }

    // ⚠️ OS DOIS FORMULÁRIOS SÃO SEPARADOS, e é isso que faz as duas coisas conviverem: um POST
    // só, com um campo só, voltaria a apagar a outra na hora de gravar.
    [Fact]
    public void Impedimento_e_concentracao_sao_formularios_diferentes()
    {
        var fonte = Details();

        var doImpedimento = fonte.IndexOf("asp-action=\"AlterarImpedimentoOrganizador\"", StringComparison.Ordinal);
        var daConcentracao = fonte.IndexOf("asp-action=\"AlterarConcentracaoOrganizador\"", StringComparison.Ordinal);

        Assert.True(doImpedimento >= 0 && daConcentracao >= 0);
        Assert.NotEqual(doImpedimento, daConcentracao);

        // O select do impedimento não pode carregar concentração nenhuma.
        var fimDoImpedimento = fonte.IndexOf("</form>", doImpedimento, StringComparison.Ordinal);
        Assert.DoesNotContain("TurnoDeConcentracao", fonte[doImpedimento..fimDoImpedimento]);
    }

    // 🗣️ "não consegue ver qual impedimento foi solicitado pelo usuário, e qual pelo organizador".
    [Fact]
    public void A_tela_diz_de_quem_foi_cada_coisa()
    {
        var fonte = Details();

        Assert.Contains("AutoriaDoImpedimentoDaDupla.Rotulo", fonte);
        Assert.Contains("posto pelo organizador", fonte);
    }

    // As duas podem se contradizer, e a grade cede calada — o aviso tem que estar na tela.
    [Fact]
    public void A_tela_avisa_quando_as_duas_se_cruzam()
    {
        var fonte = Details();

        Assert.Contains("ConcentracaoDeJogos.Conflita", fonte);
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
