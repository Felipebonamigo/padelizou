using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// 10/11/2026 — EXCLUIR UM INSCRITO DEVOLVIA O ORGANIZADOR PRO COMEÇO DA PÁGINA.
//
// 🗣️ Amadeu (Chakra), com vídeo do Gerenciar Torneio: *"tô excluindo, daí exclui o Balaka, voltou
// lá pro início; daí exclui o Eduardo, voltou lá pro início"*.
//
// 🕳️ DOIS BURACOS JUNTOS. (1) Os formulários da lista de inscritos do organizador não tinham o
// `data-manter-posicao` do js/manter-posicao-na-lista.js. (2) Esse script — e o js/jogos-abas.js,
// que lembra a ABA — só eram carregados dentro de `@if (torneioComecou)`: durante as inscrições,
// que é justamente quando se remove inscrito, a página renascia no topo E na aba padrão.
public class GerenciarInscritosFicaNoLugarTests
{
    private static string Details() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
        "Padelizou", "Views", "Torneios", "Details.cshtml"));

    // O corpo do `@if (torneioComecou) { ... }` dos scripts do fim da página.
    private static string BlocoSoDoTorneioComecado(string tela)
    {
        var inicio = tela.IndexOf("@if (torneioComecou)\n{", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Não achei o bloco @if (torneioComecou) dos scripts.");
        var fim = tela.IndexOf("\n}\n", inicio, StringComparison.Ordinal);
        return tela[inicio..fim];
    }

    [Theory]
    [InlineData("~/js/manter-posicao-na-lista.js")]
    [InlineData("~/js/jogos-abas.js")]
    // O "Abrir cobrança 1 de N" é um botão que só o JS faz funcionar — preso no bloco, ele ficava
    // MORTO justamente nas inscrições, que é quando se cobra.
    [InlineData("~/js/cobrar-todos.js")]
    public void A_memoria_de_aba_e_de_altura_carrega_tambem_durante_as_inscricoes(string script)
    {
        var tela = Details();

        Assert.DoesNotContain(script, BlocoSoDoTorneioComecado(tela));
        Assert.Contains($"<script src=\"{script}\"", tela);
    }

    [Theory]
    [InlineData("RemoverDupla")]
    [InlineData("AlternarPagamentoDupla")]
    [InlineData("TrocarCategoriaDupla")]
    [InlineData("AlterarImpedimentoOrganizador")]
    [InlineData("AlterarConcentracaoOrganizador")]
    public void As_acoes_da_lista_do_organizador_voltam_pra_mesma_altura(string acao)
    {
        var formularios = Regex.Matches(Details(), "<form[^>]*asp-action=\"" + acao + "\"[^>]*>", RegexOptions.Singleline);

        Assert.NotEmpty(formularios);
        Assert.All(formularios, f => Assert.Contains("data-manter-posicao", f.Value));
    }

    [Fact]
    public void Trocar_parceiro_do_organizador_volta_pra_mesma_altura()
    {
        // O TrocarParceiro também mora no card do próprio jogador; o do ORGANIZADOR é o da lista.
        var doOrganizador = Regex.Match(Details(), "<form[^>]*asp-action=\"TrocarParceiro\"[^>]*pdz-form-parceiro[^>]*>",
            RegexOptions.Singleline);

        Assert.True(doOrganizador.Success);
        Assert.Contains("data-manter-posicao", doOrganizador.Value);
    }

    [Fact]
    public void Cobrar_no_whatsapp_guarda_a_altura_antes_de_sair()
    {
        // 🗣️ Amadeu: *"ao clicar em marcar como pago, e cobrar, volta lá pra cima"*. O Cobrar abre o
        // WhatsApp; no celular, voltar ao app RECARREGA a página. O link guarda a altura no clique
        // (o js/manter-posicao-na-lista.js ouve `a[data-manter-posicao]`) e a página volta nela.
        var cobrar = Regex.Match(Details(), "<a[^>]*pdz-cobrar-link[^>]*>", RegexOptions.Singleline);

        Assert.True(cobrar.Success);
        Assert.Contains("data-manter-posicao", cobrar.Value);
    }
}
