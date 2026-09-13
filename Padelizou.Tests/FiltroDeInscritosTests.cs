using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// 13/09/2026 — ACHAR UM INSCRITO NA LISTA DO ORGANIZADOR. 🗣️ Felipe, com o print do
// "Gerenciar Inscritos" do 2ª Etapa ER PADEL TOUR aberto no celular: *"aqui no gerenciar
// escrito esta dificil achar, permita pesquisar por nome, coloque filtro por categoria, deixe
// melhor essa parte"*.
//
// ⚠️ POR QUE TESTE DE FONTE: a suíte não renderiza Razor. O COMPORTAMENTO do filtro (busca sem
// acento, categoria, situação, o cabeçalho da categoria vazia) tem trava de verdade no
// `Padelizou.Tests/js/conferir-filtro-de-inscritos.js`, que roda no Node. O que só existe na
// VIEW — o escopo, os cinco atributos que cada linha precisa carregar e o <script> — nenhum
// teste de comportamento alcança.
//
// 🔑 O QUE ESTE ARQUIVO GUARDA DE VERDADE É O CONTRATO ENTRE O RAZOR E O JS. Eles se falam por
// NOME DE CLASSE e por `data-*`: renomear `.pdz-fi-item` no Razor não quebra compilação, não
// quebra teste nenhum, e o filtro simplesmente para de achar gente — falha calada, e na tela
// que o organizador usa com o torneio em quadra.
public class FiltroDeInscritosTests
{
    [Fact]
    public void As_duas_listas_de_inscritos_tem_a_barra_de_filtro()
    {
        var fonte = Details();

        // O escopo precisa existir DUAS vezes: "Gerenciar Inscritos" e "Pagamentos e
        // impedimentos". É ele que impede uma lista de filtrar a outra — com um escopo só
        // (ou nenhum), digitar numa esconderia linha da vizinha.
        // ⚠️ Conta o ATRIBUTO, não a palavra: os comentários do Razor citam estes nomes de
        // classe de propósito, e contar menção faria o teste vermelho por alguém explicar melhor.
        Assert.Equal(2, Regex.Matches(fonte, "class=\"pdz-inscritos-filtraveis\"").Count);

        // E as duas usam O MESMO parcial — a barra copiada seria a segunda cópia que um dia
        // diverge da primeira.
        Assert.Equal(2, Regex.Matches(fonte, "name=\"_FiltroDeInscritos\"").Count);

        Assert.Contains("js/filtro-de-inscritos.js", fonte);
    }

    [Fact]
    public void Toda_linha_de_inscrito_carrega_os_cinco_atributos_que_o_filtro_le()
    {
        var fonte = Details();

        // ⚠️ DENTRO DA TAG, e não no arquivo inteiro: `data-categoria` JÁ EXISTIA aqui, nos
        // formulários de trocar parceiro (`pdz-form-parceiro`), que são outra coisa e outro
        // script. Contar ocorrência no arquivo daria um número que não fala de nenhuma linha.
        var itens = Regex.Matches(fonte, "<(?:li|p)[^>]*pdz-fi-item[^>]*>", RegexOptions.Singleline)
            .Select(m => m.Value)
            .ToList();

        // As duas listas + o aviso da categoria vazia.
        Assert.Equal(3, itens.Count);

        // Uma linha sem um dos atributos não dá erro nenhum: ela some do recorte, calada.
        foreach (var item in itens)
        {
            foreach (var atributo in new[] { "data-nome=", "data-categoria=", "data-pago=", "data-parceiro=", "data-espera=" })
            {
                Assert.Contains(atributo, item);
            }
        }
    }

    [Fact]
    public void A_busca_enxerga_os_DOIS_jogadores_da_dupla_e_o_apelido()
    {
        var fonte = Details();

        // `ComoChamar` é "Paulo Prass (Batata)" — nome completo E apelido na mesma string, que
        // é o que faz procurar por "batata" achar. Com `NomeNaTela` (sem apelido) ou só com o
        // Jogador1, procurar pelo parceiro não acharia a dupla — e é procurando pelo parceiro
        // que o organizador acha metade delas.
        // ⚠️ Do `data-nome=` até o `data-categoria=` que vem logo depois, e não um `[^"]*`: a
        // expressão do Razor tem ASPAS DENTRO dela (o `" "` que separa os dois nomes), e um
        // regex parando na primeira aspa leria metade do atributo e passaria achando que está tudo certo.
        var nomes = Regex.Matches(fonte, "data-nome=(.*?)data-categoria=", RegexOptions.Singleline)
            .Select(m => m.Groups[1].Value)
            .Where(v => !v.TrimStart().StartsWith("\"\"", StringComparison.Ordinal)) // o aviso de categoria vazia
            .ToList();

        Assert.NotEmpty(nomes);

        foreach (var linha in nomes)
        {

            Assert.Contains("Jogador1.ComoChamar", linha);
            Assert.Contains("Jogador2.ComoChamar", linha);
        }
    }

    [Fact]
    public void O_Razor_e_o_JS_falam_as_mesmas_classes_e_os_mesmos_data()
    {
        var js = Js();
        var razor = Details() + Parcial();

        // Todo seletor `.pdz-...` que o JS procura tem que existir no Razor. Este é o gate que
        // pega o rename calado nos dois sentidos: quem mexer num lado vê vermelho aqui.
        var classes = Regex.Matches(js, "\\.(pdz-[a-z-]+)").Select(m => m.Groups[1].Value).Distinct();
        foreach (var classe in classes)
        {
            Assert.Contains(classe, razor);
        }

        // O mesmo pros `data-*`: o JS lê `item.dataset.pago`, o Razor escreve `data-pago`.
        var dados = Regex.Matches(js, "dataset\\.([a-z]+)").Select(m => m.Groups[1].Value).Distinct();
        foreach (var dado in dados)
        {
            Assert.Contains($"data-{dado}=", razor);
        }
    }

    [Fact]
    public void Os_atalhos_de_situacao_nunca_enviam_formulario()
    {
        var parcial = Parcial();

        // A barra vive no meio de uma tela cheia de <form>. Botão sem `type` é botão de
        // ENVIAR: clicar em "Não pagos" mandaria o formulário em volta — que aqui é
        // "marcar como pago" ou "remover do torneio".
        var botoes = Regex.Matches(parcial, "<button[^>]*pdz-fi-situacao[^>]*>", RegexOptions.Singleline);
        Assert.NotEmpty(botoes);

        foreach (var botao in botoes.Select(m => m.Value))
        {
            Assert.Contains("type=\"button\"", botao);
            // A classe `active` do Bootstrap é tinta; quem usa leitor de tela precisa do estado.
            Assert.Contains("aria-pressed=", botao);
        }
    }

    private static string Details() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

    private static string Parcial() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "_FiltroDeInscritos.cshtml"));

    private static string Js() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "wwwroot", "js", "filtro-de-inscritos.js"));

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
