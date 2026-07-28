using System.Globalization;
using System.Text;

namespace Padelizou.Services;

// "Gravatai", "Gravataí" e "gravatai" são a mesma cidade. Elas apareceram juntas no filtro do
// ranking porque cada pessoa digita a cidade do seu jeito no cadastro, e a comparação era por
// igualdade exata de texto.
//
// A saída não é proibir de digitar — é comparar do jeito que uma PESSOA compara: sem acento, sem
// caixa, sem espaço sobrando. Quem digita "gravatai" cai na "Gravataí" que já existe.
public static class NomeDeCidade
{
    // Chave de comparação: minúsculo, sem acento, espaços colapsados. Nunca vai pra tela nem
    // pro banco — serve só pra responder "isto é a mesma cidade que aquilo?".
    public static string Chave(string? nome)
    {
        var limpo = Arrumar(nome).ToLowerInvariant();

        // FormD separa a letra do acento; aí basta descartar as marcas.
        var decomposto = limpo.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);

        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    // Como o nome vai ser GUARDADO e mostrado: sem espaço sobrando e sem espaço duplicado no meio.
    // O acento e a caixa do que a pessoa escreveu são preservados — "Gravataí" digitado certo não
    // vira "Gravatai", e "São Paulo" não vira "São paulo".
    public static string Arrumar(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return string.Empty;

        var partes = nome.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', partes);
    }

    // Duas grafias da mesma cidade?
    public static bool Mesma(string? a, string? b)
    {
        var chaveA = Chave(a);
        return chaveA.Length > 0 && chaveA == Chave(b);
    }

    // UF sempre em maiúscula, e só se realmente parecer uma UF — "rs" vira "RS", mas lixo não
    // vira UF só porque tem duas letras... isso quem valida é a tela; aqui só padronizamos.
    public static string? ArrumarEstado(string? uf)
    {
        var limpo = Arrumar(uf);
        return limpo.Length == 0 ? null : limpo.ToUpperInvariant();
    }
}
