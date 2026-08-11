namespace Padelizou.Services;

// Uma pessoa reduzida ao que a conta por região precisa: onde ela disse que mora e quando
// entrou. Vem assim do banco pra a tela não arrastar a linha inteira do jogador.
//
// `CriadoEm` nulo é conta anterior a 25/07/2026, quando a coluna ainda não existia.
public sealed record PessoaNaRegiao(string? Cidade, string? Estado, DateTime? CriadoEm);

// Uma região já montada, pronta pra virar linha de tabela.
public sealed record GrupoDeRegiao(
    string Rotulo,
    string Chave,
    bool NaoInformada,
    IReadOnlyList<DateTime?> Entradas)
{
    public int Total => Entradas.Count;

    // Quem entrou antes de 25/07/2026 conta no TOTAL e não cabe em faixa nenhuma — a data
    // dessa pessoa não existe. Sem esta contagem à mão, a tela mostraria uma cidade com 30
    // jogadores e nenhum cadastro na série, e pareceria defeito.
    public int SemData => Entradas.Count(d => d == null);

    public int Entre(DateTime inicio, DateTime fim) =>
        Entradas.Count(d => d != null && d >= inicio && d < fim);
}

// De onde vem a gente que se cadastrou — por estado e por cidade.
//
// Os dois campos são TEXTO LIVRE, digitado por cada pessoa no seu perfil, e a base mostra o
// que isso vira: "RS", "Rs", "rs" e "Rio Grande do Sul (RS)" pro estado; "Gravataí",
// "Gravatai" e "GRAVATAI" pra cidade. Contar por igualdade exata partiria o Rio Grande do Sul
// em quatro linhas e Gravataí em três — e o pior é que o defeito seria MUDO: a tela abre, os
// números só ficam menores. Por isso nada aqui compara texto na lata: estado passa por
// `UnidadeFederativa` e cidade por `NomeDeCidade`, os mesmos tradutores que o ranking e a
// busca já usam.
//
// ⚠️ A regra que só existe aqui é a terceira: **quem não escreveu a UF, mas escreveu a
// cidade, herda a UF que os OUTROS moradores daquela cidade declararam** — e só quando eles
// não se contradizem. Em 10/08/2026 eram 44 contas de 172 sem estado nenhum; sem isto, um
// quarto da base cairia em "não informado" e a tela por estado não serviria pra decidir nada.
// Homônima de estados diferentes (São Francisco existe em meia dúzia de UFs) não é herdada:
// duas UFs discordando é "não sei", e "não sei" fica como está.
//
// E "não informado" é uma LINHA, com nome e número. Some-lo do total seria a tela mentindo
// pra si mesma sobre o tamanho da própria base.
public static class JogadoresPorRegiao
{
    public const string PorEstado = "estado";
    public const string PorCidade = "cidade";

    public const string ChaveSemRegiao = "sem-regiao";
    public const string RotuloSemRegiao = "Não informado";

    // Qualquer coisa fora das duas cai em estado — é a visão mais grossa, e a que responde
    // "onde o Padelizou existe?". Link velho ou endereço digitado à mão não vira erro numa
    // tela de leitura (mesma escolha do `FaixasDeMetricas.Normalizar`).
    public static string Normalizar(string? por) =>
        (por ?? "").Trim().ToLowerInvariant() == PorCidade ? PorCidade : PorEstado;

    public static List<GrupoDeRegiao> Agrupar(string? por, IEnumerable<PessoaNaRegiao> pessoas) =>
        Normalizar(por) == PorCidade ? PorCidades(pessoas) : PorEstados(pessoas);

    // ---------- A UF que a cidade denuncia ----------

    // Pra cada cidade, a UF que os moradores dela declararam — desde que todos digam a mesma.
    // Duas UFs discordando devolve nada: é homônima de verdade, ou alguém errou o campo, e
    // nos dois casos chutar seria pior do que "não sei".
    public static Dictionary<string, string> UfsPelaCidade(IEnumerable<PessoaNaRegiao> pessoas)
    {
        return pessoas
            .Select(p => (Cidade: NomeDeCidade.Chave(p.Cidade), Uf: UnidadeFederativa.Normalizar(p.Estado)))
            .Where(x => x.Cidade.Length > 0 && x.Uf != null)
            .GroupBy(x => x.Cidade, StringComparer.Ordinal)
            .Where(g => g.Select(x => x.Uf).Distinct(StringComparer.Ordinal).Count() == 1)
            .ToDictionary(g => g.Key, g => g.First().Uf!, StringComparer.Ordinal);
    }

    // A UF de uma pessoa: a que ela escreveu; e, quando ela não escreveu nenhuma, a da cidade
    // dela. `null` continua significando "não sei", nunca "nenhum estado".
    public static string? UfDe(PessoaNaRegiao pessoa, IReadOnlyDictionary<string, string> ufsPelaCidade)
    {
        var declarada = UnidadeFederativa.Normalizar(pessoa.Estado);
        if (declarada != null) return declarada;

        var cidade = NomeDeCidade.Chave(pessoa.Cidade);
        return cidade.Length > 0 && ufsPelaCidade.TryGetValue(cidade, out var herdada) ? herdada : null;
    }

    // Quantas pessoas estão numa UF que elas mesmas não escreveram. Vai pro rodapé da tela:
    // um número deduzido tem que se apresentar como deduzido.
    public static int QuantasHerdaramAUfDaCidade(IEnumerable<PessoaNaRegiao> pessoas)
    {
        var lista = pessoas as IList<PessoaNaRegiao> ?? pessoas.ToList();
        var ufs = UfsPelaCidade(lista);

        return lista.Count(p => UnidadeFederativa.Normalizar(p.Estado) == null && UfDe(p, ufs) != null);
    }

    // ---------- Por estado ----------

    private static List<GrupoDeRegiao> PorEstados(IEnumerable<PessoaNaRegiao> pessoas)
    {
        var lista = pessoas as IList<PessoaNaRegiao> ?? pessoas.ToList();
        var ufs = UfsPelaCidade(lista);

        var grupos = lista
            .GroupBy(p => UfDe(p, ufs), StringComparer.Ordinal)
            .Select(g => g.Key == null
                ? SemRegiao(g)
                : new GrupoDeRegiao(g.Key, g.Key.ToLowerInvariant(), false, Datas(g)))
            .ToList();

        return Ordenar(grupos);
    }

    // ---------- Por cidade ----------

    private static List<GrupoDeRegiao> PorCidades(IEnumerable<PessoaNaRegiao> pessoas)
    {
        var lista = pessoas as IList<PessoaNaRegiao> ?? pessoas.ToList();
        var ufs = UfsPelaCidade(lista);

        // A UF entra na chave do grupo pra separar homônimas de estados diferentes — a mesma
        // regra que `CidadesSemRepetir.Agrupar` aplica no catálogo. Aqui ela sai de graça:
        // depois da herança acima, quem mora na mesma cidade já está na mesma UF, então só
        // sobra separado o que discorda de verdade.
        //
        // Cidade em branco cai numa linha só, mesmo com UFs diferentes entre essas pessoas:
        // sem cidade não é região nenhuma, é campo vazio.
        var grupos = lista
            .GroupBy(p => Slug(NomeDeCidade.Chave(p.Cidade), UfDe(p, ufs)), StringComparer.Ordinal)
            .Select(g => g.Key.Length == 0
                ? SemRegiao(g)
                : new GrupoDeRegiao(RotuloDaCidade(g, UfDe(g.First(), ufs)), g.Key, false, Datas(g)))
            .ToList();

        return Ordenar(grupos);
    }

    // A grafia que vai pra tela é a MELHOR do grupo — entre "GRAVATAI", "gravatai" e
    // "Gravataí", quem escreveu certo escreve por todo mundo (a escolha mora em
    // `CidadesSemRepetir`, e não é repetida aqui).
    private static string RotuloDaCidade(IEnumerable<PessoaNaRegiao> pessoas, string? uf)
    {
        var nome = CidadesSemRepetir.MelhorGrafia(pessoas.Select(p => p.Cidade ?? ""));
        return uf == null ? nome : $"{nome} - {uf}";
    }

    // ---------- Pedaços comuns ----------

    private static GrupoDeRegiao SemRegiao(IEnumerable<PessoaNaRegiao> pessoas) =>
        new(RotuloSemRegiao, ChaveSemRegiao, true, Datas(pessoas));

    private static List<DateTime?> Datas(IEnumerable<PessoaNaRegiao> pessoas) =>
        pessoas.Select(p => p.CriadoEm).ToList();

    // O que vai no `?regiao=` da URL. Sai da chave de comparação (sem acento, minúscula), que
    // é justamente o que não muda quando a próxima pessoa digita a cidade de outro jeito — o
    // link que o Felipe guardar continua caindo na mesma cidade.
    //
    // Chave vazia (ninguém escreveu cidade) devolve vazio, e é isso que manda a pessoa pro
    // "não informado" lá em cima.
    private static string Slug(string chaveDaCidade, string? uf)
    {
        if (chaveDaCidade.Length == 0) return "";

        var nome = chaveDaCidade.Replace(' ', '-');
        return uf == null ? nome : $"{nome}-{uf.ToLowerInvariant()}";
    }

    // Maior primeiro — a tela existe pra responder "onde estão os jogadores?". "Não informado"
    // vai pro fim de propósito, por maior que seja: ele é o buraco da medição, não uma região.
    private static List<GrupoDeRegiao> Ordenar(IEnumerable<GrupoDeRegiao> grupos) =>
        grupos
            .OrderBy(g => g.NaoInformada)
            .ThenByDescending(g => g.Total)
            .ThenBy(g => g.Chave, StringComparer.Ordinal)
            .ToList();
}
