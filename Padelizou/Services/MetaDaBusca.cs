namespace Padelizou.Services;

// O que os buscadores leem de cada tela: a frase que aparece embaixo do link no Google e quais
// telas ficam FORA do índice.
//
// O caso que motivou isto: procurar "padelizou" no Google trazia, como descrição do site, o
// texto raspado da própria home — "…acompanhe sua evolução. Criar Conta Grátis Já tenho conta" —,
// que é o rótulo dos BOTÕES. Sem <meta name="description"> o buscador escreve a frase sozinho,
// e ele escreve lendo a tela. Junto disso, /Auth/Login e /Auth/Cadastro ocupavam dois dos
// quatro primeiros resultados da própria marca.
//
// Mora num método só, e não numa linha em cada view, pelo mesmo motivo do NavegacaoDeVolta:
// são ~30 controllers, e a tela que ficasse sem a linha não daria erro nenhum — voltaria a
// raspar botão, calada.
public static class MetaDaBusca
{
    // Sai na home e em qualquer tela sem frase própria. Cabe em ~155 caracteres de propósito:
    // o Google corta o excedente, e frase cortada no meio lê pior que frase curta.
    public const string DescricaoDoSite =
        "Torneios de padel, ranking, aulas e jogos num lugar só. Inscreva-se em torneios, encontre parceiros e acompanhe sua evolução no ranking.";

    // ALLOWLIST, e não uma lista do que excluir: quase todo o site é tela de gente logada
    // (painel, pagamento, notificação), e o que vale numa busca é um punhado de telas.
    // Numa denylist a tela privada nova nasceria INDEXÁVEL e ninguém perceberia; aqui a tela
    // nova nasce fora do índice, que é o erro barato — ela some da busca, não vaza nada.
    private static readonly HashSet<string> NoIndice = new(StringComparer.OrdinalIgnoreCase)
    {
        "Home/Index", "Home/Privacy", "Home/Termos",
        "Torneios/Index", "Torneios/Details", "Torneios/Jogos", "Torneios/Cidade",
        // ⚠️ "Jogadores", com E. O ARQUIVO é `Controllers/JogadorsController.cs` (sem E, sobra
        // do scaffold), mas quem manda na rota é o nome da CLASSE — `JogadoresController`.
        // Escrito "Jogadors" aqui, o ranking caiu na regra do desconhecido e foi pro ar com
        // `noindex`: eu estava pedindo ao Google pra tirar da busca uma das melhores páginas do
        // site, e nada na tela mostrava isso. O teste de reflexão abaixo existe por causa disso.
        "Jogadores/Ranking", "Jogadores/Perfil",
        "Professores/Index", "Professores/Perfil",
    };

    // Os nomes acima existem de verdade? Estas listas são texto solto: um erro de digitação
    // não quebra build nem teste — na allowlist ele manda a página pro `noindex`, e aqui no
    // dicionário ele faz a seção cair na frase genérica. Os dois falham calados, e o sintoma
    // aparece semanas depois no Google, longe de qualquer log.
    //
    // Junta os DOIS de propósito: quando só a allowlist era conferida, o mesmo erro de grafia
    // sobreviveu no dicionário de descrições e o ranking foi pro ar com a frase de qualquer
    // página. Um teste de reflexão valida tudo isto contra os controllers de verdade.
    public static IReadOnlyCollection<string> TodasAsTelas =>
        NoIndice.Concat(PorTela.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    // Frase por SEÇÃO, não por tela: o que muda entre dois torneios é o nome, e isso a própria
    // view passa em ViewData["Descricao"]. Aqui fica o que vale pra seção inteira.
    private static readonly Dictionary<string, string> PorTela = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Torneios/Index"] =
            "Todos os torneios de padel abertos para inscrição: data, local, categorias e valor. Inscreva-se pelo site e acompanhe as chaves ao vivo.",
        ["Torneios/Details"] =
            "Chaves, jogos, classificação e inscrição deste torneio de padel, ao vivo no Padelizou.",
        ["Torneios/Jogos"] =
            "Jogos e resultados deste torneio de padel, atualizados durante a competição.",
        ["Jogadores/Ranking"] =
            "O ranking de padel do Padelizou: pontuação por torneio, filtros por categoria, clube e cidade, e a evolução de cada jogador.",
        ["Jogadores/Perfil"] =
            "Perfil do jogador no Padelizou: partidas, torneios disputados, títulos e posição no ranking.",
        ["Professores/Index"] =
            "Professores de padel disponíveis para aula: cidade, valor, horários livres e avaliação de quem já treinou.",
        ["Professores/Perfil"] =
            "Aulas de padel com este professor: horários livres, valores e agendamento pelo site.",
        ["Home/Privacy"] = "Como o Padelizou trata os seus dados.",
        ["Home/Termos"] = "Os termos de uso do Padelizou.",
    };

    private static string Chave(string? controller, string? action) => $"{controller}/{action}";

    // O site atende em DOIS endereços: padelizou.com.br e www.padelizou.com.br servem tudo,
    // idêntico, e o www não redireciona. Pro buscador são dois sites com o mesmo conteúdo, e a
    // força de cada página se divide entre eles.
    //
    // ⚠️ Isto REDUZ o dano, não conserta: a correção é um 301 do www pra raiz, e ela mora no
    // Caddy (que não está neste repo). Sem esta linha o canônico era pior que inútil — servido
    // pelo www, ele apontava pro PRÓPRIO www, confirmando os dois endereços como legítimos.
    //
    // Só o www é reescrito. localhost e dev continuam apontando pra si mesmos: canônico de
    // desenvolvimento apontando pra produção mandaria o buscador indexar a página errada — e é
    // o tipo de coisa que ninguém percebe olhando a tela.
    public static string HostCanonico(string host)
    {
        const string publico = Middleware.AdminHostMiddleware.SitePublicoHost;
        return host.Equals($"www.{publico}", StringComparison.OrdinalIgnoreCase) ? publico : host;
    }

    // "follow" e não "nofollow": a tela de login não deve APARECER na busca, mas os links do
    // rodapé que saem dela continuam valendo. Um nofollow aqui cortaria caminho pro que
    // interessa indexar.
    public static bool ForaDoIndice(string? controller, string? action)
        => !NoIndice.Contains(Chave(controller, action));

    public static string Descricao(string? controller, string? action)
        => PorTela.TryGetValue(Chave(controller, action), out var frase) ? frase : DescricaoDoSite;

    // Corta no ESPAÇO anterior ao limite e fecha com reticências: o Google trunca por conta
    // dele, mas aí a frase acaba no meio de uma palavra. Torneio com nome comprido e um resumo
    // colado do organizador chegam fácil nesse limite.
    public static string Encurtar(string? texto, int limite = 160)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;

        // Quebra de linha e espaço duplo viram um espaço só: o que vem do banco (resumo do
        // torneio) é texto de <textarea>, e o \n cru sairia dentro do atributo da meta.
        var limpo = string.Join(' ', texto.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (limpo.Length <= limite) return limpo;

        var corte = limpo.LastIndexOf(' ', limite - 1);
        if (corte <= 0) corte = limite - 1;
        return limpo[..corte].TrimEnd(',', '.', ';', ' ') + "…";
    }
}
