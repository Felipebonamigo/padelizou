namespace Padelizou.Services;

// O elenco do time repartido por categoria — a "escadinha" que todo mundo do padel lê de
// cima pra baixo: quem é da Open, quem é da 3ª, quem é da 6ª.
//
// A lista corrida por pontos respondia "quem é o melhor do time". Não respondia a pergunta
// que se faz montando dupla pra um interno: "quem aqui joga a minha categoria?".
//
// ⚠️ UMA PESSOA APARECE EM VÁRIOS GRUPOS, de propósito. `JogadorCategoria` é a lista de
// categorias que ela ACEITA jogar (várias, quase sempre), e não um nível único. Mostrá-la só
// na "mais forte" esconderia dela justamente quem procura parceiro na outra — e é para isso
// que a tela existe.
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

    public static List<Grupo<T>> Agrupar<T>(
        IEnumerable<T> membros,
        Func<T, IReadOnlyCollection<string>> categoriasDe)
    {
        var grupos = new Dictionary<string, List<T>>();

        foreach (var membro in membros)
        {
            var categorias = categoriasDe(membro);
            var nomes = categorias.Count == 0
                ? new[] { SemCategoria }
                : categorias.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToArray();

            // Sobrou vazio depois da limpeza (nome em branco no catálogo): cai no balde do
            // "sem categoria" em vez de sumir do elenco. Membro do time não pode não aparecer.
            if (nomes.Length == 0) nomes = new[] { SemCategoria };

            foreach (var nome in nomes.Distinct())
            {
                if (!grupos.TryGetValue(nome, out var lista))
                    grupos[nome] = lista = new List<T>();

                lista.Add(membro);
            }
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
}
