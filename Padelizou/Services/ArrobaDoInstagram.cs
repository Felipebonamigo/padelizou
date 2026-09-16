using System.Text.RegularExpressions;

namespace Padelizou.Services;

// O @ DO INSTAGRAM — uma régua, um lugar (14/09/2026).
//
// Nasceu com a arte do jogo pro story, e cobriu um buraco que já existia: o `Perfil.cshtml`
// montava `https://instagram.com/@(Instagram.TrimStart('@'))` à mão, DUAS vezes. O campo é
// texto livre no cadastro, então o que estava guardado ia direto pro `href` — mesma família do
// que o `FotosDoTorneio` tranca no link do álbum, com UMA diferença que aperta a régua:
//
//   lá o host é QUALQUER UM (Drive, Google Fotos, o site do fotógrafo) e só o esquema dá pra
//   conferir; AQUI o destino é UM SÓ, o instagram.com, e o @ tem forma conhecida.
//
// Então aqui não se tranca só o esquema: confere-se a forma inteira, que é a regra do próprio
// Instagram — letras, números, ponto e sublinhado, até 30 caracteres. O que não passa nisso não
// é @ que funciona em lugar nenhum, e é melhor não virar link do que virar link quebrado.
//
// ⚠️ E ISTO VALE PRO QUE JÁ ESTÁ NO BANCO, não só pro que entra agora: o `Jogador.Instagram` é
// de antes desta régua e tem de tudo — com @, sem @, com a URL inteira colada. Todas as formas
// entram e saem iguais. O que estiver guardado e não for @ de verdade simplesmente não vira
// link nem marcação — o botão do perfil que hoje aponta pra um endereço quebrado desaparece,
// e isso é conserto, não perda.
public static class ArrobaDoInstagram
{
    // O teto do Instagram. O campo do formulário guarda 40 (ver Torneio.InstagramDoOrganizador),
    // que é folga pra caber o @ na frente sem estourar a coluna.
    public const int MaximoDeCaracteres = 30;

    // Teto do TEXTO QUE CHEGA, antes de virar @: colado do navegador vem a URL inteira, e sem
    // um teto o campo aceita um texto de megabytes que só descobriríamos no backup.
    public const int MaximoDoTextoDigitado = 200;

    // A forma do @, depois de tirada a URL e o arroba. Minúsculas porque `Normalizar` já
    // rebaixou: @ do Instagram não diferencia maiúscula, e guardar duas grafias do mesmo perfil
    // faria a mesma pessoa aparecer de dois jeitos em duas telas.
    private static readonly Regex Forma = new("^[a-z0-9._]{1,30}$", RegexOptions.Compiled);

    // O que a pessoa digitou, pronto pra guardar — SEM o @ na frente. Nulo = não serve (e
    // campo em branco também é nulo, que é como se apaga).
    public static string? Normalizar(string? texto)
    {
        var t = texto?.Trim();
        if (string.IsNullOrWhiteSpace(t) || t.Length > MaximoDoTextoDigitado) return null;

        if (t.Contains("://", StringComparison.Ordinal))
        {
            // Colado do navegador. Só o instagram.com é aceito: o MESMO texto em outro host
            // não é um perfil, é um endereço que alguém escolheu — e ele viraria `href` numa
            // página nossa.
            if (!Uri.TryCreate(t, UriKind.Absolute, out var uri)) return null;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;
            if (!EhHostDoInstagram(uri.Host)) return null;
            t = uri.AbsolutePath.Trim('/');
        }
        else if (t.Contains(':'))
        {
            // ⚠️ Esquema solto, sem as duas barras: "javascript:alert(1)", "data:text/html,...".
            // São exatamente os que o campo do álbum já recusava, e chegam aqui pela mesma
            // porta — alguém digitando no campo em vez de colando um endereço.
            return null;
        }
        else if (t.Contains('/'))
        {
            // Digitado à mão, sem o https: "instagram.com/er.padel".
            var partes = t.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length < 2 || !EhHostDoInstagram(partes[0])) return null;
            t = partes[1];
        }

        t = t.TrimStart('@').Trim().ToLowerInvariant();
        return Forma.IsMatch(t) ? t : null;
    }

    private static bool EhHostDoInstagram(string host) =>
        host.Equals("instagram.com", StringComparison.OrdinalIgnoreCase)
        || host.Equals("www.instagram.com", StringComparison.OrdinalIgnoreCase);

    // O @ como se escreve na tela e na arte, com UM arroba só. Nulo = não há @ pra mostrar, e
    // quem chama decide o que pôr no lugar (o nome, na arte; nada, no perfil).
    public static string? ParaMostrar(string? guardado) =>
        Normalizar(guardado) is { } arroba ? "@" + arroba : null;

    // O endereço do perfil. Sempre https e sempre no domínio do Instagram — o `href` não sai
    // daqui montado com pedaço de texto que veio do cadastro.
    public static string? Link(string? guardado) =>
        Normalizar(guardado) is { } arroba ? "https://instagram.com/" + arroba : null;

    // "Dá pra guardar isso?" — a mensagem que a tela mostra, ou nulo se está tudo bem.
    //
    // ⚠️ Campo em branco NÃO é problema: é como se apaga o @. Mas texto errado é recusado em vez
    // de normalizado pra nulo, e a diferença importa: sem a recusa, um erro de digitação
    // ("er padel", com espaço) APAGARIA em silêncio o @ que estava certo.
    public static string? ProblemaCom(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        if (texto.Trim().Length > MaximoDoTextoDigitado)
            return $"O @ do Instagram passa de {MaximoDoTextoDigitado} caracteres. Confira o que foi colado no campo.";

        return Normalizar(texto) == null
            ? "Isso não parece um @ do Instagram. Use só letras, números, ponto e sublinhado — "
              + $"por exemplo, er.padel (até {MaximoDeCaracteres} caracteres)."
            : null;
    }
}
