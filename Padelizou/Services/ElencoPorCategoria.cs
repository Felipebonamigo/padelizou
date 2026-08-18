namespace Padelizou.Services;

// O elenco do time repartido por categoria — a "escadinha" que todo mundo do padel lê de
// cima pra baixo: quem é da Open, quem é da 3ª, quem é da 6ª.
//
// A lista corrida por pontos respondia "quem é o melhor do time". Não respondia a pergunta
// que se faz montando dupla pra um interno: "quem aqui joga a minha categoria?".
//
// ⚠️ CADA PESSOA APARECE UMA VEZ SÓ, no degrau mais forte que ela aceita (Felipe, 18/08/2026).
// `JogadorCategoria` é a lista de categorias que ela ACEITA jogar, e quem marca cinco aparecia
// cinco vezes: numa lista de doze linhas, metade era o mesmo nome, e a tela lia como bug.
// Quem procura parceiro na 4ª continua enxergando quem também joga a 4ª — as outras
// categorias vão como etiqueta na linha da pessoa (ver Views/Times/Detalhes).
//
// Genérico no tipo do membro pra continuar sendo função pura: ela não conhece ViewModel, não
// toca no banco, e por isso o teste dela cabe numa lista na memória.
public static class ElencoPorCategoria
{
    // Quem não marcou categoria nenhuma. Não é erro: o cadastro trata "sem linha" como "aceita
    // qualquer categoria", e é o estado da maioria de quem nunca abriu as preferências.
    public const string SemCategoria = "Sem categoria informada";

    public sealed record Grupo<T>(string Categoria, string Curto, List<T> Membros)
    {
        public bool EhSemCategoria => Categoria == SemCategoria;
    }

    // `sexoDe` é opcional e não é preciosismo: existe gente com categoria MASCULINA e FEMININA
    // marcadas ao mesmo tempo (engano no cadastro de preferências, e o site aceita). Sem o
    // sexo, "a mais forte" das duas escadas é sempre a masculina — e uma jogadora ia parar
    // sozinha na "5ª Masculina". Sabendo o sexo, ela cai na escada dela.
    public static List<Grupo<T>> Agrupar<T>(
        IEnumerable<T> membros,
        Func<T, IReadOnlyCollection<string>> categoriasDe,
        Func<T, string?>? sexoDe = null)
    {
        var grupos = new Dictionary<string, List<T>>();

        foreach (var membro in membros)
        {
            var nome = OndeMostrar(categoriasDe(membro), sexoDe?.Invoke(membro));

            if (!grupos.TryGetValue(nome, out var lista))
                grupos[nome] = lista = new List<T>();

            lista.Add(membro);
        }

        return grupos
            // A MESMA ordem das outras telas (masculinas da mais forte pra mais fraca, depois
            // femininas, depois mista e casais), com o balde do "sem categoria" no fim: ele é
            // o que menos ajuda quem procura parceiro.
            .OrderBy(g => g.Key == SemCategoria)
            .ThenBy(g => CategoriaNaTela.Ordem(g.Key))
            // ⚠️ O balde escapa do `Curto`: ele tira a palavra "Categoria" de qualquer lugar
            // do texto, e "Sem categoria informada" viraria "Sem informada".
            .Select(g => new Grupo<T>(
                g.Key,
                g.Key == SemCategoria ? SemCategoria : CategoriaNaTela.Curto(g.Key),
                g.Value))
            .ToList();
    }

    // Em qual grupo a pessoa aparece. Público porque a tela precisa da MESMA resposta pra
    // saber quais categorias sobraram pra virar etiqueta na linha dela.
    public static string OndeMostrar(IReadOnlyCollection<string>? categorias, string? sexo = null)
    {
        var nomes = (categorias ?? Array.Empty<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct()
            .ToList();

        // Sem categoria nenhuma — ou só nome em branco vindo do catálogo — cai no balde em vez
        // de sumir do elenco. Membro do time não pode não aparecer.
        if (nomes.Count == 0) return SemCategoria;

        // A escada do próprio sexo ganha da outra, mesmo que a outra tenha degrau "mais alto".
        // Mista e Casais não são de sexo nenhum: ficam de fora deste filtro e só entram quando
        // a pessoa não tem nenhuma categoria da escada dela.
        // ⚠️ Só filtra com sexo CONHECIDO. Sem essa guarda, quem não informou casaria com o
        // `null` de Mista/Casais — e uma pessoa com "Mista A" e "3ª Masculina" apareceria na
        // Mista, que é o degrau menos informativo dos dois.
        var daEscadaDela = SexoDoJogador.Existe(sexo)
            ? nomes.Where(n => CategoriaNaTela.SexoDaEscada(n) == sexo).ToList()
            : new List<string>();
        var candidatos = daEscadaDela.Count > 0 ? daEscadaDela : nomes;

        // "Mais forte" é o primeiro da ordem que o resto do site já usa: Open no topo, depois
        // 2ª, 3ª... — ver CategoriaNaTela.Ordem.
        return candidatos.OrderBy(CategoriaNaTela.Ordem).First();
    }
}
