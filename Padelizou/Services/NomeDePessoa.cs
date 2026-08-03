namespace Padelizou.Services;

// "Parece um nome de gente?" Existe porque não parecia: em 03/08/2026 entrou no torneio real
// um jogador chamado **"."** — alguém que não tinha os dados do parceiro e digitou um ponto
// pra passar do campo obrigatório. O `required` do HTML aceita qualquer caractere, e o
// servidor não olhava.
//
// A régua é de propósito FROUXA. Nome de gente é bagunçado — tem sobrenome de uma letra,
// partícula, apóstrofo, hífen, gente conhecida só pelo primeiro nome — e recusar um nome de
// verdade na hora da inscrição é pior do que deixar passar um esquisito. Então só se barra o
// que claramente não é nome nenhum.
public static class NomeDePessoa
{
    private const int MinimoDeLetras = 2;

    public static bool Parece(string? nome) => Problema(nome, "O nome") == null;

    // Devolve a mensagem do problema, ou null quando passa. `comoSeChama` entra na frase
    // ("O nome do jogador 1", "O nome do parceiro") pra a pessoa saber QUAL campo corrigir
    // num formulário que tem dois.
    public static string? Problema(string? nome, string comoSeChama)
    {
        var limpo = (nome ?? "").Trim();

        if (limpo.Length == 0)
            return $"{comoSeChama} está em branco.";

        // Duas letras é o piso: derruba ".", "-", "x" e afins sem incomodar quem tem nome
        // curto de verdade.
        if (limpo.Count(char.IsLetter) < MinimoDeLetras)
            return $"{comoSeChama} precisa ser um nome de verdade — só \"{limpo}\" não dá pra saber quem é.";

        // Dígito em nome não é nome: é CPF colado no campo errado, ou alguém enrolando.
        if (limpo.Any(char.IsDigit))
            return $"{comoSeChama} não pode ter números.";

        return null;
    }

    // Espaço repetido do meio some e as pontas ficam limpas: "  João   da  Silva " vira
    // "João da Silva". Sem isso o mesmo nome entra de dois jeitos e vira duas pessoas nas
    // listas.
    public static string Arrumar(string? nome) =>
        string.Join(' ', (nome ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
