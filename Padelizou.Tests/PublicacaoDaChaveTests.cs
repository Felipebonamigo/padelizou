using System.Text.RegularExpressions;
using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 🗣️ Felipe, 09/09/2026, olhando o painel do torneio do Er: "coloque um aviso, que clicando em
// sortear agora, nao publica a chave, fica apenas visivel para o organizador e adm".
//
// ⚠️ E ISSO SÓ É VERDADE NO FORMATO PADRÃO. Conferido antes de escrever na tela: o `GerarChaves`
// para em `AprovacaoDeChaves.Pendente` e só quem organiza enxerga (`ViewBag.PodeAprovarChaves`);
// mas `GerarRodadasAmericano`/`GerarRodadasAmericanoDuplas` vão **direto pra "Fase de Grupos"**,
// que já é público — está escrito em TorneiosController.Americano, no comentário de
// `DesfazerRodadasAmericano`.
//
// Um aviso incondicional mentiria justamente no caso mais perigoso: o organizador clicaria
// achando que é rascunho e o rodízio inteiro sairia pros jogadores na hora. Por isso a régua
// mora aqui, e a tela pergunta em vez de afirmar.
public class PublicacaoDaChaveTests
{
    private static Torneio Do(string formato) => new() { Nome = "T", Codigo = "T1", Formato = formato };

    [Fact]
    public void No_formato_padrao_a_chave_espera_aprovacao()
    {
        Assert.False(PublicacaoDaChave.SaiPublicaNaHora(Do(FormatoDoTorneio.Padrao)));
    }

    [Theory]
    [InlineData(FormatoDoTorneio.Americano)]
    [InlineData(FormatoDoTorneio.AmericanoDeDuplas)]
    public void No_americano_o_sorteio_ja_sai_publico(string formato)
    {
        Assert.True(PublicacaoDaChave.SaiPublicaNaHora(Do(formato)));
    }

    // O texto muda junto com a régua — senão a tela e o servidor discordam, que é o problema
    // que este arquivo existe pra impedir.
    [Fact]
    public void O_aviso_do_padrao_promete_que_nao_publica()
    {
        var aviso = PublicacaoDaChave.Aviso(Do(FormatoDoTorneio.Padrao));

        Assert.Contains("não", aviso, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("organizador", aviso, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void O_aviso_do_americano_avisa_o_contrario()
    {
        var aviso = PublicacaoDaChave.Aviso(Do(FormatoDoTorneio.Americano));

        Assert.Contains("todo mundo", aviso, StringComparison.OrdinalIgnoreCase);
    }

    // ── A tela ────────────────────────────────────────────────────────────────────────────
    // ⚠️ POR QUE TESTE DE FONTE: a suíte não renderiza Razor. A RÉGUA tem trava de verdade aqui
    // em cima; o que só existe na view é o aviso estar no lugar certo — ao lado do botão que o
    // Felipe apontou — e vir do serviço em vez de ser texto solto que envelhece sozinho.
    [Fact]
    public void O_aviso_aparece_ao_lado_do_botao_de_sortear_agora()
    {
        var fonte = Details();

        var botao = fonte.IndexOf("asp-action=\"AdiarTaxaExterno\"", StringComparison.Ordinal);
        Assert.True(botao >= 0, "Não achei o botão \"Sortear agora, pagar depois\".");

        var aviso = fonte.LastIndexOf("PublicacaoDaChave.Aviso(Model)", botao, StringComparison.Ordinal);
        Assert.True(aviso >= 0, "O aviso precisa vir ANTES do botão, no mesmo bloco.");
    }

    // ⚠️ E NÃO PODE SER TEXTO SOLTO NA VIEW: a frase e a régua têm que morrer juntas. Escrita à
    // mão, ela sobreviveria a uma mudança no fluxo de aprovação e passaria a mentir.
    [Fact]
    public void A_frase_vem_do_servico_e_nao_esta_escrita_na_view()
    {
        // ⚠️ SEM OS COMENTÁRIOS RAZOR, e não é detalhe: este projeto cita o pedido do Felipe
        // literalmente nos comentários, então a frase dele APARECE no arquivo de propósito. A
        // primeira versão deste teste caiu por causa disso — ela media o texto do comentário,
        // não o que vai pra tela. Mesma limpeza que AbaPagamentosNaPaginaDoTorneioTests já faz.
        var markup = Regex.Replace(Details(), "@\\*.*?\\*@", "", RegexOptions.Singleline);

        Assert.DoesNotContain("fica apenas visivel para o organizador", markup);
        Assert.DoesNotContain("Sortear não publica a chave", markup);
        Assert.Contains("PublicacaoDaChave.Aviso(Model)", markup);
    }

    // A confirmação do clique dizia "As chaves são liberadas na hora" — que lê como "vai pro
    // ar", justamente a confusão que este pedido veio desfazer.
    [Fact]
    public void A_confirmacao_nao_promete_mais_que_a_chave_sai_liberada()
    {
        var fonte = Details();

        Assert.DoesNotContain("As chaves são liberadas na hora", fonte);
    }

    private static string Details() =>
        System.IO.File.ReadAllText(System.IO.Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

    private static string PastaDoProjeto()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = System.IO.Path.Combine(dir.FullName, "Padelizou", "Views");
            if (System.IO.Directory.Exists(alvo)) return System.IO.Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }
        throw new System.IO.DirectoryNotFoundException("Não achei a pasta do projeto Padelizou.");
    }

    // ⚠️ Os dois avisos precisam ser DIFERENTES. Um texto só, genérico o bastante pra servir aos
    // dois, seria o mesmo que não avisar nada.
    [Fact]
    public void Os_dois_avisos_nao_sao_o_mesmo_texto()
    {
        Assert.NotEqual(PublicacaoDaChave.Aviso(Do(FormatoDoTorneio.Padrao)),
                        PublicacaoDaChave.Aviso(Do(FormatoDoTorneio.Americano)));
    }
}
