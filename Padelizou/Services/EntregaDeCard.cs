using Microsoft.AspNetCore.Mvc;

namespace Padelizou.Services;

// COMO UM CARD SAI NA RESPOSTA — o cabeçalho de cache e o nome do arquivo.
//
// Nasceu extraído de `CartoesController.Png` (10/09/2026) no dia em que o card da lista de
// jogos passou a morar no TorneiosController (ele é desenhado da lista que só aquele controller
// monta). Duas cópias da política de cache divergiriam na primeira mudança — e o cache aqui
// não é otimização, é requisito: é dele que a prévia do link no WhatsApp vive.
public static class EntregaDeCard
{
    // ⚠️ O PEDIDO DE DOWNLOAD, MARCADO NA PRÓPRIA URL (12/09/2026).
    //
    // 🗣️ Felipe: *"o baixar foto fica travado numa pagina de pre visualizacao"*. O botão "Baixar"
    // apontava pra MESMA URL da prévia e contava com o atributo `download` do `<a>` — que é uma
    // DICA, não uma ordem: WebView de app, navegador de dentro do Instagram/WhatsApp e PWA em
    // modo `standalone` ignoram e NAVEGAM. Como a resposta é `inline`, o que aparece é o PNG cru
    // no lugar da tela, sem barra de endereço e sem botão de voltar.
    //
    // O parâmetro existe em vez de uma troca seca porque o `inline` tem dois usuários legítimos
    // que o `attachment` quebraria: a meta `og:image` (a prévia do link no WhatsApp) e o `<img>`
    // da própria tela. São a mesma arte, com destinos diferentes — então quem escolhe é a URL.
    public const string Download = "baixar";

    // O endereço da mesma arte, pedindo download. Mora ao lado do cabeçalho que ele aciona: a
    // view que montasse o `?baixar=1` na mão seria a segunda régua, e a primeira a divergir.
    public static string LinkParaBaixar(string? url) =>
        url == null ? "" : url + (url.Contains('?') ? '&' : '?') + Download + "=1";

    // Uma hora. Curto o bastante pra acompanhar a grade que muda, longo o bastante pra um
    // grupo de WhatsApp inteiro abrir a mesma prévia sem redesenhar.
    public const int SegundosDeCache = 3600;

    // ⚠️ `publico: false` PRA CARD QUE EXIGE LOGIN. `Cache-Control: public` autoriza qualquer
    // cache no caminho (proxy, CDN, navegador compartilhado) a guardar a resposta e devolvê-la
    // a OUTRA pessoa — o que num card de torneio é o objetivo e num card de grupo privado é
    // vazamento. Quem passa `false` ganha `private`: o cache continua existindo, só que dentro
    // do navegador de quem pediu.
    public static FileContentResult Png(HttpResponse resposta, byte[] bytes, string nomeDoArquivo, bool publico = true)
    {
        resposta.Headers.CacheControl = $"{(publico ? "public" : "private")}, max-age={SegundosDeCache}";

        // `inline` POR PADRÃO: a imagem precisa ABRIR no navegador (é assim que a prévia do link
        // funciona e é assim que a pessoa segura o dedo pra salvar no celular). Quem pediu
        // download — o botão "Baixar", que acrescenta `?baixar=1` pelo `LinkParaBaixar` — leva
        // `attachment`, o único cabeçalho que nenhum navegador trata como sugestão.
        //
        // ⚠️ Lê a pergunta do REQUEST, e não de um parâmetro de ação, porque são onze telas de
        // arte em dois controllers: um parâmetro a mais em cada uma seria onze chances de
        // esquecer, e o esquecimento não falha — ele volta a abrir a imagem.
        var anexo = resposta.HttpContext.Request.Query.ContainsKey(Download);
        resposta.Headers.ContentDisposition = $"{(anexo ? "attachment" : "inline")}; filename=\"{nomeDoArquivo}\"";
        return new FileContentResult(bytes, "image/png");
    }
}
