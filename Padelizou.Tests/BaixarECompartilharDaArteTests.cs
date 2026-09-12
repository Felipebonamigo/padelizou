using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Services;
using System.Text.RegularExpressions;
using Xunit;

namespace Padelizou.Tests;

// 12/09/2026 — OS DOIS BOTÕES DEBAIXO DA ARTE: "Baixar" e "Compartilhar".
//
// 🗣️ Felipe, com o print da tela "Tudo numa imagem só" no celular: *"O botão compartilhar nao
// esta funcionando. E o baixar foto fica travado numa pagina de pre visualizacao depois q envia
// a foto"*.
//
// 🕳️ DOIS DEFEITOS SOMADOS, e os dois calados:
//
// 1. O `CompartilharJogos.cshtml` tinha um `return;` no meio do corpo (o atalho que evitava o
//    `else` do formato "uma por dia"). Em Razor, `@section` é uma CHAMADA — `DefineSection(...)`
//    — executada na ordem em que aparece no arquivo. Um `return` antes dela simplesmente nunca
//    a executa, e o `@await RenderSectionAsync("Scripts", required: false)` do layout responde
//    com silêncio em vez de erro. Resultado: na tela da imagem única, o `compartilhar-card.js`
//    NUNCA CARREGAVA — e o botão "Compartilhar" (que é um `<button>` sem `href`, todo o
//    comportamento no script) não fazia absolutamente nada. Junto com ele morria o
//    `compartilhar-texto.js`, que é quem faz o "Copiar o texto" da mesma página.
//
// 2. O endpoint da arte responde `Content-Disposition: inline` — de propósito, porque é assim
//    que a prévia do link no WhatsApp vive e é assim que a pessoa segura o dedo na imagem pra
//    salvar. Só que o botão "Baixar" apontava pra ESSA MESMA URL, contando com o atributo
//    `download` do `<a>` pra virar download. Esse atributo é uma DICA: WebView de app, navegador
//    dentro do Instagram/WhatsApp e PWA em modo `standalone` ignoram — e aí a "página de
//    pré-visualização" do relato é literalmente o PNG cru aberto no lugar da tela, sem barra de
//    endereço e sem volta. `Content-Disposition: attachment` não é dica: nenhum navegador o
//    ignora.
public class BaixarECompartilharDaArteTests
{
    // ── 1. O script chega na tela ──────────────────────────────────────────────────────────

    // O GATE MECÂNICO do defeito 1, e vale pra TODA view, não só pra esta: `@section` que vem
    // depois de um `return;` de Razor é seção que não existe — e o layout não reclama.
    //
    // ⚠️ O `<script>` da própria página é recortado antes da busca: `return;` de JavaScript é
    // outra língua, e é o que as telas grandes (Details, Create) têm às dezenas.
    [Fact]
    public void Nenhuma_view_define_secao_depois_de_um_return_de_razor()
    {
        var culpadas = new List<string>();

        foreach (var caminho in Directory.EnumerateFiles(PastaDasViews(), "*.cshtml", SearchOption.AllDirectories))
        {
            // Comentário Razor fora: o `@@section` escapado que um deles cita em prosa não é
            // uma seção, e contá-lo acusaria uma view que não declara nenhuma.
            var fonte = Regex.Replace(File.ReadAllText(caminho), @"@\*.*?\*@", "", RegexOptions.Singleline);

            var secao = Regex.Match(fonte, @"(?<!@)@section\s+\w+");
            if (!secao.Success) continue;

            var corpo = SemJavaScript(fonte[..secao.Index]);
            if (Regex.IsMatch(corpo, @"(^|[;{}\s])return\s*;"))
                culpadas.Add(Path.GetRelativePath(PastaDasViews(), caminho));
        }

        Assert.True(culpadas.Count == 0,
            "Estas views definem @section DEPOIS de um return de Razor — a seção nunca é " +
            "registrada e o script some da página, calado. Conserto: `else` no lugar do " +
            "`return`, ou a seção lá em cima, antes da guarda: " + string.Join(", ", culpadas));
    }

    // ── 2. "Baixar" baixa, em vez de abrir ─────────────────────────────────────────────────

    // Com o pedido de download, a resposta é ANEXO — o cabeçalho que nenhum navegador ignora.
    [Fact]
    public async Task Baixar_entrega_a_arte_como_anexo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);
        controller.HttpContext.Request.QueryString = new QueryString("?" + EntregaDeCard.Download + "=1");

        Assert.IsType<FileContentResult>(await controller.JogosImagem(torneio.Id, Fontes(), tudo: true));

        Assert.StartsWith("attachment;", controller.Response.Headers.ContentDisposition.ToString());
    }

    // E SEM ele continua `inline`: é do `inline` que vivem a prévia do link no WhatsApp (a meta
    // `og:image`) e o `<img>` da própria tela. Trocar o padrão consertaria o botão e quebraria
    // a prévia — que é o motivo de o parâmetro existir em vez de uma troca seca.
    [Fact]
    public async Task Sem_o_pedido_de_download_a_arte_continua_abrindo_na_tela()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, _, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 6);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);

        Assert.IsType<FileContentResult>(await controller.JogosImagem(torneio.Id, Fontes(), tudo: true));

        Assert.StartsWith("inline;", controller.Response.Headers.ContentDisposition.ToString());
    }

    // A régua do link mora num lugar só — quem monta o endereço de download é o `EntregaDeCard`,
    // o mesmo que decide o cabeçalho. Duas cópias (uma na view, outra no serviço) divergiriam na
    // primeira vez que o nome do parâmetro mudasse, e o sintoma seria o botão voltar a abrir a
    // imagem em vez de salvá-la.
    [Theory]
    [InlineData("/Cartoes/JogadorImagem/7", "/Cartoes/JogadorImagem/7?baixar=1")]
    [InlineData("/Torneios/JogosImagem?id=26&tudo=True", "/Torneios/JogosImagem?id=26&tudo=True&baixar=1")]
    public void O_link_de_download_marca_o_pedido_na_propria_url(string url, string esperado)
        => Assert.Equal(esperado, EntregaDeCard.LinkParaBaixar(url));

    // ── 3. Quem usa a régua ────────────────────────────────────────────────────────────────

    // TODO botão "Baixar" de arte passa pelo link de download. Um `<a download="...">` apontando
    // pra URL crua é exatamente o defeito 2 de volta — e ele não falha, ele abre a imagem.
    [Fact]
    public void Todo_botao_de_baixar_arte_pede_o_download_pela_regua()
    {
        var soltos = new List<string>();

        foreach (var caminho in Directory.EnumerateFiles(PastaDasViews(), "*.cshtml", SearchOption.AllDirectories))
            foreach (Match ancora in Regex.Matches(File.ReadAllText(caminho), @"<a[^>]*\sdownload=""[^""]*""[^>]*>", RegexOptions.Singleline))
                if (!ancora.Value.Contains("LinkParaBaixar", StringComparison.Ordinal))
                    soltos.Add(Path.GetRelativePath(PastaDasViews(), caminho));

        Assert.True(soltos.Count == 0,
            "Estes botões de baixar apontam pra URL crua da arte, que responde `inline` — no " +
            "WebView do app e no PWA em modo standalone isso ABRE a imagem em vez de salvar: "
            + string.Join(", ", soltos.Distinct()));
    }

    // O caminho de reserva do JS (quando o compartilhamento nativo não existe ou é recusado)
    // baixa pela MESMA régua. Ele monta um `<a download>` na mão — sem o `attachment` do
    // servidor, ele tromba no mesmo WebView que o botão da tela.
    [Fact]
    public void O_download_de_reserva_do_script_pede_o_anexo()
    {
        var js = File.ReadAllText(Path.Combine(PastaDoProjeto(), "wwwroot", "js", "compartilhar-card.js"));

        Assert.Contains(EntregaDeCard.Download + "=1", js);
    }

    // ── Apoio ──────────────────────────────────────────────────────────────────────────────

    // Recorta os blocos `<script>...</script>`: `return;` lá dentro é JavaScript, não Razor.
    private static string SemJavaScript(string fonte) =>
        Regex.Replace(fonte, @"<script\b.*?</script>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static FonteDoCartao Fontes() => new(Path.Combine(PastaDoProjeto(), "wwwroot", "fonts"));

    private static string PastaDasViews() => Path.Combine(PastaDoProjeto(), "Views");

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Não achei a pasta Padelizou/Views a partir de " + AppContext.BaseDirectory);
    }
}
