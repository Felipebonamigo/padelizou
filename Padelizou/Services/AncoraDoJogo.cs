using System.Globalization;
using System.Text;

namespace Padelizou.Services;

// O endereço de um jogo DENTRO da página, pra que "Vencedor Primeira Rodada 1" seja um link
// que leva até o jogo citado.
//
// A lista de um torneio grande passa de 80 linhas. Sem o link, ler "Vencedor Primeira Rodada
// 1" obrigava a rolar a página inteira caçando qual é a Primeira Rodada 1 — e o número, que
// existe justamente pra tornar a referência rastreável, só resolvia metade do problema.
//
// ⚠️ O mesmo id tem que sair dos DOIS lados: da linha do jogo (que pode ser real ou projetado)
// e do link que aponta pra ela. Por isso mora aqui, e não numa interpolação repetida em duas
// views — bastaria uma delas mudar de formato pra todo link virar um clique que não vai a
// lugar nenhum, silenciosamente.
public static class AncoraDoJogo
{
    public static string Id(string? categoria, string? fase, int numero) =>
        $"jogo-{Pedaco(categoria)}-{Pedaco(fase)}-{numero}";

    // Só a-z e 0-9: id de HTML vai em href="#..." e em querySelector, e os dois ficam frágeis
    // com caractere fora do ASCII.
    //
    // ⚠️ `char.IsLetterOrDigit` NÃO basta, e um teste pegou isso: o "ª" de "6ª Categoria" é
    // letra pra Unicode (categoria Lo) e a normalização não o decompõe, então ele passava
    // inteiro pro id. Daí o filtro ser por faixa explícita.
    private static string Pedaco(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "x";

        var semAcento = texto.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark);

        var sb = new StringBuilder();
        foreach (var c in semAcento)
        {
            var minusculo = char.ToLowerInvariant(c);
            if (minusculo is (>= 'a' and <= 'z') or (>= '0' and <= '9')) sb.Append(minusculo);
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }

        return sb.ToString().Trim('-') is { Length: > 0 } limpo ? limpo : "x";
    }
}
