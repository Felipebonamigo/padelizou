using Padelizou.Models;

namespace Padelizou.Services;

// AS FOTOS DO TORNEIO.
//
// Toda vez que um torneio acaba, as fotos aparecem em algum lugar — o álbum do fotógrafo no
// Google Fotos, uma pasta no Drive, o perfil do clube no Instagram — e o link disso circula
// solto no grupo do WhatsApp. Quem entrou no grupo depois, ou quem saiu, não acha mais.
//
// Aqui o link vira campo do torneio e vira botão na página, ao lado da chave e da
// classificação: o lugar onde a pessoa já vai olhar o resultado é o lugar onde ela procura a
// foto dela.
//
// ⚠️ ISTO NÃO HOSPEDA FOTO NENHUMA — de propósito (decisão do Felipe, 11/08/2026). Galeria
// própria (upload em massa, reconhecimento facial, venda) é assunto de outro dia; o custo que
// ela traz não é a tela, é o disco: o backup das 4h faz um `tar` COMPLETO de
// /opt/padelizou-shared todo dia e guarda 14 — um torneio de 3.000 fotos viraria ~12 GB de
// tarball. Enquanto isso não for resolvido, o que guardamos é o endereço, não a imagem.
//
// ⚠️ E o link vira `href` DENTRO da nossa página. É por isso que isto é um serviço e não um
// campo de texto solto — a mesma razão de GrupoDoTorneioNoWhatsApp existir, com UMA diferença
// que muda a trava inteira:
//
//   lá o host é conhecido (chat.whatsapp.com) e a conferência é por igualdade;
//   AQUI O HOST É QUALQUER UM — Drive, Google Fotos, Instagram, Dropbox, o site do fotógrafo.
//
// Não dá pra ter lista de permitidos sem recusar álbum legítimo. Então quem tranca aqui é o
// ESQUEMA: só http e https viram botão. Sem isso, "javascript:..." digitado neste campo
// rodaria na sessão de quem clicasse, dentro da página do torneio.
public static class FotosDoTorneio
{
    // Teto de tamanho. A tela já limita em 300, mas o POST vem do navegador e o navegador vem
    // de quem quiser: sem isto, o campo aceita um texto de megabytes que só descobrimos no
    // backup. Endereço de álbum de verdade não chega perto disso.
    public const int MaximoDeCaracteres = 500;

    // O que o organizador digitou, pronto pra guardar. Não julga se serve — quem responde isso
    // é EhEnderecoDeAlbum.
    public static string? Normalizar(string? link)
    {
        var texto = link?.Trim();
        if (string.IsNullOrWhiteSpace(texto)) return null;

        // Colado do navegador o link vem completo, mas digitado à mão vem "fotos.app.goo.gl/XX".
        // Sem esquema não é endereço absoluto, e a conferência abaixo recusaria um álbum bom.
        //
        // ⚠️ Ao contrário do link do grupo, http NÃO é promovido a https aqui. Lá o destino é
        // um só e sabidamente atende em https; aqui o destino é o site que o fotógrafo tiver,
        // e forçar https num servidor que só fala http quebraria um link que funcionava.
        if (!texto.Contains("://", StringComparison.Ordinal))
        {
            texto = "https://" + texto;
        }

        return texto;
    }

    // É um endereço de página que dá pra abrir?
    public static bool EhEnderecoDeAlbum(string? link)
    {
        var endereco = Normalizar(link);
        if (endereco == null || endereco.Length > MaximoDeCaracteres) return false;

        return Uri.TryCreate(endereco, UriKind.Absolute, out var uri)
            // A trava que importa. `javascript:`, `data:` e `file:` também são URIs absolutas
            // válidas — o que as separa de um álbum é exatamente esta linha.
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            // "https://" sozinho passa no TryCreate e não leva a lugar nenhum.
            && uri.Host.Length > 0;
    }

    // A recusa que a tela mostra, ou null quando está tudo bem. Campo em branco não é problema:
    // torneio sem álbum é o caso normal, e aí simplesmente não há botão.
    public static string? ProblemaCom(string? link)
    {
        if (Normalizar(link) == null) return null;
        if (EhEnderecoDeAlbum(link)) return null;

        return "O link das fotos precisa ser um endereço de página — como o álbum do Google "
             + "Fotos, uma pasta do Drive ou o perfil do Instagram do fotógrafo. Abra o álbum "
             + "no navegador e copie o endereço da barra.";
    }

    // A ÚNICA porta por onde o link chega a uma tela.
    //
    // ⚠️ Diferente do grupo do WhatsApp, aqui NÃO há portão de quem pode ver: álbum de torneio
    // é divulgação, e a página do torneio é pública justamente pra isso. Quem não jogou também
    // vê — é assim que a foto puxa gente pro próximo torneio.
    //
    // O que continua valendo é a segunda tranca: o que está GUARDADO é conferido de novo na
    // hora de mostrar, e não só na hora de salvar. Linha antiga, importação, correção na mão no
    // banco — nada disso passou pela validação da tela, e um endereço estranho aqui viraria um
    // botão do Padelizou levando pra fora.
    public static string? LinkPara(Torneio torneio) =>
        EhEnderecoDeAlbum(torneio.LinkDasFotos) ? Normalizar(torneio.LinkDasFotos) : null;

    // Este torneio já tem álbum? Pergunta do organizador, não do jogador.
    public static bool Configurado(Torneio torneio) => EhEnderecoDeAlbum(torneio.LinkDasFotos);
}
