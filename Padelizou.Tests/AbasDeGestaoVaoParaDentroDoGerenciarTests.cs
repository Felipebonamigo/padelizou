using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — "PAGAMENTOS E IMPEDIMENTOS" E "PLANEJAMENTO DE QUADRAS" SAEM DA BARRA DE ABAS
// DEPOIS QUE AS CHAVES SÃO PUBLICADAS.
//
// 🗣️ Felipe: *"Os menus Pagamentos e impedimentos e Planejamento de quadras, também pode mover
// para dentro do Gerenciar torneio, depois que foi publicada as chaves."*
//
// As duas são ferramentas do ANTES: impedimento alimenta o sorteio, planejamento decide quantas
// quadras alugar. Depois que a chave sai, elas viram duas linhas de abas entre quem abre a
// página e o placar — e a barra do organizador tem NOVE abas.
//
// ⚠️ O BOTÃO NÃO É APAGADO, É RECOLHIDO — e isso não é preciosismo. TREZE redirects do servidor
// voltam com o fragmento `#pagamentos` (TorneiosController.Inscricoes.cs: 615, 621, 653, 690,
// 695, 722, 761, 802, 820, 892, 924, 950, 977) e a tela cheia do Planejamento tem um link
// `asp-fragment="pagamentos"` (Views/Torneios/Planejamento.cshtml:494). O script do fim do
// Details.cshtml abre a aba procurando `#torneioTabs [data-bs-target="..."]`: apagado o botão,
// o seletor não acha nada, o script sai calado e o organizador cai na aba padrão toda vez que
// mexer num impedimento. Recolhido, o caminho continua inteiro.
public class AbasDeGestaoVaoParaDentroDoGerenciarTests
{
    [Theory]
    [InlineData("pagamentos-tab")]
    [InlineData("planejamento-tab")]
    public void O_LI_da_aba_recolhe_quando_a_chave_esta_publicada(string idDoBotao)
    {
        // ⚠️ A classe vai no <li class="nav-item">, NÃO no <button class="nav-link">. Quem é
        // filho do flex é o <li> — `site.css` dá `flex: 1 1 auto` a ele, e `1 1 calc(50% -
        // .25rem)` até 430px. Com `display:none` só no botão, sobram dois <li> VAZIOS
        // reivindicando metade da linha cada: no celular isso é uma FAIXA EM BRANCO inteira no
        // lugar exato de onde o card do Pix acabou de sair.
        var li = TagDoLi(idDoBotao);

        // ⚠️ A POLARIDADE, e não a presença dos dois literais. `Assert.Contains` dos dois
        // separados passava IDÊNTICO com o ternário INVERTIDO (`? "" : "pdz-aba-recolhida"`),
        // que é a aba sumindo justo ANTES de publicar e voltando depois — o oposto do pedido.
        // Provado por mutação em 10/09/2026: verde com o defeito instalado.
        Assert.Matches(
            new Regex(@"AprovacaoDeChaves\.ChavePublicada\(Model\)\s*\?\s*""pdz-aba-recolhida"""),
            li);
    }

    [Theory]
    [InlineData("pagamentos-tab")]
    [InlineData("planejamento-tab")]
    public void E_o_botao_NAO_leva_a_classe(string idDoBotao)
    {
        // Esconder o botão dentro de um <li> que continua ocupando largura é o buraco descrito
        // acima. Um lugar só, e é o <li>.
        Assert.DoesNotContain("pdz-aba-recolhida", TagDoBotao(idDoBotao));
    }

    [Fact]
    public void O_elemento_recolhido_e_o_MESMO_que_o_flex_estica()
    {
        // A regra que cria o buraco mora aqui: se um dia ela migrar do `.nav-item` pro
        // `.nav-link`, o `display:none` precisa migrar junto.
        var css = File.ReadAllText(CaminhoDoCss());

        Assert.Matches(new Regex(@"\.pdz-abas\s+\.nav-item\s*\{[^}]*flex:", RegexOptions.Singleline), css);
    }

    [Theory]
    [InlineData("pagamentos-tab")]
    [InlineData("planejamento-tab")]
    public void E_continua_no_DOM_pra_hash_nao_quebrar(string idDoBotao)
    {
        // O `data-bs-target` é o que o script da hash procura. Sem ele no DOM, os treze
        // redirects de `#pagamentos` caem na aba padrão.
        var botao = TagDoBotao(idDoBotao);
        var alvo = idDoBotao.Replace("-tab", "");

        Assert.Contains($"data-bs-target=\"#{alvo}\"", botao);
        Assert.Contains("data-bs-toggle=\"tab\"", botao);
    }

    [Fact]
    public void A_aba_recolhida_REAPARECE_quando_e_a_aba_ativa()
    {
        // Sem isto, o redirect de `#pagamentos` abriria o painel com a barra sem nenhuma aba
        // marcada — o conteúdo certo e nenhuma pista de onde a pessoa está.
        //
        // ⚠️ Quem ganha `.active` é o <button class="nav-link"> (é o Bootstrap que põe), mas
        // quem está escondido é o <li> PAI. Daí o `:has()` — `.pdz-aba-recolhida.active` nunca
        // casaria, porque as duas classes não vivem no mesmo elemento.
        var css = File.ReadAllText(CaminhoDoCss());

        Assert.Matches(new Regex(@"\.pdz-aba-recolhida\s*\{[^}]*display:\s*none", RegexOptions.Singleline), css);
        // ⚠️ O VALOR, e não só a propriedade: terminar em `display:` deixava
        // `{ display: none; }` passar — a aba ativa continuando invisível, que é exatamente o
        // painel abrindo com a barra sem nada marcado. Provado por mutação em 10/09/2026.
        Assert.Matches(new Regex(@"\.pdz-aba-recolhida:has\([^)]*\.nav-link\.active[^)]*\)\s*\{[^}]*display:\s*block", RegexOptions.Singleline), css);
    }

    [Fact]
    public void O_painel_Gerenciar_Torneio_oferece_as_duas()
    {
        // Recolher sem oferecer outro caminho seria esconder a ferramenta, não movê-la.
        //
        // ⚠️ NO BOTÃO, E NÃO NO PAINEL. Procurar a frase no painel inteiro não prova nada: ele
        // tem 196 MIL caracteres, e dentro dele já existiam DUAS outras aparições de
        // "Pagamentos e impedimentos" — uma no comentário que cita o pedido do Felipe e outra
        // em texto de verdade da página ("Pagamentos e impedimentos › Quadras e sedes"). Com
        // isso, apagar o rótulo do atalho deixava este teste verde. Provado por mutação em
        // 10/09/2026 — e tirar os comentários NÃO resolvia, porque a segunda aparição executa.
        Assert.Contains("Pagamentos e impedimentos", TextoDoAtalho("pagamentos-tab"));
        Assert.Contains("Planejamento de quadras", TextoDoAtalho("planejamento-tab"));
    }

    // O que está ESCRITO no atalho do painel de gestão que abre a aba `idDaAba` — do `>` que
    // fecha a tag do botão até o `</button>`.
    private static string TextoDoAtalho(string idDaAba)
    {
        var painel = PainelDoAdmin();
        var clique = painel.IndexOf($"getElementById('{idDaAba}')", StringComparison.Ordinal);
        Assert.True(clique >= 0, $"Não achei o atalho que abre a aba {idDaAba} no painel de gestão.");

        var abre = painel.IndexOf('>', clique);
        var fecha = painel.IndexOf("</button>", clique, StringComparison.Ordinal);
        Assert.True(abre >= 0 && fecha > abre, $"O atalho de {idDaAba} não é um <button> fechado.");
        return painel[(abre + 1)..fecha];
    }

    [Fact]
    public void Os_atalhos_do_painel_so_existem_quando_as_abas_estao_recolhidas()
    {
        // Antes de publicar as abas continuam na barra; repetir os atalhos ali dentro seria
        // dois caminhos pro mesmo lugar na mesma tela.
        var painel = PainelDoAdmin();
        var atalhos = painel.IndexOf("pagamentos-tab", StringComparison.Ordinal);
        Assert.True(atalhos >= 0, "Não achei os atalhos no painel de gestão.");

        // ⚠️ O `@if (` PRECISA ESTAR NA BUSCA. Procurar só "AprovacaoDeChaves.ChavePublicada
        // (Model)" deixava o `!` de fora da string: `@if (!ChavePublicada(Model))` — atalho que
        // só aparece ANTES de publicar, quando as abas ainda estão na barra — passava verde.
        // Provado por mutação em 10/09/2026.
        var gate = Regex.Matches(painel[..atalhos], @"@if\s*\(\s*AprovacaoDeChaves\.ChavePublicada\(Model\)\s*\)");
        Assert.True(gate.Count > 0,
            "Os atalhos precisam estar atrás de um `@if (AprovacaoDeChaves.ChavePublicada(Model))` — sem `!`.");

        var ultimo = gate[^1];
        Assert.True(atalhos - (ultimo.Index + ultimo.Length) < 900,
            "O `@if` está longe demais dos atalhos pra ser o portão deles.");
    }

    [Fact]
    public void O_CINTO_do_navegador_sem_has_chega_na_tela()
    {
        // 🕳️ Arquivo que existe e nunca carrega é o mesmo defeito da prévia que ficou semanas
        // sem botão de horário: o `conferir-abas-recolhidas.js` passaria verde pra sempre
        // testando um script que nenhuma página inclui. Quem prova o comportamento é o Node;
        // este aqui prova só o elo que falta — que ele está na tela.
        var view = Details();

        Assert.Contains("~/js/abas-recolhidas.js", view);
        Assert.Contains(File.Exists(Path.Combine(RaizDoRepo(), "Padelizou", "wwwroot", "js", "abas-recolhidas.js"))
            ? "~/js/abas-recolhidas.js" : "ARQUIVO NAO EXISTE", view);

        // ⚠️ `asp-append-version` é obrigatório: o Service Worker guarda script em cache pelo
        // caminho, e sem a versão o organizador fica preso no JS velho indefinidamente — a
        // mesma razão escrita no jogos.cshtml.
        var i = view.IndexOf("~/js/abas-recolhidas.js", StringComparison.Ordinal);
        var fimDaTag = view.IndexOf('>', i);
        Assert.Contains("asp-append-version", view[i..fimDaTag]);
    }

    // A tag inteira do <button>, do `<` até o `>` que a fecha.
    private static string TagDoBotao(string idDoBotao)
    {
        var view = Details();
        var i = PosicaoDoBotao(view, idDoBotao);
        return view[view.LastIndexOf('<', i)..view.IndexOf('>', i)];
    }

    // A tag do <li> que embrulha esse botão.
    private static string TagDoLi(string idDoBotao)
    {
        var view = Details();
        var i = PosicaoDoBotao(view, idDoBotao);

        var abre = view.LastIndexOf("<li", i, StringComparison.Ordinal);
        Assert.True(abre >= 0, $"Não achei o <li> de {idDoBotao}.");
        return view[abre..view.IndexOf('>', abre)];
    }

    private static int PosicaoDoBotao(string view, string idDoBotao)
    {
        var i = view.IndexOf($"id=\"{idDoBotao}\"", StringComparison.Ordinal);
        Assert.True(i >= 0, $"Não achei o botão {idDoBotao}.");
        return i;
    }

    private static string PainelDoAdmin()
    {
        var view = Details();
        var inicio = view.IndexOf("id=\"admin\" role=\"tabpanel\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "Não achei o painel Gerenciar Torneio.");

        var fim = view.IndexOf("role=\"tabpanel\"", inicio + 30, StringComparison.Ordinal);
        Assert.True(fim > inicio, "Não achei o fim do painel de gestão.");
        return view[inicio..fim];
    }

    private static string Details() =>
        File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "Views", "Torneios", "Details.cshtml"));

    private static string CaminhoDoCss() =>
        Path.Combine(RaizDoRepo(), "Padelizou", "wwwroot", "css", "site.css");

    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "wwwroot", "css", "site.css")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return dir!.FullName;
    }
}
