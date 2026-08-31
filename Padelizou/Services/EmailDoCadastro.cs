namespace Padelizou.Services;

// O E-MAIL DIGITADO ERRADO NÃO ENTRA MAIS CALADO (28/08/2026).
//
// 🐛 O CASO QUE FEZ ISTO EXISTIR: o Pedro se cadastrou com `pedrojunior_1978@hotmial.com` —
// "hotmial", não "hotmail". O `<input type="email">` da tela achou ótimo, porque a SINTAXE
// está perfeita: tem arroba, tem domínio, tem ponto. E no servidor não havia validação
// nenhuma — nem a de sintaxe.
//
// O estrago é mudo e só aparece no pior momento: o e-mail de confirmação de aula não chega, e
// "Esqueci minha senha" — que é a ÚNICA saída de quem esqueceu — manda o link pra um domínio
// que não existe. A pessoa fica trancada fora da própria conta sem entender por quê. Foi
// preciso um admin abrir a ficha dela pra alguém descobrir.
//
// ── POR QUE O TYPO É RECUSADO, E NÃO SÓ AVISADO ──────────────────────────────────────────
// Um domínio a UMA letra de gmail/hotmail/outlook não é caixa postal de ninguém: é engano de
// dedo, ou domínio de phishing que vive justamente de colher esse engano. Nos dois casos,
// mandar link de redefinição de senha pra lá é pior que recusar o cadastro.
//
// E a recusa não deixa a pessoa presa: a mensagem DIZ o endereço certo, montado, e ela
// conserta numa edição. Avisar sem barrar seria o mesmo que hoje — o Pedro passaria de novo.
//
// ⚠️ A DISTÂNCIA SÓ É MEDIDA CONTRA UMA LISTA CURTA de domínios enormes, e nunca contra o
// universo: `padelizou.com.br`, `empresa-pequena.com.br` e qualquer domínio de verdade têm que
// passar. Recusar e-mail BOM é estrago maior que o typo que isto conserta — por isso a lista
// tem só os que quase todo brasileiro usa, e a comparação é sobre o domínio inteiro.
public static class EmailDoCadastro
{
    // Os domínios que concentram quase todo cadastro daqui. Só contra ESTES a distância é
    // medida — a lista é curta de propósito: cada nome novo aqui é uma chance a mais de
    // recusar o e-mail de alguém que existe.
    private static readonly string[] Grandes =
    {
        "gmail.com", "hotmail.com", "outlook.com", "yahoo.com", "yahoo.com.br",
        "icloud.com", "live.com", "bol.com.br", "uol.com.br", "terra.com.br",
    };

    // O motivo pra não gravar, ou null quando o endereço está bom.
    public static string? Problema(string? email)
    {
        var limpo = (email ?? "").Trim();

        if (limpo.Length == 0) return "Informe o e-mail.";

        if (!SintaxeOk(limpo))
            return "Esse e-mail não parece completo. Confira se tem @ e o domínio (ex.: nome@gmail.com).";

        if (Sugestao(limpo) is { } certo)
            return $"Confira o e-mail: \"{limpo}\" não existe. Você quis dizer {certo}?";

        return null;
    }

    // O endereço certo, quando o domínio digitado está a UMA letra de um dos grandes. Null
    // quando não há nada a sugerir — que é o caminho de todo e-mail bom.
    public static string? Sugestao(string? email)
    {
        var limpo = (email ?? "").Trim();
        var arroba = limpo.LastIndexOf('@');
        if (arroba <= 0 || arroba == limpo.Length - 1) return null;

        var usuario = limpo[..arroba];
        var dominio = limpo[(arroba + 1)..].ToLowerInvariant();

        // Escrito certo não tem o que sugerir. A conferência vem ANTES da distância porque
        // "gmail.com" tem distância 0 de si mesmo — e uma régua mal escrita se acusaria.
        if (Grandes.Contains(dominio)) return null;

        foreach (var grande in Grandes)
        {
            if (Distancia(dominio, grande) == 1) return $"{usuario}@{grande}";
        }

        return null;
    }

    // Sintaxe suficiente pra existir uma caixa postal: um arroba só, algo antes, e um domínio
    // com ponto depois. Não é a RFC inteira de propósito — a RFC aceita coisas que servidor
    // nenhum entrega, e recusar e-mail bom é o erro caro aqui.
    private static bool SintaxeOk(string email)
    {
        if (email.Any(char.IsWhiteSpace)) return false;
        if (email.Count(c => c == '@') != 1) return false;

        var partes = email.Split('@');
        var usuario = partes[0];
        var dominio = partes[1];

        if (usuario.Length == 0 || dominio.Length < 3) return false;
        if (!dominio.Contains('.')) return false;
        if (dominio.StartsWith('.') || dominio.EndsWith('.') || dominio.Contains("..")) return false;

        // Precisa sobrar algo depois do último ponto (o ".com", o ".br").
        return dominio[(dominio.LastIndexOf('.') + 1)..].Length >= 2;
    }

    // ⚠️ DAMERAU-LEVENSHTEIN, e não o Levenshtein comum — a diferença é o caso do Pedro.
    //
    // "hotmial" ↔ "hotmail" é uma TRANSPOSIÇÃO: duas letras vizinhas trocadas de lugar. O
    // Levenshtein puro cobra 2 por isso (apaga uma, insere outra), então uma régua de
    // "distância 1" deixaria passar exatamente o erro mais comum de quem digita rápido —
    // "gmial", "hotmial", "yahooo". Aqui a troca de vizinhas custa 1, que é o que ela é.
    //
    // Variante OSA (optimal string alignment): basta pra typo de dedo e é a mais simples de
    // ler. Domínio tem 10-15 letras, então a matriz inteira não custa nada — o corte é só o
    // de tamanho, que descarta de cara quem não tem chance.
    private static int Distancia(string a, string b)
    {
        // Inserção/remoção mudam o tamanho em 1; substituição e transposição não mudam. Mais
        // de uma letra de diferença já é distância ≥ 2.
        if (Math.Abs(a.Length - b.Length) > 1) return 2;

        var d = new int[a.Length + 1, b.Length + 1];

        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                var custo = a[i - 1] == b[j - 1] ? 0 : 1;

                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1,      // remoção
                             d[i, j - 1] + 1),     // inserção
                    d[i - 1, j - 1] + custo);      // substituição

                // A transposição: as duas últimas letras trocadas de lugar custam 1.
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
            }
        }

        return Math.Min(d[a.Length, b.Length], 2);
    }
}
