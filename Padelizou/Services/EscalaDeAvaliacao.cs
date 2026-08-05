namespace Padelizou.Services;

// As duas réguas da planilha do professor, num lugar só.
//
// Execução é COMO o golpe sai; acertos é QUANTO entra. São independentes de propósito: dá pra
// executar lindo e errar muito, e dá pra acertar tudo empurrando a bola. O professor avalia o
// que trabalhou na aula, então as duas são opcionais.
public static class EscalaDeAvaliacao
{
    // Da pior pra melhor — a ordem importa pra dizer se o aluno subiu ou desceu.
    public static readonly IReadOnlyList<string> Execucoes = new[] { "D", "C", "B", "A" };

    // O que cada letra quer dizer, na palavra do professor. Sem isto, "C" não significa nada
    // pro aluno que abre a ficha.
    public static string DescricaoDaExecucao(string? letra) => (letra ?? "").Trim().ToUpperInvariant() switch
    {
        "D" => "Longa e lenta, sem direção",
        "C" => "Longa e lenta, com direção",
        "B" => "Técnica",
        "A" => "Plástica",
        _ => "",
    };

    public static bool ExecucaoValida(string? letra) =>
        letra != null && Execucoes.Contains(letra.Trim().ToUpperInvariant());

    // Posição na régua (0 = D, 3 = A); -1 quando não é nota. É com isto que se compara duas
    // fichas sem espalhar "A é melhor que B" por todo lado.
    public static int PesoDaExecucao(string? letra) =>
        ExecucaoValida(letra) ? Execucoes.ToList().IndexOf(letra!.Trim().ToUpperInvariant()) : -1;

    public static bool AcertosValidos(int? acertos) => acertos is >= 1 and <= 10;

    // A faixa de acertos, como a planilha lê: o número sozinho não diz o que significa.
    public static string FaixaDeAcertos(int? acertos) => acertos switch
    {
        >= 8 => "Aplicável em jogo",
        >= 5 => "Bom",
        >= 3 => "Médio",
        >= 1 => "Baixo",
        _ => "",
    };

    // Linha em branco não vira nota: o professor passa por 146 fundamentos e marca meia dúzia.
    // Gravar o resto como "sem nota" encheria o banco e, pior, faria a evolução comparar nada
    // com nada e anunciar mudança onde não houve.
    public static bool TemNota(string? execucao, int? acertos) =>
        ExecucaoValida(execucao) || AcertosValidos(acertos);

    // Normaliza o que veio do formulário: caixa alta, sem espaço, e lixo vira null.
    public static string? LimparExecucao(string? letra) =>
        ExecucaoValida(letra) ? letra!.Trim().ToUpperInvariant() : null;

    public static int? LimparAcertos(int? acertos) => AcertosValidos(acertos) ? acertos : null;
}
