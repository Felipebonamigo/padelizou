using Padelizou.Models;

namespace Padelizou.Services;

// Uma cidade do jeito que ela vai pra TELA — carregando junto todas as grafias (ou todas as
// linhas de catálogo) que são ela.
//
// As duas listas existem pelo mesmo motivo, e ele é a parte que não pode ser esquecida: no
// <select> aparece UMA opção, mas quem a consulta precisa achar são TODOS. Mostrar só
// "Gravataí" e continuar filtrando por igualdade exata faria sumir do ranking justamente quem
// digitou "GRAVATAI" — o filtro nasceria mentindo, e o defeito seria mudo (a tela abre, só
// vem gente a menos).
public sealed record CidadeNaLista(
    string Nome,
    string? Estado,
    IReadOnlyList<string> Grafias,
    IReadOnlyList<int> Ids)
{
    // O id que vai no `value=` da opção: o menor, que é a linha mais antiga do catálogo —
    // assim link velho e favorito de alguém continuam apontando pro mesmo lugar.
    public int Id => Ids.Count > 0 ? Ids[0] : 0;

    public string Rotulo => string.IsNullOrEmpty(Estado) ? Nome : $"{Nome} - {Estado}";
}

// "Gravatai", "Gravataí" e "GRAVATAI" apareceram como três opções no filtro do ranking, e
// "Porto Alegre" como três. Cada uma veio de alguém digitando a cidade do seu jeito.
//
// `NomeDeCidade` já sabia responder "isto é a mesma cidade que aquilo?"; o que faltava era
// alguém aplicar isso na hora de MONTAR a lista. É o que este arquivo faz — e faz num lugar
// só, porque as telas com filtro de cidade são seis e a regra duplicada é a origem dos bugs
// graves deste sistema.
public static class CidadesSemRepetir
{
    // ---------- Texto livre (Jogador.Cidade, digitado no perfil) ----------

    public static List<CidadeNaLista> Agrupar(IEnumerable<string?> grafias)
    {
        return grafias
            .Select(NomeDeCidade.Arrumar)
            .Where(g => g.Length > 0)
            .GroupBy(NomeDeCidade.Chave)
            .Select(g => new CidadeNaLista(
                MelhorGrafia(g),
                null,
                g.Distinct(StringComparer.Ordinal).ToList(),
                Array.Empty<int>()))
            .OrderBy(c => NomeDeCidade.Chave(c.Nome), StringComparer.Ordinal)
            .ToList();
    }

    public static List<string> Nomes(IEnumerable<string?> grafias) =>
        Agrupar(grafias).Select(c => c.Nome).ToList();

    // O nome escolhido, trocado pelo que a lista da tela mostra. Link antigo (ou compartilhado
    // no WhatsApp) traz `?cidade=GRAVATAI`; sem isto o chip do filtro apareceria escrito
    // "GRAVATAI" enquanto o select ainda ofereceria "Gravataí" logo ao lado — a mesma cidade
    // duas vezes na mesma linha, que é justamente o que se está consertando.
    public static List<string> Canonizar(IEnumerable<string?> escolhidas, IEnumerable<string> disponiveis)
    {
        var porChave = disponiveis
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .GroupBy(NomeDeCidade.Chave)
            .ToDictionary(g => g.Key, g => g.First());

        return escolhidas
            .Select(NomeDeCidade.Arrumar)
            .Where(nome => nome.Length > 0)
            .Select(nome => porChave.TryGetValue(NomeDeCidade.Chave(nome), out var oficial) ? oficial : nome)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    // Todas as grafias que a consulta tem que aceitar quando a pessoa escolhe uma cidade na
    // tela. Escolher "Gravataí" precisa trazer quem escreveu "GRAVATAI" e "gravatai".
    public static List<string> GrafiasDe(IEnumerable<string?> escolhidas, IEnumerable<string?> conhecidas)
    {
        var porChave = conhecidas
            .Select(NomeDeCidade.Arrumar)
            .Where(g => g.Length > 0)
            .GroupBy(NomeDeCidade.Chave)
            .ToDictionary(g => g.Key, g => g.Distinct(StringComparer.Ordinal).ToList());

        var saida = new List<string>();

        foreach (var escolhida in escolhidas)
        {
            var nome = NomeDeCidade.Arrumar(escolhida);
            if (nome.Length == 0) continue;

            // A escolhida entra SEMPRE, mesmo sem ninguém morando nela: link velho com uma
            // cidade que não existe mais tem que devolver lista vazia, não o Brasil inteiro.
            saida.Add(nome);

            if (porChave.TryGetValue(NomeDeCidade.Chave(nome), out var grafias))
                saida.AddRange(grafias);
        }

        // ⚠️ `Ordinal` e não `OrdinalIgnoreCase`: ignorando a caixa, "GRAVATAI" e "gravatai"
        // colapsariam numa só e a outra sumiria da lista — e sumir da lista de grafias é
        // sumir do resultado, que é exatamente o estrago que este arquivo existe pra evitar.
        return saida.Distinct(StringComparer.Ordinal).ToList();
    }

    // A cidade digitada AGORA, escrita do jeito que o cadastro já a escreve. É o conserto na
    // entrada: sem ele, agrupar na saída só esconde a bagunça, e a lista de grafias da mesma
    // cidade cresce a cada pessoa nova.
    //
    // ⚠️ Nunca rebaixa: quem digita "Gravataí" onde só existia "GRAVATAI" continua com o
    // acento — senão a primeira pessoa a escrever errado decidiria pelas próximas.
    public static string EscritaComoAsOutras(string? digitada, IEnumerable<string?> conhecidas)
    {
        var nome = NomeDeCidade.Arrumar(digitada);
        if (nome.Length == 0) return string.Empty;

        var chave = NomeDeCidade.Chave(nome);
        var mesmas = conhecidas
            .Select(NomeDeCidade.Arrumar)
            .Where(c => c.Length > 0 && NomeDeCidade.Chave(c) == chave);

        return MelhorGrafia(mesmas.Append(nome));
    }

    // ---------- Catálogo (tabela Cidades, usada por professor/clube/panelinha) ----------

    public static List<CidadeNaLista> Agrupar(IEnumerable<Cidade> cidades)
    {
        var saida = new List<CidadeNaLista>();

        foreach (var mesmoNome in cidades
                     .Where(c => !string.IsNullOrWhiteSpace(c.Nome))
                     .GroupBy(c => NomeDeCidade.Chave(c.Nome)))
        {
            // Homônimas de estados DIFERENTES são cidades diferentes de verdade (São Francisco
            // existe em meia dúzia de UFs). Mas linha sem UF é linha incompleta da mesma
            // cidade, não outra cidade — só separamos quando os estados realmente discordam.
            var ufs = mesmoNome
                .Select(c => NomeDeCidade.ArrumarEstado(c.Estado))
                .Where(uf => uf != null)
                .Distinct()
                .ToList();

            if (ufs.Count <= 1)
            {
                saida.Add(Montar(mesmoNome, ufs.FirstOrDefault()));
                continue;
            }

            foreach (var porUf in mesmoNome.GroupBy(c => NomeDeCidade.ArrumarEstado(c.Estado)))
                saida.Add(Montar(porUf, porUf.Key));
        }

        // ⚠️ Ordena pela CHAVE, não pelo nome: "Sao Francisco" e "São Francisco" são duas
        // entradas (UFs diferentes) e precisam sair lado a lado; ordenar pelo texto jogaria
        // uma pro meio da lista por causa do acento. De quebra, a ordem para de depender da
        // cultura da máquina — o CI e o servidor não rodam com a mesma.
        return saida
            .OrderBy(c => NomeDeCidade.Chave(c.Nome), StringComparer.Ordinal)
            .ThenBy(c => c.Estado ?? "", StringComparer.Ordinal)
            .ToList();
    }

    private static CidadeNaLista Montar(IEnumerable<Cidade> linhas, string? uf)
    {
        var lista = linhas.ToList();

        return new CidadeNaLista(
            MelhorGrafia(lista.Select(c => c.Nome)),
            uf,
            lista.Select(c => NomeDeCidade.Arrumar(c.Nome)).Distinct(StringComparer.Ordinal).ToList(),
            lista.Select(c => c.Id).OrderBy(id => id).ToList());
    }

    // A escolha da tela chega como UM id. Se o catálogo tem duas linhas pra mesma cidade,
    // filtrar por um id só mostra metade: o professor cadastrado na OUTRA linha some da
    // vitrine sem nenhum aviso.
    public static List<int> IdsDaMesma(int cidadeId, IEnumerable<Cidade> catalogo)
    {
        var grupo = Agrupar(catalogo).FirstOrDefault(c => c.Ids.Contains(cidadeId));
        return grupo?.Ids.ToList() ?? new List<int> { cidadeId };
    }

    // ---------- Qual grafia vai pra tela ----------

    // Entre "GRAVATAI", "gravatai" e "Gravataí", quem aparece é a última: quem escreveu certo
    // escreveu por todo mundo. A ordem das perguntas é essa de propósito — caixa primeiro,
    // porque "GRAVATAÍ" tem o acento e ainda assim é a pior das três pra ler numa lista.
    public static string MelhorGrafia(IEnumerable<string> grafias)
    {
        var candidatas = grafias
            .Select(NomeDeCidade.Arrumar)
            .Where(g => g.Length > 0)
            .GroupBy(g => g, StringComparer.Ordinal)
            .ToList();

        if (candidatas.Count == 0) return string.Empty;

        return candidatas
            .OrderByDescending(g => EscritaDeGente(g.Key))          // "Gravataí" antes de "GRAVATAI"/"gravatai"
            .ThenByDescending(g => Acentos(g.Key))                  // "Gravataí" antes de "Gravatai"
            .ThenByDescending(g => g.Count())                       // empate: a que mais gente escreveu
            .ThenBy(g => g.Key, StringComparer.Ordinal)             // e, por fim, algo estável
            .First().Key;
    }

    // Tem maiúscula E minúscula — ou seja, não é grito nem pressa.
    private static bool EscritaDeGente(string nome) =>
        nome.Any(char.IsUpper) && nome.Any(char.IsLower);

    private static int Acentos(string nome) => nome.Count(c => c > 127);
}
