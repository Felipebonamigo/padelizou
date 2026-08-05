namespace Padelizou.Services;

// Pra onde a pessoa vai depois de entrar: de volta pra tela em que ela estava, ou pro início.
//
// O `returnUrl` já existia, mas só chegava preenchido quando alguma tela se lembrava de
// mandar — o botão "Entrar" da barra, que é por onde quase todo mundo entra (e o único
// caminho fácil no celular), não mandava. Resultado: a pessoa estava olhando um torneio,
// tocava em Entrar, e caía no PERFIL, tendo que achar o torneio de novo. No celular isso é
// pior: a tela é pequena, e "achar de novo" é rolar tudo outra vez.
//
// A saída é o cabeçalho Referer em vez de acertar link por link: cobre os que existem hoje e
// os que alguém escrever amanhã sem lembrar do returnUrl.
public static class VoltarDepoisDoLogin
{
    public const string Inicio = "/";

    // Telas que não servem de volta. Cair de novo no login depois de entrar é laço; cair no
    // cadastro ou no portão é mandar quem já entrou pra porta de entrada outra vez.
    private static readonly string[] NaoServemDeDestino =
    {
        "/Auth/Login", "/Auth/Cadastro", "/Auth/EsqueciSenha", "/Auth/RedefinirSenha",
        "/Auth/Logout", "/Auth/AcessoNegado", "/AcessoAntecipado",
    };

    public static bool ServeDeDestino(string? caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho)) return false;

        // Só caminho de dentro do site. "//outro-site.com" e "/\outro-site.com" são absolutos
        // disfarçados de relativos — é o golpe clássico de "entre aqui e você cai lá".
        if (!caminho.StartsWith('/')) return false;
        if (caminho.StartsWith("//") || caminho.StartsWith("/\\")) return false;

        return !NaoServemDeDestino.Any(p => caminho.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    // O que colocar no campo escondido da tela de login: o destino que veio explícito, e na
    // falta dele, de onde a pessoa veio.
    public static string? DeOndeVeio(string? returnUrl, string? referer, string? hostAtual)
    {
        if (ServeDeDestino(returnUrl)) return returnUrl;

        var doReferer = CaminhoDoReferer(referer, hostAtual);
        return ServeDeDestino(doReferer) ? doReferer : null;
    }

    // Onde a pessoa cai depois de entrar de verdade. Sem destino, o INÍCIO — e não o perfil:
    // quem acabou de entrar quer voltar ao que estava fazendo, não ver as próprias estatísticas.
    public static string Destino(string? returnUrl) => ServeDeDestino(returnUrl) ? returnUrl! : Inicio;

    private static string? CaminhoDoReferer(string? referer, string? hostAtual)
    {
        if (string.IsNullOrWhiteSpace(referer) || string.IsNullOrWhiteSpace(hostAtual)) return null;
        if (!Uri.TryCreate(referer, UriKind.Absolute, out var uri)) return null;

        // Host E porta iguais: "de onde veio" quando vem de fora não decide pra onde a pessoa
        // vai. Um site qualquer poderia linkar pro nosso login e escolher o destino.
        return string.Equals(uri.Authority, hostAtual, StringComparison.OrdinalIgnoreCase)
            ? uri.PathAndQuery
            : null;
    }
}
