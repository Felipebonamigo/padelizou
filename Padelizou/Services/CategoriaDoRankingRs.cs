using System.Globalization;
using System.Text;

namespace Padelizou.Services;

// De-para entre a categoria do Padelizou (texto livre, escrito pelo organizador em cada
// torneio) e a categoria do Ranking RS (17 ids fixos).
//
// Por que id e não nome: a API aceita os dois, mas o nome dela tem "ª" e acento — mandar texto
// por HTTP com acento é onde nasce o bug que só aparece num servidor. Com id não há o que
// codificar errado. A própria documentação deles recomenda o id pelo mesmo motivo.
//
// ⚠️ SEXO É OBRIGATÓRIO PRA CASAR, e é aqui que mora o cuidado principal deste arquivo.
// "6ª Masculina" casa; "6ª Categoria" NÃO casa e fica sem validação de propósito. Testei
// contra a API: ela NÃO confere sexo — mandando um homem contra "6ª Feminina" ela responde
// APROVADO numa boa. Ou seja, se a gente adivinhasse o sexo errado, a resposta viria
// tranquila e ERRADA, e ninguém veria. Não casar é o resultado honesto.
public static class CategoriaDoRankingRs
{
    public record Item(int Id, string Nome);

    // Os 17 do GET /api/v1/categorias?esporte=PADEL, conferidos contra a API em 06/08/2026.
    // Lista fixa no código porque ela decide REGRA (quem pode se inscrever): se a API mudar um
    // id de baixo do nosso pé, o certo é a inscrição parar de casar — e não passar a validar
    // contra outra categoria calada.
    public static readonly IReadOnlyList<Item> Catalogo = new List<Item>
    {
        new(100, "Open Masculino"), new(101, "Open Feminino"),
        new(102, "1ª Masculina"),   new(103, "1ª Feminina"),
        new(104, "2ª Masculina"),   new(105, "2ª Feminina"),
        new(106, "3ª Masculina"),   new(107, "3ª Feminina"),
        new(108, "4ª Masculina"),   new(109, "4ª Feminina"),
        new(110, "5ª Masculina"),   new(111, "5ª Feminina"),
        new(112, "6ª Masculina"),   new(113, "6ª Feminina"),
        new(114, "Mista A"),        new(115, "Mista B"),        new(116, "Mista C"),
    };

    public static string? NomeDoId(int id) =>
        Catalogo.FirstOrDefault(c => c.Id == id)?.Nome;

    public static bool IdExiste(int id) => Catalogo.Any(c => c.Id == id);

    // Tenta adivinhar o id do Ranking RS pelo nome que o organizador escreveu. null = não deu
    // pra ter certeza, e aí a categoria simplesmente não é validada.
    //
    // Serve de SUGESTÃO na tela do organizador, não de verdade final: ele confirma ou corrige.
    // Adivinhar e aplicar calado é como se erra bonito aqui.
    public static int? Adivinhar(string? nomeDaCategoria)
    {
        string n = Normalizar(nomeDaCategoria);
        if (n.Length == 0) return null;

        // Mista vem antes do resto: "Mista A" não tem número nem sexo, e o ranking só tem
        // três faixas (A, B, C). Sem a letra não dá pra saber qual, então não casa.
        if (n.Contains("mist"))
        {
            if (LetraSolta(n, "a")) return 114;
            if (LetraSolta(n, "b")) return 115;
            if (LetraSolta(n, "c")) return 116;
            return null;
        }

        bool feminina = n.Contains("femin");
        bool masculina = n.Contains("masculin");
        if (feminina == masculina) return null; // sem sexo escrito (ou os dois) — não casa

        // "Open" não tem número; o resto vem de 1 a 6. A 7ª e "Iniciantes" do Padelizou não
        // existem no Ranking RS — quem joga lá embaixo não pontua, então não há o que validar.
        int? tier = n.Contains("open") ? 0
            : n.Contains("1") ? 1 : n.Contains("2") ? 2 : n.Contains("3") ? 3
            : n.Contains("4") ? 4 : n.Contains("5") ? 5 : n.Contains("6") ? 6
            : null;
        if (tier == null) return null;

        // Open = 100/101, 1ª = 102/103, ... 6ª = 112/113. Masculina é par, feminina é ímpar.
        return 100 + tier.Value * 2 + (feminina ? 1 : 0);
    }

    // A letra da mista aparece solta ("Mista A"), então procurar a letra dentro da palavra
    // daria falso positivo em "mista" (tem "a"). Só vale letra isolada.
    private static bool LetraSolta(string normalizado, string letra) =>
        System.Text.RegularExpressions.Regex.IsMatch(normalizado, $@"\b{letra}\b");

    // Sem acento, minúsculo: o organizador escreve "6ª Masculina", "6a masculino", "6ª MASC".
    private static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "";
        var semAcento = new StringBuilder();
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                semAcento.Append(c);
        }
        return semAcento.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant().Trim();
    }
}
