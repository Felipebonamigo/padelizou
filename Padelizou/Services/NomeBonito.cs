namespace Padelizou.Services;

// Como o nome APARECE na tela. Não mexe no que está gravado.
//
// Na lista de inscritos do "Interno Los Corneteiros" conviviam "ALAN DA SILVEIRA MACHADO" e
// "charls gustavio polese" — cada um digita como quer, e no meio de uma lista isso fica feio.
//
// Só a EXIBIÇÃO muda, de propósito: o nome é dado da pessoa, ela escreveu daquele jeito, e
// reescrever a coluna seria irreversível pra consertar uma questão de aparência. Aqui, se um
// dia a regra estiver errada pra alguém, é só mudar esta classe.
public static class NomeBonito
{
    // Partículas de nome brasileiro ficam minúsculas: "Alan da Silveira Machado", não
    // "Alan Da Silveira Machado". Nunca na primeira posição — ali é o começo do nome.
    private static readonly HashSet<string> Particulas = new(StringComparer.OrdinalIgnoreCase)
    {
        "da", "de", "do", "das", "dos", "e", "di", "du", "del", "della",
        "van", "von", "der", "la", "le", "y",
    };

    public static string Formatar(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return "";

        var palavras = nome.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var saida = new List<string>(palavras.Length);

        for (var i = 0; i < palavras.Length; i++)
        {
            var palavra = palavras[i];

            // "e" e "da" no meio do nome ficam minúsculos; no começo, não — quem se chama
            // "Del Rey" perderia a maiúscula do próprio primeiro nome.
            if (i > 0 && Particulas.Contains(palavra))
            {
                saida.Add(palavra.ToLowerInvariant());
                continue;
            }

            saida.Add(PalavraFormatada(palavra));
        }

        return string.Join(' ', saida);
    }

    // Nome composto por hífen ou apóstrofo é uma palavra só pro espaço, mas duas pra caixa:
    // "ana-maria" tem que virar "Ana-Maria", e "d'avila" vira "D'Avila".
    private static string PalavraFormatada(string palavra)
    {
        if (!Ajustavel(palavra)) return palavra;

        var resultado = new System.Text.StringBuilder(palavra.Length);
        bool comecoDePedaco = true;

        foreach (var c in palavra)
        {
            if (comecoDePedaco)
            {
                resultado.Append(char.ToUpperInvariant(c));
                comecoDePedaco = false;
            }
            else
            {
                resultado.Append(char.ToLowerInvariant(c));
            }

            if (c is '-' or '\'' or '’' or '.') comecoDePedaco = true;
        }

        return resultado.ToString();
    }

    // O ponto mais importante desta classe: só mexe em palavra que está TODA maiúscula ou
    // TODA minúscula. Quem tem maiúscula no meio escreveu assim de propósito — "McDonald",
    // "DiCaprio", "D'Ávila" — e passar o rolo compressor viraria "Mcdonald", estragando
    // justamente o nome de quem se deu ao trabalho de digitar certo.
    private static bool Ajustavel(string palavra)
    {
        var letras = palavra.Where(char.IsLetter).ToList();
        if (letras.Count == 0) return false;

        bool todaMaiuscula = letras.All(char.IsUpper);
        bool todaMinuscula = letras.All(char.IsLower);

        // "Mateus" (só a inicial maiúscula) já está no formato de destino — passar por aqui
        // devolve o mesmo texto, então tratar como ajustável não muda nada e simplifica.
        bool soAInicialMaiuscula = char.IsUpper(letras[0]) && letras.Skip(1).All(char.IsLower);

        return todaMaiuscula || todaMinuscula || soAInicialMaiuscula;
    }
}
