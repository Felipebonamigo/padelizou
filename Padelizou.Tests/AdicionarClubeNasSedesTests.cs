using System.IO;
using Xunit;

namespace Padelizou.Tests;

// "ADICIONAR CLUBE" DENTRO DE QUADRAS E SEDES (08/09/2026).
//
// 🗣️ Felipe: "permita aqui alterar para 'adicionar clube' nessa parte".
//
// O seletor de cada quadra só listava clube que já existia. Quem alugava uma quadra num lugar
// ainda não cadastrado ficava sem saída na tela: o aviso do rodapé mandava pra "Gerenciar
// Torneio", que cadastra QUADRA e não CLUBE — o clube novo não nascia em lugar nenhum do
// caminho, e o organizador voltava pra cá com a mesma lista de antes.
//
// ⚠️ POR QUE TESTE DE FONTE: a suíte não renderiza Razor nem roda JavaScript (mesmo motivo do
// SubAbaQuadrasESedesTests). O que estes testes seguram é a AMARRAÇÃO — que a tela chame a
// porta que já existe, e que o valor do <option> continue no formato que o servidor lê.
public class AdicionarClubeNasSedesTests
{
    [Fact]
    public void O_editor_de_sedes_oferece_cadastrar_clube_novo()
    {
        var fonte = Details();

        var form = fonte.IndexOf("id=\"pdzSedes\"", StringComparison.Ordinal);
        Assert.True(form >= 0, "Não achei o formulário de sedes.");

        var campo = fonte.IndexOf("id=\"pdzNovoClubeSede\"", form, StringComparison.Ordinal);
        Assert.True(campo > form, "O campo do clube novo precisa estar DENTRO do editor de sedes.");
        Assert.Contains("adicionarClubeNasSedes()", fonte);
    }

    // A cidade não é enfeite: é por ela que o clube passa a existir pra qualquer mira
    // geográfica (Services/UfDoTorneio), e é o rótulo do <optgroup> nas outras telas. As três
    // portas que já criavam clube perguntam; esta seria a quarta a não perguntar.
    [Theory]
    [InlineData("id=\"pdzNovoClubeSedeCidade\"")]
    [InlineData("id=\"pdzNovoClubeSedeEstado\"")]
    public void A_cidade_e_a_UF_tambem_sao_perguntadas(string marca)
    {
        var fonte = Details();
        var form = fonte.IndexOf("id=\"pdzSedes\"", StringComparison.Ordinal);

        Assert.True(fonte.IndexOf(marca, form, StringComparison.Ordinal) > form);
    }

    // ⚠️ O <button> tem que ser type="button". Ele mora DENTRO do <form> das sedes, e o padrão
    // do HTML pra botão sem type é submit: um clique salvaria as sedes e recarregaria a página
    // antes de o clube existir — perdendo, de quebra, o que o organizador tivesse escolhido.
    [Fact]
    public void O_botao_de_adicionar_nao_e_submit_do_formulario_de_sedes()
    {
        var fonte = Details();

        var botao = fonte.IndexOf("adicionarClubeNasSedes()", StringComparison.Ordinal);
        Assert.True(botao >= 0);

        var abre = fonte.LastIndexOf('<', botao);
        var tag = fonte.Substring(abre, botao - abre);
        Assert.Contains("type=\"button\"", tag);
    }

    [Fact]
    public void A_tela_carrega_o_arquivo_que_cadastra_local()
    {
        Assert.Contains("js/adicionar-local.js", Details());
    }

    // ── O JavaScript ────────────────────────────────────────────────────────────────────

    // ⚠️ O CABEÇALHO DO PRÓPRIO ARQUIVO conta a história: as três telas tinham o mesmo POST
    // copiado byte a byte, e quando a cidade entrou o preço foi mexer em três lugares. Esta é
    // a quarta tela; se ela trouxer um `fetch` próprio, o arquivo volta a ser o que era.
    [Fact]
    public void O_POST_para_criar_clube_continua_existindo_uma_vez_so()
    {
        var js = AdicionarLocal();

        var vezes = 0;
        for (var i = js.IndexOf("'/Clubes/Criar'", StringComparison.Ordinal); i >= 0;
                 i = js.IndexOf("'/Clubes/Criar'", i + 1, StringComparison.Ordinal))
        {
            vezes++;
        }

        Assert.Equal(1, vezes);
    }

    // ⚠️ AQUI SÃO N SELECTS, um por quadra — não o `#selectClube` único das outras telas. O
    // clube novo tem que aparecer em TODOS: o organizador acabou de cadastrar o lugar pra
    // poder escolhê-lo, e não dá pra adivinhar em qual quadra ele vai pôr.
    [Fact]
    public void O_clube_novo_entra_em_todas_as_quadras()
    {
        var js = AdicionarLocal();

        Assert.Contains("adicionarClubeNasSedes", js);
        Assert.Contains("pdz-sede-da-quadra", js);
        Assert.Contains("querySelectorAll", js);
    }

    // ⚠️ O VALOR DO <option> É "quadraId:clubeId" — é assim que
    // Services/SedesDoTorneio.LerClubePorQuadra lê o POST. Uma opção nova com só o id do clube
    // seria aceita pelo <select>, viajaria no formulário e cairia fora na leitura: a quadra
    // ficaria no clube do torneio, em silêncio, e a sede que a pessoa acabou de criar sumiria.
    [Fact]
    public void A_opcao_nova_carrega_a_quadra_junto_com_o_clube()
    {
        var js = AdicionarLocal();

        var funcao = js.IndexOf("function adicionarClubeNasSedes", StringComparison.Ordinal);
        Assert.True(funcao >= 0, "Não achei a função das sedes.");

        var trecho = js.Substring(funcao);
        Assert.Contains("split(':')[0]", trecho);
    }

    // ── O CLUBE TEM QUE IR PARAR EM ALGUMA QUADRA (08/09/2026) ──────────────────────────
    //
    // 🗣️ Felipe: "mas aqui eu nao consigo selecionar o clube, parece um bug".
    //
    // A primeira versão só punha o clube na lista das quadras e parava aí. Do lado de quem
    // usa, cadastrar um clube e ver os dois seletores continuarem em "Er Padel" é a mesma
    // coisa que não ter funcionado — e quando o nome digitado já existia (achar-ou-criar), a
    // resposta era "já estava na lista", um beco sem saída: nem cadastrou, nem colocou em
    // lugar nenhum, nem disse o que fazer em seguida.
    //
    // Agora o bloco pergunta EM QUAL QUADRA, e é isso que ele faz — cadastrar virou meio do
    // caminho, não o fim.
    [Fact]
    public void O_bloco_pergunta_em_qual_quadra_o_clube_fica()
    {
        var fonte = Details();
        var form = fonte.IndexOf("id=\"pdzSedes\"", StringComparison.Ordinal);

        var escolha = fonte.IndexOf("id=\"pdzNovoClubeSedeQuadra\"", form, StringComparison.Ordinal);
        Assert.True(escolha > form, "Falta o seletor de quadra no bloco de cadastrar clube.");

        // As opções são as quadras do torneio, endereçadas por Id — o mesmo Id que forma o
        // "quadraId:clubeId" dos seletores de cima.
        var trecho = fonte.Substring(escolha, 600);
        Assert.Contains("quadra.Id", trecho);
    }

    // ⚠️ SÓ A QUADRA ESCOLHIDA MUDA. O clube entra na LISTA de todas (é a mesma lista de
    // clubes em toda parte), mas trocar a sede de todas as quadras de uma vez esvaziaria a
    // sede principal sem ninguém ter pedido — e num torneio de duas quadras isso é o torneio
    // inteiro mudando de endereço por causa de um cadastro.
    [Fact]
    public void So_a_quadra_escolhida_recebe_o_clube()
    {
        var js = AdicionarLocal();
        var trecho = CorpoDaFuncaoDasSedes(js);

        Assert.Contains("pdzNovoClubeSedeQuadra", trecho);
        Assert.Contains("=== alvo", trecho);
    }

    // ⚠️ SEM O `change`, A TABELA DE BAIXO MENTE. "Em que clube cada categoria joga" é montada
    // a partir do que está SELECIONADO nos seletores de quadra, e quem a remonta é o listener
    // de `change` que vive no Details.cshtml. Mudar o valor por código não dispara evento
    // nenhum: a sede nova apareceria em cima e a tabela continuaria oferecendo só a antiga.
    [Fact]
    public void A_troca_avisa_a_tabela_de_categorias()
    {
        var trecho = CorpoDaFuncaoDasSedes(AdicionarLocal());

        Assert.Contains("new Event('change'", trecho);
        Assert.Contains("bubbles", trecho);
    }

    // Clube que já existia não é erro nem beco sem saída: ele é aplicado na quadra do mesmo
    // jeito. O achar-ou-criar do servidor devolve o antigo, e pra quem está na tela a diferença
    // entre "criei agora" e "já existia" não muda nada do que ela queria fazer.
    [Fact]
    public void Clube_que_ja_existia_tambem_vai_pra_quadra()
    {
        var trecho = CorpoDaFuncaoDasSedes(AdicionarLocal());

        Assert.DoesNotContain("já estava na lista", trecho);
    }

    // O aviso tem que dizer o que ainda FALTA. A troca acontece só na tela — sem clicar em
    // "Salvar sedes" nada disso chega ao banco, e sair da página perde tudo em silêncio.
    [Fact]
    public void O_aviso_lembra_de_salvar()
    {
        var trecho = CorpoDaFuncaoDasSedes(AdicionarLocal());

        Assert.Contains("Salvar sedes", trecho);
    }

    private static string CorpoDaFuncaoDasSedes(string js)
    {
        var funcao = js.IndexOf("function adicionarClubeNasSedes", StringComparison.Ordinal);
        Assert.True(funcao >= 0, "Não achei a função das sedes.");
        return js.Substring(funcao);
    }

    private static string Details() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

    private static string AdicionarLocal() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "wwwroot", "js", "adicionar-local.js"));

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
