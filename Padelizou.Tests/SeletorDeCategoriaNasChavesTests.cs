using Xunit;

namespace Padelizou.Tests;

// O SELETOR DE CATEGORIA DA ABA "Chaves e Grupos" VIROU UM <select>.
//
// 🗣️ Felipe, 10/09/2026, com o 2ª Etapa ER Padel Tour aberto em Chaves e Grupos (7 categorias):
// *"visualmente nao ta legal isso aqui tambem, acho que um drop com select seria melhor, não?"*.
//
// 🕳️ A barra era `.pdz-pills`, desenhada pra 3-4 abas FIXAS (Ao Vivo / Agendadas / Finalizadas).
// Lá, `flex: 1 1 auto` é o acerto: as abas preenchem a linha e não sobra buraco. Numa lista de
// tamanho VARIÁVEL o mesmo acerto vira defeito — com 7 categorias a sétima cai sozinha na
// segunda linha e estica pela largura inteira, virando uma faixa verde que parece título, e não
// aba escolhida. O ER já teve 12 categorias: quatro linhas de botão antes do primeiro grupo.
//
// ⚠️ O REMÉDIO NÃO É MEXER NO `.pdz-pills`: as abas fixas que o usam estão certas do jeito que
// estão. Quem troca de desenho é só esta lista, que é a única de tamanho variável da tela.
public class SeletorDeCategoriaNasChavesTests
{
    [Fact]
    public void A_escolha_da_categoria_e_um_select_e_nao_mais_uma_barra_de_pills()
    {
        var fonte = Details();

        Assert.Contains("id=\"seletorDeCategoria\"", fonte);
        Assert.DoesNotContain("id=\"categoriaPills\"", fonte);
        Assert.DoesNotContain("data-bs-toggle=\"pill\"", fonte);
    }

    // O que quebra calado se alguém mexer numa ponta só: o valor da opção e o id do painel são
    // O MESMO texto montado do mesmo Id. Divergiram, escolher a categoria não mostra nada — e a
    // tela não acusa erro nenhum, só fica vazia.
    [Fact]
    public void Cada_opcao_aponta_pro_painel_da_propria_categoria()
    {
        var fonte = Details();

        Assert.Contains("value=\"cat-@(categoria.Id)\"", fonte);
        Assert.Contains("id=\"cat-@(categoria.Id)\"", fonte);
    }

    // UMA RÉGUA SÓ decide quem entra na tela. Com duas (uma pro seletor, outra pros painéis), a
    // primeira mudança deixa opção sem painel — escolher a categoria e não ver nada — ou painel
    // sem opção, que é chave desenhada e inalcançável. O seletor sai de `comChave`, e `comChave`
    // sai do MESMO predicado que os painéis usam: a condição existe escrita uma vez só.
    [Fact]
    public void O_seletor_e_os_paineis_saem_da_MESMA_regua()
    {
        var fonte = Details();

        Assert.Equal(1, Ocorrencias(fonte, "|| c.ChaveDireta"));
        Assert.Contains("Model.Categorias.Where(temChaveParaMostrar)", fonte);
        Assert.Contains("if (temChaveParaMostrar(categoria))", fonte);
    }

    // Com uma categoria só, um select é um controle que não escolhe nada. O nome dela continua
    // na tela — é ele que diz de quem é a chave desenhada logo abaixo.
    [Fact]
    public void Uma_categoria_so_nao_ganha_um_seletor_que_nao_escolhe_nada()
    {
        var fonte = Details();

        Assert.Contains("comChave.Count > 1", fonte);
        Assert.Contains("comChave.Count == 1", fonte);
    }

    // Sem barra de abas, "tabpanel" vira papel órfão: o leitor de tela anuncia "painel de aba"
    // e procura a aba que o comanda, que não existe mais. O painel virou uma região com o nome
    // da categoria — que é o que ele é.
    [Fact]
    public void O_painel_da_categoria_nao_se_anuncia_mais_como_aba()
    {
        var fonte = Details();

        Assert.DoesNotContain("id=\"cat-@(categoria.Id)\" role=\"tabpanel\"", fonte);
        Assert.Contains("id=\"cat-@(categoria.Id)\" role=\"region\" aria-label=\"@categoria.Nome\"", fonte);
    }

    // 🗣️ Felipe, vendo o seletor no ar: *"esta fora de ordem"*. A lista saía na ordem em que as
    // categorias foram CRIADAS — o mesmo defeito de 08/08 que fez nascer o CategoriaNaTela.Ordem.
    [Fact]
    public void As_categorias_saem_na_ordem_de_tela_do_site()
    {
        // Na DECLARAÇÃO do comChave, e não em qualquer lugar da tela: a mesma chamada já existe
        // noutro trecho do arquivo, e um Contains solto passaria sem o seletor ter sido tocado.
        var fonte = Details();
        var inicio = fonte.IndexOf("var comChave =", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Sumiu a lista comChave, que é quem alimenta o seletor.");
        var declaracao = fonte[inicio..(fonte.IndexOf(';', inicio) + 1)];

        Assert.Contains("CategoriaNaTela.Ordem(c.Nome)", declaracao);
    }

    // 🗣️ *"venha sempre selecionado a categoria que o usuario esta cadastrado"*. Tem que vir
    // MARCADA DO SERVIDOR, nas duas pontas: a <option> escolhida e o painel aceso. Deixar pro JS
    // acertar depois pintaria a chave errada por um instante a cada abertura.
    [Fact]
    public void A_categoria_que_abre_vem_marcada_do_servidor_nas_duas_pontas()
    {
        var fonte = Details();

        Assert.Contains("CategoriaQueAbre.Escolher(comChave", fonte);
        Assert.Contains("categoria.Id == categoriaQueAbre?.Id ? \"selected\" : null", fonte);
        Assert.Contains("categoria.Id == categoriaQueAbre?.Id ? \"show active\" : \"\"", fonte);
        // A primeira da lista deixou de mandar: era ela que abria a tela antes.
        Assert.DoesNotContain("primeiraCatContent", fonte);
    }

    [Fact]
    public void A_tela_carrega_o_js_do_seletor()
    {
        Assert.Contains("~/js/seletor-de-categoria.js", Details());
        Assert.False(string.IsNullOrWhiteSpace(SeletorJs()));
    }

    // O painel é `.tab-pane fade`: sem a classe `show` ele fica com opacidade 0 mesmo estando
    // `active` — a categoria escolhida sumiria em vez de aparecer.
    [Fact]
    public void Trocar_de_categoria_acende_o_painel_inteiro()
    {
        var js = SeletorJs();

        Assert.Contains("\"active\"", js);
        Assert.Contains("\"show\"", js);
    }

    // A MESMA queixa de 08/08 que fez nascer o js/jogos-abas.js ("quando eu salvo aqui, ele volta
    // pra tela de ao vivo"): dentro da categoria se troca dupla de grupo e se desenha chave à
    // mão, e todo POST redesenha a página na PRIMEIRA categoria. Com 12 delas, é caçar a sua de
    // novo a cada gravação. Por torneio, como lá: quem opera dois no mesmo dia não herda um no outro.
    [Fact]
    public void A_categoria_escolhida_sobrevive_ao_salvar()
    {
        Assert.Contains("data-torneio-id=\"@Model.Id\"", Details());

        var js = SeletorJs();
        Assert.Contains("sessionStorage", js);
        Assert.Contains("data-torneio-id", js);
    }

    // O NAVEGADOR RESTAURA O VALOR DO <select> SOZINHO num F5 (form restoration), e ele não
    // restaura junto a classe do painel — que vem do servidor sempre na primeira categoria.
    // Sem sincronizar na abertura, o seletor diria "6ª Feminina" com a chave da 3ª Masculina
    // desenhada embaixo: a tela mentindo, calada.
    [Fact]
    public void A_tela_abre_mostrando_a_categoria_que_o_seletor_mostra()
    {
        var js = SeletorJs();
        // NA ABERTURA, e não só no `change`: a chamada tem que existir antes do addEventListener,
        // senão a sincronia só acontece quando alguém troca de categoria — que é tarde demais.
        var naAbertura = js.IndexOf("mostrar(select.value)", StringComparison.Ordinal);
        var ouvinte = js.IndexOf("addEventListener", StringComparison.Ordinal);

        Assert.True(naAbertura >= 0 && naAbertura < ouvinte,
            "O seletor não acerta o painel na abertura — só ao trocar de categoria.");
    }

    // sessionStorage pode ser PROIBIDO (navegação privada com cookies bloqueados). Se o acesso
    // estourar solto, o seletor morre junto e a tela trava numa categoria só — a memória é
    // conforto, e conforto não pode derrubar a escolha.
    [Fact]
    public void Sem_memoria_o_seletor_continua_trocando_de_categoria()
    {
        var js = SeletorJs();
        Assert.Contains("sessionStorage", js);   // senão este teste passa vazio e não prova nada

        var linhasSoltas = js.Split('\n')
            .Where(l => l.Contains("sessionStorage", StringComparison.Ordinal))
            .Where(l => !l.Contains("try", StringComparison.Ordinal))
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal))
            .ToList();

        Assert.True(linhasSoltas.Count == 0,
            "Acesso ao sessionStorage fora de try/catch:\n" + string.Join("\n", linhasSoltas));
    }

    // ── infra ────────────────────────────────────────────────────────────────────────────

    private static int Ocorrencias(string texto, string pedaco)
    {
        int total = 0, de = 0;
        while ((de = texto.IndexOf(pedaco, de, StringComparison.Ordinal)) >= 0) { total++; de += pedaco.Length; }
        return total;
    }

    private static string Details() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

    private static string SeletorJs()
    {
        var caminho = Path.Combine(PastaDoProjeto(), "wwwroot", "js", "seletor-de-categoria.js");
        return File.Exists(caminho) ? File.ReadAllText(caminho) : "";
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto Padelizou a partir de " + AppContext.BaseDirectory);
    }
}
