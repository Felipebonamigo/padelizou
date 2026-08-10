using System.Globalization;
using System.Text;

namespace Padelizou.Services;

// O estado do jogador é TEXTO LIVRE, e o banco de produção mostra o que isso vira na prática
// (10/08/2026, 172 contas ativas):
//
//     RS  87 · (em branco) 44 · Rs  32 · rs  6 · "Rio Grande do Sul (RS)" 1 · Sp 1 · DF 1
//
// ⚠️ Comparar `Estado == "RS"` na lata deixaria 39 pessoas DO RIO GRANDE DO SUL de fora — as
// que escreveram "Rs", "rs" ou o nome por extenso. Numa mira por estado isso não daria erro
// nenhum: elas simplesmente parariam de saber dos torneios da própria cidade.
//
// Por isso todo lado da comparação passa por aqui antes.
public static class UnidadeFederativa
{
    private static readonly Dictionary<string, string> PorNome = new(StringComparer.Ordinal)
    {
        ["ACRE"] = "AC", ["ALAGOAS"] = "AL", ["AMAPA"] = "AP", ["AMAZONAS"] = "AM",
        ["BAHIA"] = "BA", ["CEARA"] = "CE", ["DISTRITO FEDERAL"] = "DF",
        ["ESPIRITO SANTO"] = "ES", ["GOIAS"] = "GO", ["MARANHAO"] = "MA",
        ["MATO GROSSO"] = "MT", ["MATO GROSSO DO SUL"] = "MS", ["MINAS GERAIS"] = "MG",
        ["PARA"] = "PA", ["PARAIBA"] = "PB", ["PARANA"] = "PR", ["PERNAMBUCO"] = "PE",
        ["PIAUI"] = "PI", ["RIO DE JANEIRO"] = "RJ", ["RIO GRANDE DO NORTE"] = "RN",
        ["RIO GRANDE DO SUL"] = "RS", ["RONDONIA"] = "RO", ["RORAIMA"] = "RR",
        ["SANTA CATARINA"] = "SC", ["SAO PAULO"] = "SP", ["SERGIPE"] = "SE",
        ["TOCANTINS"] = "TO",
    };

    private static readonly HashSet<string> Siglas = new(PorNome.Values, StringComparer.Ordinal);

    // Devolve a sigla de duas letras, ou null quando não dá pra saber. **null significa "não
    // sei", nunca "nenhum estado"** — e quem chama tem que tratar os dois do mesmo jeito:
    // na dúvida, a pessoa continua recebendo.
    public static string? Normalizar(string? escrito)
    {
        if (string.IsNullOrWhiteSpace(escrito)) return null;

        var limpo = SemAcento(escrito.Trim().ToUpperInvariant());

        // "RS"
        if (Siglas.Contains(limpo)) return limpo;

        // "Rio Grande do Sul"
        if (PorNome.TryGetValue(limpo, out var pelaExtenso)) return pelaExtenso;

        // "Rio Grande do Sul (RS)" — a sigla entre parênteses ganha do resto do texto, porque
        // é o que a pessoa quis dizer quando escreveu as duas coisas.
        var abre = limpo.LastIndexOf('(');
        var fecha = limpo.LastIndexOf(')');
        if (abre >= 0 && fecha == abre + 3)
        {
            var dentro = limpo.Substring(abre + 1, 2);
            if (Siglas.Contains(dentro)) return dentro;
        }

        // "RIO GRANDE DO SUL - RS", "BRASIL / RS": última palavra de 2 letras que seja UF.
        foreach (var pedaco in limpo.Split(' ', ',', '-', '/', '(', ')'))
            if (pedaco.Length == 2 && Siglas.Contains(pedaco)) return pedaco;

        return null;
    }

    // Duas pessoas estão no mesmo estado? ⚠️ `null` de um lado responde **true**: quem não
    // disse onde mora não pode ser cortado por causa de um campo que nunca foi obrigatório.
    public static bool Combina(string? umLado, string? outroLado)
    {
        var a = Normalizar(umLado);
        var b = Normalizar(outroLado);
        return a == null || b == null || a == b;
    }

    private static string SemAcento(string texto)
    {
        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);

        foreach (var c in decomposto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
