namespace Padelizou.Services;

// Qual quadra cada jogo ganha, quando o organizador disse que uma categoria tem quadra
// preferida (Models/QuadraDaCategoria).
//
// Sem preferência nenhuma, a grade sempre deu a PRIMEIRA quadra livre do horário — ou seja, a
// melhor quadra do clube era de quem tivesse entrado antes na fila de jogos. Com preferência,
// a escolha passa a ter três degraus, nesta ordem:
//
//   1. uma quadra que ESTA categoria prefere;
//   2. uma quadra NEUTRA — que nenhuma categoria pediu;
//   3. qualquer livre, inclusive a preferida de outra categoria.
//
// ⚠️ O degrau 3 é o que impede a preferência de virar quadra parada. Segurar a central vazia
// esperando a final da 1ª chegar atrasaria o torneio inteiro por um jogo — e atraso na grade
// é o que faz o domingo virar a noite. Preferência aqui quer dizer "quem chega primeiro na
// fila da quadra boa", não "esta quadra é só sua".
//
// ⚠️ O degrau 2 é o que faz a preferência valer alguma coisa. Sem ele, a categoria sem
// preferência pegaria a primeira livre — que é justamente a central — e a quadra reservada
// estaria ocupada toda vez que a categoria dona precisasse dela.
//
// As quadras são identificadas por NOME, e não por Id, porque é assim que a grade inteira
// fala (Partida.NomeQuadra é texto). Quem traduz o cadastro em nomes é quem chama.
public static class PreferenciaDeQuadra
{
    private static readonly string[] Nenhuma = Array.Empty<string>();

    // As quadras preferidas por uma categoria. Categoria sem escolha devolve lista vazia, que
    // é o caminho normal: a maioria dos torneios não usa isto.
    public static IReadOnlyList<string> Da(IReadOnlyDictionary<int, string[]>? porCategoria, int categoriaId) =>
        porCategoria != null && porCategoria.TryGetValue(categoriaId, out var quadras) ? quadras : Nenhuma;

    // Toda quadra que ALGUMA categoria pediu. É o que separa a quadra neutra (degrau 2) da
    // quadra que tem dono (degrau 3).
    public static IReadOnlyCollection<string> ComDono(IReadOnlyDictionary<int, string[]>? porCategoria)
    {
        if (porCategoria == null || porCategoria.Count == 0) return Array.Empty<string>();

        var todas = new HashSet<string>();
        foreach (var quadras in porCategoria.Values)
            foreach (var quadra in quadras) todas.Add(quadra);

        return todas;
    }

    // A quadra deste jogo, entre as que estão livres no horário. Null = não sobrou nenhuma —
    // e aí o jogo fica sem nome de quadra, exatamente como já ficava antes desta regra existir.
    public static string? Escolher(IEnumerable<string> livres,
        IReadOnlyList<string> daCategoria, IReadOnlyCollection<string> comDono)
    {
        string? neutra = null;
        string? qualquer = null;

        foreach (var quadra in livres)
        {
            if (daCategoria.Contains(quadra)) return quadra;
            if (neutra == null && !comDono.Contains(quadra)) neutra = quadra;
            qualquer ??= quadra;
        }

        return neutra ?? qualquer;
    }

    // Este jogo só teria onde jogar TOMANDO a quadra preferida de outra categoria?
    //
    // É a pergunta que o encaixe faz antes de furar a ordem da fila: se a resposta for sim e
    // houver, mais atrás, um jogo da categoria DONA daquela quadra, é ele que entra na vaga.
    // Sem isso a preferência valeria por sorte — num torneio em que todo horário lota, a
    // central seria ocupada pelo primeiro da fila em quase toda rodada, e a categoria dona só
    // a pegaria quando calhasse de estar na frente.
    public static bool TomariaQuadraDeOutro(string? escolhida,
        IReadOnlyList<string> daCategoria, IReadOnlyCollection<string> comDono) =>
        escolhida != null && comDono.Contains(escolhida) && !daCategoria.Contains(escolhida);

    // ── O que os formulários mandam ───────────────────────────────────────────────────────
    // Uma caixinha por par categoria×quadra, com valor "categoria:quadra". Um campo repetido
    // (e não um dicionário indexado) porque é o que o navegador manda de graça a partir de
    // caixas de seleção, e o que o binder do ASP.NET entrega como `string[]` sem ajuda.
    //
    // ⚠️ A segunda metade é a POSIÇÃO da quadra no formulário, não o Id dela. Na criação a
    // quadra ainda não existe (nasce junto com o torneio, no mesmo POST), e na edição ela pode
    // estar nascendo agora — o organizador que sobe de 3 pra 5 quadras marca a preferência da
    // quinta antes de ela ter Id. Quem traduz posição em Id é quem acabou de criá-las.
    //
    // Par repetido é descartado: a chave da tabela é o par, e o mesmo valor duas vezes num
    // POST montado à mão estouraria a gravação inteira.
    public static List<(int Categoria, int Quadra)> Ler(IEnumerable<string>? marcadas)
    {
        var pares = new List<(int, int)>();
        if (marcadas == null) return pares;

        var jaVistos = new HashSet<(int, int)>();

        foreach (var marcada in marcadas)
        {
            var partes = marcada?.Split(':');
            if (partes is not { Length: 2 }) continue;
            if (!int.TryParse(partes[0], out var categoria) || !int.TryParse(partes[1], out var quadra)) continue;
            if (categoria <= 0 || quadra < 0) continue;
            if (!jaVistos.Add((categoria, quadra))) continue;

            pares.Add((categoria, quadra));
        }

        return pares;
    }
}
