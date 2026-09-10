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

        // `inline` e não `attachment`: a imagem precisa ABRIR no navegador (é assim que a prévia
        // do link funciona e é assim que a pessoa segura o dedo pra salvar no celular). O
        // download forçado fica no botão da tela.
        resposta.Headers.ContentDisposition = $"inline; filename=\"{nomeDoArquivo}\"";
        return new FileContentResult(bytes, "image/png");
    }
}
